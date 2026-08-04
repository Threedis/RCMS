using System.Data;
using Dapper;
using Inventory.Common.Constants;
using Inventory.Common.Enums;
using Inventory.Common.Models;
using Inventory.DAL.Context;
using Inventory.DAL.StoredProcedures;
using Inventory.Entities.Dtos;
using Inventory.Entities.Models;
using Inventory.Repository.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Inventory.Repository.Implementations;

/// <summary>
/// Audit trail repository.
/// <para>
/// Audit writing must never be able to fail the operation being audited, so
/// <see cref="WriteAsync"/> swallows and logs its own errors. Reads go through
/// a paged stored procedure because the table grows large.
/// </para>
/// </summary>
public sealed class AuditRepository : IAuditRepository
{
    private readonly ApplicationDbContext _context;
    private readonly IStoredProcedureExecutor _executor;
    private readonly ILogger<AuditRepository> _logger;

    public AuditRepository(
        ApplicationDbContext context,
        IStoredProcedureExecutor executor,
        ILogger<AuditRepository> logger)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _executor = executor ?? throw new ArgumentNullException(nameof(executor));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task<PagedResult<AuditLogDto>> GetAuditLogsAsync(
        PagedRequest request,
        DateTime? fromDate,
        DateTime? toDate,
        int? userId,
        string? entityName,
        int? action,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var parameters = new DynamicParameters();
        parameters.Add("@PageNumber", request.PageNumber);
        parameters.Add("@PageSize", request.PageSize);
        parameters.Add("@SearchTerm", request.SearchTerm);
        parameters.Add("@SortColumn", request.SortColumn);
        parameters.Add("@SortDirection", request.SafeSortDirection);
        parameters.Add("@FromDate", fromDate, DbType.DateTime2);
        parameters.Add("@ToDate", toDate, DbType.DateTime2);
        parameters.Add("@UserId", userId);
        parameters.Add("@EntityName", entityName);
        parameters.Add("@Action", action);

        var (items, total) = await _executor
            .QueryPagedAsync<AuditLogDto>(StoredProcedures.GetAuditLogs, parameters, cancellationToken)
            .ConfigureAwait(false);

        return new PagedResult<AuditLogDto>(items, total, request.PageNumber, request.PageSize);
    }

    /// <inheritdoc />
    public async Task<AuditLogDto?> GetAuditLogAsync(long id, CancellationToken cancellationToken = default)
        => await _context.AuditLogs
            .AsNoTracking()
            .Where(a => a.Id == id)
            .Select(a => new AuditLogDto
            {
                Id = a.Id,
                CreatedOn = a.CreatedOn,
                ActionName = a.Action.ToString(),
                EntityName = a.EntityName,
                EntityId = a.EntityId,
                Description = a.Description,
                UserName = a.UserName,
                IpAddress = a.IpAddress,
                Source = a.Source,
                IsSuccessful = a.IsSuccessful,
                ErrorMessage = a.ErrorMessage,
                OldValues = a.OldValues,
                NewValues = a.NewValues
            })
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

    /// <inheritdoc />
    public async Task WriteAsync(AuditLog entry, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entry);

        try
        {
            var parameters = new DynamicParameters();
            parameters.Add("@Action", (int)entry.Action);
            parameters.Add("@EntityName", entry.EntityName);
            parameters.Add("@EntityId", entry.EntityId);
            parameters.Add("@Description", entry.Description);
            parameters.Add("@OldValues", entry.OldValues);
            parameters.Add("@NewValues", entry.NewValues);
            parameters.Add("@UserId", entry.UserId);
            parameters.Add("@UserName", entry.UserName);
            parameters.Add("@IpAddress", entry.IpAddress);
            parameters.Add("@UserAgent", entry.UserAgent);
            parameters.Add("@Source", entry.Source);
            parameters.Add("@IsSuccessful", entry.IsSuccessful);
            parameters.Add("@ErrorMessage", entry.ErrorMessage);

            await _executor.ExecuteAsync(StoredProcedures.InsertAuditLog, parameters, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            // Auditing is important but it must never break the business
            // operation that triggered it.
            _logger.LogError(ex, "Failed to write audit entry for {Entity} {EntityId}.",
                entry.EntityName, entry.EntityId);
        }
    }

    /// <inheritdoc />
    public async Task<PagedResult<LoginHistoryDto>> GetLoginHistoryAsync(
        PagedRequest request,
        DateTime? fromDate,
        DateTime? toDate,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var query = _context.LoginHistories.AsNoTracking().AsQueryable();

        if (fromDate.HasValue)
        {
            query = query.Where(l => l.LoginOn >= fromDate.Value);
        }

        if (toDate.HasValue)
        {
            var inclusiveEnd = toDate.Value.Date.AddDays(1);
            query = query.Where(l => l.LoginOn < inclusiveEnd);
        }

        if (!string.IsNullOrWhiteSpace(request.SearchTerm))
        {
            var term = request.SearchTerm.Trim();
            query = query.Where(l => l.UserName.Contains(term) || (l.IpAddress != null && l.IpAddress.Contains(term)));
        }

        var total = await query.CountAsync(cancellationToken).ConfigureAwait(false);

        var items = await query
            .OrderByDescending(l => l.LoginOn)
            .Skip(request.Skip)
            .Take(request.PageSize)
            .Select(l => new LoginHistoryDto
            {
                Id = l.Id,
                UserName = l.UserName,
                FullName = l.User != null ? l.User.FullName : null,
                LoginOn = l.LoginOn,
                LogoutOn = l.LogoutOn,
                IsSuccessful = l.IsSuccessful,
                FailureReason = l.FailureReason,
                IpAddress = l.IpAddress,
                UserAgent = l.UserAgent
            })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return new PagedResult<LoginHistoryDto>(items, total, request.PageNumber, request.PageSize);
    }

    /// <inheritdoc />
    public async Task RecordLoginAsync(LoginHistory entry, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entry);

        try
        {
            var parameters = new DynamicParameters();
            parameters.Add("@UserId", entry.UserId);
            parameters.Add("@UserName", entry.UserName);
            parameters.Add("@IsSuccessful", entry.IsSuccessful);
            parameters.Add("@FailureReason", entry.FailureReason);
            parameters.Add("@IpAddress", entry.IpAddress);
            parameters.Add("@UserAgent", entry.UserAgent);
            parameters.Add("@SessionId", entry.SessionId);

            await _executor.ExecuteAsync(StoredProcedures.InsertLoginHistory, parameters, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to record login history for {UserName}.", entry.UserName);
        }
    }
}

/// <summary>Notification repository.</summary>
public sealed class NotificationRepository : GenericRepository<Notification>, INotificationRepository
{
    private readonly IStoredProcedureExecutor _executor;

    public NotificationRepository(ApplicationDbContext context, IStoredProcedureExecutor executor)
        : base(context)
    {
        _executor = executor ?? throw new ArgumentNullException(nameof(executor));
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<NotificationDto>> GetForUserAsync(
        int userId,
        bool unreadOnly = false,
        int maxRows = 20,
        CancellationToken cancellationToken = default)
        => await _executor.QueryAsync<NotificationDto>(
            StoredProcedures.GetNotifications,
            new { UserId = userId, UnreadOnly = unreadOnly, MaxRows = maxRows },
            cancellationToken).ConfigureAwait(false);

    /// <inheritdoc />
    public async Task<int> GetUnreadCountAsync(int userId, CancellationToken cancellationToken = default)
        => await DbSet.AsNoTracking()
            .CountAsync(n => n.UserId == userId && !n.IsRead, cancellationToken)
            .ConfigureAwait(false);

    /// <inheritdoc />
    public async Task<SpResult> MarkAsReadAsync(long notificationId, int userId, CancellationToken cancellationToken = default)
        => await _executor.ExecuteAsync(
            StoredProcedures.MarkNotificationRead,
            new { NotificationId = notificationId, UserId = userId, MarkAll = false },
            cancellationToken).ConfigureAwait(false);

    /// <inheritdoc />
    public async Task<SpResult> MarkAllAsReadAsync(int userId, CancellationToken cancellationToken = default)
        => await _executor.ExecuteAsync(
            StoredProcedures.MarkNotificationRead,
            new { NotificationId = (long?)null, UserId = userId, MarkAll = true },
            cancellationToken).ConfigureAwait(false);

    /// <inheritdoc />
    public async Task<SpResult> CreateAsync(Notification notification, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(notification);

        var parameters = new DynamicParameters();
        parameters.Add("@UserId", notification.UserId);
        parameters.Add("@TargetRole", notification.TargetRole);
        parameters.Add("@NotificationType", (int)notification.NotificationType);
        parameters.Add("@Title", notification.Title);
        // The procedure names this parameter @Body: @Message is reserved for the
        // shared output contract every write procedure implements.
        parameters.Add("@Body", notification.Message);
        parameters.Add("@ActionUrl", notification.ActionUrl);
        parameters.Add("@DocumentType", (int?)notification.DocumentType);
        parameters.Add("@DocumentId", notification.DocumentId);
        parameters.Add("@CurrentUserId", notification.CreatedBy);

        return await _executor
            .ExecuteAsync(StoredProcedures.InsertNotification, parameters, cancellationToken)
            .ConfigureAwait(false);
    }
}

/// <summary>Attachment metadata repository. File bytes are handled by the storage service.</summary>
public sealed class AttachmentRepository : GenericRepository<Attachment>, IAttachmentRepository
{
    public AttachmentRepository(ApplicationDbContext context)
        : base(context)
    {
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<AttachmentDto>> GetForDocumentAsync(
        DocumentType documentType,
        int documentId,
        CancellationToken cancellationToken = default)
        => await DbSet.AsNoTracking()
            .Where(a => !a.IsDeleted && a.DocumentType == documentType && a.DocumentId == documentId)
            .OrderByDescending(a => a.UploadedOn)
            .Select(a => new AttachmentDto
            {
                Id = a.Id,
                DocumentType = a.DocumentType,
                DocumentId = a.DocumentId,
                FileName = a.FileName,
                ContentType = a.ContentType,
                FileSizeBytes = a.FileSizeBytes,
                Description = a.Description,
                UploadedOn = a.UploadedOn
            })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    /// <inheritdoc />
    public async Task<Attachment?> GetForDownloadAsync(long attachmentId, CancellationToken cancellationToken = default)
        => await DbSet.AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == attachmentId && !a.IsDeleted, cancellationToken)
            .ConfigureAwait(false);
}
