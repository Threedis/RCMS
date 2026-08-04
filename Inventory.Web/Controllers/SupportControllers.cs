using System.Diagnostics;
using Inventory.BLL.Interfaces;
using Inventory.Common.Constants;
using Inventory.Common.Enums;
using Inventory.Entities.Dtos;
using Inventory.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Inventory.Web.Controllers;

/// <summary>Vendor master.</summary>
[Authorize(Policy = Policies.ReadOnly)]
public sealed class VendorController : BaseController
{
    private static readonly string[] SortableColumns =
    {
        "Code", "Name", "ContactPerson", "ContactNumber", "City",
        "GstNumber", "CreditDays", "TotalReceipts", "TotalPurchaseValue", "IsActive"
    };

    private readonly IVendorService _vendors;

    public VendorController(IVendorService vendors) => _vendors = vendors;

    [HttpGet]
    public IActionResult Index() => View();

    [HttpPost]
    public async Task<IActionResult> List(bool? blacklistedOnly, CancellationToken cancellationToken)
    {
        var request = BuildPagedRequest(SortableColumns);
        var page = await _vendors.GetPagedAsync(request, blacklistedOnly, cancellationToken).ConfigureAwait(false);

        return DataTable(page, request.Draw);
    }

    [HttpGet]
    public async Task<IActionResult> Get(int id, CancellationToken cancellationToken)
    {
        var vendor = await _vendors.GetByIdAsync(id, cancellationToken).ConfigureAwait(false);

        return vendor is null ? JsonFail("The vendor was not found.") : JsonOk(vendor, "Loaded.");
    }

    [HttpPost]
    [Authorize(Policy = Policies.ManageMasters)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Save(VendorDto model, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(model);

        if (!ModelState.IsValid)
        {
            return ValidationFailed();
        }

        var result = model.Id > 0
            ? await _vendors.UpdateAsync(model, cancellationToken).ConfigureAwait(false)
            : await _vendors.CreateAsync(model, cancellationToken).ConfigureAwait(false);

        return result.Succeeded
            ? JsonOk(null, result.Message)
            : JsonFail(result.Message, result.Errors);
    }

    [HttpPost]
    [Authorize(Policy = Policies.ManageMasters)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        var result = await _vendors.DeleteAsync(id, cancellationToken).ConfigureAwait(false);

        return result.Succeeded ? JsonOk(null, result.Message) : JsonFail(result.Message);
    }

    [HttpGet]
    public async Task<IActionResult> IsCodeAvailable(string code, int id = 0, CancellationToken cancellationToken = default)
        => Json(await _vendors.IsCodeAvailableAsync(code, id, cancellationToken).ConfigureAwait(false)
            ? (object)true
            : $"The vendor code '{code}' is already in use.");

    [HttpGet]
    public async Task<IActionResult> IsGstAvailable(string gst, int id = 0, CancellationToken cancellationToken = default)
        => Json(await _vendors.IsGstAvailableAsync(gst, id, cancellationToken).ConfigureAwait(false)
            ? (object)true
            : $"GSTIN '{gst}' is already registered against another vendor.");

    [HttpGet]
    [Authorize(Policy = Policies.ViewReports)]
    public async Task<IActionResult> Export(string? format, CancellationToken cancellationToken)
        => Download(await _vendors.ExportAsync(ParseFormat(format), cancellationToken).ConfigureAwait(false));
}

/// <summary>
/// The approval queue: one screen showing every document waiting on the signed-in
/// user, whichever type it is.
/// </summary>
[Authorize(Policy = Policies.ApproveDocuments)]
public sealed class ApprovalController : BaseController
{
    private readonly IApprovalService _approvals;
    private readonly IGrnService _grn;
    private readonly IIssueService _issues;

    public ApprovalController(IApprovalService approvals, IGrnService grn, IIssueService issues)
    {
        _approvals = approvals;
        _grn = grn;
        _issues = issues;
    }

    [HttpGet]
    public async Task<IActionResult> Index(DocumentType? documentType, CancellationToken cancellationToken)
    {
        var pending = await _approvals.GetPendingAsync(documentType, cancellationToken).ConfigureAwait(false);

        ViewData["DocumentType"] = documentType;
        return View(pending);
    }

    [HttpGet]
    public async Task<IActionResult> Pending(DocumentType? documentType, CancellationToken cancellationToken)
    {
        var pending = await _approvals.GetPendingAsync(documentType, cancellationToken).ConfigureAwait(false);
        return JsonOk(pending, $"{pending.Count} document(s) awaiting a decision.");
    }

    [HttpGet]
    public async Task<IActionResult> Count(CancellationToken cancellationToken)
        => JsonOk(new { count = await _approvals.GetPendingCountAsync(cancellationToken).ConfigureAwait(false) }, "Loaded.");

    /// <summary>Loads the document behind a queue row so the approver can review it.</summary>
    [HttpGet]
    public async Task<IActionResult> Review(DocumentType documentType, int id, CancellationToken cancellationToken)
        => documentType switch
        {
            DocumentType.GoodsReceiptNote =>
                await _grn.GetByIdAsync(id, cancellationToken).ConfigureAwait(false) is { } grn
                    ? JsonOk(grn, "Loaded.")
                    : JsonFail("The goods receipt note was not found."),

            DocumentType.MaterialIssue =>
                await _issues.GetByIdAsync(id, cancellationToken).ConfigureAwait(false) is { } issue
                    ? JsonOk(issue, "Loaded.")
                    : JsonFail("The material issue was not found."),

            _ => JsonFail("Unsupported document type.")
        };

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Decide(ApprovalRequestDto request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!request.Approve && string.IsNullOrWhiteSpace(request.Remarks))
        {
            return JsonFail("Enter a reason for the rejection.");
        }

        var result = await _approvals.DecideAsync(request, cancellationToken).ConfigureAwait(false);

        return result.Succeeded
            ? JsonOk(null, result.Message)
            : JsonFail(result.Message, result.Errors);
    }

    /// <summary>The approval trail shown on a document's timeline.</summary>
    [HttpGet]
    [Authorize(Policy = Policies.ReadOnly)]
    public async Task<IActionResult> Trail(DocumentType documentType, int id, CancellationToken cancellationToken)
    {
        var trail = await _approvals.GetTrailAsync(documentType, id, cancellationToken).ConfigureAwait(false);
        return JsonOk(trail, "Loaded.");
    }
}

/// <summary>Stock enquiry screens: current stock and the ledger.</summary>
[Authorize(Policy = Policies.ReadOnly)]
public sealed class StockController : BaseController
{
    private static readonly string[] StockColumns =
    {
        "ItemCode", "ItemName", "CategoryName", "WarehouseName",
        "Quantity", "ReservedQuantity", "AvailableQuantity", "AverageCost", "StockValue"
    };

    private static readonly string[] LedgerColumns =
    {
        "TransactionDate", "ItemCode", "ItemName", "WarehouseName",
        "MovementTypeName", "DocumentNumber", "InwardQuantity", "OutwardQuantity", "BalanceQuantity"
    };

    private readonly IStockService _stock;
    private readonly ILookupService _lookups;

    public StockController(IStockService stock, ILookupService lookups)
    {
        _stock = stock;
        _lookups = lookups;
    }

    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
        => View(new FilterViewModel
        {
            Lookups = await LookupBundle.LoadAsync(_lookups, cancellationToken).ConfigureAwait(false),
            VisibleFilters = new[] { "category", "warehouse", "site", "itemType" }
        });

    [HttpPost]
    public async Task<IActionResult> List(ReportFilter filter, CancellationToken cancellationToken)
    {
        var request = BuildPagedRequest(StockColumns);
        var page = await _stock.GetCurrentStockAsync(request, filter ?? new ReportFilter(), cancellationToken)
            .ConfigureAwait(false);

        return DataTable(page, request.Draw);
    }

    [HttpGet]
    public async Task<IActionResult> Ledger(CancellationToken cancellationToken)
        => View(new FilterViewModel
        {
            Filter = new ReportFilter { FromDate = DateTime.Today.AddMonths(-3), ToDate = DateTime.Today },
            Lookups = await LookupBundle.LoadAsync(_lookups, cancellationToken).ConfigureAwait(false),
            VisibleFilters = new[] { "item", "warehouse", "dateRange" }
        });

    [HttpPost]
    public async Task<IActionResult> LedgerList(ReportFilter filter, CancellationToken cancellationToken)
    {
        var request = BuildPagedRequest(LedgerColumns);
        var page = await _stock.GetLedgerAsync(request, filter ?? new ReportFilter(), cancellationToken)
            .ConfigureAwait(false);

        return DataTable(page, request.Draw);
    }

    [HttpGet]
    public async Task<IActionResult> LowStock(int? warehouseId, CancellationToken cancellationToken)
    {
        var items = await _stock.GetLowStockAsync(warehouseId, cancellationToken).ConfigureAwait(false);
        return View(items);
    }
}

/// <summary>The report catalog and the generic report runner.</summary>
[Authorize(Policy = Policies.ViewReports)]
public sealed class ReportController : BaseController
{
    private readonly IReportService _reports;
    private readonly ILookupService _lookups;

    public ReportController(IReportService reports, ILookupService lookups)
    {
        _reports = reports;
        _lookups = lookups;
    }

    /// <summary>The report catalog, grouped for the menu.</summary>
    [HttpGet]
    public IActionResult Index() => View(_reports.GetCatalog());

    /// <summary>
    /// Renders a report screen; the grid is empty until the user runs it.
    /// The method is named Show but routed as /Report/View so it cannot shadow
    /// Controller.View.
    /// </summary>
    [HttpGet]
    [ActionName("View")]
    public async Task<IActionResult> Show(string id, CancellationToken cancellationToken)
    {
        var descriptor = _reports.GetCatalog()
            .FirstOrDefault(r => string.Equals(r.Key, id, StringComparison.OrdinalIgnoreCase));

        if (descriptor is null)
        {
            Error($"Report '{id}' does not exist.");
            return RedirectToAction(nameof(Index));
        }

        return View("View", new ReportViewModel
        {
            Descriptor = descriptor,
            Filter = new ReportFilter
            {
                FromDate = DateTime.Today.AddMonths(-1),
                ToDate = DateTime.Today
            },
            Lookups = await LookupBundle.LoadAsync(_lookups, cancellationToken).ConfigureAwait(false)
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Run(string id, ReportFilter filter, CancellationToken cancellationToken)
    {
        var descriptor = _reports.GetCatalog()
            .FirstOrDefault(r => string.Equals(r.Key, id, StringComparison.OrdinalIgnoreCase));

        if (descriptor is null)
        {
            return JsonFail($"Report '{id}' does not exist.");
        }

        var output = await _reports
            .RunAsync(id, filter ?? new ReportFilter(), cancellationToken)
            .ConfigureAwait(false);

        return JsonOk(output, $"{output.TotalRows:N0} row(s).");
    }

    [HttpGet]
    public async Task<IActionResult> Export(
        string id,
        string? format,
        ReportFilter filter,
        CancellationToken cancellationToken)
    {
        var file = await _reports
            .ExportAsync(id, filter ?? new ReportFilter(), ParseFormat(format), cancellationToken)
            .ConfigureAwait(false);

        return Download(file);
    }
}

/// <summary>Notification bell and the notification list.</summary>
[Authorize(Policy = Policies.ReadOnly)]
public sealed class NotificationController : BaseController
{
    private readonly INotificationService _notifications;

    public NotificationController(INotificationService notifications) => _notifications = notifications;

    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
        => View(await _notifications.GetForCurrentUserAsync(false, 100, cancellationToken).ConfigureAwait(false));

    /// <summary>Feeds the bell drop-down.</summary>
    [HttpGet]
    public async Task<IActionResult> Latest(bool unreadOnly = true, CancellationToken cancellationToken = default)
    {
        var items = await _notifications
            .GetForCurrentUserAsync(unreadOnly, 10, cancellationToken)
            .ConfigureAwait(false);

        var unread = await _notifications.GetUnreadCountAsync(cancellationToken).ConfigureAwait(false);

        return JsonOk(new { items, unread }, "Loaded.");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkRead(long id, CancellationToken cancellationToken)
    {
        var result = await _notifications.MarkReadAsync(id, cancellationToken).ConfigureAwait(false);
        return result.Succeeded ? JsonOk(null, result.Message) : JsonFail(result.Message);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkAllRead(CancellationToken cancellationToken)
    {
        var result = await _notifications.MarkAllReadAsync(cancellationToken).ConfigureAwait(false);
        return result.Succeeded ? JsonOk(null, result.Message) : JsonFail(result.Message);
    }
}

/// <summary>Attachment upload, download and removal.</summary>
[Authorize(Policy = Policies.ReadOnly)]
public sealed class FileController : BaseController
{
    private readonly IAttachmentService _attachments;

    public FileController(IAttachmentService attachments) => _attachments = attachments;

    [HttpPost]
    [Authorize(Policy = Policies.ManageInventory)]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(AppConstants.MaxUploadSizeBytes)]
    public async Task<IActionResult> Upload(
        IFormFile? file,
        DocumentType documentType,
        int documentId,
        string? description,
        CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
        {
            return JsonFail("Choose a file to upload.");
        }

        if (file.Length > AppConstants.MaxUploadSizeBytes)
        {
            return JsonFail($"The file exceeds the {AppConstants.MaxUploadSizeBytes / 1024 / 1024} MB limit.");
        }

        var extension = Path.GetExtension(file.FileName)?.ToLowerInvariant();

        if (string.IsNullOrWhiteSpace(extension)
            || !AppConstants.AllowedUploadExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase))
        {
            return JsonFail(
                $"Files of type '{extension}' are not accepted. " +
                $"Allowed: {string.Join(", ", AppConstants.AllowedUploadExtensions)}.");
        }

        await using var stream = file.OpenReadStream();

        var result = await _attachments.UploadAsync(
            stream, file.FileName, file.ContentType, documentType, documentId, description, cancellationToken)
            .ConfigureAwait(false);

        return result.Succeeded
            ? JsonOk(result.Data, result.Message)
            : JsonFail(result.Message);
    }

    /// <summary>
    /// Streams a stored file. Attachments are never served as static content:
    /// every download passes through this action so authorisation applies.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Download(long id, CancellationToken cancellationToken)
    {
        var file = await _attachments.DownloadAsync(id, cancellationToken).ConfigureAwait(false);

        if (file is null)
        {
            return NotFound();
        }

        var (content, contentType, fileName) = file.Value;

        // Content-Disposition: attachment stops a stored HTML or SVG file from
        // executing in the origin of this application.
        return File(content, contentType, fileName);
    }

    [HttpPost]
    [Authorize(Policy = Policies.ManageInventory)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(long id, CancellationToken cancellationToken)
    {
        var result = await _attachments.DeleteAsync(id, cancellationToken).ConfigureAwait(false);
        return result.Succeeded ? JsonOk(null, result.Message) : JsonFail(result.Message);
    }
}

/// <summary>Searchable audit trail and login history.</summary>
[Authorize(Policy = Policies.AdministratorOnly)]
public sealed class AuditController : BaseController
{
    private static readonly string[] SortableColumns =
    {
        "CreatedOn", "UserName", "ActionName", "EntityName", "EntityId", "Description", "IpAddress"
    };

    private readonly IAuditService _audit;

    public AuditController(IAuditService audit) => _audit = audit;

    [HttpGet]
    public IActionResult Index() => View();

    [HttpPost]
    public async Task<IActionResult> List(
        DateTime? fromDate,
        DateTime? toDate,
        int? userId,
        string? entityName,
        int? action,
        CancellationToken cancellationToken)
    {
        var request = BuildPagedRequest(SortableColumns);

        var page = await _audit
            .GetLogsAsync(request, fromDate, toDate, userId, entityName, action, cancellationToken)
            .ConfigureAwait(false);

        return DataTable(page, request.Draw);
    }

    [HttpGet]
    public async Task<IActionResult> Detail(long id, CancellationToken cancellationToken)
    {
        var log = await _audit.GetLogAsync(id, cancellationToken).ConfigureAwait(false);
        return log is null ? JsonFail("The audit entry was not found.") : JsonOk(log, "Loaded.");
    }

    [HttpGet]
    public IActionResult LoginHistory() => View();

    [HttpPost]
    public async Task<IActionResult> LoginHistoryList(
        DateTime? fromDate,
        DateTime? toDate,
        CancellationToken cancellationToken)
    {
        var request = BuildPagedRequest(new[] { "LoginOn", "UserName", "IsSuccessful", "IpAddress" });

        var page = await _audit
            .GetLoginHistoryAsync(request, fromDate, toDate, cancellationToken)
            .ConfigureAwait(false);

        return DataTable(page, request.Draw);
    }

    [HttpGet]
    public async Task<IActionResult> Export(
        string? format,
        DateTime? fromDate,
        DateTime? toDate,
        CancellationToken cancellationToken)
        => Download(await _audit
            .ExportAsync(ParseFormat(format), fromDate, toDate, cancellationToken)
            .ConfigureAwait(false));
}

/// <summary>Application user administration.</summary>
[Authorize(Policy = Policies.AdministratorOnly)]
public sealed class UserController : BaseController
{
    private static readonly string[] SortableColumns =
    {
        "UserName", "FullName", "Email", "EmployeeCode", "DepartmentName", "IsActive"
    };

    private readonly IUserService _users;
    private readonly ILookupService _lookups;

    public UserController(IUserService users, ILookupService lookups)
    {
        _users = users;
        _lookups = lookups;
    }

    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        ViewData["Lookups"] = await LookupBundle.LoadAsync(_lookups, cancellationToken).ConfigureAwait(false);
        ViewData["Roles"] = await _users.GetRolesAsync(cancellationToken).ConfigureAwait(false);

        return View();
    }

    [HttpPost]
    public async Task<IActionResult> List(CancellationToken cancellationToken)
    {
        var request = BuildPagedRequest(SortableColumns);
        var page = await _users.GetPagedAsync(request, cancellationToken).ConfigureAwait(false);

        return DataTable(page, request.Draw);
    }

    [HttpGet]
    public async Task<IActionResult> Get(int id, CancellationToken cancellationToken)
    {
        var user = await _users.GetByIdAsync(id, cancellationToken).ConfigureAwait(false);
        return user is null ? JsonFail("The user was not found.") : JsonOk(user, "Loaded.");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Save(UserDto model, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(model);

        // The password fields are only required when creating an account.
        if (model.Id > 0)
        {
            ModelState.Remove(nameof(UserDto.Password));
            ModelState.Remove(nameof(UserDto.ConfirmPassword));
        }

        if (!ModelState.IsValid)
        {
            return ValidationFailed();
        }

        var result = model.Id > 0
            ? await _users.UpdateAsync(model, cancellationToken).ConfigureAwait(false)
            : await _users.CreateAsync(model, cancellationToken).ConfigureAwait(false);

        return result.Succeeded
            ? JsonOk(null, result.Message)
            : JsonFail(result.Message, result.Errors);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Deactivate(int id, CancellationToken cancellationToken)
    {
        var result = await _users.DeactivateAsync(id, cancellationToken).ConfigureAwait(false);
        return result.Succeeded ? JsonOk(null, result.Message) : JsonFail(result.Message);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResetPassword(int id, string newPassword, CancellationToken cancellationToken)
    {
        var result = await _users.ResetPasswordAsync(id, newPassword, cancellationToken).ConfigureAwait(false);
        return result.Succeeded ? JsonOk(null, result.Message) : JsonFail(result.Message);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Unlock(int id, CancellationToken cancellationToken)
    {
        var result = await _users.UnlockAsync(id, cancellationToken).ConfigureAwait(false);
        return result.Succeeded ? JsonOk(null, result.Message) : JsonFail(result.Message);
    }
}

/// <summary>Error pages and the cascading-lookup endpoints shared by every form.</summary>
[Authorize(Policy = Policies.ReadOnly)]
public sealed class HomeController : BaseController
{
    private readonly ILookupService _lookups;

    public HomeController(ILookupService lookups) => _lookups = lookups;

    [HttpGet]
    public IActionResult Index() => RedirectToAction("Index", "Dashboard");

    /// <summary>Warehouses for a site; drives the cascading warehouse select.</summary>
    [HttpGet]
    public async Task<IActionResult> Warehouses(int? siteId, CancellationToken cancellationToken)
        => JsonOk(await _lookups.WarehousesAsync(siteId, cancellationToken).ConfigureAwait(false), "Loaded.");

    /// <summary>Engineers for a department; drives the cascading engineer select.</summary>
    [HttpGet]
    public async Task<IActionResult> Engineers(int? departmentId, CancellationToken cancellationToken)
        => JsonOk(await _lookups.EngineersAsync(departmentId, cancellationToken).ConfigureAwait(false), "Loaded.");

    [AllowAnonymous]
    [HttpGet]
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error(int? code)
        => View(new ErrorViewModel
        {
            RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier,
            StatusCode = code ?? 500,
            Title = "Something went wrong",
            Message = "An unexpected error occurred. The support team has been notified."
        });

    /// <summary>
    /// Friendly page for a non-success status code. Named HttpStatus but routed
    /// as /Home/StatusCode so it cannot shadow ControllerBase.StatusCode.
    /// </summary>
    [AllowAnonymous]
    [HttpGet]
    [ActionName("StatusCode")]
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult HttpStatus(int? code)
    {
        var (title, message) = code switch
        {
            400 => ("Bad request", "The request could not be understood."),
            401 => ("Sign-in required", "Please sign in to continue."),
            403 => ("Access denied", "You do not have permission to view this page."),
            404 => ("Page not found", "The page you asked for does not exist or has been moved."),
            408 => ("Request timed out", "The request took too long. Please try again."),
            500 => ("Server error", "Something went wrong on our side. The support team has been notified."),
            _ => ("Unexpected response", "The request could not be completed.")
        };

        return View("Error", new ErrorViewModel
        {
            RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier,
            StatusCode = code ?? 500,
            Title = title,
            Message = message
        });
    }
}
