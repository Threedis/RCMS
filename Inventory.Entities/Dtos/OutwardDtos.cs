using System.ComponentModel.DataAnnotations;
using Inventory.Common.Enums;

namespace Inventory.Entities.Dtos;

/// <summary>Material Issue header + lines, used by the create/edit/view screens.</summary>
public class IssueDto : IValidatableObject
{
    public int Id { get; set; }

    [Display(Name = "Issue Number")]
    public string? IssueNumber { get; set; }

    [Required(ErrorMessage = "Issue date is required.")]
    [DataType(DataType.Date)]
    [Display(Name = "Issue Date")]
    public DateTime IssueDate { get; set; } = DateTime.Today;

    [Display(Name = "Requested By")]
    public int RequestedBy { get; set; }

    public string? RequestedByName { get; set; }

    public DateTime RequestedOn { get; set; } = DateTime.Now;

    [Display(Name = "Department")]
    public int? DepartmentId { get; set; }

    public string? DepartmentName { get; set; }

    [Display(Name = "Site")]
    public int? SiteId { get; set; }

    public string? SiteName { get; set; }

    [Display(Name = "Engineer")]
    public int? EngineerId { get; set; }

    public string? EngineerName { get; set; }

    [Required(ErrorMessage = "Warehouse is required.")]
    [Range(1, int.MaxValue, ErrorMessage = "Select a warehouse.")]
    [Display(Name = "Warehouse")]
    public int WarehouseId { get; set; }

    public string? WarehouseName { get; set; }

    [StringLength(200)]
    [Display(Name = "Project Name")]
    public string? ProjectName { get; set; }

    [StringLength(50)]
    [Display(Name = "Work Order No.")]
    public string? WorkOrderNumber { get; set; }

    [StringLength(500)]
    [Display(Name = "Purpose")]
    public string? Purpose { get; set; }

    [Display(Name = "Status")]
    public DocumentStatus Status { get; set; } = DocumentStatus.Draft;

    public string? StatusName { get; set; }

    public int? ApprovedBy { get; set; }

    public string? ApprovedByName { get; set; }

    public DateTime? ApprovedOn { get; set; }

    [StringLength(1000)]
    [Display(Name = "Approval Remarks")]
    public string? ApprovalRemarks { get; set; }

    public int? IssuedBy { get; set; }

    public string? IssuedByName { get; set; }

    public DateTime? IssuedOn { get; set; }

    [Display(Name = "Courier")]
    public int? CourierId { get; set; }

    public string? CourierName { get; set; }

    [StringLength(50)]
    [Display(Name = "Tracking Number")]
    public string? TrackingNumber { get; set; }

    [DataType(DataType.Date)]
    [Display(Name = "Dispatch Date")]
    public DateTime? DispatchDate { get; set; }

    [StringLength(150)]
    [Display(Name = "Receiver Name")]
    public string? ReceiverName { get; set; }

    public DateTime? ClosedOn { get; set; }

    public decimal TotalValue { get; set; }

    [StringLength(1000)]
    [Display(Name = "Remarks")]
    public string? Remarks { get; set; }

    public List<IssueDetailDto> Details { get; set; } = new();

    public List<AttachmentDto> Attachments { get; set; } = new();

    public List<ApprovalHistoryDto> ApprovalTrail { get; set; } = new();

    public bool CanEdit => Status is DocumentStatus.Draft or DocumentStatus.Rejected;

    public bool CanSubmit => Status is DocumentStatus.Draft or DocumentStatus.Rejected;

    public bool CanApprove => Status == DocumentStatus.PendingApproval;

    public bool CanDispatch => Status is DocumentStatus.Approved or DocumentStatus.Issued;

    public bool CanClose => Status == DocumentStatus.Issued;

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (IssueDate.Date > DateTime.Today)
        {
            yield return new ValidationResult("Issue date cannot be in the future.", new[] { nameof(IssueDate) });
        }

        if (DepartmentId is null && SiteId is null && EngineerId is null)
        {
            yield return new ValidationResult(
                "Specify at least one of department, site or engineer as the destination.",
                new[] { nameof(DepartmentId) });
        }

        if (Details.Count == 0)
        {
            yield return new ValidationResult("Add at least one item line.", new[] { nameof(Details) });
            yield break;
        }

        for (var i = 0; i < Details.Count; i++)
        {
            var line = Details[i];

            if (line.QuantityRequested <= 0)
            {
                yield return new ValidationResult($"Line {i + 1}: requested quantity must be greater than zero.",
                    new[] { $"Details[{i}].QuantityRequested" });
            }

            if (line.QuantityApproved > line.QuantityRequested)
            {
                yield return new ValidationResult(
                    $"Line {i + 1}: approved quantity cannot exceed the requested quantity.",
                    new[] { $"Details[{i}].QuantityApproved" });
            }

            if (line.QuantityIssued > line.QuantityApproved && line.QuantityApproved > 0)
            {
                yield return new ValidationResult(
                    $"Line {i + 1}: issued quantity cannot exceed the approved quantity.",
                    new[] { $"Details[{i}].QuantityIssued" });
            }
        }

        var duplicate = Details
            .GroupBy(d => new { d.ItemId, Batch = d.BatchNumber ?? string.Empty })
            .FirstOrDefault(g => g.Count() > 1);

        if (duplicate is not null)
        {
            yield return new ValidationResult("The same item/batch appears on more than one line.",
                new[] { nameof(Details) });
        }
    }
}

/// <summary>One item line on a Material Issue.</summary>
public class IssueDetailDto
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

    [Range(0.0001, 99999999, ErrorMessage = "Requested quantity must be greater than zero.")]
    [Display(Name = "Qty Requested")]
    public decimal QuantityRequested { get; set; }

    [Range(0, 99999999)]
    [Display(Name = "Qty Approved")]
    public decimal QuantityApproved { get; set; }

    [Range(0, 99999999)]
    [Display(Name = "Qty Issued")]
    public decimal QuantityIssued { get; set; }

    public decimal QuantityReserved { get; set; }

    public decimal QuantityReturned { get; set; }

    [StringLength(50)]
    [Display(Name = "Batch No.")]
    public string? BatchNumber { get; set; }

    [StringLength(100)]
    [Display(Name = "Serial No.")]
    public string? SerialNumber { get; set; }

    public decimal UnitCost { get; set; }

    public decimal TotalCost { get; set; }

    public decimal StockBefore { get; set; }

    public decimal StockAfter { get; set; }

    /// <summary>Balance available when the screen was loaded; used for client-side warnings.</summary>
    public decimal AvailableStock { get; set; }

    [StringLength(500)]
    public string? Remarks { get; set; }
}

/// <summary>Row of the Material Issue list grid.</summary>
public class IssueListDto
{
    public int Id { get; set; }

    public string IssueNumber { get; set; } = string.Empty;

    public DateTime IssueDate { get; set; }

    public string? DepartmentName { get; set; }

    public string? SiteName { get; set; }

    public string? EngineerName { get; set; }

    public string? ProjectName { get; set; }

    public string WarehouseName { get; set; } = string.Empty;

    public int LineCount { get; set; }

    public decimal TotalQuantity { get; set; }

    public decimal TotalValue { get; set; }

    public DocumentStatus Status { get; set; }

    public string StatusName { get; set; } = string.Empty;

    public string? RequestedByName { get; set; }

    public string? ApprovedByName { get; set; }

    public string? TrackingNumber { get; set; }
}

/// <summary>Payload posted by the approve/reject dialog for either document type.</summary>
public class ApprovalRequestDto
{
    [Required]
    public int DocumentId { get; set; }

    [Required]
    public DocumentType DocumentType { get; set; }

    /// <summary>True to approve, false to reject.</summary>
    public bool Approve { get; set; }

    [StringLength(1000)]
    [Display(Name = "Remarks")]
    public string? Remarks { get; set; }

    /// <summary>
    /// Optional per-line approved quantities for a Material Issue. Empty means
    /// "approve everything as requested".
    /// </summary>
    public List<ApprovalLineDto> Lines { get; set; } = new();
}

/// <summary>Per-line approved quantity supplied by the approver.</summary>
public class ApprovalLineDto
{
    public int DetailId { get; set; }

    [Range(0, 99999999)]
    public decimal QuantityApproved { get; set; }
}

/// <summary>Payload posted by the dispatch dialog.</summary>
public class DispatchRequestDto
{
    [Required]
    public int IssueId { get; set; }

    [Display(Name = "Courier")]
    public int? CourierId { get; set; }

    [StringLength(50)]
    [Display(Name = "Tracking Number")]
    public string? TrackingNumber { get; set; }

    [DataType(DataType.Date)]
    [Display(Name = "Dispatch Date")]
    public DateTime? DispatchDate { get; set; } = DateTime.Today;

    [StringLength(150)]
    [Display(Name = "Receiver Name")]
    public string? ReceiverName { get; set; }

    [StringLength(1000)]
    public string? Remarks { get; set; }

    /// <summary>Quantities actually handed over, per line.</summary>
    public List<IssueLineQuantityDto> Lines { get; set; } = new();
}

/// <summary>Quantity actually issued for one line.</summary>
public class IssueLineQuantityDto
{
    public int DetailId { get; set; }

    [Range(0, 99999999)]
    public decimal QuantityIssued { get; set; }
}

/// <summary>One entry of a document's approval trail.</summary>
public class ApprovalHistoryDto
{
    public long Id { get; set; }

    public string Action { get; set; } = string.Empty;

    public string FromStatus { get; set; } = string.Empty;

    public string ToStatus { get; set; } = string.Empty;

    public string? ActionByName { get; set; }

    public DateTime ActionOn { get; set; }

    public string? Remarks { get; set; }

    public int Level { get; set; }
}

/// <summary>Row of the pending-approvals queue.</summary>
public class PendingApprovalDto
{
    public int DocumentId { get; set; }

    public DocumentType DocumentType { get; set; }

    public string DocumentTypeName { get; set; } = string.Empty;

    public string DocumentNumber { get; set; } = string.Empty;

    public DateTime DocumentDate { get; set; }

    /// <summary>Vendor for a receipt, department/engineer for an issue.</summary>
    public string? PartyName { get; set; }

    public string? WarehouseName { get; set; }

    public int LineCount { get; set; }

    public decimal TotalValue { get; set; }

    public string? RequestedByName { get; set; }

    public DateTime SubmittedOn { get; set; }

    /// <summary>Whole days the document has been waiting.</summary>
    public int AgeInDays { get; set; }
}
