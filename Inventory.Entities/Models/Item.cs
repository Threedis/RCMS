using System.ComponentModel.DataAnnotations;
using Inventory.Common.Enums;

namespace Inventory.Entities.Models;

/// <summary>
/// Stock keeping unit. Quantity on hand is never stored on this row: the
/// authoritative balance is derived from <see cref="StockLedger"/> so the ledger
/// and the balance can never disagree.
/// </summary>
public class Item : AuditableEntity
{
    public int Id { get; set; }

    /// <summary>Internal unique item code, e.g. <c>ITM-000123</c>.</summary>
    [Required]
    [StringLength(40)]
    public string ItemCode { get; set; } = string.Empty;

    [Required]
    [StringLength(250)]
    public string ItemName { get; set; } = string.Empty;

    /// <summary>Manufacturer part number. Unique per manufacturer when supplied.</summary>
    [StringLength(100)]
    public string? PartNumber { get; set; }

    /// <summary>Alternate / superseded part number.</summary>
    [StringLength(100)]
    public string? AlternatePartNumber { get; set; }

    /// <summary>Harmonised System of Nomenclature code used for tax reporting.</summary>
    [StringLength(20)]
    public string? HsnCode { get; set; }

    [StringLength(1000)]
    public string? Description { get; set; }

    [StringLength(500)]
    public string? Specification { get; set; }

    public int CategoryId { get; set; }

    public Category Category { get; set; } = null!;

    public int UnitId { get; set; }

    public Unit Unit { get; set; } = null!;

    public int? ManufacturerId { get; set; }

    public Manufacturer? Manufacturer { get; set; }

    /// <summary>Spare / consumable / tool / asset — drives dashboard grouping.</summary>
    public ItemType ItemType { get; set; } = ItemType.Consumable;

    /// <summary>Barcode or QR payload, scanned on the GRN and issue screens.</summary>
    [StringLength(100)]
    public string? Barcode { get; set; }

    /// <summary>Balance at or below which a low-stock alert is raised.</summary>
    public decimal ReorderLevel { get; set; }

    /// <summary>Suggested replenishment quantity.</summary>
    public decimal ReorderQuantity { get; set; }

    /// <summary>Minimum balance to be maintained.</summary>
    public decimal MinimumStock { get; set; }

    /// <summary>Maximum balance the store should hold.</summary>
    public decimal MaximumStock { get; set; }

    /// <summary>Latest purchase rate; refreshed when a GRN is approved.</summary>
    public decimal StandardCost { get; set; }

    /// <summary>
    /// Weighted-average cost maintained by the GRN approval procedure and used
    /// to value stock on the dashboard and the valuation reports.
    /// </summary>
    public decimal AverageCost { get; set; }

    /// <summary>Default storage location inside the warehouse.</summary>
    [StringLength(50)]
    public string? ShelfLocation { get; set; }

    /// <summary>Item requires a batch number on receipt and issue.</summary>
    public bool IsBatchTracked { get; set; }

    /// <summary>Item requires a serial number on receipt and issue.</summary>
    public bool IsSerialTracked { get; set; }

    /// <summary>Shelf life in days; used to flag near-expiry batches.</summary>
    public int? ShelfLifeDays { get; set; }

    /// <summary>Applicable GST rate in percent.</summary>
    public decimal? TaxRate { get; set; }

    public ICollection<ItemImage> Images { get; set; } = new List<ItemImage>();

    public ICollection<StockLedger> LedgerEntries { get; set; } = new List<StockLedger>();

    public ICollection<InventoryInwardDetail> ReceiptLines { get; set; } = new List<InventoryInwardDetail>();

    public ICollection<InventoryOutwardDetail> IssueLines { get; set; } = new List<InventoryOutwardDetail>();
}

/// <summary>Photograph or drawing attached to an item.</summary>
public class ItemImage
{
    public int Id { get; set; }

    public int ItemId { get; set; }

    public Item Item { get; set; } = null!;

    [Required]
    [StringLength(260)]
    public string FileName { get; set; } = string.Empty;

    /// <summary>Relative path under the configured upload root.</summary>
    [Required]
    [StringLength(500)]
    public string FilePath { get; set; } = string.Empty;

    [StringLength(100)]
    public string? ContentType { get; set; }

    public long FileSizeBytes { get; set; }

    /// <summary>Exactly one image per item may be the primary thumbnail.</summary>
    public bool IsPrimary { get; set; }

    public DateTime UploadedOn { get; set; } = DateTime.UtcNow;

    public int UploadedBy { get; set; }
}
