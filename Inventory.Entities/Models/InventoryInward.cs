using System.ComponentModel.DataAnnotations;
using Inventory.Common.Enums;

namespace Inventory.Entities.Models;

/// <summary>
/// Goods Receipt Note header — material received from a vendor into a warehouse.
/// Stock is posted only when <see cref="Status"/> reaches
/// <see cref="DocumentStatus.Approved"/>.
/// </summary>
public class InventoryInwardHeader : AuditableEntity
{
    public int Id { get; set; }

    /// <summary>System generated, e.g. <c>GRN/2026/000145</c>. Unique.</summary>
    [Required]
    [StringLength(30)]
    public string GrnNumber { get; set; } = string.Empty;

    public DateTime GrnDate { get; set; } = DateTime.Today;

    [StringLength(50)]
    public string? PurchaseOrderNumber { get; set; }

    public DateTime? PurchaseOrderDate { get; set; }

    [StringLength(50)]
    public string? DeliveryChallanNumber { get; set; }

    public DateTime? DeliveryChallanDate { get; set; }

    [StringLength(50)]
    public string? InvoiceNumber { get; set; }

    public DateTime? InvoiceDate { get; set; }

    public int VendorId { get; set; }

    public Vendor Vendor { get; set; } = null!;

    public int? CourierId { get; set; }

    public Courier? Courier { get; set; }

    [StringLength(50)]
    public string? ConsignmentNumber { get; set; }

    /// <summary>Warehouse the material is received into.</summary>
    public int WarehouseId { get; set; }

    public Warehouse Warehouse { get; set; } = null!;

    /// <summary>Store person who physically received the consignment.</summary>
    public int ReceivedBy { get; set; }

    public DateTime ReceivedOn { get; set; } = DateTime.UtcNow;

    public DocumentStatus Status { get; set; } = DocumentStatus.Draft;

    public int? ApprovedBy { get; set; }

    public DateTime? ApprovedOn { get; set; }

    [StringLength(1000)]
    public string? ApprovalRemarks { get; set; }

    /// <summary>Sum of <c>TotalCost</c> across all lines, maintained by SQL.</summary>
    public decimal TotalValue { get; set; }

    public decimal TotalTaxAmount { get; set; }

    public decimal GrandTotal { get; set; }

    [StringLength(1000)]
    public string? Remarks { get; set; }

    public ICollection<InventoryInwardDetail> Details { get; set; } = new List<InventoryInwardDetail>();

    public ICollection<Attachment> Attachments { get; set; } = new List<Attachment>();
}

/// <summary>One received item line on a Goods Receipt Note.</summary>
public class InventoryInwardDetail
{
    public int Id { get; set; }

    public int InwardHeaderId { get; set; }

    public InventoryInwardHeader Header { get; set; } = null!;

    public int ItemId { get; set; }

    public Item Item { get; set; } = null!;

    /// <summary>Snapshot of the part number as printed on the vendor challan.</summary>
    [StringLength(100)]
    public string? PartNumber { get; set; }

    /// <summary>Quantity stated on the delivery challan.</summary>
    public decimal QuantityReceived { get; set; }

    /// <summary>Quantity that passed inspection; this is what reaches stock.</summary>
    public decimal QuantityAccepted { get; set; }

    /// <summary>Quantity rejected on inspection; never reaches stock.</summary>
    public decimal QuantityRejected { get; set; }

    [StringLength(500)]
    public string? RejectionReason { get; set; }

    [StringLength(50)]
    public string? BatchNumber { get; set; }

    [StringLength(100)]
    public string? SerialNumber { get; set; }

    public DateTime? ManufacturingDate { get; set; }

    public DateTime? ExpiryDate { get; set; }

    public decimal UnitCost { get; set; }

    public decimal DiscountPercent { get; set; }

    public decimal TaxRate { get; set; }

    public decimal TaxAmount { get; set; }

    /// <summary>Accepted quantity x unit cost, less discount, plus tax.</summary>
    public decimal TotalCost { get; set; }

    [StringLength(50)]
    public string? ShelfLocation { get; set; }

    /// <summary>Balance before this line was posted; captured for traceability.</summary>
    public decimal StockBefore { get; set; }

    /// <summary>Balance after this line was posted.</summary>
    public decimal StockAfter { get; set; }

    [StringLength(500)]
    public string? Remarks { get; set; }

    public int LineNumber { get; set; }
}
