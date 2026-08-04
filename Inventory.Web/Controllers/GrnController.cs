using Inventory.BLL.Interfaces;
using Inventory.Common.Constants;
using Inventory.Common.Enums;
using Inventory.Entities.Dtos;
using Inventory.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Inventory.Web.Controllers;

/// <summary>
/// Goods Receipt Note (inventory inward).
/// <para>
/// The controller only moves data between the form and
/// <see cref="IGrnService"/>: which statuses may be edited, whether stock may
/// be posted and who may approve are all decided in the business layer, so the
/// same rules apply however the document is reached.
/// </para>
/// </summary>
[Authorize(Policy = Policies.ReadOnly)]
public sealed class GrnController : BaseController
{
    private static readonly string[] SortableColumns =
    {
        "GrnDate", "GrnNumber", "PurchaseOrderNumber", "VendorName",
        "WarehouseName", "LineCount", "TotalQuantity", "GrandTotal", "StatusName"
    };

    private readonly IGrnService _grn;
    private readonly ILookupService _lookups;
    private readonly IAttachmentService _attachments;

    public GrnController(IGrnService grn, ILookupService lookups, IAttachmentService attachments)
    {
        _grn = grn;
        _lookups = lookups;
        _attachments = attachments;
    }

    // -----------------------------------------------------------------------
    // List
    // -----------------------------------------------------------------------

    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var model = new FilterViewModel
        {
            Filter = new ReportFilter
            {
                FromDate = DateTime.Today.AddMonths(-1),
                ToDate = DateTime.Today
            },
            Lookups = await LookupBundle.LoadAsync(_lookups, cancellationToken).ConfigureAwait(false),
            VisibleFilters = new[] { "dateRange", "vendor", "warehouse", "status" }
        };

        return View(model);
    }

    [HttpPost]
    public async Task<IActionResult> List(ReportFilter filter, CancellationToken cancellationToken)
    {
        var request = BuildPagedRequest(SortableColumns);
        var page = await _grn.GetPagedAsync(request, filter ?? new ReportFilter(), cancellationToken)
            .ConfigureAwait(false);

        return DataTable(page, request.Draw);
    }

    // -----------------------------------------------------------------------
    // Create / edit / view
    // -----------------------------------------------------------------------

    [HttpGet]
    [Authorize(Policy = Policies.ManageInventory)]
    public async Task<IActionResult> Create(CancellationToken cancellationToken)
    {
        var model = new GrnDto
        {
            GrnDate = DateTime.Today,
            Status = DocumentStatus.Draft
        };

        return View("Edit", await BuildFormAsync(model, cancellationToken).ConfigureAwait(false));
    }

    [HttpGet]
    [Authorize(Policy = Policies.ManageInventory)]
    public async Task<IActionResult> Edit(int id, CancellationToken cancellationToken)
    {
        var grn = await _grn.GetByIdAsync(id, cancellationToken).ConfigureAwait(false);

        if (grn is null)
        {
            Error("The goods receipt note was not found.");
            return RedirectToAction(nameof(Index));
        }

        if (!grn.CanEdit)
        {
            Warning($"This document is {grn.StatusName} and can only be viewed.");
            return RedirectToAction(nameof(Details), new { id });
        }

        return View(await BuildFormAsync(grn, cancellationToken).ConfigureAwait(false));
    }

    [HttpGet]
    public async Task<IActionResult> Details(int id, CancellationToken cancellationToken)
    {
        var grn = await _grn.GetByIdAsync(id, cancellationToken).ConfigureAwait(false);

        if (grn is null)
        {
            Error("The goods receipt note was not found.");
            return RedirectToAction(nameof(Index));
        }

        return View(grn);
    }

    [HttpPost]
    [Authorize(Policy = Policies.ManageInventory)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Save(GrnDto model, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(model);

        // The line grid is built client side, so an empty post means the user
        // never added a row.
        if (model.Details.Count == 0)
        {
            ModelState.AddModelError(nameof(model.Details), "Add at least one item line.");
        }

        if (!ModelState.IsValid)
        {
            return IsAjaxRequest
                ? ValidationFailed()
                : View("Edit", await BuildFormAsync(model, cancellationToken).ConfigureAwait(false));
        }

        var result = await _grn.SaveAsync(model, cancellationToken).ConfigureAwait(false);

        if (!result.Succeeded)
        {
            if (IsAjaxRequest)
            {
                return JsonFail(result.Message, result.Errors);
            }

            ApplyErrors(result);
            return View("Edit", await BuildFormAsync(model, cancellationToken).ConfigureAwait(false));
        }

        var redirect = Url.Action(nameof(Details), new { id = result.Data });

        if (IsAjaxRequest)
        {
            return JsonOk(new { id = result.Data }, result.Message, redirect);
        }

        Success(result.Message);
        return RedirectToAction(nameof(Details), new { id = result.Data });
    }

    // -----------------------------------------------------------------------
    // Workflow
    // -----------------------------------------------------------------------

    [HttpPost]
    [Authorize(Policy = Policies.ManageInventory)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Submit(int id, string? remarks, CancellationToken cancellationToken)
    {
        var result = await _grn.SubmitAsync(id, remarks, cancellationToken).ConfigureAwait(false);

        return result.Succeeded
            ? JsonOk(null, result.Message, Url.Action(nameof(Details), new { id }))
            : JsonFail(result.Message, result.Errors);
    }

    [HttpPost]
    [Authorize(Policy = Policies.ApproveDocuments)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Approve(
        int id,
        bool approve,
        string? remarks,
        CancellationToken cancellationToken)
    {
        var result = await _grn.ApproveAsync(id, approve, remarks, cancellationToken).ConfigureAwait(false);

        return result.Succeeded
            ? JsonOk(null, result.Message, Url.Action(nameof(Details), new { id }))
            : JsonFail(result.Message, result.Errors);
    }

    [HttpPost]
    [Authorize(Policy = Policies.ManageInventory)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        var result = await _grn.DeleteAsync(id, cancellationToken).ConfigureAwait(false);

        return result.Succeeded
            ? JsonOk(null, result.Message, Url.Action(nameof(Index)))
            : JsonFail(result.Message);
    }

    // -----------------------------------------------------------------------
    // Documents
    // -----------------------------------------------------------------------

    [HttpGet]
    public async Task<IActionResult> Print(int id, CancellationToken cancellationToken)
    {
        var file = await _grn.PrintAsync(id, cancellationToken).ConfigureAwait(false);

        if (file is null)
        {
            Error("The goods receipt note was not found.");
            return RedirectToAction(nameof(Index));
        }

        return Download(file);
    }

    [HttpGet]
    [Authorize(Policy = Policies.ViewReports)]
    public async Task<IActionResult> Export(string? format, ReportFilter filter, CancellationToken cancellationToken)
    {
        var file = await _grn
            .ExportAsync(ParseFormat(format), filter ?? new ReportFilter(), cancellationToken)
            .ConfigureAwait(false);

        return Download(file);
    }

    /// <summary>Attachments for the document; the upload itself goes to FileController.</summary>
    [HttpGet]
    public async Task<IActionResult> Attachments(int id, CancellationToken cancellationToken)
    {
        var files = await _attachments
            .GetForDocumentAsync(DocumentType.GoodsReceiptNote, id, cancellationToken)
            .ConfigureAwait(false);

        return JsonOk(files, "Loaded.");
    }

    // -----------------------------------------------------------------------

    private async Task<DocumentFormViewModel<GrnDto>> BuildFormAsync(
        GrnDto model,
        CancellationToken cancellationToken)
        => new()
        {
            Document = model,
            Lookups = await LookupBundle.LoadAsync(_lookups, cancellationToken).ConfigureAwait(false),
            IsEditable = model.CanEdit
        };
}
