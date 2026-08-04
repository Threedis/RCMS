using Inventory.BLL.Interfaces;
using Inventory.Common.Constants;
using Inventory.Entities.Dtos;
using Inventory.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Inventory.Web.Controllers;

/// <summary>
/// Base controller for the simple masters.
/// <para>
/// Category, Unit, Department, Site, Warehouse, Manufacturer, Courier and
/// Engineer all behave identically: a paged grid, a modal form, delete, and an
/// export. One generic controller and one shared set of views replace eight
/// near-identical copies, so a change to the grid behaviour is made once.
/// </para>
/// </summary>
/// <typeparam name="TDto">Master DTO type.</typeparam>
[Authorize(Policy = Policies.ManageMasters)]
public abstract class MasterControllerBase<TDto> : BaseController where TDto : MasterDto, new()
{
    /// <summary>Columns the grid may sort by, in the order the view renders them.</summary>
    private static readonly string[] SortableColumns = { "Code", "Name", "Description", "DisplayOrder", "IsActive" };

    private readonly IMasterService<TDto> _service;
    private readonly ILookupService _lookups;

    protected MasterControllerBase(IMasterService<TDto> service, ILookupService lookups)
    {
        _service = service;
        _lookups = lookups;
    }

    /// <summary>Singular display name, e.g. "Category". Used in titles and messages.</summary>
    protected abstract string EntityName { get; }

    /// <summary>Plural display name, e.g. "Categories".</summary>
    protected virtual string EntityNamePlural => EntityName + "s";

    /// <summary>Bootstrap icon class shown on the page header.</summary>
    protected virtual string Icon => "bi-list-ul";

    /// <summary>
    /// Name of the partial that renders the master-specific form fields, over
    /// and above the shared Code / Name / Description / Order / Active block.
    /// Return null when the master has no extra fields.
    /// </summary>
    protected virtual string? FieldsPartial => null;

    // -----------------------------------------------------------------------

    [HttpGet]
    [Authorize(Policy = Policies.ReadOnly)]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        await PopulateViewDataAsync(cancellationToken).ConfigureAwait(false);
        return View("~/Views/Shared/Master/Index.cshtml", new TDto());
    }

    /// <summary>Server-side paged grid feed for DataTables.</summary>
    [HttpPost]
    [Authorize(Policy = Policies.ReadOnly)]
    public async Task<IActionResult> List(CancellationToken cancellationToken)
    {
        var request = BuildPagedRequest(SortableColumns);
        var page = await _service.GetPagedAsync(request, cancellationToken).ConfigureAwait(false);

        return DataTable(page, request.Draw);
    }

    /// <summary>Returns one record as JSON for the edit modal.</summary>
    [HttpGet]
    [Authorize(Policy = Policies.ReadOnly)]
    public async Task<IActionResult> Get(int id, CancellationToken cancellationToken)
    {
        var dto = await _service.GetByIdAsync(id, cancellationToken).ConfigureAwait(false);

        return dto is null
            ? JsonFail($"The {EntityName.ToLowerInvariant()} was not found.")
            : JsonOk(dto, "Loaded.");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Save(TDto model, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(model);

        if (!ModelState.IsValid)
        {
            return ValidationFailed();
        }

        var result = model.Id > 0
            ? await _service.UpdateAsync(model, cancellationToken).ConfigureAwait(false)
            : await _service.CreateAsync(model, cancellationToken).ConfigureAwait(false);

        return result.Succeeded
            ? JsonOk(new { model.Id }, result.Message)
            : JsonFail(result.Message, result.Errors);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        var result = await _service.DeleteAsync(id, cancellationToken).ConfigureAwait(false);

        return result.Succeeded
            ? JsonOk(null, result.Message)
            : JsonFail(result.Message);
    }

    /// <summary>Remote validation for the unique code rule.</summary>
    [HttpGet]
    [Authorize(Policy = Policies.ReadOnly)]
    public async Task<IActionResult> IsCodeAvailable(string code, int id = 0, CancellationToken cancellationToken = default)
    {
        var available = await _service.IsCodeAvailableAsync(code, id, cancellationToken).ConfigureAwait(false);

        // jQuery Validate's remote rule expects true, or the message to display.
        return Json(available ? (object)true : $"The code '{code}' is already in use.");
    }

    [HttpGet]
    [Authorize(Policy = Policies.ViewReports)]
    public async Task<IActionResult> Export(string? format, CancellationToken cancellationToken)
    {
        var file = await _service.ExportAsync(ParseFormat(format), cancellationToken).ConfigureAwait(false);
        return Download(file);
    }

    /// <summary>Supplies the shared view with the labels and lookups it needs.</summary>
    private async Task PopulateViewDataAsync(CancellationToken cancellationToken)
    {
        ViewData["EntityName"] = EntityName;
        ViewData["EntityNamePlural"] = EntityNamePlural;
        ViewData["Icon"] = Icon;
        ViewData["FieldsPartial"] = FieldsPartial;
        ViewData["Controller"] = ControllerContext.ActionDescriptor.ControllerName;
        ViewData["Lookups"] = await LookupBundle.LoadAsync(_lookups, cancellationToken).ConfigureAwait(false);
    }
}

/// <summary>Item category master.</summary>
public sealed class CategoryController : MasterControllerBase<CategoryDto>
{
    public CategoryController(IMasterService<CategoryDto> service, ILookupService lookups)
        : base(service, lookups)
    {
    }

    protected override string EntityName => "Category";

    protected override string EntityNamePlural => "Categories";

    protected override string Icon => "bi-tags";

    protected override string? FieldsPartial => "~/Views/Shared/Master/_CategoryFields.cshtml";
}

/// <summary>Unit of measure master.</summary>
public sealed class UnitController : MasterControllerBase<UnitDto>
{
    public UnitController(IMasterService<UnitDto> service, ILookupService lookups)
        : base(service, lookups)
    {
    }

    protected override string EntityName => "Unit";

    protected override string Icon => "bi-rulers";

    protected override string? FieldsPartial => "~/Views/Shared/Master/_UnitFields.cshtml";
}

/// <summary>Department master.</summary>
public sealed class DepartmentController : MasterControllerBase<DepartmentDto>
{
    public DepartmentController(IMasterService<DepartmentDto> service, ILookupService lookups)
        : base(service, lookups)
    {
    }

    protected override string EntityName => "Department";

    protected override string Icon => "bi-diagram-3";

    protected override string? FieldsPartial => "~/Views/Shared/Master/_DepartmentFields.cshtml";
}

/// <summary>Site master.</summary>
public sealed class SiteController : MasterControllerBase<SiteDto>
{
    public SiteController(IMasterService<SiteDto> service, ILookupService lookups)
        : base(service, lookups)
    {
    }

    protected override string EntityName => "Site";

    protected override string Icon => "bi-geo-alt";

    protected override string? FieldsPartial => "~/Views/Shared/Master/_SiteFields.cshtml";
}

/// <summary>Warehouse master.</summary>
public sealed class WarehouseController : MasterControllerBase<WarehouseDto>
{
    public WarehouseController(IMasterService<WarehouseDto> service, ILookupService lookups)
        : base(service, lookups)
    {
    }

    protected override string EntityName => "Warehouse";

    protected override string Icon => "bi-building";

    protected override string? FieldsPartial => "~/Views/Shared/Master/_WarehouseFields.cshtml";
}

/// <summary>Manufacturer master.</summary>
public sealed class ManufacturerController : MasterControllerBase<ManufacturerDto>
{
    public ManufacturerController(IMasterService<ManufacturerDto> service, ILookupService lookups)
        : base(service, lookups)
    {
    }

    protected override string EntityName => "Manufacturer";

    protected override string Icon => "bi-award";

    protected override string? FieldsPartial => "~/Views/Shared/Master/_ManufacturerFields.cshtml";
}

/// <summary>Courier master.</summary>
public sealed class CourierController : MasterControllerBase<CourierDto>
{
    public CourierController(IMasterService<CourierDto> service, ILookupService lookups)
        : base(service, lookups)
    {
    }

    protected override string EntityName => "Courier";

    protected override string Icon => "bi-truck";

    protected override string? FieldsPartial => "~/Views/Shared/Master/_CourierFields.cshtml";
}

/// <summary>Engineer master.</summary>
public sealed class EngineerController : MasterControllerBase<EngineerDto>
{
    public EngineerController(IMasterService<EngineerDto> service, ILookupService lookups)
        : base(service, lookups)
    {
    }

    protected override string EntityName => "Engineer";

    protected override string Icon => "bi-person-gear";

    protected override string? FieldsPartial => "~/Views/Shared/Master/_EngineerFields.cshtml";
}
