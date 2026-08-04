using System.ComponentModel.DataAnnotations;
using Inventory.Common.Enums;

namespace Inventory.Entities.Dtos;

/// <summary>Attachment metadata returned to the UI (never the file bytes).</summary>
public class AttachmentDto
{
    public long Id { get; set; }

    public DocumentType? DocumentType { get; set; }

    public int? DocumentId { get; set; }

    public string FileName { get; set; } = string.Empty;

    public string? ContentType { get; set; }

    public long FileSizeBytes { get; set; }

    /// <summary>Human readable size, e.g. <c>1.4 MB</c>.</summary>
    public string FileSizeDisplay =>
        FileSizeBytes >= 1024 * 1024
            ? $"{FileSizeBytes / 1024d / 1024d:0.##} MB"
            : $"{Math.Max(1, FileSizeBytes / 1024d):0.##} KB";

    public string? Description { get; set; }

    public DateTime UploadedOn { get; set; }

    public string? UploadedByName { get; set; }

    /// <summary>Download URL; the file is streamed through a controller, never served statically.</summary>
    public string? DownloadUrl { get; set; }

    /// <summary>True when the browser can preview the file inline.</summary>
    public bool IsImage =>
        ContentType is not null && ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase);
}

/// <summary>In-app notification projected for the bell drop-down and the list screen.</summary>
public class NotificationDto
{
    public long Id { get; set; }

    public NotificationType NotificationType { get; set; }

    public string NotificationTypeName { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public string Message { get; set; } = string.Empty;

    public string? ActionUrl { get; set; }

    public bool IsRead { get; set; }

    public DateTime CreatedOn { get; set; }

    /// <summary>Relative age, e.g. <c>12 minutes ago</c>.</summary>
    public string TimeAgo
    {
        get
        {
            var span = DateTime.UtcNow - CreatedOn;
            return span switch
            {
                { TotalMinutes: < 1 } => "just now",
                { TotalMinutes: < 60 } => $"{(int)span.TotalMinutes} min ago",
                { TotalHours: < 24 } => $"{(int)span.TotalHours} hr ago",
                { TotalDays: < 30 } => $"{(int)span.TotalDays} day(s) ago",
                _ => CreatedOn.ToLocalTime().ToString("dd-MMM-yyyy")
            };
        }
    }

    /// <summary>Bootstrap contextual colour for the notification icon.</summary>
    public string BadgeClass => NotificationType switch
    {
        NotificationType.LowStock => "warning",
        NotificationType.PendingApproval => "info",
        NotificationType.Approved => "success",
        NotificationType.Rejected => "danger",
        NotificationType.MaterialReceipt => "primary",
        NotificationType.MaterialDispatch => "primary",
        NotificationType.SystemAlert => "danger",
        _ => "secondary"
    };
}

/// <summary>Application user, as shown and edited on the user master screen.</summary>
public class UserDto
{
    public int Id { get; set; }

    [Required(ErrorMessage = "User name is required.")]
    [StringLength(256)]
    [RegularExpression(@"^[a-zA-Z0-9._@\-]+$",
        ErrorMessage = "User name may contain letters, digits, dot, underscore, hyphen and @ only.")]
    [Display(Name = "User Name")]
    public string UserName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Full name is required.")]
    [StringLength(150)]
    [Display(Name = "Full Name")]
    public string FullName { get; set; } = string.Empty;

    [Required(ErrorMessage = "E-mail is required.")]
    [EmailAddress(ErrorMessage = "Enter a valid e-mail address.")]
    [StringLength(256)]
    [Display(Name = "E-mail")]
    public string Email { get; set; } = string.Empty;

    [Phone(ErrorMessage = "Enter a valid phone number.")]
    [StringLength(20)]
    [Display(Name = "Mobile Number")]
    public string? PhoneNumber { get; set; }

    [StringLength(30)]
    [Display(Name = "Employee Code")]
    public string? EmployeeCode { get; set; }

    [StringLength(150)]
    [Display(Name = "Designation")]
    public string? Designation { get; set; }

    [Display(Name = "Department")]
    public int? DepartmentId { get; set; }

    public string? DepartmentName { get; set; }

    [Display(Name = "Site")]
    public int? SiteId { get; set; }

    public string? SiteName { get; set; }

    /// <summary>Roles granted to the user. At least one is required.</summary>
    [Display(Name = "Roles")]
    public List<string> Roles { get; set; } = new();

    public string RolesDisplay => string.Join(", ", Roles);

    [Display(Name = "Active")]
    public bool IsActive { get; set; } = true;

    [Display(Name = "Locked Out")]
    public bool IsLockedOut { get; set; }

    public DateTime? LockoutEnd { get; set; }

    public DateTime? LastLoginOn { get; set; }

    public DateTime? PasswordChangedOn { get; set; }

    [Display(Name = "Force Password Change")]
    public bool MustChangePassword { get; set; }

    public DateTime CreatedOn { get; set; }

    /// <summary>
    /// Initial password. Required when creating a user, ignored on update
    /// (password changes go through the dedicated reset action).
    /// </summary>
    [DataType(DataType.Password)]
    [StringLength(100, MinimumLength = 8, ErrorMessage = "Password must be at least 8 characters.")]
    [Display(Name = "Password")]
    public string? Password { get; set; }

    [DataType(DataType.Password)]
    [Compare(nameof(Password), ErrorMessage = "The passwords do not match.")]
    [Display(Name = "Confirm Password")]
    public string? ConfirmPassword { get; set; }
}

/// <summary>Result of validating and importing one Excel row.</summary>
public class ImportRowResultDto
{
    /// <summary>1-based row number in the source worksheet, including the header.</summary>
    public int RowNumber { get; set; }

    /// <summary>Key column value echoed back so the user can find the row.</summary>
    public string? Key { get; set; }

    public bool IsValid { get; set; }

    /// <summary>True when the row matched an existing record and was skipped or updated.</summary>
    public bool IsDuplicate { get; set; }

    public List<string> Errors { get; set; } = new();

    public string ErrorsDisplay => string.Join(" | ", Errors);
}

/// <summary>Summary of an Excel import run.</summary>
public class ImportResultDto
{
    public string FileName { get; set; } = string.Empty;

    public int TotalRows { get; set; }

    public int ValidRows { get; set; }

    public int ImportedRows { get; set; }

    public int DuplicateRows { get; set; }

    public int FailedRows { get; set; }

    /// <summary>True when the run was a dry-run validation rather than a commit.</summary>
    public bool IsValidationOnly { get; set; }

    public List<ImportRowResultDto> Rows { get; set; } = new();

    /// <summary>Only the rows that failed; drives the downloadable error report.</summary>
    public IEnumerable<ImportRowResultDto> FailedRowDetails => Rows.Where(r => !r.IsValid);

    public bool HasErrors => FailedRows > 0;
}
