/* =============================================================================
   03_Items_Vendors.sql  -  Item master and vendor master procedures
   ========================================================================== */

SET NOCOUNT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

/* -----------------------------------------------------------------------------
   sp_InsertItem
   The item code is allocated by the database, so two concurrent inserts can
   never receive the same code.
   -------------------------------------------------------------------------- */
CREATE OR ALTER PROCEDURE dbo.sp_InsertItem
    @ItemId                 INT             = 0,
    @ItemCode               NVARCHAR(40)    = NULL,
    @ItemName               NVARCHAR(250),
    @PartNumber             NVARCHAR(100)   = NULL,
    @AlternatePartNumber    NVARCHAR(100)   = NULL,
    @HsnCode                NVARCHAR(20)    = NULL,
    @Description            NVARCHAR(1000)  = NULL,
    @Specification          NVARCHAR(500)   = NULL,
    @CategoryId             INT,
    @UnitId                 INT,
    @ManufacturerId         INT             = NULL,
    @ItemType               INT             = 2,
    @Barcode                NVARCHAR(100)   = NULL,
    @ReorderLevel           DECIMAL(18,4)   = 0,
    @ReorderQuantity        DECIMAL(18,4)   = 0,
    @MinimumStock           DECIMAL(18,4)   = 0,
    @MaximumStock           DECIMAL(18,4)   = 0,
    @StandardCost           DECIMAL(18,4)   = 0,
    @ShelfLocation          NVARCHAR(50)    = NULL,
    @IsBatchTracked         BIT             = 0,
    @IsSerialTracked        BIT             = 0,
    @ShelfLifeDays          INT             = NULL,
    @TaxRate                DECIMAL(5,2)    = NULL,
    @IsActive               BIT             = 1,
    @CurrentUserId          INT             = 0,
    @ReturnCode             INT             OUTPUT,
    @Message                NVARCHAR(500)   OUTPUT,
    @NewId                  INT             OUTPUT,
    @GeneratedNumber        NVARCHAR(30)    OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    SET @ReturnCode = 0; SET @Message = N''; SET @NewId = NULL; SET @GeneratedNumber = NULL;

    BEGIN TRY
        IF @ItemName IS NULL OR LTRIM(RTRIM(@ItemName)) = N''
        BEGIN
            SET @ReturnCode = -400; SET @Message = N'Item name is required.'; RETURN;
        END

        IF NOT EXISTS (SELECT 1 FROM dbo.Categories WHERE Id = @CategoryId AND IsDeleted = 0)
        BEGIN
            SET @ReturnCode = -400; SET @Message = N'Select a valid category.'; RETURN;
        END

        IF NOT EXISTS (SELECT 1 FROM dbo.Units WHERE Id = @UnitId AND IsDeleted = 0)
        BEGIN
            SET @ReturnCode = -400; SET @Message = N'Select a valid unit of measure.'; RETURN;
        END

        IF @MaximumStock > 0 AND @MaximumStock < @MinimumStock
        BEGIN
            SET @ReturnCode = -400;
            SET @Message = N'Maximum stock cannot be less than minimum stock.';
            RETURN;
        END

        SET @ItemName = LTRIM(RTRIM(@ItemName));
        SET @Barcode  = NULLIF(LTRIM(RTRIM(ISNULL(@Barcode, N''))), N'');

        BEGIN TRANSACTION;

        IF EXISTS (SELECT 1 FROM dbo.Items WHERE ItemName = @ItemName AND IsDeleted = 0)
        BEGIN
            ROLLBACK TRANSACTION;
            SET @ReturnCode = -409;
            SET @Message = CONCAT(N'An item named ''', @ItemName, N''' already exists.');
            RETURN;
        END

        IF @Barcode IS NOT NULL AND EXISTS (SELECT 1 FROM dbo.Items WHERE Barcode = @Barcode AND IsDeleted = 0)
        BEGIN
            ROLLBACK TRANSACTION;
            SET @ReturnCode = -409;
            SET @Message = CONCAT(N'Barcode ''', @Barcode, N''' is already assigned to another item.');
            RETURN;
        END

        /* Allocate the code inside the transaction so it cannot be reused. */
        IF @ItemCode IS NULL OR LTRIM(RTRIM(@ItemCode)) = N''
        BEGIN
            DECLARE @Allocated NVARCHAR(30);
            EXEC dbo.sp_GetNextDocumentNumber
                 @SequenceKey = N'ITEM', @UseYear = 0, @Number = @Allocated OUTPUT;

            /* sp_GetNextDocumentNumber returns PREFIX/000001; item codes use a hyphen. */
            SET @ItemCode = REPLACE(@Allocated, N'/', N'-');
        END
        ELSE
        BEGIN
            SET @ItemCode = LTRIM(RTRIM(@ItemCode));

            IF EXISTS (SELECT 1 FROM dbo.Items WHERE ItemCode = @ItemCode AND IsDeleted = 0)
            BEGIN
                ROLLBACK TRANSACTION;
                SET @ReturnCode = -409;
                SET @Message = CONCAT(N'Item code ''', @ItemCode, N''' is already in use.');
                RETURN;
            END
        END

        INSERT INTO dbo.Items
            (ItemCode, ItemName, PartNumber, AlternatePartNumber, HsnCode, Description, Specification,
             CategoryId, UnitId, ManufacturerId, ItemType, Barcode,
             ReorderLevel, ReorderQuantity, MinimumStock, MaximumStock, StandardCost, AverageCost,
             ShelfLocation, IsBatchTracked, IsSerialTracked, ShelfLifeDays, TaxRate, IsActive, CreatedBy)
        VALUES
            (@ItemCode, @ItemName, @PartNumber, @AlternatePartNumber, @HsnCode, @Description, @Specification,
             @CategoryId, @UnitId, @ManufacturerId, @ItemType, @Barcode,
             @ReorderLevel, @ReorderQuantity, @MinimumStock, @MaximumStock, @StandardCost, @StandardCost,
             @ShelfLocation, @IsBatchTracked, @IsSerialTracked, @ShelfLifeDays, @TaxRate, @IsActive, @CurrentUserId);

        SET @NewId = CAST(SCOPE_IDENTITY() AS INT);
        SET @GeneratedNumber = @ItemCode;

        INSERT INTO dbo.AuditLogs (Action, EntityName, EntityId, Description, UserId, Source)
        VALUES (4, N'Item', CAST(@NewId AS NVARCHAR(50)),
                CONCAT(N'Created item ', @ItemCode, N' - ', @ItemName), @CurrentUserId, N'sp_InsertItem');

        COMMIT TRANSACTION;

        SET @Message = CONCAT(N'Item ', @ItemCode, N' created successfully.');
    END TRY
    BEGIN CATCH
        IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;

        SET @ReturnCode = -500;
        SET @Message = N'The item could not be saved.';

        INSERT INTO dbo.AuditLogs (Action, EntityName, Description, UserId, IsSuccessful, ErrorMessage, Source)
        VALUES (15, N'Item', CONCAT(N'sp_InsertItem failed for ', @ItemName),
                @CurrentUserId, 0, ERROR_MESSAGE(), N'sp_InsertItem');
    END CATCH
END
GO

/* -----------------------------------------------------------------------------
   sp_UpdateItem
   -------------------------------------------------------------------------- */
CREATE OR ALTER PROCEDURE dbo.sp_UpdateItem
    @ItemId                 INT,
    @ItemCode               NVARCHAR(40)    = NULL,
    @ItemName               NVARCHAR(250),
    @PartNumber             NVARCHAR(100)   = NULL,
    @AlternatePartNumber    NVARCHAR(100)   = NULL,
    @HsnCode                NVARCHAR(20)    = NULL,
    @Description            NVARCHAR(1000)  = NULL,
    @Specification          NVARCHAR(500)   = NULL,
    @CategoryId             INT,
    @UnitId                 INT,
    @ManufacturerId         INT             = NULL,
    @ItemType               INT             = 2,
    @Barcode                NVARCHAR(100)   = NULL,
    @ReorderLevel           DECIMAL(18,4)   = 0,
    @ReorderQuantity        DECIMAL(18,4)   = 0,
    @MinimumStock           DECIMAL(18,4)   = 0,
    @MaximumStock           DECIMAL(18,4)   = 0,
    @StandardCost           DECIMAL(18,4)   = 0,
    @ShelfLocation          NVARCHAR(50)    = NULL,
    @IsBatchTracked         BIT             = 0,
    @IsSerialTracked        BIT             = 0,
    @ShelfLifeDays          INT             = NULL,
    @TaxRate                DECIMAL(5,2)    = NULL,
    @IsActive               BIT             = 1,
    @CurrentUserId          INT             = 0,
    @ReturnCode             INT             OUTPUT,
    @Message                NVARCHAR(500)   OUTPUT,
    @NewId                  INT             OUTPUT,
    @GeneratedNumber        NVARCHAR(30)    OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    SET @ReturnCode = 0; SET @Message = N''; SET @NewId = @ItemId; SET @GeneratedNumber = NULL;

    BEGIN TRY
        DECLARE @CurrentUnitId INT, @HasHistory BIT = 0;

        SELECT @CurrentUnitId = UnitId FROM dbo.Items WHERE Id = @ItemId AND IsDeleted = 0;

        IF @CurrentUnitId IS NULL
        BEGIN
            SET @ReturnCode = -404; SET @Message = N'The item was not found.'; RETURN;
        END

        IF EXISTS (SELECT 1 FROM dbo.StockLedger WHERE ItemId = @ItemId)
        BEGIN
            SET @HasHistory = 1;
        END

        /* Changing the unit of measure after movements exist would silently
           re-scale every historical quantity. */
        IF @HasHistory = 1 AND @UnitId <> @CurrentUnitId
        BEGIN
            SET @ReturnCode = -409;
            SET @Message = N'This item has stock movements; its unit of measure can no longer be changed.';
            RETURN;
        END

        IF @MaximumStock > 0 AND @MaximumStock < @MinimumStock
        BEGIN
            SET @ReturnCode = -400;
            SET @Message = N'Maximum stock cannot be less than minimum stock.';
            RETURN;
        END

        SET @ItemName = LTRIM(RTRIM(@ItemName));
        SET @Barcode  = NULLIF(LTRIM(RTRIM(ISNULL(@Barcode, N''))), N'');

        BEGIN TRANSACTION;

        IF EXISTS (SELECT 1 FROM dbo.Items WHERE ItemName = @ItemName AND Id <> @ItemId AND IsDeleted = 0)
        BEGIN
            ROLLBACK TRANSACTION;
            SET @ReturnCode = -409;
            SET @Message = CONCAT(N'Another item named ''', @ItemName, N''' already exists.');
            RETURN;
        END

        IF @Barcode IS NOT NULL
           AND EXISTS (SELECT 1 FROM dbo.Items WHERE Barcode = @Barcode AND Id <> @ItemId AND IsDeleted = 0)
        BEGIN
            ROLLBACK TRANSACTION;
            SET @ReturnCode = -409;
            SET @Message = CONCAT(N'Barcode ''', @Barcode, N''' is already assigned to another item.');
            RETURN;
        END

        UPDATE dbo.Items
           SET ItemName            = @ItemName,
               PartNumber          = @PartNumber,
               AlternatePartNumber = @AlternatePartNumber,
               HsnCode             = @HsnCode,
               Description         = @Description,
               Specification       = @Specification,
               CategoryId          = @CategoryId,
               UnitId              = @UnitId,
               ManufacturerId      = @ManufacturerId,
               ItemType            = @ItemType,
               Barcode             = @Barcode,
               ReorderLevel        = @ReorderLevel,
               ReorderQuantity     = @ReorderQuantity,
               MinimumStock        = @MinimumStock,
               MaximumStock        = @MaximumStock,
               StandardCost        = @StandardCost,
               ShelfLocation       = @ShelfLocation,
               IsBatchTracked      = @IsBatchTracked,
               IsSerialTracked     = @IsSerialTracked,
               ShelfLifeDays       = @ShelfLifeDays,
               TaxRate             = @TaxRate,
               IsActive            = @IsActive,
               ModifiedOn          = SYSUTCDATETIME(),
               ModifiedBy          = @CurrentUserId
         WHERE Id = @ItemId AND IsDeleted = 0;

        IF @@ROWCOUNT = 0
        BEGIN
            ROLLBACK TRANSACTION;
            SET @ReturnCode = -404; SET @Message = N'The item was not found.'; RETURN;
        END

        INSERT INTO dbo.AuditLogs (Action, EntityName, EntityId, Description, UserId, Source)
        VALUES (5, N'Item', CAST(@ItemId AS NVARCHAR(50)),
                CONCAT(N'Updated item ', @ItemName), @CurrentUserId, N'sp_UpdateItem');

        COMMIT TRANSACTION;

        SET @Message = N'Item updated successfully.';
    END TRY
    BEGIN CATCH
        IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;

        SET @ReturnCode = -500;
        SET @Message = N'The item could not be updated.';

        INSERT INTO dbo.AuditLogs (Action, EntityName, EntityId, Description, UserId, IsSuccessful, ErrorMessage, Source)
        VALUES (15, N'Item', CAST(@ItemId AS NVARCHAR(50)), N'sp_UpdateItem failed',
                @CurrentUserId, 0, ERROR_MESSAGE(), N'sp_UpdateItem');
    END CATCH
END
GO

/* -----------------------------------------------------------------------------
   sp_DeleteItem
   -------------------------------------------------------------------------- */
CREATE OR ALTER PROCEDURE dbo.sp_DeleteItem
    @ItemId             INT,
    @CurrentUserId      INT             = 0,
    @ReturnCode         INT             OUTPUT,
    @Message            NVARCHAR(500)   OUTPUT,
    @NewId              INT             OUTPUT,
    @GeneratedNumber    NVARCHAR(30)    OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    SET @ReturnCode = 0; SET @Message = N''; SET @NewId = @ItemId; SET @GeneratedNumber = NULL;

    BEGIN TRY
        DECLARE @ItemName NVARCHAR(250), @Balance DECIMAL(18,4) = 0;

        SELECT @ItemName = ItemName FROM dbo.Items WHERE Id = @ItemId AND IsDeleted = 0;

        IF @ItemName IS NULL
        BEGIN
            SET @ReturnCode = -404; SET @Message = N'The item was not found.'; RETURN;
        END

        SELECT @Balance = ISNULL(SUM(TotalQuantity), 0)
        FROM dbo.vw_ItemStockSummary WHERE ItemId = @ItemId;

        IF @Balance > 0
        BEGIN
            SET @ReturnCode = -409;
            SET @Message = CONCAT(N'This item still holds ', CAST(@Balance AS NVARCHAR(30)),
                                  N' in stock and cannot be deleted.');
            RETURN;
        END

        IF EXISTS (SELECT 1 FROM dbo.InventoryInwardDetails WHERE ItemId = @ItemId)
           OR EXISTS (SELECT 1 FROM dbo.InventoryOutwardDetails WHERE ItemId = @ItemId)
        BEGIN
            SET @ReturnCode = -409;
            SET @Message = N'This item has transaction history and cannot be deleted. Deactivate it instead.';
            RETURN;
        END

        BEGIN TRANSACTION;

        UPDATE dbo.Items
           SET IsDeleted = 1, IsActive = 0, ModifiedOn = SYSUTCDATETIME(), ModifiedBy = @CurrentUserId
         WHERE Id = @ItemId AND IsDeleted = 0;

        INSERT INTO dbo.AuditLogs (Action, EntityName, EntityId, Description, UserId, Source)
        VALUES (6, N'Item', CAST(@ItemId AS NVARCHAR(50)),
                CONCAT(N'Deleted item ', @ItemName), @CurrentUserId, N'sp_DeleteItem');

        COMMIT TRANSACTION;

        SET @Message = N'Item deleted successfully.';
    END TRY
    BEGIN CATCH
        IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
        SET @ReturnCode = -500;
        SET @Message = N'The item could not be deleted.';
    END CATCH
END
GO

/* -----------------------------------------------------------------------------
   sp_GetItems  -  paged item grid with the derived balance and value
   -------------------------------------------------------------------------- */
CREATE OR ALTER PROCEDURE dbo.sp_GetItems
    @PageNumber     INT             = 1,
    @PageSize       INT             = 25,
    @SearchTerm     NVARCHAR(200)   = NULL,
    @SortColumn     NVARCHAR(50)    = NULL,
    @SortDirection  NVARCHAR(4)     = 'ASC',
    @CategoryId     INT             = NULL,
    @ItemType       INT             = NULL,
    @LowStockOnly   BIT             = NULL,
    @TotalCount     INT             OUTPUT
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @Skip INT = (CASE WHEN @PageNumber < 1 THEN 0 ELSE @PageNumber - 1 END) * @PageSize;
    DECLARE @Search NVARCHAR(210) = CASE WHEN @SearchTerm IS NULL THEN NULL ELSE N'%' + @SearchTerm + N'%' END;

    /* Materialise the filtered set once; both the count and the page read it. */
    DECLARE @Filtered TABLE
    (
        Id              INT PRIMARY KEY,
        ItemCode        NVARCHAR(40),
        ItemName        NVARCHAR(250),
        PartNumber      NVARCHAR(100),
        CategoryName    NVARCHAR(150),
        UnitSymbol      NVARCHAR(10),
        ManufacturerName NVARCHAR(150),
        ItemTypeName    NVARCHAR(30),
        CurrentStock    DECIMAL(18,4),
        ReorderLevel    DECIMAL(18,4),
        AverageCost     DECIMAL(18,4),
        StockValue      DECIMAL(18,2),
        IsActive        BIT,
        IsLowStock      BIT
    );

    INSERT INTO @Filtered
    SELECT
        i.Id,
        i.ItemCode,
        i.ItemName,
        i.PartNumber,
        c.Name,
        u.Symbol,
        m.Name,
        dbo.fn_GetItemTypeName(i.ItemType),
        ISNULL(ss.TotalQuantity, 0),
        i.ReorderLevel,
        i.AverageCost,
        ISNULL(ss.TotalValue, 0),
        i.IsActive,
        CASE WHEN ISNULL(ss.TotalQuantity, 0) <= i.ReorderLevel THEN 1 ELSE 0 END
    FROM dbo.Items AS i
    INNER JOIN dbo.Categories       AS c  ON c.Id = i.CategoryId
    INNER JOIN dbo.Units            AS u  ON u.Id = i.UnitId
    LEFT  JOIN dbo.Manufacturers    AS m  ON m.Id = i.ManufacturerId
    LEFT  JOIN dbo.vw_ItemStockSummary AS ss ON ss.ItemId = i.Id
    WHERE i.IsDeleted = 0
      AND (@CategoryId IS NULL OR i.CategoryId = @CategoryId)
      AND (@ItemType   IS NULL OR i.ItemType = @ItemType)
      AND (@Search     IS NULL OR i.ItemName LIKE @Search
                               OR i.ItemCode LIKE @Search
                               OR i.PartNumber LIKE @Search
                               OR i.Barcode LIKE @Search)
      AND (@LowStockOnly IS NULL OR @LowStockOnly = 0
           OR ISNULL(ss.TotalQuantity, 0) <= i.ReorderLevel);

    SELECT @TotalCount = COUNT(*) FROM @Filtered;

    SELECT
        Id, ItemCode, ItemName, PartNumber, CategoryName, UnitSymbol,
        ManufacturerName, ItemTypeName, CurrentStock, ReorderLevel,
        AverageCost, StockValue, IsActive, IsLowStock
    FROM @Filtered
    ORDER BY
        CASE WHEN @SortDirection = 'ASC'  AND @SortColumn = 'ItemCode'     THEN ItemCode     END ASC,
        CASE WHEN @SortDirection = 'DESC' AND @SortColumn = 'ItemCode'     THEN ItemCode     END DESC,
        CASE WHEN @SortDirection = 'ASC'  AND @SortColumn = 'ItemName'     THEN ItemName     END ASC,
        CASE WHEN @SortDirection = 'DESC' AND @SortColumn = 'ItemName'     THEN ItemName     END DESC,
        CASE WHEN @SortDirection = 'ASC'  AND @SortColumn = 'CategoryName' THEN CategoryName END ASC,
        CASE WHEN @SortDirection = 'DESC' AND @SortColumn = 'CategoryName' THEN CategoryName END DESC,
        CASE WHEN @SortDirection = 'ASC'  AND @SortColumn = 'CurrentStock' THEN CurrentStock END ASC,
        CASE WHEN @SortDirection = 'DESC' AND @SortColumn = 'CurrentStock' THEN CurrentStock END DESC,
        CASE WHEN @SortDirection = 'ASC'  AND @SortColumn = 'StockValue'   THEN StockValue   END ASC,
        CASE WHEN @SortDirection = 'DESC' AND @SortColumn = 'StockValue'   THEN StockValue   END DESC,
        ItemName ASC
    OFFSET @Skip ROWS FETCH NEXT @PageSize ROWS ONLY;
END
GO

/* -----------------------------------------------------------------------------
   sp_GetItemById
   -------------------------------------------------------------------------- */
CREATE OR ALTER PROCEDURE dbo.sp_GetItemById
    @ItemId INT
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        i.Id,
        i.ItemCode,
        i.ItemName,
        i.PartNumber,
        i.AlternatePartNumber,
        i.HsnCode,
        i.Description,
        i.Specification,
        i.CategoryId,
        c.Name                              AS CategoryName,
        i.UnitId,
        u.Name                              AS UnitName,
        u.Symbol                            AS UnitSymbol,
        i.ManufacturerId,
        m.Name                              AS ManufacturerName,
        i.ItemType,
        dbo.fn_GetItemTypeName(i.ItemType)  AS ItemTypeName,
        i.Barcode,
        i.ReorderLevel,
        i.ReorderQuantity,
        i.MinimumStock,
        i.MaximumStock,
        i.StandardCost,
        i.AverageCost,
        i.ShelfLocation,
        i.IsBatchTracked,
        i.IsSerialTracked,
        i.ShelfLifeDays,
        i.TaxRate,
        i.IsActive,
        ISNULL(ss.TotalQuantity, 0)         AS CurrentStock,
        ISNULL(ss.TotalValue, 0)            AS StockValue,
        (SELECT TOP (1) img.FilePath FROM dbo.ItemImages img
          WHERE img.ItemId = i.Id ORDER BY img.IsPrimary DESC, img.Id) AS PrimaryImagePath,
        i.CreatedOn,
        i.ModifiedOn
    FROM dbo.Items AS i
    INNER JOIN dbo.Categories       AS c  ON c.Id = i.CategoryId
    INNER JOIN dbo.Units            AS u  ON u.Id = i.UnitId
    LEFT  JOIN dbo.Manufacturers    AS m  ON m.Id = i.ManufacturerId
    LEFT  JOIN dbo.vw_ItemStockSummary AS ss ON ss.ItemId = i.Id
    WHERE i.Id = @ItemId AND i.IsDeleted = 0;
END
GO

/* -----------------------------------------------------------------------------
   sp_SearchInventory
   Type-ahead search for the document line pickers. Returns the item plus its
   physical, reserved and available balance in the chosen warehouse.
   -------------------------------------------------------------------------- */
CREATE OR ALTER PROCEDURE dbo.sp_SearchInventory
    @SearchTerm     NVARCHAR(200)   = NULL,
    @WarehouseId    INT,
    @MaxResults     INT             = 20,
    @ExactMatch     BIT             = 0
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @Search NVARCHAR(210) = CASE WHEN @SearchTerm IS NULL THEN NULL ELSE N'%' + @SearchTerm + N'%' END;

    SELECT TOP (@MaxResults)
        i.Id                                AS ItemId,
        i.ItemCode,
        i.ItemName,
        i.PartNumber,
        u.Symbol                            AS UnitSymbol,
        i.UnitId,
        i.AverageCost,
        ISNULL(i.TaxRate, 0)                AS TaxRate,
        ISNULL(cs.Quantity, 0)              AS CurrentStock,
        ISNULL(cs.ReservedQuantity, 0)      AS ReservedStock,
        ISNULL(cs.AvailableQuantity, 0)     AS AvailableStock,
        i.IsBatchTracked,
        i.IsSerialTracked,
        i.ShelfLocation
    FROM dbo.Items AS i
    INNER JOIN dbo.Units AS u ON u.Id = i.UnitId
    LEFT JOIN dbo.vw_CurrentStock AS cs
           ON cs.ItemId = i.Id AND cs.WarehouseId = @WarehouseId
    WHERE i.IsDeleted = 0
      AND i.IsActive = 1
      AND (
            @SearchTerm IS NULL
            OR (@ExactMatch = 1 AND (i.Barcode = @SearchTerm OR i.ItemCode = @SearchTerm))
            OR (@ExactMatch = 0 AND (i.ItemName   LIKE @Search
                                  OR i.ItemCode   LIKE @Search
                                  OR i.PartNumber LIKE @Search
                                  OR i.Barcode    LIKE @Search))
          )
    ORDER BY
        CASE WHEN i.ItemCode = @SearchTerm OR i.Barcode = @SearchTerm THEN 0 ELSE 1 END,
        i.ItemName;
END
GO

/* -----------------------------------------------------------------------------
   sp_GetItemStockBalance
   -------------------------------------------------------------------------- */
CREATE OR ALTER PROCEDURE dbo.sp_GetItemStockBalance
    @ItemId         INT,
    @WarehouseId    INT
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @Balance  DECIMAL(18,4) = dbo.fn_GetStockBalance(@ItemId, @WarehouseId);
    DECLARE @Reserved DECIMAL(18,4) = dbo.fn_GetReservedQuantity(@ItemId, @WarehouseId);

    SELECT
        i.Id                    AS ItemId,
        i.ItemCode,
        i.ItemName,
        i.PartNumber,
        u.Symbol                AS UnitSymbol,
        i.UnitId,
        i.AverageCost,
        ISNULL(i.TaxRate, 0)    AS TaxRate,
        @Balance                AS CurrentStock,
        @Reserved               AS ReservedStock,
        CASE WHEN @Balance - @Reserved < 0 THEN 0 ELSE @Balance - @Reserved END AS AvailableStock,
        i.IsBatchTracked,
        i.IsSerialTracked,
        i.ShelfLocation
    FROM dbo.Items AS i
    INNER JOIN dbo.Units AS u ON u.Id = i.UnitId
    WHERE i.Id = @ItemId AND i.IsDeleted = 0;
END
GO

/* -----------------------------------------------------------------------------
   Vendor procedures
   -------------------------------------------------------------------------- */
CREATE OR ALTER PROCEDURE dbo.sp_InsertVendor
    @VendorId           INT             = 0,
    @Code               NVARCHAR(30),
    @Name               NVARCHAR(150),
    @Description        NVARCHAR(500)   = NULL,
    @Address            NVARCHAR(400)   = NULL,
    @City               NVARCHAR(100)   = NULL,
    @State              NVARCHAR(100)   = NULL,
    @PinCode            NVARCHAR(20)    = NULL,
    @ContactPerson      NVARCHAR(150)   = NULL,
    @ContactNumber      NVARCHAR(20)    = NULL,
    @Email              NVARCHAR(150)   = NULL,
    @GstNumber          NVARCHAR(20)    = NULL,
    @PanNumber          NVARCHAR(15)    = NULL,
    @BankName           NVARCHAR(100)   = NULL,
    @BankAccountNumber  NVARCHAR(30)    = NULL,
    @IfscCode           NVARCHAR(15)    = NULL,
    @CreditDays         INT             = NULL,
    @Rating             INT             = NULL,
    @IsBlacklisted      BIT             = 0,
    @DisplayOrder       INT             = 0,
    @IsActive           BIT             = 1,
    @CurrentUserId      INT             = 0,
    @ReturnCode         INT             OUTPUT,
    @Message            NVARCHAR(500)   OUTPUT,
    @NewId              INT             OUTPUT,
    @GeneratedNumber    NVARCHAR(30)    OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    SET @ReturnCode = 0; SET @Message = N''; SET @NewId = NULL; SET @GeneratedNumber = NULL;

    BEGIN TRY
        IF @Name IS NULL OR LTRIM(RTRIM(@Name)) = N''
        BEGIN
            SET @ReturnCode = -400; SET @Message = N'Vendor name is required.'; RETURN;
        END

        SET @Code      = LTRIM(RTRIM(@Code));
        SET @Name      = LTRIM(RTRIM(@Name));
        SET @GstNumber = NULLIF(UPPER(LTRIM(RTRIM(ISNULL(@GstNumber, N'')))), N'');
        SET @PanNumber = NULLIF(UPPER(LTRIM(RTRIM(ISNULL(@PanNumber, N'')))), N'');
        SET @IfscCode  = NULLIF(UPPER(LTRIM(RTRIM(ISNULL(@IfscCode,  N'')))), N'');

        BEGIN TRANSACTION;

        IF EXISTS (SELECT 1 FROM dbo.Vendors WHERE Code = @Code AND IsDeleted = 0)
        BEGIN
            ROLLBACK TRANSACTION;
            SET @ReturnCode = -409; SET @Message = N'A vendor with this code already exists.'; RETURN;
        END

        IF @GstNumber IS NOT NULL AND EXISTS (SELECT 1 FROM dbo.Vendors WHERE GstNumber = @GstNumber AND IsDeleted = 0)
        BEGIN
            ROLLBACK TRANSACTION;
            SET @ReturnCode = -409; SET @Message = N'This GSTIN is already registered against another vendor.'; RETURN;
        END

        INSERT INTO dbo.Vendors
            (Code, Name, Description, Address, City, [State], PinCode, ContactPerson, ContactNumber, Email,
             GstNumber, PanNumber, BankName, BankAccountNumber, IfscCode, CreditDays, Rating,
             IsBlacklisted, DisplayOrder, IsActive, CreatedBy)
        VALUES
            (@Code, @Name, @Description, @Address, @City, @State, @PinCode, @ContactPerson, @ContactNumber, @Email,
             @GstNumber, @PanNumber, @BankName, @BankAccountNumber, @IfscCode, @CreditDays, @Rating,
             @IsBlacklisted, @DisplayOrder, @IsActive, @CurrentUserId);

        SET @NewId = CAST(SCOPE_IDENTITY() AS INT);
        SET @GeneratedNumber = @Code;

        INSERT INTO dbo.AuditLogs (Action, EntityName, EntityId, Description, UserId, Source)
        VALUES (4, N'Vendor', CAST(@NewId AS NVARCHAR(50)),
                CONCAT(N'Created vendor ', @Code, N' - ', @Name), @CurrentUserId, N'sp_InsertVendor');

        COMMIT TRANSACTION;

        SET @Message = N'Vendor created successfully.';
    END TRY
    BEGIN CATCH
        IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
        SET @ReturnCode = -500;
        SET @Message = N'The vendor could not be saved.';

        INSERT INTO dbo.AuditLogs (Action, EntityName, Description, UserId, IsSuccessful, ErrorMessage, Source)
        VALUES (15, N'Vendor', CONCAT(N'sp_InsertVendor failed for ', @Name),
                @CurrentUserId, 0, ERROR_MESSAGE(), N'sp_InsertVendor');
    END CATCH
END
GO

CREATE OR ALTER PROCEDURE dbo.sp_UpdateVendor
    @VendorId           INT,
    @Code               NVARCHAR(30),
    @Name               NVARCHAR(150),
    @Description        NVARCHAR(500)   = NULL,
    @Address            NVARCHAR(400)   = NULL,
    @City               NVARCHAR(100)   = NULL,
    @State              NVARCHAR(100)   = NULL,
    @PinCode            NVARCHAR(20)    = NULL,
    @ContactPerson      NVARCHAR(150)   = NULL,
    @ContactNumber      NVARCHAR(20)    = NULL,
    @Email              NVARCHAR(150)   = NULL,
    @GstNumber          NVARCHAR(20)    = NULL,
    @PanNumber          NVARCHAR(15)    = NULL,
    @BankName           NVARCHAR(100)   = NULL,
    @BankAccountNumber  NVARCHAR(30)    = NULL,
    @IfscCode           NVARCHAR(15)    = NULL,
    @CreditDays         INT             = NULL,
    @Rating             INT             = NULL,
    @IsBlacklisted      BIT             = 0,
    @DisplayOrder       INT             = 0,
    @IsActive           BIT             = 1,
    @CurrentUserId      INT             = 0,
    @ReturnCode         INT             OUTPUT,
    @Message            NVARCHAR(500)   OUTPUT,
    @NewId              INT             OUTPUT,
    @GeneratedNumber    NVARCHAR(30)    OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    SET @ReturnCode = 0; SET @Message = N''; SET @NewId = @VendorId; SET @GeneratedNumber = NULL;

    BEGIN TRY
        SET @Code      = LTRIM(RTRIM(@Code));
        SET @Name      = LTRIM(RTRIM(@Name));
        SET @GstNumber = NULLIF(UPPER(LTRIM(RTRIM(ISNULL(@GstNumber, N'')))), N'');
        SET @PanNumber = NULLIF(UPPER(LTRIM(RTRIM(ISNULL(@PanNumber, N'')))), N'');
        SET @IfscCode  = NULLIF(UPPER(LTRIM(RTRIM(ISNULL(@IfscCode,  N'')))), N'');

        BEGIN TRANSACTION;

        IF EXISTS (SELECT 1 FROM dbo.Vendors WHERE Code = @Code AND Id <> @VendorId AND IsDeleted = 0)
        BEGIN
            ROLLBACK TRANSACTION;
            SET @ReturnCode = -409; SET @Message = N'Another vendor already uses this code.'; RETURN;
        END

        IF @GstNumber IS NOT NULL
           AND EXISTS (SELECT 1 FROM dbo.Vendors WHERE GstNumber = @GstNumber AND Id <> @VendorId AND IsDeleted = 0)
        BEGIN
            ROLLBACK TRANSACTION;
            SET @ReturnCode = -409; SET @Message = N'This GSTIN is already registered against another vendor.'; RETURN;
        END

        UPDATE dbo.Vendors
           SET Code = @Code, Name = @Name, Description = @Description, Address = @Address,
               City = @City, [State] = @State, PinCode = @PinCode,
               ContactPerson = @ContactPerson, ContactNumber = @ContactNumber, Email = @Email,
               GstNumber = @GstNumber, PanNumber = @PanNumber, BankName = @BankName,
               BankAccountNumber = @BankAccountNumber, IfscCode = @IfscCode,
               CreditDays = @CreditDays, Rating = @Rating, IsBlacklisted = @IsBlacklisted,
               DisplayOrder = @DisplayOrder, IsActive = @IsActive,
               ModifiedOn = SYSUTCDATETIME(), ModifiedBy = @CurrentUserId
         WHERE Id = @VendorId AND IsDeleted = 0;

        IF @@ROWCOUNT = 0
        BEGIN
            ROLLBACK TRANSACTION;
            SET @ReturnCode = -404; SET @Message = N'The vendor was not found.'; RETURN;
        END

        INSERT INTO dbo.AuditLogs (Action, EntityName, EntityId, Description, UserId, Source)
        VALUES (5, N'Vendor', CAST(@VendorId AS NVARCHAR(50)),
                CONCAT(N'Updated vendor ', @Name), @CurrentUserId, N'sp_UpdateVendor');

        COMMIT TRANSACTION;

        SET @GeneratedNumber = @Code;
        SET @Message = N'Vendor updated successfully.';
    END TRY
    BEGIN CATCH
        IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
        SET @ReturnCode = -500;
        SET @Message = N'The vendor could not be updated.';
    END CATCH
END
GO

CREATE OR ALTER PROCEDURE dbo.sp_DeleteVendor
    @VendorId           INT,
    @CurrentUserId      INT             = 0,
    @ReturnCode         INT             OUTPUT,
    @Message            NVARCHAR(500)   OUTPUT,
    @NewId              INT             OUTPUT,
    @GeneratedNumber    NVARCHAR(30)    OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    SET @ReturnCode = 0; SET @Message = N''; SET @NewId = @VendorId; SET @GeneratedNumber = NULL;

    BEGIN TRY
        DECLARE @VendorName NVARCHAR(150), @ReceiptCount INT;

        SELECT @VendorName = Name FROM dbo.Vendors WHERE Id = @VendorId AND IsDeleted = 0;

        IF @VendorName IS NULL
        BEGIN
            SET @ReturnCode = -404; SET @Message = N'The vendor was not found.'; RETURN;
        END

        SELECT @ReceiptCount = COUNT(*) FROM dbo.InventoryInwardHeader
        WHERE VendorId = @VendorId AND IsDeleted = 0;

        IF @ReceiptCount > 0
        BEGIN
            SET @ReturnCode = -409;
            SET @Message = CONCAT(N'This vendor has ', @ReceiptCount,
                                  N' goods receipt note(s) and cannot be deleted. Deactivate or blacklist instead.');
            RETURN;
        END

        BEGIN TRANSACTION;

        UPDATE dbo.Vendors
           SET IsDeleted = 1, IsActive = 0, ModifiedOn = SYSUTCDATETIME(), ModifiedBy = @CurrentUserId
         WHERE Id = @VendorId AND IsDeleted = 0;

        INSERT INTO dbo.AuditLogs (Action, EntityName, EntityId, Description, UserId, Source)
        VALUES (6, N'Vendor', CAST(@VendorId AS NVARCHAR(50)),
                CONCAT(N'Deleted vendor ', @VendorName), @CurrentUserId, N'sp_DeleteVendor');

        COMMIT TRANSACTION;

        SET @Message = N'Vendor deleted successfully.';
    END TRY
    BEGIN CATCH
        IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
        SET @ReturnCode = -500;
        SET @Message = N'The vendor could not be deleted.';
    END CATCH
END
GO

CREATE OR ALTER PROCEDURE dbo.sp_GetVendors
    @PageNumber         INT             = 1,
    @PageSize           INT             = 25,
    @SearchTerm         NVARCHAR(200)   = NULL,
    @SortColumn         NVARCHAR(50)    = NULL,
    @SortDirection      NVARCHAR(4)     = 'ASC',
    @BlacklistedOnly    BIT             = NULL,
    @TotalCount         INT             OUTPUT
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @Skip INT = (CASE WHEN @PageNumber < 1 THEN 0 ELSE @PageNumber - 1 END) * @PageSize;
    DECLARE @Search NVARCHAR(210) = CASE WHEN @SearchTerm IS NULL THEN NULL ELSE N'%' + @SearchTerm + N'%' END;

    SELECT @TotalCount = COUNT(*)
    FROM dbo.Vendors AS v
    WHERE v.IsDeleted = 0
      AND (@BlacklistedOnly IS NULL OR v.IsBlacklisted = @BlacklistedOnly)
      AND (@Search IS NULL OR v.Name LIKE @Search OR v.Code LIKE @Search
                           OR v.City LIKE @Search OR v.GstNumber LIKE @Search
                           OR v.ContactPerson LIKE @Search);

    SELECT
        v.Id, v.Code, v.Name, v.Description, v.Address, v.City, v.[State], v.PinCode,
        v.ContactPerson, v.ContactNumber, v.Email, v.GstNumber, v.PanNumber,
        v.BankName, v.BankAccountNumber, v.IfscCode, v.CreditDays, v.Rating,
        v.IsBlacklisted, v.DisplayOrder, v.IsActive, v.CreatedOn, v.ModifiedOn,
        ISNULL(agg.ReceiptCount, 0)     AS TotalReceipts,
        ISNULL(agg.PurchaseValue, 0)    AS TotalPurchaseValue,
        agg.LastSupplyDate
    FROM dbo.Vendors AS v
    OUTER APPLY
    (
        SELECT COUNT(*) AS ReceiptCount, SUM(h.GrandTotal) AS PurchaseValue, MAX(h.GrnDate) AS LastSupplyDate
        FROM dbo.InventoryInwardHeader AS h
        WHERE h.VendorId = v.Id AND h.IsDeleted = 0 AND h.Status = 3
    ) AS agg
    WHERE v.IsDeleted = 0
      AND (@BlacklistedOnly IS NULL OR v.IsBlacklisted = @BlacklistedOnly)
      AND (@Search IS NULL OR v.Name LIKE @Search OR v.Code LIKE @Search
                           OR v.City LIKE @Search OR v.GstNumber LIKE @Search
                           OR v.ContactPerson LIKE @Search)
    ORDER BY
        CASE WHEN @SortDirection = 'ASC'  AND @SortColumn = 'Code' THEN v.Code END ASC,
        CASE WHEN @SortDirection = 'DESC' AND @SortColumn = 'Code' THEN v.Code END DESC,
        CASE WHEN @SortDirection = 'ASC'  AND @SortColumn = 'Name' THEN v.Name END ASC,
        CASE WHEN @SortDirection = 'DESC' AND @SortColumn = 'Name' THEN v.Name END DESC,
        CASE WHEN @SortDirection = 'ASC'  AND @SortColumn = 'City' THEN v.City END ASC,
        CASE WHEN @SortDirection = 'DESC' AND @SortColumn = 'City' THEN v.City END DESC,
        v.Name ASC
    OFFSET @Skip ROWS FETCH NEXT @PageSize ROWS ONLY;
END
GO

CREATE OR ALTER PROCEDURE dbo.sp_GetVendorById
    @VendorId INT
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        v.Id, v.Code, v.Name, v.Description, v.Address, v.City, v.[State], v.PinCode,
        v.ContactPerson, v.ContactNumber, v.Email, v.GstNumber, v.PanNumber,
        v.BankName, v.BankAccountNumber, v.IfscCode, v.CreditDays, v.Rating,
        v.IsBlacklisted, v.DisplayOrder, v.IsActive, v.CreatedOn, v.ModifiedOn,
        ISNULL(agg.ReceiptCount, 0)     AS TotalReceipts,
        ISNULL(agg.PurchaseValue, 0)    AS TotalPurchaseValue,
        agg.LastSupplyDate
    FROM dbo.Vendors AS v
    OUTER APPLY
    (
        SELECT COUNT(*) AS ReceiptCount, SUM(h.GrandTotal) AS PurchaseValue, MAX(h.GrnDate) AS LastSupplyDate
        FROM dbo.InventoryInwardHeader AS h
        WHERE h.VendorId = v.Id AND h.IsDeleted = 0 AND h.Status = 3
    ) AS agg
    WHERE v.Id = @VendorId AND v.IsDeleted = 0;
END
GO

PRINT 'Item and vendor procedures created.';
GO
