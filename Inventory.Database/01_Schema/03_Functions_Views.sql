/* =============================================================================
   03_Functions_Views.sql  -  Scalar functions, views and triggers
   ========================================================================== */

SET NOCOUNT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

/* -----------------------------------------------------------------------------
   Functions
   -------------------------------------------------------------------------- */

/* Indian financial year label for a date, e.g. 2026-27 (year starts 1 April). */
CREATE OR ALTER FUNCTION dbo.fn_GetFinancialYear (@Date DATE)
RETURNS NVARCHAR(10)
WITH SCHEMABINDING
AS
BEGIN
    DECLARE @StartYear INT =
        CASE WHEN MONTH(@Date) >= 4 THEN YEAR(@Date) ELSE YEAR(@Date) - 1 END;

    RETURN CONCAT(@StartYear, '-', RIGHT(CONCAT('0', (@StartYear + 1) % 100), 2));
END
GO

/*
   Current balance of one item in one warehouse.
   The balance is the BalanceQuantity of the most recent ledger row, which is
   why the ledger index is ordered by (ItemId, WarehouseId, TransactionDate, Id).
*/
CREATE OR ALTER FUNCTION dbo.fn_GetStockBalance (@ItemId INT, @WarehouseId INT)
RETURNS DECIMAL(18,4)
WITH SCHEMABINDING
AS
BEGIN
    DECLARE @Balance DECIMAL(18,4);

    SELECT TOP (1) @Balance = l.BalanceQuantity
    FROM dbo.StockLedger AS l
    WHERE l.ItemId = @ItemId
      AND l.WarehouseId = @WarehouseId
    ORDER BY l.TransactionDate DESC, l.Id DESC;

    RETURN ISNULL(@Balance, 0);
END
GO

/*
   Quantity currently held by documents that are pending approval.
   Reserved stock is excluded from the issuable balance so two requests can
   never consume the same units.
*/
CREATE OR ALTER FUNCTION dbo.fn_GetReservedQuantity (@ItemId INT, @WarehouseId INT)
RETURNS DECIMAL(18,4)
WITH SCHEMABINDING
AS
BEGIN
    DECLARE @Reserved DECIMAL(18,4);

    SELECT @Reserved = SUM(d.QuantityReserved)
    FROM dbo.InventoryOutwardDetails AS d
    INNER JOIN dbo.InventoryOutwardHeader AS h ON h.Id = d.OutwardHeaderId
    WHERE d.ItemId = @ItemId
      AND h.WarehouseId = @WarehouseId
      AND h.Status = 2          /* PendingApproval */
      AND h.IsDeleted = 0;

    RETURN ISNULL(@Reserved, 0);
END
GO

/* Human readable label for a document status code. */
CREATE OR ALTER FUNCTION dbo.fn_GetStatusName (@Status INT)
RETURNS NVARCHAR(30)
AS
BEGIN
    RETURN CASE @Status
        WHEN 1 THEN N'Draft'
        WHEN 2 THEN N'Pending Approval'
        WHEN 3 THEN N'Approved'
        WHEN 4 THEN N'Rejected'
        WHEN 5 THEN N'Issued'
        WHEN 6 THEN N'Closed'
        WHEN 7 THEN N'Cancelled'
        ELSE N'Unknown'
    END;
END
GO

/* Human readable label for a stock movement type. */
CREATE OR ALTER FUNCTION dbo.fn_GetMovementTypeName (@MovementType INT)
RETURNS NVARCHAR(30)
AS
BEGIN
    RETURN CASE @MovementType
        WHEN 1 THEN N'Inward'
        WHEN 2 THEN N'Outward'
        WHEN 3 THEN N'Opening'
        WHEN 4 THEN N'Adjustment'
        WHEN 5 THEN N'Reversal'
        WHEN 6 THEN N'Transfer'
        ELSE N'Unknown'
    END;
END
GO

/* Human readable label for an item type. */
CREATE OR ALTER FUNCTION dbo.fn_GetItemTypeName (@ItemType INT)
RETURNS NVARCHAR(30)
AS
BEGIN
    RETURN CASE @ItemType
        WHEN 1 THEN N'Spare Part'
        WHEN 2 THEN N'Consumable'
        WHEN 3 THEN N'Tool'
        WHEN 4 THEN N'Asset'
        WHEN 5 THEN N'Raw Material'
        ELSE N'Unknown'
    END;
END
GO

/*
   Allocates the next document number for a key, atomically.
   The UPDLOCK/HOLDLOCK pair makes concurrent callers queue rather than collide,
   so numbers are gap-free and unique even under load.
*/
CREATE OR ALTER PROCEDURE dbo.sp_GetNextDocumentNumber
    @SequenceKey    NVARCHAR(30),
    @Date           DATE = NULL,
    @UseYear        BIT = 1,
    @Number         NVARCHAR(30) OUTPUT
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @FinancialYear NVARCHAR(10) =
        CASE WHEN @UseYear = 1 THEN dbo.fn_GetFinancialYear(ISNULL(@Date, CAST(SYSDATETIME() AS DATE))) END;

    DECLARE @Prefix NVARCHAR(20), @Next INT, @Pad INT;

    /* Create the counter on first use so a new financial year needs no setup. */
    IF NOT EXISTS (
        SELECT 1 FROM dbo.DocumentSequences WITH (UPDLOCK, HOLDLOCK)
        WHERE SequenceKey = @SequenceKey
          AND ((FinancialYear IS NULL AND @FinancialYear IS NULL) OR FinancialYear = @FinancialYear))
    BEGIN
        INSERT INTO dbo.DocumentSequences (SequenceKey, Prefix, FinancialYear, CurrentNumber, PadWidth)
        VALUES (@SequenceKey, UPPER(LEFT(@SequenceKey, 3)), @FinancialYear, 0, 6);
    END

    UPDATE s
        SET s.CurrentNumber = s.CurrentNumber + 1,
            s.ModifiedOn    = SYSUTCDATETIME(),
            @Next           = s.CurrentNumber + 1,
            @Prefix         = s.Prefix,
            @Pad            = s.PadWidth
    FROM dbo.DocumentSequences AS s WITH (UPDLOCK, ROWLOCK)
    WHERE s.SequenceKey = @SequenceKey
      AND ((s.FinancialYear IS NULL AND @FinancialYear IS NULL) OR s.FinancialYear = @FinancialYear);

    SET @Number =
        CASE
            WHEN @FinancialYear IS NULL
                THEN CONCAT(@Prefix, '/', RIGHT(REPLICATE('0', @Pad) + CAST(@Next AS NVARCHAR(20)), @Pad))
            ELSE CONCAT(@Prefix, '/', @FinancialYear, '/',
                        RIGHT(REPLICATE('0', @Pad) + CAST(@Next AS NVARCHAR(20)), @Pad))
        END;
END
GO

/* -----------------------------------------------------------------------------
   Views
   -------------------------------------------------------------------------- */

/*
   Current stock per item and warehouse.
   The ledger row with the highest (TransactionDate, Id) per item/warehouse
   carries the running balance, so the view only has to find that row.
*/
CREATE OR ALTER VIEW dbo.vw_CurrentStock
AS
WITH LatestLedger AS
(
    SELECT
        l.ItemId,
        l.WarehouseId,
        l.BalanceQuantity,
        l.BalanceAverageCost,
        l.TransactionDate,
        ROW_NUMBER() OVER (
            PARTITION BY l.ItemId, l.WarehouseId
            ORDER BY l.TransactionDate DESC, l.Id DESC) AS rn
    FROM dbo.StockLedger AS l
)
SELECT
    i.Id                            AS ItemId,
    i.ItemCode,
    i.ItemName,
    i.PartNumber,
    i.ItemType,
    c.Name                          AS CategoryName,
    c.Id                            AS CategoryId,
    u.Symbol                        AS UnitSymbol,
    w.Id                            AS WarehouseId,
    w.Name                          AS WarehouseName,
    s.Id                            AS SiteId,
    s.Name                          AS SiteName,
    ll.BalanceQuantity              AS Quantity,
    dbo.fn_GetReservedQuantity(i.Id, w.Id) AS ReservedQuantity,
    ll.BalanceQuantity - dbo.fn_GetReservedQuantity(i.Id, w.Id) AS AvailableQuantity,
    CASE WHEN ll.BalanceAverageCost > 0 THEN ll.BalanceAverageCost ELSE i.AverageCost END AS AverageCost,
    CAST(ll.BalanceQuantity *
         CASE WHEN ll.BalanceAverageCost > 0 THEN ll.BalanceAverageCost ELSE i.AverageCost END
         AS DECIMAL(18,2))          AS StockValue,
    i.ReorderLevel,
    CAST(CASE WHEN ll.BalanceQuantity <= i.ReorderLevel THEN 1 ELSE 0 END AS BIT) AS IsLowStock,
    ll.TransactionDate              AS LastMovementDate,
    i.ShelfLocation
FROM LatestLedger AS ll
INNER JOIN dbo.Items      AS i ON i.Id = ll.ItemId
INNER JOIN dbo.Categories AS c ON c.Id = i.CategoryId
INNER JOIN dbo.Units      AS u ON u.Id = i.UnitId
INNER JOIN dbo.Warehouses AS w ON w.Id = ll.WarehouseId
INNER JOIN dbo.Sites      AS s ON s.Id = w.SiteId
WHERE ll.rn = 1
  AND i.IsDeleted = 0
  AND w.IsDeleted = 0;
GO

/* Item stock rolled up across every warehouse. */
CREATE OR ALTER VIEW dbo.vw_ItemStockSummary
AS
SELECT
    i.Id                                    AS ItemId,
    i.ItemCode,
    i.ItemName,
    i.PartNumber,
    i.CategoryId,
    i.UnitId,
    i.ItemType,
    i.ReorderLevel,
    i.AverageCost,
    ISNULL(SUM(cs.Quantity), 0)             AS TotalQuantity,
    ISNULL(SUM(cs.ReservedQuantity), 0)     AS TotalReserved,
    ISNULL(SUM(cs.AvailableQuantity), 0)    AS TotalAvailable,
    ISNULL(SUM(cs.StockValue), 0)           AS TotalValue,
    MAX(cs.LastMovementDate)                AS LastMovementDate
FROM dbo.Items AS i
LEFT JOIN dbo.vw_CurrentStock AS cs ON cs.ItemId = i.Id
WHERE i.IsDeleted = 0
GROUP BY
    i.Id, i.ItemCode, i.ItemName, i.PartNumber, i.CategoryId,
    i.UnitId, i.ItemType, i.ReorderLevel, i.AverageCost;
GO

/* Every document waiting for an approval decision, with its age in days. */
CREATE OR ALTER VIEW dbo.vw_PendingApprovals
AS
SELECT
    1                                       AS DocumentType,
    N'Goods Receipt Note'                   AS DocumentTypeName,
    h.Id                                    AS DocumentId,
    h.GrnNumber                             AS DocumentNumber,
    h.GrnDate                               AS DocumentDate,
    v.Name                                  AS PartyName,
    w.Name                                  AS WarehouseName,
    (SELECT COUNT(*) FROM dbo.InventoryInwardDetails d WHERE d.InwardHeaderId = h.Id) AS LineCount,
    h.GrandTotal                            AS TotalValue,
    u.FullName                              AS RequestedByName,
    h.ModifiedOn                            AS SubmittedOn,
    DATEDIFF(DAY, ISNULL(h.ModifiedOn, h.CreatedOn), SYSUTCDATETIME()) AS AgeInDays
FROM dbo.InventoryInwardHeader AS h
INNER JOIN dbo.Vendors    AS v ON v.Id = h.VendorId
INNER JOIN dbo.Warehouses AS w ON w.Id = h.WarehouseId
LEFT  JOIN dbo.Users      AS u ON u.Id = h.ReceivedBy
WHERE h.Status = 2 AND h.IsDeleted = 0

UNION ALL

SELECT
    2                                       AS DocumentType,
    N'Material Issue'                       AS DocumentTypeName,
    h.Id                                    AS DocumentId,
    h.IssueNumber                           AS DocumentNumber,
    h.IssueDate                             AS DocumentDate,
    COALESCE(e.Name, d.Name, s.Name)        AS PartyName,
    w.Name                                  AS WarehouseName,
    (SELECT COUNT(*) FROM dbo.InventoryOutwardDetails od WHERE od.OutwardHeaderId = h.Id) AS LineCount,
    h.TotalValue                            AS TotalValue,
    u.FullName                              AS RequestedByName,
    h.ModifiedOn                            AS SubmittedOn,
    DATEDIFF(DAY, ISNULL(h.ModifiedOn, h.CreatedOn), SYSUTCDATETIME()) AS AgeInDays
FROM dbo.InventoryOutwardHeader AS h
INNER JOIN dbo.Warehouses   AS w ON w.Id = h.WarehouseId
LEFT  JOIN dbo.Departments  AS d ON d.Id = h.DepartmentId
LEFT  JOIN dbo.Sites        AS s ON s.Id = h.SiteId
LEFT  JOIN dbo.Engineers    AS e ON e.Id = h.EngineerId
LEFT  JOIN dbo.Users        AS u ON u.Id = h.RequestedBy
WHERE h.Status = 2 AND h.IsDeleted = 0;
GO

/* Items at or below their reorder level, with the shortfall to order. */
CREATE OR ALTER VIEW dbo.vw_LowStockItems
AS
SELECT
    cs.ItemId,
    cs.ItemCode,
    cs.ItemName,
    cs.CategoryName,
    cs.UnitSymbol,
    cs.WarehouseId,
    cs.WarehouseName,
    cs.Quantity                             AS CurrentStock,
    cs.ReorderLevel,
    i.ReorderQuantity,
    CASE WHEN cs.ReorderLevel > cs.Quantity
         THEN cs.ReorderLevel - cs.Quantity ELSE 0 END AS Shortfall,
    cs.AverageCost
FROM dbo.vw_CurrentStock AS cs
INNER JOIN dbo.Items AS i ON i.Id = cs.ItemId
WHERE cs.IsLowStock = 1
  AND i.IsActive = 1;
GO

/* -----------------------------------------------------------------------------
   Triggers
   -------------------------------------------------------------------------- */

/*
   The stock ledger is append only. Corrections must be posted as reversal rows
   so history is never rewritten; this trigger makes that structural rather than
   a matter of discipline.
*/
CREATE OR ALTER TRIGGER dbo.trg_StockLedger_PreventChange
ON dbo.StockLedger
INSTEAD OF UPDATE, DELETE
AS
BEGIN
    SET NOCOUNT ON;

    THROW 50001,
        N'The stock ledger is append-only. Post a reversal entry instead of updating or deleting a row.',
        1;
END
GO

/*
   Keeps only one primary image per item without the application having to
   remember to clear the previous one.
*/
CREATE OR ALTER TRIGGER dbo.trg_ItemImages_SinglePrimary
ON dbo.ItemImages
AFTER INSERT, UPDATE
AS
BEGIN
    SET NOCOUNT ON;

    IF NOT UPDATE(IsPrimary) AND NOT EXISTS (SELECT 1 FROM inserted WHERE IsPrimary = 1)
    BEGIN
        RETURN;
    END

    UPDATE img
        SET img.IsPrimary = 0
    FROM dbo.ItemImages AS img
    INNER JOIN inserted AS i ON i.ItemId = img.ItemId
    WHERE i.IsPrimary = 1
      AND img.Id <> i.Id
      AND img.IsPrimary = 1;
END
GO

/*
   Records every change of a document's status on the audit trail, so the audit
   log is complete even for a change made outside the application procedures.
*/
CREATE OR ALTER TRIGGER dbo.trg_InwardHeader_AuditStatus
ON dbo.InventoryInwardHeader
AFTER UPDATE
AS
BEGIN
    SET NOCOUNT ON;

    IF NOT UPDATE(Status)
    BEGIN
        RETURN;
    END

    INSERT INTO dbo.AuditLogs (Action, EntityName, EntityId, Description, OldValues, NewValues, UserId, UserName, Source)
    SELECT
        13,                             /* StockUpdate / document transition */
        N'GRN',
        CAST(i.Id AS NVARCHAR(50)),
        CONCAT(N'GRN ', i.GrnNumber, N' moved from ',
               dbo.fn_GetStatusName(d.Status), N' to ', dbo.fn_GetStatusName(i.Status), N'.'),
        CONCAT(N'{"Status":"', dbo.fn_GetStatusName(d.Status), N'"}'),
        CONCAT(N'{"Status":"', dbo.fn_GetStatusName(i.Status), N'"}'),
        i.ModifiedBy,
        NULL,
        N'trg_InwardHeader_AuditStatus'
    FROM inserted AS i
    INNER JOIN deleted AS d ON d.Id = i.Id
    WHERE i.Status <> d.Status;
END
GO

CREATE OR ALTER TRIGGER dbo.trg_OutwardHeader_AuditStatus
ON dbo.InventoryOutwardHeader
AFTER UPDATE
AS
BEGIN
    SET NOCOUNT ON;

    IF NOT UPDATE(Status)
    BEGIN
        RETURN;
    END

    INSERT INTO dbo.AuditLogs (Action, EntityName, EntityId, Description, OldValues, NewValues, UserId, UserName, Source)
    SELECT
        13,
        N'Issue',
        CAST(i.Id AS NVARCHAR(50)),
        CONCAT(N'Issue ', i.IssueNumber, N' moved from ',
               dbo.fn_GetStatusName(d.Status), N' to ', dbo.fn_GetStatusName(i.Status), N'.'),
        CONCAT(N'{"Status":"', dbo.fn_GetStatusName(d.Status), N'"}'),
        CONCAT(N'{"Status":"', dbo.fn_GetStatusName(i.Status), N'"}'),
        i.ModifiedBy,
        NULL,
        N'trg_OutwardHeader_AuditStatus'
    FROM inserted AS i
    INNER JOIN deleted AS d ON d.Id = i.Id
    WHERE i.Status <> d.Status;
END
GO

PRINT 'Functions, views and triggers created.';
GO
