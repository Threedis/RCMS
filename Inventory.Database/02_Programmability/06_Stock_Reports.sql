/* =============================================================================
   06_Stock_Reports.sql  -  Stock enquiry and the reporting procedures

   Every report procedure accepts the same optional filter parameters and
   ignores the ones it does not need, so one parameter builder in the data
   access layer serves all of them.
   ========================================================================== */

SET NOCOUNT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

/* -----------------------------------------------------------------------------
   sp_GetCurrentStock
   -------------------------------------------------------------------------- */
CREATE OR ALTER PROCEDURE dbo.sp_GetCurrentStock
    @PageNumber     INT             = 1,
    @PageSize       INT             = 1000,
    @SearchTerm     NVARCHAR(200)   = NULL,
    @SortColumn     NVARCHAR(50)    = NULL,
    @SortDirection  NVARCHAR(4)     = 'ASC',
    @ItemId         INT             = NULL,
    @CategoryId     INT             = NULL,
    @WarehouseId    INT             = NULL,
    @SiteId         INT             = NULL,
    @ItemType       INT             = NULL,
    @FromDate       DATE            = NULL,
    @ToDate         DATE            = NULL,
    @VendorId       INT             = NULL,
    @DepartmentId   INT             = NULL,
    @EngineerId     INT             = NULL,
    @TotalCount     INT             = NULL OUTPUT
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @Skip INT = (CASE WHEN @PageNumber < 1 THEN 0 ELSE @PageNumber - 1 END) * @PageSize;
    DECLARE @Search NVARCHAR(210) = CASE WHEN @SearchTerm IS NULL THEN NULL ELSE N'%' + @SearchTerm + N'%' END;

    SELECT @TotalCount = COUNT(*)
    FROM dbo.vw_CurrentStock AS cs
    WHERE (@ItemId      IS NULL OR cs.ItemId = @ItemId)
      AND (@CategoryId  IS NULL OR cs.CategoryId = @CategoryId)
      AND (@WarehouseId IS NULL OR cs.WarehouseId = @WarehouseId)
      AND (@SiteId      IS NULL OR cs.SiteId = @SiteId)
      AND (@ItemType    IS NULL OR cs.ItemType = @ItemType)
      AND (@Search      IS NULL OR cs.ItemName LIKE @Search
                                OR cs.ItemCode LIKE @Search
                                OR cs.PartNumber LIKE @Search);

    SELECT
        cs.ItemId, cs.ItemCode, cs.ItemName, cs.PartNumber, cs.CategoryName, cs.UnitSymbol,
        cs.WarehouseId, cs.WarehouseName, cs.SiteName,
        cs.Quantity, cs.ReservedQuantity, cs.AvailableQuantity,
        cs.AverageCost, cs.StockValue, cs.ReorderLevel, cs.IsLowStock,
        cs.LastMovementDate, cs.ShelfLocation
    FROM dbo.vw_CurrentStock AS cs
    WHERE (@ItemId      IS NULL OR cs.ItemId = @ItemId)
      AND (@CategoryId  IS NULL OR cs.CategoryId = @CategoryId)
      AND (@WarehouseId IS NULL OR cs.WarehouseId = @WarehouseId)
      AND (@SiteId      IS NULL OR cs.SiteId = @SiteId)
      AND (@ItemType    IS NULL OR cs.ItemType = @ItemType)
      AND (@Search      IS NULL OR cs.ItemName LIKE @Search
                                OR cs.ItemCode LIKE @Search
                                OR cs.PartNumber LIKE @Search)
    ORDER BY
        CASE WHEN @SortDirection = 'ASC'  AND @SortColumn = 'ItemName'   THEN cs.ItemName   END ASC,
        CASE WHEN @SortDirection = 'DESC' AND @SortColumn = 'ItemName'   THEN cs.ItemName   END DESC,
        CASE WHEN @SortDirection = 'ASC'  AND @SortColumn = 'Quantity'   THEN cs.Quantity   END ASC,
        CASE WHEN @SortDirection = 'DESC' AND @SortColumn = 'Quantity'   THEN cs.Quantity   END DESC,
        CASE WHEN @SortDirection = 'ASC'  AND @SortColumn = 'StockValue' THEN cs.StockValue END ASC,
        CASE WHEN @SortDirection = 'DESC' AND @SortColumn = 'StockValue' THEN cs.StockValue END DESC,
        cs.ItemName ASC, cs.WarehouseName ASC
    OFFSET @Skip ROWS FETCH NEXT @PageSize ROWS ONLY;
END
GO

/* -----------------------------------------------------------------------------
   sp_GetStockLedger
   -------------------------------------------------------------------------- */
CREATE OR ALTER PROCEDURE dbo.sp_GetStockLedger
    @PageNumber     INT             = 1,
    @PageSize       INT             = 1000,
    @SearchTerm     NVARCHAR(200)   = NULL,
    @SortColumn     NVARCHAR(50)    = NULL,
    @SortDirection  NVARCHAR(4)     = 'ASC',
    @ItemId         INT             = NULL,
    @WarehouseId    INT             = NULL,
    @FromDate       DATE            = NULL,
    @ToDate         DATE            = NULL,
    @CategoryId     INT             = NULL,
    @SiteId         INT             = NULL,
    @VendorId       INT             = NULL,
    @DepartmentId   INT             = NULL,
    @EngineerId     INT             = NULL,
    @ItemType       INT             = NULL,
    @TotalCount     INT             = NULL OUTPUT
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @Skip INT = (CASE WHEN @PageNumber < 1 THEN 0 ELSE @PageNumber - 1 END) * @PageSize;

    SELECT @TotalCount = COUNT(*)
    FROM dbo.StockLedger AS l
    INNER JOIN dbo.Items AS i ON i.Id = l.ItemId
    WHERE (@ItemId      IS NULL OR l.ItemId = @ItemId)
      AND (@WarehouseId IS NULL OR l.WarehouseId = @WarehouseId)
      AND (@CategoryId  IS NULL OR i.CategoryId = @CategoryId)
      AND (@FromDate    IS NULL OR l.TransactionDate >= @FromDate)
      AND (@ToDate      IS NULL OR l.TransactionDate <= @ToDate);

    SELECT
        l.Id,
        l.TransactionDate,
        i.ItemCode,
        i.ItemName,
        u.Symbol                                    AS UnitSymbol,
        w.Name                                      AS WarehouseName,
        dbo.fn_GetMovementTypeName(l.MovementType)  AS MovementTypeName,
        l.DocumentNumber,
        CASE l.DocumentType
            WHEN 1 THEN N'Goods Receipt Note'
            WHEN 2 THEN N'Material Issue'
            WHEN 3 THEN N'Stock Adjustment'
            ELSE NULL
        END                                         AS DocumentTypeName,
        l.InwardQuantity,
        l.OutwardQuantity,
        l.BalanceQuantity,
        l.UnitCost,
        l.Value,
        l.BatchNumber,
        l.SerialNumber,
        l.Remarks,
        usr.FullName                                AS CreatedByName
    FROM dbo.StockLedger AS l
    INNER JOIN dbo.Items      AS i   ON i.Id = l.ItemId
    INNER JOIN dbo.Units      AS u   ON u.Id = i.UnitId
    INNER JOIN dbo.Warehouses AS w   ON w.Id = l.WarehouseId
    LEFT  JOIN dbo.Users      AS usr ON usr.Id = l.CreatedBy
    WHERE (@ItemId      IS NULL OR l.ItemId = @ItemId)
      AND (@WarehouseId IS NULL OR l.WarehouseId = @WarehouseId)
      AND (@CategoryId  IS NULL OR i.CategoryId = @CategoryId)
      AND (@FromDate    IS NULL OR l.TransactionDate >= @FromDate)
      AND (@ToDate      IS NULL OR l.TransactionDate <= @ToDate)
    ORDER BY l.TransactionDate ASC, l.Id ASC
    OFFSET @Skip ROWS FETCH NEXT @PageSize ROWS ONLY;
END
GO

/* -----------------------------------------------------------------------------
   sp_GetLowStockItems
   -------------------------------------------------------------------------- */
CREATE OR ALTER PROCEDURE dbo.sp_GetLowStockItems
    @WarehouseId    INT = NULL,
    @MaxRows        INT = 100
AS
BEGIN
    SET NOCOUNT ON;

    SELECT TOP (@MaxRows)
        ls.ItemId, ls.ItemCode, ls.ItemName, ls.CategoryName, ls.UnitSymbol,
        ls.WarehouseName, ls.CurrentStock, ls.ReorderLevel, ls.ReorderQuantity,
        ls.Shortfall, ls.AverageCost,
        /* The vendor that most recently supplied the item, as a purchasing hint. */
        (SELECT TOP (1) v.Name
         FROM dbo.InventoryInwardDetails AS d
         INNER JOIN dbo.InventoryInwardHeader AS h ON h.Id = d.InwardHeaderId
         INNER JOIN dbo.Vendors AS v ON v.Id = h.VendorId
         WHERE d.ItemId = ls.ItemId AND h.Status = 3 AND h.IsDeleted = 0
         ORDER BY h.GrnDate DESC) AS PreferredVendorName
    FROM dbo.vw_LowStockItems AS ls
    WHERE (@WarehouseId IS NULL OR ls.WarehouseId = @WarehouseId)
    ORDER BY ls.Shortfall DESC, ls.ItemName;
END
GO

/* -----------------------------------------------------------------------------
   sp_GetSiteWiseStock / sp_GetWarehouseStock
   -------------------------------------------------------------------------- */
CREATE OR ALTER PROCEDURE dbo.sp_GetSiteWiseStock
    @SiteId         INT     = NULL,
    @CategoryId     INT     = NULL,
    @WarehouseId    INT     = NULL,
    @ItemId         INT     = NULL,
    @ItemType       INT     = NULL,
    @FromDate       DATE    = NULL,
    @ToDate         DATE    = NULL,
    @VendorId       INT     = NULL,
    @DepartmentId   INT     = NULL,
    @EngineerId     INT     = NULL,
    @SearchTerm     NVARCHAR(200) = NULL
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        s.Id                                        AS LocationId,
        s.Name                                      AS LocationName,
        s.City                                      AS ParentName,
        COUNT(DISTINCT cs.ItemId)                   AS ItemCount,
        ISNULL(SUM(cs.Quantity), 0)                 AS TotalQuantity,
        ISNULL(SUM(cs.StockValue), 0)               AS TotalValue,
        SUM(CASE WHEN cs.IsLowStock = 1 THEN 1 ELSE 0 END) AS LowStockCount
    FROM dbo.Sites AS s
    LEFT JOIN dbo.vw_CurrentStock AS cs
           ON cs.SiteId = s.Id
          AND (@CategoryId IS NULL OR cs.CategoryId = @CategoryId)
          AND (@ItemType   IS NULL OR cs.ItemType = @ItemType)
    WHERE s.IsDeleted = 0
      AND (@SiteId IS NULL OR s.Id = @SiteId)
    GROUP BY s.Id, s.Name, s.City
    ORDER BY TotalValue DESC, s.Name;
END
GO

CREATE OR ALTER PROCEDURE dbo.sp_GetWarehouseStock
    @WarehouseId    INT     = NULL,
    @SiteId         INT     = NULL,
    @CategoryId     INT     = NULL,
    @ItemId         INT     = NULL,
    @ItemType       INT     = NULL,
    @FromDate       DATE    = NULL,
    @ToDate         DATE    = NULL,
    @VendorId       INT     = NULL,
    @DepartmentId   INT     = NULL,
    @EngineerId     INT     = NULL,
    @SearchTerm     NVARCHAR(200) = NULL
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        w.Id                                        AS LocationId,
        w.Name                                      AS LocationName,
        s.Name                                      AS ParentName,
        COUNT(DISTINCT cs.ItemId)                   AS ItemCount,
        ISNULL(SUM(cs.Quantity), 0)                 AS TotalQuantity,
        ISNULL(SUM(cs.StockValue), 0)               AS TotalValue,
        SUM(CASE WHEN cs.IsLowStock = 1 THEN 1 ELSE 0 END) AS LowStockCount
    FROM dbo.Warehouses AS w
    INNER JOIN dbo.Sites AS s ON s.Id = w.SiteId
    LEFT JOIN dbo.vw_CurrentStock AS cs
           ON cs.WarehouseId = w.Id
          AND (@CategoryId IS NULL OR cs.CategoryId = @CategoryId)
          AND (@ItemType   IS NULL OR cs.ItemType = @ItemType)
    WHERE w.IsDeleted = 0
      AND (@WarehouseId IS NULL OR w.Id = @WarehouseId)
      AND (@SiteId      IS NULL OR w.SiteId = @SiteId)
    GROUP BY w.Id, w.Name, s.Name
    ORDER BY TotalValue DESC, w.Name;
END
GO

/* -----------------------------------------------------------------------------
   sp_GetVendorPurchase
   -------------------------------------------------------------------------- */
CREATE OR ALTER PROCEDURE dbo.sp_GetVendorPurchase
    @FromDate       DATE    = NULL,
    @ToDate         DATE    = NULL,
    @VendorId       INT     = NULL,
    @ItemId         INT     = NULL,
    @CategoryId     INT     = NULL,
    @WarehouseId    INT     = NULL,
    @SiteId         INT     = NULL,
    @DepartmentId   INT     = NULL,
    @EngineerId     INT     = NULL,
    @ItemType       INT     = NULL,
    @SearchTerm     NVARCHAR(200) = NULL
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        v.Id                                    AS VendorId,
        v.Code                                  AS VendorCode,
        v.Name                                  AS VendorName,
        COUNT(DISTINCT h.Id)                    AS ReceiptCount,
        COUNT(DISTINCT d.ItemId)                AS ItemCount,
        ISNULL(SUM(d.QuantityAccepted), 0)      AS TotalQuantity,
        ISNULL(SUM(d.TotalCost), 0)             AS TotalValue,
        ISNULL(SUM(d.QuantityRejected), 0)      AS RejectedQuantity,
        CASE WHEN SUM(d.QuantityReceived) > 0
             THEN CAST(SUM(d.QuantityRejected) * 100.0 / SUM(d.QuantityReceived) AS DECIMAL(18,2))
             ELSE 0 END                         AS RejectionRate,
        MAX(h.GrnDate)                          AS LastSupplyDate
    FROM dbo.Vendors AS v
    INNER JOIN dbo.InventoryInwardHeader  AS h ON h.VendorId = v.Id AND h.IsDeleted = 0 AND h.Status = 3
    INNER JOIN dbo.InventoryInwardDetails AS d ON d.InwardHeaderId = h.Id
    INNER JOIN dbo.Items                  AS i ON i.Id = d.ItemId
    WHERE (@VendorId    IS NULL OR v.Id = @VendorId)
      AND (@FromDate    IS NULL OR h.GrnDate >= @FromDate)
      AND (@ToDate      IS NULL OR h.GrnDate <= @ToDate)
      AND (@WarehouseId IS NULL OR h.WarehouseId = @WarehouseId)
      AND (@ItemId      IS NULL OR d.ItemId = @ItemId)
      AND (@CategoryId  IS NULL OR i.CategoryId = @CategoryId)
      AND (@ItemType    IS NULL OR i.ItemType = @ItemType)
    GROUP BY v.Id, v.Code, v.Name
    ORDER BY TotalValue DESC;
END
GO

/* -----------------------------------------------------------------------------
   sp_GetEngineerWiseIssue / sp_GetDepartmentWiseConsumption
   -------------------------------------------------------------------------- */
CREATE OR ALTER PROCEDURE dbo.sp_GetEngineerWiseIssue
    @FromDate       DATE    = NULL,
    @ToDate         DATE    = NULL,
    @EngineerId     INT     = NULL,
    @DepartmentId   INT     = NULL,
    @ItemId         INT     = NULL,
    @CategoryId     INT     = NULL,
    @WarehouseId    INT     = NULL,
    @SiteId         INT     = NULL,
    @VendorId       INT     = NULL,
    @ItemType       INT     = NULL,
    @SearchTerm     NVARCHAR(200) = NULL
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        e.Id                                    AS EntityId,
        ISNULL(e.EmployeeCode, e.Code)          AS EntityCode,
        e.Name                                  AS EntityName,
        dep.Name                                AS GroupName,
        COUNT(DISTINCT h.Id)                    AS IssueCount,
        COUNT(DISTINCT d.ItemId)                AS ItemCount,
        ISNULL(SUM(d.QuantityIssued), 0)        AS TotalQuantity,
        ISNULL(SUM(d.TotalCost), 0)             AS TotalValue,
        MAX(h.IssueDate)                        AS LastIssueDate
    FROM dbo.Engineers AS e
    INNER JOIN dbo.InventoryOutwardHeader  AS h   ON h.EngineerId = e.Id AND h.IsDeleted = 0 AND h.Status IN (3,5,6)
    INNER JOIN dbo.InventoryOutwardDetails AS d   ON d.OutwardHeaderId = h.Id
    INNER JOIN dbo.Items                   AS i   ON i.Id = d.ItemId
    LEFT  JOIN dbo.Departments             AS dep ON dep.Id = e.DepartmentId
    WHERE (@EngineerId   IS NULL OR e.Id = @EngineerId)
      AND (@DepartmentId IS NULL OR e.DepartmentId = @DepartmentId)
      AND (@FromDate     IS NULL OR h.IssueDate >= @FromDate)
      AND (@ToDate       IS NULL OR h.IssueDate <= @ToDate)
      AND (@WarehouseId  IS NULL OR h.WarehouseId = @WarehouseId)
      AND (@SiteId       IS NULL OR h.SiteId = @SiteId)
      AND (@ItemId       IS NULL OR d.ItemId = @ItemId)
      AND (@CategoryId   IS NULL OR i.CategoryId = @CategoryId)
      AND (@ItemType     IS NULL OR i.ItemType = @ItemType)
    GROUP BY e.Id, e.EmployeeCode, e.Code, e.Name, dep.Name
    ORDER BY TotalValue DESC;
END
GO

CREATE OR ALTER PROCEDURE dbo.sp_GetDepartmentWiseConsumption
    @FromDate       DATE    = NULL,
    @ToDate         DATE    = NULL,
    @DepartmentId   INT     = NULL,
    @EngineerId     INT     = NULL,
    @ItemId         INT     = NULL,
    @CategoryId     INT     = NULL,
    @WarehouseId    INT     = NULL,
    @SiteId         INT     = NULL,
    @VendorId       INT     = NULL,
    @ItemType       INT     = NULL,
    @SearchTerm     NVARCHAR(200) = NULL
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        dep.Id                                  AS EntityId,
        dep.Code                                AS EntityCode,
        dep.Name                                AS EntityName,
        dep.HeadOfDepartment                    AS GroupName,
        COUNT(DISTINCT h.Id)                    AS IssueCount,
        COUNT(DISTINCT d.ItemId)                AS ItemCount,
        ISNULL(SUM(d.QuantityIssued), 0)        AS TotalQuantity,
        ISNULL(SUM(d.TotalCost), 0)             AS TotalValue,
        MAX(h.IssueDate)                        AS LastIssueDate
    FROM dbo.Departments AS dep
    INNER JOIN dbo.InventoryOutwardHeader  AS h ON h.DepartmentId = dep.Id AND h.IsDeleted = 0 AND h.Status IN (3,5,6)
    INNER JOIN dbo.InventoryOutwardDetails AS d ON d.OutwardHeaderId = h.Id
    INNER JOIN dbo.Items                   AS i ON i.Id = d.ItemId
    WHERE (@DepartmentId IS NULL OR dep.Id = @DepartmentId)
      AND (@FromDate     IS NULL OR h.IssueDate >= @FromDate)
      AND (@ToDate       IS NULL OR h.IssueDate <= @ToDate)
      AND (@WarehouseId  IS NULL OR h.WarehouseId = @WarehouseId)
      AND (@SiteId       IS NULL OR h.SiteId = @SiteId)
      AND (@ItemId       IS NULL OR d.ItemId = @ItemId)
      AND (@CategoryId   IS NULL OR i.CategoryId = @CategoryId)
      AND (@ItemType     IS NULL OR i.ItemType = @ItemType)
    GROUP BY dep.Id, dep.Code, dep.Name, dep.HeadOfDepartment
    ORDER BY TotalValue DESC;
END
GO

/* -----------------------------------------------------------------------------
   Period consumption: monthly, quarterly, half-yearly and yearly.
   The four procedures differ only in how the period key and label are formed,
   so the shape of the result set is identical and one DTO serves all four.
   -------------------------------------------------------------------------- */
CREATE OR ALTER PROCEDURE dbo.sp_GetMonthlyConsumption
    @FromDate       DATE    = NULL,
    @ToDate         DATE    = NULL,
    @ItemId         INT     = NULL,
    @CategoryId     INT     = NULL,
    @WarehouseId    INT     = NULL,
    @SiteId         INT     = NULL,
    @VendorId       INT     = NULL,
    @DepartmentId   INT     = NULL,
    @EngineerId     INT     = NULL,
    @ItemType       INT     = NULL,
    @SearchTerm     NVARCHAR(200) = NULL
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        (YEAR(l.TransactionDate) * 100 + MONTH(l.TransactionDate))              AS PeriodKey,
        CONCAT(LEFT(DATENAME(MONTH, l.TransactionDate), 3), ' ', YEAR(l.TransactionDate)) AS PeriodLabel,
        i.Id                                    AS ItemId,
        i.ItemCode,
        i.ItemName,
        c.Name                                  AS CategoryName,
        u.Symbol                                AS UnitSymbol,
        ISNULL(SUM(l.OutwardQuantity), 0)       AS IssuedQuantity,
        ISNULL(SUM(CASE WHEN l.OutwardQuantity > 0 THEN l.Value ELSE 0 END), 0)  AS IssuedValue,
        ISNULL(SUM(l.InwardQuantity), 0)        AS ReceivedQuantity,
        ISNULL(SUM(CASE WHEN l.InwardQuantity  > 0 THEN l.Value ELSE 0 END), 0)  AS ReceivedValue,
        COUNT(*)                                AS TransactionCount
    FROM dbo.StockLedger AS l
    INNER JOIN dbo.Items      AS i ON i.Id = l.ItemId
    INNER JOIN dbo.Categories AS c ON c.Id = i.CategoryId
    INNER JOIN dbo.Units      AS u ON u.Id = i.UnitId
    WHERE l.MovementType IN (1, 2)
      AND (@FromDate    IS NULL OR l.TransactionDate >= @FromDate)
      AND (@ToDate      IS NULL OR l.TransactionDate <= @ToDate)
      AND (@ItemId      IS NULL OR l.ItemId = @ItemId)
      AND (@CategoryId  IS NULL OR i.CategoryId = @CategoryId)
      AND (@WarehouseId IS NULL OR l.WarehouseId = @WarehouseId)
      AND (@ItemType    IS NULL OR i.ItemType = @ItemType)
    GROUP BY YEAR(l.TransactionDate), MONTH(l.TransactionDate),
             DATENAME(MONTH, l.TransactionDate), i.Id, i.ItemCode, i.ItemName, c.Name, u.Symbol
    ORDER BY PeriodKey, i.ItemName;
END
GO

CREATE OR ALTER PROCEDURE dbo.sp_GetQuarterlyConsumption
    @FromDate       DATE    = NULL,
    @ToDate         DATE    = NULL,
    @ItemId         INT     = NULL,
    @CategoryId     INT     = NULL,
    @WarehouseId    INT     = NULL,
    @SiteId         INT     = NULL,
    @VendorId       INT     = NULL,
    @DepartmentId   INT     = NULL,
    @EngineerId     INT     = NULL,
    @ItemType       INT     = NULL,
    @SearchTerm     NVARCHAR(200) = NULL
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        (YEAR(l.TransactionDate) * 10 + DATEPART(QUARTER, l.TransactionDate))   AS PeriodKey,
        CONCAT('Q', DATEPART(QUARTER, l.TransactionDate), ' ', YEAR(l.TransactionDate)) AS PeriodLabel,
        i.Id AS ItemId, i.ItemCode, i.ItemName, c.Name AS CategoryName, u.Symbol AS UnitSymbol,
        ISNULL(SUM(l.OutwardQuantity), 0)       AS IssuedQuantity,
        ISNULL(SUM(CASE WHEN l.OutwardQuantity > 0 THEN l.Value ELSE 0 END), 0)  AS IssuedValue,
        ISNULL(SUM(l.InwardQuantity), 0)        AS ReceivedQuantity,
        ISNULL(SUM(CASE WHEN l.InwardQuantity  > 0 THEN l.Value ELSE 0 END), 0)  AS ReceivedValue,
        COUNT(*)                                AS TransactionCount
    FROM dbo.StockLedger AS l
    INNER JOIN dbo.Items      AS i ON i.Id = l.ItemId
    INNER JOIN dbo.Categories AS c ON c.Id = i.CategoryId
    INNER JOIN dbo.Units      AS u ON u.Id = i.UnitId
    WHERE l.MovementType IN (1, 2)
      AND (@FromDate    IS NULL OR l.TransactionDate >= @FromDate)
      AND (@ToDate      IS NULL OR l.TransactionDate <= @ToDate)
      AND (@ItemId      IS NULL OR l.ItemId = @ItemId)
      AND (@CategoryId  IS NULL OR i.CategoryId = @CategoryId)
      AND (@WarehouseId IS NULL OR l.WarehouseId = @WarehouseId)
      AND (@ItemType    IS NULL OR i.ItemType = @ItemType)
    GROUP BY YEAR(l.TransactionDate), DATEPART(QUARTER, l.TransactionDate),
             i.Id, i.ItemCode, i.ItemName, c.Name, u.Symbol
    ORDER BY PeriodKey, i.ItemName;
END
GO

CREATE OR ALTER PROCEDURE dbo.sp_GetHalfYearlyConsumption
    @FromDate       DATE    = NULL,
    @ToDate         DATE    = NULL,
    @ItemId         INT     = NULL,
    @CategoryId     INT     = NULL,
    @WarehouseId    INT     = NULL,
    @SiteId         INT     = NULL,
    @VendorId       INT     = NULL,
    @DepartmentId   INT     = NULL,
    @EngineerId     INT     = NULL,
    @ItemType       INT     = NULL,
    @SearchTerm     NVARCHAR(200) = NULL
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        (YEAR(l.TransactionDate) * 10 + CASE WHEN MONTH(l.TransactionDate) <= 6 THEN 1 ELSE 2 END) AS PeriodKey,
        CONCAT('H', CASE WHEN MONTH(l.TransactionDate) <= 6 THEN 1 ELSE 2 END, ' ',
               YEAR(l.TransactionDate))         AS PeriodLabel,
        i.Id AS ItemId, i.ItemCode, i.ItemName, c.Name AS CategoryName, u.Symbol AS UnitSymbol,
        ISNULL(SUM(l.OutwardQuantity), 0)       AS IssuedQuantity,
        ISNULL(SUM(CASE WHEN l.OutwardQuantity > 0 THEN l.Value ELSE 0 END), 0)  AS IssuedValue,
        ISNULL(SUM(l.InwardQuantity), 0)        AS ReceivedQuantity,
        ISNULL(SUM(CASE WHEN l.InwardQuantity  > 0 THEN l.Value ELSE 0 END), 0)  AS ReceivedValue,
        COUNT(*)                                AS TransactionCount
    FROM dbo.StockLedger AS l
    INNER JOIN dbo.Items      AS i ON i.Id = l.ItemId
    INNER JOIN dbo.Categories AS c ON c.Id = i.CategoryId
    INNER JOIN dbo.Units      AS u ON u.Id = i.UnitId
    WHERE l.MovementType IN (1, 2)
      AND (@FromDate    IS NULL OR l.TransactionDate >= @FromDate)
      AND (@ToDate      IS NULL OR l.TransactionDate <= @ToDate)
      AND (@ItemId      IS NULL OR l.ItemId = @ItemId)
      AND (@CategoryId  IS NULL OR i.CategoryId = @CategoryId)
      AND (@WarehouseId IS NULL OR l.WarehouseId = @WarehouseId)
      AND (@ItemType    IS NULL OR i.ItemType = @ItemType)
    GROUP BY YEAR(l.TransactionDate), CASE WHEN MONTH(l.TransactionDate) <= 6 THEN 1 ELSE 2 END,
             i.Id, i.ItemCode, i.ItemName, c.Name, u.Symbol
    ORDER BY PeriodKey, i.ItemName;
END
GO

CREATE OR ALTER PROCEDURE dbo.sp_GetYearlyConsumption
    @FromDate       DATE    = NULL,
    @ToDate         DATE    = NULL,
    @ItemId         INT     = NULL,
    @CategoryId     INT     = NULL,
    @WarehouseId    INT     = NULL,
    @SiteId         INT     = NULL,
    @VendorId       INT     = NULL,
    @DepartmentId   INT     = NULL,
    @EngineerId     INT     = NULL,
    @ItemType       INT     = NULL,
    @SearchTerm     NVARCHAR(200) = NULL
AS
BEGIN
    SET NOCOUNT ON;

    /* Grouped by financial year (April to March), which is how the business
       reports consumption. */
    SELECT
        CASE WHEN MONTH(l.TransactionDate) >= 4
             THEN YEAR(l.TransactionDate) ELSE YEAR(l.TransactionDate) - 1 END  AS PeriodKey,
        dbo.fn_GetFinancialYear(l.TransactionDate)                              AS PeriodLabel,
        i.Id AS ItemId, i.ItemCode, i.ItemName, c.Name AS CategoryName, u.Symbol AS UnitSymbol,
        ISNULL(SUM(l.OutwardQuantity), 0)       AS IssuedQuantity,
        ISNULL(SUM(CASE WHEN l.OutwardQuantity > 0 THEN l.Value ELSE 0 END), 0)  AS IssuedValue,
        ISNULL(SUM(l.InwardQuantity), 0)        AS ReceivedQuantity,
        ISNULL(SUM(CASE WHEN l.InwardQuantity  > 0 THEN l.Value ELSE 0 END), 0)  AS ReceivedValue,
        COUNT(*)                                AS TransactionCount
    FROM dbo.StockLedger AS l
    INNER JOIN dbo.Items      AS i ON i.Id = l.ItemId
    INNER JOIN dbo.Categories AS c ON c.Id = i.CategoryId
    INNER JOIN dbo.Units      AS u ON u.Id = i.UnitId
    WHERE l.MovementType IN (1, 2)
      AND (@FromDate    IS NULL OR l.TransactionDate >= @FromDate)
      AND (@ToDate      IS NULL OR l.TransactionDate <= @ToDate)
      AND (@ItemId      IS NULL OR l.ItemId = @ItemId)
      AND (@CategoryId  IS NULL OR i.CategoryId = @CategoryId)
      AND (@WarehouseId IS NULL OR l.WarehouseId = @WarehouseId)
      AND (@ItemType    IS NULL OR i.ItemType = @ItemType)
    GROUP BY
        CASE WHEN MONTH(l.TransactionDate) >= 4
             THEN YEAR(l.TransactionDate) ELSE YEAR(l.TransactionDate) - 1 END,
        dbo.fn_GetFinancialYear(l.TransactionDate),
        i.Id, i.ItemCode, i.ItemName, c.Name, u.Symbol
    ORDER BY PeriodKey, i.ItemName;
END
GO

/* -----------------------------------------------------------------------------
   sp_GetAbcAnalysis
   Ranks items by consumption value and draws the class boundaries at 70% and
   90% of the running total, the standard ABC split.
   -------------------------------------------------------------------------- */
CREATE OR ALTER PROCEDURE dbo.sp_GetAbcAnalysis
    @FromDate       DATE    = NULL,
    @ToDate         DATE    = NULL,
    @CategoryId     INT     = NULL,
    @ItemId         INT     = NULL,
    @WarehouseId    INT     = NULL,
    @SiteId         INT     = NULL,
    @VendorId       INT     = NULL,
    @DepartmentId   INT     = NULL,
    @EngineerId     INT     = NULL,
    @ItemType       INT     = NULL,
    @SearchTerm     NVARCHAR(200) = NULL
AS
BEGIN
    SET NOCOUNT ON;

    WITH Consumption AS
    (
        SELECT
            i.Id            AS ItemId,
            i.ItemCode,
            i.ItemName,
            c.Name          AS CategoryName,
            SUM(l.OutwardQuantity)  AS AnnualQuantity,
            SUM(l.Value)            AS AnnualValue
        FROM dbo.StockLedger AS l
        INNER JOIN dbo.Items      AS i ON i.Id = l.ItemId
        INNER JOIN dbo.Categories AS c ON c.Id = i.CategoryId
        WHERE l.MovementType = 2
          AND (@FromDate    IS NULL OR l.TransactionDate >= @FromDate)
          AND (@ToDate      IS NULL OR l.TransactionDate <= @ToDate)
          AND (@CategoryId  IS NULL OR i.CategoryId = @CategoryId)
          AND (@WarehouseId IS NULL OR l.WarehouseId = @WarehouseId)
          AND (@ItemType    IS NULL OR i.ItemType = @ItemType)
        GROUP BY i.Id, i.ItemCode, i.ItemName, c.Name
        HAVING SUM(l.Value) > 0
    ),
    Ranked AS
    (
        SELECT
            c.*,
            ROW_NUMBER() OVER (ORDER BY c.AnnualValue DESC)                     AS [Rank],
            CAST(c.AnnualValue * 100.0 / NULLIF(SUM(c.AnnualValue) OVER (), 0)
                 AS DECIMAL(18,2))                                              AS ValuePercent,
            CAST(SUM(c.AnnualValue) OVER (ORDER BY c.AnnualValue DESC
                                          ROWS UNBOUNDED PRECEDING) * 100.0
                 / NULLIF(SUM(c.AnnualValue) OVER (), 0) AS DECIMAL(18,2))      AS CumulativePercent
        FROM Consumption AS c
    )
    SELECT
        r.ItemId, r.ItemCode, r.ItemName, r.CategoryName,
        r.AnnualQuantity, r.AnnualValue, r.ValuePercent, r.CumulativePercent,
        CASE WHEN r.CumulativePercent <= 70 THEN 1
             WHEN r.CumulativePercent <= 90 THEN 2
             ELSE 3 END                             AS Classification,
        CASE WHEN r.CumulativePercent <= 70 THEN N'A'
             WHEN r.CumulativePercent <= 90 THEN N'B'
             ELSE N'C' END                          AS ClassificationName,
        r.[Rank]
    FROM Ranked AS r
    ORDER BY r.[Rank];
END
GO

/* -----------------------------------------------------------------------------
   sp_GetMovementAnalysis  -  fast / slow / dead classification
   -------------------------------------------------------------------------- */
CREATE OR ALTER PROCEDURE dbo.sp_GetMovementAnalysis
    @FromDate           DATE    = NULL,
    @ToDate             DATE    = NULL,
    @CategoryId         INT     = NULL,
    @ItemId             INT     = NULL,
    @WarehouseId        INT     = NULL,
    @SiteId             INT     = NULL,
    @VendorId           INT     = NULL,
    @DepartmentId       INT     = NULL,
    @EngineerId         INT     = NULL,
    @ItemType           INT     = NULL,
    @SearchTerm         NVARCHAR(200) = NULL,
    @MovementCategory   INT     = NULL,
    @FastThreshold      INT     = 12,
    @DeadStockDays      INT     = 180
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @Today DATE = CAST(SYSDATETIME() AS DATE);

    WITH Movement AS
    (
        SELECT
            i.Id                        AS ItemId,
            i.ItemCode,
            i.ItemName,
            c.Name                      AS CategoryName,
            u.Symbol                    AS UnitSymbol,
            ISNULL(ss.TotalQuantity, 0) AS CurrentStock,
            ISNULL(ss.TotalValue, 0)    AS StockValue,
            ISNULL(iss.IssueCount, 0)   AS IssueCount,
            ISNULL(iss.IssuedQuantity, 0) AS IssuedQuantity,
            iss.LastIssueDate,
            rcv.LastReceiptDate
        FROM dbo.Items AS i
        INNER JOIN dbo.Categories AS c ON c.Id = i.CategoryId
        INNER JOIN dbo.Units      AS u ON u.Id = i.UnitId
        LEFT  JOIN dbo.vw_ItemStockSummary AS ss ON ss.ItemId = i.Id
        OUTER APPLY
        (
            SELECT COUNT(*) AS IssueCount, SUM(l.OutwardQuantity) AS IssuedQuantity,
                   MAX(l.TransactionDate) AS LastIssueDate
            FROM dbo.StockLedger AS l
            WHERE l.ItemId = i.Id AND l.MovementType = 2
              AND (@FromDate    IS NULL OR l.TransactionDate >= @FromDate)
              AND (@ToDate      IS NULL OR l.TransactionDate <= @ToDate)
              AND (@WarehouseId IS NULL OR l.WarehouseId = @WarehouseId)
        ) AS iss
        OUTER APPLY
        (
            SELECT MAX(l.TransactionDate) AS LastReceiptDate
            FROM dbo.StockLedger AS l
            WHERE l.ItemId = i.Id AND l.MovementType = 1
        ) AS rcv
        WHERE i.IsDeleted = 0
          AND (@CategoryId IS NULL OR i.CategoryId = @CategoryId)
          AND (@ItemId     IS NULL OR i.Id = @ItemId)
          AND (@ItemType   IS NULL OR i.ItemType = @ItemType)
    ),
    Classified AS
    (
        SELECT
            m.*,
            DATEDIFF(DAY, ISNULL(m.LastIssueDate, m.LastReceiptDate), @Today) AS DaysSinceLastMovement,
            CASE WHEN m.CurrentStock > 0
                 THEN CAST(m.IssuedQuantity / NULLIF(m.CurrentStock, 0) AS DECIMAL(18,2))
                 ELSE 0 END                                                    AS TurnoverRatio,
            CASE
                WHEN m.LastIssueDate IS NULL AND m.CurrentStock > 0                                   THEN 4  /* NonMoving */
                WHEN m.CurrentStock > 0
                     AND DATEDIFF(DAY, ISNULL(m.LastIssueDate, m.LastReceiptDate), @Today) >= @DeadStockDays
                                                                                                       THEN 3  /* Dead */
                WHEN m.IssueCount >= @FastThreshold                                                    THEN 1  /* Fast */
                ELSE 2                                                                                         /* Slow */
            END                                                                AS MovementCategory
        FROM Movement AS m
    )
    SELECT
        c.ItemId, c.ItemCode, c.ItemName, c.CategoryName, c.UnitSymbol,
        c.CurrentStock, c.StockValue, c.IssueCount, c.IssuedQuantity,
        c.LastIssueDate, c.LastReceiptDate, c.DaysSinceLastMovement, c.TurnoverRatio,
        c.MovementCategory                          AS Category,
        CASE c.MovementCategory
            WHEN 1 THEN N'Fast' WHEN 2 THEN N'Slow'
            WHEN 3 THEN N'Dead' ELSE N'Non-moving'
        END                                         AS CategoryLabel
    FROM Classified AS c
    WHERE (@MovementCategory IS NULL OR c.MovementCategory = @MovementCategory)
    ORDER BY
        CASE WHEN @MovementCategory = 1 THEN c.IssueCount END DESC,
        c.StockValue DESC, c.ItemName;
END
GO

/* -----------------------------------------------------------------------------
   sp_GetDeadStock
   -------------------------------------------------------------------------- */
CREATE OR ALTER PROCEDURE dbo.sp_GetDeadStock
    @FromDate       DATE    = NULL,
    @ToDate         DATE    = NULL,
    @CategoryId     INT     = NULL,
    @ItemId         INT     = NULL,
    @WarehouseId    INT     = NULL,
    @SiteId         INT     = NULL,
    @VendorId       INT     = NULL,
    @DepartmentId   INT     = NULL,
    @EngineerId     INT     = NULL,
    @ItemType       INT     = NULL,
    @SearchTerm     NVARCHAR(200) = NULL,
    @DeadStockDays  INT     = 180
AS
BEGIN
    SET NOCOUNT ON;

    EXEC dbo.sp_GetMovementAnalysis
         @CategoryId = @CategoryId,
         @ItemId = @ItemId,
         @WarehouseId = @WarehouseId,
         @ItemType = @ItemType,
         @MovementCategory = 3,
         @DeadStockDays = @DeadStockDays;
END
GO

/* -----------------------------------------------------------------------------
   sp_GetReceiptRegister / sp_GetIssueRegister
   -------------------------------------------------------------------------- */
CREATE OR ALTER PROCEDURE dbo.sp_GetReceiptRegister
    @FromDate       DATE    = NULL,
    @ToDate         DATE    = NULL,
    @VendorId       INT     = NULL,
    @WarehouseId    INT     = NULL,
    @ItemId         INT     = NULL,
    @CategoryId     INT     = NULL,
    @SiteId         INT     = NULL,
    @DepartmentId   INT     = NULL,
    @EngineerId     INT     = NULL,
    @ItemType       INT     = NULL,
    @SearchTerm     NVARCHAR(200) = NULL
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        h.GrnDate, h.GrnNumber, v.Name AS VendorName,
        h.PurchaseOrderNumber, h.DeliveryChallanNumber,
        i.ItemCode, i.ItemName, u.Symbol AS UnitSymbol,
        d.QuantityReceived, d.QuantityAccepted, d.QuantityRejected,
        d.UnitCost, d.TotalCost, w.Name AS WarehouseName, d.BatchNumber,
        dbo.fn_GetStatusName(h.Status) AS StatusName,
        au.FullName AS ApprovedByName
    FROM dbo.InventoryInwardHeader  AS h
    INNER JOIN dbo.InventoryInwardDetails AS d ON d.InwardHeaderId = h.Id
    INNER JOIN dbo.Vendors     AS v  ON v.Id = h.VendorId
    INNER JOIN dbo.Warehouses  AS w  ON w.Id = h.WarehouseId
    INNER JOIN dbo.Items       AS i  ON i.Id = d.ItemId
    INNER JOIN dbo.Units       AS u  ON u.Id = i.UnitId
    LEFT  JOIN dbo.Users       AS au ON au.Id = h.ApprovedBy
    WHERE h.IsDeleted = 0
      AND (@FromDate    IS NULL OR h.GrnDate >= @FromDate)
      AND (@ToDate      IS NULL OR h.GrnDate <= @ToDate)
      AND (@VendorId    IS NULL OR h.VendorId = @VendorId)
      AND (@WarehouseId IS NULL OR h.WarehouseId = @WarehouseId)
      AND (@ItemId      IS NULL OR d.ItemId = @ItemId)
      AND (@CategoryId  IS NULL OR i.CategoryId = @CategoryId)
      AND (@ItemType    IS NULL OR i.ItemType = @ItemType)
    ORDER BY h.GrnDate DESC, h.GrnNumber, d.LineNumber;
END
GO

CREATE OR ALTER PROCEDURE dbo.sp_GetIssueRegister
    @FromDate       DATE    = NULL,
    @ToDate         DATE    = NULL,
    @DepartmentId   INT     = NULL,
    @SiteId         INT     = NULL,
    @EngineerId     INT     = NULL,
    @WarehouseId    INT     = NULL,
    @ItemId         INT     = NULL,
    @CategoryId     INT     = NULL,
    @VendorId       INT     = NULL,
    @ItemType       INT     = NULL,
    @SearchTerm     NVARCHAR(200) = NULL
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        h.IssueDate, h.IssueNumber,
        dep.Name AS DepartmentName, s.Name AS SiteName, e.Name AS EngineerName, h.ProjectName,
        i.ItemCode, i.ItemName, u.Symbol AS UnitSymbol,
        d.QuantityRequested, d.QuantityApproved, d.QuantityIssued,
        d.UnitCost, d.TotalCost, w.Name AS WarehouseName,
        dbo.fn_GetStatusName(h.Status) AS StatusName,
        au.FullName AS ApprovedByName, h.TrackingNumber
    FROM dbo.InventoryOutwardHeader AS h
    INNER JOIN dbo.InventoryOutwardDetails AS d ON d.OutwardHeaderId = h.Id
    INNER JOIN dbo.Warehouses  AS w   ON w.Id = h.WarehouseId
    INNER JOIN dbo.Items       AS i   ON i.Id = d.ItemId
    INNER JOIN dbo.Units       AS u   ON u.Id = i.UnitId
    LEFT  JOIN dbo.Departments AS dep ON dep.Id = h.DepartmentId
    LEFT  JOIN dbo.Sites       AS s   ON s.Id = h.SiteId
    LEFT  JOIN dbo.Engineers   AS e   ON e.Id = h.EngineerId
    LEFT  JOIN dbo.Users       AS au  ON au.Id = h.ApprovedBy
    WHERE h.IsDeleted = 0
      AND (@FromDate     IS NULL OR h.IssueDate >= @FromDate)
      AND (@ToDate       IS NULL OR h.IssueDate <= @ToDate)
      AND (@DepartmentId IS NULL OR h.DepartmentId = @DepartmentId)
      AND (@SiteId       IS NULL OR h.SiteId = @SiteId)
      AND (@EngineerId   IS NULL OR h.EngineerId = @EngineerId)
      AND (@WarehouseId  IS NULL OR h.WarehouseId = @WarehouseId)
      AND (@ItemId       IS NULL OR d.ItemId = @ItemId)
      AND (@CategoryId   IS NULL OR i.CategoryId = @CategoryId)
      AND (@ItemType     IS NULL OR i.ItemType = @ItemType)
    ORDER BY h.IssueDate DESC, h.IssueNumber, d.LineNumber;
END
GO

PRINT 'Stock and reporting procedures created.';
GO
