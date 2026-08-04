using AutoMapper;
using Dapper;
using Inventory.BLL.Interfaces;
using Inventory.Common.Constants;
using Inventory.Common.Enums;
using Inventory.Common.Extensions;
using Inventory.Common.Models;
using Inventory.Common.Security;
using Inventory.DAL.StoredProcedures;
using Inventory.Entities.Dtos;
using Inventory.Entities.Models;
using Inventory.Repository.Interfaces;
using Inventory.Repository.UnitOfWork;
using Inventory.Services.Caching;
using Inventory.Services.Export;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Inventory.BLL.Services;

/// <summary>
/// Static description of one simple master: which entity it maps to, which
/// discriminator the shared stored procedures expect, and how its codes are
/// generated. Adding a new lookup master is a one-line change here.
/// </summary>
/// <typeparam name="TEntity">Entity type.</typeparam>
/// <typeparam name="TDto">DTO type.</typeparam>
public sealed class MasterDefinition<TEntity, TDto>
    where TEntity : MasterEntity
    where TDto : MasterDto
{
    public required string MasterType { get; init; }

    public required string DisplayName { get; init; }

    public required string CodePrefix { get; init; }

    public required string CacheKey { get; init; }
}

/// <summary>
/// Generic CRUD service for the seven simple masters.
/// <para>
/// Every write is executed by the shared <c>sp_InsertMaster</c> /
/// <c>sp_UpdateMaster</c> / <c>sp_DeleteMaster</c> procedures, which branch on a
/// master-type discriminator with fully static statements — no dynamic SQL
/// anywhere. Reads that only feed a grid or a drop-down use the repository's
/// parameterised LINQ, and lookups are cached.
/// </para>
/// </summary>
public class MasterService<TEntity, TDto> : IMasterService<TDto>
    where TEntity : MasterEntity
    where TDto : MasterDto
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IStoredProcedureExecutor _executor;
    private readonly IMapper _mapper;
    private readonly ICacheService _cache;
    private readonly ICurrentUser _currentUser;
    private readonly IAuditService _audit;
    private readonly IExportService _export;
    private readonly ILogger _logger;
    private readonly MasterDefinition<TEntity, TDto> _definition;

    public MasterService(
        IUnitOfWork unitOfWork,
        IStoredProcedureExecutor executor,
        IMapper mapper,
        ICacheService cache,
        ICurrentUser currentUser,
        IAuditService audit,
        IExportService export,
        ILogger logger,
        MasterDefinition<TEntity, TDto> definition)
    {
        _unitOfWork = unitOfWork;
        _executor = executor;
        _mapper = mapper;
        _cache = cache;
        _currentUser = currentUser;
        _audit = audit;
        _export = export;
        _logger = logger;
        _definition = definition;
    }

    /// <summary>Repository for the concrete entity, resolved from the unit of work.</summary>
    private IGenericRepository<TEntity> Repo => _unitOfWork.Repository<TEntity>();

    /// <inheritdoc />
    public virtual async Task<PagedResult<TDto>> GetPagedAsync(
        PagedRequest request,
        CancellationToken cancellationToken = default)
    {
        var page = await Repo.GetPagedAsync(request, cancellationToken: cancellationToken).ConfigureAwait(false);

        return new PagedResult<TDto>(
            page.Items.Select(e => _mapper.Map<TDto>(e)).ToList(),
            page.TotalCount,
            page.PageNumber,
            page.PageSize);
    }

    /// <inheritdoc />
    public virtual async Task<TDto?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var entity = await Repo.GetByIdAsync(id, cancellationToken).ConfigureAwait(false);
        return entity is null ? null : _mapper.Map<TDto>(entity);
    }

    /// <inheritdoc />
    public virtual async Task<IReadOnlyList<LookupDto>> GetLookupAsync(CancellationToken cancellationToken = default)
        => await _cache.GetOrCreateAsync(
            _definition.CacheKey,
            async () => await LoadLookupAsync(cancellationToken).ConfigureAwait(false),
            CacheKeys.LookupDuration,
            cancellationToken).ConfigureAwait(false);

    /// <inheritdoc />
    public virtual async Task<ServiceResult<int>> CreateAsync(TDto dto, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        var validation = await ValidateAsync(dto, isUpdate: false, cancellationToken).ConfigureAwait(false);

        if (!validation.Succeeded)
        {
            return ServiceResult<int>.From(validation);
        }

        if (dto.Code.IsBlank())
        {
            dto.Code = await GenerateCodeAsync(cancellationToken).ConfigureAwait(false);
        }

        var result = await _executor
            .ExecuteAsync(StoredProcedures.InsertMaster, BuildParameters(dto), cancellationToken)
            .ConfigureAwait(false);

        if (!result.IsSuccess)
        {
            return ServiceResult<int>.Failure(result.Message, result.ReturnCode);
        }

        _cache.Remove(_definition.CacheKey);

        await _audit.LogAsync(
            AuditAction.Create,
            _definition.DisplayName,
            result.NewId?.ToString(),
            $"Created {_definition.DisplayName} '{dto.Name}'.",
            newValues: Serialize(dto),
            cancellationToken: cancellationToken).ConfigureAwait(false);

        return ServiceResult<int>.Success(result.NewId ?? 0, $"{_definition.DisplayName} created successfully.");
    }

    /// <inheritdoc />
    public virtual async Task<ServiceResult> UpdateAsync(TDto dto, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        var existing = await Repo.GetByIdAsync(dto.Id, cancellationToken).ConfigureAwait(false);

        if (existing is null)
        {
            return ServiceResult.Failure($"{_definition.DisplayName} was not found.", -404);
        }

        var validation = await ValidateAsync(dto, isUpdate: true, cancellationToken).ConfigureAwait(false);

        if (!validation.Succeeded)
        {
            return validation;
        }

        var before = Serialize(_mapper.Map<TDto>(existing));

        var result = await _executor
            .ExecuteAsync(StoredProcedures.UpdateMaster, BuildParameters(dto), cancellationToken)
            .ConfigureAwait(false);

        if (!result.IsSuccess)
        {
            return ServiceResult.Failure(result.Message, result.ReturnCode);
        }

        _cache.Remove(_definition.CacheKey);

        await _audit.LogAsync(
            AuditAction.Update,
            _definition.DisplayName,
            dto.Id.ToString(),
            $"Updated {_definition.DisplayName} '{dto.Name}'.",
            oldValues: before,
            newValues: Serialize(dto),
            cancellationToken: cancellationToken).ConfigureAwait(false);

        return ServiceResult.Success($"{_definition.DisplayName} updated successfully.");
    }

    /// <inheritdoc />
    public virtual async Task<ServiceResult> DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        var existing = await Repo.GetByIdAsync(id, cancellationToken).ConfigureAwait(false);

        if (existing is null)
        {
            return ServiceResult.Failure($"{_definition.DisplayName} was not found.", -404);
        }

        var parameters = new DynamicParameters();
        parameters.Add("@MasterType", _definition.MasterType);
        parameters.Add("@Id", id);
        parameters.Add("@CurrentUserId", _currentUser.UserId);

        // The procedure refuses the delete when dependent rows exist, so
        // referential integrity is enforced where the data lives.
        var result = await _executor
            .ExecuteAsync(StoredProcedures.DeleteMaster, parameters, cancellationToken)
            .ConfigureAwait(false);

        if (!result.IsSuccess)
        {
            return ServiceResult.Failure(result.Message, result.ReturnCode);
        }

        _cache.Remove(_definition.CacheKey);

        await _audit.LogAsync(
            AuditAction.Delete,
            _definition.DisplayName,
            id.ToString(),
            $"Deleted {_definition.DisplayName} '{existing.Name}'.",
            oldValues: Serialize(_mapper.Map<TDto>(existing)),
            cancellationToken: cancellationToken).ConfigureAwait(false);

        return ServiceResult.Success($"{_definition.DisplayName} deleted successfully.");
    }

    /// <inheritdoc />
    public virtual async Task<bool> IsCodeAvailableAsync(
        string code,
        int excludeId = 0,
        CancellationToken cancellationToken = default)
    {
        if (code.IsBlank())
        {
            return true;
        }

        var normalized = code.Trim();

        return !await Repo
            .AnyAsync(e => !e.IsDeleted && e.Code == normalized && e.Id != excludeId, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public virtual async Task<FileExportResult> ExportAsync(
        ExportFormat format,
        CancellationToken cancellationToken = default)
    {
        var rows = await Repo.GetAllAsync(cancellationToken).ConfigureAwait(false);
        var dtos = rows.Select(e => _mapper.Map<TDto>(e)).OrderBy(d => d.Name).ToList();

        var definition = Exports.For<TDto>()
            .WithTitle($"{_definition.DisplayName} Master")
            .WithSubTitle($"{dtos.Count:N0} record(s)")
            .Column("Code", d => d.Code, width: 0.7f)
            .Column("Name", d => d.Name, width: 2f)
            .Column("Description", d => d.Description, width: 2.5f)
            .Column("Display Order", d => d.DisplayOrder, alignRight: true)
            .Column("Status", d => d.IsActive ? "Active" : "Inactive", width: 0.6f)
            .DateColumn("Created On", d => d.CreatedOn)
            .ShowTotals(false)
            .Build(dtos);

        await _audit.LogAsync(
            AuditAction.Export,
            _definition.DisplayName,
            description: $"Exported the {_definition.DisplayName} master as {format}.",
            cancellationToken: cancellationToken).ConfigureAwait(false);

        return _export.Export(definition, format);
    }

    // -----------------------------------------------------------------------
    // Extension points
    // -----------------------------------------------------------------------

    /// <summary>
    /// Validates business rules that data annotations cannot express.
    /// Derived services override this to add master-specific checks.
    /// </summary>
    protected virtual async Task<ServiceResult> ValidateAsync(
        TDto dto,
        bool isUpdate,
        CancellationToken cancellationToken)
    {
        var errors = new List<string>();

        if (dto.Name.IsBlank())
        {
            errors.Add("Name is required.");
        }

        if (!dto.Code.IsBlank()
            && !await IsCodeAvailableAsync(dto.Code, isUpdate ? dto.Id : 0, cancellationToken).ConfigureAwait(false))
        {
            errors.Add($"The code '{dto.Code}' is already in use.");
        }

        return errors.Count == 0 ? ServiceResult.Success() : ServiceResult.ValidationFailure(errors);
    }

    /// <summary>
    /// Maps the DTO onto the shared master procedure's parameter list. Derived
    /// services add the columns that only their master has.
    /// </summary>
    protected virtual DynamicParameters BuildParameters(TDto dto)
    {
        var parameters = new DynamicParameters();

        parameters.Add("@MasterType", _definition.MasterType);
        parameters.Add("@Id", dto.Id);
        parameters.Add("@Code", dto.Code);
        parameters.Add("@Name", dto.Name);
        parameters.Add("@Description", dto.Description);
        parameters.Add("@DisplayOrder", dto.DisplayOrder);
        parameters.Add("@IsActive", dto.IsActive);
        parameters.Add("@CurrentUserId", _currentUser.UserId);

        // Optional columns; the procedure ignores the ones its branch does not use.
        parameters.Add("@ParentId", null, System.Data.DbType.Int32);
        parameters.Add("@Symbol", null, System.Data.DbType.String, size: 10);
        parameters.Add("@DecimalPlaces", null, System.Data.DbType.Int32);
        parameters.Add("@Address", null, System.Data.DbType.String, size: 400);
        parameters.Add("@City", null, System.Data.DbType.String, size: 100);
        parameters.Add("@State", null, System.Data.DbType.String, size: 100);
        parameters.Add("@PinCode", null, System.Data.DbType.String, size: 20);
        parameters.Add("@ContactPerson", null, System.Data.DbType.String, size: 150);
        parameters.Add("@ContactNumber", null, System.Data.DbType.String, size: 20);
        parameters.Add("@Email", null, System.Data.DbType.String, size: 150);
        parameters.Add("@Country", null, System.Data.DbType.String, size: 100);
        parameters.Add("@Website", null, System.Data.DbType.String, size: 200);
        parameters.Add("@TrackingUrlTemplate", null, System.Data.DbType.String, size: 300);
        parameters.Add("@IsDefault", false);
        parameters.Add("@EmployeeCode", null, System.Data.DbType.String, size: 30);
        parameters.Add("@Designation", null, System.Data.DbType.String, size: 150);
        parameters.Add("@MobileNumber", null, System.Data.DbType.String, size: 20);
        parameters.Add("@SecondaryParentId", null, System.Data.DbType.Int32);

        ApplySpecificParameters(dto, parameters);

        return parameters;
    }

    /// <summary>Hook for the master-specific columns.</summary>
    protected virtual void ApplySpecificParameters(TDto dto, DynamicParameters parameters)
    {
    }

    /// <summary>Loads the lookup list; overridden where extra columns are needed.</summary>
    protected virtual async Task<IReadOnlyList<LookupDto>> LoadLookupAsync(CancellationToken cancellationToken)
        => await Repo.Query()
            .Where(e => e.IsActive && !e.IsDeleted)
            .OrderBy(e => e.DisplayOrder)
            .ThenBy(e => e.Name)
            .Select(e => new LookupDto { Id = e.Id, Text = e.Name, Code = e.Code })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    private async Task<string> GenerateCodeAsync(CancellationToken cancellationToken)
    {
        var lastId = await Repo.CountAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
        var candidate = $"{_definition.CodePrefix}-{lastId + 1:D4}";

        for (var attempt = 1; attempt <= 100; attempt++)
        {
            if (await IsCodeAvailableAsync(candidate, 0, cancellationToken).ConfigureAwait(false))
            {
                return candidate;
            }

            candidate = $"{_definition.CodePrefix}-{lastId + 1 + attempt:D4}";
        }

        _logger.LogWarning("Could not allocate a sequential code for {Master}; falling back to a timestamp.",
            _definition.DisplayName);

        return $"{_definition.CodePrefix}-{DateTime.UtcNow:HHmmssfff}";
    }

    /// <summary>Compact JSON snapshot written to the audit trail.</summary>
    private static string Serialize(TDto dto)
        => System.Text.Json.JsonSerializer.Serialize(dto, new System.Text.Json.JsonSerializerOptions
        {
            WriteIndented = false,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
        });
}
