using Inventory.Common.Constants;
using Inventory.Common.Enums;
using Inventory.Common.Models;
using Inventory.Entities.Dtos;
using Inventory.Services.Export;
using Microsoft.AspNetCore.Mvc;

namespace Inventory.Web.Controllers;

/// <summary>
/// Shared behaviour for every controller: flash messages, uniform AJAX
/// envelopes, DataTables request binding and file downloads.
/// <para>
/// Controllers never contain business logic. They bind and validate input,
/// call one business service, and translate the <see cref="ServiceResult"/> it
/// returns into a view, a redirect or a JSON response.
/// </para>
/// </summary>
public abstract class BaseController : Controller
{
    /// <summary>Shows a green toast on the next rendered page.</summary>
    protected void Success(string message) => TempData["Success"] = message;

    /// <summary>Shows a red toast on the next rendered page.</summary>
    protected void Error(string message) => TempData["Error"] = message;

    /// <summary>Shows an amber toast on the next rendered page.</summary>
    protected void Warning(string message) => TempData["Warning"] = message;

    /// <summary>Shows a blue toast on the next rendered page.</summary>
    protected void Info(string message) => TempData["Info"] = message;

    /// <summary>True when the current request came from the AJAX helper in site.js.</summary>
    protected bool IsAjaxRequest =>
        string.Equals(Request.Headers["X-Requested-With"], "XMLHttpRequest", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Copies a failed <see cref="ServiceResult"/> into <see cref="ModelStateDictionary"/>
    /// so the same messages render next to the fields and in the summary.
    /// </summary>
    protected void ApplyErrors(ServiceResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        if (result.Errors.Count == 0)
        {
            ModelState.AddModelError(string.Empty, result.Message);
            return;
        }

        foreach (var (key, messages) in result.Errors)
        {
            foreach (var message in messages)
            {
                ModelState.AddModelError(key, message);
            }
        }
    }

    /// <summary>Flattens model state into the dictionary shape the AJAX envelope uses.</summary>
    protected IDictionary<string, string[]> ModelStateErrors()
        => ModelState
            .Where(entry => entry.Value?.Errors.Count > 0)
            .ToDictionary(
                entry => entry.Key,
                entry => entry.Value!.Errors.Select(e => e.ErrorMessage).ToArray(),
                StringComparer.OrdinalIgnoreCase);

    /// <summary>Standard failure envelope built from the current model state.</summary>
    protected JsonResult ValidationFailed(string message = "Please correct the highlighted fields.")
        => Json(AjaxResponse.Fail(message, ModelStateErrors()));

    /// <summary>
    /// Standard success envelope. Named JsonOk rather than Ok so it cannot be
    /// confused with ControllerBase.Ok, which produces a different shape.
    /// </summary>
    protected JsonResult JsonOk(object? data = null, string message = "Saved successfully.", string? redirectUrl = null)
        => Json(new AjaxResponse
        {
            Success = true,
            Data = data,
            Message = message,
            RedirectUrl = redirectUrl
        });

    /// <summary>Standard failure envelope.</summary>
    protected JsonResult JsonFail(string message, IDictionary<string, string[]>? errors = null)
        => Json(AjaxResponse.Fail(message, errors));

    /// <summary>
    /// Builds a <see cref="PagedRequest"/> from a DataTables server-side payload.
    /// DataTables posts <c>start</c>/<c>length</c> and a sorted column index; this
    /// converts them into the page number and column name the services expect.
    /// </summary>
    protected PagedRequest BuildPagedRequest(IReadOnlyList<string>? sortableColumns = null)
    {
        var form = Request.HasFormContentType ? Request.Form : null;

        int Read(string key, int fallback)
        {
            var raw = form?[key].ToString() ?? Request.Query[key].ToString();
            return int.TryParse(raw, out var value) ? value : fallback;
        }

        string ReadText(string key)
            => form?[key].ToString() ?? Request.Query[key].ToString();

        var start = Read("start", 0);
        var length = Read("length", AppConstants.DefaultPageSize);
        var draw = Read("draw", 1);

        if (length <= 0)
        {
            length = AppConstants.DefaultPageSize;
        }

        var request = new PagedRequest
        {
            PageSize = length,
            PageNumber = (start / length) + 1,
            Draw = draw,
            SearchTerm = ReadText("search[value]").Trim() is { Length: > 0 } term ? term : null
        };

        // DataTables identifies the sorted column by index; resolve it against
        // the white-list the caller supplied so no client value reaches SQL.
        var orderColumn = ReadText("order[0][column]");
        var orderDirection = ReadText("order[0][dir]");

        if (sortableColumns is { Count: > 0 }
            && int.TryParse(orderColumn, out var columnIndex)
            && columnIndex >= 0
            && columnIndex < sortableColumns.Count)
        {
            request.SortColumn = sortableColumns[columnIndex];
            request.SortDirection = string.Equals(orderDirection, "desc", StringComparison.OrdinalIgnoreCase)
                ? "DESC"
                : "ASC";
        }

        return request;
    }

    /// <summary>Wraps a page of rows in the DataTables response envelope.</summary>
    protected JsonResult DataTable<T>(PagedResult<T> page, int draw)
        => Json(DataTableResponse<T>.From(page, draw));

    /// <summary>Streams a generated report or document to the browser.</summary>
    protected FileContentResult Download(FileExportResult file)
    {
        ArgumentNullException.ThrowIfNull(file);
        return File(file.Content, file.ContentType, file.FileName);
    }

    /// <summary>Parses the export format from a query string value.</summary>
    protected static ExportFormat ParseFormat(string? format) => format?.ToLowerInvariant() switch
    {
        "pdf" => ExportFormat.Pdf,
        "csv" => ExportFormat.Csv,
        _ => ExportFormat.Excel
    };
}
