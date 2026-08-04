/* =============================================================================
   Enterprise Inventory Management System
   01_Tables.sql  -  Core schema (3NF)

   Conventions used throughout the schema
     - Every business table carries CreatedOn/CreatedBy/ModifiedOn/ModifiedBy,
       IsActive, IsDeleted and a RowVersion concurrency token.
     - Nothing is physically deleted: IsDeleted = 1 is the delete.
     - Quantities are DECIMAL(18,4); money is DECIMAL(18,2); rates DECIMAL(18,4).
     - All dates that represent a business day are DATE; audit timestamps are
       DATETIME2(3) in UTC (SYSUTCDATETIME()).

   The script is re-runnable: every object is created only when absent.
   ========================================================================== */

SET NOCOUNT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

/* -----------------------------------------------------------------------------
   ASP.NET Core Identity
   The table names match the OnModelCreating mapping in ApplicationDbContext.
   -------------------------------------------------------------------------- */

IF OBJECT_ID(N'dbo.Roles', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Roles
    (
        Id                  INT             IDENTITY(1,1)   NOT NULL,
        Name                NVARCHAR(256)                   NULL,
        NormalizedName      NVARCHAR(256)                   NULL,
        ConcurrencyStamp    NVARCHAR(MAX)                   NULL,
        Description         NVARCHAR(300)                   NULL,
        IsSystemRole        BIT             NOT NULL        CONSTRAINT DF_Roles_IsSystemRole DEFAULT (0),
        CONSTRAINT PK_Roles PRIMARY KEY CLUSTERED (Id)
    );

    CREATE UNIQUE NONCLUSTERED INDEX RoleNameIndex
        ON dbo.Roles (NormalizedName) WHERE NormalizedName IS NOT NULL;
END
GO

IF OBJECT_ID(N'dbo.Users', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Users
    (
        Id                      INT             IDENTITY(1,1)   NOT NULL,
        UserName                NVARCHAR(256)                   NULL,
        NormalizedUserName      NVARCHAR(256)                   NULL,
        Email                   NVARCHAR(256)                   NULL,
        NormalizedEmail         NVARCHAR(256)                   NULL,
        EmailConfirmed          BIT             NOT NULL        CONSTRAINT DF_Users_EmailConfirmed DEFAULT (0),
        PasswordHash            NVARCHAR(MAX)                   NULL,
        SecurityStamp           NVARCHAR(MAX)                   NULL,
        ConcurrencyStamp        NVARCHAR(MAX)                   NULL,
        PhoneNumber             NVARCHAR(MAX)                   NULL,
        PhoneNumberConfirmed    BIT             NOT NULL        CONSTRAINT DF_Users_PhoneConfirmed DEFAULT (0),
        TwoFactorEnabled        BIT             NOT NULL        CONSTRAINT DF_Users_TwoFactor DEFAULT (0),
        LockoutEnd              DATETIMEOFFSET(7)               NULL,
        LockoutEnabled          BIT             NOT NULL        CONSTRAINT DF_Users_LockoutEnabled DEFAULT (1),
        AccessFailedCount       INT             NOT NULL        CONSTRAINT DF_Users_AccessFailed DEFAULT (0),

        /* Organisational extensions */
        FullName                NVARCHAR(150)   NOT NULL        CONSTRAINT DF_Users_FullName DEFAULT (''),
        EmployeeCode            NVARCHAR(30)                    NULL,
        Designation             NVARCHAR(150)                   NULL,
        DepartmentId            INT                             NULL,
        SiteId                  INT                             NULL,
        PasswordChangedOn       DATETIME2(3)                    NULL,
        MustChangePassword      BIT             NOT NULL        CONSTRAINT DF_Users_MustChangePwd DEFAULT (0),
        LastLoginOn             DATETIME2(3)                    NULL,
        CreatedOn               DATETIME2(3)    NOT NULL        CONSTRAINT DF_Users_CreatedOn DEFAULT (SYSUTCDATETIME()),
        CreatedBy               INT             NOT NULL        CONSTRAINT DF_Users_CreatedBy DEFAULT (0),
        ModifiedOn              DATETIME2(3)                    NULL,
        ModifiedBy              INT                             NULL,
        IsActive                BIT             NOT NULL        CONSTRAINT DF_Users_IsActive DEFAULT (1),
        IsDeleted               BIT             NOT NULL        CONSTRAINT DF_Users_IsDeleted DEFAULT (0),
        CONSTRAINT PK_Users PRIMARY KEY CLUSTERED (Id)
    );

    CREATE UNIQUE NONCLUSTERED INDEX UserNameIndex
        ON dbo.Users (NormalizedUserName) WHERE NormalizedUserName IS NOT NULL;
    CREATE NONCLUSTERED INDEX EmailIndex ON dbo.Users (NormalizedEmail);
    CREATE UNIQUE NONCLUSTERED INDEX UX_Users_EmployeeCode
        ON dbo.Users (EmployeeCode) WHERE EmployeeCode IS NOT NULL AND IsDeleted = 0;
END
GO

IF OBJECT_ID(N'dbo.UserRoles', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.UserRoles
    (
        UserId  INT NOT NULL,
        RoleId  INT NOT NULL,
        CONSTRAINT PK_UserRoles PRIMARY KEY CLUSTERED (UserId, RoleId),
        CONSTRAINT FK_UserRoles_Users FOREIGN KEY (UserId) REFERENCES dbo.Users (Id) ON DELETE CASCADE,
        CONSTRAINT FK_UserRoles_Roles FOREIGN KEY (RoleId) REFERENCES dbo.Roles (Id) ON DELETE CASCADE
    );

    CREATE NONCLUSTERED INDEX IX_UserRoles_RoleId ON dbo.UserRoles (RoleId);
END
GO

IF OBJECT_ID(N'dbo.UserClaims', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.UserClaims
    (
        Id          INT IDENTITY(1,1) NOT NULL,
        UserId      INT NOT NULL,
        ClaimType   NVARCHAR(MAX) NULL,
        ClaimValue  NVARCHAR(MAX) NULL,
        CONSTRAINT PK_UserClaims PRIMARY KEY CLUSTERED (Id),
        CONSTRAINT FK_UserClaims_Users FOREIGN KEY (UserId) REFERENCES dbo.Users (Id) ON DELETE CASCADE
    );

    CREATE NONCLUSTERED INDEX IX_UserClaims_UserId ON dbo.UserClaims (UserId);
END
GO

IF OBJECT_ID(N'dbo.RoleClaims', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.RoleClaims
    (
        Id          INT IDENTITY(1,1) NOT NULL,
        RoleId      INT NOT NULL,
        ClaimType   NVARCHAR(MAX) NULL,
        ClaimValue  NVARCHAR(MAX) NULL,
        CONSTRAINT PK_RoleClaims PRIMARY KEY CLUSTERED (Id),
        CONSTRAINT FK_RoleClaims_Roles FOREIGN KEY (RoleId) REFERENCES dbo.Roles (Id) ON DELETE CASCADE
    );

    CREATE NONCLUSTERED INDEX IX_RoleClaims_RoleId ON dbo.RoleClaims (RoleId);
END
GO

IF OBJECT_ID(N'dbo.UserLogins', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.UserLogins
    (
        LoginProvider       NVARCHAR(450) NOT NULL,
        ProviderKey         NVARCHAR(450) NOT NULL,
        ProviderDisplayName NVARCHAR(MAX) NULL,
        UserId              INT NOT NULL,
        CONSTRAINT PK_UserLogins PRIMARY KEY CLUSTERED (LoginProvider, ProviderKey),
        CONSTRAINT FK_UserLogins_Users FOREIGN KEY (UserId) REFERENCES dbo.Users (Id) ON DELETE CASCADE
    );

    CREATE NONCLUSTERED INDEX IX_UserLogins_UserId ON dbo.UserLogins (UserId);
END
GO

IF OBJECT_ID(N'dbo.UserTokens', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.UserTokens
    (
        UserId          INT NOT NULL,
        LoginProvider   NVARCHAR(450) NOT NULL,
        Name            NVARCHAR(450) NOT NULL,
        Value           NVARCHAR(MAX) NULL,
        CONSTRAINT PK_UserTokens PRIMARY KEY CLUSTERED (UserId, LoginProvider, Name),
        CONSTRAINT FK_UserTokens_Users FOREIGN KEY (UserId) REFERENCES dbo.Users (Id) ON DELETE CASCADE
    );
END
GO

/* -----------------------------------------------------------------------------
   Master data
   -------------------------------------------------------------------------- */

IF OBJECT_ID(N'dbo.Categories', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Categories
    (
        Id                  INT             IDENTITY(1,1) NOT NULL,
        Code                NVARCHAR(30)    NOT NULL,
        Name                NVARCHAR(150)   NOT NULL,
        Description         NVARCHAR(500)   NULL,
        ParentCategoryId    INT             NULL,
        DisplayOrder        INT             NOT NULL CONSTRAINT DF_Categories_Order DEFAULT (0),
        CreatedOn           DATETIME2(3)    NOT NULL CONSTRAINT DF_Categories_CreatedOn DEFAULT (SYSUTCDATETIME()),
        CreatedBy           INT             NOT NULL CONSTRAINT DF_Categories_CreatedBy DEFAULT (0),
        ModifiedOn          DATETIME2(3)    NULL,
        ModifiedBy          INT             NULL,
        IsActive            BIT             NOT NULL CONSTRAINT DF_Categories_IsActive DEFAULT (1),
        IsDeleted           BIT             NOT NULL CONSTRAINT DF_Categories_IsDeleted DEFAULT (0),
        RowVersion          ROWVERSION      NOT NULL,
        CONSTRAINT PK_Categories PRIMARY KEY CLUSTERED (Id),
        CONSTRAINT FK_Categories_Parent FOREIGN KEY (ParentCategoryId) REFERENCES dbo.Categories (Id)
    );

    CREATE UNIQUE NONCLUSTERED INDEX UX_Category_Code ON dbo.Categories (Code) WHERE IsDeleted = 0;
    CREATE NONCLUSTERED INDEX IX_Category_Name ON dbo.Categories (Name);
END
GO

IF OBJECT_ID(N'dbo.Units', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Units
    (
        Id              INT             IDENTITY(1,1) NOT NULL,
        Code            NVARCHAR(30)    NOT NULL,
        Name            NVARCHAR(150)   NOT NULL,
        Description     NVARCHAR(500)   NULL,
        Symbol          NVARCHAR(10)    NOT NULL CONSTRAINT DF_Units_Symbol DEFAULT (''),
        DecimalPlaces   INT             NOT NULL CONSTRAINT DF_Units_Decimals DEFAULT (0),
        DisplayOrder    INT             NOT NULL CONSTRAINT DF_Units_Order DEFAULT (0),
        CreatedOn       DATETIME2(3)    NOT NULL CONSTRAINT DF_Units_CreatedOn DEFAULT (SYSUTCDATETIME()),
        CreatedBy       INT             NOT NULL CONSTRAINT DF_Units_CreatedBy DEFAULT (0),
        ModifiedOn      DATETIME2(3)    NULL,
        ModifiedBy      INT             NULL,
        IsActive        BIT             NOT NULL CONSTRAINT DF_Units_IsActive DEFAULT (1),
        IsDeleted       BIT             NOT NULL CONSTRAINT DF_Units_IsDeleted DEFAULT (0),
        RowVersion      ROWVERSION      NOT NULL,
        CONSTRAINT PK_Units PRIMARY KEY CLUSTERED (Id),
        CONSTRAINT CK_Units_Decimals CHECK (DecimalPlaces BETWEEN 0 AND 3)
    );

    CREATE UNIQUE NONCLUSTERED INDEX UX_Unit_Code ON dbo.Units (Code) WHERE IsDeleted = 0;
    CREATE NONCLUSTERED INDEX IX_Unit_Name ON dbo.Units (Name);
END
GO

IF OBJECT_ID(N'dbo.Departments', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Departments
    (
        Id                  INT             IDENTITY(1,1) NOT NULL,
        Code                NVARCHAR(30)    NOT NULL,
        Name                NVARCHAR(150)   NOT NULL,
        Description         NVARCHAR(500)   NULL,
        HeadOfDepartment    NVARCHAR(150)   NULL,
        Email               NVARCHAR(150)   NULL,
        DisplayOrder        INT             NOT NULL CONSTRAINT DF_Departments_Order DEFAULT (0),
        CreatedOn           DATETIME2(3)    NOT NULL CONSTRAINT DF_Departments_CreatedOn DEFAULT (SYSUTCDATETIME()),
        CreatedBy           INT             NOT NULL CONSTRAINT DF_Departments_CreatedBy DEFAULT (0),
        ModifiedOn          DATETIME2(3)    NULL,
        ModifiedBy          INT             NULL,
        IsActive            BIT             NOT NULL CONSTRAINT DF_Departments_IsActive DEFAULT (1),
        IsDeleted           BIT             NOT NULL CONSTRAINT DF_Departments_IsDeleted DEFAULT (0),
        RowVersion          ROWVERSION      NOT NULL,
        CONSTRAINT PK_Departments PRIMARY KEY CLUSTERED (Id)
    );

    CREATE UNIQUE NONCLUSTERED INDEX UX_Department_Code ON dbo.Departments (Code) WHERE IsDeleted = 0;
    CREATE NONCLUSTERED INDEX IX_Department_Name ON dbo.Departments (Name);
END
GO

IF OBJECT_ID(N'dbo.Sites', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Sites
    (
        Id              INT             IDENTITY(1,1) NOT NULL,
        Code            NVARCHAR(30)    NOT NULL,
        Name            NVARCHAR(150)   NOT NULL,
        Description     NVARCHAR(500)   NULL,
        Address         NVARCHAR(400)   NULL,
        City            NVARCHAR(100)   NULL,
        [State]         NVARCHAR(100)   NULL,
        PinCode         NVARCHAR(20)    NULL,
        ContactPerson   NVARCHAR(150)   NULL,
        ContactNumber   NVARCHAR(20)    NULL,
        DisplayOrder    INT             NOT NULL CONSTRAINT DF_Sites_Order DEFAULT (0),
        CreatedOn       DATETIME2(3)    NOT NULL CONSTRAINT DF_Sites_CreatedOn DEFAULT (SYSUTCDATETIME()),
        CreatedBy       INT             NOT NULL CONSTRAINT DF_Sites_CreatedBy DEFAULT (0),
        ModifiedOn      DATETIME2(3)    NULL,
        ModifiedBy      INT             NULL,
        IsActive        BIT             NOT NULL CONSTRAINT DF_Sites_IsActive DEFAULT (1),
        IsDeleted       BIT             NOT NULL CONSTRAINT DF_Sites_IsDeleted DEFAULT (0),
        RowVersion      ROWVERSION      NOT NULL,
        CONSTRAINT PK_Sites PRIMARY KEY CLUSTERED (Id)
    );

    CREATE UNIQUE NONCLUSTERED INDEX UX_Site_Code ON dbo.Sites (Code) WHERE IsDeleted = 0;
    CREATE NONCLUSTERED INDEX IX_Site_Name ON dbo.Sites (Name);
END
GO

IF OBJECT_ID(N'dbo.Warehouses', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Warehouses
    (
        Id              INT             IDENTITY(1,1) NOT NULL,
        Code            NVARCHAR(30)    NOT NULL,
        Name            NVARCHAR(150)   NOT NULL,
        Description     NVARCHAR(500)   NULL,
        SiteId          INT             NOT NULL,
        InchargeName    NVARCHAR(150)   NULL,
        ContactNumber   NVARCHAR(20)    NULL,
        IsDefault       BIT             NOT NULL CONSTRAINT DF_Warehouses_IsDefault DEFAULT (0),
        DisplayOrder    INT             NOT NULL CONSTRAINT DF_Warehouses_Order DEFAULT (0),
        CreatedOn       DATETIME2(3)    NOT NULL CONSTRAINT DF_Warehouses_CreatedOn DEFAULT (SYSUTCDATETIME()),
        CreatedBy       INT             NOT NULL CONSTRAINT DF_Warehouses_CreatedBy DEFAULT (0),
        ModifiedOn      DATETIME2(3)    NULL,
        ModifiedBy      INT             NULL,
        IsActive        BIT             NOT NULL CONSTRAINT DF_Warehouses_IsActive DEFAULT (1),
        IsDeleted       BIT             NOT NULL CONSTRAINT DF_Warehouses_IsDeleted DEFAULT (0),
        RowVersion      ROWVERSION      NOT NULL,
        CONSTRAINT PK_Warehouses PRIMARY KEY CLUSTERED (Id),
        CONSTRAINT FK_Warehouses_Sites FOREIGN KEY (SiteId) REFERENCES dbo.Sites (Id)
    );

    CREATE UNIQUE NONCLUSTERED INDEX UX_Warehouse_Code ON dbo.Warehouses (Code) WHERE IsDeleted = 0;
    CREATE NONCLUSTERED INDEX IX_Warehouses_SiteId ON dbo.Warehouses (SiteId);
END
GO

IF OBJECT_ID(N'dbo.Manufacturers', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Manufacturers
    (
        Id              INT             IDENTITY(1,1) NOT NULL,
        Code            NVARCHAR(30)    NOT NULL,
        Name            NVARCHAR(150)   NOT NULL,
        Description     NVARCHAR(500)   NULL,
        Country         NVARCHAR(100)   NULL,
        Website         NVARCHAR(200)   NULL,
        DisplayOrder    INT             NOT NULL CONSTRAINT DF_Manufacturers_Order DEFAULT (0),
        CreatedOn       DATETIME2(3)    NOT NULL CONSTRAINT DF_Manufacturers_CreatedOn DEFAULT (SYSUTCDATETIME()),
        CreatedBy       INT             NOT NULL CONSTRAINT DF_Manufacturers_CreatedBy DEFAULT (0),
        ModifiedOn      DATETIME2(3)    NULL,
        ModifiedBy      INT             NULL,
        IsActive        BIT             NOT NULL CONSTRAINT DF_Manufacturers_IsActive DEFAULT (1),
        IsDeleted       BIT             NOT NULL CONSTRAINT DF_Manufacturers_IsDeleted DEFAULT (0),
        RowVersion      ROWVERSION      NOT NULL,
        CONSTRAINT PK_Manufacturers PRIMARY KEY CLUSTERED (Id)
    );

    CREATE UNIQUE NONCLUSTERED INDEX UX_Manufacturer_Code ON dbo.Manufacturers (Code) WHERE IsDeleted = 0;
    CREATE NONCLUSTERED INDEX IX_Manufacturer_Name ON dbo.Manufacturers (Name);
END
GO

IF OBJECT_ID(N'dbo.Couriers', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Couriers
    (
        Id                  INT             IDENTITY(1,1) NOT NULL,
        Code                NVARCHAR(30)    NOT NULL,
        Name                NVARCHAR(150)   NOT NULL,
        Description         NVARCHAR(500)   NULL,
        ContactPerson       NVARCHAR(150)   NULL,
        ContactNumber       NVARCHAR(20)    NULL,
        TrackingUrlTemplate NVARCHAR(300)   NULL,
        DisplayOrder        INT             NOT NULL CONSTRAINT DF_Couriers_Order DEFAULT (0),
        CreatedOn           DATETIME2(3)    NOT NULL CONSTRAINT DF_Couriers_CreatedOn DEFAULT (SYSUTCDATETIME()),
        CreatedBy           INT             NOT NULL CONSTRAINT DF_Couriers_CreatedBy DEFAULT (0),
        ModifiedOn          DATETIME2(3)    NULL,
        ModifiedBy          INT             NULL,
        IsActive            BIT             NOT NULL CONSTRAINT DF_Couriers_IsActive DEFAULT (1),
        IsDeleted           BIT             NOT NULL CONSTRAINT DF_Couriers_IsDeleted DEFAULT (0),
        RowVersion          ROWVERSION      NOT NULL,
        CONSTRAINT PK_Couriers PRIMARY KEY CLUSTERED (Id)
    );

    CREATE UNIQUE NONCLUSTERED INDEX UX_Courier_Code ON dbo.Couriers (Code) WHERE IsDeleted = 0;
    CREATE NONCLUSTERED INDEX IX_Courier_Name ON dbo.Couriers (Name);
END
GO

IF OBJECT_ID(N'dbo.Engineers', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Engineers
    (
        Id              INT             IDENTITY(1,1) NOT NULL,
        Code            NVARCHAR(30)    NOT NULL,
        Name            NVARCHAR(150)   NOT NULL,
        Description     NVARCHAR(500)   NULL,
        EmployeeCode    NVARCHAR(30)    NULL,
        DepartmentId    INT             NULL,
        SiteId          INT             NULL,
        Email           NVARCHAR(150)   NULL,
        MobileNumber    NVARCHAR(20)    NULL,
        Designation     NVARCHAR(150)   NULL,
        DisplayOrder    INT             NOT NULL CONSTRAINT DF_Engineers_Order DEFAULT (0),
        CreatedOn       DATETIME2(3)    NOT NULL CONSTRAINT DF_Engineers_CreatedOn DEFAULT (SYSUTCDATETIME()),
        CreatedBy       INT             NOT NULL CONSTRAINT DF_Engineers_CreatedBy DEFAULT (0),
        ModifiedOn      DATETIME2(3)    NULL,
        ModifiedBy      INT             NULL,
        IsActive        BIT             NOT NULL CONSTRAINT DF_Engineers_IsActive DEFAULT (1),
        IsDeleted       BIT             NOT NULL CONSTRAINT DF_Engineers_IsDeleted DEFAULT (0),
        RowVersion      ROWVERSION      NOT NULL,
        CONSTRAINT PK_Engineers PRIMARY KEY CLUSTERED (Id),
        CONSTRAINT FK_Engineers_Departments FOREIGN KEY (DepartmentId) REFERENCES dbo.Departments (Id),
        CONSTRAINT FK_Engineers_Sites FOREIGN KEY (SiteId) REFERENCES dbo.Sites (Id)
    );

    CREATE UNIQUE NONCLUSTERED INDEX UX_Engineer_Code ON dbo.Engineers (Code) WHERE IsDeleted = 0;
    CREATE NONCLUSTERED INDEX IX_Engineer_Name ON dbo.Engineers (Name);
    CREATE NONCLUSTERED INDEX IX_Engineers_DepartmentId ON dbo.Engineers (DepartmentId);
END
GO

IF OBJECT_ID(N'dbo.Vendors', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Vendors
    (
        Id                  INT             IDENTITY(1,1) NOT NULL,
        Code                NVARCHAR(30)    NOT NULL,
        Name                NVARCHAR(150)   NOT NULL,
        Description         NVARCHAR(500)   NULL,
        Address             NVARCHAR(400)   NULL,
        City                NVARCHAR(100)   NULL,
        [State]             NVARCHAR(100)   NULL,
        PinCode             NVARCHAR(20)    NULL,
        ContactPerson       NVARCHAR(150)   NULL,
        ContactNumber       NVARCHAR(20)    NULL,
        Email               NVARCHAR(150)   NULL,
        GstNumber           NVARCHAR(20)    NULL,
        PanNumber           NVARCHAR(15)    NULL,
        BankName            NVARCHAR(100)   NULL,
        BankAccountNumber   NVARCHAR(30)    NULL,
        IfscCode            NVARCHAR(15)    NULL,
        CreditDays          INT             NULL,
        Rating              INT             NULL,
        IsBlacklisted       BIT             NOT NULL CONSTRAINT DF_Vendors_Blacklisted DEFAULT (0),
        DisplayOrder        INT             NOT NULL CONSTRAINT DF_Vendors_Order DEFAULT (0),
        CreatedOn           DATETIME2(3)    NOT NULL CONSTRAINT DF_Vendors_CreatedOn DEFAULT (SYSUTCDATETIME()),
        CreatedBy           INT             NOT NULL CONSTRAINT DF_Vendors_CreatedBy DEFAULT (0),
        ModifiedOn          DATETIME2(3)    NULL,
        ModifiedBy          INT             NULL,
        IsActive            BIT             NOT NULL CONSTRAINT DF_Vendors_IsActive DEFAULT (1),
        IsDeleted           BIT             NOT NULL CONSTRAINT DF_Vendors_IsDeleted DEFAULT (0),
        RowVersion          ROWVERSION      NOT NULL,
        CONSTRAINT PK_Vendors PRIMARY KEY CLUSTERED (Id),
        CONSTRAINT CK_Vendors_Rating CHECK (Rating IS NULL OR Rating BETWEEN 1 AND 5),
        CONSTRAINT CK_Vendors_CreditDays CHECK (CreditDays IS NULL OR CreditDays BETWEEN 0 AND 365)
    );

    CREATE UNIQUE NONCLUSTERED INDEX UX_Vendor_Code ON dbo.Vendors (Code) WHERE IsDeleted = 0;
    CREATE UNIQUE NONCLUSTERED INDEX UX_Vendors_GstNumber
        ON dbo.Vendors (GstNumber) WHERE GstNumber IS NOT NULL AND IsDeleted = 0;
    CREATE NONCLUSTERED INDEX IX_Vendor_Name ON dbo.Vendors (Name);
END
GO

/* Deferred foreign keys on Users, now that Departments and Sites exist. */
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_Users_Departments')
BEGIN
    ALTER TABLE dbo.Users WITH CHECK
        ADD CONSTRAINT FK_Users_Departments FOREIGN KEY (DepartmentId) REFERENCES dbo.Departments (Id);
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_Users_Sites')
BEGIN
    ALTER TABLE dbo.Users WITH CHECK
        ADD CONSTRAINT FK_Users_Sites FOREIGN KEY (SiteId) REFERENCES dbo.Sites (Id);
END
GO

/* -----------------------------------------------------------------------------
   Items
   -------------------------------------------------------------------------- */

IF OBJECT_ID(N'dbo.Items', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Items
    (
        Id                  INT             IDENTITY(1,1) NOT NULL,
        ItemCode            NVARCHAR(40)    NOT NULL,
        ItemName            NVARCHAR(250)   NOT NULL,
        PartNumber          NVARCHAR(100)   NULL,
        AlternatePartNumber NVARCHAR(100)   NULL,
        HsnCode             NVARCHAR(20)    NULL,
        Description         NVARCHAR(1000)  NULL,
        Specification       NVARCHAR(500)   NULL,
        CategoryId          INT             NOT NULL,
        UnitId              INT             NOT NULL,
        ManufacturerId      INT             NULL,
        ItemType            INT             NOT NULL CONSTRAINT DF_Items_ItemType DEFAULT (2),
        Barcode             NVARCHAR(100)   NULL,
        ReorderLevel        DECIMAL(18,4)   NOT NULL CONSTRAINT DF_Items_ReorderLevel DEFAULT (0),
        ReorderQuantity     DECIMAL(18,4)   NOT NULL CONSTRAINT DF_Items_ReorderQty DEFAULT (0),
        MinimumStock        DECIMAL(18,4)   NOT NULL CONSTRAINT DF_Items_MinStock DEFAULT (0),
        MaximumStock        DECIMAL(18,4)   NOT NULL CONSTRAINT DF_Items_MaxStock DEFAULT (0),
        StandardCost        DECIMAL(18,4)   NOT NULL CONSTRAINT DF_Items_StdCost DEFAULT (0),
        AverageCost         DECIMAL(18,4)   NOT NULL CONSTRAINT DF_Items_AvgCost DEFAULT (0),
        ShelfLocation       NVARCHAR(50)    NULL,
        IsBatchTracked      BIT             NOT NULL CONSTRAINT DF_Items_Batch DEFAULT (0),
        IsSerialTracked     BIT             NOT NULL CONSTRAINT DF_Items_Serial DEFAULT (0),
        ShelfLifeDays       INT             NULL,
        TaxRate             DECIMAL(5,2)    NULL,
        CreatedOn           DATETIME2(3)    NOT NULL CONSTRAINT DF_Items_CreatedOn DEFAULT (SYSUTCDATETIME()),
        CreatedBy           INT             NOT NULL CONSTRAINT DF_Items_CreatedBy DEFAULT (0),
        ModifiedOn          DATETIME2(3)    NULL,
        ModifiedBy          INT             NULL,
        IsActive            BIT             NOT NULL CONSTRAINT DF_Items_IsActive DEFAULT (1),
        IsDeleted           BIT             NOT NULL CONSTRAINT DF_Items_IsDeleted DEFAULT (0),
        RowVersion          ROWVERSION      NOT NULL,
        CONSTRAINT PK_Items PRIMARY KEY CLUSTERED (Id),
        CONSTRAINT FK_Items_Categories FOREIGN KEY (CategoryId) REFERENCES dbo.Categories (Id),
        CONSTRAINT FK_Items_Units FOREIGN KEY (UnitId) REFERENCES dbo.Units (Id),
        CONSTRAINT FK_Items_Manufacturers FOREIGN KEY (ManufacturerId) REFERENCES dbo.Manufacturers (Id),
        CONSTRAINT CK_Items_NonNegative CHECK
            (ReorderLevel >= 0 AND ReorderQuantity >= 0 AND MinimumStock >= 0
             AND MaximumStock >= 0 AND StandardCost >= 0 AND AverageCost >= 0),
        CONSTRAINT CK_Items_MinMax CHECK (MaximumStock = 0 OR MaximumStock >= MinimumStock),
        CONSTRAINT CK_Items_TaxRate CHECK (TaxRate IS NULL OR TaxRate BETWEEN 0 AND 100)
    );

    CREATE UNIQUE NONCLUSTERED INDEX UX_Items_ItemCode ON dbo.Items (ItemCode) WHERE IsDeleted = 0;
    CREATE UNIQUE NONCLUSTERED INDEX UX_Items_Barcode
        ON dbo.Items (Barcode) WHERE Barcode IS NOT NULL AND IsDeleted = 0;
    CREATE NONCLUSTERED INDEX IX_Items_ItemName ON dbo.Items (ItemName);
    CREATE NONCLUSTERED INDEX IX_Items_CategoryId ON dbo.Items (CategoryId)
        INCLUDE (ItemCode, ItemName, UnitId, AverageCost, ReorderLevel);
    CREATE NONCLUSTERED INDEX IX_Items_PartNumber_Manufacturer ON dbo.Items (PartNumber, ManufacturerId);
END
GO

IF OBJECT_ID(N'dbo.ItemImages', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.ItemImages
    (
        Id              INT             IDENTITY(1,1) NOT NULL,
        ItemId          INT             NOT NULL,
        FileName        NVARCHAR(260)   NOT NULL,
        FilePath        NVARCHAR(500)   NOT NULL,
        ContentType     NVARCHAR(100)   NULL,
        FileSizeBytes   BIGINT          NOT NULL CONSTRAINT DF_ItemImages_Size DEFAULT (0),
        IsPrimary       BIT             NOT NULL CONSTRAINT DF_ItemImages_Primary DEFAULT (0),
        UploadedOn      DATETIME2(3)    NOT NULL CONSTRAINT DF_ItemImages_UploadedOn DEFAULT (SYSUTCDATETIME()),
        UploadedBy      INT             NOT NULL CONSTRAINT DF_ItemImages_UploadedBy DEFAULT (0),
        CONSTRAINT PK_ItemImages PRIMARY KEY CLUSTERED (Id),
        CONSTRAINT FK_ItemImages_Items FOREIGN KEY (ItemId) REFERENCES dbo.Items (Id) ON DELETE CASCADE
    );

    CREATE NONCLUSTERED INDEX IX_ItemImages_ItemId ON dbo.ItemImages (ItemId);
    /* At most one primary image per item. */
    CREATE UNIQUE NONCLUSTERED INDEX UX_ItemImages_Primary
        ON dbo.ItemImages (ItemId) WHERE IsPrimary = 1;
END
GO

/* -----------------------------------------------------------------------------
   Inventory inward (Goods Receipt Note)
   -------------------------------------------------------------------------- */

IF OBJECT_ID(N'dbo.InventoryInwardHeader', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.InventoryInwardHeader
    (
        Id                      INT             IDENTITY(1,1) NOT NULL,
        GrnNumber               NVARCHAR(30)    NOT NULL,
        GrnDate                 DATE            NOT NULL,
        PurchaseOrderNumber     NVARCHAR(50)    NULL,
        PurchaseOrderDate       DATE            NULL,
        DeliveryChallanNumber   NVARCHAR(50)    NULL,
        DeliveryChallanDate     DATE            NULL,
        InvoiceNumber           NVARCHAR(50)    NULL,
        InvoiceDate             DATE            NULL,
        VendorId                INT             NOT NULL,
        CourierId               INT             NULL,
        ConsignmentNumber       NVARCHAR(50)    NULL,
        WarehouseId             INT             NOT NULL,
        ReceivedBy              INT             NOT NULL,
        ReceivedOn              DATETIME2(3)    NOT NULL CONSTRAINT DF_Inward_ReceivedOn DEFAULT (SYSUTCDATETIME()),
        Status                  INT             NOT NULL CONSTRAINT DF_Inward_Status DEFAULT (1),
        ApprovedBy              INT             NULL,
        ApprovedOn              DATETIME2(3)    NULL,
        ApprovalRemarks         NVARCHAR(1000)  NULL,
        TotalValue              DECIMAL(18,2)   NOT NULL CONSTRAINT DF_Inward_TotalValue DEFAULT (0),
        TotalTaxAmount          DECIMAL(18,2)   NOT NULL CONSTRAINT DF_Inward_TotalTax DEFAULT (0),
        GrandTotal              DECIMAL(18,2)   NOT NULL CONSTRAINT DF_Inward_GrandTotal DEFAULT (0),
        Remarks                 NVARCHAR(1000)  NULL,
        CreatedOn               DATETIME2(3)    NOT NULL CONSTRAINT DF_Inward_CreatedOn DEFAULT (SYSUTCDATETIME()),
        CreatedBy               INT             NOT NULL CONSTRAINT DF_Inward_CreatedBy DEFAULT (0),
        ModifiedOn              DATETIME2(3)    NULL,
        ModifiedBy              INT             NULL,
        IsActive                BIT             NOT NULL CONSTRAINT DF_Inward_IsActive DEFAULT (1),
        IsDeleted               BIT             NOT NULL CONSTRAINT DF_Inward_IsDeleted DEFAULT (0),
        RowVersion              ROWVERSION      NOT NULL,
        CONSTRAINT PK_InventoryInwardHeader PRIMARY KEY CLUSTERED (Id),
        CONSTRAINT FK_Inward_Vendors FOREIGN KEY (VendorId) REFERENCES dbo.Vendors (Id),
        CONSTRAINT FK_Inward_Warehouses FOREIGN KEY (WarehouseId) REFERENCES dbo.Warehouses (Id),
        CONSTRAINT FK_Inward_Couriers FOREIGN KEY (CourierId) REFERENCES dbo.Couriers (Id),
        CONSTRAINT CK_Inward_Status CHECK (Status BETWEEN 1 AND 7)
    );

    CREATE UNIQUE NONCLUSTERED INDEX UX_InventoryInwardHeader_GrnNumber ON dbo.InventoryInwardHeader (GrnNumber);
    CREATE NONCLUSTERED INDEX IX_InventoryInwardHeader_Date_Status
        ON dbo.InventoryInwardHeader (GrnDate, Status) INCLUDE (VendorId, WarehouseId, GrandTotal);
    CREATE NONCLUSTERED INDEX IX_InventoryInwardHeader_VendorId ON dbo.InventoryInwardHeader (VendorId);
END
GO

IF OBJECT_ID(N'dbo.InventoryInwardDetails', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.InventoryInwardDetails
    (
        Id                  INT             IDENTITY(1,1) NOT NULL,
        InwardHeaderId      INT             NOT NULL,
        ItemId              INT             NOT NULL,
        LineNumber          INT             NOT NULL CONSTRAINT DF_InwardDet_Line DEFAULT (1),
        PartNumber          NVARCHAR(100)   NULL,
        QuantityReceived    DECIMAL(18,4)   NOT NULL,
        QuantityAccepted    DECIMAL(18,4)   NOT NULL CONSTRAINT DF_InwardDet_Accepted DEFAULT (0),
        QuantityRejected    DECIMAL(18,4)   NOT NULL CONSTRAINT DF_InwardDet_Rejected DEFAULT (0),
        RejectionReason     NVARCHAR(500)   NULL,
        BatchNumber         NVARCHAR(50)    NULL,
        SerialNumber        NVARCHAR(100)   NULL,
        ManufacturingDate   DATE            NULL,
        ExpiryDate          DATE            NULL,
        UnitCost            DECIMAL(18,4)   NOT NULL CONSTRAINT DF_InwardDet_UnitCost DEFAULT (0),
        DiscountPercent     DECIMAL(5,2)    NOT NULL CONSTRAINT DF_InwardDet_Discount DEFAULT (0),
        TaxRate             DECIMAL(5,2)    NOT NULL CONSTRAINT DF_InwardDet_TaxRate DEFAULT (0),
        TaxAmount           DECIMAL(18,2)   NOT NULL CONSTRAINT DF_InwardDet_TaxAmount DEFAULT (0),
        TotalCost           DECIMAL(18,2)   NOT NULL CONSTRAINT DF_InwardDet_TotalCost DEFAULT (0),
        ShelfLocation       NVARCHAR(50)    NULL,
        StockBefore         DECIMAL(18,4)   NOT NULL CONSTRAINT DF_InwardDet_StockBefore DEFAULT (0),
        StockAfter          DECIMAL(18,4)   NOT NULL CONSTRAINT DF_InwardDet_StockAfter DEFAULT (0),
        Remarks             NVARCHAR(500)   NULL,
        CONSTRAINT PK_InventoryInwardDetails PRIMARY KEY CLUSTERED (Id),
        CONSTRAINT FK_InwardDet_Header FOREIGN KEY (InwardHeaderId)
            REFERENCES dbo.InventoryInwardHeader (Id) ON DELETE CASCADE,
        CONSTRAINT FK_InwardDet_Items FOREIGN KEY (ItemId) REFERENCES dbo.Items (Id),
        CONSTRAINT CK_InwardDet_Quantities CHECK
            (QuantityReceived > 0 AND QuantityAccepted >= 0 AND QuantityRejected >= 0
             AND QuantityAccepted + QuantityRejected = QuantityReceived),
        CONSTRAINT CK_InwardDet_UnitCost CHECK (UnitCost >= 0)
    );

    CREATE NONCLUSTERED INDEX IX_InventoryInwardDetails_HeaderId ON dbo.InventoryInwardDetails (InwardHeaderId);
    CREATE NONCLUSTERED INDEX IX_InventoryInwardDetails_ItemId ON dbo.InventoryInwardDetails (ItemId);
END
GO

/* -----------------------------------------------------------------------------
   Inventory outward (Material Issue)
   -------------------------------------------------------------------------- */

IF OBJECT_ID(N'dbo.InventoryOutwardHeader', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.InventoryOutwardHeader
    (
        Id                  INT             IDENTITY(1,1) NOT NULL,
        IssueNumber         NVARCHAR(30)    NOT NULL,
        IssueDate           DATE            NOT NULL,
        RequestedBy         INT             NOT NULL,
        RequestedOn         DATETIME2(3)    NOT NULL CONSTRAINT DF_Outward_RequestedOn DEFAULT (SYSUTCDATETIME()),
        DepartmentId        INT             NULL,
        SiteId              INT             NULL,
        EngineerId          INT             NULL,
        WarehouseId         INT             NOT NULL,
        ProjectName         NVARCHAR(200)   NULL,
        WorkOrderNumber     NVARCHAR(50)    NULL,
        Purpose             NVARCHAR(500)   NULL,
        Status              INT             NOT NULL CONSTRAINT DF_Outward_Status DEFAULT (1),
        ApprovedBy          INT             NULL,
        ApprovedOn          DATETIME2(3)    NULL,
        ApprovalRemarks     NVARCHAR(1000)  NULL,
        IssuedBy            INT             NULL,
        IssuedOn            DATETIME2(3)    NULL,
        CourierId           INT             NULL,
        TrackingNumber      NVARCHAR(50)    NULL,
        DispatchDate        DATE            NULL,
        ReceiverName        NVARCHAR(150)   NULL,
        ClosedOn            DATETIME2(3)    NULL,
        TotalValue          DECIMAL(18,2)   NOT NULL CONSTRAINT DF_Outward_TotalValue DEFAULT (0),
        Remarks             NVARCHAR(1000)  NULL,
        CreatedOn           DATETIME2(3)    NOT NULL CONSTRAINT DF_Outward_CreatedOn DEFAULT (SYSUTCDATETIME()),
        CreatedBy           INT             NOT NULL CONSTRAINT DF_Outward_CreatedBy DEFAULT (0),
        ModifiedOn          DATETIME2(3)    NULL,
        ModifiedBy          INT             NULL,
        IsActive            BIT             NOT NULL CONSTRAINT DF_Outward_IsActive DEFAULT (1),
        IsDeleted           BIT             NOT NULL CONSTRAINT DF_Outward_IsDeleted DEFAULT (0),
        RowVersion          ROWVERSION      NOT NULL,
        CONSTRAINT PK_InventoryOutwardHeader PRIMARY KEY CLUSTERED (Id),
        CONSTRAINT FK_Outward_Warehouses FOREIGN KEY (WarehouseId) REFERENCES dbo.Warehouses (Id),
        CONSTRAINT FK_Outward_Departments FOREIGN KEY (DepartmentId) REFERENCES dbo.Departments (Id),
        CONSTRAINT FK_Outward_Sites FOREIGN KEY (SiteId) REFERENCES dbo.Sites (Id),
        CONSTRAINT FK_Outward_Engineers FOREIGN KEY (EngineerId) REFERENCES dbo.Engineers (Id),
        CONSTRAINT FK_Outward_Couriers FOREIGN KEY (CourierId) REFERENCES dbo.Couriers (Id),
        CONSTRAINT CK_Outward_Status CHECK (Status BETWEEN 1 AND 7),
        /* Every issue must have a destination. */
        CONSTRAINT CK_Outward_Destination CHECK
            (DepartmentId IS NOT NULL OR SiteId IS NOT NULL OR EngineerId IS NOT NULL)
    );

    CREATE UNIQUE NONCLUSTERED INDEX UX_InventoryOutwardHeader_IssueNumber
        ON dbo.InventoryOutwardHeader (IssueNumber);
    CREATE NONCLUSTERED INDEX IX_InventoryOutwardHeader_Date_Status
        ON dbo.InventoryOutwardHeader (IssueDate, Status)
        INCLUDE (DepartmentId, SiteId, EngineerId, WarehouseId, TotalValue);
END
GO

IF OBJECT_ID(N'dbo.InventoryOutwardDetails', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.InventoryOutwardDetails
    (
        Id                  INT             IDENTITY(1,1) NOT NULL,
        OutwardHeaderId     INT             NOT NULL,
        ItemId              INT             NOT NULL,
        LineNumber          INT             NOT NULL CONSTRAINT DF_OutwardDet_Line DEFAULT (1),
        PartNumber          NVARCHAR(100)   NULL,
        QuantityRequested   DECIMAL(18,4)   NOT NULL,
        QuantityApproved    DECIMAL(18,4)   NOT NULL CONSTRAINT DF_OutwardDet_Approved DEFAULT (0),
        QuantityIssued      DECIMAL(18,4)   NOT NULL CONSTRAINT DF_OutwardDet_Issued DEFAULT (0),
        QuantityReserved    DECIMAL(18,4)   NOT NULL CONSTRAINT DF_OutwardDet_Reserved DEFAULT (0),
        QuantityReturned    DECIMAL(18,4)   NOT NULL CONSTRAINT DF_OutwardDet_Returned DEFAULT (0),
        BatchNumber         NVARCHAR(50)    NULL,
        SerialNumber        NVARCHAR(100)   NULL,
        UnitCost            DECIMAL(18,4)   NOT NULL CONSTRAINT DF_OutwardDet_UnitCost DEFAULT (0),
        TotalCost           DECIMAL(18,2)   NOT NULL CONSTRAINT DF_OutwardDet_TotalCost DEFAULT (0),
        StockBefore         DECIMAL(18,4)   NOT NULL CONSTRAINT DF_OutwardDet_StockBefore DEFAULT (0),
        StockAfter          DECIMAL(18,4)   NOT NULL CONSTRAINT DF_OutwardDet_StockAfter DEFAULT (0),
        Remarks             NVARCHAR(500)   NULL,
        CONSTRAINT PK_InventoryOutwardDetails PRIMARY KEY CLUSTERED (Id),
        CONSTRAINT FK_OutwardDet_Header FOREIGN KEY (OutwardHeaderId)
            REFERENCES dbo.InventoryOutwardHeader (Id) ON DELETE CASCADE,
        CONSTRAINT FK_OutwardDet_Items FOREIGN KEY (ItemId) REFERENCES dbo.Items (Id),
        CONSTRAINT CK_OutwardDet_Quantities CHECK
            (QuantityRequested > 0
             AND QuantityApproved >= 0 AND QuantityApproved <= QuantityRequested
             AND QuantityIssued  >= 0 AND QuantityIssued  <= QuantityApproved
             AND QuantityReserved >= 0 AND QuantityReturned >= 0)
    );

    CREATE NONCLUSTERED INDEX IX_InventoryOutwardDetails_HeaderId ON dbo.InventoryOutwardDetails (OutwardHeaderId);
    CREATE NONCLUSTERED INDEX IX_InventoryOutwardDetails_ItemId ON dbo.InventoryOutwardDetails (ItemId);
END
GO

/* -----------------------------------------------------------------------------
   Stock ledger  -  the single source of truth for every balance.
   Append only: corrections are posted as reversals, never as updates.
   -------------------------------------------------------------------------- */

IF OBJECT_ID(N'dbo.StockLedger', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.StockLedger
    (
        Id                  BIGINT          IDENTITY(1,1) NOT NULL,
        ItemId              INT             NOT NULL,
        WarehouseId         INT             NOT NULL,
        TransactionDate     DATE            NOT NULL,
        MovementType        INT             NOT NULL,
        DocumentType        INT             NULL,
        DocumentId          INT             NULL,
        DocumentNumber      NVARCHAR(30)    NULL,
        DocumentDetailId    INT             NULL,
        InwardQuantity      DECIMAL(18,4)   NOT NULL CONSTRAINT DF_Ledger_Inward DEFAULT (0),
        OutwardQuantity     DECIMAL(18,4)   NOT NULL CONSTRAINT DF_Ledger_Outward DEFAULT (0),
        BalanceQuantity     DECIMAL(18,4)   NOT NULL CONSTRAINT DF_Ledger_Balance DEFAULT (0),
        UnitCost            DECIMAL(18,4)   NOT NULL CONSTRAINT DF_Ledger_UnitCost DEFAULT (0),
        Value               DECIMAL(18,2)   NOT NULL CONSTRAINT DF_Ledger_Value DEFAULT (0),
        BalanceAverageCost  DECIMAL(18,4)   NOT NULL CONSTRAINT DF_Ledger_AvgCost DEFAULT (0),
        BatchNumber         NVARCHAR(50)    NULL,
        SerialNumber        NVARCHAR(100)   NULL,
        Remarks             NVARCHAR(500)   NULL,
        CreatedOn           DATETIME2(3)    NOT NULL CONSTRAINT DF_Ledger_CreatedOn DEFAULT (SYSUTCDATETIME()),
        CreatedBy           INT             NOT NULL CONSTRAINT DF_Ledger_CreatedBy DEFAULT (0),
        CONSTRAINT PK_StockLedger PRIMARY KEY CLUSTERED (Id),
        CONSTRAINT FK_Ledger_Items FOREIGN KEY (ItemId) REFERENCES dbo.Items (Id),
        CONSTRAINT FK_Ledger_Warehouses FOREIGN KEY (WarehouseId) REFERENCES dbo.Warehouses (Id),
        CONSTRAINT CK_Ledger_Quantities CHECK (InwardQuantity >= 0 AND OutwardQuantity >= 0),
        /* The balance can never go negative: the rule is enforced by the
           procedures and backed up here so no path can violate it. */
        CONSTRAINT CK_Ledger_NonNegativeBalance CHECK (BalanceQuantity >= 0),
        CONSTRAINT CK_Ledger_MovementType CHECK (MovementType BETWEEN 1 AND 6)
    );

    /* The covering index every balance and ledger query relies on. */
    CREATE NONCLUSTERED INDEX IX_StockLedger_Item_Warehouse_Date
        ON dbo.StockLedger (ItemId, WarehouseId, TransactionDate, Id)
        INCLUDE (InwardQuantity, OutwardQuantity, BalanceQuantity, BalanceAverageCost, Value);

    CREATE NONCLUSTERED INDEX IX_StockLedger_Document ON dbo.StockLedger (DocumentType, DocumentId);
    CREATE NONCLUSTERED INDEX IX_StockLedger_TransactionDate ON dbo.StockLedger (TransactionDate)
        INCLUDE (ItemId, WarehouseId, InwardQuantity, OutwardQuantity, Value);
END
GO

/* -----------------------------------------------------------------------------
   Workflow, files, notifications and audit
   -------------------------------------------------------------------------- */

IF OBJECT_ID(N'dbo.ApprovalHistory', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.ApprovalHistory
    (
        Id              BIGINT          IDENTITY(1,1) NOT NULL,
        DocumentType    INT             NOT NULL,
        DocumentId      INT             NOT NULL,
        DocumentNumber  NVARCHAR(30)    NULL,
        [Action]        INT             NOT NULL,
        FromStatus      INT             NOT NULL,
        ToStatus        INT             NOT NULL,
        ActionBy        INT             NOT NULL,
        ActionByName    NVARCHAR(150)   NULL,
        ActionOn        DATETIME2(3)    NOT NULL CONSTRAINT DF_Approval_ActionOn DEFAULT (SYSUTCDATETIME()),
        Remarks         NVARCHAR(1000)  NULL,
        [Level]         INT             NOT NULL CONSTRAINT DF_Approval_Level DEFAULT (1),
        CONSTRAINT PK_ApprovalHistory PRIMARY KEY CLUSTERED (Id)
    );

    CREATE NONCLUSTERED INDEX IX_ApprovalHistory_Document ON dbo.ApprovalHistory (DocumentType, DocumentId, ActionOn);
END
GO

IF OBJECT_ID(N'dbo.Attachments', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Attachments
    (
        Id                  BIGINT          IDENTITY(1,1) NOT NULL,
        DocumentType        INT             NULL,
        DocumentId          INT             NULL,
        InwardHeaderId      INT             NULL,
        OutwardHeaderId     INT             NULL,
        FileName            NVARCHAR(260)   NOT NULL,
        StoredFileName      NVARCHAR(100)   NOT NULL,
        FilePath            NVARCHAR(500)   NOT NULL,
        ContentType         NVARCHAR(100)   NULL,
        FileSizeBytes       BIGINT          NOT NULL CONSTRAINT DF_Attachments_Size DEFAULT (0),
        Checksum            NVARCHAR(64)    NULL,
        Description         NVARCHAR(300)   NULL,
        UploadedOn          DATETIME2(3)    NOT NULL CONSTRAINT DF_Attachments_UploadedOn DEFAULT (SYSUTCDATETIME()),
        UploadedBy          INT             NOT NULL CONSTRAINT DF_Attachments_UploadedBy DEFAULT (0),
        IsDeleted           BIT             NOT NULL CONSTRAINT DF_Attachments_IsDeleted DEFAULT (0),
        CONSTRAINT PK_Attachments PRIMARY KEY CLUSTERED (Id),
        CONSTRAINT FK_Attachments_Inward FOREIGN KEY (InwardHeaderId)
            REFERENCES dbo.InventoryInwardHeader (Id) ON DELETE CASCADE,
        CONSTRAINT FK_Attachments_Outward FOREIGN KEY (OutwardHeaderId)
            REFERENCES dbo.InventoryOutwardHeader (Id) ON DELETE CASCADE,
        /* 10 MB ceiling, matching AppConstants.MaxUploadSizeBytes. */
        CONSTRAINT CK_Attachments_Size CHECK (FileSizeBytes >= 0 AND FileSizeBytes <= 10485760)
    );

    CREATE NONCLUSTERED INDEX IX_Attachments_Document ON dbo.Attachments (DocumentType, DocumentId)
        WHERE IsDeleted = 0;
END
GO

IF OBJECT_ID(N'dbo.Notifications', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Notifications
    (
        Id                  BIGINT          IDENTITY(1,1) NOT NULL,
        UserId              INT             NULL,
        TargetRole          NVARCHAR(100)   NULL,
        NotificationType    INT             NOT NULL CONSTRAINT DF_Notifications_Type DEFAULT (1),
        Title               NVARCHAR(200)   NOT NULL,
        [Message]           NVARCHAR(1000)  NOT NULL,
        ActionUrl           NVARCHAR(300)   NULL,
        DocumentType        INT             NULL,
        DocumentId          INT             NULL,
        IsRead              BIT             NOT NULL CONSTRAINT DF_Notifications_IsRead DEFAULT (0),
        ReadOn              DATETIME2(3)    NULL,
        IsEmailSent         BIT             NOT NULL CONSTRAINT DF_Notifications_EmailSent DEFAULT (0),
        EmailSentOn         DATETIME2(3)    NULL,
        CreatedOn           DATETIME2(3)    NOT NULL CONSTRAINT DF_Notifications_CreatedOn DEFAULT (SYSUTCDATETIME()),
        CreatedBy           INT             NOT NULL CONSTRAINT DF_Notifications_CreatedBy DEFAULT (0),
        CONSTRAINT PK_Notifications PRIMARY KEY CLUSTERED (Id),
        CONSTRAINT FK_Notifications_Users FOREIGN KEY (UserId) REFERENCES dbo.Users (Id) ON DELETE CASCADE
    );

    CREATE NONCLUSTERED INDEX IX_Notifications_User_Unread
        ON dbo.Notifications (UserId, IsRead, CreatedOn DESC) INCLUDE (Title, NotificationType);
END
GO

IF OBJECT_ID(N'dbo.AuditLogs', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.AuditLogs
    (
        Id              BIGINT          IDENTITY(1,1) NOT NULL,
        [Action]        INT             NOT NULL,
        EntityName      NVARCHAR(100)   NULL,
        EntityId        NVARCHAR(50)    NULL,
        Description     NVARCHAR(500)   NULL,
        OldValues       NVARCHAR(MAX)   NULL,
        NewValues       NVARCHAR(MAX)   NULL,
        UserId          INT             NULL,
        UserName        NVARCHAR(256)   NULL,
        IpAddress       NVARCHAR(45)    NULL,
        UserAgent       NVARCHAR(400)   NULL,
        Source          NVARCHAR(200)   NULL,
        IsSuccessful    BIT             NOT NULL CONSTRAINT DF_AuditLogs_Success DEFAULT (1),
        ErrorMessage    NVARCHAR(1000)  NULL,
        CreatedOn       DATETIME2(3)    NOT NULL CONSTRAINT DF_AuditLogs_CreatedOn DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT PK_AuditLogs PRIMARY KEY CLUSTERED (Id)
    );

    CREATE NONCLUSTERED INDEX IX_AuditLogs_CreatedOn ON dbo.AuditLogs (CreatedOn DESC)
        INCLUDE (Action, EntityName, UserName, Description);
    CREATE NONCLUSTERED INDEX IX_AuditLogs_Entity ON dbo.AuditLogs (EntityName, EntityId);
    CREATE NONCLUSTERED INDEX IX_AuditLogs_UserId ON dbo.AuditLogs (UserId);
END
GO

IF OBJECT_ID(N'dbo.LoginHistory', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.LoginHistory
    (
        Id              BIGINT          IDENTITY(1,1) NOT NULL,
        UserId          INT             NULL,
        UserName        NVARCHAR(256)   NOT NULL,
        LoginOn         DATETIME2(3)    NOT NULL CONSTRAINT DF_LoginHistory_LoginOn DEFAULT (SYSUTCDATETIME()),
        LogoutOn        DATETIME2(3)    NULL,
        IsSuccessful    BIT             NOT NULL CONSTRAINT DF_LoginHistory_Success DEFAULT (0),
        FailureReason   NVARCHAR(300)   NULL,
        IpAddress       NVARCHAR(45)    NULL,
        UserAgent       NVARCHAR(400)   NULL,
        SessionId       NVARCHAR(100)   NULL,
        CONSTRAINT PK_LoginHistory PRIMARY KEY CLUSTERED (Id),
        CONSTRAINT FK_LoginHistory_Users FOREIGN KEY (UserId) REFERENCES dbo.Users (Id)
    );

    CREATE NONCLUSTERED INDEX IX_LoginHistory_User_Date ON dbo.LoginHistory (UserName, LoginOn DESC);
END
GO

IF OBJECT_ID(N'dbo.DocumentSequences', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.DocumentSequences
    (
        Id              INT             IDENTITY(1,1) NOT NULL,
        SequenceKey     NVARCHAR(30)    NOT NULL,
        Prefix          NVARCHAR(20)    NOT NULL CONSTRAINT DF_Sequences_Prefix DEFAULT (''),
        FinancialYear   NVARCHAR(10)    NULL,
        CurrentNumber   INT             NOT NULL CONSTRAINT DF_Sequences_Current DEFAULT (0),
        PadWidth        INT             NOT NULL CONSTRAINT DF_Sequences_Pad DEFAULT (6),
        ModifiedOn      DATETIME2(3)    NOT NULL CONSTRAINT DF_Sequences_ModifiedOn DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT PK_DocumentSequences PRIMARY KEY CLUSTERED (Id)
    );

    /* One counter per key per financial year. NULL year = a single global counter. */
    CREATE UNIQUE NONCLUSTERED INDEX UX_DocumentSequences_Key_Year
        ON dbo.DocumentSequences (SequenceKey, FinancialYear);
END
GO

PRINT 'Schema created.';
GO
