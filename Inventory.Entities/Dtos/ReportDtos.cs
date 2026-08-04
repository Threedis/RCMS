using Inventory.Common.Enums;

namespace Inventory.Entities.Dtos;

/// <summary>Current stock report row (item x warehouse).</summary>
public class CurrentStockDto
{
    public int ItemId { get; set; }

    public string ItemCode { get; set; } = string.Empty;

    public string ItemName { get; set; } = string.Empty;

    public string? PartNumber { get; set; }

    public string? CategoryName { get; set; }

    public string? UnitSymbol { get; set; }

    public int WarehouseId { get; set; }

    public string WarehouseName { get; set; } = string.Empty;

    public string? SiteName { get; set; }

    public decimal Quantity { get; set; }

    public decimal ReservedQuantity { get; set; }

    public decimal AvailableQuantity { get; set; }

    public decimal AverageCost { get; set; }

    public decimal StockValue { get; set; }

    public decimal ReorderLevel { get; set; }

    public bool IsLowStock { get; set; }

    public DateTime? LastMovementDate { get; set; }

    public string? ShelfLocation { get; set; }
}

/// <summary>Stock ledger report row — one movement.</summary>
public class StockLedgerDto
{
    public long Id { get; set; }

    public DateTime TransactionDate { get; set; }

    public string ItemCode { get; set; } = string.Empty;

    public string ItemName { get; set; } = string.Empty;

    public string? UnitSymbol { get; set; }

    public string WarehouseName { get; set; } = string.Empty;

    public string MovementTypeName { get; set; } = string.Empty;

    public string? DocumentNumber { get; set; }

    public string? DocumentTypeName { get; set; }

    public decimal InwardQuantity { get; set; }

    public decimal OutwardQuantity { get; set; }

    public decimal BalanceQuantity { get; set; }

    public decimal UnitCost { get; set; }

    public decimal Value { get; set; }

    public string? BatchNumber { get; set; }

    public string? SerialNumber { get; set; }

    public string? Remarks { get; set; }

    public string? CreatedByName { get; set; }
}

/// <summary>Low-stock alert row.</summary>
public class LowStockDto
{
    public int ItemId { get; set; }

    public string ItemCode { get; set; } = string.Empty;

    public string ItemName { get; set; } = string.Empty;

    public string? CategoryName { get; set; }

    public string? UnitSymbol { get; set; }

    public string? WarehouseName { get; set; }

    public decimal CurrentStock { get; set; }

    public decimal ReorderLevel { get; set; }

    public decimal ReorderQuantity { get; set; }

    /// <summary>How far below the reorder level the balance has fallen.</summary>
    public decimal Shortfall { get; set; }

    public decimal AverageCost { get; set; }

    public string? PreferredVendorName { get; set; }
}

/// <summary>Site-wise / warehouse-wise stock summary row.</summary>
public class LocationStockDto
{
    public int LocationId { get; set; }

    public string LocationName { get; set; } = string.Empty;

    public string? ParentName { get; set; }

    public int ItemCount { get; set; }

    public decimal TotalQuantity { get; set; }

    public decimal TotalValue { get; set; }

    public int LowStockCount { get; set; }
}

/// <summary>Vendor purchase analysis row.</summary>
public class VendorPurchaseDto
{
    public int VendorId { get; set; }

    public string VendorCode { get; set; } = string.Empty;

    public string VendorName { get; set; } = string.Empty;

    public int ReceiptCount { get; set; }

    public int ItemCount { get; set; }

    public decimal TotalQuantity { get; set; }

    public decimal TotalValue { get; set; }

    public decimal RejectedQuantity { get; set; }

    /// <summary>Rejected quantity as a percentage of received quantity.</summary>
    public decimal RejectionRate { get; set; }

    public DateTime? LastSupplyDate { get; set; }
}

/// <summary>Engineer-wise / department-wise issue analysis row.</summary>
public class ConsumptionDto
{
    /// <summary>Engineer, department, site or item id depending on the report.</summary>
    public int EntityId { get; set; }

    public string EntityCode { get; set; } = string.Empty;

    public string EntityName { get; set; } = string.Empty;

    public string? GroupName { get; set; }

    public int IssueCount { get; set; }

    public int ItemCount { get; set; }

    public decimal TotalQuantity { get; set; }

    public decimal TotalValue { get; set; }

    public DateTime? LastIssueDate { get; set; }
}

/// <summary>
/// Period consumption row used by the monthly / quarterly / half-yearly /
/// yearly reports. <see cref="PeriodLabel"/> is produced by SQL so all four
/// reports share one shape, one grid and one export path.
/// </summary>
public class PeriodConsumptionDto
{
    /// <summary>Sort key, e.g. <c>202604</c> for April 2026.</summary>
    public int PeriodKey { get; set; }

    /// <summary>Display label, e.g. <c>Apr 2026</c>, <c>Q1 2026-27</c>.</summary>
    public string PeriodLabel { get; set; } = string.Empty;

    public int? ItemId { get; set; }

    public string? ItemCode { get; set; }

    public string? ItemName { get; set; }

    public string? CategoryName { get; set; }

    public string? UnitSymbol { get; set; }

    public decimal IssuedQuantity { get; set; }

    public decimal IssuedValue { get; set; }

    public decimal ReceivedQuantity { get; set; }

    public decimal ReceivedValue { get; set; }

    public int TransactionCount { get; set; }
}

/// <summary>ABC classification row.</summary>
public class AbcAnalysisDto
{
    public int ItemId { get; set; }

    public string ItemCode { get; set; } = string.Empty;

    public string ItemName { get; set; } = string.Empty;

    public string? CategoryName { get; set; }

    public decimal AnnualQuantity { get; set; }

    public decimal AnnualValue { get; set; }

    /// <summary>Share of total consumption value, in percent.</summary>
    public decimal ValuePercent { get; set; }

    /// <summary>Running share used to draw the class boundaries (70 / 90 / 100).</summary>
    public decimal CumulativePercent { get; set; }

    public AbcClass Classification { get; set; }

    public string ClassificationName { get; set; } = string.Empty;

    public int Rank { get; set; }
}

/// <summary>Fast / slow / dead stock analysis row.</summary>
public class MovementAnalysisDto
{
    public int ItemId { get; set; }

    public string ItemCode { get; set; } = string.Empty;

    public string ItemName { get; set; } = string.Empty;

    public string? CategoryName { get; set; }

    public string? UnitSymbol { get; set; }

    public decimal CurrentStock { get; set; }

    public decimal StockValue { get; set; }

    public int IssueCount { get; set; }

    public decimal IssuedQuantity { get; set; }

    public DateTime? LastIssueDate { get; set; }

    public DateTime? LastReceiptDate { get; set; }

    /// <summary>Days since the last outward movement.</summary>
    public int DaysSinceLastMovement { get; set; }

    /// <summary>Issued quantity divided by average stock held.</summary>
    public decimal TurnoverRatio { get; set; }

    public MovementCategory Category { get; set; }

    public string CategoryLabel { get; set; } = string.Empty;
}

/// <summary>Material receipt register row (one line per GRN detail).</summary>
public class ReceiptRegisterDto
{
    public DateTime GrnDate { get; set; }

    public string GrnNumber { get; set; } = string.Empty;

    public string VendorName { get; set; } = string.Empty;

    public string? PurchaseOrderNumber { get; set; }

    public string? DeliveryChallanNumber { get; set; }

    public string ItemCode { get; set; } = string.Empty;

    public string ItemName { get; set; } = string.Empty;

    public string? UnitSymbol { get; set; }

    public decimal QuantityReceived { get; set; }

    public decimal QuantityAccepted { get; set; }

    public decimal QuantityRejected { get; set; }

    public decimal UnitCost { get; set; }

    public decimal TotalCost { get; set; }

    public string WarehouseName { get; set; } = string.Empty;

    public string? BatchNumber { get; set; }

    public string StatusName { get; set; } = string.Empty;

    public string? ApprovedByName { get; set; }
}

/// <summary>Material issue register row (one line per issue detail).</summary>
public class IssueRegisterDto
{
    public DateTime IssueDate { get; set; }

    public string IssueNumber { get; set; } = string.Empty;

    public string? DepartmentName { get; set; }

    public string? SiteName { get; set; }

    public string? EngineerName { get; set; }

    public string? ProjectName { get; set; }

    public string ItemCode { get; set; } = string.Empty;

    public string ItemName { get; set; } = string.Empty;

    public string? UnitSymbol { get; set; }

    public decimal QuantityRequested { get; set; }

    public decimal QuantityApproved { get; set; }

    public decimal QuantityIssued { get; set; }

    public decimal UnitCost { get; set; }

    public decimal TotalCost { get; set; }

    public string WarehouseName { get; set; } = string.Empty;

    public string StatusName { get; set; } = string.Empty;

    public string? ApprovedByName { get; set; }

    public string? TrackingNumber { get; set; }
}

/// <summary>Audit log row shown on the searchable audit screen.</summary>
public class AuditLogDto
{
    public long Id { get; set; }

    public DateTime CreatedOn { get; set; }

    public string ActionName { get; set; } = string.Empty;

    public string? EntityName { get; set; }

    public string? EntityId { get; set; }

    public string? Description { get; set; }

    public string? UserName { get; set; }

    public string? IpAddress { get; set; }

    public string? Source { get; set; }

    public bool IsSuccessful { get; set; }

    public string? ErrorMessage { get; set; }

    /// <summary>Only loaded on the detail dialog, never on the grid.</summary>
    public string? OldValues { get; set; }

    public string? NewValues { get; set; }
}

/// <summary>Login history row.</summary>
public class LoginHistoryDto
{
    public long Id { get; set; }

    public string UserName { get; set; } = string.Empty;

    public string? FullName { get; set; }

    public DateTime LoginOn { get; set; }

    public DateTime? LogoutOn { get; set; }

    public bool IsSuccessful { get; set; }

    public string? FailureReason { get; set; }

    public string? IpAddress { get; set; }

    public string? UserAgent { get; set; }
}
