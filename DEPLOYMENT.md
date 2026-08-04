# Deployment Guide - Enterprise Inventory Management System

## Pre-Deployment Checklist

- [ ] SQL Server 2022 instance is running and accessible
- [ ] .NET 8 SDK installed on deployment machine
- [ ] IIS 10+ installed (for production)
- [ ] SSL certificates obtained and installed
- [ ] Email/SMTP credentials configured
- [ ] File storage path accessible and writable
- [ ] Backup procedures documented and tested
- [ ] Disaster recovery plan in place

## Phase 1: Database Setup

### 1.1 SQL Server Configuration

```sql
-- Enable TCP/IP protocol (if not already enabled)
-- Use SQL Server Configuration Manager

-- Create database
CREATE DATABASE RCMS;

-- Create application user
CREATE LOGIN rcms_app WITH PASSWORD = 'StrongPassword123!';
CREATE USER rcms_app FOR LOGIN rcms_app;

-- Grant minimum required permissions
ALTER ROLE db_owner ADD MEMBER rcms_app;
```

### 1.2 Deploy Database Schema

**Automated Deployment (Recommended)**

```powershell
# PowerShell (Run as Administrator)
Set-ExecutionPolicy -ExecutionPolicy Bypass -Scope Process

cd Inventory.Database
.\Deploy.ps1 `
  -ServerInstance "SERVER_NAME\INSTANCE_NAME" `
  -Database "RCMS" `
  -Username "sa" `
  -Password "YourPassword" `
  -UseIntegratedSecurity $false `
  -IncludeAgentJobs $true

# Wait for completion (typically 2-5 minutes)
```

**Manual Deployment**

If PowerShell script fails, execute SQL scripts in order:

```powershell
$server = "SERVER_NAME\INSTANCE_NAME"
$database = "RCMS"
$username = "sa"
$password = "YourPassword"

$scripts = @(
    ".\01_Schema\01_Tables.sql",
    ".\01_Schema\02_Types.sql",
    ".\01_Schema\03_Functions_Views.sql",
    ".\02_Programmability\01_Security_Audit.sql",
    ".\02_Programmability\02_Masters.sql",
    ".\02_Programmability\03_Items_Vendors.sql",
    ".\02_Programmability\04_GRN.sql",
    ".\02_Programmability\05_Issue.sql",
    ".\02_Programmability\06_Stock_Reports.sql",
    ".\02_Programmability\07_Dashboard_Notifications.sql",
    ".\02_Programmability\08_Maintenance.sql",
    ".\03_Data\01_SeedMasters.sql",
    ".\04_Jobs\01_AgentJobs.sql"
)

foreach ($script in $scripts) {
    Write-Host "Executing: $script"
    sqlcmd -S $server -U $username -P $password -d $database -i $script
    if ($LASTEXITCODE -ne 0) { throw "Script failed: $script" }
}

Write-Host "Database deployment completed successfully!"
```

### 1.3 Verify Database Setup

```sql
-- Connect to RCMS database
USE RCMS;

-- Check tables exist
SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLES 
WHERE TABLE_TYPE = 'BASE TABLE';  -- Should be 30+

-- Check stored procedures
SELECT COUNT(*) FROM INFORMATION_SCHEMA.ROUTINES 
WHERE ROUTINE_TYPE = 'PROCEDURE';  -- Should be 50+

-- Verify seed data
SELECT * FROM Roles;  -- Should have 6 roles
SELECT * FROM Units;  -- Should have 8 units
SELECT * FROM Categories;  -- Should have 8 categories

-- Check SQL Agent Jobs
EXEC msdb.dbo.sp_help_job @job_name = 'Inventory - Low Stock Alerts';
```

## Phase 2: Application Build

### 2.1 Prepare Environment

```bash
# Clone or download repository
git clone https://github.com/Threedis/RCMS.git
cd RCMS

# Verify .NET 8 installation
dotnet --version  # Should output 8.0.x or higher
```

### 2.2 Restore & Build

```bash
# Restore NuGet packages
dotnet restore

# Build solution
dotnet build -c Release

# Verify build succeeded (no errors)
# Output should show "Build succeeded" message
```

### 2.3 Configuration Files

Create `Inventory.Web/appsettings.Production.json`:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=YOUR_SERVER;Database=RCMS;User Id=rcms_app;Password=YourPassword;Encrypt=true;TrustServerCertificate=false;"
  },
  "Authentication": {
    "RequirePasswordChange": false,
    "PasswordExpiryDays": 90,
    "SessionTimeoutMinutes": 30,
    "LockoutThreshold": 5,
    "LockoutDurationMinutes": 30
  },
  "Email": {
    "SmtpServer": "smtp.gmail.com",
    "SmtpPort": 587,
    "SmtpUsername": "your-email@gmail.com",
    "SmtpPassword": "your-app-password",
    "SmtpEnableSsl": true,
    "FromAddress": "noreply@company.com",
    "FromName": "RCMS Inventory System"
  },
  "FileStorage": {
    "BasePath": "C:\\AppData\\RCMS\\Files",
    "MaxFileSizeMB": 50,
    "AllowedExtensions": ".xlsx,.xls,.pdf,.jpg,.png,.doc,.docx"
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft": "Warning",
      "Microsoft.AspNetCore": "Warning"
    },
    "Serilog": {
      "MinimumLevel": "Information",
      "WriteTo": [
        {
          "Name": "File",
          "Args": {
            "path": "C:\\Logs\\RCMS\\log-.txt",
            "rollingInterval": "Day",
            "retainedFileCountLimit": 30
          }
        }
      ]
    }
  },
  "AllowedHosts": "yourdomain.com,www.yourdomain.com"
}
```

### 2.4 Publish Application

```bash
# Publish for production
dotnet publish -c Release -o ./publish

# Output location: ./publish/

# Verify published files exist
ls -la publish/
```

## Phase 3: IIS Setup (Windows Server)

### 3.1 Create Application Pool

```powershell
# Open IIS Manager or use PowerShell:

Import-Module IISAdministration

# Create application pool
New-IISAppPool -Name "RCMS" -ManagedRuntimeVersion ""

# Set pool identity
$pool = Get-IISAppPool -Name "RCMS"
$pool.ProcessModel.IdentityType = "ApplicationPoolIdentity"
$pool | Set-IISAppPool

# Set SQL Server permissions for app pool identity
# In SQL Server Management Studio:
# - Right-click Logins > New Login
# - Select "Windows authentication"
# - Enter "IIS APPPOOL\RCMS"
# - Map to rcms_app user
```

### 3.2 Create Website

```powershell
# Create website
New-IISSite -Name "RCMS" `
  -PhysicalPath "C:\inetpub\wwwroot\rcms" `
  -BindingInformation "*:443:yourdomain.com" `
  -Protocol "https" `
  -ApplicationPool "RCMS"

# Add additional bindings
New-IISBinding -Website "RCMS" `
  -Protocol "http" `
  -BindingInformation "*:80:yourdomain.com"

# Add SSL certificate
# Open IIS Manager > Sites > RCMS > Edit Bindings > HTTPS binding
# Select installed certificate (or create self-signed for testing)
```

### 3.3 Deploy Published Files

```powershell
# Copy published files to IIS path
Copy-Item -Path "C:\BuildOutput\publish\*" `
          -Destination "C:\inetpub\wwwroot\rcms" `
          -Recurse -Force

# Set NTFS permissions
$path = "C:\inetpub\wwwroot\rcms"
$acl = Get-Acl $path
$rule = New-Object System.Security.AccessControl.FileSystemAccessRule(
  "IIS APPPOOL\RCMS",
  "Modify",
  "ContainerInherit,ObjectInherit",
  "None",
  "Allow"
)
$acl.AddAccessRule($rule)
Set-Acl -Path $path -AclObject $acl
```

### 3.4 Configure IIS Settings

**Application Pool Settings:**
- Start Mode: AlwaysRunning
- Idle Time-out: Disabled
- Maximum Worker Processes: 1 (or more for load balancing)

**Website Settings:**
- Enable HTTPS redirect (HTTP to HTTPS)
- Set default documents: index.html, default.aspx

**Application Settings:**
- ASP.NET Compilation: Release
- Managed Pipeline Mode: Integrated

### 3.5 Create Log & File Storage Directories

```powershell
# Create directories
New-Item -ItemType Directory -Path "C:\Logs\RCMS" -Force
New-Item -ItemType Directory -Path "C:\AppData\RCMS\Files" -Force
New-Item -ItemType Directory -Path "C:\AppData\RCMS\Imports" -Force

# Set permissions for app pool
$acl = Get-Acl "C:\Logs\RCMS"
$rule = New-Object System.Security.AccessControl.FileSystemAccessRule(
  "IIS APPPOOL\RCMS",
  "Modify",
  "ContainerInherit,ObjectInherit",
  "None",
  "Allow"
)
$acl.AddAccessRule($rule)
Set-Acl -Path "C:\Logs\RCMS" -AclObject $acl

# Repeat for other directories
Set-Acl -Path "C:\AppData\RCMS" -AclObject $acl
```

## Phase 4: Post-Deployment Verification

### 4.1 Health Checks

```bash
# Check application is responding
curl -k https://yourdomain.com/health

# Should return: {"status":"Healthy"}

# Check database connectivity
curl -k https://yourdomain.com/health/ready

# Check authentication
curl -k https://yourdomain.com/Account/Login
```

### 4.2 Database Validation

```sql
USE RCMS;

-- Check document sequences are initialized
SELECT * FROM DocumentSequences;

-- Verify default admin user exists (optional)
SELECT * FROM AspNetUsers;

-- Check audit log is recording
SELECT COUNT(*) FROM AuditLog;
```

### 4.3 Initial Login

1. Navigate to `https://yourdomain.com`
2. Default credentials (if not changed during deployment):
   - Username: `admin`
   - Password: `AdminPassword@123`
3. **Change default password immediately**

### 4.4 Run Sanity Tests

```bash
# Test Dashboard
GET /Dashboard

# Test Masters
GET /Category

# Test Transactions
GET /Grn

# Test Reports
GET /Report

# All should return 200 OK
```

## Phase 5: Production Hardening

### 5.1 Security Configuration

```powershell
# Enable HTTPS only
# In IIS: Edit Site Bindings, disable HTTP

# Set security headers (already in app, verify)
# X-Content-Type-Options: nosniff
# X-Frame-Options: DENY
# Content-Security-Policy: script-src 'self'

# Enable request filtering
# IIS > Sites > RCMS > Request Filtering
# - Block suspicious patterns
# - Set file upload limits (50 MB)
```

### 5.2 Database Security

```sql
-- Disable sa account
ALTER LOGIN [sa] DISABLE;

-- Set database recovery model to FULL
ALTER DATABASE RCMS SET RECOVERY FULL;

-- Enable encryption (TDE)
USE master;
CREATE MASTER KEY ENCRYPTION BY PASSWORD = 'YourStrongPassword';
CREATE CERTIFICATE RCMS_Cert WITH SUBJECT = 'RCMS TDE';
CREATE DATABASE ENCRYPTION KEY
  WITH ALGORITHM = AES_256
  ENCRYPTION BY SERVER CERTIFICATE RCMS_Cert;
ALTER DATABASE RCMS SET ENCRYPTION ON;
```

### 5.3 Backup Configuration

```powershell
# Create backup folder
New-Item -ItemType Directory -Path "C:\SQLBackups" -Force

# SQL Server backup job (via SQL Agent):
-- Full backup: Daily 2 AM
-- Differential: Every 6 hours
-- Transaction log: Every 15 minutes
-- Retention: 30 days
```

### 5.4 Monitoring Setup

```powershell
# Install Application Insights (optional)
# In Visual Studio: Add Application Insights to project
# Configure in appsettings.json

# Enable Windows Event Log monitoring
# Event Viewer > Windows Logs > Application
# Look for .NET application events

# Set up email alerts for critical errors
```

## Phase 6: Performance Tuning

### 6.1 Database Optimization

```sql
-- Run index maintenance job manually
EXEC sp_MaintainIndexes;

-- Check index fragmentation
SELECT 
    OBJECT_NAME(ips.object_id) AS TableName,
    i.name AS IndexName,
    ips.avg_fragmentation_in_percent AS Fragmentation
FROM sys.dm_db_index_physical_stats(DB_ID(), NULL, NULL, NULL, 'LIMITED') ips
JOIN sys.indexes i ON ips.object_id = i.object_id
    AND ips.index_id = i.index_id
WHERE ips.avg_fragmentation_in_percent > 10
    AND ips.page_count > 1000;
```

### 6.2 Application Tuning

```json
// In appsettings.Production.json
{
  "Caching": {
    "EnableDistributedCache": true,
    "CacheDurationMinutes": 15
  },
  "DataTables": {
    "PageSizeDefault": 25,
    "PageSizeMax": 100
  }
}
```

### 6.3 IIS Tuning

```powershell
# Increase app pool queue length
$pool = Get-IISAppPool -Name "RCMS"
$pool.QueueLength = 5000
$pool | Set-IISAppPool

# Enable output caching
# IIS > Output Caching > Add...
# Cache GET/POST requests for 60 seconds
```

## Rollback Procedure

If deployment fails or issues arise:

```powershell
# 1. Stop the website
Stop-IISSite -Name "RCMS"

# 2. Restore previous application version
Remove-Item "C:\inetpub\wwwroot\rcms\*" -Recurse -Force
Copy-Item -Path "C:\Backups\RCMS_v1.0\*" -Destination "C:\inetpub\wwwroot\rcms" -Recurse -Force

# 3. Restore previous database (if schema changed)
RESTORE DATABASE RCMS FROM DISK = 'C:\SQLBackups\RCMS_20240104_backup.bak'
WITH REPLACE, NORECOVERY

# 4. Start website
Start-IISSite -Name "RCMS"

# 5. Verify
curl -k https://yourdomain.com/health
```

## Monitoring After Deployment

### Daily Checks
- [ ] Application is accessible and responding
- [ ] No errors in application logs
- [ ] Database backups completed
- [ ] Disk space is adequate

### Weekly Checks
- [ ] Review audit log for suspicious activity
- [ ] Check database index fragmentation
- [ ] Verify all scheduled jobs completed
- [ ] Performance metrics are normal

### Monthly Checks
- [ ] Test disaster recovery procedure
- [ ] Review and update security patches
- [ ] Capacity planning (disk, memory, connections)
- [ ] User feedback and issues resolution

## Support & Troubleshooting

| Issue | Solution |
|-------|----------|
| Website returns 500 error | Check application logs in C:\Logs\RCMS |
| Database connection fails | Verify connection string, check SQL Server is running |
| Very slow response | Check database query plans, verify indexes, review IIS settings |
| Users cannot login | Check user exists in AspNetUsers, verify password policy |
| Export/Import fails | Verify file storage path has write permissions |

---

**Version**: 1.0  
**Last Updated**: August 4, 2024  
**Next Review**: August 4, 2025
