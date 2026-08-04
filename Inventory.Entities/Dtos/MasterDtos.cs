using System.ComponentModel.DataAnnotations;

namespace Inventory.Entities.Dtos;

/// <summary>
/// Shape shared by the seven "code + name" masters (Category, Unit, Department,
/// Site, Warehouse, Manufacturer, Courier). One DTO plus one generic service
/// removes seven near-identical CRUD implementations.
/// </summary>
public class MasterDto
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Code is required.")]
    [StringLength(30, ErrorMessage = "Code cannot exceed 30 characters.")]
    [Display(Name = "Code")]
    public string Code { get; set; } = string.Empty;

    [Required(ErrorMessage = "Name is required.")]
    [StringLength(150, ErrorMessage = "Name cannot exceed 150 characters.")]
    [Display(Name = "Name")]
    public string Name { get; set; } = string.Empty;

    [StringLength(500)]
    [Display(Name = "Description")]
    public string? Description { get; set; }

    [Display(Name = "Display Order")]
    [Range(0, 9999)]
    public int DisplayOrder { get; set; }

    [Display(Name = "Active")]
    public bool IsActive { get; set; } = true;

    public DateTime CreatedOn { get; set; }

    public string? CreatedByName { get; set; }

    public DateTime? ModifiedOn { get; set; }
}

/// <summary>Category master, adds an optional parent for a two-level tree.</summary>
public class CategoryDto : MasterDto
{
    [Display(Name = "Parent Category")]
    public int? ParentCategoryId { get; set; }

    public string? ParentCategoryName { get; set; }

    /// <summary>Number of items filed under the category; read-only.</summary>
    public int ItemCount { get; set; }
}

/// <summary>Unit of measure master.</summary>
public class UnitDto : MasterDto
{
    [Required(ErrorMessage = "Symbol is required.")]
    [StringLength(10)]
    [Display(Name = "Symbol")]
    public string Symbol { get; set; } = string.Empty;

    [Range(0, 3, ErrorMessage = "Decimal places must be between 0 and 3.")]
    [Display(Name = "Decimal Places")]
    public int DecimalPlaces { get; set; }
}

/// <summary>Department master.</summary>
public class DepartmentDto : MasterDto
{
    [StringLength(150)]
    [Display(Name = "Head of Department")]
    public string? HeadOfDepartment { get; set; }

    [EmailAddress(ErrorMessage = "Enter a valid e-mail address.")]
    [StringLength(150)]
    [Display(Name = "E-mail")]
    public string? Email { get; set; }
}

/// <summary>Site / project location master.</summary>
public class SiteDto : MasterDto
{
    [StringLength(400)]
    [Display(Name = "Address")]
    public string? Address { get; set; }

    [StringLength(100)]
    [Display(Name = "City")]
    public string? City { get; set; }

    [StringLength(100)]
    [Display(Name = "State")]
    public string? State { get; set; }

    [StringLength(20)]
    [RegularExpression(@"^\d{6}$", ErrorMessage = "PIN code must be 6 digits.")]
    [Display(Name = "PIN Code")]
    public string? PinCode { get; set; }

    [StringLength(150)]
    [Display(Name = "Contact Person")]
    public string? ContactPerson { get; set; }

    [StringLength(20)]
    [Phone(ErrorMessage = "Enter a valid contact number.")]
    [Display(Name = "Contact Number")]
    public string? ContactNumber { get; set; }

    public int WarehouseCount { get; set; }
}

/// <summary>Warehouse master; always belongs to a site.</summary>
public class WarehouseDto : MasterDto
{
    [Required(ErrorMessage = "Site is required.")]
    [Range(1, int.MaxValue, ErrorMessage = "Select a site.")]
    [Display(Name = "Site")]
    public int SiteId { get; set; }

    public string? SiteName { get; set; }

    [StringLength(150)]
    [Display(Name = "Store In-charge")]
    public string? InchargeName { get; set; }

    [StringLength(20)]
    [Display(Name = "Contact Number")]
    public string? ContactNumber { get; set; }

    [Display(Name = "Default Warehouse")]
    public bool IsDefault { get; set; }
}

/// <summary>Manufacturer / brand master.</summary>
public class ManufacturerDto : MasterDto
{
    [StringLength(100)]
    [Display(Name = "Country")]
    public string? Country { get; set; }

    [StringLength(200)]
    [Url(ErrorMessage = "Enter a valid URL.")]
    [Display(Name = "Website")]
    public string? Website { get; set; }
}

/// <summary>Courier master.</summary>
public class CourierDto : MasterDto
{
    [StringLength(150)]
    [Display(Name = "Contact Person")]
    public string? ContactPerson { get; set; }

    [StringLength(20)]
    [Display(Name = "Contact Number")]
    public string? ContactNumber { get; set; }

    [StringLength(300)]
    [Display(Name = "Tracking URL Template")]
    public string? TrackingUrlTemplate { get; set; }
}

/// <summary>Engineer master.</summary>
public class EngineerDto : MasterDto
{
    [StringLength(30)]
    [Display(Name = "Employee Code")]
    public string? EmployeeCode { get; set; }

    [Display(Name = "Department")]
    public int? DepartmentId { get; set; }

    public string? DepartmentName { get; set; }

    [Display(Name = "Site")]
    public int? SiteId { get; set; }

    public string? SiteName { get; set; }

    [EmailAddress(ErrorMessage = "Enter a valid e-mail address.")]
    [StringLength(150)]
    [Display(Name = "E-mail")]
    public string? Email { get; set; }

    [StringLength(20)]
    [Display(Name = "Mobile Number")]
    public string? MobileNumber { get; set; }

    [StringLength(150)]
    [Display(Name = "Designation")]
    public string? Designation { get; set; }
}

/// <summary>Vendor master. Carries the commercial and statutory attributes.</summary>
public class VendorDto : MasterDto
{
    [StringLength(400)]
    [Display(Name = "Address")]
    public string? Address { get; set; }

    [StringLength(100)]
    [Display(Name = "City")]
    public string? City { get; set; }

    [StringLength(100)]
    [Display(Name = "State")]
    public string? State { get; set; }

    [StringLength(20)]
    [Display(Name = "PIN Code")]
    public string? PinCode { get; set; }

    [StringLength(150)]
    [Display(Name = "Contact Person")]
    public string? ContactPerson { get; set; }

    [StringLength(20)]
    [Display(Name = "Contact Number")]
    public string? ContactNumber { get; set; }

    [EmailAddress(ErrorMessage = "Enter a valid e-mail address.")]
    [StringLength(150)]
    [Display(Name = "E-mail")]
    public string? Email { get; set; }

    [StringLength(20)]
    [RegularExpression(@"^[0-9]{2}[A-Z]{5}[0-9]{4}[A-Z]{1}[1-9A-Z]{1}Z[0-9A-Z]{1}$",
        ErrorMessage = "Enter a valid 15 character GSTIN.")]
    [Display(Name = "GST Number")]
    public string? GstNumber { get; set; }

    [StringLength(15)]
    [RegularExpression(@"^[A-Z]{5}[0-9]{4}[A-Z]{1}$", ErrorMessage = "Enter a valid 10 character PAN.")]
    [Display(Name = "PAN Number")]
    public string? PanNumber { get; set; }

    [StringLength(100)]
    [Display(Name = "Bank Name")]
    public string? BankName { get; set; }

    [StringLength(30)]
    [Display(Name = "Account Number")]
    public string? BankAccountNumber { get; set; }

    [StringLength(15)]
    [RegularExpression(@"^[A-Z]{4}0[A-Z0-9]{6}$", ErrorMessage = "Enter a valid IFSC code.")]
    [Display(Name = "IFSC Code")]
    public string? IfscCode { get; set; }

    [Range(0, 365)]
    [Display(Name = "Credit Days")]
    public int? CreditDays { get; set; }

    [Range(1, 5)]
    [Display(Name = "Rating")]
    public int? Rating { get; set; }

    [Display(Name = "Blacklisted")]
    public bool IsBlacklisted { get; set; }

    /// <summary>Aggregates shown on the vendor list; populated by the stored procedure.</summary>
    public int TotalReceipts { get; set; }

    public decimal TotalPurchaseValue { get; set; }

    public DateTime? LastSupplyDate { get; set; }
}
