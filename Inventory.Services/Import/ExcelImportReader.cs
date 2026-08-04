using ClosedXML.Excel;
using Inventory.Common.Exceptions;
using Microsoft.Extensions.Logging;

namespace Inventory.Services.Import;

/// <summary>ClosedXML implementation of <see cref="IExcelImportReader"/>.</summary>
public sealed class ExcelImportReader : IExcelImportReader
{
    private readonly ILogger<ExcelImportReader> _logger;

    public ExcelImportReader(ILogger<ExcelImportReader> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public ImportWorkbook Read(Stream stream, string? sheetName = null, int maxRows = 5000)
    {
        ArgumentNullException.ThrowIfNull(stream);

        XLWorkbook workbook;

        try
        {
            workbook = new XLWorkbook(stream);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Uploaded file could not be opened as a workbook.");
            throw new ValidationException(
                "The uploaded file could not be read. Save it as .xlsx and try again.");
        }

        using (workbook)
        {
            var sheet = ResolveWorksheet(workbook, sheetName);

            if (sheet is null)
            {
                throw new ValidationException(
                    sheetName is null
                        ? "The workbook does not contain any worksheet."
                        : $"The workbook does not contain a worksheet named '{sheetName}'.");
            }

            var used = sheet.RangeUsed();

            if (used is null)
            {
                return new ImportWorkbook(Array.Empty<string>(), Array.Empty<ImportRow>());
            }

            var firstRow = used.FirstRow();
            var headers = new List<string>();
            var headerColumns = new List<int>();

            foreach (var cell in firstRow.Cells())
            {
                var header = cell.GetFormattedString().Trim();

                if (string.IsNullOrWhiteSpace(header))
                {
                    continue;
                }

                headers.Add(header);
                headerColumns.Add(cell.Address.ColumnNumber);
            }

            if (headers.Count == 0)
            {
                throw new ValidationException("The first row of the worksheet does not contain any column headings.");
            }

            var duplicate = headers
                .GroupBy(h => h, StringComparer.OrdinalIgnoreCase)
                .FirstOrDefault(g => g.Count() > 1);

            if (duplicate is not null)
            {
                throw new ValidationException($"The column heading '{duplicate.Key}' appears more than once.");
            }

            var dataRows = used.RowsUsed().Skip(1).ToList();

            if (dataRows.Count > maxRows)
            {
                throw new ValidationException(
                    $"The file contains {dataRows.Count:N0} rows; the maximum accepted in one import is {maxRows:N0}.");
            }

            var rows = new List<ImportRow>(dataRows.Count);

            foreach (var row in dataRows)
            {
                var cells = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);

                for (var i = 0; i < headers.Count; i++)
                {
                    var value = row.Worksheet.Cell(row.RowNumber(), headerColumns[i]).GetFormattedString();
                    cells[headers[i]] = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
                }

                var importRow = new ImportRow(row.RowNumber(), cells);

                // Skip blank spacer rows rather than reporting them as errors.
                if (!importRow.IsEmpty)
                {
                    rows.Add(importRow);
                }
            }

            return new ImportWorkbook(headers, rows);
        }
    }

    private static IXLWorksheet? ResolveWorksheet(XLWorkbook workbook, string? sheetName)
    {
        if (!string.IsNullOrWhiteSpace(sheetName)
            && workbook.Worksheets.TryGetWorksheet(sheetName, out var named))
        {
            return named;
        }

        return workbook.Worksheets.FirstOrDefault();
    }
}
