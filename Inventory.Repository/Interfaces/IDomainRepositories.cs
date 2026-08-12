using Inventory.Common.Enums;
using Inventory.Common.Models;
using Inventory.DAL.StoredProcedures;
using Inventory.Entities.Dtos;
using Inventory.Entities.Models;

namespace Inventory.Repository.Interfaces;

/// <summary>Item master access. Writes go through stored procedures.</summary>
public interface IItemRepository : IGenericRepository<Item>
{
    /// <summary>Paged item grid, including the derived stock balance and value.</summary>
    Task<PagedResult<ItemListDto>> GetPagedItemsAsync(
        PagedRequest request,
        int? categoryId = null,
        int? itemType = null,
        bool? lowStockOnly = null,
        CancellationToken cancellationToken = default);

    /// <summary>Loads one item with its lookups resolved, ready for the edit screen.</summary>
    Task<ItemDto?> GetItemAsync(int itemId, CancellationToken cancellationToken = default);

    /// <summary>Inserts an item; the procedure allocates the item code.</summary>
    Task<SpResult> InsertItemAsync(ItemDto item, int userId, CancellationToken cancellationToken = default);

    Task<SpResult> UpdateItemAsync(ItemDto item, int userId, CancellationToken cancellationToken = default);

    /// <summary>Soft-deletes an item; refuses when stock or history exists.</summary>
    Task<SpResult> DeleteItemAsync(int itemId, int userId, CancellationToken cancellationToken = default);

    /// <summary>Type-ahead search used by the Select2 item pickers.</summary>
    Task<IReadOnlyList<ItemStockSnapshotDto>> SearchItemsForDocumentAsync(
        string? term,
        int warehouseId,
        int maxResults = 20,
        CancellationToken cancellationToken = default);

    /// <summary>Resolves a scanned barcode to an item plus its balance.</summary>
    Task<ItemStockSnapshotDto?> GetByBarcodeAsync(
        string barcode,
        int warehouseId,
        CancellationToken cancellationToken = default);

    /// <summary>True when the code is already used by another live item.</summary>
    Task<bool> IsItemCodeTakenAsync(string itemCode, int excludeId = 0, CancellationToken cancellationToken = default);
}

/// <summary>Vendor master access.</summary>
public interface IVendorRepository : IMasterRepository<Vendor>
{
    Task<PagedResult<VendorDto>> GetPagedVendorsAsync(
        PagedRequest request,
        bool? blacklistedOnly = null,
        CancellationToken cancellationToken = default);

    Task<VendorDto?> GetVendorAsync(int vendorId, CancellationToken cancellationToken = default);

    Task<SpResult> InsertVendorAsync(VendorDto vendor, int userId, CancellationToken cancellationToken = default);

    Task<SpResult> UpdateVendorAsync(VendorDto vendor, int userId, CancellationToken cancellationToken = default);

    Task<SpResult> DeleteVendorAsync(int vendorId, int userId, CancellationToken cancellationToken = default);

    Task<bool> IsGstNumberTakenAsync(string gstNumber, int excludeId = 0, CancellationToken cancellationToken = default);
}

/// <summary>
/// Inventory document access: goods receipts, material issues, approvals and
/// the stock ledger. Every method here maps to exactly one stored procedure so
/// the multi-table, multi-row postings stay atomic inside SQL Server.
/// </summary>
public interface IInventoryRepository
{
    // ---- Goods Receipt Note ----------------------------------------------
    Task<PagedResult<GrnListDto>> GetPagedGrnsAsync(
        PagedRequest request,
        ReportFilter filter,
        CancellationToken cancellationToken = default);

    Task<GrnDto?> GetGrnAsync(int grnId, CancellationToken cancellationToken = default);

    Task<SpResult> SaveGrnAsync(GrnDto grn, int userId, bool isUpdate, CancellationToken cancellationToken = default);

    Task<SpResult> DeleteGrnAsync(int grnId, int userId, CancellationToken cancellationToken = default);

    Task<SpResult> SubmitGrnAsync(int grnId, int userId, string? remarks, CancellationToken cancellationToken = default);

    /// <summary>Approves or rejects a GRN and, on approval, posts the stock.</summary>
    Task<SpResult> ApproveGrnAsync(
        int grnId,
        int userId,
        bool approve,
        string? remarks,
        CancellationToken cancellationToken = default);

    // ---- Material Issue ---------------------------------------------------
    Task<PagedResult<IssueListDto>> GetPagedIssuesAsync(
        PagedRequest request,
        ReportFilter filter,
        CancellationToken cancellationToken = default);

    Task<IssueDto?> GetIssueAsync(int issueId, CancellationToken cancellationToken = default);

    Task<SpResult> SaveIssueAsync(IssueDto issue, int userId, bool isUpdate, CancellationToken cancellationToken = default);

    Task<SpResult> DeleteIssueAsync(int issueId, int userId, CancellationToken cancellationToken = default);

    /// <summary>Submits for approval and reserves the requested stock.</summary>
    Task<SpResult> SubmitIssueAsync(int issueId, int userId, string? remarks, CancellationToken cancellationToken = default);

    /// <summary>Approves (optionally with reduced quantities) or rejects an issue.</summary>
    Task<SpResult> ApproveIssueAsync(
        ApprovalRequestDto request,
        int userId,
        CancellationToken cancellationToken = default);

    /// <summary>Records the physical hand-over and posts the outward ledger rows.</summary>
    Task<SpResult> DispatchIssueAsync(
        DispatchRequestDto request,
        int userId,
        CancellationToken cancellationToken = default);

    // ---- Stock ------------------------------------------------------------
    Task<PagedResult<CurrentStockDto>> GetCurrentStockAsync(
        PagedRequest request,
        ReportFilter filter,
        CancellationToken cancellationToken = default);

    Task<PagedResult<StockLedgerDto>> GetStockLedgerAsync(
        PagedRequest request,
        ReportFilter filter,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<LowStockDto>> GetLowStockItemsAsync(
        int? warehouseId = null,
        int maxRows = 100,
        CancellationToken cancellationToken = default);

    /// <summary>Physical, reserved and available balance of one item in one warehouse.</summary>
    Task<ItemStockSnapshotDto?> GetItemBalanceAsync(
        int itemId,
        int warehouseId,
        CancellationToken cancellationToken = default);

    // ---- Approvals --------------------------------------------------------
    Task<IReadOnlyList<PendingApprovalDto>> GetPendingApprovalsAsync(
        int userId,
        DocumentType? documentType = null,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ApprovalHistoryDto>> GetApprovalTrailAsync(
        DocumentType documentType,
        int documentId,
        CancellationToken cancellationToken = default);
}

/// <summary>Read-only reporting access. Every method wraps one stored procedure.</summary>
public interface IReportRepository
{
    Task<IReadOnlyList<CurrentStockDto>> GetCurrentStockAsync(ReportFilter filter, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<StockLedgerDto>> GetStockLedgerAsync(ReportFilter filter, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<LocationStockDto>> GetSiteWiseStockAsync(ReportFilter filter, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<LocationStockDto>> GetWarehouseStockAsync(ReportFilter filter, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<VendorPurchaseDto>> GetVendorPurchaseAsync(ReportFilter filter, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ConsumptionDto>> GetEngineerWiseIssueAsync(ReportFilter filter, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ConsumptionDto>> GetDepartmentWiseConsumptionAsync(ReportFilter filter, CancellationToken cancellationToken = default);

    /// <summary>
    /// One procedure serves the monthly, quarterly, half-yearly and yearly
    /// reports; <paramref name="procedureName"/> selects the grain.
    /// </summary>
    Task<IReadOnlyList<PeriodConsumptionDto>> GetPeriodConsumptionAsync(
        string procedureName,
        ReportFilter filter,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AbcAnalysisDto>> GetAbcAnalysisAsync(ReportFilter filter, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<MovementAnalysisDto>> GetMovementAnalysisAsync(
        ReportFilter filter,
        MovementCategory? category = null,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<MovementAnalysisDto>> GetDeadStockAsync(ReportFilter filter, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<LowStockDto>> GetLowStockAsync(ReportFilter filter, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ReceiptRegisterDto>> GetReceiptRegisterAsync(ReportFilter filter, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<IssueRegisterDto>> GetIssueRegisterAsync(ReportFilter filter, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PendingApprovalDto>> GetPendingApprovalsAsync(ReportFilter filter, CancellationToken cancellationToken = default);
}

/// <summary>Dashboard aggregates, produced by a single multi-result procedure.</summary>
public interface IDashboardRepository
{
    Task<DashboardDto> GetDashboardAsync(int userId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<RecentActivityDto>> GetRecentActivitiesAsync(
        int maxRows = 15,
        CancellationToken cancellationToken = default);
}

/// <summary>Audit trail and login history access.</summary>
public interface IAuditRepository
{
    Task<PagedResult<AuditLogDto>> GetAuditLogsAsync(
        PagedRequest request,
        DateTime? fromDate,
        DateTime? toDate,
        int? userId,
        string? entityName,
        int? action,
        CancellationToken cancellationToken = default);

    Task<AuditLogDto?> GetAuditLogAsync(long id, CancellationToken cancellationToken = default);

    /// <summary>Writes one audit row. Never throws: auditing must not break the caller.</summary>
    Task WriteAsync(AuditLog entry, CancellationToken cancellationToken = default);

    Task<PagedResult<LoginHistoryDto>> GetLoginHistoryAsync(
        PagedRequest request,
        DateTime? fromDate,
        DateTime? toDate,
        CancellationToken cancellationToken = default);

    Task RecordLoginAsync(LoginHistory entry, CancellationToken cancellationToken = default);
}

/// <summary>Notification persistence.</summary>
public interface INotificationRepository : IGenericRepository<Notification>
{
    Task<IReadOnlyList<NotificationDto>> GetForUserAsync(
        int userId,
        bool unreadOnly = false,
        int maxRows = 20,
        CancellationToken cancellationToken = default);

    Task<int> GetUnreadCountAsync(int userId, CancellationToken cancellationToken = default);

    Task<SpResult> MarkAsReadAsync(long notificationId, int userId, CancellationToken cancellationToken = default);

    Task<SpResult> MarkAllAsReadAsync(int userId, CancellationToken cancellationToken = default);

    /// <summary>Fans a role-targeted notification out to every active member of the role.</summary>
    Task<SpResult> CreateAsync(Notification notification, CancellationToken cancellationToken = default);
}

/// <summary>Attachment metadata persistence (bytes live on disk).</summary>
public interface IAttachmentRepository : IGenericRepository<Attachment>
{
    Task<IReadOnlyList<AttachmentDto>> GetForDocumentAsync(
        DocumentType documentType,
        int documentId,
        CancellationToken cancellationToken = default);

    Task<Attachment?> GetForDownloadAsync(long attachmentId, CancellationToken cancellationToken = default);
}

/// <summary>Access to the small "code + name" masters through one shared contract.</summary>
/// <typeparam name="TEntity">Concrete master entity.</typeparam>
public interface IMasterRepository<TEntity> : IGenericRepository<TEntity> where TEntity : MasterEntity
{
    /// <summary>Active rows projected for a drop-down, ordered by display order then name.</summary>
    Task<IReadOnlyList<LookupDto>> GetLookupAsync(CancellationToken cancellationToken = default);

    /// <summary>True when another live row already uses the code.</summary>
    Task<bool> IsCodeTakenAsync(string code, int excludeId = 0, CancellationToken cancellationToken = default);

    /// <summary>Next code in the master's own series, e.g. <c>CAT-0007</c>.</summary>
    Task<string> GenerateCodeAsync(string prefix, CancellationToken cancellationToken = default);
}

/// <summary>Warehouse master access; adds the site-scoped lookup for cascading drop-downs.</summary>
public interface IWarehouseRepository : IMasterRepository<Warehouse>
{
    /// <summary>Active warehouses, optionally limited to one site.</summary>
    Task<IReadOnlyList<LookupDto>> GetLookupBySiteAsync(int? siteId, CancellationToken cancellationToken = default);
}

/// <summary>Engineer master access; adds the department-scoped lookup.</summary>
public interface IEngineerRepository : IMasterRepository<Engineer>
{
    /// <summary>Active engineers, optionally limited to one department.</summary>
    Task<IReadOnlyList<LookupDto>> GetLookupByDepartmentAsync(
        int? departmentId,
        CancellationToken cancellationToken = default);
}
