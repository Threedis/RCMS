using Inventory.Common.Enums;

namespace Inventory.Services.Export;

/// <summary>A generated file: bytes, content type and download name.</summary>
public sealed class FileExportResult
{
    public FileExportResult(byte[] content, string contentType, string fileName)
    {
        Content = content;
        ContentType = contentType;
        FileName = fileName;
    }

    public byte[] Content { get; }

    public string ContentType { get; }

    public string FileName { get; }
}

/// <summary>
/// Renders an <see cref="ExportDefinition{T}"/> to Excel, PDF or CSV.
/// The controllers call one method and pass the requested format, so adding a
/// new export format never touches a controller.
/// </summary>
public interface IExportService
{
    /// <summary>Renders in the requested format.</summary>
    FileExportResult Export<T>(ExportDefinition<T> definition, ExportFormat format);

    /// <summary>Renders a styled Excel workbook.</summary>
    FileExportResult ToExcel<T>(ExportDefinition<T> definition);

    /// <summary>Renders a paginated PDF with a header, footer and page numbers.</summary>
    FileExportResult ToPdf<T>(ExportDefinition<T> definition);

    /// <summary>Renders RFC 4180 CSV with formula-injection protection.</summary>
    FileExportResult ToCsv<T>(ExportDefinition<T> definition);

    /// <summary>
    /// Builds a blank import template: a header row, an instruction sheet and
    /// data validation notes for the columns the import wizard expects.
    /// </summary>
    FileExportResult BuildImportTemplate(
        string sheetName,
        IReadOnlyList<ImportTemplateColumn> columns,
        IReadOnlyList<string>? instructions = null);
}

/// <summary>One column of a downloadable import template.</summary>
public sealed class ImportTemplateColumn
{
    public ImportTemplateColumn(string header, bool isRequired, string? example = null, string? notes = null)
    {
        Header = header;
        IsRequired = isRequired;
        Example = example;
        Notes = notes;
    }

    public string Header { get; }

    public bool IsRequired { get; }

    /// <summary>Sample value written into the first (grey) example row.</summary>
    public string? Example { get; }

    /// <summary>Explanation shown on the instructions sheet.</summary>
    public string? Notes { get; }
}
