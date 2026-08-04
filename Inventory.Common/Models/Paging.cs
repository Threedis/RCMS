using Inventory.Common.Constants;

namespace Inventory.Common.Models;

/// <summary>
/// Server-side paging / sorting / searching request. Bound directly from the
/// DataTables AJAX payload by the controllers and forwarded, unchanged, all the
/// way down to the stored procedure.
/// </summary>
public class PagedRequest
{
    private int _pageNumber = 1;
    private int _pageSize = AppConstants.DefaultPageSize;

    /// <summary>1-based page index.</summary>
    public int PageNumber
    {
        get => _pageNumber;
        set => _pageNumber = value < 1 ? 1 : value;
    }

    /// <summary>Rows per page, clamped to <see cref="AppConstants.MaxPageSize"/>.</summary>
    public int PageSize
    {
        get => _pageSize;
        set => _pageSize = value switch
        {
            < 1 => AppConstants.DefaultPageSize,
            > AppConstants.MaxPageSize => AppConstants.MaxPageSize,
            _ => value
        };
    }

    /// <summary>Free-text search term applied by the stored procedure.</summary>
    public string? SearchTerm { get; set; }

    /// <summary>Column to sort by. Validated against a white-list in SQL.</summary>
    public string? SortColumn { get; set; }

    /// <summary>Sort direction, <c>ASC</c> or <c>DESC</c>.</summary>
    public string SortDirection { get; set; } = "ASC";

    /// <summary>Rows to skip; derived from <see cref="PageNumber"/>.</summary>
    public int Skip => (PageNumber - 1) * PageSize;

    /// <summary>Normalised, injection-safe sort direction.</summary>
    public string SafeSortDirection =>
        string.Equals(SortDirection, "DESC", StringComparison.OrdinalIgnoreCase) ? "DESC" : "ASC";

    /// <summary>Echoed back to DataTables so it can discard stale responses.</summary>
    public int Draw { get; set; }
}

/// <summary>A single page of results plus the total row count.</summary>
/// <typeparam name="T">Row type.</typeparam>
public class PagedResult<T>
{
    public PagedResult()
    {
    }

    public PagedResult(IReadOnlyList<T> items, int totalCount, int pageNumber, int pageSize)
    {
        Items = items;
        TotalCount = totalCount;
        PageNumber = pageNumber;
        PageSize = pageSize;
    }

    /// <summary>Rows belonging to the requested page.</summary>
    public IReadOnlyList<T> Items { get; set; } = Array.Empty<T>();

    /// <summary>Total rows matching the filter across all pages.</summary>
    public int TotalCount { get; set; }

    public int PageNumber { get; set; } = 1;

    public int PageSize { get; set; } = AppConstants.DefaultPageSize;

    /// <summary>Total number of pages for the current page size.</summary>
    public int TotalPages => PageSize <= 0 ? 0 : (int)Math.Ceiling(TotalCount / (double)PageSize);

    public bool HasPreviousPage => PageNumber > 1;

    public bool HasNextPage => PageNumber < TotalPages;

    /// <summary>Convenience factory for an empty page.</summary>
    public static PagedResult<T> Empty(int pageSize = AppConstants.DefaultPageSize)
        => new(Array.Empty<T>(), 0, 1, pageSize);
}

/// <summary>Shape expected by the jQuery DataTables server-side processing API.</summary>
/// <typeparam name="T">Row type.</typeparam>
public class DataTableResponse<T>
{
    public int Draw { get; set; }

    public int RecordsTotal { get; set; }

    public int RecordsFiltered { get; set; }

    public IReadOnlyList<T> Data { get; set; } = Array.Empty<T>();

    public string? Error { get; set; }

    public static DataTableResponse<T> From(PagedResult<T> page, int draw)
        => new()
        {
            Draw = draw,
            RecordsTotal = page.TotalCount,
            RecordsFiltered = page.TotalCount,
            Data = page.Items
        };
}
