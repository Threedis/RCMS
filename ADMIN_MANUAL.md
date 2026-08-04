# Administrator Manual - Enterprise Inventory Management System

## Overview

This manual covers administrative tasks, configuration, user management, system monitoring, and troubleshooting for the RCMS system.

## Table of Contents
1. [User Administration](#user-administration)
2. [Role Management](#role-management)
3. [System Configuration](#system-configuration)
4. [Monitoring & Maintenance](#monitoring--maintenance)
5. [Data Management](#data-management)
6. [Troubleshooting](#troubleshooting)

## User Administration

### Creating a New User

1. Navigate to **Administration > Users**
2. Click **New User** button
3. Fill in user details:
   - **Username**: Unique login identifier (alphanumeric, no spaces)
   - **Email**: Valid email address for password reset
   - **Full Name**: Display name for audit logs
   - **Employee Code**: Optional employee ID
4. Select one or more roles (at least one required):
   - Administrator: Full system access
   - Inventory Manager: Create/approve GRN and Issue documents
   - Store Executive: Create GRN documents (limited approval)
   - Approver: Approve GRN and Issue documents
   - Project Manager: Create/manage issue documents
   - Viewer: Read-only access to all screens
5. Assign Department (optional, for filtering)
6. Check **Active** to enable the user
7. Click **Save**

**System-Generated Password**: User receives temporary password via email. They must change it on first login.

### Editing User Details

1. Navigate to **Administration > Users**
2. Click the **Edit** icon for the desired user
3. Modify details as needed
4. Update role assignments if required
5. Click **Save**

### Resetting User Password

1. Navigate to **Administration > Users**
2. Click the **Reset Password** (key) icon
3. Confirm the action
4. System generates temporary password and sends to user email
5. User must change password on next login

### Deactivating a User

1. Navigate to **Administration > Users**
2. Click the **Deactivate** (lock) icon
3. Confirm the action
4. User can no longer login but all historical data is preserved
5. To reactivate: Edit user and check **Active** checkbox

## Role Management

### Understanding Roles

| Role | GRN Create | GRN Approve | Issue Create | Issue Approve | Master Edit | User Admin | View Reports |
|------|-----------|-------------|-------------|---------------|------------|-----------|--------------|
| Administrator | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ |
| Inventory Manager | ✓ | - | ✓ | - | ✓ | - | ✓ |
| Store Executive | ✓ | - | - | - | - | - | Limited |
| Approver | - | ✓ | - | ✓ | - | - | Limited |
| Project Manager | - | - | ✓ | - | - | - | Limited |
| Viewer | - | - | - | - | - | - | View Only |

### Assigning Multiple Roles

Users can have multiple roles (e.g., Inventory Manager + Approver). Role intersection determines final permissions.

Example: User with both "Inventory Manager" and "Approver" can:
- Create and approve GRN documents
- Create issue documents
- Edit master data

## System Configuration

### Database Connection

**File**: `appsettings.Production.json`

```json
"ConnectionStrings": {
  "DefaultConnection": "Server=YOUR_SERVER;Database=RCMS;User Id=rcms_app;Password=YourPassword;"
}
```

**Verify Connection**:
1. Application Home page should load without database errors
2. Dashboard should display current KPIs
3. Check System > Health endpoint returns "Healthy"

### Email Configuration

**File**: `appsettings.Production.json`

```json
"Email": {
  "SmtpServer": "smtp.gmail.com",
  "SmtpPort": 587,
  "SmtpUsername": "your-email@gmail.com",
  "SmtpPassword": "your-app-password",
  "SmtpEnableSsl": true,
  "FromAddress": "noreply@company.com"
}
```

**Test Email**:
1. Go to **Administration > Settings** (if available)
2. Click "Send Test Email"
3. Check email inbox for test message

**Common Issues**:
- Gmail: Use [App Passwords](https://support.google.com/accounts/answer/185833) instead of account password
- Office 365: Ensure "Allow less secure apps" is enabled
- Corporate SMTP: Verify with IT department

### Session & Timeout

**File**: `appsettings.Production.json`

```json
"Authentication": {
  "SessionTimeoutMinutes": 30,
  "PasswordExpiryDays": 90,
  "LockoutThreshold": 5,
  "LockoutDurationMinutes": 30
}
```

**Effects**:
- SessionTimeoutMinutes: User is logged out after inactivity
- PasswordExpiryDays: Users forced to change password
- LockoutThreshold: Account locked after failed login attempts

### File Storage

**Path**: Configured in `appsettings.Production.json`

```json
"FileStorage": {
  "BasePath": "C:\\AppData\\RCMS\\Files",
  "MaxFileSizeMB": 50,
  "AllowedExtensions": ".xlsx,.xls,.pdf,.jpg,.png"
}
```

**Permissions**: Ensure IIS app pool has full (Modify) permissions on this directory.

## Monitoring & Maintenance

### Audit Log Review

1. Navigate to **Administration > Audit Log**
2. Use date filters to narrow search
3. Click on any record to view details:
   - User who performed action
   - Entity and action type
   - Specific field changes
   - IP address and user agent
   - Timestamp

**Alert**: Any unauthorized access attempts or suspicious activity should be investigated immediately.

### System Health

**Health Endpoint**: `https://yourdomain.com/health`

Returns:
```json
{
  "status": "Healthy",
  "checks": {
    "database": "Healthy",
    "cache": "Healthy",
    "fileStorage": "Healthy"
  }
}
```

### Log Files Review

**Location**: `C:\Logs\RCMS\` (or configured path)

**Daily Rotation**: `log-20240104.txt`, `log-20240105.txt`, etc.

**Log Levels**:
- ERROR: Critical issues requiring immediate attention
- WARNING: Potentially problematic situations
- INFORMATION: Normal operational events

**Search Logs**:
```powershell
# Search for errors on specific date
Get-Content "C:\Logs\RCMS\log-20240104.txt" | Select-String "ERROR"

# Search across multiple days
Get-ChildItem "C:\Logs\RCMS\log-*.txt" | xargs grep "ERROR"
```

### Database Maintenance

**Automatic Jobs** (Run via SQL Agent):
- Index Maintenance: Weekly (Sunday 2 AM)
- Audit Log Purge: Monthly (1st day, 1 AM)
- Low Stock Alerts: Daily (7 AM)

**Manual Maintenance**:
```sql
-- Run index defragmentation
EXEC sp_MaintainIndexes;

-- Check table integrity
DBCC CHECKDB (RCMS);

-- Purge old audit logs (older than 1 year)
DELETE FROM AuditLog 
WHERE ChangedOn < DATEADD(YEAR, -1, GETDATE());
```

### Backup Verification

**Backup Location**: `C:\SQLBackups\`

**Backup Schedule**:
- Full: Daily 2 AM
- Differential: Every 6 hours
- Transaction Log: Every 15 minutes

**Test Restore** (Monthly):
```sql
-- List available backups
RESTORE HEADERONLY FROM DISK = 'C:\SQLBackups\RCMS_20240104_backup.bak';

-- Test restore to alternate database
RESTORE DATABASE RCMS_Test FROM DISK = 'C:\SQLBackups\RCMS_20240104_backup.bak'
WITH REPLACE, NORECOVERY;

-- Verify data
USE RCMS_Test;
SELECT COUNT(*) FROM InventoryInwardHeaders;  -- Verify count matches
```

## Data Management

### Master Data Maintenance

#### Categories
- **Add Category**: Administration > Masters > Category > New
- **Disable Category**: Edit > Uncheck Active
- **Archive**: Cannot delete, soft delete preserves history

#### Units
- Standard units included (Nos, Mtr, Kg, Ltr, Set, Box, Rol, Pr)
- Add custom units as needed
- Symbol used in GRN/Issue line items

#### Departments
- Maps to organizational structure
- Used for filtering Issue documents
- Each has optional head and email

#### Sites & Warehouses
- Site: Physical location (HQ, Branch 1, Project Site)
- Warehouse: Storage within a site
- Default warehouse flag determines first selection in forms

#### Vendors
- Supplier master with GST/PAN validation
- Credit Days: Payment terms
- Blacklist flag: Prevents selection on new GRN

### Document Number Sequences

**Automatic Sequences** (Configured during seed):
- GRN: Annual sequence (GRN-2024-0001)
- ISSUE: Annual sequence (ISSUE-2024-0001)
- ITEM: Continuous sequence (ITEM-001)

**Reset Sequence** (Yearly):
```sql
UPDATE DocumentSequences 
SET NextNumber = 1 
WHERE SequenceCode = 'GRN';
```

### Data Import

#### Item Import (Excel)
1. Navigate to **Inventory > Items**
2. Click **Import** button
3. Download template
4. Fill in items with required columns:
   - Code, Name, Category, Unit, Cost, ReorderLevel
5. Upload file
6. Validate data (dry run)
7. Commit to database

**Validation Rules**:
- Code must be unique
- Category must exist in master
- Unit must exist in master
- Cost must be positive

#### Vendor Import (CSV)
```csv
Code,Name,CreditDays,GstNumber,PanNumber,IsActive
V001,Supplier A,30,18AABCU1234H1Z0,AAAAA0000A,1
V002,Supplier B,15,18AABCU1234H1Z1,BBBBB0000B,1
```

### Data Export

All grids support export:
1. Click **Excel** or **PDF** button
2. Downloads filtered/sorted data
3. Maintains formatting and formulas (Excel)

## Troubleshooting

### User Cannot Login

**Issue**: "Invalid username or password" error

**Diagnosis**:
```sql
SELECT UserName, Email, EmailConfirmed, IsActive, LockoutEnd
FROM AspNetUsers
WHERE UserName = 'username';
```

**Solutions**:
1. Verify user exists and is Active (IsActive = 1)
2. Check account lockout: LockoutEnd > GETDATE()
   - Wait 30 minutes or reset in admin
3. Reset password via admin interface
4. Check browser cookies/cache (clear and retry)

### Slow Database Performance

**Diagnosis**:
```sql
-- Check missing indexes
SELECT * FROM sys.dm_db_missing_index_details;

-- Check table sizes
SELECT 
    OBJECT_NAME(ps.object_id) as TableName,
    SUM(ps.reserved_page_count) as ReservedPages
FROM sys.dm_db_partition_stats ps
GROUP BY ps.object_id
ORDER BY SUM(ps.reserved_page_count) DESC;

-- Check active queries
SELECT * FROM sys.dm_exec_requests
WHERE session_id > 50;
```

**Solutions**:
1. Run index maintenance:
   ```sql
   EXEC sp_MaintainIndexes;
   ```
2. Update table statistics:
   ```sql
   EXEC sp_updatestats;
   ```
3. Review slow query log and add indexes
4. Archive old audit logs:
   ```sql
   DELETE FROM AuditLog WHERE ChangedOn < DATEADD(YEAR, -1, GETDATE());
   ```

### Application Crashes

**Log Investigation**:
1. Check error logs: `C:\Logs\RCMS\log-*.txt`
2. Search for "ERROR" or "FATAL"
3. Note timestamp and exception details
4. Check IIS application pool status:
   ```powershell
   Get-IISAppPool -Name "RCMS" | Select-Object State, ManagedRuntimeVersion
   ```

**Common Causes**:
- Database connection lost: Verify SQL Server is running
- Disk full: Check available disk space
- Memory exhaustion: Increase app pool memory limit
- Configuration error: Verify appsettings.json syntax

### Export Failed

**Issue**: Excel/PDF export returns error

**Diagnosis**:
1. Verify EPPlus license (if using commercial version)
2. Check file storage path exists and is writable
3. Verify user has READ permission on data

**Solutions**:
```powershell
# Check folder permissions
icacls "C:\AppData\RCMS\Files" /T

# Add permissions for app pool if needed
icacls "C:\AppData\RCMS\Files" /grant "IIS APPPOOL\RCMS:(OI)(CI)F" /T
```

### Email Not Sending

**Diagnosis**:
1. Check email configuration in appsettings.json
2. Review error logs for SMTP errors
3. Test SMTP connection:
   ```powershell
   $smtp = New-Object Net.Mail.SmtpClient("smtp.gmail.com", 587)
   $smtp.EnableSsl = $true
   $smtp.Credentials = New-Object System.Net.NetworkCredential("user@gmail.com", "password")
   $smtp.Send("from@gmail.com", "to@gmail.com", "Test", "Test message")
   ```

**Solutions**:
1. Verify SMTP server and port
2. Use [App Passwords](https://support.google.com/accounts/answer/185833) for Gmail
3. Ensure firewall allows outbound SMTP (port 587)
4. Check spam folder for sent emails

## Security Best Practices

1. **Change Default Passwords**: Immediately after deployment
2. **Disable SQL sa Account**: Reduce attack surface
3. **Enable HTTPS Only**: Redirect all HTTP to HTTPS
4. **Regular Backups**: Test restoration monthly
5. **Review Audit Logs**: Weekly or more frequently
6. **Update .NET & SQL Server**: Apply security patches immediately
7. **Strong Passwords**: Enforce complexity requirements
8. **MFA**: Consider implementing for admin accounts
9. **IP Whitelisting**: Restrict admin access to known IPs
10. **Regular Security Scan**: Use tools like Nessus or OWASP ZAP

## Performance Optimization

### Monitoring Performance

```sql
-- Database query performance
SELECT TOP 10
    qs.execution_count,
    qs.total_elapsed_time / 1000 as total_ms,
    qs.total_elapsed_time / 1000 / qs.execution_count as avg_ms,
    st.text
FROM sys.dm_exec_query_stats qs
CROSS APPLY sys.dm_exec_sql_text(qs.sql_handle) st
ORDER BY qs.total_elapsed_time DESC;
```

### Caching Strategy

The application implements caching for:
- Master data lookups (Categories, Units, etc.)
- Dashboard KPI data (5-minute TTL)
- User role information (session-based)

To clear cache:
```csharp
// In code (requires rebuild)
_cacheService.RemoveAsync("key");
```

### Connection Pool Tuning

```json
"ConnectionStrings": {
  "DefaultConnection": "Server=...;Max Pool Size=100;Min Pool Size=5;..."
}
```

---

**Version**: 1.0  
**Last Updated**: August 4, 2024  
**Questions?** Contact the development team
