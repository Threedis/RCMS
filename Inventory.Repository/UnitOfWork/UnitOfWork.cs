using System.Collections.Concurrent;
using Inventory.DAL.Context;
using Inventory.DAL.StoredProcedures;
using Inventory.Entities.Models;
using Inventory.Repository.Implementations;
using Inventory.Repository.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;

namespace Inventory.Repository.UnitOfWork;

/// <summary>
/// Default <see cref="IUnitOfWork"/>. Registered as a scoped service, so one
/// instance — and therefore one context, one connection and one transaction —
/// serves a whole HTTP request.
/// <para>
/// Repositories are created lazily and cached, which keeps construction cheap
/// for the many requests that touch only one or two of them.
/// </para>
/// </summary>
public sealed class UnitOfWork : IUnitOfWork
{
    private readonly ApplicationDbContext _context;
    private readonly IStoredProcedureExecutor _executor;
    private readonly ILoggerFactory _loggerFactory;
    private readonly ConcurrentDictionary<Type, object> _repositories = new();

    private IDbContextTransaction? _transaction;
    private bool _disposed;

    public UnitOfWork(
        ApplicationDbContext context,
        IStoredProcedureExecutor executor,
        ILoggerFactory loggerFactory)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _executor = executor ?? throw new ArgumentNullException(nameof(executor));
        _loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
    }

    // ---- Master repositories ---------------------------------------------

    public IMasterRepository<Category> Categories => GetOrAdd<IMasterRepository<Category>>(
        () => new MasterRepository<Category>(_context));

    public IMasterRepository<Unit> Units => GetOrAdd<IMasterRepository<Unit>>(
        () => new MasterRepository<Unit>(_context));

    public IMasterRepository<Department> Departments => GetOrAdd<IMasterRepository<Department>>(
        () => new MasterRepository<Department>(_context));

    public IMasterRepository<Site> Sites => GetOrAdd<IMasterRepository<Site>>(
        () => new MasterRepository<Site>(_context));

    public IWarehouseRepository Warehouses => GetOrAdd<IWarehouseRepository>(
        () => new WarehouseRepository(_context));

    public IMasterRepository<Manufacturer> Manufacturers => GetOrAdd<IMasterRepository<Manufacturer>>(
        () => new MasterRepository<Manufacturer>(_context));

    public IMasterRepository<Courier> Couriers => GetOrAdd<IMasterRepository<Courier>>(
        () => new MasterRepository<Courier>(_context));

    public IEngineerRepository Engineers => GetOrAdd<IEngineerRepository>(
        () => new EngineerRepository(_context));

    // ---- Domain repositories ---------------------------------------------

    public IItemRepository Items => GetOrAdd<IItemRepository>(
        () => new ItemRepository(_context, _executor));

    public IVendorRepository Vendors => GetOrAdd<IVendorRepository>(
        () => new VendorRepository(_context, _executor));

    public IInventoryRepository Inventory => GetOrAdd<IInventoryRepository>(
        () => new InventoryRepository(_executor));

    public IReportRepository Reports => GetOrAdd<IReportRepository>(
        () => new ReportRepository(_executor));

    public IDashboardRepository Dashboard => GetOrAdd<IDashboardRepository>(
        () => new DashboardRepository(_executor));

    public IAuditRepository Audit => GetOrAdd<IAuditRepository>(
        () => new AuditRepository(_context, _executor, _loggerFactory.CreateLogger<AuditRepository>()));

    public INotificationRepository Notifications => GetOrAdd<INotificationRepository>(
        () => new NotificationRepository(_context, _executor));

    public IAttachmentRepository Attachments => GetOrAdd<IAttachmentRepository>(
        () => new AttachmentRepository(_context));

    /// <inheritdoc />
    public IGenericRepository<TEntity> Repository<TEntity>() where TEntity : class
        => GetOrAdd<IGenericRepository<TEntity>>(() => new GenericRepository<TEntity>(_context));

    // ---- Transaction control ---------------------------------------------

    /// <inheritdoc />
    public bool HasActiveTransaction => _transaction is not null;

    /// <inheritdoc />
    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        => await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

    /// <inheritdoc />
    public async Task BeginTransactionAsync(CancellationToken cancellationToken = default)
    {
        // Reusing an open transaction lets one service call another without
        // either of them having to know who owns the boundary.
        if (_transaction is not null)
        {
            return;
        }

        _transaction = await _context.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task CommitAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            if (_transaction is not null)
            {
                await _transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            }
        }
        catch
        {
            await RollbackAsync(cancellationToken).ConfigureAwait(false);
            throw;
        }
        finally
        {
            await DisposeTransactionAsync().ConfigureAwait(false);
        }
    }

    /// <inheritdoc />
    public async Task RollbackAsync(CancellationToken cancellationToken = default)
    {
        if (_transaction is null)
        {
            return;
        }

        try
        {
            await _transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            await DisposeTransactionAsync().ConfigureAwait(false);
        }
    }

    private async Task DisposeTransactionAsync()
    {
        if (_transaction is null)
        {
            return;
        }

        await _transaction.DisposeAsync().ConfigureAwait(false);
        _transaction = null;
    }

    private TRepository GetOrAdd<TRepository>(Func<TRepository> factory) where TRepository : class
        => (TRepository)_repositories.GetOrAdd(typeof(TRepository), _ => factory());

    // ---- Disposal ---------------------------------------------------------

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _transaction?.Dispose();
        _context.Dispose();
        _repositories.Clear();
        _disposed = true;

        GC.SuppressFinalize(this);
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        await DisposeTransactionAsync().ConfigureAwait(false);
        await _context.DisposeAsync().ConfigureAwait(false);
        _repositories.Clear();
        _disposed = true;

        GC.SuppressFinalize(this);
    }
}
