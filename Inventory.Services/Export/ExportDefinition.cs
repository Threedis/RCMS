using System.Linq.Expressions;
using System.Reflection;

namespace Inventory.Services.Export;

/// <summary>
/// Declarative description of one exportable column. The Excel, PDF and CSV
/// writers all consume the same definition, so a report is laid out once and
/// every output format stays consistent.
/// </summary>
/// <typeparam name="T">Row type.</typeparam>
public sealed class ExportColumn<T>
{
    public ExportColumn(string header, Func<T, object?> value)
    {
        Header = header;
        Value = value;
    }

    /// <summary>Column heading.</summary>
    public string Header { get; }

    /// <summary>Extracts the cell value from a row.</summary>
    public Func<T, object?> Value { get; }

    /// <summary>Excel number format, e.g. <c>#,##0.00</c>.</summary>
    public string? Format { get; init; }

    /// <summary>Relative width used for PDF layout and Excel column sizing.</summary>
    public float Width { get; init; } = 1f;

    /// <summary>Right-align numeric columns; the writers apply this to every format.</summary>
    public bool AlignRight { get; init; }

    /// <summary>Include the column in a total row at the bottom of the sheet.</summary>
    public bool Summable { get; init; }
}

/// <summary>A complete export: title, filters echo, columns and rows.</summary>
/// <typeparam name="T">Row type.</typeparam>
public sealed class ExportDefinition<T>
{
    public ExportDefinition(string title, IReadOnlyList<ExportColumn<T>> columns, IReadOnlyList<T> rows)
    {
        Title = title;
        Columns = columns;
        Rows = rows;
    }

    /// <summary>Report title printed on the sheet and the PDF header.</summary>
    public string Title { get; }

    /// <summary>Optional sub-title, typically the applied date range.</summary>
    public string? SubTitle { get; init; }

    /// <summary>Filter summary lines printed under the title.</summary>
    public IReadOnlyList<string> FilterSummary { get; init; } = Array.Empty<string>();

    public IReadOnlyList<ExportColumn<T>> Columns { get; }

    public IReadOnlyList<T> Rows { get; }

    /// <summary>Worksheet name; defaults to a sanitised <see cref="Title"/>.</summary>
    public string SheetName { get; init; } = "Report";

    /// <summary>Print a totals row for the columns marked <c>Summable</c>.</summary>
    public bool ShowTotals { get; init; } = true;

    /// <summary>Print the PDF in landscape; ignored by the other writers.</summary>
    public bool Landscape { get; init; } = true;
}

/// <summary>
/// Fluent helper that builds an <see cref="ExportDefinition{T}"/> without the
/// caller having to construct column objects by hand.
/// </summary>
/// <typeparam name="T">Row type.</typeparam>
public sealed class ExportBuilder<T>
{
    private readonly List<ExportColumn<T>> _columns = new();
    private readonly List<string> _filters = new();
    private string _title = "Report";
    private string? _subTitle;
    private string _sheetName = "Report";
    private bool _showTotals = true;
    private bool _landscape = true;

    public ExportBuilder<T> WithTitle(string title)
    {
        _title = title;
        _sheetName = SanitiseSheetName(title);
        return this;
    }

    public ExportBuilder<T> WithSubTitle(string? subTitle)
    {
        _subTitle = subTitle;
        return this;
    }

    /// <summary>Adds a line to the filter summary, skipping blank values.</summary>
    public ExportBuilder<T> WithFilter(string label, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            _filters.Add($"{label}: {value}");
        }

        return this;
    }

    public ExportBuilder<T> Column(
        string header,
        Func<T, object?> value,
        string? format = null,
        float width = 1f,
        bool alignRight = false,
        bool summable = false)
    {
        _columns.Add(new ExportColumn<T>(header, value)
        {
            Format = format,
            Width = width,
            AlignRight = alignRight,
            Summable = summable
        });

        return this;
    }

    /// <summary>Adds a money column with the standard two-decimal format.</summary>
    public ExportBuilder<T> MoneyColumn(string header, Func<T, object?> value, bool summable = true)
        => Column(header, value, "#,##0.00", 1f, alignRight: true, summable: summable);

    /// <summary>Adds a quantity column with the standard three-decimal format.</summary>
    public ExportBuilder<T> QuantityColumn(string header, Func<T, object?> value, bool summable = true)
        => Column(header, value, "#,##0.###", 0.8f, alignRight: true, summable: summable);

    /// <summary>Adds a date column with the application-wide date format.</summary>
    public ExportBuilder<T> DateColumn(string header, Func<T, object?> value)
        => Column(header, value, "dd-MMM-yyyy", 0.8f);

    public ExportBuilder<T> ShowTotals(bool show)
    {
        _showTotals = show;
        return this;
    }

    public ExportBuilder<T> Landscape(bool landscape)
    {
        _landscape = landscape;
        return this;
    }

    /// <summary>
    /// Adds one column per public readable property. Useful for ad-hoc exports
    /// where a hand-written column list would add nothing.
    /// </summary>
    public ExportBuilder<T> AutoColumns()
    {
        foreach (var property in typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance)
                     .Where(p => p.CanRead && p.GetIndexParameters().Length == 0))
        {
            var captured = property;
            _columns.Add(new ExportColumn<T>(
                SplitPascalCase(captured.Name),
                row => captured.GetValue(row))
            {
                AlignRight = IsNumeric(captured.PropertyType)
            });
        }

        return this;
    }

    public ExportDefinition<T> Build(IReadOnlyList<T> rows)
        => new(_title, _columns, rows)
        {
            SubTitle = _subTitle,
            FilterSummary = _filters,
            SheetName = _sheetName,
            ShowTotals = _showTotals,
            Landscape = _landscape
        };

    private static bool IsNumeric(Type type)
    {
        var actual = Nullable.GetUnderlyingType(type) ?? type;
        return actual == typeof(int) || actual == typeof(long) || actual == typeof(decimal)
               || actual == typeof(double) || actual == typeof(float) || actual == typeof(short);
    }

    private static string SplitPascalCase(string value)
        => string.Concat(value.Select((c, i) => i > 0 && char.IsUpper(c) ? " " + c : c.ToString()));

    /// <summary>Excel rejects sheet names over 31 characters or containing <c>:\/?*[]</c>.</summary>
    private static string SanitiseSheetName(string value)
    {
        var cleaned = new string(value.Where(c => !@":\/?*[]".Contains(c)).ToArray()).Trim();
        return cleaned.Length is 0 ? "Report" : cleaned[..Math.Min(31, cleaned.Length)];
    }
}

/// <summary>
/// Convenience factory so callers can write <c>Exports.For&lt;T&gt;()</c>.
/// Named in the plural to stay distinct from the enclosing
/// <c>Inventory.Services.Export</c> namespace.
/// </summary>
public static class Exports
{
    public static ExportBuilder<T> For<T>() => new();

    /// <summary>Builds a value accessor from a property expression.</summary>
    public static Func<T, object?> Get<T, TValue>(Expression<Func<T, TValue>> selector)
    {
        var compiled = selector.Compile();
        return row => compiled(row);
    }
}
