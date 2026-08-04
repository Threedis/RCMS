/* =============================================================================
   04_GRN.sql  -  Goods Receipt Note lifecycle

       Draft -> Pending Approval -> Approved (stock posted) | Rejected

   Stock is posted only by sp_ApproveGRN, and only inside a transaction that
   also writes the ledger, refreshes the item's weighted-average cost, stamps
   the header and records the approval trail. Either all of that happens or
   none of it does.
   ========================================================================== */

SET NOCOUNT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

/* -----------------------------------------------------------------------------
   sp_InsertGRN  -  creates a draft with its lines
   -------------------------------------------------------------------------- */
CREATE OR ALTER PROCEDURE dbo.sp_InsertGRN
    @GrnId                  INT             = 0,
    @GrnDate                DATE,
    @PurchaseOrderNumber    NVARCHAR(50)    = NULL,
    @PurchaseOrderDate      DATE            = NULL,
    @DeliveryChallanNumber  NVARCHAR(50)    = NULL,
    @DeliveryChallanDate    DATE            = NULL,
    @InvoiceNumber          NVARCHAR(50)    = NULL,
    @InvoiceDate            DATE            = NULL,
    @VendorId               INT,
    @CourierId              INT             = NULL,
    @ConsignmentNumber      NVARCHAR(50)    = NULL,
    @WarehouseId            INT,
    @Remarks                NVARCHAR(1000)  = NULL,
    @Details                dbo.GrnDetailType READONLY,
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
        /* ---- Validate before opening a transaction ---------------------- */
        IF NOT EXISTS (SELECT 1 FROM @Details)
        BEGIN
            SET @ReturnCode = -400; SET @Message = N'Add at least one item line.'; RETURN;
        END

        IF @GrnDate > CAST(SYSDATETIME() AS DATE)
        BEGIN
            SET @ReturnCode = -400; SET @Message = N'The GRN date cannot be in the future.'; RETURN;
        END

        IF NOT EXISTS (SELECT 1 FROM dbo.Vendors WHERE Id = @VendorId AND IsDeleted = 0 AND IsActive = 1)
        BEGIN
            SET @ReturnCode = -400; SET @Message = N'Select a valid, active vendor.'; RETURN;
        END

        IF EXISTS (SELECT 1 FROM dbo.Vendors WHERE Id = @VendorId AND IsBlacklisted = 1)
        BEGIN
            SET @ReturnCode = -409; SET @Message = N'This vendor is blacklisted and cannot supply material.'; RETURN;
        END

        IF NOT EXISTS (SELECT 1 FROM dbo.Warehouses WHERE Id = @WarehouseId AND IsDeleted = 0 AND IsActive = 1)
        BEGIN
            SET @ReturnCode = -400; SET @Message = N'Select a valid, active warehouse.'; RETURN;
        END

        IF EXISTS (SELECT 1 FROM @Details WHERE QuantityAccepted + QuantityRejected <> QuantityReceived)
        BEGIN
            SET @ReturnCode = -400;
            SET @Message = N'On every line, accepted plus rejected quantity must equal the received quantity.';
            RETURN;
        END

        IF EXISTS (SELECT 1 FROM @Details WHERE QuantityReceived <= 0 OR UnitCost < 0)
        BEGIN
            SET @ReturnCode = -400;
            SET @Message = N'Received quantity must be greater than zero and unit cost cannot be negative.';
            RETURN;
        END

        IF EXISTS (SELECT d.ItemId FROM @Details AS d
                   GROUP BY d.ItemId, ISNULL(d.BatchNumber, N''), ISNULL(d.SerialNumber, N'')
                   HAVING COUNT(*) > 1)
        BEGIN
            SET @ReturnCode = -409;
            SET @Message = N'The same item, batch and serial combination appears on more than one line.';
            RETURN;
        END

        IF EXISTS (SELECT 1 FROM @Details AS d
                   LEFT JOIN dbo.Items AS i ON i.Id = d.ItemId AND i.IsDeleted = 0
                   WHERE i.Id IS NULL)
        BEGIN
            SET @ReturnCode = -400; SET @Message = N'One or more selected items no longer exist.'; RETURN;
        END

        /* Batch and serial tracking are item level rules. */
        IF EXISTS (SELECT 1 FROM @Details AS d
                   INNER JOIN dbo.Items AS i ON i.Id = d.ItemId
                   WHERE i.IsBatchTracked = 1 AND (d.BatchNumber IS NULL OR LTRIM(RTRIM(d.BatchNumber)) = N''))
        BEGIN
            SET @ReturnCode = -400;
            SET @Message = N'A batch number is required for every batch tracked item.';
            RETURN;
        END

        IF EXISTS (SELECT 1 FROM @Details AS d
                   INNER JOIN dbo.Items AS i ON i.Id = d.ItemId
                   WHERE i.IsSerialTracked = 1 AND (d.SerialNumber IS NULL OR LTRIM(RTRIM(d.SerialNumber)) = N''))
        BEGIN
            SET @ReturnCode = -400;
            SET @Message = N'A serial number is required for every serial tracked item.';
            RETURN;
        END

        /* Duplicate challan detection: the same vendor and challan number is
           almost always a double entry. */
        IF @DeliveryChallanNumber IS NOT NULL
           AND EXISTS (SELECT 1 FROM dbo.InventoryInwardHeader
                       WHERE VendorId = @VendorId
                         AND DeliveryChallanNumber = @DeliveryChallanNumber
                         AND IsDeleted = 0)
        BEGIN
            SET @ReturnCode = -409;
            SET @Message = CONCAT(N'Delivery challan ''', @DeliveryChallanNumber,
                                  N''' has already been recorded for this vendor.');
            RETURN;
        END

        BEGIN TRANSACTION;

        DECLARE @Number NVARCHAR(30);
        EXEC dbo.sp_GetNextDocumentNumber
             @SequenceKey = N'GRN', @Date = @GrnDate, @UseYear = 1, @Number = @Number OUTPUT;

        INSERT INTO dbo.InventoryInwardHeader
            (GrnNumber, GrnDate, PurchaseOrderNumber, PurchaseOrderDate,
             DeliveryChallanNumber, DeliveryChallanDate, InvoiceNumber, InvoiceDate,
             VendorId, CourierId, ConsignmentNumber, WarehouseId, ReceivedBy,
             Status, Remarks, CreatedBy)
        VALUES
            (@Number, @GrnDate, @PurchaseOrderNumber, @PurchaseOrderDate,
             @DeliveryChallanNumber, @DeliveryChallanDate, @InvoiceNumber, @InvoiceDate,
             @VendorId, @CourierId, @ConsignmentNumber, @WarehouseId, @CurrentUserId,
             1 /* Draft */, @Remarks, @CurrentUserId);

        SET @NewId = CAST(SCOPE_IDENTITY() AS INT);

        EXEC dbo.sp_WriteGrnDetails @GrnId = @NewId, @Details = @Details;
        EXEC dbo.sp_RecalculateGrnTotals @GrnId = @NewId;

        INSERT INTO dbo.ApprovalHistory
            (DocumentType, DocumentId, DocumentNumber, [Action], FromStatus, ToStatus, ActionBy, ActionByName, Remarks)
        SELECT 1, @NewId, @Number, 1 /* Submitted (created) */, 1, 1, @CurrentUserId, u.FullName, N'Draft created.'
        FROM dbo.Users AS u WHERE u.Id = @CurrentUserId;

        INSERT INTO dbo.AuditLogs (Action, EntityName, EntityId, Description, UserId, Source)
        VALUES (4, N'GRN', CAST(@NewId AS NVARCHAR(50)),
                CONCAT(N'Created goods receipt note ', @Number), @CurrentUserId, N'sp_InsertGRN');

        COMMIT TRANSACTION;

        SET @GeneratedNumber = @Number;
        SET @Message = CONCAT(N'Goods receipt note ', @Number, N' created.');
    END TRY
    BEGIN CATCH
        IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;

        SET @ReturnCode = -500;
        SET @Message = N'The goods receipt note could not be saved.';

        INSERT INTO dbo.AuditLogs (Action, EntityName, Description, UserId, IsSuccessful, ErrorMessage, Source)
        VALUES (15, N'GRN', N'sp_InsertGRN failed', @CurrentUserId, 0, ERROR_MESSAGE(), N'sp_InsertGRN');
    END CATCH
END
GO

/* -----------------------------------------------------------------------------
   sp_WriteGrnDetails  -  replaces the line set of a draft (internal helper)
   -------------------------------------------------------------------------- */
CREATE OR ALTER PROCEDURE dbo.sp_WriteGrnDetails
    @GrnId      INT,
    @Details    dbo.GrnDetailType READONLY
AS
BEGIN
    SET NOCOUNT ON;

    DELETE FROM dbo.InventoryInwardDetails WHERE InwardHeaderId = @GrnId;

    INSERT INTO dbo.InventoryInwardDetails
        (InwardHeaderId, ItemId, LineNumber, PartNumber,
         QuantityReceived, QuantityAccepted, QuantityRejected, RejectionReason,
         BatchNumber, SerialNumber, ManufacturingDate, ExpiryDate,
         UnitCost, DiscountPercent, TaxRate, TaxAmount, TotalCost, ShelfLocation, Remarks)
    SELECT
        @GrnId,
        d.ItemId,
        d.LineNumber,
        ISNULL(d.PartNumber, i.PartNumber),
        d.QuantityReceived,
        d.QuantityAccepted,
        d.QuantityRejected,
        d.RejectionReason,
        d.BatchNumber,
        d.SerialNumber,
        d.ManufacturingDate,
        d.ExpiryDate,
        d.UnitCost,
        d.DiscountPercent,
        d.TaxRate,
        /* Tax applies to the accepted quantity, after discount. */
        CAST(d.QuantityAccepted * d.UnitCost * (1 - d.DiscountPercent / 100.0) * (d.TaxRate / 100.0) AS DECIMAL(18,2)),
        CAST(d.QuantityAccepted * d.UnitCost * (1 - d.DiscountPercent / 100.0) * (1 + d.TaxRate / 100.0) AS DECIMAL(18,2)),
        ISNULL(d.ShelfLocation, i.ShelfLocation),
        d.Remarks
    FROM @Details AS d
    INNER JOIN dbo.Items AS i ON i.Id = d.ItemId;
END
GO

/* -----------------------------------------------------------------------------
   sp_RecalculateGrnTotals  -  keeps the header totals in step with the lines
   -------------------------------------------------------------------------- */
CREATE OR ALTER PROCEDURE dbo.sp_RecalculateGrnTotals
    @GrnId INT
AS
BEGIN
    SET NOCOUNT ON;

    UPDATE h
       SET h.TotalValue     = ISNULL(t.NetValue, 0),
           h.TotalTaxAmount = ISNULL(t.TaxAmount, 0),
           h.GrandTotal     = ISNULL(t.GrandTotal, 0)
    FROM dbo.InventoryInwardHeader AS h
    OUTER APPLY
    (
        SELECT
            SUM(d.QuantityAccepted * d.UnitCost * (1 - d.DiscountPercent / 100.0))  AS NetValue,
            SUM(d.TaxAmount)                                                        AS TaxAmount,
            SUM(d.TotalCost)                                                        AS GrandTotal
        FROM dbo.InventoryInwardDetails AS d
        WHERE d.InwardHeaderId = h.Id
    ) AS t
    WHERE h.Id = @GrnId;
END
GO

/* -----------------------------------------------------------------------------
   sp_UpdateGRN  -  edits a draft or a rejected document
   -------------------------------------------------------------------------- */
CREATE OR ALTER PROCEDURE dbo.sp_UpdateGRN
    @GrnId                  INT,
    @GrnDate                DATE,
    @PurchaseOrderNumber    NVARCHAR(50)    = NULL,
    @PurchaseOrderDate      DATE            = NULL,
    @DeliveryChallanNumber  NVARCHAR(50)    = NULL,
    @DeliveryChallanDate    DATE            = NULL,
    @InvoiceNumber          NVARCHAR(50)    = NULL,
    @InvoiceDate            DATE            = NULL,
    @VendorId               INT,
    @CourierId              INT             = NULL,
    @ConsignmentNumber      NVARCHAR(50)    = NULL,
    @WarehouseId            INT,
    @Remarks                NVARCHAR(1000)  = NULL,
    @Details                dbo.GrnDetailType READONLY,
    @CurrentUserId          INT             = 0,
    @ReturnCode             INT             OUTPUT,
    @Message                NVARCHAR(500)   OUTPUT,
    @NewId                  INT             OUTPUT,
    @GeneratedNumber        NVARCHAR(30)    OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    SET @ReturnCode = 0; SET @Message = N''; SET @NewId = @GrnId; SET @GeneratedNumber = NULL;

    BEGIN TRY
        DECLARE @Status INT, @Number NVARCHAR(30);

        SELECT @Status = Status, @Number = GrnNumber
        FROM dbo.InventoryInwardHeader
        WHERE Id = @GrnId AND IsDeleted = 0;

        IF @Status IS NULL
        BEGIN
            SET @ReturnCode = -404; SET @Message = N'The goods receipt note was not found.'; RETURN;
        END

        /* Only a draft or a rejected document may be edited: once approved the
           stock has moved and the document is a historical record. */
        IF @Status NOT IN (1, 4)
        BEGIN
            SET @ReturnCode = -409;
            SET @Message = CONCAT(N'A goods receipt note in ''', dbo.fn_GetStatusName(@Status),
                                  N''' status cannot be edited.');
            RETURN;
        END

        IF NOT EXISTS (SELECT 1 FROM @Details)
        BEGIN
            SET @ReturnCode = -400; SET @Message = N'Add at least one item line.'; RETURN;
        END

        IF EXISTS (SELECT 1 FROM @Details WHERE QuantityAccepted + QuantityRejected <> QuantityReceived)
        BEGIN
            SET @ReturnCode = -400;
            SET @Message = N'On every line, accepted plus rejected quantity must equal the received quantity.';
            RETURN;
        END

        BEGIN TRANSACTION;

        UPDATE dbo.InventoryInwardHeader
           SET GrnDate = @GrnDate,
               PurchaseOrderNumber = @PurchaseOrderNumber,
               PurchaseOrderDate = @PurchaseOrderDate,
               DeliveryChallanNumber = @DeliveryChallanNumber,
               DeliveryChallanDate = @DeliveryChallanDate,
               InvoiceNumber = @InvoiceNumber,
               InvoiceDate = @InvoiceDate,
               VendorId = @VendorId,
               CourierId = @CourierId,
               ConsignmentNumber = @ConsignmentNumber,
               WarehouseId = @WarehouseId,
               Remarks = @Remarks,
               Status = 1,      /* editing returns a rejected document to draft */
               ModifiedOn = SYSUTCDATETIME(),
               ModifiedBy = @CurrentUserId
         WHERE Id = @GrnId;

        EXEC dbo.sp_WriteGrnDetails @GrnId = @GrnId, @Details = @Details;
        EXEC dbo.sp_RecalculateGrnTotals @GrnId = @GrnId;

        INSERT INTO dbo.AuditLogs (Action, EntityName, EntityId, Description, UserId, Source)
        VALUES (5, N'GRN', CAST(@GrnId AS NVARCHAR(50)),
                CONCAT(N'Updated goods receipt note ', @Number), @CurrentUserId, N'sp_UpdateGRN');

        COMMIT TRANSACTION;

        SET @GeneratedNumber = @Number;
        SET @Message = CONCAT(N'Goods receipt note ', @Number, N' updated.');
    END TRY
    BEGIN CATCH
        IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
        SET @ReturnCode = -500;
        SET @Message = N'The goods receipt note could not be updated.';

        INSERT INTO dbo.AuditLogs (Action, EntityName, EntityId, Description, UserId, IsSuccessful, ErrorMessage, Source)
        VALUES (15, N'GRN', CAST(@GrnId AS NVARCHAR(50)), N'sp_UpdateGRN failed',
                @CurrentUserId, 0, ERROR_MESSAGE(), N'sp_UpdateGRN');
    END CATCH
END
GO

/* -----------------------------------------------------------------------------
   sp_DeleteGRN  -  a draft only; anything approved is history
   -------------------------------------------------------------------------- */
CREATE OR ALTER PROCEDURE dbo.sp_DeleteGRN
    @GrnId              INT,
    @CurrentUserId      INT             = 0,
    @ReturnCode         INT             OUTPUT,
    @Message            NVARCHAR(500)   OUTPUT,
    @NewId              INT             OUTPUT,
    @GeneratedNumber    NVARCHAR(30)    OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    SET @ReturnCode = 0; SET @Message = N''; SET @NewId = @GrnId; SET @GeneratedNumber = NULL;

    BEGIN TRY
        DECLARE @Status INT, @Number NVARCHAR(30);

        SELECT @Status = Status, @Number = GrnNumber
        FROM dbo.InventoryInwardHeader WHERE Id = @GrnId AND IsDeleted = 0;

        IF @Status IS NULL
        BEGIN
            SET @ReturnCode = -404; SET @Message = N'The goods receipt note was not found.'; RETURN;
        END

        IF @Status <> 1
        BEGIN
            SET @ReturnCode = -409;
            SET @Message = N'Only a draft goods receipt note can be deleted.';
            RETURN;
        END

        BEGIN TRANSACTION;

        UPDATE dbo.InventoryInwardHeader
           SET IsDeleted = 1, IsActive = 0, Status = 7 /* Cancelled */,
               ModifiedOn = SYSUTCDATETIME(), ModifiedBy = @CurrentUserId
         WHERE Id = @GrnId;

        INSERT INTO dbo.AuditLogs (Action, EntityName, EntityId, Description, UserId, Source)
        VALUES (6, N'GRN', CAST(@GrnId AS NVARCHAR(50)),
                CONCAT(N'Deleted draft goods receipt note ', @Number), @CurrentUserId, N'sp_DeleteGRN');

        COMMIT TRANSACTION;

        SET @Message = N'Goods receipt note deleted.';
    END TRY
    BEGIN CATCH
        IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
        SET @ReturnCode = -500;
        SET @Message = N'The goods receipt note could not be deleted.';
    END CATCH
END
GO

/* -----------------------------------------------------------------------------
   sp_SubmitGRN  -  Draft -> Pending Approval
   -------------------------------------------------------------------------- */
CREATE OR ALTER PROCEDURE dbo.sp_SubmitGRN
    @GrnId              INT,
    @Remarks            NVARCHAR(1000)  = NULL,
    @CurrentUserId      INT             = 0,
    @ReturnCode         INT             OUTPUT,
    @Message            NVARCHAR(500)   OUTPUT,
    @NewId              INT             OUTPUT,
    @GeneratedNumber    NVARCHAR(30)    OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    SET @ReturnCode = 0; SET @Message = N''; SET @NewId = @GrnId; SET @GeneratedNumber = NULL;

    BEGIN TRY
        DECLARE @Status INT, @Number NVARCHAR(30), @LineCount INT, @AcceptedTotal DECIMAL(18,4);

        SELECT @Status = Status, @Number = GrnNumber
        FROM dbo.InventoryInwardHeader WHERE Id = @GrnId AND IsDeleted = 0;

        IF @Status IS NULL
        BEGIN
            SET @ReturnCode = -404; SET @Message = N'The goods receipt note was not found.'; RETURN;
        END

        IF @Status NOT IN (1, 4)
        BEGIN
            SET @ReturnCode = -409;
            SET @Message = CONCAT(N'A goods receipt note in ''', dbo.fn_GetStatusName(@Status),
                                  N''' status cannot be submitted.');
            RETURN;
        END

        SELECT @LineCount = COUNT(*), @AcceptedTotal = ISNULL(SUM(QuantityAccepted), 0)
        FROM dbo.InventoryInwardDetails WHERE InwardHeaderId = @GrnId;

        IF @LineCount = 0
        BEGIN
            SET @ReturnCode = -400; SET @Message = N'Add at least one item line before submitting.'; RETURN;
        END

        IF @AcceptedTotal <= 0
        BEGIN
            SET @ReturnCode = -400;
            SET @Message = N'Every line was rejected; there is nothing to receive into stock.';
            RETURN;
        END

        BEGIN TRANSACTION;

        UPDATE dbo.InventoryInwardHeader
           SET Status = 2, ModifiedOn = SYSUTCDATETIME(), ModifiedBy = @CurrentUserId
         WHERE Id = @GrnId;

        INSERT INTO dbo.ApprovalHistory
            (DocumentType, DocumentId, DocumentNumber, [Action], FromStatus, ToStatus, ActionBy, ActionByName, Remarks)
        SELECT 1, @GrnId, @Number, 1, @Status, 2, @CurrentUserId, u.FullName,
               ISNULL(@Remarks, N'Submitted for approval.')
        FROM dbo.Users AS u WHERE u.Id = @CurrentUserId;

        INSERT INTO dbo.AuditLogs (Action, EntityName, EntityId, Description, UserId, Source)
        VALUES (9, N'GRN', CAST(@GrnId AS NVARCHAR(50)),
                CONCAT(N'Submitted goods receipt note ', @Number, N' for approval'),
                @CurrentUserId, N'sp_SubmitGRN');

        COMMIT TRANSACTION;

        SET @GeneratedNumber = @Number;
        SET @Message = N'Goods receipt note submitted for approval.';
    END TRY
    BEGIN CATCH
        IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
        SET @ReturnCode = -500;
        SET @Message = N'The goods receipt note could not be submitted.';
    END CATCH
END
GO

/* -----------------------------------------------------------------------------
   sp_ApproveGRN
   Approves or rejects. On approval it posts, for every accepted line and inside
   one transaction:
       1. a stock ledger row carrying the new running balance,
       2. the line's StockBefore / StockAfter snapshot,
       3. the item's new weighted-average cost,
       4. the header stamp and the approval trail entry.
   -------------------------------------------------------------------------- */
CREATE OR ALTER PROCEDURE dbo.sp_ApproveGRN
    @GrnId              INT,
    @Approve            BIT,
    @Remarks            NVARCHAR(1000)  = NULL,
    @CurrentUserId      INT             = 0,
    @ReturnCode         INT             OUTPUT,
    @Message            NVARCHAR(500)   OUTPUT,
    @NewId              INT             OUTPUT,
    @GeneratedNumber    NVARCHAR(30)    OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    SET @ReturnCode = 0; SET @Message = N''; SET @NewId = @GrnId; SET @GeneratedNumber = NULL;

    BEGIN TRY
        DECLARE @Status INT, @Number NVARCHAR(30), @WarehouseId INT, @GrnDate DATE, @ReceivedBy INT;

        SELECT @Status = Status, @Number = GrnNumber, @WarehouseId = WarehouseId,
               @GrnDate = GrnDate, @ReceivedBy = ReceivedBy
        FROM dbo.InventoryInwardHeader WHERE Id = @GrnId AND IsDeleted = 0;

        IF @Status IS NULL
        BEGIN
            SET @ReturnCode = -404; SET @Message = N'The goods receipt note was not found.'; RETURN;
        END

        IF @Status <> 2
        BEGIN
            SET @ReturnCode = -409;
            SET @Message = CONCAT(N'A goods receipt note in ''', dbo.fn_GetStatusName(@Status),
                                  N''' status is not awaiting approval.');
            RETURN;
        END

        IF @Approve = 0 AND (@Remarks IS NULL OR LTRIM(RTRIM(@Remarks)) = N'')
        BEGIN
            SET @ReturnCode = -400; SET @Message = N'A reason is required when rejecting a document.'; RETURN;
        END

        BEGIN TRANSACTION;

        /* ---- Rejection: no stock moves --------------------------------- */
        IF @Approve = 0
        BEGIN
            UPDATE dbo.InventoryInwardHeader
               SET Status = 4, ApprovedBy = @CurrentUserId, ApprovedOn = SYSUTCDATETIME(),
                   ApprovalRemarks = @Remarks, ModifiedOn = SYSUTCDATETIME(), ModifiedBy = @CurrentUserId
             WHERE Id = @GrnId;

            INSERT INTO dbo.ApprovalHistory
                (DocumentType, DocumentId, DocumentNumber, [Action], FromStatus, ToStatus,
                 ActionBy, ActionByName, Remarks)
            SELECT 1, @GrnId, @Number, 3, 2, 4, @CurrentUserId, u.FullName, @Remarks
            FROM dbo.Users AS u WHERE u.Id = @CurrentUserId;

            INSERT INTO dbo.AuditLogs (Action, EntityName, EntityId, Description, UserId, Source)
            VALUES (8, N'GRN', CAST(@GrnId AS NVARCHAR(50)),
                    CONCAT(N'Rejected goods receipt note ', @Number), @CurrentUserId, N'sp_ApproveGRN');

            COMMIT TRANSACTION;

            SET @GeneratedNumber = @Number;
            SET @Message = N'Goods receipt note rejected.';
            RETURN;
        END

        /* ---- Approval: post the stock ---------------------------------- */
        DECLARE @DetailId       INT,
                @ItemId         INT,
                @Accepted       DECIMAL(18,4),
                @UnitCost       DECIMAL(18,4),
                @Batch          NVARCHAR(50),
                @Serial         NVARCHAR(100),
                @Balance        DECIMAL(18,4),
                @AvgCost        DECIMAL(18,4),
                @NewBalance     DECIMAL(18,4),
                @NewAvgCost     DECIMAL(18,4);

        DECLARE line_cursor CURSOR LOCAL FAST_FORWARD FOR
            SELECT d.Id, d.ItemId, d.QuantityAccepted, d.UnitCost, d.BatchNumber, d.SerialNumber
            FROM dbo.InventoryInwardDetails AS d
            WHERE d.InwardHeaderId = @GrnId AND d.QuantityAccepted > 0
            ORDER BY d.LineNumber;

        OPEN line_cursor;
        FETCH NEXT FROM line_cursor INTO @DetailId, @ItemId, @Accepted, @UnitCost, @Batch, @Serial;

        WHILE @@FETCH_STATUS = 0
        BEGIN
            /* Lock the item row: the average cost is read, changed and written,
               so two concurrent approvals must not interleave here. */
            SELECT @AvgCost = AverageCost
            FROM dbo.Items WITH (UPDLOCK, ROWLOCK)
            WHERE Id = @ItemId;

            SET @Balance = dbo.fn_GetStockBalance(@ItemId, @WarehouseId);
            SET @NewBalance = @Balance + @Accepted;

            /* Weighted average: (old value + new value) / new quantity.
               A zero opening balance simply adopts the receipt rate. */
            SET @NewAvgCost =
                CASE
                    WHEN @NewBalance <= 0 THEN @UnitCost
                    ELSE CAST(((@Balance * ISNULL(@AvgCost, 0)) + (@Accepted * @UnitCost)) / @NewBalance
                              AS DECIMAL(18,4))
                END;

            INSERT INTO dbo.StockLedger
                (ItemId, WarehouseId, TransactionDate, MovementType, DocumentType, DocumentId,
                 DocumentNumber, DocumentDetailId, InwardQuantity, OutwardQuantity, BalanceQuantity,
                 UnitCost, Value, BalanceAverageCost, BatchNumber, SerialNumber, Remarks, CreatedBy)
            VALUES
                (@ItemId, @WarehouseId, @GrnDate, 1 /* Inward */, 1 /* GRN */, @GrnId,
                 @Number, @DetailId, @Accepted, 0, @NewBalance,
                 @UnitCost, CAST(@Accepted * @UnitCost AS DECIMAL(18,2)), @NewAvgCost,
                 @Batch, @Serial, CONCAT(N'Receipt against ', @Number), @CurrentUserId);

            UPDATE dbo.InventoryInwardDetails
               SET StockBefore = @Balance, StockAfter = @NewBalance
             WHERE Id = @DetailId;

            UPDATE dbo.Items
               SET AverageCost = @NewAvgCost,
                   StandardCost = @UnitCost,
                   ModifiedOn = SYSUTCDATETIME(),
                   ModifiedBy = @CurrentUserId
             WHERE Id = @ItemId;

            FETCH NEXT FROM line_cursor INTO @DetailId, @ItemId, @Accepted, @UnitCost, @Batch, @Serial;
        END

        CLOSE line_cursor;
        DEALLOCATE line_cursor;

        UPDATE dbo.InventoryInwardHeader
           SET Status = 3, ApprovedBy = @CurrentUserId, ApprovedOn = SYSUTCDATETIME(),
               ApprovalRemarks = @Remarks, ModifiedOn = SYSUTCDATETIME(), ModifiedBy = @CurrentUserId
         WHERE Id = @GrnId;

        INSERT INTO dbo.ApprovalHistory
            (DocumentType, DocumentId, DocumentNumber, [Action], FromStatus, ToStatus,
             ActionBy, ActionByName, Remarks)
        SELECT 1, @GrnId, @Number, 2, 2, 3, @CurrentUserId, u.FullName,
               ISNULL(@Remarks, N'Approved; stock posted.')
        FROM dbo.Users AS u WHERE u.Id = @CurrentUserId;

        INSERT INTO dbo.AuditLogs (Action, EntityName, EntityId, Description, UserId, Source)
        VALUES (7, N'GRN', CAST(@GrnId AS NVARCHAR(50)),
                CONCAT(N'Approved goods receipt note ', @Number, N'; stock updated'),
                @CurrentUserId, N'sp_ApproveGRN');

        COMMIT TRANSACTION;

        SET @GeneratedNumber = @Number;
        SET @Message = N'Goods receipt note approved and stock updated.';
    END TRY
    BEGIN CATCH
        IF CURSOR_STATUS('local', 'line_cursor') >= 0
        BEGIN
            CLOSE line_cursor;
            DEALLOCATE line_cursor;
        END

        IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;

        SET @ReturnCode = -500;
        SET @Message = N'The approval could not be completed; no stock was changed.';

        INSERT INTO dbo.AuditLogs (Action, EntityName, EntityId, Description, UserId, IsSuccessful, ErrorMessage, Source)
        VALUES (15, N'GRN', CAST(@GrnId AS NVARCHAR(50)), N'sp_ApproveGRN failed',
                @CurrentUserId, 0, ERROR_MESSAGE(), N'sp_ApproveGRN');
    END CATCH
END
GO

/* -----------------------------------------------------------------------------
   sp_GetGRNs  -  paged list
   -------------------------------------------------------------------------- */
CREATE OR ALTER PROCEDURE dbo.sp_GetGRNs
    @PageNumber     INT             = 1,
    @PageSize       INT             = 25,
    @SearchTerm     NVARCHAR(200)   = NULL,
    @SortColumn     NVARCHAR(50)    = NULL,
    @SortDirection  NVARCHAR(4)     = 'DESC',
    @FromDate       DATE            = NULL,
    @ToDate         DATE            = NULL,
    @VendorId       INT             = NULL,
    @WarehouseId    INT             = NULL,
    @Status         INT             = NULL,
    @TotalCount     INT             OUTPUT
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @Skip INT = (CASE WHEN @PageNumber < 1 THEN 0 ELSE @PageNumber - 1 END) * @PageSize;
    DECLARE @Search NVARCHAR(210) = CASE WHEN @SearchTerm IS NULL THEN NULL ELSE N'%' + @SearchTerm + N'%' END;

    SELECT @TotalCount = COUNT(*)
    FROM dbo.InventoryInwardHeader AS h
    INNER JOIN dbo.Vendors AS v ON v.Id = h.VendorId
    WHERE h.IsDeleted = 0
      AND (@FromDate    IS NULL OR h.GrnDate >= @FromDate)
      AND (@ToDate      IS NULL OR h.GrnDate <= @ToDate)
      AND (@VendorId    IS NULL OR h.VendorId = @VendorId)
      AND (@WarehouseId IS NULL OR h.WarehouseId = @WarehouseId)
      AND (@Status      IS NULL OR h.Status = @Status)
      AND (@Search      IS NULL OR h.GrnNumber LIKE @Search
                                OR h.PurchaseOrderNumber LIKE @Search
                                OR h.DeliveryChallanNumber LIKE @Search
                                OR v.Name LIKE @Search);

    SELECT
        h.Id,
        h.GrnNumber,
        h.GrnDate,
        h.PurchaseOrderNumber,
        v.Name                              AS VendorName,
        w.Name                              AS WarehouseName,
        ISNULL(d.LineCount, 0)              AS LineCount,
        ISNULL(d.TotalQuantity, 0)          AS TotalQuantity,
        h.GrandTotal,
        h.Status,
        dbo.fn_GetStatusName(h.Status)      AS StatusName,
        ru.FullName                         AS ReceivedByName,
        au.FullName                         AS ApprovedByName,
        h.ApprovedOn
    FROM dbo.InventoryInwardHeader AS h
    INNER JOIN dbo.Vendors    AS v  ON v.Id = h.VendorId
    INNER JOIN dbo.Warehouses AS w  ON w.Id = h.WarehouseId
    LEFT  JOIN dbo.Users      AS ru ON ru.Id = h.ReceivedBy
    LEFT  JOIN dbo.Users      AS au ON au.Id = h.ApprovedBy
    OUTER APPLY
    (
        SELECT COUNT(*) AS LineCount, SUM(dd.QuantityAccepted) AS TotalQuantity
        FROM dbo.InventoryInwardDetails AS dd WHERE dd.InwardHeaderId = h.Id
    ) AS d
    WHERE h.IsDeleted = 0
      AND (@FromDate    IS NULL OR h.GrnDate >= @FromDate)
      AND (@ToDate      IS NULL OR h.GrnDate <= @ToDate)
      AND (@VendorId    IS NULL OR h.VendorId = @VendorId)
      AND (@WarehouseId IS NULL OR h.WarehouseId = @WarehouseId)
      AND (@Status      IS NULL OR h.Status = @Status)
      AND (@Search      IS NULL OR h.GrnNumber LIKE @Search
                                OR h.PurchaseOrderNumber LIKE @Search
                                OR h.DeliveryChallanNumber LIKE @Search
                                OR v.Name LIKE @Search)
    ORDER BY
        CASE WHEN @SortDirection = 'ASC'  AND @SortColumn = 'GrnNumber'  THEN h.GrnNumber  END ASC,
        CASE WHEN @SortDirection = 'DESC' AND @SortColumn = 'GrnNumber'  THEN h.GrnNumber  END DESC,
        CASE WHEN @SortDirection = 'ASC'  AND @SortColumn = 'GrnDate'    THEN h.GrnDate    END ASC,
        CASE WHEN @SortDirection = 'DESC' AND @SortColumn = 'GrnDate'    THEN h.GrnDate    END DESC,
        CASE WHEN @SortDirection = 'ASC'  AND @SortColumn = 'VendorName' THEN v.Name       END ASC,
        CASE WHEN @SortDirection = 'DESC' AND @SortColumn = 'VendorName' THEN v.Name       END DESC,
        CASE WHEN @SortDirection = 'ASC'  AND @SortColumn = 'GrandTotal' THEN h.GrandTotal END ASC,
        CASE WHEN @SortDirection = 'DESC' AND @SortColumn = 'GrandTotal' THEN h.GrandTotal END DESC,
        h.GrnDate DESC, h.Id DESC
    OFFSET @Skip ROWS FETCH NEXT @PageSize ROWS ONLY;
END
GO

/* -----------------------------------------------------------------------------
   sp_GetGRNById  -  four result sets: header, lines, attachments, trail
   -------------------------------------------------------------------------- */
CREATE OR ALTER PROCEDURE dbo.sp_GetGRNById
    @GrnId INT
AS
BEGIN
    SET NOCOUNT ON;

    /* 1. Header */
    SELECT
        h.Id, h.GrnNumber, h.GrnDate, h.PurchaseOrderNumber, h.PurchaseOrderDate,
        h.DeliveryChallanNumber, h.DeliveryChallanDate, h.InvoiceNumber, h.InvoiceDate,
        h.VendorId, v.Name AS VendorName,
        h.CourierId, c.Name AS CourierName, h.ConsignmentNumber,
        h.WarehouseId, w.Name AS WarehouseName,
        h.ReceivedBy, ru.FullName AS ReceivedByName, h.ReceivedOn,
        h.Status, dbo.fn_GetStatusName(h.Status) AS StatusName,
        h.ApprovedBy, au.FullName AS ApprovedByName, h.ApprovedOn, h.ApprovalRemarks,
        h.TotalValue, h.TotalTaxAmount, h.GrandTotal, h.Remarks
    FROM dbo.InventoryInwardHeader AS h
    INNER JOIN dbo.Vendors    AS v  ON v.Id = h.VendorId
    INNER JOIN dbo.Warehouses AS w  ON w.Id = h.WarehouseId
    LEFT  JOIN dbo.Couriers   AS c  ON c.Id = h.CourierId
    LEFT  JOIN dbo.Users      AS ru ON ru.Id = h.ReceivedBy
    LEFT  JOIN dbo.Users      AS au ON au.Id = h.ApprovedBy
    WHERE h.Id = @GrnId AND h.IsDeleted = 0;

    /* 2. Lines */
    SELECT
        d.Id, d.LineNumber, d.ItemId, i.ItemCode, i.ItemName, u.Symbol AS UnitSymbol,
        d.PartNumber, d.QuantityReceived, d.QuantityAccepted, d.QuantityRejected, d.RejectionReason,
        d.BatchNumber, d.SerialNumber, d.ManufacturingDate, d.ExpiryDate,
        d.UnitCost, d.DiscountPercent, d.TaxRate, d.TaxAmount, d.TotalCost,
        d.ShelfLocation, d.StockBefore, d.StockAfter, d.Remarks
    FROM dbo.InventoryInwardDetails AS d
    INNER JOIN dbo.Items AS i ON i.Id = d.ItemId
    INNER JOIN dbo.Units AS u ON u.Id = i.UnitId
    WHERE d.InwardHeaderId = @GrnId
    ORDER BY d.LineNumber;

    /* 3. Attachments */
    SELECT
        a.Id, a.DocumentType, a.DocumentId, a.FileName, a.ContentType, a.FileSizeBytes,
        a.Description, a.UploadedOn, u.FullName AS UploadedByName
    FROM dbo.Attachments AS a
    LEFT JOIN dbo.Users AS u ON u.Id = a.UploadedBy
    WHERE a.DocumentType = 1 AND a.DocumentId = @GrnId AND a.IsDeleted = 0
    ORDER BY a.UploadedOn DESC;

    /* 4. Approval trail */
    SELECT
        a.Id,
        CASE a.[Action]
            WHEN 1 THEN N'Submitted' WHEN 2 THEN N'Approved' WHEN 3 THEN N'Rejected'
            WHEN 4 THEN N'Cancelled' WHEN 5 THEN N'Re-opened' WHEN 6 THEN N'Issued'
            WHEN 7 THEN N'Closed'    ELSE N'Unknown'
        END                                     AS [Action],
        dbo.fn_GetStatusName(a.FromStatus)      AS FromStatus,
        dbo.fn_GetStatusName(a.ToStatus)        AS ToStatus,
        a.ActionByName, a.ActionOn, a.Remarks, a.[Level]
    FROM dbo.ApprovalHistory AS a
    WHERE a.DocumentType = 1 AND a.DocumentId = @GrnId
    ORDER BY a.ActionOn, a.Id;
END
GO

PRINT 'Goods receipt note procedures created.';
GO
