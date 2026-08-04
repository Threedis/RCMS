using Inventory.Common.Security;
using Inventory.Entities.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Inventory.DAL.Context;

/// <summary>
/// Entity Framework Core context for the whole application.
/// <para>
/// The context is responsible for the object model, Identity storage, schema
/// migrations and the connection/transaction that the stored-procedure executor
/// enlists in. Business reads and writes go through stored procedures; the
/// context's own change tracker is used for Identity, lookups and navigation
/// loading.
/// </para>
/// </summary>
public class ApplicationDbContext
    : IdentityDbContext<ApplicationUser, ApplicationRole, int,
        IdentityUserClaim<int>, IdentityUserRole<int>, IdentityUserLogin<int>,
        IdentityRoleClaim<int>, IdentityUserToken<int>>
{
    private readonly ICurrentUser _currentUser;

    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : this(options, SystemUser.Instance)
    {
    }

    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options, ICurrentUser currentUser)
        : base(options)
    {
        _currentUser = currentUser ?? SystemUser.Instance;
    }

    // ---- Master data ------------------------------------------------------
    public DbSet<Category> Categories => Set<Category>();

    public DbSet<Unit> Units => Set<Unit>();

    public DbSet<Department> Departments => Set<Department>();

    public DbSet<Site> Sites => Set<Site>();

    public DbSet<Warehouse> Warehouses => Set<Warehouse>();

    public DbSet<Manufacturer> Manufacturers => Set<Manufacturer>();

    public DbSet<Courier> Couriers => Set<Courier>();

    public DbSet<Engineer> Engineers => Set<Engineer>();

    public DbSet<Vendor> Vendors => Set<Vendor>();

    public DbSet<Item> Items => Set<Item>();

    public DbSet<ItemImage> ItemImages => Set<ItemImage>();

    // ---- Transactions -----------------------------------------------------
    public DbSet<InventoryInwardHeader> InventoryInwardHeaders => Set<InventoryInwardHeader>();

    public DbSet<InventoryInwardDetail> InventoryInwardDetails => Set<InventoryInwardDetail>();

    public DbSet<InventoryOutwardHeader> InventoryOutwardHeaders => Set<InventoryOutwardHeader>();

    public DbSet<InventoryOutwardDetail> InventoryOutwardDetails => Set<InventoryOutwardDetail>();

    public DbSet<StockLedger> StockLedgers => Set<StockLedger>();

    // ---- Workflow / system ------------------------------------------------
    public DbSet<ApprovalHistory> ApprovalHistories => Set<ApprovalHistory>();

    public DbSet<Attachment> Attachments => Set<Attachment>();

    public DbSet<Notification> Notifications => Set<Notification>();

    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    public DbSet<LoginHistory> LoginHistories => Set<LoginHistory>();

    public DbSet<DocumentSequence> DocumentSequences => Set<DocumentSequence>();

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // Rename the Identity tables to the names used by the SQL scripts.
        builder.Entity<ApplicationUser>().ToTable("Users");
        builder.Entity<ApplicationRole>().ToTable("Roles");
        builder.Entity<IdentityUserRole<int>>().ToTable("UserRoles");
        builder.Entity<IdentityUserClaim<int>>().ToTable("UserClaims");
        builder.Entity<IdentityUserLogin<int>>().ToTable("UserLogins");
        builder.Entity<IdentityRoleClaim<int>>().ToTable("RoleClaims");
        builder.Entity<IdentityUserToken<int>>().ToTable("UserTokens");

        // Pick up every IEntityTypeConfiguration<T> in this assembly.
        builder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);
    }

    /// <inheritdoc />
    public override int SaveChanges()
        => SaveChangesAsync().GetAwaiter().GetResult();

    /// <summary>
    /// Stamps the audit columns before delegating to the base implementation,
    /// and converts hard deletes of auditable entities into soft deletes.
    /// </summary>
    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        ApplyAuditInformation();
        return await base.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    private void ApplyAuditInformation()
    {
        var now = DateTime.UtcNow;
        var userId = _currentUser.UserId;

        foreach (var entry in ChangeTracker.Entries<AuditableEntity>())
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    entry.Entity.CreatedOn = now;
                    entry.Entity.CreatedBy = userId;
                    break;

                case EntityState.Modified:
                    entry.Entity.ModifiedOn = now;
                    entry.Entity.ModifiedBy = userId;
                    entry.Property(nameof(AuditableEntity.CreatedOn)).IsModified = false;
                    entry.Property(nameof(AuditableEntity.CreatedBy)).IsModified = false;
                    break;

                case EntityState.Deleted:
                    // Nothing is physically removed: history and referential
                    // integrity must survive a "delete".
                    entry.State = EntityState.Modified;
                    entry.Entity.IsDeleted = true;
                    entry.Entity.IsActive = false;
                    entry.Entity.ModifiedOn = now;
                    entry.Entity.ModifiedBy = userId;
                    break;

                case EntityState.Detached:
                case EntityState.Unchanged:
                default:
                    break;
            }
        }
    }
}
