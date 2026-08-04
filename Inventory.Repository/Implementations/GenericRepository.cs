using System.Linq.Expressions;
using System.Reflection;
using Inventory.Common.Models;
using Inventory.DAL.Context;
using Inventory.Entities.Models;
using Inventory.Repository.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Inventory.Repository.Implementations;

/// <summary>
/// Entity Framework Core implementation of <see cref="IGenericRepository{TEntity}"/>.
/// <para>
/// Soft-deleted rows are excluded centrally by <see cref="ApplyNotDeleted"/> so
/// no caller has to remember the filter, and sorting is resolved against the
/// entity's real CLR properties, which makes an injected sort expression
/// impossible.
/// </para>
/// </summary>
/// <typeparam name="TEntity">Entity type.</typeparam>
public class GenericRepository<TEntity> : IGenericRepository<TEntity> where TEntity : class
{
    /// <summary>Shared context; the unit of work owns its lifetime.</summary>
    protected readonly ApplicationDbContext Context;

    /// <summary>Convenience handle to the entity set.</summary>
    protected readonly DbSet<TEntity> DbSet;

    private static readonly PropertyInfo[] StringProperties = typeof(TEntity)
        .GetProperties(BindingFlags.Public | BindingFlags.Instance)
        .Where(p => p.PropertyType == typeof(string) && p.CanRead)
        .ToArray();

    private static readonly bool HasSoftDelete = typeof(TEntity).GetProperty(nameof(AuditableEntity.IsDeleted)) is not null;

    public GenericRepository(ApplicationDbContext context)
    {
        Context = context ?? throw new ArgumentNullException(nameof(context));
        DbSet = context.Set<TEntity>();
    }

    /// <inheritdoc />
    public virtual async Task<TEntity?> GetByIdAsync(object id, CancellationToken cancellationToken = default)
        => await DbSet.FindAsync(new[] { id }, cancellationToken).ConfigureAwait(false);

    /// <inheritdoc />
    public virtual async Task<TEntity?> GetByIdAsync(
        int id,
        CancellationToken cancellationToken = default,
        params Expression<Func<TEntity, object>>[] includes)
    {
        IQueryable<TEntity> query = DbSet;

        foreach (var include in includes)
        {
            query = query.Include(include);
        }

        // "Id" is the primary key on every entity in this model.
        var parameter = Expression.Parameter(typeof(TEntity), "e");
        var idProperty = typeof(TEntity).GetProperty("Id")
                         ?? throw new InvalidOperationException(
                             $"{typeof(TEntity).Name} does not expose an 'Id' property.");

        var keyValue = Convert.ChangeType(id, idProperty.PropertyType, System.Globalization.CultureInfo.InvariantCulture);

        var body = Expression.Equal(
            Expression.Property(parameter, idProperty),
            Expression.Constant(keyValue, idProperty.PropertyType));

        var predicate = Expression.Lambda<Func<TEntity, bool>>(body, parameter);

        return await query.FirstOrDefaultAsync(predicate, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public virtual async Task<IReadOnlyList<TEntity>> GetAllAsync(CancellationToken cancellationToken = default)
        => await ApplyNotDeleted(DbSet.AsNoTracking())
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    /// <inheritdoc />
    public virtual async Task<IReadOnlyList<TEntity>> FindAsync(
        Expression<Func<TEntity, bool>> predicate,
        CancellationToken cancellationToken = default)
        => await ApplyNotDeleted(DbSet.AsNoTracking())
            .Where(predicate)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    /// <inheritdoc />
    public virtual async Task<TEntity?> FirstOrDefaultAsync(
        Expression<Func<TEntity, bool>> predicate,
        CancellationToken cancellationToken = default)
        => await ApplyNotDeleted(DbSet)
            .FirstOrDefaultAsync(predicate, cancellationToken)
            .ConfigureAwait(false);

    /// <inheritdoc />
    public virtual async Task<IReadOnlyList<TEntity>> SearchAsync(
        string? searchTerm,
        int maxResults = 50,
        CancellationToken cancellationToken = default)
    {
        var query = ApplyNotDeleted(DbSet.AsNoTracking());

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            var predicate = BuildSearchPredicate(searchTerm.Trim());
            if (predicate is not null)
            {
                query = query.Where(predicate);
            }
        }

        return await query.Take(maxResults).ToListAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public virtual async Task<PagedResult<TEntity>> GetPagedAsync(
        PagedRequest request,
        Expression<Func<TEntity, bool>>? filter = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var query = BuildPagedQuery(request, filter);
        var total = await query.CountAsync(cancellationToken).ConfigureAwait(false);

        var items = await ApplySorting(query, request)
            .Skip(request.Skip)
            .Take(request.PageSize)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return new PagedResult<TEntity>(items, total, request.PageNumber, request.PageSize);
    }

    /// <inheritdoc />
    public virtual async Task<PagedResult<TResult>> GetPagedAsync<TResult>(
        PagedRequest request,
        Expression<Func<TEntity, TResult>> selector,
        Expression<Func<TEntity, bool>>? filter = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(selector);

        var query = BuildPagedQuery(request, filter);
        var total = await query.CountAsync(cancellationToken).ConfigureAwait(false);

        var items = await ApplySorting(query, request)
            .Skip(request.Skip)
            .Take(request.PageSize)
            .Select(selector)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return new PagedResult<TResult>(items, total, request.PageNumber, request.PageSize);
    }

    /// <inheritdoc />
    public virtual async Task<bool> AnyAsync(
        Expression<Func<TEntity, bool>> predicate,
        CancellationToken cancellationToken = default)
        => await ApplyNotDeleted(DbSet.AsNoTracking())
            .AnyAsync(predicate, cancellationToken)
            .ConfigureAwait(false);

    /// <inheritdoc />
    public virtual async Task<int> CountAsync(
        Expression<Func<TEntity, bool>>? predicate = null,
        CancellationToken cancellationToken = default)
    {
        var query = ApplyNotDeleted(DbSet.AsNoTracking());

        if (predicate is not null)
        {
            query = query.Where(predicate);
        }

        return await query.CountAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public virtual async Task AddAsync(TEntity entity, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entity);
        await DbSet.AddAsync(entity, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public virtual async Task AddRangeAsync(IEnumerable<TEntity> entities, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entities);
        await DbSet.AddRangeAsync(entities, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public virtual void Update(TEntity entity)
    {
        ArgumentNullException.ThrowIfNull(entity);
        DbSet.Update(entity);
    }

    /// <inheritdoc />
    public virtual void UpdateRange(IEnumerable<TEntity> entities)
    {
        ArgumentNullException.ThrowIfNull(entities);
        DbSet.UpdateRange(entities);
    }

    /// <inheritdoc />
    public virtual void Delete(TEntity entity)
    {
        ArgumentNullException.ThrowIfNull(entity);
        DbSet.Remove(entity);
    }

    /// <inheritdoc />
    public virtual void DeleteRange(IEnumerable<TEntity> entities)
    {
        ArgumentNullException.ThrowIfNull(entities);
        DbSet.RemoveRange(entities);
    }

    /// <inheritdoc />
    public virtual IQueryable<TEntity> Query()
        => ApplyNotDeleted(DbSet.AsNoTracking());

    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------

    /// <summary>Excludes soft-deleted rows when the entity supports the flag.</summary>
    protected static IQueryable<TEntity> ApplyNotDeleted(IQueryable<TEntity> query)
    {
        if (!HasSoftDelete)
        {
            return query;
        }

        var parameter = Expression.Parameter(typeof(TEntity), "e");
        var body = Expression.Equal(
            Expression.Property(parameter, nameof(AuditableEntity.IsDeleted)),
            Expression.Constant(false));

        return query.Where(Expression.Lambda<Func<TEntity, bool>>(body, parameter));
    }

    private IQueryable<TEntity> BuildPagedQuery(PagedRequest request, Expression<Func<TEntity, bool>>? filter)
    {
        var query = ApplyNotDeleted(DbSet.AsNoTracking());

        if (filter is not null)
        {
            query = query.Where(filter);
        }

        if (!string.IsNullOrWhiteSpace(request.SearchTerm))
        {
            var predicate = BuildSearchPredicate(request.SearchTerm.Trim());
            if (predicate is not null)
            {
                query = query.Where(predicate);
            }
        }

        return query;
    }

    /// <summary>
    /// Builds <c>x =&gt; x.A.Contains(term) || x.B.Contains(term) ...</c> across
    /// every string property of the entity.
    /// </summary>
    private static Expression<Func<TEntity, bool>>? BuildSearchPredicate(string term)
    {
        if (StringProperties.Length == 0)
        {
            return null;
        }

        var containsMethod = typeof(string).GetMethod(nameof(string.Contains), new[] { typeof(string) })!;
        var parameter = Expression.Parameter(typeof(TEntity), "e");
        var constant = Expression.Constant(term, typeof(string));

        Expression? combined = null;

        foreach (var property in StringProperties)
        {
            var access = Expression.Property(parameter, property);
            // Guard against NULL columns: (x.Prop != null && x.Prop.Contains(term))
            var notNull = Expression.NotEqual(access, Expression.Constant(null, typeof(string)));
            var contains = Expression.Call(access, containsMethod, constant);
            var clause = Expression.AndAlso(notNull, contains);

            combined = combined is null ? clause : Expression.OrElse(combined, clause);
        }

        return combined is null ? null : Expression.Lambda<Func<TEntity, bool>>(combined, parameter);
    }

    /// <summary>
    /// Applies the requested sort. The column name is matched against the
    /// entity's actual properties; an unknown name falls back to <c>Id</c>,
    /// which keeps paging deterministic and rejects injected expressions.
    /// </summary>
    private static IQueryable<TEntity> ApplySorting(IQueryable<TEntity> query, PagedRequest request)
    {
        var property = ResolveSortProperty(request.SortColumn);

        if (property is null)
        {
            return query;
        }

        var parameter = Expression.Parameter(typeof(TEntity), "e");
        var access = Expression.Property(parameter, property);

        // Build the selector with the property's real CLR type: converting to
        // object here would produce an expression SQL Server cannot translate.
        var selector = Expression.Lambda(access, parameter);

        var methodName = request.SafeSortDirection == "DESC" ? nameof(Queryable.OrderByDescending) : nameof(Queryable.OrderBy);

        var call = Expression.Call(
            typeof(Queryable),
            methodName,
            new[] { typeof(TEntity), property.PropertyType },
            query.Expression,
            Expression.Quote(selector));

        return query.Provider.CreateQuery<TEntity>(call);
    }

    private static PropertyInfo? ResolveSortProperty(string? requested)
    {
        var name = string.IsNullOrWhiteSpace(requested) ? "Id" : requested.Trim();

        return typeof(TEntity)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .FirstOrDefault(p =>
                p.CanRead
                && string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase)
                && (p.PropertyType.IsValueType || p.PropertyType == typeof(string)));
    }
}
