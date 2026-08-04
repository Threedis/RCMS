namespace Inventory.Services.Import;

/// <summary>One parsed worksheet row, addressed by column heading.</summary>
public sealed class ImportRow
{
    private readonly IReadOnlyDictionary<string, string?> _cells;

    public ImportRow(int rowNumber, IReadOnlyDictionary<string, string?> cells)
    {
        RowNumber = rowNumber;
        _cells = cells;
    }

    /// <summary>1-based worksheet row number, so error messages point at the real row.</summary>
    public int RowNumber { get; }

    /// <summary>True when every cell in the row is blank.</summary>
    public bool IsEmpty => _cells.Values.All(string.IsNullOrWhiteSpace);

    /// <summary>Raw trimmed text of a column, or null when absent/blank.</summary>
    public string? Text(string column)
        => _cells.TryGetValue(column, out var value) && !string.IsNullOrWhiteSpace(value)
            ? value.Trim()
            : null;

    /// <summary>Parses a column as a decimal; returns null when blank or invalid.</summary>
    public decimal? Decimal(string column)
        => decimal.TryParse(Text(column), System.Globalization.NumberStyles.Any,
            System.Globalization.CultureInfo.InvariantCulture, out var value)
            ? value
            : null;

    /// <summary>Parses a column as an integer; returns null when blank or invalid.</summary>
    public int? Int(string column)
        => int.TryParse(Text(column), out var value) ? value : null;

    /// <summary>Parses a column as a date; returns null when blank or invalid.</summary>
    public DateTime? Date(string column)
        => DateTime.TryParse(Text(column), System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.None, out var value)
            ? value
            : null;

    /// <summary>
    /// Parses a column as a yes/no flag. <c>yes</c>, <c>y</c>, <c>true</c> and
    /// <c>1</c> all mean true; anything else (including blank) means false.
    /// </summary>
    public bool Bool(string column)
    {
        var text = Text(column);
        return text is not null
               && (text.Equals("yes", StringComparison.OrdinalIgnoreCase)
                   || text.Equals("y", StringComparison.OrdinalIgnoreCase)
                   || text.Equals("true", StringComparison.OrdinalIgnoreCase)
                   || text == "1");
    }
}

/// <summary>Result of reading a worksheet.</summary>
public sealed class ImportWorkbook
{
    public ImportWorkbook(IReadOnlyList<string> headers, IReadOnlyList<ImportRow> rows)
    {
        Headers = headers;
        Rows = rows;
    }

    public IReadOnlyList<string> Headers { get; }

    public IReadOnlyList<ImportRow> Rows { get; }

    /// <summary>Headings the caller expected but the file does not contain.</summary>
    public IReadOnlyList<string> MissingColumns(IEnumerable<string> required)
        => required.Where(r => !Headers.Contains(r, StringComparer.OrdinalIgnoreCase)).ToList();
}

/// <summary>
/// Reads an uploaded workbook into plain rows. The reader never interprets the
/// data — validation and mapping belong to the business layer, which keeps this
/// service reusable by every import wizard.
/// </summary>
public interface IExcelImportReader
{
    /// <summary>
    /// Reads the first worksheet (or <paramref name="sheetName"/> when given),
    /// treating the first non-empty row as the header.
    /// </summary>
    /// <exception cref="Inventory.Common.Exceptions.ValidationException">
    /// The stream is not a readable workbook, or it exceeds the row limit.
    /// </exception>
    ImportWorkbook Read(Stream stream, string? sheetName = null, int maxRows = 5000);
}
