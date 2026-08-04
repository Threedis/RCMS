namespace Inventory.Entities.Dtos;

/// <summary>
/// Minimal shape used to populate every drop-down / Select2 control.
/// Kept deliberately small so lookup payloads stay cheap to cache and transfer.
/// </summary>
public class LookupDto
{
    public int Id { get; set; }

    /// <summary>Text rendered in the list.</summary>
    public string Text { get; set; } = string.Empty;

    /// <summary>Business code, shown as a secondary label.</summary>
    public string? Code { get; set; }

    /// <summary>Optional grouping key (for example warehouses grouped by site).</summary>
    public int? ParentId { get; set; }

    /// <summary>Extra payload used by cascading controls (unit symbol, average cost, ...).</summary>
    public string? Extra { get; set; }
}

/// <summary>
/// Envelope returned by every AJAX endpoint so the client-side helper
/// (<c>site.js</c>) can handle success and failure uniformly.
/// </summary>
public class AjaxResponse
{
    public bool Success { get; set; }

    public string Message { get; set; } = string.Empty;

    public object? Data { get; set; }

    /// <summary>Field-level validation messages keyed by input name.</summary>
    public IDictionary<string, string[]>? Errors { get; set; }

    /// <summary>Optional URL the browser should navigate to after a success.</summary>
    public string? RedirectUrl { get; set; }

    public static AjaxResponse Ok(object? data = null, string message = "Saved successfully.")
        => new() { Success = true, Data = data, Message = message };

    public static AjaxResponse Fail(string message, IDictionary<string, string[]>? errors = null)
        => new() { Success = false, Message = message, Errors = errors };
}

/// <summary>Common date-range filter shared by every report request.</summary>
public class DateRangeFilter
{
    public DateTime? FromDate { get; set; }

    public DateTime? ToDate { get; set; }

    /// <summary>Inclusive start; defaults to the first day of the current month.</summary>
    public DateTime EffectiveFrom => (FromDate ?? new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1)).Date;

    /// <summary>Inclusive end; defaults to today.</summary>
    public DateTime EffectiveTo => (ToDate ?? DateTime.Today).Date;
}

/// <summary>Filter accepted by the stock, register and consumption reports.</summary>
public class ReportFilter : DateRangeFilter
{
    public int? ItemId { get; set; }

    public int? CategoryId { get; set; }

    public int? WarehouseId { get; set; }

    public int? SiteId { get; set; }

    public int? VendorId { get; set; }

    public int? DepartmentId { get; set; }

    public int? EngineerId { get; set; }

    public int? ItemType { get; set; }

    public int? Status { get; set; }

    public string? SearchTerm { get; set; }

    /// <summary>Financial year label, e.g. <c>2026-27</c>; used by the period reports.</summary>
    public string? FinancialYear { get; set; }

    public int? Year { get; set; }
}
