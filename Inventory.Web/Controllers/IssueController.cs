using Inventory.BLL.Interfaces;
using Inventory.Common.Constants;
using Inventory.Common.Enums;
using Inventory.Entities.Dtos;
using Inventory.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Inventory.Web.Controllers;

/// <summary>
/// Material Issue (inventory outward), including the dispatch step.
/// </summary>
[Authorize(Policy = Policies.ReadOnly)]
public sealed class IssueController : BaseController
{
    private static readonly string[] SortableColumns =
    {
        "IssueDate", "IssueNumber", "DepartmentName", "SiteName", "EngineerName",
        "ProjectName", "WarehouseName", "LineCount", "TotalQuantity", "TotalValue", "StatusName"
    };

    private readonly IIssueService _issues;
    private readonly IStockService _stock;
    private readonly ILookupService _lookups;
    private readonly IAttachmentService _attachments;

    public IssueController(
        IIssueService issues,
        IStockService stock,
        ILookupService lookups,
        IAttachmentService attachments)
    {
        _issues = issues;
        _stock = stock;
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
            VisibleFilters = new[] { "dateRange", "department", "site", "engineer", "warehouse", "status" }
        };

        return View(model);
    }

    [HttpPost]
    public async Task<IActionResult> List(ReportFilter filter, CancellationToken cancellationToken)
    {
        var request = BuildPagedRequest(SortableColumns);
        var page = await _issues.GetPagedAsync(request, filter ?? new ReportFilter(), cancellationToken)
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
        var model = new IssueDto
        {
            IssueDate = DateTime.Today,
            Status = DocumentStatus.Draft
        };

        return View("Edit", await BuildFormAsync(model, cancellationToken).ConfigureAwait(false));
    }

    [HttpGet]
    [Authorize(Policy = Policies.ManageInventory)]
    public async Task<IActionResult> Edit(int id, CancellationToken cancellationToken)
    {
        var issue = await _issues.GetByIdAsync(id, cancellationToken).ConfigureAwait(false);

        if (issue is null)
        {
            Error("The material issue was not found.");
            return RedirectToAction(nameof(Index));
        }

        if (!issue.CanEdit)
        {
            Warning($"This document is {issue.StatusName} and can only be viewed.");
            return RedirectToAction(nameof(Details), new { id });
        }

        return View(await BuildFormAsync(issue, cancellationToken).ConfigureAwait(false));
    }

    [HttpGet]
    public async Task<IActionResult> Details(int id, CancellationToken cancellationToken)
    {
        var issue = await _issues.GetByIdAsync(id, cancellationToken).ConfigureAwait(false);

        if (issue is null)
        {
            Error("The material issue was not found.");
            return RedirectToAction(nameof(Index));
        }

        ViewData["Couriers"] = (await _lookups.CouriersAsync(cancellationToken).ConfigureAwait(false));
        return View(issue);
    }

    [HttpPost]
    [Authorize(Policy = Policies.ManageInventory)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Save(IssueDto model, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(model);

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

        var result = await _issues.SaveAsync(model, cancellationToken).ConfigureAwait(false);

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
        var result = await _issues.SubmitAsync(id, remarks, cancellationToken).ConfigureAwait(false);

        return result.Succeeded
            ? JsonOk(null, result.Message, Url.Action(nameof(Details), new { id }))
            : JsonFail(result.Message, result.Errors);
    }

    [HttpPost]
    [Authorize(Policy = Policies.ApproveDocuments)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Approve(ApprovalRequestDto request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        request.DocumentType = DocumentType.MaterialIssue;

        var result = await _issues.ApproveAsync(request, cancellationToken).ConfigureAwait(false);

        return result.Succeeded
            ? JsonOk(null, result.Message, Url.Action(nameof(Details), new { id = request.DocumentId }))
            : JsonFail(result.Message, result.Errors);
    }

    [HttpPost]
    [Authorize(Policy = Policies.ManageInventory)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Dispatch(DispatchRequestDto request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var result = await _issues.DispatchAsync(request, cancellationToken).ConfigureAwait(false);

        return result.Succeeded
            ? JsonOk(null, result.Message, Url.Action(nameof(Details), new { id = request.IssueId }))
            : JsonFail(result.Message, result.Errors);
    }

    [HttpPost]
    [Authorize(Policy = Policies.ManageInventory)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        var result = await _issues.DeleteAsync(id, cancellationToken).ConfigureAwait(false);

        return result.Succeeded
            ? JsonOk(null, result.Message, Url.Action(nameof(Index)))
            : JsonFail(result.Message);
    }

    // -----------------------------------------------------------------------
    // Helpers used by the line grid
    // -----------------------------------------------------------------------

    /// <summary>Live balance for one item, shown as the user types a quantity.</summary>
    [HttpGet]
    public async Task<IActionResult> Balance(int itemId, int warehouseId, CancellationToken cancellationToken)
    {
        if (itemId <= 0 || warehouseId <= 0)
        {
            return JsonFail("Select an item and a warehouse.");
        }

        var balance = await _stock.GetBalanceAsync(itemId, warehouseId, cancellationToken).ConfigureAwait(false);

        return balance is null
            ? JsonFail("No stock record was found for this item in the selected warehouse.")
            : JsonOk(balance, "Loaded.");
    }

    // -----------------------------------------------------------------------
    // Documents
    // -----------------------------------------------------------------------

    [HttpGet]
    public async Task<IActionResult> Print(int id, CancellationToken cancellationToken)
    {
        var file = await _issues.PrintAsync(id, cancellationToken).ConfigureAwait(false);

        if (file is null)
        {
            Error("The material issue was not found.");
            return RedirectToAction(nameof(Index));
        }

        return Download(file);
    }

    [HttpGet]
    [Authorize(Policy = Policies.ViewReports)]
    public async Task<IActionResult> Export(string? format, ReportFilter filter, CancellationToken cancellationToken)
    {
        var file = await _issues
            .ExportAsync(ParseFormat(format), filter ?? new ReportFilter(), cancellationToken)
            .ConfigureAwait(false);

        return Download(file);
    }

    [HttpGet]
    public async Task<IActionResult> Attachments(int id, CancellationToken cancellationToken)
    {
        var files = await _attachments
            .GetForDocumentAsync(DocumentType.MaterialIssue, id, cancellationToken)
            .ConfigureAwait(false);

        return JsonOk(files, "Loaded.");
    }

    // -----------------------------------------------------------------------

    private async Task<DocumentFormViewModel<IssueDto>> BuildFormAsync(
        IssueDto model,
        CancellationToken cancellationToken)
        => new()
        {
            Document = model,
            Lookups = await LookupBundle.LoadAsync(_lookups, cancellationToken).ConfigureAwait(false),
            IsEditable = model.CanEdit
        };
}
