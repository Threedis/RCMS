/* =============================================================================
   08_Maintenance.sql  -  Index and statistics maintenance

   Invoked weekly by the "Inventory - Index Maintenance" SQL Server Agent job.
   The stock ledger and the audit log grow continuously and their covering
   indexes sit on the hot path of every balance query and every audit search, so
   keeping fragmentation in check is what stops the application slowing down as
   the data set grows.
   ========================================================================== */

SET NOCOUNT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

CREATE OR ALTER PROCEDURE dbo.sp_MaintainIndexes
    @ReorganizeThreshold    DECIMAL(5,2) = 10.0,    /* reorganise from this % */
    @RebuildThreshold       DECIMAL(5,2) = 30.0,    /* rebuild from this % */
    @MinimumPageCount       INT          = 100      /* ignore tiny indexes */
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @Schema      SYSNAME,
            @Table       SYSNAME,
            @Index       SYSNAME,
            @Fragmention DECIMAL(5,2),
            @Sql         NVARCHAR(MAX),
            @Processed   INT = 0;

    DECLARE index_cursor CURSOR LOCAL FAST_FORWARD FOR
        SELECT
            s.name,
            t.name,
            i.name,
            CAST(ips.avg_fragmentation_in_percent AS DECIMAL(5,2))
        FROM sys.dm_db_index_physical_stats(DB_ID(), NULL, NULL, NULL, N'LIMITED') AS ips
        INNER JOIN sys.indexes AS i  ON i.object_id = ips.object_id AND i.index_id = ips.index_id
        INNER JOIN sys.tables  AS t  ON t.object_id = i.object_id
        INNER JOIN sys.schemas AS s  ON s.schema_id = t.schema_id
        WHERE ips.avg_fragmentation_in_percent >= @ReorganizeThreshold
          AND ips.page_count >= @MinimumPageCount
          AND i.name IS NOT NULL            /* skip heaps */
          AND i.is_disabled = 0
          AND t.is_ms_shipped = 0;

    OPEN index_cursor;
    FETCH NEXT FROM index_cursor INTO @Schema, @Table, @Index, @Fragmention;

    WHILE @@FETCH_STATUS = 0
    BEGIN
        /* QUOTENAME is what makes this dynamic statement safe: the names come
           from system catalogue views and are escaped as identifiers. */
        IF @Fragmention >= @RebuildThreshold
        BEGIN
            SET @Sql = N'ALTER INDEX ' + QUOTENAME(@Index)
                     + N' ON ' + QUOTENAME(@Schema) + N'.' + QUOTENAME(@Table)
                     + N' REBUILD WITH (ONLINE = OFF, SORT_IN_TEMPDB = ON, FILLFACTOR = 90);';
        END
        ELSE
        BEGIN
            SET @Sql = N'ALTER INDEX ' + QUOTENAME(@Index)
                     + N' ON ' + QUOTENAME(@Schema) + N'.' + QUOTENAME(@Table)
                     + N' REORGANIZE;';
        END

        BEGIN TRY
            EXEC sp_executesql @Sql;
            SET @Processed = @Processed + 1;
        END TRY
        BEGIN CATCH
            /* One unavailable index must not abort the whole maintenance run. */
            INSERT INTO dbo.AuditLogs (Action, EntityName, EntityId, Description, IsSuccessful, ErrorMessage, Source)
            VALUES (15, N'Index', @Index,
                    CONCAT(N'Maintenance failed for ', @Schema, N'.', @Table, N'.', @Index),
                    0, ERROR_MESSAGE(), N'sp_MaintainIndexes');
        END CATCH

        FETCH NEXT FROM index_cursor INTO @Schema, @Table, @Index, @Fragmention;
    END

    CLOSE index_cursor;
    DEALLOCATE index_cursor;

    /* Refresh statistics so the optimiser sees the new distribution. */
    EXEC sp_updatestats;

    INSERT INTO dbo.AuditLogs (Action, EntityName, Description, Source)
    VALUES (13, N'Index',
            CONCAT(N'Index maintenance processed ', @Processed, N' index(es).'),
            N'sp_MaintainIndexes');
END
GO

/* -----------------------------------------------------------------------------
   sp_VerifyStockIntegrity
   A reconciliation check for the operations team: it recomputes each item and
   warehouse balance from the movement columns and reports any row where the
   stored running balance disagrees. A healthy system returns no rows.
   -------------------------------------------------------------------------- */
CREATE OR ALTER PROCEDURE dbo.sp_VerifyStockIntegrity
AS
BEGIN
    SET NOCOUNT ON;

    WITH Recomputed AS
    (
        SELECT
            l.ItemId,
            l.WarehouseId,
            SUM(l.InwardQuantity) - SUM(l.OutwardQuantity) AS ComputedBalance
        FROM dbo.StockLedger AS l
        GROUP BY l.ItemId, l.WarehouseId
    ),
    Stored AS
    (
        SELECT cs.ItemId, cs.WarehouseId, cs.Quantity AS StoredBalance
        FROM dbo.vw_CurrentStock AS cs
    )
    SELECT
        i.ItemCode,
        i.ItemName,
        w.Name                                          AS WarehouseName,
        r.ComputedBalance,
        s.StoredBalance,
        r.ComputedBalance - s.StoredBalance             AS Difference
    FROM Recomputed AS r
    INNER JOIN Stored     AS s ON s.ItemId = r.ItemId AND s.WarehouseId = r.WarehouseId
    INNER JOIN dbo.Items  AS i ON i.Id = r.ItemId
    INNER JOIN dbo.Warehouses AS w ON w.Id = r.WarehouseId
    WHERE ABS(r.ComputedBalance - s.StoredBalance) > 0.0001
    ORDER BY ABS(r.ComputedBalance - s.StoredBalance) DESC;
END
GO

PRINT 'Maintenance procedures created.';
GO
