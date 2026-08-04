/* =============================================================================
   07_Dashboard_Notifications.sql  -  Dashboard aggregates and notifications
   ========================================================================== */

SET NOCOUNT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

/* -----------------------------------------------------------------------------
   sp_GetDashboard
   Returns eleven result sets in one round trip so the whole executive
   dashboard - KPI cards, five charts and three panels - loads with a single
   call rather than eleven.

     1  KPI card values
     2  Stock value by category      (donut)
     3  Stock value by warehouse     (bar)
     4  Twelve month inward/outward  (line)
     5  Top vendors                  (bar)
     6  Fast moving items            (bar)
     7  Slow moving items            (bar)
     8  Document status split        (pie)
     9  Low stock panel
    10  Pending approvals panel
    11  Recent activity feed
   -------------------------------------------------------------------------- */
CREATE OR ALTER PROCEDURE dbo.sp_GetDashboard
    @CurrentUserId  INT = 0,
    @DeadStockDays  INT = 180
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @Today          DATE = CAST(SYSDATETIME() AS DATE);
    DECLARE @MonthStart     DATE = DATEFROMPARTS(YEAR(@Today), MONTH(@Today), 1);
    DECLARE @PrevMonthStart DATE = DATEADD(MONTH, -1, @MonthStart);
    DECLARE @TwelveMonths   DATE = DATEADD(MONTH, -11, @MonthStart);

    /* ---- 1. KPI cards ------------------------------------------------- */
    DECLARE @MonthConsumption   DECIMAL(18,2),
            @PrevConsumption    DECIMAL(18,2),
            @MonthPurchase      DECIMAL(18,2),
            @PrevPurchase       DECIMAL(18,2);

    SELECT
        @MonthConsumption = ISNULL(SUM(CASE WHEN l.TransactionDate >= @MonthStart
                                            AND l.MovementType = 2 THEN l.Value END), 0),
        @PrevConsumption  = ISNULL(SUM(CASE WHEN l.TransactionDate >= @PrevMonthStart
                                            AND l.TransactionDate <  @MonthStart
                                            AND l.MovementType = 2 THEN l.Value END), 0),
        @MonthPurchase    = ISNULL(SUM(CASE WHEN l.TransactionDate >= @MonthStart
                                            AND l.MovementType = 1 THEN l.Value END), 0),
        @PrevPurchase     = ISNULL(SUM(CASE WHEN l.TransactionDate >= @PrevMonthStart
                                            AND l.TransactionDate <  @MonthStart
                                            AND l.MovementType = 1 THEN l.Value END), 0)
    FROM dbo.StockLedger AS l
    WHERE l.TransactionDate >= @PrevMonthStart;

    SELECT
        ISNULL((SELECT SUM(StockValue) FROM dbo.vw_CurrentStock), 0)            AS TotalInventoryValue,
        ISNULL((SELECT SUM(Quantity)   FROM dbo.vw_CurrentStock), 0)            AS TotalStockQuantity,
        (SELECT COUNT(*) FROM dbo.Items WHERE IsDeleted = 0)                    AS TotalItems,
        (SELECT COUNT(*) FROM dbo.Items WHERE IsDeleted = 0 AND ItemType = 1)   AS TotalSpareParts,
        (SELECT COUNT(*) FROM dbo.Items WHERE IsDeleted = 0 AND ItemType = 2)   AS TotalConsumables,
        (SELECT COUNT(*) FROM dbo.Vendors WHERE IsDeleted = 0 AND IsActive = 1) AS TotalVendors,
        (SELECT COUNT(*) FROM dbo.Warehouses WHERE IsDeleted = 0 AND IsActive = 1) AS TotalWarehouses,

        (SELECT COUNT(*) FROM dbo.InventoryInwardHeader
          WHERE GrnDate = @Today AND IsDeleted = 0)                             AS TodayInwardCount,
        ISNULL((SELECT SUM(GrandTotal) FROM dbo.InventoryInwardHeader
                 WHERE GrnDate = @Today AND IsDeleted = 0), 0)                  AS TodayInwardValue,

        (SELECT COUNT(*) FROM dbo.InventoryOutwardHeader
          WHERE IssueDate = @Today AND IsDeleted = 0)                           AS TodayOutwardCount,
        ISNULL((SELECT SUM(TotalValue) FROM dbo.InventoryOutwardHeader
                 WHERE IssueDate = @Today AND IsDeleted = 0), 0)                AS TodayOutwardValue,

        (SELECT COUNT(*) FROM dbo.vw_PendingApprovals)                          AS PendingApprovalCount,
        (SELECT COUNT(*) FROM dbo.vw_LowStockItems)                             AS LowStockCount,

        ISNULL(dead.DeadCount, 0)                                               AS DeadStockCount,
        ISNULL(dead.DeadValue, 0)                                               AS DeadStockValue,
        ISNULL(velocity.FastCount, 0)                                           AS FastMovingCount,
        ISNULL(velocity.SlowCount, 0)                                           AS SlowMovingCount,

        @MonthConsumption                                                       AS MonthConsumptionValue,
        @MonthPurchase                                                          AS MonthPurchaseValue,
        CASE WHEN @PrevConsumption > 0
             THEN CAST((@MonthConsumption - @PrevConsumption) * 100.0 / @PrevConsumption AS DECIMAL(18,2))
             ELSE 0 END                                                         AS ConsumptionChangePercent,
        CASE WHEN @PrevPurchase > 0
             THEN CAST((@MonthPurchase - @PrevPurchase) * 100.0 / @PrevPurchase AS DECIMAL(18,2))
             ELSE 0 END                                                         AS PurchaseChangePercent
    FROM
    (
        SELECT
            COUNT(*)                    AS DeadCount,
            ISNULL(SUM(s.TotalValue),0) AS DeadValue
        FROM dbo.vw_ItemStockSummary AS s
        WHERE s.TotalQuantity > 0
          AND (s.LastMovementDate IS NULL
               OR DATEDIFF(DAY, s.LastMovementDate, @Today) >= @DeadStockDays)
    ) AS dead
    CROSS JOIN
    (
        SELECT
            SUM(CASE WHEN x.IssueCount >= 12 THEN 1 ELSE 0 END) AS FastCount,
            SUM(CASE WHEN x.IssueCount <  12 THEN 1 ELSE 0 END) AS SlowCount
        FROM
        (
            SELECT l.ItemId, COUNT(*) AS IssueCount
            FROM dbo.StockLedger AS l
            WHERE l.MovementType = 2 AND l.TransactionDate >= DATEADD(YEAR, -1, @Today)
            GROUP BY l.ItemId
        ) AS x
    ) AS velocity;

    /* ---- 2. Stock value by category ----------------------------------- */
    SELECT TOP (10)
        cs.CategoryName             AS Label,
        SUM(cs.StockValue)          AS Value,
        SUM(cs.Quantity)            AS SecondaryValue,
        MAX(cs.CategoryId)          AS EntityId
    FROM dbo.vw_CurrentStock AS cs
    GROUP BY cs.CategoryName
    HAVING SUM(cs.StockValue) > 0
    ORDER BY Value DESC;

    /* ---- 3. Stock value by warehouse ---------------------------------- */
    SELECT TOP (10)
        cs.WarehouseName            AS Label,
        SUM(cs.StockValue)          AS Value,
        SUM(cs.Quantity)            AS SecondaryValue,
        MAX(cs.WarehouseId)         AS EntityId
    FROM dbo.vw_CurrentStock AS cs
    GROUP BY cs.WarehouseName
    HAVING SUM(cs.StockValue) > 0
    ORDER BY Value DESC;

    /* ---- 4. Twelve month inward / outward trend ------------------------ */
    ;WITH Months AS
    (
        SELECT @TwelveMonths AS MonthStart
        UNION ALL
        SELECT DATEADD(MONTH, 1, MonthStart) FROM Months WHERE MonthStart < @MonthStart
    )
    SELECT
        YEAR(m.MonthStart)                                              AS [Year],
        MONTH(m.MonthStart)                                             AS [Month],
        CONCAT(LEFT(DATENAME(MONTH, m.MonthStart), 3), ' ', YEAR(m.MonthStart)) AS MonthLabel,
        ISNULL(SUM(CASE WHEN l.MovementType = 1 THEN l.Value END), 0)   AS InwardValue,
        ISNULL(SUM(CASE WHEN l.MovementType = 2 THEN l.Value END), 0)   AS OutwardValue,
        ISNULL(SUM(l.InwardQuantity), 0)                                AS InwardQuantity,
        ISNULL(SUM(l.OutwardQuantity), 0)                               AS OutwardQuantity
    FROM Months AS m
    LEFT JOIN dbo.StockLedger AS l
           ON l.TransactionDate >= m.MonthStart
          AND l.TransactionDate <  DATEADD(MONTH, 1, m.MonthStart)
          AND l.MovementType IN (1, 2)
    GROUP BY YEAR(m.MonthStart), MONTH(m.MonthStart), DATENAME(MONTH, m.MonthStart), m.MonthStart
    ORDER BY m.MonthStart
    OPTION (MAXRECURSION 24);

    /* ---- 5. Top vendors (current financial year) ---------------------- */
    SELECT TOP (10)
        v.Name                  AS Label,
        SUM(h.GrandTotal)       AS Value,
        COUNT(*)                AS SecondaryValue,
        v.Id                    AS EntityId
    FROM dbo.InventoryInwardHeader AS h
    INNER JOIN dbo.Vendors AS v ON v.Id = h.VendorId
    WHERE h.IsDeleted = 0 AND h.Status = 3
      AND dbo.fn_GetFinancialYear(h.GrnDate) = dbo.fn_GetFinancialYear(@Today)
    GROUP BY v.Id, v.Name
    ORDER BY Value DESC;

    /* ---- 6. Fast moving items ----------------------------------------- */
    SELECT TOP (10)
        i.ItemName              AS Label,
        SUM(l.OutwardQuantity)  AS Value,
        COUNT(*)                AS SecondaryValue,
        i.Id                    AS EntityId
    FROM dbo.StockLedger AS l
    INNER JOIN dbo.Items AS i ON i.Id = l.ItemId
    WHERE l.MovementType = 2 AND l.TransactionDate >= DATEADD(MONTH, -3, @Today)
    GROUP BY i.Id, i.ItemName
    ORDER BY SecondaryValue DESC, Value DESC;

    /* ---- 7. Slow moving items ----------------------------------------- */
    SELECT TOP (10)
        s.ItemName              AS Label,
        s.TotalValue            AS Value,
        s.TotalQuantity         AS SecondaryValue,
        s.ItemId                AS EntityId
    FROM dbo.vw_ItemStockSummary AS s
    WHERE s.TotalQuantity > 0
      AND (s.LastMovementDate IS NULL OR DATEDIFF(DAY, s.LastMovementDate, @Today) >= 60)
    ORDER BY s.TotalValue DESC;

    /* ---- 8. Document status split ------------------------------------- */
    SELECT
        dbo.fn_GetStatusName(x.Status)  AS Label,
        CAST(COUNT(*) AS DECIMAL(18,2)) AS Value,
        CAST(0 AS DECIMAL(18,2))        AS SecondaryValue,
        MIN(x.Status)                   AS EntityId
    FROM
    (
        SELECT Status FROM dbo.InventoryInwardHeader  WHERE IsDeleted = 0
        UNION ALL
        SELECT Status FROM dbo.InventoryOutwardHeader WHERE IsDeleted = 0
    ) AS x
    GROUP BY x.Status
    ORDER BY MIN(x.Status);

    /* ---- 9. Low stock panel ------------------------------------------- */
    SELECT TOP (10)
        ls.ItemId, ls.ItemCode, ls.ItemName, ls.CategoryName, ls.UnitSymbol,
        ls.WarehouseName, ls.CurrentStock, ls.ReorderLevel, ls.ReorderQuantity,
        ls.Shortfall, ls.AverageCost,
        CAST(NULL AS NVARCHAR(150)) AS PreferredVendorName
    FROM dbo.vw_LowStockItems AS ls
    ORDER BY ls.Shortfall DESC;

    /* ---- 10. Pending approvals panel ---------------------------------- */
    SELECT TOP (10)
        p.DocumentId, p.DocumentType, p.DocumentTypeName, p.DocumentNumber, p.DocumentDate,
        p.PartyName, p.WarehouseName, p.LineCount, p.TotalValue, p.RequestedByName,
        ISNULL(p.SubmittedOn, SYSUTCDATETIME()) AS SubmittedOn, p.AgeInDays
    FROM dbo.vw_PendingApprovals AS p
    ORDER BY p.AgeInDays DESC;

    /* ---- 11. Recent activity feed ------------------------------------- */
    SELECT TOP (15)
        a.CreatedOn         AS ActivityOn,
        CASE a.Action
            WHEN 1  THEN N'Login'        WHEN 4  THEN N'Create'
            WHEN 5  THEN N'Update'       WHEN 6  THEN N'Delete'
            WHEN 7  THEN N'Approve'      WHEN 8  THEN N'Reject'
            WHEN 9  THEN N'Submit'       WHEN 10 THEN N'Issue'
            WHEN 11 THEN N'Export'       WHEN 12 THEN N'Import'
            WHEN 13 THEN N'Stock Update' ELSE N'Activity'
        END                 AS ActionName,
        a.EntityName,
        a.Description,
        a.UserName,
        CASE a.EntityName
            WHEN N'GRN'   THEN N'bi-box-arrow-in-down'
            WHEN N'Issue' THEN N'bi-box-arrow-up'
            WHEN N'Item'  THEN N'bi-box-seam'
            WHEN N'User'  THEN N'bi-person'
            ELSE N'bi-activity'
        END                 AS Icon,
        CASE a.EntityName
            WHEN N'GRN'   THEN CONCAT(N'/Grn/Details/', a.EntityId)
            WHEN N'Issue' THEN CONCAT(N'/Issue/Details/', a.EntityId)
            WHEN N'Item'  THEN CONCAT(N'/Item/Edit/', a.EntityId)
            ELSE NULL
        END                 AS ActionUrl
    FROM dbo.AuditLogs AS a
    WHERE a.Action IN (4, 5, 6, 7, 8, 9, 10, 13)
    ORDER BY a.CreatedOn DESC;
END
GO

/* -----------------------------------------------------------------------------
   sp_GetDashboardCharts  -  chart data alone, for the auto-refresh poller
   -------------------------------------------------------------------------- */
CREATE OR ALTER PROCEDURE dbo.sp_GetDashboardCharts
AS
BEGIN
    SET NOCOUNT ON;

    SELECT TOP (10)
        cs.CategoryName AS Label, SUM(cs.StockValue) AS Value,
        SUM(cs.Quantity) AS SecondaryValue, MAX(cs.CategoryId) AS EntityId
    FROM dbo.vw_CurrentStock AS cs
    GROUP BY cs.CategoryName
    HAVING SUM(cs.StockValue) > 0
    ORDER BY Value DESC;
END
GO

/* -----------------------------------------------------------------------------
   sp_GetRecentActivities
   -------------------------------------------------------------------------- */
CREATE OR ALTER PROCEDURE dbo.sp_GetRecentActivities
    @MaxRows INT = 15
AS
BEGIN
    SET NOCOUNT ON;

    SELECT TOP (@MaxRows)
        a.CreatedOn AS ActivityOn,
        CASE a.Action
            WHEN 1  THEN N'Login'        WHEN 4  THEN N'Create'
            WHEN 5  THEN N'Update'       WHEN 6  THEN N'Delete'
            WHEN 7  THEN N'Approve'      WHEN 8  THEN N'Reject'
            WHEN 9  THEN N'Submit'       WHEN 10 THEN N'Issue'
            WHEN 11 THEN N'Export'       WHEN 12 THEN N'Import'
            WHEN 13 THEN N'Stock Update' ELSE N'Activity'
        END         AS ActionName,
        a.EntityName, a.Description, a.UserName,
        CASE a.EntityName
            WHEN N'GRN'   THEN N'bi-box-arrow-in-down'
            WHEN N'Issue' THEN N'bi-box-arrow-up'
            WHEN N'Item'  THEN N'bi-box-seam'
            ELSE N'bi-activity'
        END         AS Icon,
        CASE a.EntityName
            WHEN N'GRN'   THEN CONCAT(N'/Grn/Details/', a.EntityId)
            WHEN N'Issue' THEN CONCAT(N'/Issue/Details/', a.EntityId)
            ELSE NULL
        END         AS ActionUrl
    FROM dbo.AuditLogs AS a
    WHERE a.Action IN (4, 5, 6, 7, 8, 9, 10, 13)
    ORDER BY a.CreatedOn DESC;
END
GO

/* -----------------------------------------------------------------------------
   sp_InsertNotification
   A role-targeted notification is fanned out to one row per active member, so
   the unread count stays a simple indexed count per user.
   -------------------------------------------------------------------------- */
CREATE OR ALTER PROCEDURE dbo.sp_InsertNotification
    @UserId             INT             = NULL,
    @TargetRole         NVARCHAR(100)   = NULL,
    @NotificationType   INT             = 1,
    @Title              NVARCHAR(200),
    @Body               NVARCHAR(1000),
    @ActionUrl          NVARCHAR(300)   = NULL,
    @DocumentType       INT             = NULL,
    @DocumentId         INT             = NULL,
    @CurrentUserId      INT             = 0,
    @ReturnCode         INT             OUTPUT,
    @Message            NVARCHAR(500)   OUTPUT,
    @NewId              INT             OUTPUT,
    @GeneratedNumber    NVARCHAR(30)    OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    SET @ReturnCode = 0; SET @Message = N''; SET @NewId = NULL; SET @GeneratedNumber = NULL;

    BEGIN TRY
        IF @UserId IS NULL AND @TargetRole IS NULL
        BEGIN
            SET @ReturnCode = -400;
            SET @Message = N'A notification needs either a recipient or a target role.';
            RETURN;
        END

        IF @UserId IS NOT NULL
        BEGIN
            INSERT INTO dbo.Notifications
                (UserId, TargetRole, NotificationType, Title, [Message], ActionUrl,
                 DocumentType, DocumentId, CreatedBy)
            VALUES
                (@UserId, @TargetRole, @NotificationType, @Title, @Body, @ActionUrl,
                 @DocumentType, @DocumentId, @CurrentUserId);

            SET @NewId = CAST(SCOPE_IDENTITY() AS INT);
        END
        ELSE
        BEGIN
            INSERT INTO dbo.Notifications
                (UserId, TargetRole, NotificationType, Title, [Message], ActionUrl,
                 DocumentType, DocumentId, CreatedBy)
            SELECT
                u.Id, @TargetRole, @NotificationType, @Title, @Body, @ActionUrl,
                @DocumentType, @DocumentId, @CurrentUserId
            FROM dbo.Users AS u
            INNER JOIN dbo.UserRoles AS ur ON ur.UserId = u.Id
            INNER JOIN dbo.Roles     AS r  ON r.Id = ur.RoleId
            WHERE r.Name = @TargetRole
              AND u.IsActive = 1
              AND u.IsDeleted = 0
              /* Do not notify the person who caused the event. */
              AND u.Id <> @CurrentUserId;
        END

        SET @Message = N'Notification created.';
    END TRY
    BEGIN CATCH
        /* A notification failure must never fail the business transaction. */
        SET @ReturnCode = 0;
        SET @Message = N'The notification could not be created.';

        INSERT INTO dbo.AuditLogs (Action, EntityName, Description, UserId, IsSuccessful, ErrorMessage, Source)
        VALUES (15, N'Notification', @Title, @CurrentUserId, 0, ERROR_MESSAGE(), N'sp_InsertNotification');
    END CATCH
END
GO

/* -----------------------------------------------------------------------------
   sp_GetNotifications
   -------------------------------------------------------------------------- */
CREATE OR ALTER PROCEDURE dbo.sp_GetNotifications
    @UserId     INT,
    @UnreadOnly BIT = 0,
    @MaxRows    INT = 20
AS
BEGIN
    SET NOCOUNT ON;

    SELECT TOP (@MaxRows)
        n.Id,
        n.NotificationType,
        CASE n.NotificationType
            WHEN 1 THEN N'Information'      WHEN 2 THEN N'Low Stock'
            WHEN 3 THEN N'Pending Approval' WHEN 4 THEN N'Approved'
            WHEN 5 THEN N'Rejected'         WHEN 6 THEN N'Material Receipt'
            WHEN 7 THEN N'Material Dispatch' WHEN 8 THEN N'System Alert'
            ELSE N'Information'
        END             AS NotificationTypeName,
        n.Title,
        n.[Message],
        n.ActionUrl,
        n.IsRead,
        n.CreatedOn
    FROM dbo.Notifications AS n
    WHERE n.UserId = @UserId
      AND (@UnreadOnly = 0 OR n.IsRead = 0)
    ORDER BY n.CreatedOn DESC;
END
GO

/* -----------------------------------------------------------------------------
   sp_MarkNotificationRead
   -------------------------------------------------------------------------- */
CREATE OR ALTER PROCEDURE dbo.sp_MarkNotificationRead
    @NotificationId     BIGINT          = NULL,
    @UserId             INT,
    @MarkAll            BIT             = 0,
    @ReturnCode         INT             OUTPUT,
    @Message            NVARCHAR(500)   OUTPUT,
    @NewId              INT             OUTPUT,
    @GeneratedNumber    NVARCHAR(30)    OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    SET @ReturnCode = 0; SET @Message = N''; SET @NewId = NULL; SET @GeneratedNumber = NULL;

    BEGIN TRY
        IF @MarkAll = 1
        BEGIN
            UPDATE dbo.Notifications
               SET IsRead = 1, ReadOn = SYSUTCDATETIME()
             WHERE UserId = @UserId AND IsRead = 0;

            SET @Message = N'All notifications marked as read.';
            RETURN;
        END

        IF @NotificationId IS NULL
        BEGIN
            SET @ReturnCode = -400; SET @Message = N'A notification id is required.'; RETURN;
        END

        /* Scoped by user id: one user can never mark another user's row. */
        UPDATE dbo.Notifications
           SET IsRead = 1, ReadOn = SYSUTCDATETIME()
         WHERE Id = @NotificationId AND UserId = @UserId;

        IF @@ROWCOUNT = 0
        BEGIN
            SET @ReturnCode = -404; SET @Message = N'The notification was not found.'; RETURN;
        END

        SET @Message = N'Notification marked as read.';
    END TRY
    BEGIN CATCH
        SET @ReturnCode = -500;
        SET @Message = N'The notification could not be updated.';
    END CATCH
END
GO

/* -----------------------------------------------------------------------------
   sp_GenerateLowStockAlerts
   Run nightly by SQL Server Agent. Raises one low-stock notification per item
   per day, so a persistent shortage does not flood the inbox.
   -------------------------------------------------------------------------- */
CREATE OR ALTER PROCEDURE dbo.sp_GenerateLowStockAlerts
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @Today DATE = CAST(SYSDATETIME() AS DATE);
    DECLARE @Raised INT = 0;

    INSERT INTO dbo.Notifications
        (UserId, TargetRole, NotificationType, Title, [Message], ActionUrl, CreatedBy)
    SELECT
        u.Id,
        N'Inventory Manager',
        2,      /* LowStock */
        CONCAT(N'Low stock: ', ls.ItemName),
        CONCAT(ls.ItemName, N' in ', ls.WarehouseName, N' is down to ',
               CAST(CAST(ls.CurrentStock AS DECIMAL(18,2)) AS NVARCHAR(30)),
               N' against a reorder level of ',
               CAST(CAST(ls.ReorderLevel AS DECIMAL(18,2)) AS NVARCHAR(30)), N'.'),
        CONCAT(N'/Report/View/low-stock'),
        0
    FROM dbo.vw_LowStockItems AS ls
    CROSS JOIN
    (
        SELECT u.Id
        FROM dbo.Users AS u
        INNER JOIN dbo.UserRoles AS ur ON ur.UserId = u.Id
        INNER JOIN dbo.Roles     AS r  ON r.Id = ur.RoleId
        WHERE r.Name IN (N'Inventory Manager', N'Administrator')
          AND u.IsActive = 1 AND u.IsDeleted = 0
    ) AS u
    /* Only once per item per day. */
    WHERE NOT EXISTS
    (
        SELECT 1 FROM dbo.Notifications AS n
        WHERE n.UserId = u.Id
          AND n.NotificationType = 2
          AND n.Title = CONCAT(N'Low stock: ', ls.ItemName)
          AND CAST(n.CreatedOn AS DATE) = @Today
    );

    SET @Raised = @@ROWCOUNT;

    INSERT INTO dbo.AuditLogs (Action, EntityName, Description, Source)
    VALUES (13, N'Notification',
            CONCAT(N'Low stock alert job raised ', @Raised, N' notification(s).'),
            N'sp_GenerateLowStockAlerts');
END
GO

/* -----------------------------------------------------------------------------
   sp_PurgeOldAuditLogs
   Run monthly by SQL Server Agent. Keeps the audit table's indexes healthy
   without losing the recent history the auditors actually read.
   -------------------------------------------------------------------------- */
CREATE OR ALTER PROCEDURE dbo.sp_PurgeOldAuditLogs
    @RetentionDays INT = 730
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @Cutoff DATETIME2(3) = DATEADD(DAY, -@RetentionDays, SYSUTCDATETIME());
    DECLARE @Deleted INT = 0, @Batch INT = 1;

    /* Delete in batches so the log file and locks stay manageable. */
    WHILE @Batch > 0
    BEGIN
        DELETE TOP (5000) FROM dbo.AuditLogs
        WHERE CreatedOn < @Cutoff AND Action <> 15;   /* keep error rows */

        SET @Batch = @@ROWCOUNT;
        SET @Deleted = @Deleted + @Batch;
    END

    DELETE FROM dbo.LoginHistory WHERE LoginOn < @Cutoff;

    INSERT INTO dbo.AuditLogs (Action, EntityName, Description, Source)
    VALUES (13, N'AuditLog',
            CONCAT(N'Purged ', @Deleted, N' audit row(s) older than ', @RetentionDays, N' days.'),
            N'sp_PurgeOldAuditLogs');
END
GO

PRINT 'Dashboard and notification procedures created.';
GO
