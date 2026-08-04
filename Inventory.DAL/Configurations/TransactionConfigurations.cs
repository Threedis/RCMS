using Inventory.Entities.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Inventory.DAL.Configurations;

/// <summary>Item master configuration.</summary>
public sealed class ItemConfiguration : IEntityTypeConfiguration<Item>
{
    public void Configure(EntityTypeBuilder<Item> builder)
    {
        builder.ToTable("Items");
        builder.HasKey(i => i.Id);

        builder.Property(i => i.ItemCode).IsRequired().HasMaxLength(40);
        builder.Property(i => i.ItemName).IsRequired().HasMaxLength(250);
        builder.Property(i => i.PartNumber).HasMaxLength(100);
        builder.Property(i => i.AlternatePartNumber).HasMaxLength(100);
        builder.Property(i => i.HsnCode).HasMaxLength(20);
        builder.Property(i => i.Description).HasMaxLength(1000);
        builder.Property(i => i.Specification).HasMaxLength(500);
        builder.Property(i => i.Barcode).HasMaxLength(100);
        builder.Property(i => i.ShelfLocation).HasMaxLength(50);

        builder.Property(i => i.ItemType).HasConversion<int>();

        // Quantities carry 4 decimals, money 2 - matching the SQL scripts.
        builder.Property(i => i.ReorderLevel).HasPrecision(18, 4);
        builder.Property(i => i.ReorderQuantity).HasPrecision(18, 4);
        builder.Property(i => i.MinimumStock).HasPrecision(18, 4);
        builder.Property(i => i.MaximumStock).HasPrecision(18, 4);
        builder.Property(i => i.StandardCost).HasPrecision(18, 4);
        builder.Property(i => i.AverageCost).HasPrecision(18, 4);
        builder.Property(i => i.TaxRate).HasPrecision(5, 2);

        builder.Property(i => i.IsActive).HasDefaultValue(true);
        builder.Property(i => i.IsDeleted).HasDefaultValue(false);
        builder.Property(i => i.CreatedOn).HasDefaultValueSql("SYSUTCDATETIME()");
        builder.Property(i => i.RowVersion).IsRowVersion();

        builder.HasOne(i => i.Category)
            .WithMany(c => c.Items)
            .HasForeignKey(i => i.CategoryId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(i => i.Unit)
            .WithMany(u => u.Items)
            .HasForeignKey(i => i.UnitId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(i => i.Manufacturer)
            .WithMany(m => m.Items)
            .HasForeignKey(i => i.ManufacturerId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(i => i.ItemCode)
            .IsUnique()
            .HasFilter("[IsDeleted] = 0")
            .HasDatabaseName("UX_Items_ItemCode");

        builder.HasIndex(i => i.Barcode)
            .IsUnique()
            .HasFilter("[Barcode] IS NOT NULL AND [IsDeleted] = 0")
            .HasDatabaseName("UX_Items_Barcode");

        builder.HasIndex(i => new { i.PartNumber, i.ManufacturerId })
            .HasDatabaseName("IX_Items_PartNumber_Manufacturer");

        builder.HasIndex(i => i.ItemName).HasDatabaseName("IX_Items_ItemName");
        builder.HasIndex(i => i.CategoryId).HasDatabaseName("IX_Items_CategoryId");
    }
}

/// <summary>Item image configuration.</summary>
public sealed class ItemImageConfiguration : IEntityTypeConfiguration<ItemImage>
{
    public void Configure(EntityTypeBuilder<ItemImage> builder)
    {
        builder.ToTable("ItemImages");
        builder.HasKey(i => i.Id);

        builder.Property(i => i.FileName).IsRequired().HasMaxLength(260);
        builder.Property(i => i.FilePath).IsRequired().HasMaxLength(500);
        builder.Property(i => i.ContentType).HasMaxLength(100);
        builder.Property(i => i.UploadedOn).HasDefaultValueSql("SYSUTCDATETIME()");

        builder.HasOne(i => i.Item)
            .WithMany(i => i.Images)
            .HasForeignKey(i => i.ItemId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(i => i.ItemId).HasDatabaseName("IX_ItemImages_ItemId");
    }
}

/// <summary>Goods Receipt Note header configuration.</summary>
public sealed class InventoryInwardHeaderConfiguration : IEntityTypeConfiguration<InventoryInwardHeader>
{
    public void Configure(EntityTypeBuilder<InventoryInwardHeader> builder)
    {
        builder.ToTable("InventoryInwardHeader");
        builder.HasKey(h => h.Id);

        builder.Property(h => h.GrnNumber).IsRequired().HasMaxLength(30);
        builder.Property(h => h.PurchaseOrderNumber).HasMaxLength(50);
        builder.Property(h => h.DeliveryChallanNumber).HasMaxLength(50);
        builder.Property(h => h.InvoiceNumber).HasMaxLength(50);
        builder.Property(h => h.ConsignmentNumber).HasMaxLength(50);
        builder.Property(h => h.ApprovalRemarks).HasMaxLength(1000);
        builder.Property(h => h.Remarks).HasMaxLength(1000);

        builder.Property(h => h.Status).HasConversion<int>();
        builder.Property(h => h.GrnDate).HasColumnType("date");
        builder.Property(h => h.PurchaseOrderDate).HasColumnType("date");
        builder.Property(h => h.DeliveryChallanDate).HasColumnType("date");
        builder.Property(h => h.InvoiceDate).HasColumnType("date");

        builder.Property(h => h.TotalValue).HasPrecision(18, 2);
        builder.Property(h => h.TotalTaxAmount).HasPrecision(18, 2);
        builder.Property(h => h.GrandTotal).HasPrecision(18, 2);

        builder.Property(h => h.IsActive).HasDefaultValue(true);
        builder.Property(h => h.IsDeleted).HasDefaultValue(false);
        builder.Property(h => h.CreatedOn).HasDefaultValueSql("SYSUTCDATETIME()");
        builder.Property(h => h.RowVersion).IsRowVersion();

        builder.HasOne(h => h.Vendor)
            .WithMany(v => v.Receipts)
            .HasForeignKey(h => h.VendorId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(h => h.Warehouse)
            .WithMany()
            .HasForeignKey(h => h.WarehouseId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(h => h.Courier)
            .WithMany()
            .HasForeignKey(h => h.CourierId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(h => h.GrnNumber)
            .IsUnique()
            .HasDatabaseName("UX_InventoryInwardHeader_GrnNumber");

        builder.HasIndex(h => new { h.GrnDate, h.Status })
            .HasDatabaseName("IX_InventoryInwardHeader_Date_Status");

        builder.HasIndex(h => h.VendorId).HasDatabaseName("IX_InventoryInwardHeader_VendorId");
    }
}

/// <summary>Goods Receipt Note detail configuration.</summary>
public sealed class InventoryInwardDetailConfiguration : IEntityTypeConfiguration<InventoryInwardDetail>
{
    public void Configure(EntityTypeBuilder<InventoryInwardDetail> builder)
    {
        builder.ToTable("InventoryInwardDetails");
        builder.HasKey(d => d.Id);

        builder.Property(d => d.PartNumber).HasMaxLength(100);
        builder.Property(d => d.RejectionReason).HasMaxLength(500);
        builder.Property(d => d.BatchNumber).HasMaxLength(50);
        builder.Property(d => d.SerialNumber).HasMaxLength(100);
        builder.Property(d => d.ShelfLocation).HasMaxLength(50);
        builder.Property(d => d.Remarks).HasMaxLength(500);

        builder.Property(d => d.ManufacturingDate).HasColumnType("date");
        builder.Property(d => d.ExpiryDate).HasColumnType("date");

        builder.Property(d => d.QuantityReceived).HasPrecision(18, 4);
        builder.Property(d => d.QuantityAccepted).HasPrecision(18, 4);
        builder.Property(d => d.QuantityRejected).HasPrecision(18, 4);
        builder.Property(d => d.StockBefore).HasPrecision(18, 4);
        builder.Property(d => d.StockAfter).HasPrecision(18, 4);
        builder.Property(d => d.UnitCost).HasPrecision(18, 4);
        builder.Property(d => d.DiscountPercent).HasPrecision(5, 2);
        builder.Property(d => d.TaxRate).HasPrecision(5, 2);
        builder.Property(d => d.TaxAmount).HasPrecision(18, 2);
        builder.Property(d => d.TotalCost).HasPrecision(18, 2);

        builder.HasOne(d => d.Header)
            .WithMany(h => h.Details)
            .HasForeignKey(d => d.InwardHeaderId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(d => d.Item)
            .WithMany(i => i.ReceiptLines)
            .HasForeignKey(d => d.ItemId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(d => d.InwardHeaderId).HasDatabaseName("IX_InventoryInwardDetails_HeaderId");
        builder.HasIndex(d => d.ItemId).HasDatabaseName("IX_InventoryInwardDetails_ItemId");
    }
}

/// <summary>Material Issue header configuration.</summary>
public sealed class InventoryOutwardHeaderConfiguration : IEntityTypeConfiguration<InventoryOutwardHeader>
{
    public void Configure(EntityTypeBuilder<InventoryOutwardHeader> builder)
    {
        builder.ToTable("InventoryOutwardHeader");
        builder.HasKey(h => h.Id);

        builder.Property(h => h.IssueNumber).IsRequired().HasMaxLength(30);
        builder.Property(h => h.ProjectName).HasMaxLength(200);
        builder.Property(h => h.WorkOrderNumber).HasMaxLength(50);
        builder.Property(h => h.Purpose).HasMaxLength(500);
        builder.Property(h => h.ApprovalRemarks).HasMaxLength(1000);
        builder.Property(h => h.TrackingNumber).HasMaxLength(50);
        builder.Property(h => h.ReceiverName).HasMaxLength(150);
        builder.Property(h => h.Remarks).HasMaxLength(1000);

        builder.Property(h => h.Status).HasConversion<int>();
        builder.Property(h => h.IssueDate).HasColumnType("date");
        builder.Property(h => h.DispatchDate).HasColumnType("date");

        builder.Property(h => h.TotalValue).HasPrecision(18, 2);

        builder.Property(h => h.IsActive).HasDefaultValue(true);
        builder.Property(h => h.IsDeleted).HasDefaultValue(false);
        builder.Property(h => h.CreatedOn).HasDefaultValueSql("SYSUTCDATETIME()");
        builder.Property(h => h.RowVersion).IsRowVersion();

        builder.HasOne(h => h.Warehouse)
            .WithMany()
            .HasForeignKey(h => h.WarehouseId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(h => h.Department)
            .WithMany(d => d.Issues)
            .HasForeignKey(h => h.DepartmentId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(h => h.Site)
            .WithMany()
            .HasForeignKey(h => h.SiteId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(h => h.Engineer)
            .WithMany(e => e.Issues)
            .HasForeignKey(h => h.EngineerId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(h => h.Courier)
            .WithMany()
            .HasForeignKey(h => h.CourierId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(h => h.IssueNumber)
            .IsUnique()
            .HasDatabaseName("UX_InventoryOutwardHeader_IssueNumber");

        builder.HasIndex(h => new { h.IssueDate, h.Status })
            .HasDatabaseName("IX_InventoryOutwardHeader_Date_Status");
    }
}

/// <summary>Material Issue detail configuration.</summary>
public sealed class InventoryOutwardDetailConfiguration : IEntityTypeConfiguration<InventoryOutwardDetail>
{
    public void Configure(EntityTypeBuilder<InventoryOutwardDetail> builder)
    {
        builder.ToTable("InventoryOutwardDetails");
        builder.HasKey(d => d.Id);

        builder.Property(d => d.PartNumber).HasMaxLength(100);
        builder.Property(d => d.BatchNumber).HasMaxLength(50);
        builder.Property(d => d.SerialNumber).HasMaxLength(100);
        builder.Property(d => d.Remarks).HasMaxLength(500);

        builder.Property(d => d.QuantityRequested).HasPrecision(18, 4);
        builder.Property(d => d.QuantityApproved).HasPrecision(18, 4);
        builder.Property(d => d.QuantityIssued).HasPrecision(18, 4);
        builder.Property(d => d.QuantityReserved).HasPrecision(18, 4);
        builder.Property(d => d.QuantityReturned).HasPrecision(18, 4);
        builder.Property(d => d.StockBefore).HasPrecision(18, 4);
        builder.Property(d => d.StockAfter).HasPrecision(18, 4);
        builder.Property(d => d.UnitCost).HasPrecision(18, 4);
        builder.Property(d => d.TotalCost).HasPrecision(18, 2);

        builder.HasOne(d => d.Header)
            .WithMany(h => h.Details)
            .HasForeignKey(d => d.OutwardHeaderId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(d => d.Item)
            .WithMany(i => i.IssueLines)
            .HasForeignKey(d => d.ItemId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(d => d.OutwardHeaderId).HasDatabaseName("IX_InventoryOutwardDetails_HeaderId");
        builder.HasIndex(d => d.ItemId).HasDatabaseName("IX_InventoryOutwardDetails_ItemId");
    }
}

/// <summary>Stock ledger configuration. The table is append-only.</summary>
public sealed class StockLedgerConfiguration : IEntityTypeConfiguration<StockLedger>
{
    public void Configure(EntityTypeBuilder<StockLedger> builder)
    {
        builder.ToTable("StockLedger");
        builder.HasKey(l => l.Id);

        builder.Property(l => l.MovementType).HasConversion<int>();
        builder.Property(l => l.DocumentType).HasConversion<int>();
        builder.Property(l => l.DocumentNumber).HasMaxLength(30);
        builder.Property(l => l.BatchNumber).HasMaxLength(50);
        builder.Property(l => l.SerialNumber).HasMaxLength(100);
        builder.Property(l => l.Remarks).HasMaxLength(500);
        builder.Property(l => l.TransactionDate).HasColumnType("date");

        builder.Property(l => l.InwardQuantity).HasPrecision(18, 4);
        builder.Property(l => l.OutwardQuantity).HasPrecision(18, 4);
        builder.Property(l => l.BalanceQuantity).HasPrecision(18, 4);
        builder.Property(l => l.UnitCost).HasPrecision(18, 4);
        builder.Property(l => l.BalanceAverageCost).HasPrecision(18, 4);
        builder.Property(l => l.Value).HasPrecision(18, 2);

        builder.Property(l => l.CreatedOn).HasDefaultValueSql("SYSUTCDATETIME()");

        builder.HasOne(l => l.Item)
            .WithMany(i => i.LedgerEntries)
            .HasForeignKey(l => l.ItemId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(l => l.Warehouse)
            .WithMany(w => w.LedgerEntries)
            .HasForeignKey(l => l.WarehouseId)
            .OnDelete(DeleteBehavior.Restrict);

        // The covering index behind every balance and ledger query.
        builder.HasIndex(l => new { l.ItemId, l.WarehouseId, l.TransactionDate, l.Id })
            .HasDatabaseName("IX_StockLedger_Item_Warehouse_Date");

        builder.HasIndex(l => new { l.DocumentType, l.DocumentId })
            .HasDatabaseName("IX_StockLedger_Document");

        builder.HasIndex(l => l.TransactionDate).HasDatabaseName("IX_StockLedger_TransactionDate");
    }
}
