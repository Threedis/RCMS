using Inventory.BLL.Interfaces;
using Inventory.Common.Enums;
using Inventory.Common.Extensions;
using Inventory.Entities.Dtos;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace Inventory.Web.Models;

/// <summary>
/// Every drop-down a screen needs, resolved once by the controller.
/// Loading them together keeps a form's cold-start cost to one batch of cached
/// lookups instead of one round trip per select.
/// </summary>
public sealed class LookupBundle
{
    public IReadOnlyList<LookupDto> Categories { get; set; } = Array.Empty<LookupDto>();

    public IReadOnlyList<LookupDto> Units { get; set; } = Array.Empty<LookupDto>();

    public IReadOnlyList<LookupDto> Departments { get; set; } = Array.Empty<LookupDto>();

    public IReadOnlyList<LookupDto> Sites { get; set; } = Array.Empty<LookupDto>();

    public IReadOnlyList<LookupDto> Warehouses { get; set; } = Array.Empty<LookupDto>();

    public IReadOnlyList<LookupDto> Manufacturers { get; set; } = Array.Empty<LookupDto>();

    public IReadOnlyList<LookupDto> Couriers { get; set; } = Array.Empty<LookupDto>();

    public IReadOnlyList<LookupDto> Engineers { get; set; } = Array.Empty<LookupDto>();

    public IReadOnlyList<LookupDto> Vendors { get; set; } = Array.Empty<LookupDto>();

    /// <summary>Renders a lookup list as Razor select items.</summary>
    public static IEnumerable<SelectListItem> ToSelectList(
        IReadOnlyList<LookupDto> source,
        int? selectedId = null,
        bool showCode = false)
        => source.Select(item => new SelectListItem
        {
            Value = item.Id.ToString(),
            Text = showCode && !string.IsNullOrWhiteSpace(item.Code)
                ? $"{item.Code} — {item.Text}"
                : item.Text,
            Selected = selectedId.HasValue && selectedId.Value == item.Id
        });

    /// <summary>Loads the whole bundle from the lookup service.</summary>
    public static async Task<LookupBundle> LoadAsync(
        ILookupService lookups,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(lookups);

        return new LookupBundle
        {
            Categories = await lookups.CategoriesAsync(cancellationToken).ConfigureAwait(false),
            Units = await lookups.UnitsAsync(cancellationToken).ConfigureAwait(false),
            Departments = await lookups.DepartmentsAsync(cancellationToken).ConfigureAwait(false),
            Sites = await lookups.SitesAsync(cancellationToken).ConfigureAwait(false),
            Warehouses = await lookups.WarehousesAsync(null, cancellationToken).ConfigureAwait(false),
            Manufacturers = await lookups.ManufacturersAsync(cancellationToken).ConfigureAwait(false),
            Couriers = await lookups.CouriersAsync(cancellationToken).ConfigureAwait(false),
            Engineers = await lookups.EngineersAsync(null, cancellationToken).ConfigureAwait(false),
            Vendors = await lookups.VendorsAsync(cancellationToken).ConfigureAwait(false)
        };
    }
}

/// <summary>Wraps a document DTO with the lookups its form needs.</summary>
/// <typeparam name="TDocument">Document DTO type.</typeparam>
public sealed class DocumentFormViewModel<TDocument>
{
    public required TDocument Document { get; init; }

    public required LookupBundle Lookups { get; init; }

    /// <summary>False when the document is read-only in its current status.</summary>
    public bool IsEditable { get; init; } = true;
}

/// <summary>Filter panel state shared by the list and report screens.</summary>
public sealed class FilterViewModel
{
    public ReportFilter Filter { get; set; } = new();

    public LookupBundle Lookups { get; set; } = new();

    /// <summary>Which filter controls the current screen should render.</summary>
    public IReadOnlyList<string> VisibleFilters { get; set; } = Array.Empty<string>();

    public bool Shows(string filter)
        => VisibleFilters.Contains(filter, StringComparer.OrdinalIgnoreCase);

    /// <summary>Document statuses, for the status drop-down.</summary>
    public static IEnumerable<SelectListItem> StatusOptions(int? selected = null)
        => Enum.GetValues<DocumentStatus>().Select(status => new SelectListItem
        {
            Value = ((int)status).ToString(),
            Text = status.ToDisplayName(),
            Selected = selected.HasValue && selected.Value == (int)status
        });

    /// <summary>Item types, for the item-type drop-down.</summary>
    public static IEnumerable<SelectListItem> ItemTypeOptions(int? selected = null)
        => Enum.GetValues<ItemType>().Select(type => new SelectListItem
        {
            Value = ((int)type).ToString(),
            Text = type.ToDisplayName(),
            Selected = selected.HasValue && selected.Value == (int)type
        });
}

/// <summary>Report screen model: the catalog entry, its filters and its rows.</summary>
public sealed class ReportViewModel
{
    public required ReportDescriptor Descriptor { get; init; }

    public required ReportFilter Filter { get; init; }

    public required LookupBundle Lookups { get; init; }

    /// <summary>Null until the user runs the report.</summary>
    public ReportOutput? Output { get; init; }

    public bool HasRun => Output is not null;
}

/// <summary>Excel import wizard state.</summary>
public sealed class ImportViewModel
{
    /// <summary>What is being imported; currently only <c>items</c>.</summary>
    public string Target { get; set; } = "items";

    /// <summary>True to validate without writing anything.</summary>
    public bool ValidateOnly { get; set; } = true;

    /// <summary>Populated after a run.</summary>
    public ImportResultDto? Result { get; set; }

    public bool HasResult => Result is not null;
}

/// <summary>Model for the shared error page.</summary>
public sealed class ErrorViewModel
{
    public string? RequestId { get; set; }

    public bool ShowRequestId => !string.IsNullOrWhiteSpace(RequestId);

    public int StatusCode { get; set; } = 500;

    public string Title { get; set; } = "Something went wrong";

    public string Message { get; set; } =
        "An unexpected error occurred. The support team has been notified.";
}
