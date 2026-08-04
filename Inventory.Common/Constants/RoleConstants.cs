namespace Inventory.Common.Constants;

/// <summary>
/// The six application roles. Authorisation is role based; policies in
/// <see cref="Policies"/> compose these roles so controllers never hard-code
/// long role lists.
/// </summary>
public static class Roles
{
    public const string Administrator = "Administrator";
    public const string InventoryManager = "Inventory Manager";
    public const string StoreExecutive = "Store Executive";
    public const string Approver = "Approver";
    public const string ProjectManager = "Project Manager";
    public const string Viewer = "Viewer";

    /// <summary>All roles, used when seeding the database.</summary>
    public static readonly string[] All =
    {
        Administrator, InventoryManager, StoreExecutive, Approver, ProjectManager, Viewer
    };

    /// <summary>Roles allowed to create and edit inventory documents.</summary>
    public static readonly string[] CanTransact =
    {
        Administrator, InventoryManager, StoreExecutive
    };

    /// <summary>Roles allowed to approve or reject documents.</summary>
    public static readonly string[] CanApprove =
    {
        Administrator, InventoryManager, Approver
    };

    /// <summary>Roles allowed to maintain master data.</summary>
    public static readonly string[] CanManageMasters =
    {
        Administrator, InventoryManager
    };
}

/// <summary>
/// Authorization policy names registered in <c>Program.cs</c>.
/// Use <c>[Authorize(Policy = Policies.X)]</c> rather than role literals.
/// </summary>
public static class Policies
{
    /// <summary>Administrator only — user management, system settings, audit purge.</summary>
    public const string AdministratorOnly = "Policy.AdministratorOnly";

    /// <summary>Create/update master data.</summary>
    public const string ManageMasters = "Policy.ManageMasters";

    /// <summary>Create/update inward and outward documents.</summary>
    public const string ManageInventory = "Policy.ManageInventory";

    /// <summary>Approve or reject documents.</summary>
    public const string ApproveDocuments = "Policy.ApproveDocuments";

    /// <summary>View reports and export them.</summary>
    public const string ViewReports = "Policy.ViewReports";

    /// <summary>Read-only access to the application (every authenticated role).</summary>
    public const string ReadOnly = "Policy.ReadOnly";
}

/// <summary>Custom claim types issued at sign-in.</summary>
public static class AppClaimTypes
{
    public const string FullName = "inv:full_name";
    public const string EmployeeCode = "inv:employee_code";
    public const string DepartmentId = "inv:department_id";
    public const string SiteId = "inv:site_id";
    public const string MustChangePassword = "inv:must_change_password";
}
