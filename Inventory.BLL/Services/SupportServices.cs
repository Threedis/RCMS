using Inventory.BLL.Interfaces;
using Inventory.Common.Configuration;
using Inventory.Common.Constants;
using Inventory.Common.Enums;
using Inventory.Common.Extensions;
using Inventory.Common.Models;
using Inventory.Common.Security;
using Inventory.Entities.Dtos;
using Inventory.Entities.Models;
using Inventory.Repository.UnitOfWork;
using Inventory.Services.Caching;
using Inventory.Services.Email;
using Inventory.Services.Export;
using Inventory.Services.Files;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Inventory.BLL.Services;

/// <summary>
/// Approval queue. It routes a decision to the document service that owns the
/// rules, so the workflow logic is never duplicated between the two document
/// types or between the queue and the document screens.
/// </summary>
public sealed class ApprovalService : IApprovalService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUser _currentUser;
    private readonly IGrnService _grnService;
    private readonly IIssueService _issueService;

    public ApprovalService(
        IUnitOfWork unitOfWork,
        ICurrentUser currentUser,
        IGrnService grnService,
        IIssueService issueService)
    {
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _currentUser = currentUser ?? throw new ArgumentNullException(nameof(currentUser));
        _grnService = grnService ?? throw new ArgumentNullException(nameof(grnService));
        _issueService = issueService ?? throw new ArgumentNullException(nameof(issueService));
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<PendingApprovalDto>> GetPendingAsync(
        DocumentType? documentType = null,
        CancellationToken cancellationToken = default)
        => _unitOfWork.Inventory.GetPendingApprovalsAsync(_currentUser.UserId, documentType, cancellationToken);

    /// <inheritdoc />
    public async Task<int> GetPendingCountAsync(CancellationToken cancellationToken = default)
    {
        var pending = await GetPendingAsync(null, cancellationToken).ConfigureAwait(false);
        return pending.Count;
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<ApprovalHistoryDto>> GetTrailAsync(
        DocumentType documentType,
        int documentId,
        CancellationToken cancellationToken = default)
        => _unitOfWork.Inventory.GetApprovalTrailAsync(documentType, documentId, cancellationToken);

    /// <inheritdoc />
    public Task<ServiceResult> DecideAsync(ApprovalRequestDto request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        return request.DocumentType switch
        {
            DocumentType.GoodsReceiptNote =>
                _grnService.ApproveAsync(request.DocumentId, request.Approve, request.Remarks, cancellationToken),
            DocumentType.MaterialIssue =>
                _issueService.ApproveAsync(request, cancellationToken),
            _ => Task.FromResult(ServiceResult.Failure("Unsupported document type.", -400))
        };
    }
}

/// <summary>Stock enquiry service; a thin pass-through onto the stock procedures.</summary>
public sealed class StockService : IStockService
{
    private readonly IUnitOfWork _unitOfWork;

    public StockService(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
    }

    /// <inheritdoc />
    public Task<PagedResult<CurrentStockDto>> GetCurrentStockAsync(
        PagedRequest request,
        ReportFilter filter,
        CancellationToken cancellationToken = default)
        => _unitOfWork.Inventory.GetCurrentStockAsync(request, filter, cancellationToken);

    /// <inheritdoc />
    public Task<PagedResult<StockLedgerDto>> GetLedgerAsync(
        PagedRequest request,
        ReportFilter filter,
        CancellationToken cancellationToken = default)
        => _unitOfWork.Inventory.GetStockLedgerAsync(request, filter, cancellationToken);

    /// <inheritdoc />
    public Task<IReadOnlyList<LowStockDto>> GetLowStockAsync(
        int? warehouseId = null,
        CancellationToken cancellationToken = default)
        => _unitOfWork.Inventory.GetLowStockItemsAsync(warehouseId, 200, cancellationToken);

    /// <inheritdoc />
    public Task<ItemStockSnapshotDto?> GetBalanceAsync(
        int itemId,
        int warehouseId,
        CancellationToken cancellationToken = default)
        => _unitOfWork.Inventory.GetItemBalanceAsync(itemId, warehouseId, cancellationToken);
}

/// <summary>
/// Dashboard service. The aggregates are expensive to compute, so the whole
/// payload is cached briefly per user; the cache key includes the user because
/// the pending-approval panel is personal.
/// </summary>
public sealed class DashboardService : IDashboardService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUser _currentUser;
    private readonly ICacheService _cache;

    public DashboardService(IUnitOfWork unitOfWork, ICurrentUser currentUser, ICacheService cache)
    {
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _currentUser = currentUser ?? throw new ArgumentNullException(nameof(currentUser));
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
    }

    /// <inheritdoc />
    public Task<DashboardDto> GetDashboardAsync(CancellationToken cancellationToken = default)
        => _cache.GetOrCreateAsync(
            $"dashboard:{_currentUser.UserId}",
            () => _unitOfWork.Dashboard.GetDashboardAsync(_currentUser.UserId, cancellationToken),
            CacheKeys.DashboardDuration,
            cancellationToken);

    /// <inheritdoc />
    public Task<IReadOnlyList<RecentActivityDto>> GetRecentActivitiesAsync(
        int maxRows = 15,
        CancellationToken cancellationToken = default)
        => _unitOfWork.Dashboard.GetRecentActivitiesAsync(maxRows, cancellationToken);
}

/// <summary>
/// Notification service. In-app rows are always written; e-mail is attempted
/// only when enabled, and a mail failure never propagates to the caller.
/// </summary>
public sealed class NotificationService : INotificationService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUser _currentUser;
    private readonly IEmailSender _email;
    private readonly ApplicationSettings _settings;
    private readonly ILogger<NotificationService> _logger;

    public NotificationService(
        IUnitOfWork unitOfWork,
        ICurrentUser currentUser,
        IEmailSender email,
        IOptions<ApplicationSettings> settings,
        ILogger<NotificationService> logger)
    {
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _currentUser = currentUser ?? throw new ArgumentNullException(nameof(currentUser));
        _email = email ?? throw new ArgumentNullException(nameof(email));
        _settings = settings?.Value ?? new ApplicationSettings();
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<NotificationDto>> GetForCurrentUserAsync(
        bool unreadOnly = false,
        int maxRows = 20,
        CancellationToken cancellationToken = default)
        => _unitOfWork.Notifications.GetForUserAsync(_currentUser.UserId, unreadOnly, maxRows, cancellationToken);

    /// <inheritdoc />
    public Task<int> GetUnreadCountAsync(CancellationToken cancellationToken = default)
        => _unitOfWork.Notifications.GetUnreadCountAsync(_currentUser.UserId, cancellationToken);

    /// <inheritdoc />
    public async Task<ServiceResult> MarkReadAsync(long id, CancellationToken cancellationToken = default)
    {
        var result = await _unitOfWork.Notifications
            .MarkAsReadAsync(id, _currentUser.UserId, cancellationToken)
            .ConfigureAwait(false);

        return result.IsSuccess
            ? ServiceResult.Success("Notification marked as read.")
            : ServiceResult.Failure(result.Message, result.ReturnCode);
    }

    /// <inheritdoc />
    public async Task<ServiceResult> MarkAllReadAsync(CancellationToken cancellationToken = default)
    {
        var result = await _unitOfWork.Notifications
            .MarkAllAsReadAsync(_currentUser.UserId, cancellationToken)
            .ConfigureAwait(false);

        return result.IsSuccess
            ? ServiceResult.Success("All notifications marked as read.")
            : ServiceResult.Failure(result.Message, result.ReturnCode);
    }

    /// <inheritdoc />
    public async Task NotifyAsync(
        NotificationType type,
        string title,
        string message,
        string? actionUrl = null,
        int? userId = null,
        string? targetRole = null,
        DocumentType? documentType = null,
        int? documentId = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var notification = new Notification
            {
                UserId = userId,
                TargetRole = targetRole,
                NotificationType = type,
                Title = title.Truncate(200),
                Message = message.Truncate(1000),
                ActionUrl = actionUrl,
                DocumentType = documentType,
                DocumentId = documentId,
                CreatedBy = _currentUser.UserId
            };

            // The procedure fans a role-targeted notification out to every
            // active member of that role.
            await _unitOfWork.Notifications.CreateAsync(notification, cancellationToken).ConfigureAwait(false);

            if (!_settings.EnableEmailNotifications)
            {
                return;
            }

            var recipients = await ResolveEmailRecipientsAsync(userId, targetRole, cancellationToken)
                .ConfigureAwait(false);

            if (recipients.Count == 0)
            {
                return;
            }

            await _email.SendAsync(new EmailMessage
            {
                To = recipients,
                Subject = $"[{_settings.ApplicationName}] {title}",
                HtmlBody = _email.BuildTemplate(
                    title,
                    $"<p>{System.Net.WebUtility.HtmlEncode(message)}</p>",
                    actionUrl,
                    "Open in the application")
            }, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            // Never let a notification failure roll back the business action.
            _logger.LogError(ex, "Failed to raise notification '{Title}'.", title);
        }
    }

    private async Task<IReadOnlyList<string>> ResolveEmailRecipientsAsync(
        int? userId,
        string? targetRole,
        CancellationToken cancellationToken)
    {
        var users = _unitOfWork.Repository<ApplicationUser>().Query();

        if (userId is > 0)
        {
            return await users
                .Where(u => u.Id == userId && u.IsActive && !u.IsDeleted && u.Email != null)
                .Select(u => u.Email!)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);
        }

        if (targetRole.IsBlank())
        {
            return Array.Empty<string>();
        }

        // Role membership lives in the Identity join table; the notification
        // procedure resolves it in SQL for the in-app rows, and the same join is
        // repeated here for the e-mail copy.
        return await _unitOfWork.Repository<ApplicationUser>().Query()
            .Where(u => u.IsActive && !u.IsDeleted && u.Email != null)
            .Join(
                _unitOfWork.Repository<Microsoft.AspNetCore.Identity.IdentityUserRole<int>>().Query(),
                u => u.Id, ur => ur.UserId, (u, ur) => new { u.Email, ur.RoleId })
            .Join(
                _unitOfWork.Repository<ApplicationRole>().Query().Where(r => r.Name == targetRole),
                x => x.RoleId, r => r.Id, (x, r) => x.Email!)
            .Distinct()
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }
}

/// <summary>Audit trail service; the single place that writes audit rows.</summary>
public sealed class AuditService : IAuditService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUser _currentUser;
    private readonly IExportService _export;

    public AuditService(IUnitOfWork unitOfWork, ICurrentUser currentUser, IExportService export)
    {
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _currentUser = currentUser ?? throw new ArgumentNullException(nameof(currentUser));
        _export = export ?? throw new ArgumentNullException(nameof(export));
    }

    /// <inheritdoc />
    public Task<PagedResult<AuditLogDto>> GetLogsAsync(
        PagedRequest request,
        DateTime? fromDate,
        DateTime? toDate,
        int? userId,
        string? entityName,
        int? action,
        CancellationToken cancellationToken = default)
        => _unitOfWork.Audit.GetAuditLogsAsync(request, fromDate, toDate, userId, entityName, action, cancellationToken);

    /// <inheritdoc />
    public Task<AuditLogDto?> GetLogAsync(long id, CancellationToken cancellationToken = default)
        => _unitOfWork.Audit.GetAuditLogAsync(id, cancellationToken);

    /// <inheritdoc />
    public Task<PagedResult<LoginHistoryDto>> GetLoginHistoryAsync(
        PagedRequest request,
        DateTime? fromDate,
        DateTime? toDate,
        CancellationToken cancellationToken = default)
        => _unitOfWork.Audit.GetLoginHistoryAsync(request, fromDate, toDate, cancellationToken);

    /// <inheritdoc />
    public Task LogAsync(
        AuditAction action,
        string? entityName = null,
        string? entityId = null,
        string? description = null,
        string? oldValues = null,
        string? newValues = null,
        bool isSuccessful = true,
        string? errorMessage = null,
        string? source = null,
        CancellationToken cancellationToken = default)
        => _unitOfWork.Audit.WriteAsync(new AuditLog
        {
            Action = action,
            EntityName = entityName,
            EntityId = entityId,
            Description = description.Truncate(500),
            OldValues = oldValues,
            NewValues = newValues,
            UserId = _currentUser.UserId > 0 ? _currentUser.UserId : null,
            UserName = _currentUser.UserName,
            IpAddress = _currentUser.IpAddress,
            UserAgent = _currentUser.UserAgent.Truncate(400),
            Source = source,
            IsSuccessful = isSuccessful,
            ErrorMessage = errorMessage.Truncate(1000)
        }, cancellationToken);

    /// <inheritdoc />
    public Task RecordLoginAsync(
        int? userId,
        string userName,
        bool successful,
        string? failureReason = null,
        CancellationToken cancellationToken = default)
        => _unitOfWork.Audit.RecordLoginAsync(new LoginHistory
        {
            UserId = userId,
            UserName = userName.Truncate(256),
            IsSuccessful = successful,
            FailureReason = failureReason.Truncate(300),
            IpAddress = _currentUser.IpAddress,
            UserAgent = _currentUser.UserAgent.Truncate(400)
        }, cancellationToken);

    /// <inheritdoc />
    public async Task<FileExportResult> ExportAsync(
        ExportFormat format,
        DateTime? fromDate,
        DateTime? toDate,
        CancellationToken cancellationToken = default)
    {
        var page = await _unitOfWork.Audit.GetAuditLogsAsync(
            new PagedRequest { PageNumber = 1, PageSize = AppConstants.MaxPageSize, SortColumn = "CreatedOn", SortDirection = "DESC" },
            fromDate, toDate, null, null, null,
            cancellationToken).ConfigureAwait(false);

        var definition = Exports.For<AuditLogDto>()
            .WithTitle("Audit Trail")
            .WithSubTitle(BuildRangeSubTitle(fromDate, toDate))
            .Column("Timestamp", a => a.CreatedOn, "dd-MMM-yyyy HH:mm:ss", width: 1.2f)
            .Column("User", a => a.UserName, width: 1.1f)
            .Column("Action", a => a.ActionName, width: 0.8f)
            .Column("Entity", a => a.EntityName, width: 0.9f)
            .Column("Key", a => a.EntityId, width: 0.6f)
            .Column("Description", a => a.Description, width: 3f)
            .Column("IP Address", a => a.IpAddress, width: 0.9f)
            .Column("Result", a => a.IsSuccessful ? "Success" : "Failed", width: 0.6f)
            .ShowTotals(false)
            .Build(page.Items);

        return _export.Export(definition, format);
    }

    /// <summary>Renders the applied date range as a readable sub-title.</summary>
    private static string BuildRangeSubTitle(DateTime? fromDate, DateTime? toDate) => (fromDate, toDate) switch
    {
        (null, null) => "All dates",
        (not null, null) => $"From {fromDate!.Value.ToDisplayDate()}",
        (null, not null) => $"Up to {toDate!.Value.ToDisplayDate()}",
        _ => $"{fromDate!.Value.ToDisplayDate()} to {toDate!.Value.ToDisplayDate()}"
    };
}

/// <summary>Attachment service: validation, storage and metadata in one place.</summary>
public sealed class AttachmentService : IAttachmentService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IFileStorageService _storage;
    private readonly ICurrentUser _currentUser;
    private readonly IAuditService _audit;
    private readonly ILogger<AttachmentService> _logger;

    public AttachmentService(
        IUnitOfWork unitOfWork,
        IFileStorageService storage,
        ICurrentUser currentUser,
        IAuditService audit,
        ILogger<AttachmentService> logger)
    {
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _storage = storage ?? throw new ArgumentNullException(nameof(storage));
        _currentUser = currentUser ?? throw new ArgumentNullException(nameof(currentUser));
        _audit = audit ?? throw new ArgumentNullException(nameof(audit));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<AttachmentDto>> GetForDocumentAsync(
        DocumentType documentType,
        int documentId,
        CancellationToken cancellationToken = default)
        => _unitOfWork.Attachments.GetForDocumentAsync(documentType, documentId, cancellationToken);

    /// <inheritdoc />
    public async Task<ServiceResult<AttachmentDto>> UploadAsync(
        Stream content,
        string fileName,
        string? contentType,
        DocumentType documentType,
        int documentId,
        string? description,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(content);

        if (documentId <= 0)
        {
            return ServiceResult<AttachmentDto>.Failure("Save the document before attaching files.", -400);
        }

        StoredFile stored;

        try
        {
            stored = await _storage
                .SaveAsync(content, fileName, contentType, documentType.ToString(), cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Inventory.Common.Exceptions.ValidationException ex)
        {
            return ServiceResult<AttachmentDto>.Failure(ex.Message, -400);
        }

        // Duplicate detection by checksum, scoped to the document.
        var duplicate = await _unitOfWork.Attachments
            .FirstOrDefaultAsync(
                a => !a.IsDeleted
                     && a.DocumentType == documentType
                     && a.DocumentId == documentId
                     && a.Checksum == stored.Checksum,
                cancellationToken)
            .ConfigureAwait(false);

        if (duplicate is not null)
        {
            _storage.Delete(stored.RelativePath);
            return ServiceResult<AttachmentDto>.Failure(
                $"'{duplicate.FileName}' with identical content is already attached to this document.", -409);
        }

        var attachment = new Attachment
        {
            DocumentType = documentType,
            DocumentId = documentId,
            InwardHeaderId = documentType == DocumentType.GoodsReceiptNote ? documentId : null,
            OutwardHeaderId = documentType == DocumentType.MaterialIssue ? documentId : null,
            FileName = stored.OriginalFileName,
            StoredFileName = stored.StoredFileName,
            FilePath = stored.RelativePath,
            ContentType = stored.ContentType,
            FileSizeBytes = stored.SizeBytes,
            Checksum = stored.Checksum,
            Description = description.Truncate(300),
            UploadedBy = _currentUser.UserId
        };

        await _unitOfWork.Attachments.AddAsync(attachment, cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        await _audit.LogAsync(
            AuditAction.Create, "Attachment", attachment.Id.ToString(),
            $"Attached '{attachment.FileName}' to {documentType} {documentId}.",
            cancellationToken: cancellationToken).ConfigureAwait(false);

        return ServiceResult<AttachmentDto>.Success(new AttachmentDto
        {
            Id = attachment.Id,
            DocumentType = documentType,
            DocumentId = documentId,
            FileName = attachment.FileName,
            ContentType = attachment.ContentType,
            FileSizeBytes = attachment.FileSizeBytes,
            Description = attachment.Description,
            UploadedOn = attachment.UploadedOn,
            UploadedByName = _currentUser.FullName
        }, "File uploaded successfully.");
    }

    /// <inheritdoc />
    public async Task<(Stream Content, string ContentType, string FileName)?> DownloadAsync(
        long attachmentId,
        CancellationToken cancellationToken = default)
    {
        var attachment = await _unitOfWork.Attachments
            .GetForDownloadAsync(attachmentId, cancellationToken)
            .ConfigureAwait(false);

        if (attachment is null)
        {
            return null;
        }

        var stream = await _storage.OpenReadAsync(attachment.FilePath, cancellationToken).ConfigureAwait(false);

        if (stream is null)
        {
            _logger.LogWarning("Attachment {Id} is recorded but its file is missing at {Path}.",
                attachmentId, attachment.FilePath);
            return null;
        }

        return (stream, attachment.ContentType ?? "application/octet-stream", attachment.FileName);
    }

    /// <inheritdoc />
    public async Task<ServiceResult> DeleteAsync(long attachmentId, CancellationToken cancellationToken = default)
    {
        var attachment = await _unitOfWork.Attachments
            .GetByIdAsync(attachmentId, cancellationToken)
            .ConfigureAwait(false);

        if (attachment is null || attachment.IsDeleted)
        {
            return ServiceResult.Failure("The attachment was not found.", -404);
        }

        // The metadata row is kept (soft delete) so the audit trail still
        // explains what was removed and by whom.
        attachment.IsDeleted = true;
        _unitOfWork.Attachments.Update(attachment);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        _storage.Delete(attachment.FilePath);

        await _audit.LogAsync(
            AuditAction.Delete, "Attachment", attachmentId.ToString(),
            $"Removed attachment '{attachment.FileName}'.",
            cancellationToken: cancellationToken).ConfigureAwait(false);

        return ServiceResult.Success("Attachment removed.");
    }
}
