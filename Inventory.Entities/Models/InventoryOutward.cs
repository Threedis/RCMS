using System.ComponentModel.DataAnnotations;
using Inventory.Common.Enums;

namespace Inventory.Entities.Models;

/// <summary>
/// Material Issue header — stock leaving a warehouse for a department, site or
/// engineer. Stock is deducted when the document is approved; the dispatch step
/// only records logistics information.
/// </summary>
public class InventoryOutwardHeader : AuditableEntity
{
    public int Id { get; set; }

    /// <summary>System generated, e.g. <c>ISS/2026/000320</c>. Unique.</summary>
    [Required]
    [StringLength(30)]
    public string IssueNumber { get; set; } = string.Empty;

    public DateTime IssueDate { get; set; } = DateTime.Today;

    /// <summary>User who raised the request.</summary>
    public int RequestedBy { get; set; }

    public DateTime RequestedOn { get; set; } = DateTime.UtcNow;

    public int? DepartmentId { get; set; }

    public Department? Department { get; set; }

    public int? SiteId { get; set; }

    public Site? Site { get; set; }

    public int? EngineerId { get; set; }

    public Engineer? Engineer { get; set; }

    /// <summary>Warehouse the stock is drawn from.</summary>
    public int WarehouseId { get; set; }

    public Warehouse Warehouse { get; set; } = null!;

    [StringLength(200)]
    public string? ProjectName { get; set; }

    [StringLength(50)]
    public string? WorkOrderNumber { get; set; }

    /// <summary>Reason for the issue, shown on the approval screen.</summary>
    [StringLength(500)]
    public string? Purpose { get; set; }

    public DocumentStatus Status { get; set; } = DocumentStatus.Draft;

    public int? ApprovedBy { get; set; }

    public DateTime? ApprovedOn { get; set; }

    [StringLength(1000)]
    public string? ApprovalRemarks { get; set; }

    public int? IssuedBy { get; set; }

    public DateTime? IssuedOn { get; set; }

    // ---- Dispatch / logistics --------------------------------------------
    public int? CourierId { get; set; }

    public Courier? Courier { get; set; }

    [StringLength(50)]
    public string? TrackingNumber { get; set; }

    public DateTime? DispatchDate { get; set; }

    [StringLength(150)]
    public string? ReceiverName { get; set; }

    public DateTime? ClosedOn { get; set; }

    /// <summary>Sum of issued quantity x average cost across the lines.</summary>
    public decimal TotalValue { get; set; }

    [StringLength(1000)]
    public string? Remarks { get; set; }

    public ICollection<InventoryOutwardDetail> Details { get; set; } = new List<InventoryOutwardDetail>();

    public ICollection<Attachment> Attachments { get; set; } = new List<Attachment>();
}

/// <summary>One requested/issued item line on a Material Issue.</summary>
public class InventoryOutwardDetail
{
    public int Id { get; set; }

    public int OutwardHeaderId { get; set; }

    public InventoryOutwardHeader Header { get; set; } = null!;

    public int ItemId { get; set; }

    public Item Item { get; set; } = null!;

    [StringLength(100)]
    public string? PartNumber { get; set; }

    /// <summary>What the requester asked for.</summary>
    public decimal QuantityRequested { get; set; }

    /// <summary>What the approver sanctioned; may be less than requested.</summary>
    public decimal QuantityApproved { get; set; }

    /// <summary>What the store actually handed over; drives the ledger.</summary>
    public decimal QuantityIssued { get; set; }

    /// <summary>
    /// Quantity held against this line while the document is pending approval.
    /// Reserved stock is excluded from the issuable balance so two requests
    /// cannot consume the same units.
    /// </summary>
    public decimal QuantityReserved { get; set; }

    /// <summary>Quantity returned by the engineer against this issue.</summary>
    public decimal QuantityReturned { get; set; }

    [StringLength(50)]
    public string? BatchNumber { get; set; }

    [StringLength(100)]
    public string? SerialNumber { get; set; }

    /// <summary>Valuation rate applied at the time of issue (weighted average).</summary>
    public decimal UnitCost { get; set; }

    public decimal TotalCost { get; set; }

    public decimal StockBefore { get; set; }

    public decimal StockAfter { get; set; }

    [StringLength(500)]
    public string? Remarks { get; set; }

    public int LineNumber { get; set; }
}
