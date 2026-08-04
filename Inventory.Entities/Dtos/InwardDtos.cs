using System.ComponentModel.DataAnnotations;
using Inventory.Common.Enums;

namespace Inventory.Entities.Dtos;

/// <summary>Goods Receipt Note header + lines, used by the create/edit/view screens.</summary>
public class GrnDto : IValidatableObject
{
    public int Id { get; set; }

    /// <summary>Blank on create; the stored procedure allocates the number.</summary>
    [Display(Name = "GRN Number")]
    public string? GrnNumber { get; set; }

    [Required(ErrorMessage = "GRN date is required.")]
    [DataType(DataType.Date)]
    [Display(Name = "GRN Date")]
    public DateTime GrnDate { get; set; } = DateTime.Today;

    [StringLength(50)]
    [Display(Name = "Purchase Order No.")]
    public string? PurchaseOrderNumber { get; set; }

    [DataType(DataType.Date)]
    [Display(Name = "PO Date")]
    public DateTime? PurchaseOrderDate { get; set; }

    [StringLength(50)]
    [Display(Name = "Delivery Challan No.")]
    public string? DeliveryChallanNumber { get; set; }

    [DataType(DataType.Date)]
    [Display(Name = "Challan Date")]
    public DateTime? DeliveryChallanDate { get; set; }

    [StringLength(50)]
    [Display(Name = "Invoice No.")]
    public string? InvoiceNumber { get; set; }

    [DataType(DataType.Date)]
    [Display(Name = "Invoice Date")]
    public DateTime? InvoiceDate { get; set; }

    [Required(ErrorMessage = "Vendor is required.")]
    [Range(1, int.MaxValue, ErrorMessage = "Select a vendor.")]
    [Display(Name = "Vendor")]
    public int VendorId { get; set; }

    public string? VendorName { get; set; }

    [Display(Name = "Courier")]
    public int? CourierId { get; set; }

    public string? CourierName { get; set; }

    [StringLength(50)]
    [Display(Name = "Consignment No.")]
    public string? ConsignmentNumber { get; set; }

    [Required(ErrorMessage = "Warehouse is required.")]
    [Range(1, int.MaxValue, ErrorMessage = "Select a warehouse.")]
    [Display(Name = "Warehouse")]
    public int WarehouseId { get; set; }

    public string? WarehouseName { get; set; }

    [Display(Name = "Received By")]
    public int ReceivedBy { get; set; }

    public string? ReceivedByName { get; set; }

    public DateTime ReceivedOn { get; set; } = DateTime.Now;

    [Display(Name = "Status")]
    public DocumentStatus Status { get; set; } = DocumentStatus.Draft;

    public string? StatusName { get; set; }

    public int? ApprovedBy { get; set; }

    public string? ApprovedByName { get; set; }

    public DateTime? ApprovedOn { get; set; }

    [StringLength(1000)]
    [Display(Name = "Approval Remarks")]
    public string? ApprovalRemarks { get; set; }

    public decimal TotalValue { get; set; }

    public decimal TotalTaxAmount { get; set; }

    public decimal GrandTotal { get; set; }

    [StringLength(1000)]
    [Display(Name = "Remarks")]
    public string? Remarks { get; set; }

    /// <summary>Item lines. At least one is required to submit the document.</summary>
    public List<GrnDetailDto> Details { get; set; } = new();

    public List<AttachmentDto> Attachments { get; set; } = new();

    public List<ApprovalHistoryDto> ApprovalTrail { get; set; } = new();

    /// <summary>Convenience flags used by the Razor view to enable/disable actions.</summary>
    public bool CanEdit => Status is DocumentStatus.Draft or DocumentStatus.Rejected;

    public bool CanSubmit => Status is DocumentStatus.Draft or DocumentStatus.Rejected;

    public bool CanApprove => Status == DocumentStatus.PendingApproval;

    /// <summary>Cross-field rules the data annotations cannot express.</summary>
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (GrnDate.Date > DateTime.Today)
        {
            yield return new ValidationResult("GRN date cannot be in the future.", new[] { nameof(GrnDate) });
        }

        if (PurchaseOrderDate.HasValue && PurchaseOrderDate.Value.Date > GrnDate.Date)
        {
            yield return new ValidationResult("Purchase order date cannot be after the GRN date.",
                new[] { nameof(PurchaseOrderDate) });
        }

        if (Details.Count == 0)
        {
            yield return new ValidationResult("Add at least one item line.", new[] { nameof(Details) });
            yield break;
        }

        for (var i = 0; i < Details.Count; i++)
        {
            var line = Details[i];

            if (line.QuantityReceived <= 0)
            {
                yield return new ValidationResult($"Line {i + 1}: received quantity must be greater than zero.",
                    new[] { $"Details[{i}].QuantityReceived" });
            }

            if (line.QuantityAccepted + line.QuantityRejected != line.QuantityReceived)
            {
                yield return new ValidationResult(
                    $"Line {i + 1}: accepted plus rejected quantity must equal the received quantity.",
                    new[] { $"Details[{i}].QuantityAccepted" });
            }

            if (line.UnitCost < 0)
            {
                yield return new ValidationResult($"Line {i + 1}: unit cost cannot be negative.",
                    new[] { $"Details[{i}].UnitCost" });
            }
        }

        var duplicate = Details
            .GroupBy(d => new { d.ItemId, Batch = d.BatchNumber ?? string.Empty, Serial = d.SerialNumber ?? string.Empty })
            .FirstOrDefault(g => g.Count() > 1);

        if (duplicate is not null)
        {
            yield return new ValidationResult(
                "The same item/batch/serial combination appears on more than one line.",
                new[] { nameof(Details) });
        }
    }
}

/// <summary>One item line on a Goods Receipt Note.</summary>
public class GrnDetailDto
{
    public int Id { get; set; }

    public int LineNumber { get; set; }

    [Required(ErrorMessage = "Item is required.")]
    [Range(1, int.MaxValue, ErrorMessage = "Select an item.")]
    [Display(Name = "Item")]
    public int ItemId { get; set; }

    public string? ItemCode { get; set; }

    public string? ItemName { get; set; }

    public string? UnitSymbol { get; set; }

    [StringLength(100)]
    [Display(Name = "Part Number")]
    public string? PartNumber { get; set; }

    [Range(0.0001, 99999999, ErrorMessage = "Received quantity must be greater than zero.")]
    [Display(Name = "Qty Received")]
    public decimal QuantityReceived { get; set; }

    [Range(0, 99999999)]
    [Display(Name = "Qty Accepted")]
    public decimal QuantityAccepted { get; set; }

    [Range(0, 99999999)]
    [Display(Name = "Qty Rejected")]
    public decimal QuantityRejected { get; set; }

    [StringLength(500)]
    [Display(Name = "Rejection Reason")]
    public string? RejectionReason { get; set; }

    [StringLength(50)]
    [Display(Name = "Batch No.")]
    public string? BatchNumber { get; set; }

    [StringLength(100)]
    [Display(Name = "Serial No.")]
    public string? SerialNumber { get; set; }

    [DataType(DataType.Date)]
    [Display(Name = "Mfg. Date")]
    public DateTime? ManufacturingDate { get; set; }

    [DataType(DataType.Date)]
    [Display(Name = "Expiry Date")]
    public DateTime? ExpiryDate { get; set; }

    [Range(0, 99999999)]
    [Display(Name = "Unit Cost")]
    public decimal UnitCost { get; set; }

    [Range(0, 100)]
    [Display(Name = "Discount %")]
    public decimal DiscountPercent { get; set; }

    [Range(0, 100)]
    [Display(Name = "Tax %")]
    public decimal TaxRate { get; set; }

    public decimal TaxAmount { get; set; }

    public decimal TotalCost { get; set; }

    [StringLength(50)]
    [Display(Name = "Shelf Location")]
    public string? ShelfLocation { get; set; }

    public decimal StockBefore { get; set; }

    public decimal StockAfter { get; set; }

    [StringLength(500)]
    public string? Remarks { get; set; }
}

/// <summary>Row of the Goods Receipt Note list grid.</summary>
public class GrnListDto
{
    public int Id { get; set; }

    public string GrnNumber { get; set; } = string.Empty;

    public DateTime GrnDate { get; set; }

    public string? PurchaseOrderNumber { get; set; }

    public string VendorName { get; set; } = string.Empty;

    public string WarehouseName { get; set; } = string.Empty;

    public int LineCount { get; set; }

    public decimal TotalQuantity { get; set; }

    public decimal GrandTotal { get; set; }

    public DocumentStatus Status { get; set; }

    public string StatusName { get; set; } = string.Empty;

    public string? ReceivedByName { get; set; }

    public string? ApprovedByName { get; set; }

    public DateTime? ApprovedOn { get; set; }
}
