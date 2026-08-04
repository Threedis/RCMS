using Inventory.Entities.Models;
using Inventory.Repository.Interfaces;

namespace Inventory.Repository.UnitOfWork;

/// <summary>
/// One transactional boundary over one <c>DbContext</c>.
/// <para>
/// Business services obtain every repository from here, so a whole use case
/// shares a single connection, a single change tracker and — when
/// <see cref="BeginTransactionAsync"/> is used — a single SQL transaction that
/// also encloses any stored-procedure work performed through the repositories.
/// </para>
/// </summary>
public interface IUnitOfWork : IAsyncDisposable, IDisposable
{
    // ---- Master repositories ---------------------------------------------
    IMasterRepository<Category> Categories { get; }

    IMasterRepository<Unit> Units { get; }

    IMasterRepository<Department> Departments { get; }

    IMasterRepository<Site> Sites { get; }

    IWarehouseRepository Warehouses { get; }

    IMasterRepository<Manufacturer> Manufacturers { get; }

    IMasterRepository<Courier> Couriers { get; }

    IEngineerRepository Engineers { get; }

    // ---- Domain repositories ---------------------------------------------
    IItemRepository Items { get; }

    IVendorRepository Vendors { get; }

    IInventoryRepository Inventory { get; }

    IReportRepository Reports { get; }

    IDashboardRepository Dashboard { get; }

    IAuditRepository Audit { get; }

    INotificationRepository Notifications { get; }

    IAttachmentRepository Attachments { get; }

    /// <summary>Escape hatch for entities without a dedicated repository.</summary>
    IGenericRepository<TEntity> Repository<TEntity>() where TEntity : class;

    // ---- Transaction control ---------------------------------------------

    /// <summary>Flushes all pending Entity Framework changes.</summary>
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Starts an explicit transaction. Calls are idempotent: if one is already
    /// open, the existing transaction is reused so nested services compose.
    /// </summary>
    Task BeginTransactionAsync(CancellationToken cancellationToken = default);

    /// <summary>Saves pending changes and commits the ambient transaction.</summary>
    Task CommitAsync(CancellationToken cancellationToken = default);

    /// <summary>Discards the ambient transaction.</summary>
    Task RollbackAsync(CancellationToken cancellationToken = default);

    /// <summary>True while an explicit transaction is open.</summary>
    bool HasActiveTransaction { get; }
}
