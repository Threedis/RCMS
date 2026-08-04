using Inventory.BLL.Interfaces;
using Inventory.BLL.Mapping;
using Inventory.BLL.Services;
using Inventory.Entities.Dtos;
using Inventory.Entities.Models;
using Microsoft.Extensions.DependencyInjection;

namespace Inventory.BLL;

/// <summary>Registers the business logic layer with the dependency injection container.</summary>
public static class BusinessServiceCollectionExtensions
{
    /// <summary>
    /// Adds AutoMapper and every business service. All are scoped because they
    /// depend on the request-scoped unit of work.
    /// </summary>
    public static IServiceCollection AddBusinessLayer(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddAutoMapper(typeof(MappingProfile).Assembly);

        // ---- Master services ----------------------------------------------
        // Each concrete service supplies its own MasterDefinition, so the shared
        // generic implementation needs no extra registration.
        services.AddScoped<IMasterService<CategoryDto>, CategoryService>();
        services.AddScoped<IMasterService<UnitDto>, UnitService>();
        services.AddScoped<IMasterService<DepartmentDto>, DepartmentService>();
        services.AddScoped<IMasterService<SiteDto>, SiteService>();
        services.AddScoped<IMasterService<WarehouseDto>, WarehouseService>();
        services.AddScoped<IMasterService<ManufacturerDto>, ManufacturerService>();
        services.AddScoped<IMasterService<CourierDto>, CourierService>();
        services.AddScoped<IMasterService<EngineerDto>, EngineerService>();

        // ---- Domain services ----------------------------------------------
        services.AddScoped<IItemService, ItemService>();
        services.AddScoped<IVendorService, VendorService>();
        services.AddScoped<IGrnService, GrnService>();
        services.AddScoped<IIssueService, IssueService>();
        services.AddScoped<IApprovalService, ApprovalService>();
        services.AddScoped<IStockService, StockService>();
        services.AddScoped<IDashboardService, DashboardService>();
        services.AddScoped<IReportService, ReportService>();
        services.AddScoped<INotificationService, NotificationService>();
        services.AddScoped<IAuditService, AuditService>();
        services.AddScoped<IAttachmentService, AttachmentService>();
        services.AddScoped<IUserService, UserService>();
        services.AddScoped<ILookupService, LookupService>();

        return services;
    }
}
