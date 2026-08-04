using Inventory.Entities.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Inventory.DAL.Configurations;

/// <summary>Identity user configuration; adds the organisational columns.</summary>
public sealed class ApplicationUserConfiguration : IEntityTypeConfiguration<ApplicationUser>
{
    public void Configure(EntityTypeBuilder<ApplicationUser> builder)
    {
        builder.Property(u => u.FullName).IsRequired().HasMaxLength(150);
        builder.Property(u => u.EmployeeCode).HasMaxLength(30);
        builder.Property(u => u.Designation).HasMaxLength(150);
        builder.Property(u => u.IsActive).HasDefaultValue(true);
        builder.Property(u => u.IsDeleted).HasDefaultValue(false);
        builder.Property(u => u.CreatedOn).HasDefaultValueSql("SYSUTCDATETIME()");

        builder.HasOne(u => u.Department)
            .WithMany()
            .HasForeignKey(u => u.DepartmentId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(u => u.Site)
            .WithMany()
            .HasForeignKey(u => u.SiteId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(u => u.EmployeeCode)
            .IsUnique()
            .HasFilter("[EmployeeCode] IS NOT NULL AND [IsDeleted] = 0")
            .HasDatabaseName("UX_Users_EmployeeCode");
    }
}

/// <summary>Identity role configuration.</summary>
public sealed class ApplicationRoleConfiguration : IEntityTypeConfiguration<ApplicationRole>
{
    public void Configure(EntityTypeBuilder<ApplicationRole> builder)
    {
        builder.Property(r => r.Description).HasMaxLength(300);
        builder.Property(r => r.IsSystemRole).HasDefaultValue(false);
    }
}

/// <summary>Login history configuration.</summary>
public sealed class LoginHistoryConfiguration : IEntityTypeConfiguration<LoginHistory>
{
    public void Configure(EntityTypeBuilder<LoginHistory> builder)
    {
        builder.ToTable("LoginHistory");
        builder.HasKey(l => l.Id);

        builder.Property(l => l.UserName).IsRequired().HasMaxLength(256);
        builder.Property(l => l.FailureReason).HasMaxLength(300);
        builder.Property(l => l.IpAddress).HasMaxLength(45);
        builder.Property(l => l.UserAgent).HasMaxLength(400);
        builder.Property(l => l.SessionId).HasMaxLength(100);
        builder.Property(l => l.LoginOn).HasDefaultValueSql("SYSUTCDATETIME()");

        builder.HasOne(l => l.User)
            .WithMany(u => u.LoginHistories)
            .HasForeignKey(l => l.UserId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(l => new { l.UserName, l.LoginOn }).HasDatabaseName("IX_LoginHistory_User_Date");
    }
}

/// <summary>Approval history configuration. Rows are never updated.</summary>
public sealed class ApprovalHistoryConfiguration : IEntityTypeConfiguration<ApprovalHistory>
{
    public void Configure(EntityTypeBuilder<ApprovalHistory> builder)
    {
        builder.ToTable("ApprovalHistory");
        builder.HasKey(a => a.Id);

        builder.Property(a => a.DocumentType).HasConversion<int>();
        builder.Property(a => a.Action).HasConversion<int>();
        builder.Property(a => a.FromStatus).HasConversion<int>();
        builder.Property(a => a.ToStatus).HasConversion<int>();
        builder.Property(a => a.DocumentNumber).HasMaxLength(30);
        builder.Property(a => a.ActionByName).HasMaxLength(150);
        builder.Property(a => a.Remarks).HasMaxLength(1000);
        builder.Property(a => a.ActionOn).HasDefaultValueSql("SYSUTCDATETIME()");
        builder.Property(a => a.Level).HasDefaultValue(1);

        builder.HasIndex(a => new { a.DocumentType, a.DocumentId })
            .HasDatabaseName("IX_ApprovalHistory_Document");
    }
}

/// <summary>Attachment configuration.</summary>
public sealed class AttachmentConfiguration : IEntityTypeConfiguration<Attachment>
{
    public void Configure(EntityTypeBuilder<Attachment> builder)
    {
        builder.ToTable("Attachments");
        builder.HasKey(a => a.Id);

        builder.Property(a => a.DocumentType).HasConversion<int>();
        builder.Property(a => a.FileName).IsRequired().HasMaxLength(260);
        builder.Property(a => a.StoredFileName).IsRequired().HasMaxLength(100);
        builder.Property(a => a.FilePath).IsRequired().HasMaxLength(500);
        builder.Property(a => a.ContentType).HasMaxLength(100);
        builder.Property(a => a.Checksum).HasMaxLength(64);
        builder.Property(a => a.Description).HasMaxLength(300);
        builder.Property(a => a.UploadedOn).HasDefaultValueSql("SYSUTCDATETIME()");
        builder.Property(a => a.IsDeleted).HasDefaultValue(false);

        builder.HasOne(a => a.InwardHeader)
            .WithMany(h => h.Attachments)
            .HasForeignKey(a => a.InwardHeaderId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(a => a.OutwardHeader)
            .WithMany(h => h.Attachments)
            .HasForeignKey(a => a.OutwardHeaderId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(a => new { a.DocumentType, a.DocumentId })
            .HasDatabaseName("IX_Attachments_Document");
    }
}

/// <summary>Notification configuration.</summary>
public sealed class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> builder)
    {
        builder.ToTable("Notifications");
        builder.HasKey(n => n.Id);

        builder.Property(n => n.NotificationType).HasConversion<int>();
        builder.Property(n => n.DocumentType).HasConversion<int>();
        builder.Property(n => n.TargetRole).HasMaxLength(100);
        builder.Property(n => n.Title).IsRequired().HasMaxLength(200);
        builder.Property(n => n.Message).IsRequired().HasMaxLength(1000);
        builder.Property(n => n.ActionUrl).HasMaxLength(300);
        builder.Property(n => n.IsRead).HasDefaultValue(false);
        builder.Property(n => n.IsEmailSent).HasDefaultValue(false);
        builder.Property(n => n.CreatedOn).HasDefaultValueSql("SYSUTCDATETIME()");

        builder.HasOne(n => n.User)
            .WithMany(u => u.Notifications)
            .HasForeignKey(n => n.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(n => new { n.UserId, n.IsRead, n.CreatedOn })
            .HasDatabaseName("IX_Notifications_User_Unread");
    }
}

/// <summary>Audit log configuration. The table is append-only and heavily indexed by date.</summary>
public sealed class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> builder)
    {
        builder.ToTable("AuditLogs");
        builder.HasKey(a => a.Id);

        builder.Property(a => a.Action).HasConversion<int>();
        builder.Property(a => a.EntityName).HasMaxLength(100);
        builder.Property(a => a.EntityId).HasMaxLength(50);
        builder.Property(a => a.Description).HasMaxLength(500);
        builder.Property(a => a.UserName).HasMaxLength(256);
        builder.Property(a => a.IpAddress).HasMaxLength(45);
        builder.Property(a => a.UserAgent).HasMaxLength(400);
        builder.Property(a => a.Source).HasMaxLength(200);
        builder.Property(a => a.ErrorMessage).HasMaxLength(1000);
        builder.Property(a => a.IsSuccessful).HasDefaultValue(true);
        builder.Property(a => a.CreatedOn).HasDefaultValueSql("SYSUTCDATETIME()");

        builder.HasIndex(a => a.CreatedOn).HasDatabaseName("IX_AuditLogs_CreatedOn");
        builder.HasIndex(a => new { a.EntityName, a.EntityId }).HasDatabaseName("IX_AuditLogs_Entity");
        builder.HasIndex(a => a.UserId).HasDatabaseName("IX_AuditLogs_UserId");
    }
}

/// <summary>Document number sequence configuration.</summary>
public sealed class DocumentSequenceConfiguration : IEntityTypeConfiguration<DocumentSequence>
{
    public void Configure(EntityTypeBuilder<DocumentSequence> builder)
    {
        builder.ToTable("DocumentSequences");
        builder.HasKey(s => s.Id);

        builder.Property(s => s.SequenceKey).IsRequired().HasMaxLength(30);
        builder.Property(s => s.Prefix).HasMaxLength(20);
        builder.Property(s => s.FinancialYear).HasMaxLength(10);
        builder.Property(s => s.PadWidth).HasDefaultValue(6);
        builder.Property(s => s.ModifiedOn).HasDefaultValueSql("SYSUTCDATETIME()");

        builder.HasIndex(s => new { s.SequenceKey, s.FinancialYear })
            .IsUnique()
            .HasDatabaseName("UX_DocumentSequences_Key_Year");
    }
}
