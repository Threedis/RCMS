using System.Linq.Expressions;
using Inventory.Common.Models;

namespace Inventory.Repository.Interfaces;

/// <summary>
/// Type-safe data access contract shared by every entity.
/// <para>
/// Reads are expressed as LINQ over the object model — never as SQL text —
/// while every state change that must be transactional or audited is delegated
/// to a stored procedure by the specific repositories. Nothing in this
/// interface, or in its implementation, concatenates SQL.
/// </para>
/// </summary>
/// <typeparam name="TEntity">Entity type.</typeparam>
public interface IGenericRepository<TEntity> where TEntity : class
{
    // ---- Read -------------------------------------------------------------

    /// <summary>Finds an entity by primary key, or null.</summary>
    Task<TEntity?> GetByIdAsync(object id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Loads an entity by key together with the requested navigation properties.
    /// </summary>
    Task<TEntity?> GetByIdAsync(
        int id,
        CancellationToken cancellationToken = default,
        params Expression<Func<TEntity, object>>[] includes);

    /// <summary>Returns every non-deleted row.</summary>
    Task<IReadOnlyList<TEntity>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>Returns every non-deleted row matching <paramref name="predicate"/>.</summary>
    Task<IReadOnlyList<TEntity>> FindAsync(
        Expression<Func<TEntity, bool>> predicate,
        CancellationToken cancellationToken = default);

    /// <summary>Returns the first match, or null.</summary>
    Task<TEntity?> FirstOrDefaultAsync(
        Expression<Func<TEntity, bool>> predicate,
        CancellationToken cancellationToken = default);

    /// <summary>Full-text style search over the entity's searchable columns.</summary>
    Task<IReadOnlyList<TEntity>> SearchAsync(
        string? searchTerm,
        int maxResults = 50,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Server-side paged query. Sorting is applied through a white-listed
    /// column name so the caller can never inject an expression.
    /// </summary>
    Task<PagedResult<TEntity>> GetPagedAsync(
        PagedRequest request,
        Expression<Func<TEntity, bool>>? filter = null,
        CancellationToken cancellationToken = default);

    /// <summary>Projects a paged query straight into a DTO, avoiding entity materialisation.</summary>
    Task<PagedResult<TResult>> GetPagedAsync<TResult>(
        PagedRequest request,
        Expression<Func<TEntity, TResult>> selector,
        Expression<Func<TEntity, bool>>? filter = null,
        CancellationToken cancellationToken = default);

    /// <summary>True when at least one row matches.</summary>
    Task<bool> AnyAsync(
        Expression<Func<TEntity, bool>> predicate,
        CancellationToken cancellationToken = default);

    /// <summary>Number of rows matching the optional predicate.</summary>
    Task<int> CountAsync(
        Expression<Func<TEntity, bool>>? predicate = null,
        CancellationToken cancellationToken = default);

    // ---- Write ------------------------------------------------------------

    /// <summary>Stages an insert. Call <c>IUnitOfWork.SaveChangesAsync</c> to commit.</summary>
    Task AddAsync(TEntity entity, CancellationToken cancellationToken = default);

    /// <summary>Stages a batch insert.</summary>
    Task AddRangeAsync(IEnumerable<TEntity> entities, CancellationToken cancellationToken = default);

    /// <summary>Stages an update.</summary>
    void Update(TEntity entity);

    /// <summary>Stages a batch update.</summary>
    void UpdateRange(IEnumerable<TEntity> entities);

    /// <summary>
    /// Stages a delete. Auditable entities are soft-deleted by the context, so
    /// history and referential integrity survive.
    /// </summary>
    void Delete(TEntity entity);

    /// <summary>Stages a batch delete.</summary>
    void DeleteRange(IEnumerable<TEntity> entities);

    /// <summary>
    /// Composable query for the rare case where a caller needs to build a
    /// projection the interface does not cover. Deleted rows are already
    /// filtered out. Read-only: the result is not change-tracked.
    /// </summary>
    IQueryable<TEntity> Query();
}
