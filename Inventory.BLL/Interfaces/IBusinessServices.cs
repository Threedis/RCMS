using Inventory.Common.Enums;
using Inventory.Common.Models;
using Inventory.Entities.Dtos;
using Inventory.Entities.Models;
using Inventory.Services.Export;

namespace Inventory.BLL.Interfaces;

/// <summary>
/// CRUD contract shared by the simple masters. One generic service backs
/// Category, Unit, Department, Site, Warehouse, Manufacturer and Courier.
/// </summary>
/// <typeparam name="TDto">DTO exposed to the presentation layer.</typeparam>
public interface IMasterService<TDto> where TDto : MasterDto
{
    Task<PagedResult<TDto>> GetPagedAsync(PagedRequest request, CancellationToken cancellationToken = default);

    Task<TDto?> GetByIdAsync(int id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<LookupDto>> GetLookupAsync(CancellationToken cancellationToken = default);

    Task<ServiceResult<int>> CreateAsync(TDto dto, CancellationToken cancellationToken = default);

    Task<ServiceResult> UpdateAsync(TDto dto, CancellationToken cancellationToken = default);

    Task<ServiceResult> DeleteAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>Remote validation endpoint for the unique code rule.</summary>
    Task<bool> IsCodeAvailableAsync(string code, int excludeId = 0, CancellationToken cancellationToken = default);

    /// <summary>Builds the export definition for the master's list screen.</summary>
    Task<FileExportResult> ExportAsync(ExportFormat format, CancellationToken cancellationToken = default);
}

/// <summary>Item master business service.</summary>
public interface IItemService
{
    Task<PagedResult<ItemListDto>> GetPagedAsync(
        PagedRequest request,
        int? categoryId = null,
        int? itemType = null,
        bool? lowStockOnly = null,
        CancellationToken cancellationToken = default);

    Task<ItemDto?> GetByIdAsync(int id, CancellationToken cancellationToken = default);

    Task<ServiceResult<int>> CreateAsync(ItemDto dto, CancellationToken cancellationToken = default);

    Task<ServiceResult> UpdateAsync(ItemDto dto, CancellationToken cancellationToken = default);

    Task<ServiceResult> DeleteAsync(int id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<LookupDto>> GetLookupAsync(CancellationToken cancellationToken = default);

    /// <summary>Type-ahead search for the document line pickers.</summary>
    Task<IReadOnlyList<ItemStockSnapshotDto>> SearchForDocumentAsync(
        string? term,
        int warehouseId,
        CancellationToken cancellationToken = default);

    Task<ItemStockSnapshotDto?> ResolveBarcodeAsync(
        string barcode,
        int warehouseId,
        CancellationToken cancellationToken = default);

    Task<bool> IsItemCodeAvailableAsync(string code, int excludeId = 0, CancellationToken cancellationToken = default);

    Task<FileExportResult> ExportAsync(
        ExportFormat format,
        int? categoryId = null,
        CancellationToken cancellationToken = default);

    /// <summary>Blank workbook with the columns the item import expects.</summary>
    FileExportResult BuildImportTemplate();

    /// <summary>
    /// Validates, and optionally commits, an uploaded item workbook.
    /// </summary>
    /// <param name="content">Uploaded stream.</param>
    /// <param name="fileName">Original file name, echoed in the result.</param>
    /// <param name="commit">False performs a dry run and reports what would happen.</param>
    Task<ImportResultDto> ImportAsync(
        Stream content,
        string fileName,
        bool commit,
        CancellationToken cancellationToken = default);
}

/// <summary>Vendor master business service.</summary>
public interface IVendorService
{
    Task<PagedResult<VendorDto>> GetPagedAsync(
        PagedRequest request,
        bool? blacklistedOnly = null,
        CancellationToken cancellationToken = default);

    Task<VendorDto?> GetByIdAsync(int id, CancellationToken cancellationToken = default);

    Task<ServiceResult<int>> CreateAsync(VendorDto dto, CancellationToken cancellationToken = default);

    Task<ServiceResult> UpdateAsync(VendorDto dto, CancellationToken cancellationToken = default);

    Task<ServiceResult> DeleteAsync(int id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<LookupDto>> GetLookupAsync(CancellationToken cancellationToken = default);

    Task<bool> IsCodeAvailableAsync(string code, int excludeId = 0, CancellationToken cancellationToken = default);

    Task<bool> IsGstAvailableAsync(string gst, int excludeId = 0, CancellationToken cancellationToken = default);

    Task<FileExportResult> ExportAsync(ExportFormat format, CancellationToken cancellationToken = default);
}

/// <summary>Goods Receipt Note business service.</summary>
public interface IGrnService
{
    Task<PagedResult<GrnListDto>> GetPagedAsync(
        PagedRequest request,
        ReportFilter filter,
        CancellationToken cancellationToken = default);

    Task<GrnDto?> GetByIdAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>Creates or updates a draft.</summary>
    Task<ServiceResult<int>> SaveAsync(GrnDto dto, CancellationToken cancellationToken = default);

    Task<ServiceResult> DeleteAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>Moves a draft to Pending Approval and notifies the approvers.</summary>
    Task<ServiceResult> SubmitAsync(int id, string? remarks, CancellationToken cancellationToken = default);

    /// <summary>Approves (posting stock) or rejects the document.</summary>
    Task<ServiceResult> ApproveAsync(
        int id,
        bool approve,
        string? remarks,
        CancellationToken cancellationToken = default);

    Task<FileExportResult> ExportAsync(
        ExportFormat format,
        ReportFilter filter,
        CancellationToken cancellationToken = default);

    /// <summary>Renders one GRN as a printable PDF.</summary>
    Task<FileExportResult?> PrintAsync(int id, CancellationToken cancellationToken = default);
}

/// <summary>Material Issue business service.</summary>
public interface IIssueService
{
    Task<PagedResult<IssueListDto>> GetPagedAsync(
        PagedRequest request,
        ReportFilter filter,
        CancellationToken cancellationToken = default);

    Task<IssueDto?> GetByIdAsync(int id, CancellationToken cancellationToken = default);

    Task<ServiceResult<int>> SaveAsync(IssueDto dto, CancellationToken cancellationToken = default);

    Task<ServiceResult> DeleteAsync(int id, CancellationToken cancellationToken = default);

    Task<ServiceResult> SubmitAsync(int id, string? remarks, CancellationToken cancellationToken = default);

    Task<ServiceResult> ApproveAsync(ApprovalRequestDto request, CancellationToken cancellationToken = default);

    /// <summary>Records the hand-over, deducts stock and closes the loop.</summary>
    Task<ServiceResult> DispatchAsync(DispatchRequestDto request, CancellationToken cancellationToken = default);

    Task<FileExportResult> ExportAsync(
        ExportFormat format,
        ReportFilter filter,
        CancellationToken cancellationToken = default);

    Task<FileExportResult?> PrintAsync(int id, CancellationToken cancellationToken = default);
}

/// <summary>Approval queue shared by both document types.</summary>
public interface IApprovalService
{
    Task<IReadOnlyList<PendingApprovalDto>> GetPendingAsync(
        DocumentType? documentType = null,
        CancellationToken cancellationToken = default);

    Task<int> GetPendingCountAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ApprovalHistoryDto>> GetTrailAsync(
        DocumentType documentType,
        int documentId,
        CancellationToken cancellationToken = default);

    /// <summary>Routes an approve/reject decision to the right document service.</summary>
    Task<ServiceResult> DecideAsync(ApprovalRequestDto request, CancellationToken cancellationToken = default);
}

/// <summary>Stock enquiry service.</summary>
public interface IStockService
{
    Task<PagedResult<CurrentStockDto>> GetCurrentStockAsync(
        PagedRequest request,
        ReportFilter filter,
        CancellationToken cancellationToken = default);

    Task<PagedResult<StockLedgerDto>> GetLedgerAsync(
        PagedRequest request,
        ReportFilter filter,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<LowStockDto>> GetLowStockAsync(
        int? warehouseId = null,
        CancellationToken cancellationToken = default);

    Task<ItemStockSnapshotDto?> GetBalanceAsync(
        int itemId,
        int warehouseId,
        CancellationToken cancellationToken = default);
}

/// <summary>Executive dashboard service.</summary>
public interface IDashboardService
{
    Task<DashboardDto> GetDashboardAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<RecentActivityDto>> GetRecentActivitiesAsync(
        int maxRows = 15,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Reporting service. Each report is identified by a key so the controller,
/// the menu and the export endpoint all address it the same way.
/// </summary>
public interface IReportService
{
    /// <summary>Every report the user may run, in menu order.</summary>
    IReadOnlyList<ReportDescriptor> GetCatalog();

    /// <summary>Runs a report and returns its rows plus the column layout.</summary>
    Task<ReportOutput> RunAsync(string reportKey, ReportFilter filter, CancellationToken cancellationToken = default);

    /// <summary>Runs a report and renders it to a file.</summary>
    Task<FileExportResult> ExportAsync(
        string reportKey,
        ReportFilter filter,
        ExportFormat format,
        CancellationToken cancellationToken = default);
}

/// <summary>Metadata describing one report in the catalog.</summary>
public sealed class ReportDescriptor
{
    public required string Key { get; init; }

    public required string Title { get; init; }

    public required string Group { get; init; }

    public string? Description { get; init; }

    public string Icon { get; init; } = "bi-file-earmark-bar-graph";

    /// <summary>Filter controls the screen should render for this report.</summary>
    public IReadOnlyList<string> Filters { get; init; } = Array.Empty<string>();
}

/// <summary>Rows plus column headings, ready for the generic report grid.</summary>
public sealed class ReportOutput
{
    public required string Title { get; init; }

    public required IReadOnlyList<string> Columns { get; init; }

    /// <summary>Rows as ordered cell values, aligned with <see cref="Columns"/>.</summary>
    public required IReadOnlyList<IReadOnlyList<string>> Rows { get; init; }

    /// <summary>Column indexes that should be right-aligned in the grid.</summary>
    public IReadOnlyList<int> NumericColumns { get; init; } = Array.Empty<int>();

    public int TotalRows => Rows.Count;
}

/// <summary>Notification service.</summary>
public interface INotificationService
{
    Task<IReadOnlyList<NotificationDto>> GetForCurrentUserAsync(
        bool unreadOnly = false,
        int maxRows = 20,
        CancellationToken cancellationToken = default);

    Task<int> GetUnreadCountAsync(CancellationToken cancellationToken = default);

    Task<ServiceResult> MarkReadAsync(long id, CancellationToken cancellationToken = default);

    Task<ServiceResult> MarkAllReadAsync(CancellationToken cancellationToken = default);

    /// <summary>Raises an in-app notification and, when enabled, mirrors it to e-mail.</summary>
    Task NotifyAsync(
        NotificationType type,
        string title,
        string message,
        string? actionUrl = null,
        int? userId = null,
        string? targetRole = null,
        DocumentType? documentType = null,
        int? documentId = null,
        CancellationToken cancellationToken = default);
}

/// <summary>Audit trail service.</summary>
public interface IAuditService
{
    Task<PagedResult<AuditLogDto>> GetLogsAsync(
        PagedRequest request,
        DateTime? fromDate,
        DateTime? toDate,
        int? userId,
        string? entityName,
        int? action,
        CancellationToken cancellationToken = default);

    Task<AuditLogDto?> GetLogAsync(long id, CancellationToken cancellationToken = default);

    Task<PagedResult<LoginHistoryDto>> GetLoginHistoryAsync(
        PagedRequest request,
        DateTime? fromDate,
        DateTime? toDate,
        CancellationToken cancellationToken = default);

    /// <summary>Records one audited operation.</summary>
    Task LogAsync(
        AuditAction action,
        string? entityName = null,
        string? entityId = null,
        string? description = null,
        string? oldValues = null,
        string? newValues = null,
        bool isSuccessful = true,
        string? errorMessage = null,
        string? source = null,
        CancellationToken cancellationToken = default);

    Task RecordLoginAsync(
        int? userId,
        string userName,
        bool successful,
        string? failureReason = null,
        CancellationToken cancellationToken = default);

    Task<FileExportResult> ExportAsync(
        ExportFormat format,
        DateTime? fromDate,
        DateTime? toDate,
        CancellationToken cancellationToken = default);
}

/// <summary>Attachment service: validates, stores and records uploaded files.</summary>
public interface IAttachmentService
{
    Task<IReadOnlyList<AttachmentDto>> GetForDocumentAsync(
        DocumentType documentType,
        int documentId,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<AttachmentDto>> UploadAsync(
        Stream content,
        string fileName,
        string? contentType,
        DocumentType documentType,
        int documentId,
        string? description,
        CancellationToken cancellationToken = default);

    /// <summary>Opens a stored file, returning null when it is missing or deleted.</summary>
    Task<(Stream Content, string ContentType, string FileName)?> DownloadAsync(
        long attachmentId,
        CancellationToken cancellationToken = default);

    Task<ServiceResult> DeleteAsync(long attachmentId, CancellationToken cancellationToken = default);
}

/// <summary>Application user administration.</summary>
public interface IUserService
{
    Task<PagedResult<UserDto>> GetPagedAsync(PagedRequest request, CancellationToken cancellationToken = default);

    Task<UserDto?> GetByIdAsync(int id, CancellationToken cancellationToken = default);

    Task<ServiceResult<int>> CreateAsync(UserDto dto, CancellationToken cancellationToken = default);

    Task<ServiceResult> UpdateAsync(UserDto dto, CancellationToken cancellationToken = default);

    /// <summary>Deactivates a user; accounts are never physically removed.</summary>
    Task<ServiceResult> DeactivateAsync(int id, CancellationToken cancellationToken = default);

    Task<ServiceResult> ResetPasswordAsync(int id, string newPassword, CancellationToken cancellationToken = default);

    Task<ServiceResult> UnlockAsync(int id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<string>> GetRolesAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<LookupDto>> GetApproverLookupAsync(CancellationToken cancellationToken = default);
}

/// <summary>Supplies every drop-down list the UI needs, with caching.</summary>
public interface ILookupService
{
    Task<IReadOnlyList<LookupDto>> CategoriesAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<LookupDto>> UnitsAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<LookupDto>> DepartmentsAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<LookupDto>> SitesAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<LookupDto>> WarehousesAsync(int? siteId = null, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<LookupDto>> ManufacturersAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<LookupDto>> CouriersAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<LookupDto>> EngineersAsync(int? departmentId = null, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<LookupDto>> VendorsAsync(CancellationToken cancellationToken = default);

    /// <summary>Drops every cached lookup; called whenever master data changes.</summary>
    void InvalidateAll();
}
