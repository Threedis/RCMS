/* =============================================================================
   01_SeedMasters.sql  -  Reference data for a new installation

   Idempotent: every insert is guarded, so the script can be re-run after an
   upgrade without duplicating anything. Application users and their password
   hashes are seeded by DbInitializer in the web project, because hashes must be
   produced by the configured ASP.NET Core Identity password hasher.
   ========================================================================== */

SET NOCOUNT ON;
GO

/* -----------------------------------------------------------------------------
   Roles  (kept in step with Inventory.Common.Constants.Roles)
   -------------------------------------------------------------------------- */
MERGE dbo.Roles AS target
USING (VALUES
    (N'Administrator',     N'ADMINISTRATOR',     N'Full access including user management and system settings.'),
    (N'Inventory Manager', N'INVENTORY MANAGER', N'Manages master data, inventory documents and approvals.'),
    (N'Store Executive',   N'STORE EXECUTIVE',   N'Records goods receipts and material issues.'),
    (N'Approver',          N'APPROVER',          N'Approves or rejects inventory documents.'),
    (N'Project Manager',   N'PROJECT MANAGER',   N'Raises material requests and tracks site consumption.'),
    (N'Viewer',            N'VIEWER',            N'Read-only access to screens and reports.')
) AS source (Name, NormalizedName, Description)
ON target.NormalizedName = source.NormalizedName
WHEN NOT MATCHED BY TARGET THEN
    INSERT (Name, NormalizedName, ConcurrencyStamp, Description, IsSystemRole)
    VALUES (source.Name, source.NormalizedName, NEWID(), source.Description, 1);
GO

/* -----------------------------------------------------------------------------
   Units of measure
   -------------------------------------------------------------------------- */
MERGE dbo.Units AS target
USING (VALUES
    (N'UOM-0001', N'Numbers',  N'Nos', 0, 1),
    (N'UOM-0002', N'Metre',    N'Mtr', 2, 2),
    (N'UOM-0003', N'Kilogram', N'Kg',  3, 3),
    (N'UOM-0004', N'Litre',    N'Ltr', 3, 4),
    (N'UOM-0005', N'Set',      N'Set', 0, 5),
    (N'UOM-0006', N'Box',      N'Box', 0, 6),
    (N'UOM-0007', N'Roll',     N'Rol', 0, 7),
    (N'UOM-0008', N'Pair',     N'Pr',  0, 8)
) AS source (Code, Name, Symbol, DecimalPlaces, DisplayOrder)
ON target.Code = source.Code
WHEN NOT MATCHED BY TARGET THEN
    INSERT (Code, Name, Symbol, DecimalPlaces, DisplayOrder, IsActive)
    VALUES (source.Code, source.Name, source.Symbol, source.DecimalPlaces, source.DisplayOrder, 1);
GO

/* -----------------------------------------------------------------------------
   Categories
   -------------------------------------------------------------------------- */
MERGE dbo.Categories AS target
USING (VALUES
    (N'CAT-0001', N'Electrical',        N'Cables, switchgear, lighting and accessories', 1),
    (N'CAT-0002', N'Mechanical',        N'Bearings, fasteners, seals and drive components', 2),
    (N'CAT-0003', N'IT Hardware',       N'Computers, networking and peripherals', 3),
    (N'CAT-0004', N'Instrumentation',   N'Sensors, transmitters and controllers', 4),
    (N'CAT-0005', N'Consumables',       N'Adhesives, lubricants, cleaning material', 5),
    (N'CAT-0006', N'Safety Equipment',  N'Personal protective equipment', 6),
    (N'CAT-0007', N'Tools',             N'Hand tools and power tools', 7),
    (N'CAT-0008', N'Civil',             N'Construction and finishing material', 8)
) AS source (Code, Name, Description, DisplayOrder)
ON target.Code = source.Code
WHEN NOT MATCHED BY TARGET THEN
    INSERT (Code, Name, Description, DisplayOrder, IsActive)
    VALUES (source.Code, source.Name, source.Description, source.DisplayOrder, 1);
GO

/* -----------------------------------------------------------------------------
   Departments
   -------------------------------------------------------------------------- */
MERGE dbo.Departments AS target
USING (VALUES
    (N'DEP-0001', N'Projects',      1),
    (N'DEP-0002', N'Maintenance',   2),
    (N'DEP-0003', N'Operations',    3),
    (N'DEP-0004', N'Quality',       4),
    (N'DEP-0005', N'Stores',        5),
    (N'DEP-0006', N'Administration',6)
) AS source (Code, Name, DisplayOrder)
ON target.Code = source.Code
WHEN NOT MATCHED BY TARGET THEN
    INSERT (Code, Name, DisplayOrder, IsActive)
    VALUES (source.Code, source.Name, source.DisplayOrder, 1);
GO

/* -----------------------------------------------------------------------------
   Sites and warehouses
   -------------------------------------------------------------------------- */
MERGE dbo.Sites AS target
USING (VALUES
    (N'SIT-0001', N'Head Office',       N'Ahmedabad', N'Gujarat',    1),
    (N'SIT-0002', N'Central Warehouse', N'Ahmedabad', N'Gujarat',    2),
    (N'SIT-0003', N'Project Site North',N'Delhi',     N'Delhi',      3),
    (N'SIT-0004', N'Project Site West', N'Mumbai',    N'Maharashtra',4)
) AS source (Code, Name, City, [State], DisplayOrder)
ON target.Code = source.Code
WHEN NOT MATCHED BY TARGET THEN
    INSERT (Code, Name, City, [State], DisplayOrder, IsActive)
    VALUES (source.Code, source.Name, source.City, source.[State], source.DisplayOrder, 1);
GO

INSERT INTO dbo.Warehouses (Code, Name, SiteId, InchargeName, IsDefault, DisplayOrder, IsActive)
SELECT v.Code, v.Name, s.Id, v.Incharge, v.IsDefault, v.DisplayOrder, 1
FROM (VALUES
    (N'WHS-0001', N'Main Store',        N'SIT-0002', N'Store In-charge', CAST(1 AS BIT), 1),
    (N'WHS-0002', N'Spares Store',      N'SIT-0002', N'Store In-charge', CAST(0 AS BIT), 2),
    (N'WHS-0003', N'North Site Store',  N'SIT-0003', N'Site Store Keeper', CAST(0 AS BIT), 3),
    (N'WHS-0004', N'West Site Store',   N'SIT-0004', N'Site Store Keeper', CAST(0 AS BIT), 4)
) AS v (Code, Name, SiteCode, Incharge, IsDefault, DisplayOrder)
INNER JOIN dbo.Sites AS s ON s.Code = v.SiteCode
WHERE NOT EXISTS (SELECT 1 FROM dbo.Warehouses AS w WHERE w.Code = v.Code);
GO

/* -----------------------------------------------------------------------------
   Manufacturers and couriers
   -------------------------------------------------------------------------- */
MERGE dbo.Manufacturers AS target
USING (VALUES
    (N'MFR-0001', N'Siemens',           N'Germany', 1),
    (N'MFR-0002', N'SKF',               N'Sweden',  2),
    (N'MFR-0003', N'Schneider Electric',N'France',  3),
    (N'MFR-0004', N'Havells',           N'India',   4),
    (N'MFR-0005', N'Bosch',             N'Germany', 5),
    (N'MFR-0006', N'L&T',               N'India',   6)
) AS source (Code, Name, Country, DisplayOrder)
ON target.Code = source.Code
WHEN NOT MATCHED BY TARGET THEN
    INSERT (Code, Name, Country, DisplayOrder, IsActive)
    VALUES (source.Code, source.Name, source.Country, source.DisplayOrder, 1);
GO

MERGE dbo.Couriers AS target
USING (VALUES
    (N'CUR-0001', N'Blue Dart',   N'https://www.bluedart.com/tracking?awb=', 1),
    (N'CUR-0002', N'DTDC',        N'https://www.dtdc.in/tracking?awb=',      2),
    (N'CUR-0003', N'Delhivery',   N'https://www.delhivery.com/track/?awb=',  3),
    (N'CUR-0004', N'By Hand',     NULL,                                      4),
    (N'CUR-0005', N'Own Vehicle', NULL,                                      5)
) AS source (Code, Name, TrackingUrlTemplate, DisplayOrder)
ON target.Code = source.Code
WHEN NOT MATCHED BY TARGET THEN
    INSERT (Code, Name, TrackingUrlTemplate, DisplayOrder, IsActive)
    VALUES (source.Code, source.Name, source.TrackingUrlTemplate, source.DisplayOrder, 1);
GO

/* -----------------------------------------------------------------------------
   Document number sequences
   -------------------------------------------------------------------------- */
DECLARE @FinancialYear NVARCHAR(10) = dbo.fn_GetFinancialYear(CAST(SYSDATETIME() AS DATE));

INSERT INTO dbo.DocumentSequences (SequenceKey, Prefix, FinancialYear, CurrentNumber, PadWidth)
SELECT v.SequenceKey, v.Prefix, v.FinancialYear, 0, 6
FROM (VALUES
    (N'GRN',   N'GRN', @FinancialYear),
    (N'ISSUE', N'ISS', @FinancialYear),
    (N'ITEM',  N'ITM', CAST(NULL AS NVARCHAR(10)))
) AS v (SequenceKey, Prefix, FinancialYear)
WHERE NOT EXISTS
(
    SELECT 1 FROM dbo.DocumentSequences AS s
    WHERE s.SequenceKey = v.SequenceKey
      AND ((s.FinancialYear IS NULL AND v.FinancialYear IS NULL) OR s.FinancialYear = v.FinancialYear)
);
GO

PRINT 'Reference data seeded.';
GO
