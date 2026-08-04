using System.ComponentModel.DataAnnotations;

namespace Inventory.Entities.Models;

/// <summary>
/// Columns every business table carries. The values are written by
/// <c>ApplicationDbContext.SaveChangesAsync</c> (for EF-tracked writes) and by
/// the stored procedures (for SP-driven writes), so the trail is complete
/// regardless of which path created the row.
/// </summary>
public abstract class AuditableEntity
{
    /// <summary>UTC timestamp of insertion.</summary>
    public DateTime CreatedOn { get; set; } = DateTime.UtcNow;

    /// <summary>User id that inserted the row (0 = system).</summary>
    public int CreatedBy { get; set; }

    /// <summary>UTC timestamp of the last update, null when never updated.</summary>
    public DateTime? ModifiedOn { get; set; }

    /// <summary>User id of the last update.</summary>
    public int? ModifiedBy { get; set; }

    /// <summary>
    /// Soft-delete flag. Nothing is ever physically removed: every query filter
    /// and every stored procedure excludes rows where this is false.
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>Set when the row is soft-deleted.</summary>
    public bool IsDeleted { get; set; }

    /// <summary>Optimistic concurrency token (SQL Server <c>rowversion</c>).</summary>
    [Timestamp]
    public byte[]? RowVersion { get; set; }
}

/// <summary>
/// Convenience base for the many "code + name" lookup tables
/// (Category, Unit, Department, Site, Warehouse, Manufacturer, Courier).
/// </summary>
public abstract class MasterEntity : AuditableEntity
{
    public int Id { get; set; }

    /// <summary>Short unique business code, e.g. <c>CAT-001</c>.</summary>
    [Required]
    [StringLength(30)]
    public string Code { get; set; } = string.Empty;

    /// <summary>Display name.</summary>
    [Required]
    [StringLength(150)]
    public string Name { get; set; } = string.Empty;

    /// <summary>Optional free-text description.</summary>
    [StringLength(500)]
    public string? Description { get; set; }

    /// <summary>Sort order used when the list is rendered in a drop-down.</summary>
    public int DisplayOrder { get; set; }
}
