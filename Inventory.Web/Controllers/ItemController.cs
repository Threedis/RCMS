using Inventory.BLL.Interfaces;
using Inventory.Common.Constants;
using Inventory.Entities.Dtos;
using Inventory.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Inventory.Web.Controllers;

/// <summary>
/// Item master: grid, create/edit form, barcode lookup, export and the Excel
/// import wizard.
/// </summary>
[Authorize(Policy = Policies.ReadOnly)]
public sealed class ItemController : BaseController
{
    private static readonly string[] SortableColumns =
    {
        "ItemCode", "ItemName", "PartNumber", "CategoryName", "ItemTypeName",
        "UnitSymbol", "CurrentStock", "ReorderLevel", "AverageCost", "StockValue"
    };

    private readonly IItemService _items;
    private readonly ILookupService _lookups;

    public ItemController(IItemService items, ILookupService lookups)
    {
        _items = items;
        _lookups = lookups;
    }

    // -----------------------------------------------------------------------
    // Grid
    // -----------------------------------------------------------------------

    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        ViewData["Lookups"] = await LookupBundle.LoadAsync(_lookups, cancellationToken).ConfigureAwait(false);
        return View();
    }

    [HttpPost]
    public async Task<IActionResult> List(
        int? categoryId,
        int? itemType,
        bool? lowStockOnly,
        CancellationToken cancellationToken)
    {
        var request = BuildPagedRequest(SortableColumns);

        var page = await _items
            .GetPagedAsync(request, categoryId, itemType, lowStockOnly, cancellationToken)
            .ConfigureAwait(false);

        return DataTable(page, request.Draw);
    }

    // -----------------------------------------------------------------------
    // Create / edit
    // -----------------------------------------------------------------------

    [HttpGet]
    [Authorize(Policy = Policies.ManageMasters)]
    public async Task<IActionResult> Create(CancellationToken cancellationToken)
    {
        var model = new ItemDto { IsActive = true };
        return View("Edit", await BuildFormAsync(model, cancellationToken).ConfigureAwait(false));
    }

    [HttpGet]
    [Authorize(Policy = Policies.ManageMasters)]
    public async Task<IActionResult> Edit(int id, CancellationToken cancellationToken)
    {
        var item = await _items.GetByIdAsync(id, cancellationToken).ConfigureAwait(false);

        if (item is null)
        {
            Error("The item was not found.");
            return RedirectToAction(nameof(Index));
        }

        return View(await BuildFormAsync(item, cancellationToken).ConfigureAwait(false));
    }

    [HttpPost]
    [Authorize(Policy = Policies.ManageMasters)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Save(ItemDto model, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(model);

        if (!ModelState.IsValid)
        {
            if (IsAjaxRequest)
            {
                return ValidationFailed();
            }

            return View("Edit", await BuildFormAsync(model, cancellationToken).ConfigureAwait(false));
        }

        var result = model.Id > 0
            ? await _items.UpdateAsync(model, cancellationToken).ConfigureAwait(false)
            : await _items.CreateAsync(model, cancellationToken).ConfigureAwait(false);

        if (!result.Succeeded)
        {
            if (IsAjaxRequest)
            {
                return JsonFail(result.Message, result.Errors);
            }

            ApplyErrors(result);
            return View("Edit", await BuildFormAsync(model, cancellationToken).ConfigureAwait(false));
        }

        if (IsAjaxRequest)
        {
            return JsonOk(null, result.Message, Url.Action(nameof(Index)));
        }

        Success(result.Message);
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [Authorize(Policy = Policies.ManageMasters)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        var result = await _items.DeleteAsync(id, cancellationToken).ConfigureAwait(false);

        return result.Succeeded
            ? JsonOk(null, result.Message)
            : JsonFail(result.Message);
    }

    // -----------------------------------------------------------------------
    // Pickers used by the document screens
    // -----------------------------------------------------------------------

    /// <summary>Type-ahead search for the Select2 item picker on GRN and issue lines.</summary>
    [HttpGet]
    public async Task<IActionResult> Search(string? term, int warehouseId, CancellationToken cancellationToken)
    {
        if (warehouseId <= 0)
        {
            return JsonFail("Select a warehouse before choosing items.");
        }

        var results = await _items
            .SearchForDocumentAsync(term, warehouseId, cancellationToken)
            .ConfigureAwait(false);

        // Shape expected by Select2's ajax data source.
        return Json(new
        {
            results = results.Select(item => new
            {
                id = item.ItemId,
                text = $"{item.ItemCode} — {item.ItemName}",
                item.ItemCode,
                item.ItemName,
                item.PartNumber,
                item.UnitSymbol,
                item.AverageCost,
                item.TaxRate,
                item.CurrentStock,
                item.AvailableStock,
                item.IsBatchTracked,
                item.IsSerialTracked,
                item.ShelfLocation
            })
        });
    }

    /// <summary>Resolves a scanned barcode to an item and its balance.</summary>
    [HttpGet]
    public async Task<IActionResult> Barcode(string code, int warehouseId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return JsonFail("Scan or enter a barcode.");
        }

        var item = await _items.ResolveBarcodeAsync(code, warehouseId, cancellationToken).ConfigureAwait(false);

        return item is null
            ? JsonFail($"No item matches barcode '{code}'.")
            : JsonOk(item, $"{item.ItemName} found.");
    }

    /// <summary>Remote validation for the unique item code rule.</summary>
    [HttpGet]
    public async Task<IActionResult> IsCodeAvailable(string code, int id = 0, CancellationToken cancellationToken = default)
    {
        var available = await _items.IsItemCodeAvailableAsync(code, id, cancellationToken).ConfigureAwait(false);
        return Json(available ? (object)true : $"The item code '{code}' is already in use.");
    }

    // -----------------------------------------------------------------------
    // Export and import
    // -----------------------------------------------------------------------

    [HttpGet]
    [Authorize(Policy = Policies.ViewReports)]
    public async Task<IActionResult> Export(string? format, int? categoryId, CancellationToken cancellationToken)
    {
        var file = await _items
            .ExportAsync(ParseFormat(format), categoryId, cancellationToken)
            .ConfigureAwait(false);

        return Download(file);
    }

    [HttpGet]
    [Authorize(Policy = Policies.ManageMasters)]
    public IActionResult Template() => Download(_items.BuildImportTemplate());

    [HttpGet]
    [Authorize(Policy = Policies.ManageMasters)]
    public IActionResult Import() => View(new ImportViewModel());

    [HttpPost]
    [Authorize(Policy = Policies.ManageMasters)]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(AppConstants.MaxUploadSizeBytes)]
    public async Task<IActionResult> Import(IFormFile? file, bool validateOnly, CancellationToken cancellationToken)
    {
        var model = new ImportViewModel { ValidateOnly = validateOnly };

        if (file is null || file.Length == 0)
        {
            Error("Choose a workbook to import.");
            return View(model);
        }

        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();

        if (extension is not ".xlsx")
        {
            Error("Only .xlsx workbooks are accepted. Save the file as Excel Workbook and try again.");
            return View(model);
        }

        if (file.Length > AppConstants.MaxUploadSizeBytes)
        {
            Error($"The file exceeds the {AppConstants.MaxUploadSizeBytes / 1024 / 1024} MB limit.");
            return View(model);
        }

        await using var stream = file.OpenReadStream();

        model.Result = await _items
            .ImportAsync(stream, file.FileName, commit: !validateOnly, cancellationToken)
            .ConfigureAwait(false);

        if (validateOnly)
        {
            Info($"Validated {model.Result.TotalRows} row(s): " +
                 $"{model.Result.ValidRows} ready to import, {model.Result.FailedRows} with errors.");
        }
        else if (model.Result.FailedRows == 0)
        {
            Success($"Imported {model.Result.ImportedRows} item(s) successfully.");
        }
        else
        {
            Warning($"Imported {model.Result.ImportedRows} item(s); " +
                    $"{model.Result.FailedRows} row(s) were skipped. See the error list below.");
        }

        return View(model);
    }

    // -----------------------------------------------------------------------

    private async Task<DocumentFormViewModel<ItemDto>> BuildFormAsync(
        ItemDto model,
        CancellationToken cancellationToken)
        => new()
        {
            Document = model,
            Lookups = await LookupBundle.LoadAsync(_lookups, cancellationToken).ConfigureAwait(false),
            IsEditable = true
        };
}
