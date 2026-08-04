using System.Globalization;
using System.Text;
using ClosedXML.Excel;
using Inventory.Common.Configuration;
using Inventory.Common.Constants;
using Inventory.Common.Enums;
using Inventory.Common.Extensions;
using Inventory.Common.Security;
using Microsoft.Extensions.Options;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Inventory.Services.Export;

/// <summary>
/// Single implementation of <see cref="IExportService"/> covering all three
/// output formats. ClosedXML produces the workbook, QuestPDF the document and a
/// plain writer the CSV; every path shares the same column definitions so the
/// three outputs never drift apart.
/// </summary>
public sealed class ExportService : IExportService
{
    private const string ExcelContentType =
        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    private static readonly CultureInfo Culture = CultureInfo.GetCultureInfo(AppConstants.DefaultCulture);

    private readonly ApplicationSettings _settings;

    public ExportService(IOptions<ApplicationSettings> settings)
    {
        _settings = settings?.Value ?? new ApplicationSettings();

        // QuestPDF requires the licence type to be declared before first use.
        // Community covers organisations under the published revenue threshold;
        // set this to LicenseType.Professional if your licence differs.
        QuestPDF.Settings.License = LicenseType.Community;
    }

    /// <inheritdoc />
    public FileExportResult Export<T>(ExportDefinition<T> definition, ExportFormat format) => format switch
    {
        ExportFormat.Excel => ToExcel(definition),
        ExportFormat.Pdf => ToPdf(definition),
        ExportFormat.Csv => ToCsv(definition),
        _ => throw new ArgumentOutOfRangeException(nameof(format), format, "Unsupported export format.")
    };

    // -----------------------------------------------------------------------
    // Excel
    // -----------------------------------------------------------------------

    /// <inheritdoc />
    public FileExportResult ToExcel<T>(ExportDefinition<T> definition)
    {
        ArgumentNullException.ThrowIfNull(definition);

        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add(definition.SheetName);

        var columnCount = Math.Max(1, definition.Columns.Count);
        var row = 1;

        // ---- Title block --------------------------------------------------
        var titleCell = sheet.Cell(row, 1);
        titleCell.Value = definition.Title;
        titleCell.Style.Font.Bold = true;
        titleCell.Style.Font.FontSize = 14;
        sheet.Range(row, 1, row, columnCount).Merge();
        row++;

        if (!string.IsNullOrWhiteSpace(definition.SubTitle))
        {
            sheet.Cell(row, 1).Value = definition.SubTitle;
            sheet.Cell(row, 1).Style.Font.Italic = true;
            sheet.Range(row, 1, row, columnCount).Merge();
            row++;
        }

        foreach (var filter in definition.FilterSummary)
        {
            sheet.Cell(row, 1).Value = filter;
            sheet.Cell(row, 1).Style.Font.FontSize = 9;
            sheet.Range(row, 1, row, columnCount).Merge();
            row++;
        }

        sheet.Cell(row, 1).Value =
            $"{_settings.CompanyName} · Generated {DateTime.Now.ToString(AppConstants.DateTimeFormat, Culture)}";
        sheet.Cell(row, 1).Style.Font.FontSize = 8;
        sheet.Cell(row, 1).Style.Font.FontColor = XLColor.Gray;
        sheet.Range(row, 1, row, columnCount).Merge();
        row += 2;

        // ---- Header row ---------------------------------------------------
        var headerRow = row;

        for (var c = 0; c < definition.Columns.Count; c++)
        {
            var cell = sheet.Cell(headerRow, c + 1);
            cell.Value = definition.Columns[c].Header;
            cell.Style.Font.Bold = true;
            cell.Style.Font.FontColor = XLColor.White;
            cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#0d6efd");
            cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            cell.Style.Alignment.WrapText = true;
            cell.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        }

        row++;

        // ---- Data rows ----------------------------------------------------
        var firstDataRow = row;

        foreach (var item in definition.Rows)
        {
            for (var c = 0; c < definition.Columns.Count; c++)
            {
                var column = definition.Columns[c];
                var cell = sheet.Cell(row, c + 1);

                SetCellValue(cell, column.Value(item));

                if (!string.IsNullOrWhiteSpace(column.Format))
                {
                    cell.Style.NumberFormat.Format = column.Format;
                }

                if (column.AlignRight)
                {
                    cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
                }

                cell.Style.Border.OutsideBorder = XLBorderStyleValues.Hair;
                cell.Style.Border.OutsideBorderColor = XLColor.LightGray;
            }

            row++;
        }

        var lastDataRow = row - 1;

        // ---- Totals -------------------------------------------------------
        if (definition.ShowTotals && definition.Rows.Count > 0 && definition.Columns.Any(c => c.Summable))
        {
            sheet.Cell(row, 1).Value = "Total";
            sheet.Cell(row, 1).Style.Font.Bold = true;

            for (var c = 0; c < definition.Columns.Count; c++)
            {
                if (!definition.Columns[c].Summable)
                {
                    continue;
                }

                var cell = sheet.Cell(row, c + 1);
                cell.FormulaA1 = $"SUM({sheet.Cell(firstDataRow, c + 1).Address.ToStringRelative()}:" +
                                 $"{sheet.Cell(lastDataRow, c + 1).Address.ToStringRelative()})";
                cell.Style.Font.Bold = true;
                cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;

                if (!string.IsNullOrWhiteSpace(definition.Columns[c].Format))
                {
                    cell.Style.NumberFormat.Format = definition.Columns[c].Format;
                }
            }

            sheet.Range(row, 1, row, columnCount).Style.Fill.BackgroundColor = XLColor.FromHtml("#e9ecef");
            sheet.Range(row, 1, row, columnCount).Style.Border.TopBorder = XLBorderStyleValues.Double;
        }

        // ---- Finishing touches --------------------------------------------
        if (definition.Rows.Count > 0)
        {
            sheet.Range(headerRow, 1, lastDataRow, columnCount).SetAutoFilter();
            sheet.SheetView.FreezeRows(headerRow);
        }

        sheet.Columns().AdjustToContents(5d, 45d);
        sheet.PageSetup.PageOrientation = definition.Landscape
            ? XLPageOrientation.Landscape
            : XLPageOrientation.Portrait;
        sheet.PageSetup.FitToPages(1, 0);

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);

        return new FileExportResult(
            stream.ToArray(),
            ExcelContentType,
            BuildFileName(definition.Title, "xlsx"));
    }

    // -----------------------------------------------------------------------
    // PDF
    // -----------------------------------------------------------------------

    /// <inheritdoc />
    public FileExportResult ToPdf<T>(ExportDefinition<T> definition)
    {
        ArgumentNullException.ThrowIfNull(definition);

        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(definition.Landscape ? PageSizes.A4.Landscape() : PageSizes.A4);
                page.Margin(18, QuestPDF.Infrastructure.Unit.Point);
                page.DefaultTextStyle(x => x.FontSize(8).FontFamily(Fonts.Calibri));

                page.Header().Element(header => ComposeHeader(header, definition));
                page.Content().PaddingVertical(6).Element(content => ComposeTable(content, definition));

                page.Footer().AlignCenter().Text(text =>
                {
                    text.DefaultTextStyle(x => x.FontSize(7).FontColor(Colors.Grey.Darken1));
                    text.Span("Page ");
                    text.CurrentPageNumber();
                    text.Span(" of ");
                    text.TotalPages();
                });
            });
        });

        return new FileExportResult(
            document.GeneratePdf(),
            "application/pdf",
            BuildFileName(definition.Title, "pdf"));
    }

    private void ComposeHeader<T>(IContainer container, ExportDefinition<T> definition)
    {
        container.Column(column =>
        {
            column.Item().Row(row =>
            {
                row.RelativeItem().Column(left =>
                {
                    left.Item().Text(_settings.CompanyName).FontSize(12).Bold();
                    left.Item().Text(definition.Title).FontSize(10).SemiBold();

                    if (!string.IsNullOrWhiteSpace(definition.SubTitle))
                    {
                        left.Item().Text(definition.SubTitle!).FontSize(8).FontColor(Colors.Grey.Darken2);
                    }
                });

                row.ConstantItem(160).AlignRight().Column(right =>
                {
                    right.Item().AlignRight()
                        .Text(DateTime.Now.ToString(AppConstants.DateTimeFormat, Culture))
                        .FontSize(8).FontColor(Colors.Grey.Darken1);
                    right.Item().AlignRight()
                        .Text($"{definition.Rows.Count:N0} record(s)")
                        .FontSize(8).FontColor(Colors.Grey.Darken1);
                });
            });

            foreach (var filter in definition.FilterSummary)
            {
                column.Item().Text(filter).FontSize(7).FontColor(Colors.Grey.Darken1);
            }

            column.Item().PaddingTop(4).LineHorizontal(0.8f).LineColor(Colors.Blue.Medium);
        });
    }

    private static void ComposeTable<T>(IContainer container, ExportDefinition<T> definition)
    {
        if (definition.Columns.Count == 0)
        {
            container.Text("No columns defined.");
            return;
        }

        container.Table(table =>
        {
            table.ColumnsDefinition(columns =>
            {
                foreach (var column in definition.Columns)
                {
                    columns.RelativeColumn(column.Width);
                }
            });

            table.Header(header =>
            {
                foreach (var column in definition.Columns)
                {
                    header.Cell()
                        .Background(Colors.Blue.Medium)
                        .Padding(3)
                        .AlignMiddle()
                        .Text(column.Header)
                        .FontColor(Colors.White)
                        .Bold()
                        .FontSize(7.5f);
                }
            });

            var index = 0;

            foreach (var item in definition.Rows)
            {
                var background = index % 2 == 0 ? Colors.White : Colors.Grey.Lighten4;

                foreach (var column in definition.Columns)
                {
                    var cell = table.Cell().Background(background).Padding(2.5f);
                    var text = FormatForText(column.Value(item), column.Format);

                    if (column.AlignRight)
                    {
                        cell.AlignRight().Text(text).FontSize(7.5f);
                    }
                    else
                    {
                        cell.Text(text).FontSize(7.5f);
                    }
                }

                index++;
            }
        });
    }

    // -----------------------------------------------------------------------
    // CSV
    // -----------------------------------------------------------------------

    /// <inheritdoc />
    public FileExportResult ToCsv<T>(ExportDefinition<T> definition)
    {
        ArgumentNullException.ThrowIfNull(definition);

        var builder = new StringBuilder();

        builder.AppendLine(string.Join(",", definition.Columns.Select(c => c.Header.ToCsvField())));

        foreach (var item in definition.Rows)
        {
            var cells = definition.Columns
                .Select(c => FormatForText(c.Value(item), c.Format).ToCsvField());

            builder.AppendLine(string.Join(",", cells));
        }

        // The UTF-8 BOM makes Excel open the file with the right encoding.
        var bytes = Encoding.UTF8.GetPreamble()
            .Concat(Encoding.UTF8.GetBytes(builder.ToString()))
            .ToArray();

        return new FileExportResult(bytes, "text/csv", BuildFileName(definition.Title, "csv"));
    }

    // -----------------------------------------------------------------------
    // Import template
    // -----------------------------------------------------------------------

    /// <inheritdoc />
    public FileExportResult BuildImportTemplate(
        string sheetName,
        IReadOnlyList<ImportTemplateColumn> columns,
        IReadOnlyList<string>? instructions = null)
    {
        ArgumentNullException.ThrowIfNull(columns);

        using var workbook = new XLWorkbook();

        // ---- Data sheet ---------------------------------------------------
        var sheet = workbook.Worksheets.Add(sheetName);

        for (var c = 0; c < columns.Count; c++)
        {
            var cell = sheet.Cell(1, c + 1);
            cell.Value = columns[c].Header;
            cell.Style.Font.Bold = true;
            cell.Style.Font.FontColor = XLColor.White;
            cell.Style.Fill.BackgroundColor = columns[c].IsRequired
                ? XLColor.FromHtml("#dc3545")   // required columns in red
                : XLColor.FromHtml("#6c757d");  // optional columns in grey
            cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

            if (!string.IsNullOrWhiteSpace(columns[c].Example))
            {
                var example = sheet.Cell(2, c + 1);
                example.Value = columns[c].Example!;
                example.Style.Font.Italic = true;
                example.Style.Font.FontColor = XLColor.Gray;
                example.Style.Fill.BackgroundColor = XLColor.FromHtml("#f8f9fa");
            }
        }

        sheet.SheetView.FreezeRows(1);
        sheet.Columns().AdjustToContents(10d, 30d);

        // ---- Instructions sheet -------------------------------------------
        var help = workbook.Worksheets.Add("Instructions");
        var row = 1;

        help.Cell(row, 1).Value = $"{sheetName} — import instructions";
        help.Cell(row, 1).Style.Font.Bold = true;
        help.Cell(row, 1).Style.Font.FontSize = 13;
        row += 2;

        help.Cell(row, 1).Value = "Row 2 of the data sheet holds example values. Delete it before importing.";
        row++;
        help.Cell(row, 1).Value = "Red headings are mandatory; grey headings are optional.";
        row++;
        help.Cell(row, 1).Value = $"A single file may contain at most {AppConstants.MaxImportRows:N0} data rows.";
        row += 2;

        foreach (var line in instructions.OrEmpty())
        {
            help.Cell(row, 1).Value = line;
            row++;
        }

        row++;
        help.Cell(row, 1).Value = "Column";
        help.Cell(row, 2).Value = "Required";
        help.Cell(row, 3).Value = "Notes";
        help.Range(row, 1, row, 3).Style.Font.Bold = true;
        help.Range(row, 1, row, 3).Style.Fill.BackgroundColor = XLColor.FromHtml("#e9ecef");
        row++;

        foreach (var column in columns)
        {
            help.Cell(row, 1).Value = column.Header;
            help.Cell(row, 2).Value = column.IsRequired ? "Yes" : "No";
            help.Cell(row, 3).Value = column.Notes ?? string.Empty;
            row++;
        }

        help.Columns().AdjustToContents(12d, 70d);

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);

        return new FileExportResult(
            stream.ToArray(),
            ExcelContentType,
            BuildFileName($"{sheetName} Import Template", "xlsx"));
    }

    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------

    /// <summary>
    /// Writes a value with its natural Excel type. Strings are passed through
    /// the spreadsheet sanitiser so an exported value can never be evaluated as
    /// a formula when the file is reopened.
    /// </summary>
    private static void SetCellValue(IXLCell cell, object? value)
    {
        // XLCellValue only converts implicitly from blank, bool, double, string
        // and DateTime, so every other numeric type is widened to double here.
        switch (value)
        {
            case null:
                cell.Value = Blank.Value;
                break;
            case string text:
                cell.Value = InputSanitizer.SanitizeForSpreadsheet(text);
                break;
            case DateTime date:
                cell.Value = date;
                break;
            case bool flag:
                cell.Value = flag ? "Yes" : "No";
                break;
            case decimal number:
                cell.Value = (double)number;
                break;
            case double number:
                cell.Value = number;
                break;
            case float number:
                cell.Value = number;
                break;
            case int number:
                cell.Value = number;
                break;
            case long number:
                cell.Value = number;
                break;
            case short number:
                cell.Value = number;
                break;
            case Enum enumValue:
                cell.Value = enumValue.ToDisplayName();
                break;
            default:
                cell.Value = InputSanitizer.SanitizeForSpreadsheet(
                    Convert.ToString(value, Culture) ?? string.Empty);
                break;
        }
    }

    /// <summary>Renders a value as display text for the PDF and CSV writers.</summary>
    private static string FormatForText(object? value, string? format)
    {
        return value switch
        {
            null => string.Empty,
            DateTime date => date.ToString(
                string.IsNullOrWhiteSpace(format) ? AppConstants.DateFormat : format, Culture),
            decimal number => number.ToString(
                string.IsNullOrWhiteSpace(format) ? "N2" : format, Culture),
            double number => number.ToString(
                string.IsNullOrWhiteSpace(format) ? "N2" : format, Culture),
            bool flag => flag ? "Yes" : "No",
            Enum enumValue => enumValue.ToDisplayName(),
            _ => Convert.ToString(value, Culture) ?? string.Empty
        };
    }

    /// <summary>Builds a safe, timestamped download name.</summary>
    private static string BuildFileName(string title, string extension)
        => $"{title.ToSafeFileName().Replace(' ', '_')}_{DateTime.Now:yyyyMMdd_HHmmss}.{extension}";
}
