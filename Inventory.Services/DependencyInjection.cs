using Inventory.Services.Caching;
using Inventory.Services.Email;
using Inventory.Services.Export;
using Inventory.Services.Files;
using Inventory.Services.Import;
using Microsoft.Extensions.DependencyInjection;

namespace Inventory.Services;

/// <summary>Registers the cross-cutting infrastructure services.</summary>
public static class ServicesServiceCollectionExtensions
{
    /// <summary>
    /// Adds document generation, file storage, e-mail and caching.
    /// These services hold no request state, so they are registered as
    /// singletons except where they depend on scoped collaborators.
    /// </summary>
    public static IServiceCollection AddInfrastructureServices(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        // A bounded cache: entries declare Size = 1, so this is a hard cap on
        // the number of cached lookups rather than an unbounded memory sink.
        services.AddMemoryCache(options => options.SizeLimit = 2048);

        services.AddSingleton<ICacheService, MemoryCacheService>();
        services.AddSingleton<IExportService, ExportService>();
        services.AddSingleton<IExcelImportReader, ExcelImportReader>();
        services.AddSingleton<IFileStorageService, FileStorageService>();
        services.AddSingleton<IEmailSender, SmtpEmailSender>();

        return services;
    }
}
