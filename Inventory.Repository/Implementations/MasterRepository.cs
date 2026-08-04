using Inventory.DAL.Context;
using Inventory.Entities.Dtos;
using Inventory.Entities.Models;
using Inventory.Repository.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Inventory.Repository.Implementations;

/// <summary>
/// Shared implementation for the seven "code + name" masters. One class serves
/// Category, Unit, Department, Site, Warehouse, Manufacturer and Courier, which
/// removes seven near-identical repositories from the solution.
/// </summary>
/// <typeparam name="TEntity">Concrete master entity.</typeparam>
public class MasterRepository<TEntity> : GenericRepository<TEntity>, IMasterRepository<TEntity>
    where TEntity : MasterEntity
{
    public MasterRepository(ApplicationDbContext context)
        : base(context)
    {
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<LookupDto>> GetLookupAsync(CancellationToken cancellationToken = default)
        => await DbSet.AsNoTracking()
            .Where(e => e.IsActive && !e.IsDeleted)
            .OrderBy(e => e.DisplayOrder)
            .ThenBy(e => e.Name)
            .Select(e => new LookupDto
            {
                Id = e.Id,
                Text = e.Name,
                Code = e.Code
            })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    /// <inheritdoc />
    public async Task<bool> IsCodeTakenAsync(string code, int excludeId = 0, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return false;
        }

        var normalized = code.Trim();

        return await DbSet.AsNoTracking()
            .AnyAsync(e => !e.IsDeleted && e.Code == normalized && e.Id != excludeId, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<string> GenerateCodeAsync(string prefix, CancellationToken cancellationToken = default)
    {
        var safePrefix = string.IsNullOrWhiteSpace(prefix) ? "MST" : prefix.Trim().ToUpperInvariant();

        // Highest existing id is enough: master codes are cosmetic, and a real
        // uniqueness guarantee is provided by the unique index on Code.
        var lastId = await DbSet.AsNoTracking()
            .Select(e => (int?)e.Id)
            .MaxAsync(cancellationToken)
            .ConfigureAwait(false) ?? 0;

        var candidate = $"{safePrefix}-{lastId + 1:D4}";
        var attempt = 1;

        while (await IsCodeTakenAsync(candidate, 0, cancellationToken).ConfigureAwait(false))
        {
            candidate = $"{safePrefix}-{lastId + 1 + attempt:D4}";
            attempt++;

            if (attempt > 100)
            {
                // Extremely unlikely; fall back to a time-based suffix.
                candidate = $"{safePrefix}-{DateTime.UtcNow:HHmmssfff}";
                break;
            }
        }

        return candidate;
    }
}

/// <summary>Warehouse repository; adds the site-scoped lookup used by cascading drop-downs.</summary>
public sealed class WarehouseRepository : MasterRepository<Warehouse>, IWarehouseRepository
{
    public WarehouseRepository(ApplicationDbContext context)
        : base(context)
    {
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<LookupDto>> GetLookupBySiteAsync(
        int? siteId,
        CancellationToken cancellationToken = default)
    {
        var query = DbSet.AsNoTracking().Where(w => w.IsActive && !w.IsDeleted);

        if (siteId is > 0)
        {
            query = query.Where(w => w.SiteId == siteId);
        }

        return await query
            .OrderBy(w => w.DisplayOrder)
            .ThenBy(w => w.Name)
            .Select(w => new LookupDto
            {
                Id = w.Id,
                Text = w.Name,
                Code = w.Code,
                ParentId = w.SiteId,
                Extra = w.Site.Name
            })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }
}

/// <summary>Engineer repository; the engineer master carries department and site.</summary>
public sealed class EngineerRepository : MasterRepository<Engineer>, IEngineerRepository
{
    public EngineerRepository(ApplicationDbContext context)
        : base(context)
    {
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<LookupDto>> GetLookupByDepartmentAsync(
        int? departmentId,
        CancellationToken cancellationToken = default)
    {
        var query = DbSet.AsNoTracking().Where(e => e.IsActive && !e.IsDeleted);

        if (departmentId is > 0)
        {
            query = query.Where(e => e.DepartmentId == departmentId);
        }

        return await query
            .OrderBy(e => e.Name)
            .Select(e => new LookupDto
            {
                Id = e.Id,
                Text = e.Name,
                Code = e.EmployeeCode ?? e.Code,
                ParentId = e.DepartmentId,
                Extra = e.Designation
            })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }
}
