using System.Globalization;
using Inventory.BLL;
using Inventory.Common.Configuration;
using Inventory.Common.Constants;
using Inventory.Common.Security;
using Inventory.DAL.Context;
using Inventory.DAL.Seed;
using Inventory.Entities.Models;
using Inventory.Repository;
using Inventory.Services;
using Inventory.Web.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Net.Http.Headers;
using Serilog;
using Serilog.Events;

// -----------------------------------------------------------------------------
// Bootstrap logger: captures failures that happen before the host is built.
// -----------------------------------------------------------------------------
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    Log.Information("Starting the Enterprise Inventory Management System.");

    var builder = WebApplication.CreateBuilder(args);

    // -------------------------------------------------------------------------
    // Logging: Serilog to console, rolling file and SQL Server
    // -------------------------------------------------------------------------
    builder.Host.UseSerilog((context, services, configuration) => configuration
        .ReadFrom.Configuration(context.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext()
        .Enrich.WithMachineName()
        .Enrich.WithThreadId()
        .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
        .MinimumLevel.Override("Microsoft.EntityFrameworkCore.Database.Command", LogEventLevel.Warning));

    // -------------------------------------------------------------------------
    // Configuration binding
    // -------------------------------------------------------------------------
    builder.Services.Configure<ApplicationSettings>(
        builder.Configuration.GetSection(ApplicationSettings.SectionName));
    builder.Services.Configure<EmailSettings>(
        builder.Configuration.GetSection(EmailSettings.SectionName));
    builder.Services.Configure<FileStorageSettings>(
        builder.Configuration.GetSection(FileStorageSettings.SectionName));

    var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
        ?? throw new InvalidOperationException(
            "ConnectionStrings:DefaultConnection is not configured. " +
            "Set it in appsettings.json, user secrets or an environment variable.");

    // -------------------------------------------------------------------------
    // Data access
    // -------------------------------------------------------------------------
    builder.Services.AddDbContext<ApplicationDbContext>((serviceProvider, options) =>
    {
        options.UseSqlServer(connectionString, sql =>
        {
            sql.EnableRetryOnFailure(
                maxRetryCount: 3,
                maxRetryDelay: TimeSpan.FromSeconds(5),
                errorNumbersToAdd: null);
            sql.CommandTimeout(60);
            sql.MigrationsAssembly(typeof(ApplicationDbContext).Assembly.FullName);
        });

        if (builder.Environment.IsDevelopment())
        {
            options.EnableDetailedErrors();
            // Never enable sensitive data logging outside development: it writes
            // parameter values, including personal data, to the log.
            options.EnableSensitiveDataLogging();
        }
    });

    // -------------------------------------------------------------------------
    // Identity
    // -------------------------------------------------------------------------
    builder.Services
        .AddIdentity<ApplicationUser, ApplicationRole>(options =>
        {
            options.Password.RequireDigit = true;
            options.Password.RequireLowercase = true;
            options.Password.RequireUppercase = true;
            options.Password.RequireNonAlphanumeric = true;
            options.Password.RequiredLength = 8;
            options.Password.RequiredUniqueChars = 4;

            options.Lockout.MaxFailedAccessAttempts = AppConstants.MaxFailedLoginAttempts;
            options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(AppConstants.LockoutMinutes);
            options.Lockout.AllowedForNewUsers = true;

            options.User.RequireUniqueEmail = true;
            options.SignIn.RequireConfirmedAccount = false;
        })
        .AddEntityFrameworkStores<ApplicationDbContext>()
        .AddDefaultTokenProviders();

    builder.Services.ConfigureApplicationCookie(options =>
    {
        options.LoginPath = "/Account/Login";
        options.LogoutPath = "/Account/Logout";
        options.AccessDeniedPath = "/Account/AccessDenied";
        options.ExpireTimeSpan = TimeSpan.FromMinutes(AppConstants.SessionTimeoutMinutes);
        options.SlidingExpiration = true;
        options.Cookie.Name = "Inventory.Auth";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    });

    // Re-issue the principal every 30 minutes so a role or lockout change takes
    // effect without waiting for the cookie to expire.
    builder.Services.Configure<SecurityStampValidatorOptions>(options =>
        options.ValidationInterval = TimeSpan.FromMinutes(30));

    // -------------------------------------------------------------------------
    // Authorization policies
    // -------------------------------------------------------------------------
    builder.Services.AddAuthorization(options =>
    {
        options.AddPolicy(Policies.AdministratorOnly, policy =>
            policy.RequireRole(Roles.Administrator));

        options.AddPolicy(Policies.ManageMasters, policy =>
            policy.RequireRole(Roles.CanManageMasters));

        options.AddPolicy(Policies.ManageInventory, policy =>
            policy.RequireRole(Roles.CanTransact));

        options.AddPolicy(Policies.ApproveDocuments, policy =>
            policy.RequireRole(Roles.CanApprove));

        options.AddPolicy(Policies.ViewReports, policy =>
            policy.RequireRole(Roles.All));

        options.AddPolicy(Policies.ReadOnly, policy =>
            policy.RequireAuthenticatedUser());

        // Nothing is anonymous unless a controller opts out with [AllowAnonymous].
        options.FallbackPolicy = new AuthorizationPolicyBuilder()
            .RequireAuthenticatedUser()
            .Build();
    });

    // -------------------------------------------------------------------------
    // Application layers
    // -------------------------------------------------------------------------
    builder.Services.AddHttpContextAccessor();
    builder.Services.AddScoped<ICurrentUser, CurrentUser>();
    builder.Services.AddRepositoryLayer();
    builder.Services.AddInfrastructureServices();
    builder.Services.AddBusinessLayer();

    // -------------------------------------------------------------------------
    // MVC
    // -------------------------------------------------------------------------
    builder.Services.AddControllersWithViews(options =>
    {
        // Every non-GET request must carry a valid anti-forgery token.
        options.Filters.Add(new AutoValidateAntiforgeryTokenAttribute());
        options.Filters.Add<GlobalExceptionFilter>();
        options.Filters.Add<AuditActionFilter>();
        options.Filters.Add<PasswordExpiryFilter>();
    });

    builder.Services.AddAntiforgery(options =>
    {
        options.HeaderName = "RequestVerificationToken";
        options.Cookie.Name = "Inventory.Antiforgery";
        options.Cookie.HttpOnly = true;
        options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    });

    builder.Services.AddResponseCompression(options =>
    {
        options.EnableForHttps = true;
        options.MimeTypes = new[]
        {
            "text/plain", "text/css", "application/javascript",
            "text/html", "application/xml", "text/xml",
            "application/json", "text/json", "image/svg+xml"
        };
    });

    builder.Services.AddOutputCache(options =>
    {
        // Only genuinely static, non-personal responses are cached.
        options.AddPolicy("Lookups", policy => policy.Expire(TimeSpan.FromMinutes(5)));
    });

    builder.Services.AddSession(options =>
    {
        options.IdleTimeout = TimeSpan.FromMinutes(AppConstants.SessionTimeoutMinutes);
        options.Cookie.Name = "Inventory.Session";
        options.Cookie.HttpOnly = true;
        options.Cookie.IsEssential = true;
        options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    });

    builder.Services.AddDataProtection()
        .SetApplicationName("InventoryManagement");

    builder.Services.Configure<FormOptions>(options =>
    {
        options.MultipartBodyLengthLimit = AppConstants.MaxUploadSizeBytes;
    });

    builder.Services.AddHealthChecks()
        .AddDbContextCheck<ApplicationDbContext>("database");

    var app = builder.Build();

    // -------------------------------------------------------------------------
    // Culture: one culture everywhere so money and dates never vary by browser.
    // -------------------------------------------------------------------------
    var culture = CultureInfo.GetCultureInfo(AppConstants.DefaultCulture);
    app.UseRequestLocalization(new RequestLocalizationOptions
    {
        DefaultRequestCulture = new RequestCulture(culture),
        SupportedCultures = new[] { culture },
        SupportedUICultures = new[] { culture }
    });

    // -------------------------------------------------------------------------
    // Pipeline
    // -------------------------------------------------------------------------
    if (app.Environment.IsDevelopment())
    {
        app.UseDeveloperExceptionPage();
    }
    else
    {
        app.UseExceptionHandler("/Home/Error");
        // Six months of HSTS; browsers refuse plain HTTP for the host afterwards.
        app.UseHsts();
    }

    app.UseStatusCodePagesWithReExecute("/Home/StatusCode", "?code={0}");
    app.UseHttpsRedirection();
    app.UseResponseCompression();

    app.UseStaticFiles(new StaticFileOptions
    {
        OnPrepareResponse = context =>
        {
            // Static assets are versioned by asp-append-version, so they can be
            // cached hard.
            context.Context.Response.Headers[HeaderNames.CacheControl] = "public,max-age=604800";
        }
    });

    app.UseMiddleware<SecurityHeadersMiddleware>();
    app.UseRouting();
    app.UseSession();
    app.UseAuthentication();
    app.UseAuthorization();
    app.UseOutputCache();
    app.UseSerilogRequestLogging(options =>
        options.GetLevel = (httpContext, elapsed, ex) =>
            ex is not null ? LogEventLevel.Error
            : httpContext.Response.StatusCode >= 500 ? LogEventLevel.Error
            : elapsed > 3000 ? LogEventLevel.Warning
            : LogEventLevel.Debug);

    app.MapControllerRoute(
        name: "areas",
        pattern: "{area:exists}/{controller=Home}/{action=Index}/{id?}");

    app.MapControllerRoute(
        name: "default",
        pattern: "{controller=Dashboard}/{action=Index}/{id?}");

    app.MapHealthChecks("/health").AllowAnonymous();

    // -------------------------------------------------------------------------
    // First-run seeding: roles, the bootstrap administrator, number sequences
    // -------------------------------------------------------------------------
    await SeedDatabaseAsync(app);

    app.Run();
}
catch (Exception ex) when (ex is not HostAbortedException)
{
    Log.Fatal(ex, "The application terminated unexpectedly.");
    throw;
}
finally
{
    await Log.CloseAndFlushAsync();
}

/// <summary>
/// Applies migrations (when configured) and seeds the data that Identity owns.
/// Master data comes from the SQL scripts in Inventory.Database.
/// </summary>
static async Task SeedDatabaseAsync(WebApplication app)
{
    using var scope = app.Services.CreateScope();
    var services = scope.ServiceProvider;
    var logger = services.GetRequiredService<ILogger<Program>>();

    try
    {
        var context = services.GetRequiredService<ApplicationDbContext>();
        var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
        var roleManager = services.GetRequiredService<RoleManager<ApplicationRole>>();
        var configuration = services.GetRequiredService<IConfiguration>();

        await DbInitializer.SeedAsync(
            context,
            userManager,
            roleManager,
            logger,
            adminUserName: configuration["Seed:AdminUserName"] ?? "admin",
            adminEmail: configuration["Seed:AdminEmail"] ?? "admin@example.com",
            // Supplied through user secrets in development and an environment
            // variable or Key Vault in production; never committed.
            adminPassword: configuration["Seed:AdminPassword"] ?? string.Empty,
            applyMigrations: configuration.GetValue("Seed:ApplyMigrations", false));
    }
    catch (Exception ex)
    {
        // A seeding failure must not stop an otherwise healthy application from
        // starting; it is logged loudly instead.
        logger.LogError(ex, "Database seeding failed. The application will start regardless.");
    }
}

/// <summary>Exposed so the integration tests can reference the entry point assembly.</summary>
public partial class Program
{
}
