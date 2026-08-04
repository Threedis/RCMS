using AutoMapper;
using Dapper;
using Inventory.BLL.Interfaces;
using Inventory.Common.Constants;
using Inventory.Common.Extensions;
using Inventory.Common.Models;
using Inventory.Common.Security;
using Inventory.DAL.StoredProcedures;
using Inventory.Entities.Dtos;
using Inventory.Entities.Models;
using Inventory.Repository.UnitOfWork;
using Inventory.Services.Caching;
using Inventory.Services.Export;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Inventory.BLL.Services;

/// <summary>Category master; adds the parent-category rules.</summary>
public sealed class CategoryService : MasterService<Category, CategoryDto>
{
    public CategoryService(
        IUnitOfWork unitOfWork,
        IStoredProcedureExecutor executor,
        IMapper mapper,
        ICacheService cache,
        ICurrentUser currentUser,
        IAuditService audit,
        IExportService export,
        ILogger<CategoryService> logger)
        : base(unitOfWork, executor, mapper, cache, currentUser, audit, export, logger,
            new MasterDefinition<Category, CategoryDto>
            {
                MasterType = "Category",
                DisplayName = "Category",
                CodePrefix = "CAT",
                CacheKey = CacheKeys.Categories
            })
    {
        _unitOfWork = unitOfWork;
    }

    private readonly IUnitOfWork _unitOfWork;

    /// <inheritdoc />
    protected override void ApplySpecificParameters(CategoryDto dto, DynamicParameters parameters)
        => parameters.Add("@ParentId", dto.ParentCategoryId);

    /// <inheritdoc />
    protected override async Task<ServiceResult> ValidateAsync(
        CategoryDto dto,
        bool isUpdate,
        CancellationToken cancellationToken)
    {
        var baseResult = await base.ValidateAsync(dto, isUpdate, cancellationToken).ConfigureAwait(false);

        if (!baseResult.Succeeded)
        {
            return baseResult;
        }

        // A category cannot be its own parent, and only two levels are supported.
        if (dto.ParentCategoryId == dto.Id && dto.Id > 0)
        {
            return ServiceResult.ValidationFailure(new[] { "A category cannot be its own parent." });
        }

        if (dto.ParentCategoryId is > 0)
        {
            var parent = await _unitOfWork.Categories
                .GetByIdAsync(dto.ParentCategoryId.Value, cancellationToken)
                .ConfigureAwait(false);

            if (parent is null)
            {
                return ServiceResult.ValidationFailure(new[] { "The selected parent category does not exist." });
            }

            if (parent.ParentCategoryId is not null)
            {
                return ServiceResult.ValidationFailure(
                    new[] { "Categories support two levels only; the selected parent is already a sub-category." });
            }
        }

        return ServiceResult.Success();
    }
}

/// <summary>Unit of measure master.</summary>
public sealed class UnitService : MasterService<Unit, UnitDto>
{
    public UnitService(
        IUnitOfWork unitOfWork,
        IStoredProcedureExecutor executor,
        IMapper mapper,
        ICacheService cache,
        ICurrentUser currentUser,
        IAuditService audit,
        IExportService export,
        ILogger<UnitService> logger)
        : base(unitOfWork, executor, mapper, cache, currentUser, audit, export, logger,
            new MasterDefinition<Unit, UnitDto>
            {
                MasterType = "Unit",
                DisplayName = "Unit",
                CodePrefix = "UOM",
                CacheKey = CacheKeys.Units
            })
    {
    }

    /// <inheritdoc />
    protected override void ApplySpecificParameters(UnitDto dto, DynamicParameters parameters)
    {
        parameters.Add("@Symbol", dto.Symbol);
        parameters.Add("@DecimalPlaces", dto.DecimalPlaces);
    }

    /// <inheritdoc />
    protected override async Task<ServiceResult> ValidateAsync(
        UnitDto dto,
        bool isUpdate,
        CancellationToken cancellationToken)
    {
        var baseResult = await base.ValidateAsync(dto, isUpdate, cancellationToken).ConfigureAwait(false);

        if (!baseResult.Succeeded)
        {
            return baseResult;
        }

        return dto.Symbol.IsBlank()
            ? ServiceResult.ValidationFailure(new[] { "Symbol is required." })
            : ServiceResult.Success();
    }
}

/// <summary>Department master.</summary>
public sealed class DepartmentService : MasterService<Department, DepartmentDto>
{
    public DepartmentService(
        IUnitOfWork unitOfWork,
        IStoredProcedureExecutor executor,
        IMapper mapper,
        ICacheService cache,
        ICurrentUser currentUser,
        IAuditService audit,
        IExportService export,
        ILogger<DepartmentService> logger)
        : base(unitOfWork, executor, mapper, cache, currentUser, audit, export, logger,
            new MasterDefinition<Department, DepartmentDto>
            {
                MasterType = "Department",
                DisplayName = "Department",
                CodePrefix = "DEP",
                CacheKey = CacheKeys.Departments
            })
    {
    }

    /// <inheritdoc />
    protected override void ApplySpecificParameters(DepartmentDto dto, DynamicParameters parameters)
    {
        parameters.Add("@ContactPerson", dto.HeadOfDepartment);
        parameters.Add("@Email", dto.Email);
    }
}

/// <summary>Site master.</summary>
public sealed class SiteService : MasterService<Site, SiteDto>
{
    public SiteService(
        IUnitOfWork unitOfWork,
        IStoredProcedureExecutor executor,
        IMapper mapper,
        ICacheService cache,
        ICurrentUser currentUser,
        IAuditService audit,
        IExportService export,
        ILogger<SiteService> logger)
        : base(unitOfWork, executor, mapper, cache, currentUser, audit, export, logger,
            new MasterDefinition<Site, SiteDto>
            {
                MasterType = "Site",
                DisplayName = "Site",
                CodePrefix = "SIT",
                CacheKey = CacheKeys.Sites
            })
    {
    }

    /// <inheritdoc />
    protected override void ApplySpecificParameters(SiteDto dto, DynamicParameters parameters)
    {
        parameters.Add("@Address", dto.Address);
        parameters.Add("@City", dto.City);
        parameters.Add("@State", dto.State);
        parameters.Add("@PinCode", dto.PinCode);
        parameters.Add("@ContactPerson", dto.ContactPerson);
        parameters.Add("@ContactNumber", dto.ContactNumber);
    }
}

/// <summary>Warehouse master; a warehouse must belong to an active site.</summary>
public sealed class WarehouseService : MasterService<Warehouse, WarehouseDto>
{
    private readonly IUnitOfWork _unitOfWork;

    public WarehouseService(
        IUnitOfWork unitOfWork,
        IStoredProcedureExecutor executor,
        IMapper mapper,
        ICacheService cache,
        ICurrentUser currentUser,
        IAuditService audit,
        IExportService export,
        ILogger<WarehouseService> logger)
        : base(unitOfWork, executor, mapper, cache, currentUser, audit, export, logger,
            new MasterDefinition<Warehouse, WarehouseDto>
            {
                MasterType = "Warehouse",
                DisplayName = "Warehouse",
                CodePrefix = "WHS",
                CacheKey = CacheKeys.Warehouses
            })
    {
        _unitOfWork = unitOfWork;
    }

    /// <inheritdoc />
    protected override void ApplySpecificParameters(WarehouseDto dto, DynamicParameters parameters)
    {
        parameters.Add("@ParentId", dto.SiteId);
        parameters.Add("@ContactPerson", dto.InchargeName);
        parameters.Add("@ContactNumber", dto.ContactNumber);
        parameters.Add("@IsDefault", dto.IsDefault);
    }

    /// <inheritdoc />
    protected override async Task<ServiceResult> ValidateAsync(
        WarehouseDto dto,
        bool isUpdate,
        CancellationToken cancellationToken)
    {
        var baseResult = await base.ValidateAsync(dto, isUpdate, cancellationToken).ConfigureAwait(false);

        if (!baseResult.Succeeded)
        {
            return baseResult;
        }

        var site = await _unitOfWork.Sites.GetByIdAsync(dto.SiteId, cancellationToken).ConfigureAwait(false);

        if (site is null || site.IsDeleted)
        {
            return ServiceResult.ValidationFailure(new[] { "Select a valid site for the warehouse." });
        }

        return ServiceResult.Success();
    }

    /// <inheritdoc />
    protected override async Task<IReadOnlyList<LookupDto>> LoadLookupAsync(CancellationToken cancellationToken)
        => await _unitOfWork.Warehouses.GetLookupBySiteAsync(null, cancellationToken).ConfigureAwait(false);
}

/// <summary>Manufacturer master.</summary>
public sealed class ManufacturerService : MasterService<Manufacturer, ManufacturerDto>
{
    public ManufacturerService(
        IUnitOfWork unitOfWork,
        IStoredProcedureExecutor executor,
        IMapper mapper,
        ICacheService cache,
        ICurrentUser currentUser,
        IAuditService audit,
        IExportService export,
        ILogger<ManufacturerService> logger)
        : base(unitOfWork, executor, mapper, cache, currentUser, audit, export, logger,
            new MasterDefinition<Manufacturer, ManufacturerDto>
            {
                MasterType = "Manufacturer",
                DisplayName = "Manufacturer",
                CodePrefix = "MFR",
                CacheKey = CacheKeys.Manufacturers
            })
    {
    }

    /// <inheritdoc />
    protected override void ApplySpecificParameters(ManufacturerDto dto, DynamicParameters parameters)
    {
        parameters.Add("@Country", dto.Country);
        parameters.Add("@Website", dto.Website);
    }
}

/// <summary>Courier master.</summary>
public sealed class CourierService : MasterService<Courier, CourierDto>
{
    public CourierService(
        IUnitOfWork unitOfWork,
        IStoredProcedureExecutor executor,
        IMapper mapper,
        ICacheService cache,
        ICurrentUser currentUser,
        IAuditService audit,
        IExportService export,
        ILogger<CourierService> logger)
        : base(unitOfWork, executor, mapper, cache, currentUser, audit, export, logger,
            new MasterDefinition<Courier, CourierDto>
            {
                MasterType = "Courier",
                DisplayName = "Courier",
                CodePrefix = "CUR",
                CacheKey = CacheKeys.Couriers
            })
    {
    }

    /// <inheritdoc />
    protected override void ApplySpecificParameters(CourierDto dto, DynamicParameters parameters)
    {
        parameters.Add("@ContactPerson", dto.ContactPerson);
        parameters.Add("@ContactNumber", dto.ContactNumber);
        parameters.Add("@TrackingUrlTemplate", dto.TrackingUrlTemplate);
    }
}

/// <summary>Engineer master.</summary>
public sealed class EngineerService : MasterService<Engineer, EngineerDto>
{
    private readonly IUnitOfWork _unitOfWork;

    public EngineerService(
        IUnitOfWork unitOfWork,
        IStoredProcedureExecutor executor,
        IMapper mapper,
        ICacheService cache,
        ICurrentUser currentUser,
        IAuditService audit,
        IExportService export,
        ILogger<EngineerService> logger)
        : base(unitOfWork, executor, mapper, cache, currentUser, audit, export, logger,
            new MasterDefinition<Engineer, EngineerDto>
            {
                MasterType = "Engineer",
                DisplayName = "Engineer",
                CodePrefix = "ENG",
                CacheKey = CacheKeys.Engineers
            })
    {
        _unitOfWork = unitOfWork;
    }

    /// <inheritdoc />
    protected override void ApplySpecificParameters(EngineerDto dto, DynamicParameters parameters)
    {
        parameters.Add("@ParentId", dto.DepartmentId);
        parameters.Add("@SecondaryParentId", dto.SiteId);
        parameters.Add("@EmployeeCode", dto.EmployeeCode);
        parameters.Add("@Email", dto.Email);
        parameters.Add("@MobileNumber", dto.MobileNumber);
        parameters.Add("@Designation", dto.Designation);
    }

    /// <inheritdoc />
    protected override async Task<IReadOnlyList<LookupDto>> LoadLookupAsync(CancellationToken cancellationToken)
        => await _unitOfWork.Engineers.GetLookupByDepartmentAsync(null, cancellationToken).ConfigureAwait(false);
}

/// <summary>
/// Supplies every drop-down list in the application from a single cached place,
/// so a screen with eight selects still issues at most eight cheap reads on a
/// cold cache and none at all afterwards.
/// </summary>
public sealed class LookupService : ILookupService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICacheService _cache;

    public LookupService(IUnitOfWork unitOfWork, ICacheService cache)
    {
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<LookupDto>> CategoriesAsync(CancellationToken cancellationToken = default)
        => Cached(CacheKeys.Categories, () => _unitOfWork.Categories.GetLookupAsync(cancellationToken), cancellationToken);

    /// <inheritdoc />
    public Task<IReadOnlyList<LookupDto>> UnitsAsync(CancellationToken cancellationToken = default)
        => Cached(CacheKeys.Units, async () => (IReadOnlyList<LookupDto>)await _unitOfWork.Units.Query()
            .Where(u => u.IsActive && !u.IsDeleted)
            .OrderBy(u => u.DisplayOrder).ThenBy(u => u.Name)
            .Select(u => new LookupDto { Id = u.Id, Text = u.Name, Code = u.Code, Extra = u.Symbol })
            .ToListAsync(cancellationToken).ConfigureAwait(false), cancellationToken);

    /// <inheritdoc />
    public Task<IReadOnlyList<LookupDto>> DepartmentsAsync(CancellationToken cancellationToken = default)
        => Cached(CacheKeys.Departments, () => _unitOfWork.Departments.GetLookupAsync(cancellationToken), cancellationToken);

    /// <inheritdoc />
    public Task<IReadOnlyList<LookupDto>> SitesAsync(CancellationToken cancellationToken = default)
        => Cached(CacheKeys.Sites, () => _unitOfWork.Sites.GetLookupAsync(cancellationToken), cancellationToken);

    /// <inheritdoc />
    public async Task<IReadOnlyList<LookupDto>> WarehousesAsync(
        int? siteId = null,
        CancellationToken cancellationToken = default)
    {
        var all = await Cached(
            CacheKeys.Warehouses,
            () => _unitOfWork.Warehouses.GetLookupBySiteAsync(null, cancellationToken),
            cancellationToken).ConfigureAwait(false);

        // Filtering in memory avoids caching one entry per site.
        return siteId is > 0 ? all.Where(w => w.ParentId == siteId).ToList() : all;
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<LookupDto>> ManufacturersAsync(CancellationToken cancellationToken = default)
        => Cached(CacheKeys.Manufacturers, () => _unitOfWork.Manufacturers.GetLookupAsync(cancellationToken), cancellationToken);

    /// <inheritdoc />
    public Task<IReadOnlyList<LookupDto>> CouriersAsync(CancellationToken cancellationToken = default)
        => Cached(CacheKeys.Couriers, () => _unitOfWork.Couriers.GetLookupAsync(cancellationToken), cancellationToken);

    /// <inheritdoc />
    public async Task<IReadOnlyList<LookupDto>> EngineersAsync(
        int? departmentId = null,
        CancellationToken cancellationToken = default)
    {
        var all = await Cached(
            CacheKeys.Engineers,
            () => _unitOfWork.Engineers.GetLookupByDepartmentAsync(null, cancellationToken),
            cancellationToken).ConfigureAwait(false);

        return departmentId is > 0 ? all.Where(e => e.ParentId == departmentId).ToList() : all;
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<LookupDto>> VendorsAsync(CancellationToken cancellationToken = default)
        => Cached(CacheKeys.Vendors, async () => (IReadOnlyList<LookupDto>)await _unitOfWork.Vendors.Query()
            .Where(v => v.IsActive && !v.IsDeleted && !v.IsBlacklisted)
            .OrderBy(v => v.Name)
            .Select(v => new LookupDto { Id = v.Id, Text = v.Name, Code = v.Code, Extra = v.City })
            .ToListAsync(cancellationToken).ConfigureAwait(false), cancellationToken);

    /// <inheritdoc />
    public void InvalidateAll() => _cache.RemoveByPrefix("lookup:");

    private Task<IReadOnlyList<LookupDto>> Cached(
        string key,
        Func<Task<IReadOnlyList<LookupDto>>> factory,
        CancellationToken cancellationToken)
        => _cache.GetOrCreateAsync(key, factory, CacheKeys.LookupDuration, cancellationToken);
}
