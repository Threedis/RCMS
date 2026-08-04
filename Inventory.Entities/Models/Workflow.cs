using System.ComponentModel.DataAnnotations;
using Inventory.Common.Enums;

namespace Inventory.Entities.Models;

/// <summary>
/// One row per state transition of an inventory document. Together the rows
/// form the complete, immutable approval trail shown on the document view.
/// </summary>
public class ApprovalHistory
{
    public long Id { get; set; }

    public DocumentType DocumentType { get; set; }

    /// <summary>Primary key of the header row the transition applies to.</summary>
    public int DocumentId { get; set; }

    [StringLength(30)]
    public string? DocumentNumber { get; set; }

    public ApprovalAction Action { get; set; }

    public DocumentStatus FromStatus { get; set; }

    public DocumentStatus ToStatus { get; set; }

    public int ActionBy { get; set; }

    [StringLength(150)]
    public string? ActionByName { get; set; }

    public DateTime ActionOn { get; set; } = DateTime.UtcNow;

    [StringLength(1000)]
    public string? Remarks { get; set; }

    /// <summary>Approval level, reserved for future multi-level workflows.</summary>
    public int Level { get; set; } = 1;
}

/// <summary>File attached to a document (challan scan, inspection report, photo).</summary>
public class Attachment
{
    public long Id { get; set; }

    public DocumentType? DocumentType { get; set; }

    /// <summary>Header row the file belongs to.</summary>
    public int? DocumentId { get; set; }

    /// <summary>Set when the attachment belongs to a Goods Receipt Note.</summary>
    public int? InwardHeaderId { get; set; }

    public InventoryInwardHeader? InwardHeader { get; set; }

    /// <summary>Set when the attachment belongs to a Material Issue.</summary>
    public int? OutwardHeaderId { get; set; }

    public InventoryOutwardHeader? OutwardHeader { get; set; }

    /// <summary>Original file name supplied by the browser (sanitised).</summary>
    [Required]
    [StringLength(260)]
    public string FileName { get; set; } = string.Empty;

    /// <summary>Randomised name actually written to disk; prevents collisions and traversal.</summary>
    [Required]
    [StringLength(100)]
    public string StoredFileName { get; set; } = string.Empty;

    /// <summary>Path relative to the configured storage root.</summary>
    [Required]
    [StringLength(500)]
    public string FilePath { get; set; } = string.Empty;

    [StringLength(100)]
    public string? ContentType { get; set; }

    public long FileSizeBytes { get; set; }

    /// <summary>SHA-256 of the stored bytes; used for duplicate detection.</summary>
    [StringLength(64)]
    public string? Checksum { get; set; }

    [StringLength(300)]
    public string? Description { get; set; }

    public DateTime UploadedOn { get; set; } = DateTime.UtcNow;

    public int UploadedBy { get; set; }

    public bool IsDeleted { get; set; }
}

/// <summary>In-app notification, optionally mirrored to e-mail.</summary>
public class Notification
{
    public long Id { get; set; }

    /// <summary>Recipient. Null means a broadcast to every active user.</summary>
    public int? UserId { get; set; }

    public ApplicationUser? User { get; set; }

    /// <summary>Role-targeted notification (for example every Approver).</summary>
    [StringLength(100)]
    public string? TargetRole { get; set; }

    public NotificationType NotificationType { get; set; } = NotificationType.Information;

    [Required]
    [StringLength(200)]
    public string Title { get; set; } = string.Empty;

    [Required]
    [StringLength(1000)]
    public string Message { get; set; } = string.Empty;

    /// <summary>Relative URL the notification links to.</summary>
    [StringLength(300)]
    public string? ActionUrl { get; set; }

    public DocumentType? DocumentType { get; set; }

    public int? DocumentId { get; set; }

    public bool IsRead { get; set; }

    public DateTime? ReadOn { get; set; }

    public bool IsEmailSent { get; set; }

    public DateTime? EmailSentOn { get; set; }

    public DateTime CreatedOn { get; set; } = DateTime.UtcNow;

    public int CreatedBy { get; set; }
}

/// <summary>
/// Immutable record of every meaningful operation. Written by the stored
/// procedures for data changes and by the web layer for security events.
/// </summary>
public class AuditLog
{
    public long Id { get; set; }

    public AuditAction Action { get; set; }

    /// <summary>Logical entity affected, e.g. <c>Item</c>, <c>GRN</c>.</summary>
    [StringLength(100)]
    public string? EntityName { get; set; }

    /// <summary>Primary key of the affected row, as text (keys vary in type).</summary>
    [StringLength(50)]
    public string? EntityId { get; set; }

    [StringLength(500)]
    public string? Description { get; set; }

    /// <summary>JSON snapshot before the change (updates and deletes).</summary>
    public string? OldValues { get; set; }

    /// <summary>JSON snapshot after the change (inserts and updates).</summary>
    public string? NewValues { get; set; }

    public int? UserId { get; set; }

    [StringLength(256)]
    public string? UserName { get; set; }

    [StringLength(45)]
    public string? IpAddress { get; set; }

    [StringLength(400)]
    public string? UserAgent { get; set; }

    /// <summary>Controller/action or service method that produced the entry.</summary>
    [StringLength(200)]
    public string? Source { get; set; }

    public bool IsSuccessful { get; set; } = true;

    [StringLength(1000)]
    public string? ErrorMessage { get; set; }

    public DateTime CreatedOn { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Sequence generator backing the auto numbers (GRN, Issue, Item code).
/// Increments are performed inside the document stored procedures under an
/// <c>UPDLOCK</c> so numbers are gap-free and collision-free under concurrency.
/// </summary>
public class DocumentSequence
{
    public int Id { get; set; }

    /// <summary>Key such as <c>GRN</c>, <c>ISSUE</c> or <c>ITEM</c>.</summary>
    [Required]
    [StringLength(30)]
    public string SequenceKey { get; set; } = string.Empty;

    /// <summary>Text placed before the number, e.g. <c>GRN/</c>.</summary>
    [StringLength(20)]
    public string Prefix { get; set; } = string.Empty;

    /// <summary>Financial year the counter belongs to, e.g. <c>2026-27</c>.</summary>
    [StringLength(10)]
    public string? FinancialYear { get; set; }

    public int CurrentNumber { get; set; }

    /// <summary>Zero-padding width of the numeric part.</summary>
    public int PadWidth { get; set; } = 6;

    public DateTime ModifiedOn { get; set; } = DateTime.UtcNow;
}
