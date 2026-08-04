using Inventory.Entities.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Inventory.DAL.Configurations;

/// <summary>
/// Shared Fluent API rules for the "code + name" masters. Every master gets the
/// same audit columns, the same unique code index and the same string lengths,
/// which keeps the generated schema identical to the hand-written SQL scripts.
/// </summary>
/// <typeparam name="TEntity">Concrete master entity.</typeparam>
public abstract class MasterEntityConfiguration<TEntity> : IEntityTypeConfiguration<TEntity>
    where TEntity : MasterEntity
{
    public virtual void Configure(EntityTypeBuilder<TEntity> builder)
    {
        builder.HasKey(e => e.Id);

        builder.Property(e => e.Code).IsRequired().HasMaxLength(30);
        builder.Property(e => e.Name).IsRequired().HasMaxLength(150);
        builder.Property(e => e.Description).HasMaxLength(500);

        builder.Property(e => e.IsActive).HasDefaultValue(true);
        builder.Property(e => e.IsDeleted).HasDefaultValue(false);
        builder.Property(e => e.CreatedOn).HasDefaultValueSql("SYSUTCDATETIME()");
        builder.Property(e => e.RowVersion).IsRowVersion();

        // A code is unique among the rows that are still alive; re-using the
        // code of a deleted record is deliberately allowed.
        builder.HasIndex(e => e.Code)
            .IsUnique()
            .HasFilter("[IsDeleted] = 0")
            .HasDatabaseName($"UX_{typeof(TEntity).Name}_Code");

        builder.HasIndex(e => e.Name)
            .HasDatabaseName($"IX_{typeof(TEntity).Name}_Name");
    }
}

/// <summary>Category configuration; adds the optional parent self-reference.</summary>
public sealed class CategoryConfiguration : MasterEntityConfiguration<Category>
{
    public override void Configure(EntityTypeBuilder<Category> builder)
    {
        builder.ToTable("Categories");
        base.Configure(builder);

        builder.HasOne(c => c.ParentCategory)
            .WithMany(c => c.SubCategories)
            .HasForeignKey(c => c.ParentCategoryId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

/// <summary>Unit of measure configuration.</summary>
public sealed class UnitConfiguration : MasterEntityConfiguration<Unit>
{
    public override void Configure(EntityTypeBuilder<Unit> builder)
    {
        builder.ToTable("Units");
        base.Configure(builder);

        builder.Property(u => u.Symbol).IsRequired().HasMaxLength(10);
        builder.Property(u => u.DecimalPlaces).HasDefaultValue(0);
    }
}

/// <summary>Department configuration.</summary>
public sealed class DepartmentConfiguration : MasterEntityConfiguration<Department>
{
    public override void Configure(EntityTypeBuilder<Department> builder)
    {
        builder.ToTable("Departments");
        base.Configure(builder);

        builder.Property(d => d.HeadOfDepartment).HasMaxLength(150);
        builder.Property(d => d.Email).HasMaxLength(150);
    }
}

/// <summary>Site configuration.</summary>
public sealed class SiteConfiguration : MasterEntityConfiguration<Site>
{
    public override void Configure(EntityTypeBuilder<Site> builder)
    {
        builder.ToTable("Sites");
        base.Configure(builder);

        builder.Property(s => s.Address).HasMaxLength(400);
        builder.Property(s => s.City).HasMaxLength(100);
        builder.Property(s => s.State).HasMaxLength(100);
        builder.Property(s => s.PinCode).HasMaxLength(20);
        builder.Property(s => s.ContactPerson).HasMaxLength(150);
        builder.Property(s => s.ContactNumber).HasMaxLength(20);
    }
}

/// <summary>Warehouse configuration; a warehouse always belongs to a site.</summary>
public sealed class WarehouseConfiguration : MasterEntityConfiguration<Warehouse>
{
    public override void Configure(EntityTypeBuilder<Warehouse> builder)
    {
        builder.ToTable("Warehouses");
        base.Configure(builder);

        builder.Property(w => w.InchargeName).HasMaxLength(150);
        builder.Property(w => w.ContactNumber).HasMaxLength(20);
        builder.Property(w => w.IsDefault).HasDefaultValue(false);

        builder.HasOne(w => w.Site)
            .WithMany(s => s.Warehouses)
            .HasForeignKey(w => w.SiteId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(w => w.SiteId).HasDatabaseName("IX_Warehouses_SiteId");
    }
}

/// <summary>Manufacturer configuration.</summary>
public sealed class ManufacturerConfiguration : MasterEntityConfiguration<Manufacturer>
{
    public override void Configure(EntityTypeBuilder<Manufacturer> builder)
    {
        builder.ToTable("Manufacturers");
        base.Configure(builder);

        builder.Property(m => m.Country).HasMaxLength(100);
        builder.Property(m => m.Website).HasMaxLength(200);
    }
}

/// <summary>Courier configuration.</summary>
public sealed class CourierConfiguration : MasterEntityConfiguration<Courier>
{
    public override void Configure(EntityTypeBuilder<Courier> builder)
    {
        builder.ToTable("Couriers");
        base.Configure(builder);

        builder.Property(c => c.ContactPerson).HasMaxLength(150);
        builder.Property(c => c.ContactNumber).HasMaxLength(20);
        builder.Property(c => c.TrackingUrlTemplate).HasMaxLength(300);
    }
}

/// <summary>Engineer configuration.</summary>
public sealed class EngineerConfiguration : MasterEntityConfiguration<Engineer>
{
    public override void Configure(EntityTypeBuilder<Engineer> builder)
    {
        builder.ToTable("Engineers");
        base.Configure(builder);

        builder.Property(e => e.EmployeeCode).HasMaxLength(30);
        builder.Property(e => e.Email).HasMaxLength(150);
        builder.Property(e => e.MobileNumber).HasMaxLength(20);
        builder.Property(e => e.Designation).HasMaxLength(150);

        builder.HasOne(e => e.Department)
            .WithMany(d => d.Engineers)
            .HasForeignKey(e => e.DepartmentId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(e => e.Site)
            .WithMany()
            .HasForeignKey(e => e.SiteId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}

/// <summary>Vendor configuration.</summary>
public sealed class VendorConfiguration : MasterEntityConfiguration<Vendor>
{
    public override void Configure(EntityTypeBuilder<Vendor> builder)
    {
        builder.ToTable("Vendors");
        base.Configure(builder);

        builder.Property(v => v.Address).HasMaxLength(400);
        builder.Property(v => v.City).HasMaxLength(100);
        builder.Property(v => v.State).HasMaxLength(100);
        builder.Property(v => v.PinCode).HasMaxLength(20);
        builder.Property(v => v.ContactPerson).HasMaxLength(150);
        builder.Property(v => v.ContactNumber).HasMaxLength(20);
        builder.Property(v => v.Email).HasMaxLength(150);
        builder.Property(v => v.GstNumber).HasMaxLength(20);
        builder.Property(v => v.PanNumber).HasMaxLength(15);
        builder.Property(v => v.BankName).HasMaxLength(100);
        builder.Property(v => v.BankAccountNumber).HasMaxLength(30);
        builder.Property(v => v.IfscCode).HasMaxLength(15);
        builder.Property(v => v.IsBlacklisted).HasDefaultValue(false);

        builder.HasIndex(v => v.GstNumber)
            .IsUnique()
            .HasFilter("[GstNumber] IS NOT NULL AND [IsDeleted] = 0")
            .HasDatabaseName("UX_Vendors_GstNumber");
    }
}
