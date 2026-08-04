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
/// Goods Receipt Note business service.
/// <para>
/// It owns the document's rules — who may edit it in which state, that accepted
/// plus rejected must equal received, that a blacklisted vendor cannot supply —
/// and delegates the actual posting to <c>sp_ApproveGRN</c>, which updates the
/// header, the lines, the stock ledger and the item's weighted-average cost in
/// one SQL transaction.
/// </para>
/// </summary>
public sealed class GrnService : IGrnService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUser _currentUser;
    private readonly IAuditService _audit;
    private readonly INotificationService _notifications;
    private readonly IExportService _export;
    private readonly ILogger<GrnService> _logger;

    public GrnService(
        IUnitOfWork unitOfWork,
        ICurrentUser currentUser,
        IAuditService audit,
        INotificationService notifications,
        IExportService export,
        ILogger<GrnService> logger)
    {
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _currentUser = currentUser ?? throw new ArgumentNullException(nameof(currentUser));
        _audit = audit ?? throw new ArgumentNullException(nameof(audit));
        _notifications = notifications ?? throw new ArgumentNullException(nameof(notifications));
        _export = export ?? throw new ArgumentNullException(nameof(export));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public Task<PagedResult<GrnListDto>> GetPagedAsync(
        PagedRequest request,
        ReportFilter filter,
        CancellationToken cancellationToken = default)
        => _unitOfWork.Inventory.GetPagedGrnsAsync(request, filter, cancellationToken);

    /// <inheritdoc />
    public async Task<GrnDto?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var grn = await _unitOfWork.Inventory.GetGrnAsync(id, cancellationToken).ConfigureAwait(false);

        if (grn is not null)
        {
            grn.Attachments = (await _unitOfWork.Attachments
                    .GetForDocumentAsync(DocumentType.GoodsReceiptNote, id, cancellationToken)
                    .ConfigureAwait(false))
                .ToList();
        }

        return grn;
    }

    /// <inheritdoc />
    public async Task<ServiceResult<int>> SaveAsync(GrnDto dto, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        var isUpdate = dto.Id > 0;

        if (isUpdate)
        {
            var existing = await _unitOfWork.Inventory.GetGrnAsync(dto.Id, cancellationToken).ConfigureAwait(false);

            if (existing is null)
            {
                return ServiceResult<int>.Failure("The goods receipt note was not found.", -404);
            }

            if (!existing.CanEdit)
            {
                return ServiceResult<int>.Failure(
                    $"A goods receipt note in '{existing.Status.ToDisplayName()}' status cannot be edited.", -409);
            }
        }

        var validation = await ValidateAsync(dto, cancellationToken).ConfigureAwait(false);

        if (!validation.Succeeded)
        {
            return ServiceResult<int>.From(validation);
        }

        NormaliseLines(dto);

        var result = await _unitOfWork.Inventory
            .SaveGrnAsync(dto, _currentUser.UserId, isUpdate, cancellationToken)
            .ConfigureAwait(false);

        if (!result.IsSuccess)
        {
            return ServiceResult<int>.Failure(result.Message, result.ReturnCode);
        }

        var grnId = isUpdate ? dto.Id : result.NewId ?? 0;
        var number = result.GeneratedNumber ?? dto.GrnNumber;

        await _audit.LogAsync(
            isUpdate ? AuditAction.Update : AuditAction.Create,
            "GRN",
            grnId.ToString(),
            $"{(isUpdate ? "Updated" : "Created")} goods receipt note {number} " +
            $"with {dto.Details.Count} line(s).",
            newValues: Serialize(dto),
            cancellationToken: cancellationToken).ConfigureAwait(false);

        return ServiceResult<int>.Success(grnId, $"Goods receipt note {number} saved successfully.");
    }

    /// <inheritdoc />
    public async Task<ServiceResult> DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        var existing = await _unitOfWork.Inventory.GetGrnAsync(id, cancellationToken).ConfigureAwait(false);

        if (existing is null)
        {
            return ServiceResult.Failure("The goods receipt note was not found.", -404);
        }

        if (existing.Status != DocumentStatus.Draft)
        {
            return ServiceResult.Failure("Only a draft goods receipt note can be deleted.", -409);
        }

        var result = await _unitOfWork.Inventory
            .DeleteGrnAsync(id, _currentUser.UserId, cancellationToken)
            .ConfigureAwait(false);

        if (!result.IsSuccess)
        {
            return ServiceResult.Failure(result.Message, result.ReturnCode);
        }

        await _audit.LogAsync(
            AuditAction.Delete, "GRN", id.ToString(),
            $"Deleted draft goods receipt note {existing.GrnNumber}.",
            cancellationToken: cancellationToken).ConfigureAwait(false);

        return ServiceResult.Success("Goods receipt note deleted successfully.");
    }

    /// <inheritdoc />
    public async Task<ServiceResult> SubmitAsync(int id, string? remarks, CancellationToken cancellationToken = default)
    {
        var existing = await _unitOfWork.Inventory.GetGrnAsync(id, cancellationToken).ConfigureAwait(false);

        if (existing is null)
        {
            return ServiceResult.Failure("The goods receipt note was not found.", -404);
        }

        if (!existing.CanSubmit)
        {
            return ServiceResult.Failure(
                $"A goods receipt note in '{existing.Status.ToDisplayName()}' status cannot be submitted.", -409);
        }

        if (existing.Details.Count == 0)
        {
            return ServiceResult.Failure("Add at least one item line before submitting.", -400);
        }

        if (existing.Details.All(d => d.QuantityAccepted <= 0))
        {
            return ServiceResult.Failure(
                "Every line was rejected; there is nothing to receive into stock.", -400);
        }

        var result = await _unitOfWork.Inventory
            .SubmitGrnAsync(id, _currentUser.UserId, remarks, cancellationToken)
            .ConfigureAwait(false);

        if (!result.IsSuccess)
        {
            return ServiceResult.Failure(result.Message, result.ReturnCode);
        }

        await _audit.LogAsync(
            AuditAction.Submit, "GRN", id.ToString(),
            $"Submitted goods receipt note {existing.GrnNumber} for approval.",
            cancellationToken: cancellationToken).ConfigureAwait(false);

        await _notifications.NotifyAsync(
            NotificationType.PendingApproval,
            "Goods receipt note awaiting approval",
            $"{existing.GrnNumber} from {existing.VendorName} " +
            $"({existing.GrandTotal.ToMoney()}) needs your approval.",
            actionUrl: $"/Grn/Details/{id}",
            targetRole: Roles.Approver,
            documentType: DocumentType.GoodsReceiptNote,
            documentId: id,
            cancellationToken: cancellationToken).ConfigureAwait(false);

        return ServiceResult.Success("Goods receipt note submitted for approval.");
    }

    /// <inheritdoc />
    public async Task<ServiceResult> ApproveAsync(
        int id,
        bool approve,
        string? remarks,
        CancellationToken cancellationToken = default)
    {
        var existing = await _unitOfWork.Inventory.GetGrnAsync(id, cancellationToken).ConfigureAwait(false);

        if (existing is null)
        {
            return ServiceResult.Failure("The goods receipt note was not found.", -404);
        }

        if (!existing.CanApprove)
        {
            return ServiceResult.Failure(
                $"A goods receipt note in '{existing.Status.ToDisplayName()}' status cannot be approved.", -409);
        }

        // Separation of duties: whoever recorded the receipt may not approve it.
        if (existing.ReceivedBy == _currentUser.UserId && !_currentUser.IsInRole(Roles.Administrator))
        {
            return ServiceResult.Failure(
                "You recorded this receipt, so it must be approved by someone else.", -403);
        }

        if (!approve && remarks.IsBlank())
        {
            return ServiceResult.Failure("A reason is required when rejecting a document.", -400);
        }

        var result = await _unitOfWork.Inventory
            .ApproveGrnAsync(id, _currentUser.UserId, approve, remarks, cancellationToken)
            .ConfigureAwait(false);

        if (!result.IsSuccess)
        {
            return ServiceResult.Failure(result.Message, result.ReturnCode);
        }

        await _audit.LogAsync(
            approve ? AuditAction.Approve : AuditAction.Reject,
            "GRN",
            id.ToString(),
            $"{(approve ? "Approved" : "Rejected")} goods receipt note {existing.GrnNumber}. {remarks}".Trim(),
            cancellationToken: cancellationToken).ConfigureAwait(false);

        await _notifications.NotifyAsync(
            approve ? NotificationType.Approved : NotificationType.Rejected,
            approve ? "Goods receipt note approved" : "Goods receipt note rejected",
            approve
                ? $"{existing.GrnNumber} was approved and stock has been updated."
                : $"{existing.GrnNumber} was rejected. {remarks}",
            actionUrl: $"/Grn/Details/{id}",
            userId: existing.ReceivedBy,
            documentType: DocumentType.GoodsReceiptNote,
            documentId: id,
            cancellationToken: cancellationToken).ConfigureAwait(false);

        if (approve)
        {
            await _notifications.NotifyAsync(
                NotificationType.MaterialReceipt,
                "Material received",
                $"{existing.Details.Count} item(s) received into {existing.WarehouseName} on {existing.GrnNumber}.",
                actionUrl: $"/Grn/Details/{id}",
                targetRole: Roles.InventoryManager,
                documentType: DocumentType.GoodsReceiptNote,
                documentId: id,
                cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        return ServiceResult.Success(
            approve ? "Goods receipt note approved and stock updated." : "Goods receipt note rejected.");
    }

    /// <inheritdoc />
    public async Task<FileExportResult> ExportAsync(
        ExportFormat format,
        ReportFilter filter,
        CancellationToken cancellationToken = default)
    {
        var page = await _unitOfWork.Inventory.GetPagedGrnsAsync(
            new PagedRequest { PageNumber = 1, PageSize = AppConstants.MaxPageSize },
            filter,
            cancellationToken).ConfigureAwait(false);

        var definition = Exports.For<GrnListDto>()
            .WithTitle("Material Receipt Register")
            .WithSubTitle($"{filter.EffectiveFrom.ToDisplayDate()} to {filter.EffectiveTo.ToDisplayDate()}")
            .WithFilter("Vendor", filter.VendorId?.ToString())
            .WithFilter("Warehouse", filter.WarehouseId?.ToString())
            .DateColumn("Date", g => g.GrnDate)
            .Column("GRN Number", g => g.GrnNumber, width: 1.2f)
            .Column("PO Number", g => g.PurchaseOrderNumber, width: 1f)
            .Column("Vendor", g => g.VendorName, width: 2f)
            .Column("Warehouse", g => g.WarehouseName, width: 1.2f)
            .Column("Lines", g => g.LineCount, alignRight: true, summable: true)
            .QuantityColumn("Quantity", g => g.TotalQuantity)
            .MoneyColumn("Value", g => g.GrandTotal)
            .Column("Status", g => g.StatusName, width: 0.9f)
            .Column("Approved By", g => g.ApprovedByName, width: 1.2f)
            .Build(page.Items);

        await _audit.LogAsync(
            AuditAction.Export, "GRN", description: $"Exported the receipt register as {format}.",
            cancellationToken: cancellationToken).ConfigureAwait(false);

        return _export.Export(definition, format);
    }

    /// <inheritdoc />
    public async Task<FileExportResult?> PrintAsync(int id, CancellationToken cancellationToken = default)
    {
        var grn = await GetByIdAsync(id, cancellationToken).ConfigureAwait(false);

        if (grn is null)
        {
            return null;
        }

        var definition = Exports.For<GrnDetailDto>()
            .WithTitle($"Goods Receipt Note {grn.GrnNumber}")
            .WithSubTitle($"{grn.VendorName} · {grn.GrnDate.ToDisplayDate()} · {grn.StatusName}")
            .WithFilter("Purchase Order", grn.PurchaseOrderNumber)
            .WithFilter("Delivery Challan", grn.DeliveryChallanNumber)
            .WithFilter("Warehouse", grn.WarehouseName)
            .WithFilter("Received By", grn.ReceivedByName)
            .WithFilter("Approved By", grn.ApprovedByName)
            .Column("#", d => d.LineNumber, width: 0.3f, alignRight: true)
            .Column("Item Code", d => d.ItemCode, width: 0.9f)
            .Column("Item", d => d.ItemName, width: 2.4f)
            .Column("Part No.", d => d.PartNumber, width: 1f)
            .Column("Batch", d => d.BatchNumber, width: 0.8f)
            .QuantityColumn("Received", d => d.QuantityReceived)
            .QuantityColumn("Accepted", d => d.QuantityAccepted)
            .QuantityColumn("Rejected", d => d.QuantityRejected)
            .MoneyColumn("Rate", d => d.UnitCost, summable: false)
            .MoneyColumn("Amount", d => d.TotalCost)
            .Landscape(true)
            .Build(grn.Details);

        await _audit.LogAsync(
            AuditAction.Export, "GRN", id.ToString(),
            $"Printed goods receipt note {grn.GrnNumber}.",
            cancellationToken: cancellationToken).ConfigureAwait(false);

        return _export.ToPdf(definition);
    }

    // -----------------------------------------------------------------------
    // Business rules
    // -----------------------------------------------------------------------

    private async Task<ServiceResult> ValidateAsync(GrnDto dto, CancellationToken cancellationToken)
    {
        var errors = new List<string>();

        if (dto.GrnDate.Date > DateTime.Today)
        {
            errors.Add("The GRN date cannot be in the future.");
        }

        if (dto.Details.Count == 0)
        {
            errors.Add("Add at least one item line.");
        }

        var vendor = await _unitOfWork.Vendors.GetByIdAsync(dto.VendorId, cancellationToken).ConfigureAwait(false);

        if (vendor is null || vendor.IsDeleted)
        {
            errors.Add("Select a valid vendor.");
        }
        else if (vendor.IsBlacklisted)
        {
            errors.Add($"Vendor '{vendor.Name}' is blacklisted and cannot supply material.");
        }
        else if (!vendor.IsActive)
        {
            errors.Add($"Vendor '{vendor.Name}' is inactive.");
        }

        var warehouse = await _unitOfWork.Warehouses.GetByIdAsync(dto.WarehouseId, cancellationToken)
            .ConfigureAwait(false);

        if (warehouse is null || warehouse.IsDeleted || !warehouse.IsActive)
        {
            errors.Add("Select a valid, active warehouse.");
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

            if (line.QuantityReceived <= 0)
            {
                errors.Add($"Line {position}: received quantity must be greater than zero.");
            }

            if (line.QuantityAccepted < 0 || line.QuantityRejected < 0)
            {
                errors.Add($"Line {position}: quantities cannot be negative.");
            }

            if (line.QuantityAccepted + line.QuantityRejected != line.QuantityReceived)
            {
                errors.Add(
                    $"Line {position}: accepted ({line.QuantityAccepted:N3}) plus rejected " +
                    $"({line.QuantityRejected:N3}) must equal received ({line.QuantityReceived:N3}).");
            }

            if (line.QuantityRejected > 0 && line.RejectionReason.IsBlank())
            {
                errors.Add($"Line {position}: give a reason for the rejected quantity.");
            }

            if (line.UnitCost < 0)
            {
                errors.Add($"Line {position}: unit cost cannot be negative.");
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

            if (item.IsSerialTracked && line.SerialNumber.IsBlank())
            {
                errors.Add($"Line {position}: '{item.ItemName}' is serial tracked, so a serial number is required.");
            }

            if (line.ExpiryDate.HasValue && line.ExpiryDate.Value.Date <= dto.GrnDate.Date)
            {
                errors.Add($"Line {position}: the expiry date must be after the receipt date.");
            }
        }

        // Duplicate detection: the same item/batch/serial must not appear twice.
        var duplicates = dto.Details
            .GroupBy(d => new
            {
                d.ItemId,
                Batch = d.BatchNumber?.Trim().ToUpperInvariant() ?? string.Empty,
                Serial = d.SerialNumber?.Trim().ToUpperInvariant() ?? string.Empty
            })
            .Where(g => g.Count() > 1)
            .ToList();

        foreach (var group in duplicates)
        {
            errors.Add($"Item id {group.Key.ItemId} appears on more than one line with the same batch and serial.");
        }

        return errors.Count == 0 ? ServiceResult.Success() : ServiceResult.ValidationFailure(errors);
    }

    /// <summary>
    /// Fills in the values the UI may have left blank and renumbers the lines so
    /// the stored procedure receives a clean, ordered set.
    /// </summary>
    private static void NormaliseLines(GrnDto dto)
    {
        var line = 1;

        foreach (var detail in dto.Details)
        {
            detail.LineNumber = line++;

            // A blank accepted quantity means "all of it passed inspection".
            if (detail.QuantityAccepted == 0 && detail.QuantityRejected == 0)
            {
                detail.QuantityAccepted = detail.QuantityReceived;
            }

            detail.BatchNumber = detail.BatchNumber.NormalizeOrNull();
            detail.SerialNumber = detail.SerialNumber.NormalizeOrNull();
            detail.PartNumber = detail.PartNumber.NormalizeOrNull();
        }
    }

    private static string Serialize(GrnDto dto)
        => System.Text.Json.JsonSerializer.Serialize(new
        {
            dto.GrnNumber,
            dto.GrnDate,
            dto.VendorId,
            dto.WarehouseId,
            dto.PurchaseOrderNumber,
            LineCount = dto.Details.Count,
            TotalQuantity = dto.Details.Sum(d => d.QuantityAccepted)
        });
}
