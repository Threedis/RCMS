using System.ComponentModel.DataAnnotations;
using Inventory.Common.Enums;

namespace Inventory.Entities.Models;

/// <summary>
/// Append-only movement journal. Every change of stock — receipt, issue,
/// opening balance, adjustment, transfer or reversal — writes exactly one row
/// here inside the same transaction as the document that caused it.
/// <para>
/// Rows are never updated or deleted: corrections are posted as reversals. The
/// current balance of an item in a warehouse is the running
/// <see cref="BalanceQuantity"/> of its most recent row, and the SQL view
/// <c>vw_CurrentStock</c> exposes it for reporting.
/// </para>
/// </summary>
public class StockLedger
{
    public long Id { get; set; }

    public int ItemId { get; set; }

    public Item Item { get; set; } = null!;

    public int WarehouseId { get; set; }

    public Warehouse Warehouse { get; set; } = null!;

    /// <summary>Business date of the movement (not the insert timestamp).</summary>
    public DateTime TransactionDate { get; set; } = DateTime.Today;

    public MovementType MovementType { get; set; }

    /// <summary>Document class that produced the movement.</summary>
    public DocumentType? DocumentType { get; set; }

    /// <summary>Primary key of the source header row.</summary>
    public int? DocumentId { get; set; }

    /// <summary>Human readable source reference, e.g. the GRN or issue number.</summary>
    [StringLength(30)]
    public string? DocumentNumber { get; set; }

    /// <summary>Primary key of the source detail row.</summary>
    public int? DocumentDetailId { get; set; }

    /// <summary>Quantity added to stock (0 for outward movements).</summary>
    public decimal InwardQuantity { get; set; }

    /// <summary>Quantity removed from stock (0 for inward movements).</summary>
    public decimal OutwardQuantity { get; set; }

    /// <summary>Running balance after this movement was applied.</summary>
    public decimal BalanceQuantity { get; set; }

    /// <summary>Rate used to value the movement.</summary>
    public decimal UnitCost { get; set; }

    /// <summary>Movement value = quantity x <see cref="UnitCost"/>.</summary>
    public decimal Value { get; set; }

    /// <summary>Weighted-average cost of the item after this movement.</summary>
    public decimal BalanceAverageCost { get; set; }

    [StringLength(50)]
    public string? BatchNumber { get; set; }

    [StringLength(100)]
    public string? SerialNumber { get; set; }

    [StringLength(500)]
    public string? Remarks { get; set; }

    public DateTime CreatedOn { get; set; } = DateTime.UtcNow;

    public int CreatedBy { get; set; }
}
