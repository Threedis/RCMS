using Inventory.Common.Constants;
using Inventory.DAL.Context;
using Inventory.Entities.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Inventory.DAL.Seed;

/// <summary>
/// Creates the six application roles and the bootstrap administrator on first
/// run. Master data and demo transactions are seeded by the SQL scripts in
/// <c>Inventory.Database</c>; this class only covers what Identity owns, because
/// password hashes must be produced by the configured
/// <see cref="IPasswordHasher{TUser}"/> rather than hard-coded in SQL.
/// </summary>
public static class DbInitializer
{
    /// <summary>
    /// Applies pending migrations (optional) and seeds roles plus the default
    /// administrator. Safe to call on every application start: it is idempotent.
    /// </summary>
    /// <param name="context">Application context.</param>
    /// <param name="userManager">Identity user manager.</param>
    /// <param name="roleManager">Identity role manager.</param>
    /// <param name="logger">Destination for progress messages.</param>
    /// <param name="adminUserName">Login of the bootstrap administrator.</param>
    /// <param name="adminEmail">E-mail of the bootstrap administrator.</param>
    /// <param name="adminPassword">
    /// Initial password. Supply it from configuration/user-secrets; the account
    /// is created with <c>MustChangePassword</c> set so it cannot stay in use.
    /// </param>
    /// <param name="applyMigrations">Run <c>Database.MigrateAsync()</c> first.</param>
    public static async Task SeedAsync(
        ApplicationDbContext context,
        UserManager<ApplicationUser> userManager,
        RoleManager<ApplicationRole> roleManager,
        ILogger logger,
        string adminUserName,
        string adminEmail,
        string adminPassword,
        bool applyMigrations = false)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(userManager);
        ArgumentNullException.ThrowIfNull(roleManager);
        ArgumentNullException.ThrowIfNull(logger);

        if (applyMigrations)
        {
            logger.LogInformation("Applying pending Entity Framework migrations.");
            await context.Database.MigrateAsync().ConfigureAwait(false);
        }

        await SeedRolesAsync(roleManager, logger).ConfigureAwait(false);
        await SeedAdministratorAsync(userManager, logger, adminUserName, adminEmail, adminPassword)
            .ConfigureAwait(false);
        await SeedDocumentSequencesAsync(context, logger).ConfigureAwait(false);
    }

    private static async Task SeedRolesAsync(RoleManager<ApplicationRole> roleManager, ILogger logger)
    {
        var descriptions = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [Roles.Administrator] = "Full access including user management and system settings.",
            [Roles.InventoryManager] = "Manages master data, inventory documents and approvals.",
            [Roles.StoreExecutive] = "Records goods receipts and material issues.",
            [Roles.Approver] = "Approves or rejects inventory documents.",
            [Roles.ProjectManager] = "Raises material requests and tracks site consumption.",
            [Roles.Viewer] = "Read-only access to screens and reports."
        };

        foreach (var roleName in Roles.All)
        {
            if (await roleManager.RoleExistsAsync(roleName).ConfigureAwait(false))
            {
                continue;
            }

            var role = new ApplicationRole(roleName)
            {
                Description = descriptions.TryGetValue(roleName, out var text) ? text : null,
                IsSystemRole = true
            };

            var result = await roleManager.CreateAsync(role).ConfigureAwait(false);

            if (result.Succeeded)
            {
                logger.LogInformation("Created role {Role}.", roleName);
            }
            else
            {
                logger.LogError("Failed to create role {Role}: {Errors}",
                    roleName, string.Join("; ", result.Errors.Select(e => e.Description)));
            }
        }
    }

    private static async Task SeedAdministratorAsync(
        UserManager<ApplicationUser> userManager,
        ILogger logger,
        string userName,
        string email,
        string password)
    {
        if (string.IsNullOrWhiteSpace(userName) || string.IsNullOrWhiteSpace(password))
        {
            logger.LogWarning("Administrator seed skipped: no bootstrap credentials were configured.");
            return;
        }

        var existing = await userManager.FindByNameAsync(userName).ConfigureAwait(false);
        if (existing is not null)
        {
            return;
        }

        var admin = new ApplicationUser
        {
            UserName = userName,
            Email = email,
            EmailConfirmed = true,
            FullName = "System Administrator",
            EmployeeCode = "ADMIN",
            Designation = "Administrator",
            IsActive = true,
            // The bootstrap password is known to whoever deployed the system,
            // so force a change at first sign-in.
            MustChangePassword = true,
            PasswordChangedOn = DateTime.UtcNow
        };

        var created = await userManager.CreateAsync(admin, password).ConfigureAwait(false);

        if (!created.Succeeded)
        {
            logger.LogError("Failed to create the administrator account: {Errors}",
                string.Join("; ", created.Errors.Select(e => e.Description)));
            return;
        }

        await userManager.AddToRoleAsync(admin, Roles.Administrator).ConfigureAwait(false);
        logger.LogInformation("Created bootstrap administrator {UserName}.", userName);
    }

    private static async Task SeedDocumentSequencesAsync(ApplicationDbContext context, ILogger logger)
    {
        var year = DateTime.Today.Month >= 4 ? DateTime.Today.Year : DateTime.Today.Year - 1;
        var financialYear = $"{year}-{(year + 1) % 100:00}";

        var required = new[]
        {
            new DocumentSequence { SequenceKey = "GRN", Prefix = "GRN", FinancialYear = financialYear, PadWidth = 6 },
            new DocumentSequence { SequenceKey = "ISSUE", Prefix = "ISS", FinancialYear = financialYear, PadWidth = 6 },
            new DocumentSequence { SequenceKey = "ITEM", Prefix = "ITM", FinancialYear = null, PadWidth = 6 }
        };

        foreach (var sequence in required)
        {
            var exists = await context.DocumentSequences
                .AnyAsync(s => s.SequenceKey == sequence.SequenceKey && s.FinancialYear == sequence.FinancialYear)
                .ConfigureAwait(false);

            if (exists)
            {
                continue;
            }

            context.DocumentSequences.Add(sequence);
            logger.LogInformation("Created document sequence {Key} for {Year}.",
                sequence.SequenceKey, sequence.FinancialYear ?? "all years");
        }

        await context.SaveChangesAsync().ConfigureAwait(false);
    }
}
