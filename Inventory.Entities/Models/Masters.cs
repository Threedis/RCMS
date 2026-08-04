using System.ComponentModel.DataAnnotations;

namespace Inventory.Entities.Models;

/// <summary>Item classification (Electrical, Mechanical, IT Hardware, ...).</summary>
public class Category : MasterEntity
{
    /// <summary>Optional self-reference for a two-level category tree.</summary>
    public int? ParentCategoryId { get; set; }

    public Category? ParentCategory { get; set; }

    public ICollection<Category> SubCategories { get; set; } = new List<Category>();

    public ICollection<Item> Items { get; set; } = new List<Item>();
}

/// <summary>Unit of measure (Nos, Metre, Litre, Kg, Set, ...).</summary>
public class Unit : MasterEntity
{
    /// <summary>Short symbol shown in grids, e.g. <c>Nos</c>.</summary>
    [StringLength(10)]
    public string Symbol { get; set; } = string.Empty;

    /// <summary>Decimal places allowed for quantities in this unit (0-3).</summary>
    public int DecimalPlaces { get; set; }

    public ICollection<Item> Items { get; set; } = new List<Item>();
}

/// <summary>Organisational department that consumes material.</summary>
public class Department : MasterEntity
{
    [StringLength(150)]
    public string? HeadOfDepartment { get; set; }

    [StringLength(150)]
    public string? Email { get; set; }

    public ICollection<Engineer> Engineers { get; set; } = new List<Engineer>();

    public ICollection<InventoryOutwardHeader> Issues { get; set; } = new List<InventoryOutwardHeader>();
}

/// <summary>Physical project site / location.</summary>
public class Site : MasterEntity
{
    [StringLength(400)]
    public string? Address { get; set; }

    [StringLength(100)]
    public string? City { get; set; }

    [StringLength(100)]
    public string? State { get; set; }

    [StringLength(20)]
    public string? PinCode { get; set; }

    [StringLength(150)]
    public string? ContactPerson { get; set; }

    [StringLength(20)]
    public string? ContactNumber { get; set; }

    public ICollection<Warehouse> Warehouses { get; set; } = new List<Warehouse>();
}

/// <summary>Store / warehouse that physically holds stock. Always belongs to a site.</summary>
public class Warehouse : MasterEntity
{
    public int SiteId { get; set; }

    public Site Site { get; set; } = null!;

    [StringLength(150)]
    public string? InchargeName { get; set; }

    [StringLength(20)]
    public string? ContactNumber { get; set; }

    /// <summary>Marks the warehouse used when the user does not pick one.</summary>
    public bool IsDefault { get; set; }

    public ICollection<StockLedger> LedgerEntries { get; set; } = new List<StockLedger>();
}

/// <summary>Original equipment manufacturer / brand of an item.</summary>
public class Manufacturer : MasterEntity
{
    [StringLength(100)]
    public string? Country { get; set; }

    [StringLength(200)]
    public string? Website { get; set; }

    public ICollection<Item> Items { get; set; } = new List<Item>();
}

/// <summary>Courier / logistics partner used for dispatch and inbound delivery.</summary>
public class Courier : MasterEntity
{
    [StringLength(150)]
    public string? ContactPerson { get; set; }

    [StringLength(20)]
    public string? ContactNumber { get; set; }

    /// <summary>Base URL for consignment tracking; the tracking number is appended.</summary>
    [StringLength(300)]
    public string? TrackingUrlTemplate { get; set; }
}

/// <summary>Field engineer who requests and receives material.</summary>
public class Engineer : MasterEntity
{
    [StringLength(30)]
    public string? EmployeeCode { get; set; }

    public int? DepartmentId { get; set; }

    public Department? Department { get; set; }

    public int? SiteId { get; set; }

    public Site? Site { get; set; }

    [StringLength(150)]
    public string? Email { get; set; }

    [StringLength(20)]
    public string? MobileNumber { get; set; }

    [StringLength(150)]
    public string? Designation { get; set; }

    public ICollection<InventoryOutwardHeader> Issues { get; set; } = new List<InventoryOutwardHeader>();
}

/// <summary>Supplier of material.</summary>
public class Vendor : MasterEntity
{
    [StringLength(400)]
    public string? Address { get; set; }

    [StringLength(100)]
    public string? City { get; set; }

    [StringLength(100)]
    public string? State { get; set; }

    [StringLength(20)]
    public string? PinCode { get; set; }

    [StringLength(150)]
    public string? ContactPerson { get; set; }

    [StringLength(20)]
    public string? ContactNumber { get; set; }

    [StringLength(150)]
    public string? Email { get; set; }

    /// <summary>Goods and Services Tax identification number.</summary>
    [StringLength(20)]
    public string? GstNumber { get; set; }

    /// <summary>Permanent Account Number.</summary>
    [StringLength(15)]
    public string? PanNumber { get; set; }

    [StringLength(100)]
    public string? BankName { get; set; }

    [StringLength(30)]
    public string? BankAccountNumber { get; set; }

    [StringLength(15)]
    public string? IfscCode { get; set; }

    /// <summary>Agreed payment terms in days.</summary>
    public int? CreditDays { get; set; }

    /// <summary>1-5 supplier performance rating maintained by the buyer.</summary>
    public int? Rating { get; set; }

    /// <summary>Blocks the vendor from being selected on new GRNs.</summary>
    public bool IsBlacklisted { get; set; }

    public ICollection<InventoryInwardHeader> Receipts { get; set; } = new List<InventoryInwardHeader>();
}
