# Enterprise Inventory Management System (RCMS)

A production-ready, multi-tier inventory management system built with ASP.NET Core 8, SQL Server 2022, and a modern JavaScript frontend. Designed for multi-store, multi-warehouse, and multi-project operations with comprehensive approval workflows, role-based access control, and detailed audit logging.

## System Architecture

### Technology Stack

- **Backend**: ASP.NET Core 8 MVC, C# 12
- **Database**: SQL Server 2022 with stored procedures
- **Frontend**: HTML5, CSS3 (Bootstrap 5), JavaScript (jQuery, DataTables, Select2, Chart.js)
- **Architecture**: 3-Tier (Controllers → Business Services → Repositories → Stored Procedures)
- **Authentication**: ASP.NET Core Identity with JWT claims
- **Logging**: Serilog (structured logging)
- **Caching**: In-memory cache with distributed options

### Project Structure

```
InventoryManagement.sln
├── Inventory.Common/              # Shared constants, enums, exceptions, models
├── Inventory.Entities/            # EF Core models and DTOs
├── Inventory.DAL/                 # Entity Framework DbContext, configurations
├── Inventory.Repository/          # Repository pattern implementation, UnitOfWork
├── Inventory.Services/            # Infrastructure services (Email, Export, Import, Caching)
├── Inventory.BLL/                 # Business logic layer with AutoMapper
├── Inventory.Web/                 # ASP.NET Core MVC application
├── Inventory.Database/            # SQL Server schema, SPs, seed data
└── Inventory.Tests/               # Unit and integration tests
```

## Core Features

### 1. Master Data Management
- Categories, Units, Departments, Sites, Warehouses, Manufacturers, Couriers, Engineers
- Item master with barcode support and Excel import
- Vendor master with GST/PAN validation and credit days tracking

### 2. Transaction Processing
- **Goods Receipt Notes (GRN)**: Inward goods with multi-line item entry, vendor tracking
- **Material Issues**: Outward materials with consumption tracking and courier dispatch
- Document workflow: Draft → Pending Approval → Approved → Completed

### 3. Approval Workflow
- Unified approval queue showing all pending documents
- Role-based approval authority (Approver role)
- Approval/rejection with comments and audit trail
- Age tracking for SLA monitoring

### 4. Stock Management
- Real-time inventory balances across multiple warehouses
- Stock ledger with transaction history and movement analysis
- Low stock alerts with reorder level tracking
- Dead stock identification based on aging

### 5. Reporting & Analytics
- 10+ built-in reports (Receipt Register, Issue Register, Stock Valuation, etc.)
- ABC analysis for inventory classification
- Consumption and purchase trend analysis
- Dashboard with KPI cards and trend visualization
- Export to Excel/PDF for all reports

### 6. Security & Compliance
- 6-tier role-based access control (Administrator, Inventory Manager, Store Executive, Approver, Project Manager, Viewer)
- ASP.NET Identity with password complexity requirements
- Account lockout after failed login attempts
- Comprehensive audit logging of all changes
- Anti-forgery token protection on all forms
- HTTPS enforcement and security headers

### 7. User Administration
- User CRUD with role assignment
- Password reset functionality
- Account deactivation without data loss
- Login history tracking

## Database Design

### Key Tables
- **Masters**: Categories, Units, Departments, Sites, Warehouses, Manufacturers, Couriers, Engineers
- **Items**: Product master with barcode, category, UOM, cost
- **Vendors**: Supplier master with GST/PAN, bank details
- **InventoryInwardHeaders/Details**: GRN documents with line items
- **InventoryOutwardHeaders/Details**: Issue/dispatch documents with line items
- **StockLedger**: Transaction audit trail for all stock movements
- **AuditLog**: Complete change tracking for compliance

### Stored Procedures (50+ SPs)
- **Master CRUD**: Generic CRUD, code validation, list operations
- **Transaction Processing**: GRN and Issue creation, approval, completion
- **Stock Management**: Current stock, ledger queries, low stock alerts
- **Reporting**: Aggregate queries for all report types
- **System**: Notifications, audit log management, index maintenance

All SPs follow a standard contract:
```sql
@ReturnCode INT = 0 OUTPUT,
@Message NVARCHAR(500) = NULL OUTPUT,
@NewId BIGINT = NULL OUTPUT,
@GeneratedNumber NVARCHAR(50) = NULL OUTPUT
```

Return codes: 0 (success), -400 (validation), -401 (auth), -403 (forbidden), -404 (not found), -409 (conflict), -500 (error)

## Deployment Guide

### Prerequisites
- SQL Server 2022 (Express, Standard, or Enterprise)
- .NET 8 SDK
- IIS 10+ (for production hosting)
- Windows Server 2016 or later

### Database Deployment

1. **Automated Deployment (PowerShell)**
   ```powershell
   .\Inventory.Database\Deploy.ps1 `
     -ServerInstance "YOUR_SERVER_INSTANCE" `
     -Database "RCMS" `
     -UseIntegratedSecurity $true `
     -IncludeAgentJobs $true
   ```

2. **Manual Deployment**
   - Execute scripts in order: 01_Schema → 02_Programmability → 03_Data → 04_Jobs
   - All scripts are idempotent and safe to re-run
   - Seed data includes master entities, roles, and default sequences

### Application Deployment

#### Development
```bash
# Restore packages
dotnet restore

# Build
dotnet build

# Run migrations (if using EF migrations)
dotnet ef database update

# Run application
dotnet run --project Inventory.Web
```

#### Production (IIS)

1. **Publish**
   ```bash
   dotnet publish -c Release -o ./publish
   ```

2. **IIS Setup**
   - Create application pool (.NET CLR version: "No Managed Code")
   - Create website pointing to published folder
   - Enable HTTPS with valid certificate
   - Set app pool identity to have SQL Server access

3. **Application Settings**
   - Update `appsettings.Production.json` with:
     - Database connection string
     - SMTP server settings
     - File storage path
     - Session timeout
   
4. **Security Configuration**
   - Enable Windows Authentication in IIS
   - Configure IP restrictions if needed
   - Set up SSL/TLS certificates
   - Configure request filtering

#### Docker (Optional)
```dockerfile
# See Dockerfile in root for containerization
docker build -t rcms:latest .
docker run -p 443:443 -e ConnectionStrings__DefaultConnection="..." rcms:latest
```

## Configuration

### appsettings.json
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=.;Database=RCMS;Trusted_Connection=true;"
  },
  "Authentication": {
    "RequirePasswordChange": true,
    "PasswordExpiryDays": 90,
    "SessionTimeoutMinutes": 30
  },
  "Email": {
    "SmtpServer": "smtp.gmail.com",
    "SmtpPort": 587,
    "FromAddress": "noreply@company.com"
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft": "Warning"
    }
  }
}
```

## API Endpoints

### Authentication
- `POST /Account/Login` - User login
- `POST /Account/Logout` - User logout
- `POST /Account/ChangePassword` - Change password
- `POST /Account/ForgotPassword` - Initiate password reset

### Dashboard
- `GET /Dashboard` - Executive dashboard view
- `GET /Dashboard/Data` - AJAX refresh of KPI data
- `GET /Dashboard/RecentActivity` - Activity feed

### Transactions (GRN)
- `GET /Grn` - List all GRNs
- `GET /Grn/List` - DataTables server-side data
- `GET /Grn/Create` - Create form
- `POST /Grn/Save` - Save GRN
- `GET /Grn/Details/{id}` - View details
- `POST /Grn/Submit` - Submit for approval
- `POST /Grn/Approve` - Approve/reject
- `POST /Grn/Delete` - Soft delete

### Transactions (Issue)
- `GET /Issue` - List all issues
- `GET /Issue/List` - DataTables server-side data
- `GET /Issue/Create` - Create form
- `POST /Issue/Save` - Save issue
- `GET /Issue/Details/{id}` - View details
- `POST /Issue/Approve` - Approve/reject
- `POST /Issue/Dispatch` - Dispatch with courier
- `GET /Issue/Balance` - Real-time stock balance

### Masters (Generic)
- `GET /{Master}` - List all records
- `GET /{Master}/List` - DataTables server-side data
- `POST /{Master}/Save` - Create/update
- `GET /{Master}/Get/{id}` - Get single record
- `POST /{Master}/Delete` - Delete (soft)
- `GET /{Master}/IsCodeAvailable` - Remote validation
- `GET /{Master}/Export` - Export to Excel/PDF

### Stock & Reports
- `GET /Stock` - Current stock inquiry
- `GET /Stock/List` - Stock grid data
- `GET /Stock/Ledger` - Stock ledger
- `GET /Stock/LedgerList` - Ledger data
- `GET /Stock/LowStock` - Low stock items
- `GET /Report` - Report catalog
- `GET /Report/View` - Report runner
- `POST /Report/Run` - Execute report

### Administration
- `GET /User` - User list
- `GET /User/List` - Users grid
- `POST /User/Save` - Create/update user
- `POST /User/ResetPassword` - Reset password
- `GET /Audit` - Audit log
- `GET /Audit/List` - Audit log grid

## User Roles & Permissions

| Role | GRN | Issue | Approve | Users | Reports | Stock |
|------|-----|-------|---------|-------|---------|-------|
| Administrator | R/W/Delete | R/W/Delete | Yes | R/W/Delete | All | View |
| Inventory Manager | R/W/Submit | R/W/Submit/Dispatch | - | - | All | View |
| Store Executive | View | R/W/Submit | - | - | Limited | View |
| Approver | View | View | Yes | - | All | View |
| Project Manager | View | R/W/Submit | - | - | Limited | View |
| Viewer | View | View | - | - | Limited | View |

## Testing

### Unit Tests
```bash
dotnet test Inventory.Tests --filter Category=Unit
```

### Integration Tests
```bash
dotnet test Inventory.Tests --filter Category=Integration
```

### Load Testing
- Use [Locust](https://locust.io) or [k6](https://k6.io) for performance testing
- Sample load test configs in `/Inventory.Tests/LoadTests/`

## Logging & Monitoring

### Structured Logging (Serilog)
- All app operations logged to file (daily rotation)
- All user actions logged to database (AuditLog table)
- SQL queries logged in development
- Error details with stack traces

### Health Checks
- `GET /health` - Overall health status
- `GET /health/ready` - Readiness probe
- `GET /health/live` - Liveness probe

## Performance Tuning

### Database Indexes
- Automatic index maintenance via SQL Agent job (weekly)
- Fragmentation monitoring and rebuild

### Caching Strategy
- Category/Unit/Department lookup caching
- Dashboard KPI caching with 5-minute TTL
- User role caching per session

### Query Optimization
- All CRUD via stored procedures (no inline SQL)
- Paged queries with DataTables server-side processing
- Index hints on frequently queried columns

## Backup & Disaster Recovery

### Backup Strategy
- Full backup: Daily at 2 AM
- Differential backup: Every 6 hours
- Transaction log backup: Every 15 minutes
- Retention: 30 days full, 7 days differential

### Restore Procedures
```sql
-- Full restore
RESTORE DATABASE RCMS FROM DISK = 'backup_file.bak'

-- Point-in-time restore
RESTORE DATABASE RCMS FROM DISK = 'backup_file.bak'
WITH STOPAT = '2024-01-15 14:30:00'
```

## Troubleshooting

### Common Issues

**Database Connection Fails**
- Verify SQL Server is running
- Check connection string in appsettings.json
- Ensure SQL Server authentication is enabled

**Login Issues**
- Check user exists in AspNetUsers table
- Verify user is Active (IsActive = 1)
- Check password hasn't expired

**Approval Queue Empty**
- Verify Approver role is assigned to user
- Check if documents are in PendingApproval status
- Review audit log for approval actions

**Excel Export Fails**
- Verify EPPlus license is configured
- Check file path has write permissions
- Ensure export service is running

## License

Proprietary - All rights reserved to Threedis Pvt. Ltd.

## Support

For issues and support, contact the development team or open an issue in the repository.

---

**Last Updated**: August 4, 2024  
**Version**: 1.0  
**Build**: Production Ready
