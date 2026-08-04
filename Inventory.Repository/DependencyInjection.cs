using Inventory.DAL.StoredProcedures;
using Inventory.Repository.Implementations;
using Inventory.Repository.Interfaces;
using Inventory.Repository.UnitOfWork;
using Microsoft.Extensions.DependencyInjection;

namespace Inventory.Repository;

/// <summary>
/// Registers the data access layer with the dependency injection container.
/// Keeping registration next to the implementations means the web project
/// never has to know which concrete type serves which interface.
/// </summary>
public static class RepositoryServiceCollectionExtensions
{
    /// <summary>
    /// Adds the stored-procedure executor, the unit of work and every
    /// repository. All are scoped so a request shares one context and one
    /// transaction.
    /// </summary>
    public static IServiceCollection AddRepositoryLayer(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddScoped<IStoredProcedureExecutor, StoredProcedureExecutor>();
        services.AddScoped<IUnitOfWork, UnitOfWork.UnitOfWork>();

        // Repositories are also registered individually so a service that needs
        // exactly one of them can take it directly instead of the whole
        // unit of work.
        services.AddScoped(typeof(IGenericRepository<>), typeof(GenericRepository<>));
        services.AddScoped(typeof(IMasterRepository<>), typeof(MasterRepository<>));
        services.AddScoped<IWarehouseRepository, WarehouseRepository>();
        services.AddScoped<IEngineerRepository, EngineerRepository>();
        services.AddScoped<IItemRepository, ItemRepository>();
        services.AddScoped<IVendorRepository, VendorRepository>();
        services.AddScoped<IInventoryRepository, InventoryRepository>();
        services.AddScoped<IReportRepository, ReportRepository>();
        services.AddScoped<IDashboardRepository, DashboardRepository>();
        services.AddScoped<IAuditRepository, AuditRepository>();
        services.AddScoped<INotificationRepository, NotificationRepository>();
        services.AddScoped<IAttachmentRepository, AttachmentRepository>();

        return services;
    }
}
