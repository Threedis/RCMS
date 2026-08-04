using Inventory.BLL.Interfaces;
using Inventory.Common.Constants;
using Inventory.Common.Enums;
using Inventory.Common.Extensions;
using Inventory.Common.Models;
using Inventory.Common.Security;
using Inventory.Entities.Dtos;
using Inventory.Repository.UnitOfWork;
using Inventory.Services.Export;
using Microsoft.Extensions.Logging;

namespace Inventory.BLL.Services;

/// <summary>
/// Material Issue business service.
/// <para>
/// The critical rule — stock can never go negative — is enforced twice: once
/// here against the available balance so the user gets an immediate, readable
/// message, and once inside <c>sp_ApproveInventoryIssue</c> under a row lock so
/// two concurrent approvals cannot both pass the check. The database is the
/// authority; this layer exists to give a good error rather than a constraint
/// violation.
/// </para>
/// </summary>
public sealed class IssueService : IIssueService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUser _currentUser;
    private readonly IAuditService _audit;
    private readonly INotificationService _notifications;
    private readonly IExportService _export;
    private readonly ILogger<IssueService> _logger;

    public IssueService(
        IUnitOfWork unitOfWork,
        ICurrentUser currentUser,
        IAuditService audit,
        INotificationService notifications,
        IExportService export,
        ILogger<IssueService> logger)
    {
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _currentUser = currentUser ?? throw new ArgumentNullException(nameof(currentUser));
        _audit = audit ?? throw new ArgumentNullException(nameof(audit));
        _notifications = notifications ?? throw new ArgumentNullException(nameof(notifications));
        _export = export ?? throw new ArgumentNullException(nameof(export));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public Task<PagedResult<IssueListDto>> GetPagedAsync(
        PagedRequest request,
        ReportFilter filter,
        CancellationToken cancellationToken = default)
        => _unitOfWork.Inventory.GetPagedIssuesAsync(request, filter, cancellationToken);

    /// <inheritdoc />
    public async Task<IssueDto?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var issue = await _unitOfWork.Inventory.GetIssueAsync(id, cancellationToken).ConfigureAwait(false);

        if (issue is null)
        {
            return null;
        }

        issue.Attachments = (await _unitOfWork.Attachments
                .GetForDocumentAsync(DocumentType.MaterialIssue, id, cancellationToken)
                .ConfigureAwait(false))
            .ToList();

        // Refresh the available balance so the approver sees today's position,
        // not the position when the request was raised.
        foreach (var line in issue.Details)
        {
            var balance = await _unitOfWork.Inventory
                .GetItemBalanceAsync(line.ItemId, issue.WarehouseId, cancellationToken)
                .ConfigureAwait(false);

            line.AvailableStock = balance?.AvailableStock ?? 0m;
        }

        return issue;
    }

    /// <inheritdoc />
    public async Task<ServiceResult<int>> SaveAsync(IssueDto dto, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        var isUpdate = dto.Id > 0;

        if (isUpdate)
        {
            var existing = await _unitOfWork.Inventory.GetIssueAsync(dto.Id, cancellationToken).ConfigureAwait(false);

            if (existing is null)
            {
                return ServiceResult<int>.Failure("The material issue was not found.", -404);
            }

            if (!existing.CanEdit)
            {
                return ServiceResult<int>.Failure(
                    $"A material issue in '{existing.Status.ToDisplayName()}' status cannot be edited.", -409);
            }
        }

        var validation = await ValidateAsync(dto, cancellationToken).ConfigureAwait(false);

        if (!validation.Succeeded)
        {
            return ServiceResult<int>.From(validation);
        }

        NormaliseLines(dto);

        var result = await _unitOfWork.Inventory
            .SaveIssueAsync(dto, _currentUser.UserId, isUpdate, cancellationToken)
            .ConfigureAwait(false);

        if (!result.IsSuccess)
        {
            return ServiceResult<int>.Failure(result.Message, result.ReturnCode);
        }

        var issueId = isUpdate ? dto.Id : result.NewId ?? 0;
        var number = result.GeneratedNumber ?? dto.IssueNumber;

        await _audit.LogAsync(
            isUpdate ? AuditAction.Update : AuditAction.Create,
            "Issue",
            issueId.ToString(),
            $"{(isUpdate ? "Updated" : "Created")} material issue {number} with {dto.Details.Count} line(s).",
            newValues: Serialize(dto),
            cancellationToken: cancellationToken).ConfigureAwait(false);

        return ServiceResult<int>.Success(issueId, $"Material issue {number} saved successfully.");
    }

    /// <inheritdoc />
    public async Task<ServiceResult> DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        var existing = await _unitOfWork.Inventory.GetIssueAsync(id, cancellationToken).ConfigureAwait(false);

        if (existing is null)
        {
            return ServiceResult.Failure("The material issue was not found.", -404);
        }

        if (existing.Status != DocumentStatus.Draft)
        {
            return ServiceResult.Failure("Only a draft material issue can be deleted.", -409);
        }

        var result = await _unitOfWork.Inventory
            .DeleteIssueAsync(id, _currentUser.UserId, cancellationToken)
            .ConfigureAwait(false);

        if (!result.IsSuccess)
        {
            return ServiceResult.Failure(result.Message, result.ReturnCode);
        }

        await _audit.LogAsync(
            AuditAction.Delete, "Issue", id.ToString(),
            $"Deleted draft material issue {existing.IssueNumber}.",
            cancellationToken: cancellationToken).ConfigureAwait(false);

        return ServiceResult.Success("Material issue deleted successfully.");
    }

    /// <inheritdoc />
    public async Task<ServiceResult> SubmitAsync(int id, string? remarks, CancellationToken cancellationToken = default)
    {
        var existing = await GetByIdAsync(id, cancellationToken).ConfigureAwait(false);

        if (existing is null)
        {
            return ServiceResult.Failure("The material issue was not found.", -404);
        }

        if (!existing.CanSubmit)
        {
            return ServiceResult.Failure(
                $"A material issue in '{existing.Status.ToDisplayName()}' status cannot be submitted.", -409);
        }

        if (existing.Details.Count == 0)
        {
            return ServiceResult.Failure("Add at least one item line before submitting.", -400);
        }

        // Warn early: the procedure re-checks under a lock, but a clear message
        // here saves the approver a wasted round trip.
        var shortages = existing.Details
            .Where(d => d.QuantityRequested > d.AvailableStock)
            .Select(d => $"{d.ItemName}: requested {d.QuantityRequested:N3}, available {d.AvailableStock:N3}")
            .ToList();

        if (shortages.Count > 0)
        {
            return ServiceResult.ValidationFailure(
                shortages.Prepend("The request exceeds the available stock:"));
        }

        var result = await _unitOfWork.Inventory
            .SubmitIssueAsync(id, _currentUser.UserId, remarks, cancellationToken)
            .ConfigureAwait(false);

        if (!result.IsSuccess)
        {
            return ServiceResult.Failure(result.Message, result.ReturnCode);
        }

        await _audit.LogAsync(
            AuditAction.Submit, "Issue", id.ToString(),
            $"Submitted material issue {existing.IssueNumber} for approval.",
            cancellationToken: cancellationToken).ConfigureAwait(false);

        await _notifications.NotifyAsync(
            NotificationType.PendingApproval,
            "Material issue awaiting approval",
            $"{existing.IssueNumber} for {existing.DepartmentName ?? existing.SiteName ?? existing.EngineerName} " +
            $"({existing.Details.Count} line(s)) needs your approval.",
            actionUrl: $"/Issue/Details/{id}",
            targetRole: Roles.Approver,
            documentType: DocumentType.MaterialIssue,
            documentId: id,
            cancellationToken: cancellationToken).ConfigureAwait(false);

        return ServiceResult.Success("Material issue submitted for approval. Stock has been reserved.");
    }

    /// <inheritdoc />
    public async Task<ServiceResult> ApproveAsync(
        ApprovalRequestDto request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var existing = await GetByIdAsync(request.DocumentId, cancellationToken).ConfigureAwait(false);

        if (existing is null)
        {
            return ServiceResult.Failure("The material issue was not found.", -404);
        }

        if (!existing.CanApprove)
        {
            return ServiceResult.Failure(
                $"A material issue in '{existing.Status.ToDisplayName()}' status cannot be approved.", -409);
        }

        if (existing.RequestedBy == _currentUser.UserId && !_currentUser.IsInRole(Roles.Administrator))
        {
            return ServiceResult.Failure("You raised this request, so it must be approved by someone else.", -403);
        }

        if (!request.Approve && request.Remarks.IsBlank())
        {
            return ServiceResult.Failure("A reason is required when rejecting a document.", -400);
        }

        if (request.Approve)
        {
            var errors = ValidateApprovedQuantities(existing, request);

            if (errors.Count > 0)
            {
                return ServiceResult.ValidationFailure(errors);
            }
        }

        var result = await _unitOfWork.Inventory
            .ApproveIssueAsync(request, _currentUser.UserId, cancellationToken)
            .ConfigureAwait(false);

        if (!result.IsSuccess)
        {
            return ServiceResult.Failure(result.Message, result.ReturnCode);
        }

        await _audit.LogAsync(
            request.Approve ? AuditAction.Approve : AuditAction.Reject,
            "Issue",
            request.DocumentId.ToString(),
            $"{(request.Approve ? "Approved" : "Rejected")} material issue {existing.IssueNumber}. {request.Remarks}"
                .Trim(),
            cancellationToken: cancellationToken).ConfigureAwait(false);

        await _notifications.NotifyAsync(
            request.Approve ? NotificationType.Approved : NotificationType.Rejected,
            request.Approve ? "Material issue approved" : "Material issue rejected",
            request.Approve
                ? $"{existing.IssueNumber} was approved and is ready for dispatch."
                : $"{existing.IssueNumber} was rejected. {request.Remarks}",
            actionUrl: $"/Issue/Details/{request.DocumentId}",
            userId: existing.RequestedBy,
            documentType: DocumentType.MaterialIssue,
            documentId: request.DocumentId,
            cancellationToken: cancellationToken).ConfigureAwait(false);

        return ServiceResult.Success(
            request.Approve ? "Material issue approved and stock deducted." : "Material issue rejected.");
    }

    /// <inheritdoc />
    public async Task<ServiceResult> DispatchAsync(
        DispatchRequestDto request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var existing = await _unitOfWork.Inventory
            .GetIssueAsync(request.IssueId, cancellationToken)
            .ConfigureAwait(false);

        if (existing is null)
        {
            return ServiceResult.Failure("The material issue was not found.", -404);
        }

        if (!existing.CanDispatch)
        {
            return ServiceResult.Failure(
                $"A material issue in '{existing.Status.ToDisplayName()}' status cannot be dispatched.", -409);
        }

        var errors = new List<string>();

        if (request.DispatchDate.HasValue && request.DispatchDate.Value.Date < existing.IssueDate.Date)
        {
            errors.Add("The dispatch date cannot be earlier than the issue date.");
        }

        if (request.CourierId is > 0 && request.TrackingNumber.IsBlank())
        {
            errors.Add("Enter the tracking number for the selected courier.");
        }

        foreach (var line in request.Lines)
        {
            var detail = existing.Details.FirstOrDefault(d => d.Id == line.DetailId);

            if (detail is null)
            {
                errors.Add($"Line {line.DetailId} does not belong to this document.");
                continue;
            }

            if (line.QuantityIssued < 0)
            {
                errors.Add($"{detail.ItemName}: the issued quantity cannot be negative.");
            }

            if (line.QuantityIssued > detail.QuantityApproved)
            {
                errors.Add(
                    $"{detail.ItemName}: cannot issue {line.QuantityIssued:N3} against " +
                    $"an approved quantity of {detail.QuantityApproved:N3}.");
            }
        }

        if (errors.Count > 0)
        {
            return ServiceResult.ValidationFailure(errors);
        }

        var result = await _unitOfWork.Inventory
            .DispatchIssueAsync(request, _currentUser.UserId, cancellationToken)
            .ConfigureAwait(false);

        if (!result.IsSuccess)
        {
            return ServiceResult.Failure(result.Message, result.ReturnCode);
        }

        await _audit.LogAsync(
            AuditAction.Issue, "Issue", request.IssueId.ToString(),
            $"Dispatched material issue {existing.IssueNumber}" +
            (request.TrackingNumber.IsBlank() ? "." : $" under tracking number {request.TrackingNumber}."),
            cancellationToken: cancellationToken).ConfigureAwait(false);

        await _notifications.NotifyAsync(
            NotificationType.MaterialDispatch,
            "Material dispatched",
            $"{existing.IssueNumber} has been dispatched" +
            (request.TrackingNumber.IsBlank() ? "." : $" (tracking {request.TrackingNumber})."),
            actionUrl: $"/Issue/Details/{request.IssueId}",
            userId: existing.RequestedBy,
            documentType: DocumentType.MaterialIssue,
            documentId: request.IssueId,
            cancellationToken: cancellationToken).ConfigureAwait(false);

        return ServiceResult.Success("Material issue dispatched successfully.");
    }

    /// <inheritdoc />
    public async Task<FileExportResult> ExportAsync(
        ExportFormat format,
        ReportFilter filter,
        CancellationToken cancellationToken = default)
    {
        var page = await _unitOfWork.Inventory.GetPagedIssuesAsync(
            new PagedRequest { PageNumber = 1, PageSize = AppConstants.MaxPageSize },
            filter,
            cancellationToken).ConfigureAwait(false);

        var definition = Exports.For<IssueListDto>()
            .WithTitle("Material Issue Register")
            .WithSubTitle($"{filter.EffectiveFrom.ToDisplayDate()} to {filter.EffectiveTo.ToDisplayDate()}")
            .DateColumn("Date", i => i.IssueDate)
            .Column("Issue Number", i => i.IssueNumber, width: 1.2f)
            .Column("Department", i => i.DepartmentName, width: 1.3f)
            .Column("Site", i => i.SiteName, width: 1.2f)
            .Column("Engineer", i => i.EngineerName, width: 1.3f)
            .Column("Project", i => i.ProjectName, width: 1.4f)
            .Column("Warehouse", i => i.WarehouseName, width: 1.1f)
            .Column("Lines", i => i.LineCount, alignRight: true, summable: true)
            .QuantityColumn("Quantity", i => i.TotalQuantity)
            .MoneyColumn("Value", i => i.TotalValue)
            .Column("Status", i => i.StatusName, width: 0.9f)
            .Build(page.Items);

        await _audit.LogAsync(
            AuditAction.Export, "Issue", description: $"Exported the issue register as {format}.",
            cancellationToken: cancellationToken).ConfigureAwait(false);

        return _export.Export(definition, format);
    }

    /// <inheritdoc />
    public async Task<FileExportResult?> PrintAsync(int id, CancellationToken cancellationToken = default)
    {
        var issue = await GetByIdAsync(id, cancellationToken).ConfigureAwait(false);

        if (issue is null)
        {
            return null;
        }

        var definition = Exports.For<IssueDetailDto>()
            .WithTitle($"Material Issue {issue.IssueNumber}")
            .WithSubTitle($"{issue.IssueDate.ToDisplayDate()} · {issue.StatusName}")
            .WithFilter("Department", issue.DepartmentName)
            .WithFilter("Site", issue.SiteName)
            .WithFilter("Engineer", issue.EngineerName)
            .WithFilter("Project", issue.ProjectName)
            .WithFilter("Warehouse", issue.WarehouseName)
            .WithFilter("Approved By", issue.ApprovedByName)
            .WithFilter("Tracking", issue.TrackingNumber)
            .Column("#", d => d.LineNumber, width: 0.3f, alignRight: true)
            .Column("Item Code", d => d.ItemCode, width: 0.9f)
            .Column("Item", d => d.ItemName, width: 2.6f)
            .Column("Part No.", d => d.PartNumber, width: 1f)
            .Column("Batch", d => d.BatchNumber, width: 0.8f)
            .QuantityColumn("Requested", d => d.QuantityRequested)
            .QuantityColumn("Approved", d => d.QuantityApproved)
            .QuantityColumn("Issued", d => d.QuantityIssued)
            .MoneyColumn("Rate", d => d.UnitCost, summable: false)
            .MoneyColumn("Value", d => d.TotalCost)
            .Build(issue.Details);

        await _audit.LogAsync(
            AuditAction.Export, "Issue", id.ToString(),
            $"Printed material issue {issue.IssueNumber}.",
            cancellationToken: cancellationToken).ConfigureAwait(false);

        return _export.ToPdf(definition);
    }

    // -----------------------------------------------------------------------
    // Business rules
    // -----------------------------------------------------------------------

    private async Task<ServiceResult> ValidateAsync(IssueDto dto, CancellationToken cancellationToken)
    {
        var errors = new List<string>();

        if (dto.IssueDate.Date > DateTime.Today)
        {
            errors.Add("The issue date cannot be in the future.");
        }

        if (dto.DepartmentId is null && dto.SiteId is null && dto.EngineerId is null)
        {
            errors.Add("Specify at least one of department, site or engineer as the destination.");
        }

        if (dto.Details.Count == 0)
        {
            errors.Add("Add at least one item line.");
        }

        var warehouse = await _unitOfWork.Warehouses
            .GetByIdAsync(dto.WarehouseId, cancellationToken)
            .ConfigureAwait(false);

        if (warehouse is null || warehouse.IsDeleted || !warehouse.IsActive)
        {
            errors.Add("Select a valid, active warehouse.");

            // Without a warehouse there is no balance to check against.
            return errors.Count == 0 ? ServiceResult.Success() : ServiceResult.ValidationFailure(errors);
        }

        for (var i = 0; i < dto.Details.Count; i++)
        {
            var line = dto.Details[i];
            var position = i + 1;

            if (line.ItemId <= 0)
            {
                errors.Add($"Line {position}: select an item.");
                continue;
            }

            if (line.QuantityRequested <= 0)
            {
                errors.Add($"Line {position}: the requested quantity must be greater than zero.");
                continue;
            }

            var item = await _unitOfWork.Items.GetByIdAsync(line.ItemId, cancellationToken).ConfigureAwait(false);

            if (item is null || item.IsDeleted)
            {
                errors.Add($"Line {position}: the selected item no longer exists.");
                continue;
            }

            if (item.IsBatchTracked && line.BatchNumber.IsBlank())
            {
                errors.Add($"Line {position}: '{item.ItemName}' is batch tracked, so a batch number is required.");
            }

            var balance = await _unitOfWork.Inventory
                .GetItemBalanceAsync(line.ItemId, dto.WarehouseId, cancellationToken)
                .ConfigureAwait(false);

            var available = balance?.AvailableStock ?? 0m;
            line.AvailableStock = available;

            if (line.QuantityRequested > available)
            {
                errors.Add(
                    $"Line {position}: '{item.ItemName}' has {available:N3} available in " +
                    $"{warehouse.Name} but {line.QuantityRequested:N3} was requested.");
            }
        }

        var duplicates = dto.Details
            .GroupBy(d => new { d.ItemId, Batch = d.BatchNumber?.Trim().ToUpperInvariant() ?? string.Empty })
            .Where(g => g.Count() > 1)
            .ToList();

        foreach (var group in duplicates)
        {
            errors.Add($"Item id {group.Key.ItemId} appears on more than one line with the same batch.");
        }

        return errors.Count == 0 ? ServiceResult.Success() : ServiceResult.ValidationFailure(errors);
    }

    /// <summary>
    /// Checks the per-line approved quantities supplied by the approver.
    /// An empty line list means "approve everything as requested".
    /// </summary>
    private static List<string> ValidateApprovedQuantities(IssueDto issue, ApprovalRequestDto request)
    {
        var errors = new List<string>();

        if (request.Lines.Count == 0)
        {
            foreach (var detail in issue.Details.Where(d => d.QuantityRequested > d.AvailableStock))
            {
                errors.Add(
                    $"{detail.ItemName}: only {detail.AvailableStock:N3} is available against " +
                    $"a request for {detail.QuantityRequested:N3}. Reduce the approved quantity.");
            }

            return errors;
        }

        foreach (var line in request.Lines)
        {
            var detail = issue.Details.FirstOrDefault(d => d.Id == line.DetailId);

            if (detail is null)
            {
                errors.Add($"Line {line.DetailId} does not belong to this document.");
                continue;
            }

            if (line.QuantityApproved < 0)
            {
                errors.Add($"{detail.ItemName}: the approved quantity cannot be negative.");
            }

            if (line.QuantityApproved > detail.QuantityRequested)
            {
                errors.Add(
                    $"{detail.ItemName}: cannot approve {line.QuantityApproved:N3} against " +
                    $"a request for {detail.QuantityRequested:N3}.");
            }

            // Reserved stock for this document is already excluded from
            // AvailableStock, so add it back before comparing.
            var issuable = detail.AvailableStock + detail.QuantityReserved;

            if (line.QuantityApproved > issuable)
            {
                errors.Add(
                    $"{detail.ItemName}: only {issuable:N3} is in stock; " +
                    $"{line.QuantityApproved:N3} cannot be approved.");
            }
        }

        if (request.Lines.All(l => l.QuantityApproved <= 0))
        {
            errors.Add("At least one line must have an approved quantity greater than zero.");
        }

        return errors;
    }

    private static void NormaliseLines(IssueDto dto)
    {
        var line = 1;

        foreach (var detail in dto.Details)
        {
            detail.LineNumber = line++;
            detail.BatchNumber = detail.BatchNumber.NormalizeOrNull();
            detail.SerialNumber = detail.SerialNumber.NormalizeOrNull();
            detail.PartNumber = detail.PartNumber.NormalizeOrNull();
        }
    }

    private static string Serialize(IssueDto dto)
        => System.Text.Json.JsonSerializer.Serialize(new
        {
            dto.IssueNumber,
            dto.IssueDate,
            dto.DepartmentId,
            dto.SiteId,
            dto.EngineerId,
            dto.WarehouseId,
            dto.ProjectName,
            LineCount = dto.Details.Count,
            TotalQuantity = dto.Details.Sum(d => d.QuantityRequested)
        });
}
