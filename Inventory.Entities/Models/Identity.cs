using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;

namespace Inventory.Entities.Models;

/// <summary>
/// Application user. Extends ASP.NET Core Identity with the organisational
/// attributes the inventory workflow needs (department, site, employee code)
/// and with password-expiry bookkeeping.
/// </summary>
public class ApplicationUser : IdentityUser<int>
{
    [Required]
    [StringLength(150)]
    public string FullName { get; set; } = string.Empty;

    [StringLength(30)]
    public string? EmployeeCode { get; set; }

    [StringLength(150)]
    public string? Designation { get; set; }

    /// <summary>Home department; drives the default filter on issue screens.</summary>
    public int? DepartmentId { get; set; }

    public Department? Department { get; set; }

    /// <summary>Home site; drives the default filter on stock screens.</summary>
    public int? SiteId { get; set; }

    public Site? Site { get; set; }

    /// <summary>Last successful password change; used to enforce expiry.</summary>
    public DateTime? PasswordChangedOn { get; set; }

    /// <summary>Forces a password change on next sign-in (new / reset accounts).</summary>
    public bool MustChangePassword { get; set; }

    public DateTime? LastLoginOn { get; set; }

    public DateTime CreatedOn { get; set; } = DateTime.UtcNow;

    public int CreatedBy { get; set; }

    public DateTime? ModifiedOn { get; set; }

    public int? ModifiedBy { get; set; }

    public bool IsActive { get; set; } = true;

    public bool IsDeleted { get; set; }

    public ICollection<LoginHistory> LoginHistories { get; set; } = new List<LoginHistory>();

    public ICollection<Notification> Notifications { get; set; } = new List<Notification>();
}

/// <summary>Application role. Kept as a distinct type so extra metadata can be added.</summary>
public class ApplicationRole : IdentityRole<int>
{
    public ApplicationRole()
    {
    }

    public ApplicationRole(string roleName)
        : base(roleName)
    {
    }

    [StringLength(300)]
    public string? Description { get; set; }

    public bool IsSystemRole { get; set; }
}

/// <summary>Successful and failed sign-in attempts, retained for security audit.</summary>
public class LoginHistory
{
    public long Id { get; set; }

    public int? UserId { get; set; }

    public ApplicationUser? User { get; set; }

    /// <summary>Captured even when the user id could not be resolved.</summary>
    [Required]
    [StringLength(256)]
    public string UserName { get; set; } = string.Empty;

    public DateTime LoginOn { get; set; } = DateTime.UtcNow;

    public DateTime? LogoutOn { get; set; }

    public bool IsSuccessful { get; set; }

    [StringLength(300)]
    public string? FailureReason { get; set; }

    [StringLength(45)]
    public string? IpAddress { get; set; }

    [StringLength(400)]
    public string? UserAgent { get; set; }

    [StringLength(100)]
    public string? SessionId { get; set; }
}
