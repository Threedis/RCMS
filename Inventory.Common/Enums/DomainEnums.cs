namespace Inventory.Common.Enums;

/// <summary>
/// Lifecycle of an inventory document (Goods Receipt Note / Material Issue).
/// The numeric values are persisted in SQL Server and MUST NOT be re-ordered.
/// </summary>
public enum DocumentStatus
{
    /// <summary>Saved but not yet submitted; fully editable, no stock impact.</summary>
    Draft = 1,

    /// <summary>Submitted and waiting for an approver.</summary>
    PendingApproval = 2,

    /// <summary>Approved. Stock has been posted to the ledger.</summary>
    Approved = 3,

    /// <summary>Rejected by an approver; may be re-opened to Draft.</summary>
    Rejected = 4,

    /// <summary>Material physically issued/dispatched (outward only).</summary>
    Issued = 5,

    /// <summary>Document closed; no further changes allowed.</summary>
    Closed = 6,

    /// <summary>Cancelled before approval; reversal entries posted if required.</summary>
    Cancelled = 7
}

/// <summary>Direction of a stock ledger movement.</summary>
public enum MovementType
{
    /// <summary>Goods received into a warehouse (increases stock).</summary>
    Inward = 1,

    /// <summary>Material issued out of a warehouse (decreases stock).</summary>
    Outward = 2,

    /// <summary>Opening balance posted at go-live.</summary>
    Opening = 3,

    /// <summary>Manual/physical stock adjustment (can be either sign).</summary>
    Adjustment = 4,

    /// <summary>Reversal of a previously posted document.</summary>
    Reversal = 5,

    /// <summary>Transfer between warehouses (posted as a pair of rows).</summary>
    Transfer = 6
}

/// <summary>Classification used for consumption reporting and ABC analysis.</summary>
public enum ItemType
{
    /// <summary>Repairable / rotable spare part.</summary>
    SparePart = 1,

    /// <summary>Consumed on use, not returnable.</summary>
    Consumable = 2,

    /// <summary>Tool issued to an engineer and expected back.</summary>
    Tool = 3,

    /// <summary>Capital asset tracked by serial number.</summary>
    Asset = 4,

    /// <summary>Raw material used in fabrication.</summary>
    RawMaterial = 5
}

/// <summary>Action recorded on the approval trail.</summary>
public enum ApprovalAction
{
    Submitted = 1,
    Approved = 2,
    Rejected = 3,
    Cancelled = 4,
    ReOpened = 5,
    Issued = 6,
    Closed = 7
}

/// <summary>The kind of document an approval/notification refers to.</summary>
public enum DocumentType
{
    GoodsReceiptNote = 1,
    MaterialIssue = 2,
    StockAdjustment = 3
}

/// <summary>Category of an in-app / e-mail notification.</summary>
public enum NotificationType
{
    Information = 1,
    LowStock = 2,
    PendingApproval = 3,
    Approved = 4,
    Rejected = 5,
    MaterialReceipt = 6,
    MaterialDispatch = 7,
    SystemAlert = 8
}

/// <summary>Operation captured in the audit trail.</summary>
public enum AuditAction
{
    Login = 1,
    Logout = 2,
    LoginFailed = 3,
    Create = 4,
    Update = 5,
    Delete = 6,
    Approve = 7,
    Reject = 8,
    Submit = 9,
    Issue = 10,
    Export = 11,
    Import = 12,
    StockUpdate = 13,
    PasswordChange = 14,
    Error = 15
}

/// <summary>Stock movement velocity band produced by the movement-analysis report.</summary>
public enum MovementCategory
{
    Fast = 1,
    Slow = 2,
    Dead = 3,
    NonMoving = 4
}

/// <summary>ABC value classification.</summary>
public enum AbcClass
{
    A = 1,
    B = 2,
    C = 3
}

/// <summary>Supported export formats for every report screen.</summary>
public enum ExportFormat
{
    Excel = 1,
    Pdf = 2,
    Csv = 3
}
