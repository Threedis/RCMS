using System.ComponentModel.DataAnnotations;
using Inventory.Common.Enums;

namespace Inventory.Entities.Dtos;

/// <summary>Full item master payload used by the create/edit screen.</summary>
public class ItemDto
{
    public int Id { get; set; }

    /// <summary>Left blank on create — the stored procedure generates it.</summary>
    [StringLength(40)]
    [Display(Name = "Item Code")]
    public string? ItemCode { get; set; }

    [Required(ErrorMessage = "Item name is required.")]
    [StringLength(250)]
    [Display(Name = "Item Name")]
    public string ItemName { get; set; } = string.Empty;

    [StringLength(100)]
    [Display(Name = "Part Number")]
    public string? PartNumber { get; set; }

    [StringLength(100)]
    [Display(Name = "Alternate Part Number")]
    public string? AlternatePartNumber { get; set; }

    [StringLength(20)]
    [Display(Name = "HSN Code")]
    public string? HsnCode { get; set; }

    [StringLength(1000)]
    [Display(Name = "Description")]
    public string? Description { get; set; }

    [StringLength(500)]
    [Display(Name = "Specification")]
    public string? Specification { get; set; }

    [Required(ErrorMessage = "Category is required.")]
    [Range(1, int.MaxValue, ErrorMessage = "Select a category.")]
    [Display(Name = "Category")]
    public int CategoryId { get; set; }

    public string? CategoryName { get; set; }

    [Required(ErrorMessage = "Unit is required.")]
    [Range(1, int.MaxValue, ErrorMessage = "Select a unit.")]
    [Display(Name = "Unit")]
    public int UnitId { get; set; }

    public string? UnitName { get; set; }

    public string? UnitSymbol { get; set; }

    [Display(Name = "Manufacturer")]
    public int? ManufacturerId { get; set; }

    public string? ManufacturerName { get; set; }

    [Display(Name = "Item Type")]
    public ItemType ItemType { get; set; } = ItemType.Consumable;

    public string? ItemTypeName { get; set; }

    [StringLength(100)]
    [Display(Name = "Barcode")]
    public string? Barcode { get; set; }

    [Range(0, 9999999, ErrorMessage = "Reorder level cannot be negative.")]
    [Display(Name = "Reorder Level")]
    public decimal ReorderLevel { get; set; }

    [Range(0, 9999999)]
    [Display(Name = "Reorder Quantity")]
    public decimal ReorderQuantity { get; set; }

    [Range(0, 9999999)]
    [Display(Name = "Minimum Stock")]
    public decimal MinimumStock { get; set; }

    [Range(0, 9999999)]
    [Display(Name = "Maximum Stock")]
    public decimal MaximumStock { get; set; }

    [Range(0, 99999999)]
    [Display(Name = "Standard Cost")]
    public decimal StandardCost { get; set; }

    [Display(Name = "Average Cost")]
    public decimal AverageCost { get; set; }

    [StringLength(50)]
    [Display(Name = "Shelf Location")]
    public string? ShelfLocation { get; set; }

    [Display(Name = "Batch Tracked")]
    public bool IsBatchTracked { get; set; }

    [Display(Name = "Serial Tracked")]
    public bool IsSerialTracked { get; set; }

    [Range(0, 20000)]
    [Display(Name = "Shelf Life (days)")]
    public int? ShelfLifeDays { get; set; }

    [Range(0, 100)]
    [Display(Name = "Tax Rate %")]
    public decimal? TaxRate { get; set; }

    [Display(Name = "Active")]
    public bool IsActive { get; set; } = true;

    /// <summary>Balance across all warehouses; read-only, sourced from the ledger.</summary>
    public decimal CurrentStock { get; set; }

    public decimal StockValue { get; set; }

    public string? PrimaryImagePath { get; set; }

    public DateTime CreatedOn { get; set; }

    public DateTime? ModifiedOn { get; set; }

    /// <summary>True when the balance has fallen to or below the reorder level.</summary>
    public bool IsBelowReorderLevel => CurrentStock <= ReorderLevel;
}

/// <summary>Slim projection used by the server-side paged item grid.</summary>
public class ItemListDto
{
    public int Id { get; set; }

    public string ItemCode { get; set; } = string.Empty;

    public string ItemName { get; set; } = string.Empty;

    public string? PartNumber { get; set; }

    public string? CategoryName { get; set; }

    public string? UnitSymbol { get; set; }

    public string? ManufacturerName { get; set; }

    public string ItemTypeName { get; set; } = string.Empty;

    public decimal CurrentStock { get; set; }

    public decimal ReorderLevel { get; set; }

    public decimal AverageCost { get; set; }

    public decimal StockValue { get; set; }

    public bool IsActive { get; set; }

    public bool IsLowStock { get; set; }
}

/// <summary>Row returned when the user scans a barcode or picks an item on a document.</summary>
public class ItemStockSnapshotDto
{
    public int ItemId { get; set; }

    public string ItemCode { get; set; } = string.Empty;

    public string ItemName { get; set; } = string.Empty;

    public string? PartNumber { get; set; }

    public string? UnitSymbol { get; set; }

    public int UnitId { get; set; }

    public decimal AverageCost { get; set; }

    public decimal TaxRate { get; set; }

    /// <summary>Physical balance in the selected warehouse.</summary>
    public decimal CurrentStock { get; set; }

    /// <summary>Quantity held by pending issues; not available to a new request.</summary>
    public decimal ReservedStock { get; set; }

    /// <summary>Balance the user may actually draw on.</summary>
    public decimal AvailableStock { get; set; }

    public bool IsBatchTracked { get; set; }

    public bool IsSerialTracked { get; set; }

    public string? ShelfLocation { get; set; }
}
