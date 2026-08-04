namespace Inventory.Common.Constants;

/// <summary>
/// Application-wide immutable values. Centralising them keeps magic strings out
/// of the controllers, services and views.
/// </summary>
public static class AppConstants
{
    /// <summary>Default page size used by every server-side paged grid.</summary>
    public const int DefaultPageSize = 25;

    /// <summary>Upper bound accepted from the client to protect the database.</summary>
    public const int MaxPageSize = 200;

    /// <summary>Maximum accepted upload size in bytes (10 MB).</summary>
    public const long MaxUploadSizeBytes = 10 * 1024 * 1024;

    /// <summary>Extensions accepted by the attachment module.</summary>
    public static readonly string[] AllowedUploadExtensions =
    {
        ".pdf", ".docx", ".xlsx", ".jpg", ".jpeg", ".png"
    };

    /// <summary>Content types matched against <see cref="AllowedUploadExtensions"/>.</summary>
    public static readonly string[] AllowedUploadContentTypes =
    {
        "application/pdf",
        "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        "image/jpeg",
        "image/png"
    };

    /// <summary>Sliding session lifetime in minutes.</summary>
    public const int SessionTimeoutMinutes = 30;

    /// <summary>Days after which a password must be changed.</summary>
    public const int PasswordExpiryDays = 90;

    /// <summary>Failed attempts before the account is locked out.</summary>
    public const int MaxFailedLoginAttempts = 5;

    /// <summary>Lockout duration in minutes once <see cref="MaxFailedLoginAttempts"/> is hit.</summary>
    public const int LockoutMinutes = 15;

    /// <summary>Currency symbol used on screens and reports.</summary>
    public const string CurrencySymbol = "₹";

    /// <summary>Culture used to format money and dates.</summary>
    public const string DefaultCulture = "en-IN";

    /// <summary>Date format used consistently across UI, Excel and PDF output.</summary>
    public const string DateFormat = "dd-MMM-yyyy";

    /// <summary>Date+time format used on audit and activity screens.</summary>
    public const string DateTimeFormat = "dd-MMM-yyyy HH:mm";

    /// <summary>Maximum rows accepted by the Excel import wizard in one run.</summary>
    public const int MaxImportRows = 5000;
}

/// <summary>Cache keys and durations for <c>IMemoryCache</c> backed lookups.</summary>
public static class CacheKeys
{
    public const string Categories = "lookup:categories";
    public const string Units = "lookup:units";
    public const string Vendors = "lookup:vendors";
    public const string Manufacturers = "lookup:manufacturers";
    public const string Sites = "lookup:sites";
    public const string Warehouses = "lookup:warehouses";
    public const string Departments = "lookup:departments";
    public const string Engineers = "lookup:engineers";
    public const string Couriers = "lookup:couriers";
    public const string Items = "lookup:items";

    /// <summary>Lookup lists rarely change; ten minutes is a safe default.</summary>
    public static readonly TimeSpan LookupDuration = TimeSpan.FromMinutes(10);

    /// <summary>Dashboard aggregates are refreshed more aggressively.</summary>
    public static readonly TimeSpan DashboardDuration = TimeSpan.FromMinutes(2);
}

/// <summary>
/// Names of every stored procedure invoked by the data access layer.
/// Nothing else in the solution builds a SQL string.
/// </summary>
public static class StoredProcedures
{
    // ---- Security ---------------------------------------------------------
    public const string Login = "dbo.sp_Login";
    public const string GetAuditLogs = "dbo.sp_GetAuditLogs";
    public const string InsertAuditLog = "dbo.sp_InsertAuditLog";
    public const string InsertLoginHistory = "dbo.sp_InsertLoginHistory";

    // ---- Dashboard --------------------------------------------------------
    public const string GetDashboard = "dbo.sp_GetDashboard";
    public const string GetDashboardCharts = "dbo.sp_GetDashboardCharts";
    public const string GetRecentActivities = "dbo.sp_GetRecentActivities";

    // ---- Item master ------------------------------------------------------
    public const string InsertItem = "dbo.sp_InsertItem";
    public const string UpdateItem = "dbo.sp_UpdateItem";
    public const string DeleteItem = "dbo.sp_DeleteItem";
    public const string GetItems = "dbo.sp_GetItems";
    public const string GetItemById = "dbo.sp_GetItemById";

    // ---- Vendor master ----------------------------------------------------
    public const string InsertVendor = "dbo.sp_InsertVendor";
    public const string UpdateVendor = "dbo.sp_UpdateVendor";
    public const string DeleteVendor = "dbo.sp_DeleteVendor";
    public const string GetVendors = "dbo.sp_GetVendors";
    public const string GetVendorById = "dbo.sp_GetVendorById";

    // ---- Generic (single-column) masters ----------------------------------
    public const string InsertMaster = "dbo.sp_InsertMaster";
    public const string UpdateMaster = "dbo.sp_UpdateMaster";
    public const string DeleteMaster = "dbo.sp_DeleteMaster";
    public const string GetMasters = "dbo.sp_GetMasters";

    // ---- Goods Receipt Note (inward) --------------------------------------
    public const string InsertGRN = "dbo.sp_InsertGRN";
    public const string UpdateGRN = "dbo.sp_UpdateGRN";
    public const string DeleteGRN = "dbo.sp_DeleteGRN";
    public const string ApproveGRN = "dbo.sp_ApproveGRN";
    public const string SubmitGRN = "dbo.sp_SubmitGRN";
    public const string GetGRNs = "dbo.sp_GetGRNs";
    public const string GetGRNById = "dbo.sp_GetGRNById";

    // ---- Material issue (outward) -----------------------------------------
    public const string InsertInventoryIssue = "dbo.sp_InsertInventoryIssue";
    public const string UpdateInventoryIssue = "dbo.sp_UpdateInventoryIssue";
    public const string DeleteInventoryIssue = "dbo.sp_DeleteInventoryIssue";
    public const string SubmitInventoryIssue = "dbo.sp_SubmitInventoryIssue";
    public const string ApproveInventoryIssue = "dbo.sp_ApproveInventoryIssue";
    public const string DispatchInventoryIssue = "dbo.sp_DispatchInventoryIssue";
    public const string GetInventoryIssues = "dbo.sp_GetInventoryIssues";
    public const string GetInventoryIssueById = "dbo.sp_GetInventoryIssueById";

    // ---- Stock ------------------------------------------------------------
    public const string GetCurrentStock = "dbo.sp_GetCurrentStock";
    public const string GetStockLedger = "dbo.sp_GetStockLedger";
    public const string GetLowStockItems = "dbo.sp_GetLowStockItems";
    public const string GetSiteWiseStock = "dbo.sp_GetSiteWiseStock";
    public const string GetWarehouseStock = "dbo.sp_GetWarehouseStock";
    public const string SearchInventory = "dbo.sp_SearchInventory";
    public const string GetItemStockBalance = "dbo.sp_GetItemStockBalance";

    // ---- Reports ----------------------------------------------------------
    public const string GetEngineerWiseIssue = "dbo.sp_GetEngineerWiseIssue";
    public const string GetDepartmentWiseConsumption = "dbo.sp_GetDepartmentWiseConsumption";
    public const string GetVendorPurchase = "dbo.sp_GetVendorPurchase";
    public const string GetMonthlyConsumption = "dbo.sp_GetMonthlyConsumption";
    public const string GetQuarterlyConsumption = "dbo.sp_GetQuarterlyConsumption";
    public const string GetHalfYearlyConsumption = "dbo.sp_GetHalfYearlyConsumption";
    public const string GetYearlyConsumption = "dbo.sp_GetYearlyConsumption";
    public const string GetAbcAnalysis = "dbo.sp_GetAbcAnalysis";
    public const string GetMovementAnalysis = "dbo.sp_GetMovementAnalysis";
    public const string GetDeadStock = "dbo.sp_GetDeadStock";
    public const string GetPendingApprovals = "dbo.sp_GetPendingApprovals";
    public const string GetReceiptRegister = "dbo.sp_GetReceiptRegister";
    public const string GetIssueRegister = "dbo.sp_GetIssueRegister";

    // ---- Notifications ----------------------------------------------------
    public const string InsertNotification = "dbo.sp_InsertNotification";
    public const string GetNotifications = "dbo.sp_GetNotifications";
    public const string MarkNotificationRead = "dbo.sp_MarkNotificationRead";
}
