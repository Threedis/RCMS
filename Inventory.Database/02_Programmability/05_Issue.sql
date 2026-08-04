/* =============================================================================
   05_Issue.sql  -  Material Issue lifecycle

       Draft -> Pending Approval (stock reserved) -> Approved (stock deducted)
             -> Issued (dispatched) -> Closed        | Rejected (reservation released)

   The rule that stock can never go negative is enforced here, under a row lock
   taken on the item, so two approvals racing for the last unit cannot both
   succeed. The CHECK constraint on StockLedger.BalanceQuantity is the final
   backstop.
   ========================================================================== */

SET NOCOUNT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

/* -----------------------------------------------------------------------------
   sp_WriteIssueDetails  -  replaces the line set of a draft (internal helper)
   -------------------------------------------------------------------------- */
CREATE OR ALTER PROCEDURE dbo.sp_WriteIssueDetails
    @IssueId    INT,
    @Details    dbo.IssueDetailType READONLY
AS
BEGIN
    SET NOCOUNT ON;

    DELETE FROM dbo.InventoryOutwardDetails WHERE OutwardHeaderId = @IssueId;

    INSERT INTO dbo.InventoryOutwardDetails
        (OutwardHeaderId, ItemId, LineNumber, PartNumber, QuantityRequested,
         QuantityApproved, QuantityIssued, QuantityReserved,
         BatchNumber, SerialNumber, UnitCost, Remarks)
    SELECT
        @IssueId,
        d.ItemId,
        d.LineNumber,
        ISNULL(d.PartNumber, i.PartNumber),
        d.QuantityRequested,
        0, 0, 0,
        d.BatchNumber,
        d.SerialNumber,
        i.AverageCost,      /* valuation rate captured at request time */
        d.Remarks
    FROM @Details AS d
    INNER JOIN dbo.Items AS i ON i.Id = d.ItemId;
END
GO

/* -----------------------------------------------------------------------------
   sp_InsertInventoryIssue
   -------------------------------------------------------------------------- */
CREATE OR ALTER PROCEDURE dbo.sp_InsertInventoryIssue
    @IssueId            INT             = 0,
    @IssueDate          DATE,
    @DepartmentId       INT             = NULL,
    @SiteId             INT             = NULL,
    @EngineerId         INT             = NULL,
    @WarehouseId        INT,
    @ProjectName        NVARCHAR(200)   = NULL,
    @WorkOrderNumber    NVARCHAR(50)    = NULL,
    @Purpose            NVARCHAR(500)   = NULL,
    @Remarks            NVARCHAR(1000)  = NULL,
    @Details            dbo.IssueDetailType READONLY,
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
        IF NOT EXISTS (SELECT 1 FROM @Details)
        BEGIN
            SET @ReturnCode = -400; SET @Message = N'Add at least one item line.'; RETURN;
        END

        IF @IssueDate > CAST(SYSDATETIME() AS DATE)
        BEGIN
            SET @ReturnCode = -400; SET @Message = N'The issue date cannot be in the future.'; RETURN;
        END

        IF @DepartmentId IS NULL AND @SiteId IS NULL AND @EngineerId IS NULL
        BEGIN
            SET @ReturnCode = -400;
            SET @Message = N'Specify at least one of department, site or engineer as the destination.';
            RETURN;
        END

        IF NOT EXISTS (SELECT 1 FROM dbo.Warehouses WHERE Id = @WarehouseId AND IsDeleted = 0 AND IsActive = 1)
        BEGIN
            SET @ReturnCode = -400; SET @Message = N'Select a valid, active warehouse.'; RETURN;
        END

        IF EXISTS (SELECT 1 FROM @Details WHERE QuantityRequested <= 0)
        BEGIN
            SET @ReturnCode = -400; SET @Message = N'Every requested quantity must be greater than zero.'; RETURN;
        END

        IF EXISTS (SELECT d.ItemId FROM @Details AS d
                   GROUP BY d.ItemId, ISNULL(d.BatchNumber, N'')
                   HAVING COUNT(*) > 1)
        BEGIN
            SET @ReturnCode = -409;
            SET @Message = N'The same item and batch appears on more than one line.';
            RETURN;
        END

        /* Availability check at request time. It is re-checked under a lock at
           approval; this one exists so the user finds out immediately. */
        DECLARE @ShortItem NVARCHAR(250), @ShortRequested DECIMAL(18,4), @ShortAvailable DECIMAL(18,4);

        SELECT TOP (1)
            @ShortItem      = i.ItemName,
            @ShortRequested = d.QuantityRequested,
            @ShortAvailable = dbo.fn_GetStockBalance(d.ItemId, @WarehouseId)
                              - dbo.fn_GetReservedQuantity(d.ItemId, @WarehouseId)
        FROM @Details AS d
        INNER JOIN dbo.Items AS i ON i.Id = d.ItemId
        WHERE d.QuantityRequested > dbo.fn_GetStockBalance(d.ItemId, @WarehouseId)
                                     - dbo.fn_GetReservedQuantity(d.ItemId, @WarehouseId);

        IF @ShortItem IS NOT NULL
        BEGIN
            SET @ReturnCode = -409;
            SET @Message = CONCAT(N'''', @ShortItem, N''' has ',
                                  CAST(CAST(@ShortAvailable AS DECIMAL(18,3)) AS NVARCHAR(30)),
                                  N' available but ',
                                  CAST(CAST(@ShortRequested AS DECIMAL(18,3)) AS NVARCHAR(30)),
                                  N' was requested.');
            RETURN;
        END

        BEGIN TRANSACTION;

        DECLARE @Number NVARCHAR(30);
        EXEC dbo.sp_GetNextDocumentNumber
             @SequenceKey = N'ISSUE', @Date = @IssueDate, @UseYear = 1, @Number = @Number OUTPUT;

        INSERT INTO dbo.InventoryOutwardHeader
            (IssueNumber, IssueDate, RequestedBy, DepartmentId, SiteId, EngineerId, WarehouseId,
             ProjectName, WorkOrderNumber, Purpose, Status, Remarks, CreatedBy)
        VALUES
            (@Number, @IssueDate, @CurrentUserId, @DepartmentId, @SiteId, @EngineerId, @WarehouseId,
             @ProjectName, @WorkOrderNumber, @Purpose, 1 /* Draft */, @Remarks, @CurrentUserId);

        SET @NewId = CAST(SCOPE_IDENTITY() AS INT);

        EXEC dbo.sp_WriteIssueDetails @IssueId = @NewId, @Details = @Details;

        INSERT INTO dbo.ApprovalHistory
            (DocumentType, DocumentId, DocumentNumber, [Action], FromStatus, ToStatus, ActionBy, ActionByName, Remarks)
        SELECT 2, @NewId, @Number, 1, 1, 1, @CurrentUserId, u.FullName, N'Request created.'
        FROM dbo.Users AS u WHERE u.Id = @CurrentUserId;

        INSERT INTO dbo.AuditLogs (Action, EntityName, EntityId, Description, UserId, Source)
        VALUES (4, N'Issue', CAST(@NewId AS NVARCHAR(50)),
                CONCAT(N'Created material issue ', @Number), @CurrentUserId, N'sp_InsertInventoryIssue');

        COMMIT TRANSACTION;

        SET @GeneratedNumber = @Number;
        SET @Message = CONCAT(N'Material issue ', @Number, N' created.');
    END TRY
    BEGIN CATCH
        IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
        SET @ReturnCode = -500;
        SET @Message = N'The material issue could not be saved.';

        INSERT INTO dbo.AuditLogs (Action, EntityName, Description, UserId, IsSuccessful, ErrorMessage, Source)
        VALUES (15, N'Issue', N'sp_InsertInventoryIssue failed', @CurrentUserId, 0,
                ERROR_MESSAGE(), N'sp_InsertInventoryIssue');
    END CATCH
END
GO

/* -----------------------------------------------------------------------------
   sp_UpdateInventoryIssue
   -------------------------------------------------------------------------- */
CREATE OR ALTER PROCEDURE dbo.sp_UpdateInventoryIssue
    @IssueId            INT,
    @IssueDate          DATE,
    @DepartmentId       INT             = NULL,
    @SiteId             INT             = NULL,
    @EngineerId         INT             = NULL,
    @WarehouseId        INT,
    @ProjectName        NVARCHAR(200)   = NULL,
    @WorkOrderNumber    NVARCHAR(50)    = NULL,
    @Purpose            NVARCHAR(500)   = NULL,
    @Remarks            NVARCHAR(1000)  = NULL,
    @Details            dbo.IssueDetailType READONLY,
    @CurrentUserId      INT             = 0,
    @ReturnCode         INT             OUTPUT,
    @Message            NVARCHAR(500)   OUTPUT,
    @NewId              INT             OUTPUT,
    @GeneratedNumber    NVARCHAR(30)    OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    SET @ReturnCode = 0; SET @Message = N''; SET @NewId = @IssueId; SET @GeneratedNumber = NULL;

    BEGIN TRY
        DECLARE @Status INT, @Number NVARCHAR(30);

        SELECT @Status = Status, @Number = IssueNumber
        FROM dbo.InventoryOutwardHeader WHERE Id = @IssueId AND IsDeleted = 0;

        IF @Status IS NULL
        BEGIN
            SET @ReturnCode = -404; SET @Message = N'The material issue was not found.'; RETURN;
        END

        IF @Status NOT IN (1, 4)
        BEGIN
            SET @ReturnCode = -409;
            SET @Message = CONCAT(N'A material issue in ''', dbo.fn_GetStatusName(@Status),
                                  N''' status cannot be edited.');
            RETURN;
        END

        IF NOT EXISTS (SELECT 1 FROM @Details)
        BEGIN
            SET @ReturnCode = -400; SET @Message = N'Add at least one item line.'; RETURN;
        END

        IF @DepartmentId IS NULL AND @SiteId IS NULL AND @EngineerId IS NULL
        BEGIN
            SET @ReturnCode = -400;
            SET @Message = N'Specify at least one of department, site or engineer as the destination.';
            RETURN;
        END

        BEGIN TRANSACTION;

        UPDATE dbo.InventoryOutwardHeader
           SET IssueDate = @IssueDate, DepartmentId = @DepartmentId, SiteId = @SiteId,
               EngineerId = @EngineerId, WarehouseId = @WarehouseId, ProjectName = @ProjectName,
               WorkOrderNumber = @WorkOrderNumber, Purpose = @Purpose, Remarks = @Remarks,
               Status = 1,
               ModifiedOn = SYSUTCDATETIME(), ModifiedBy = @CurrentUserId
         WHERE Id = @IssueId;

        EXEC dbo.sp_WriteIssueDetails @IssueId = @IssueId, @Details = @Details;

        INSERT INTO dbo.AuditLogs (Action, EntityName, EntityId, Description, UserId, Source)
        VALUES (5, N'Issue', CAST(@IssueId AS NVARCHAR(50)),
                CONCAT(N'Updated material issue ', @Number), @CurrentUserId, N'sp_UpdateInventoryIssue');

        COMMIT TRANSACTION;

        SET @GeneratedNumber = @Number;
        SET @Message = CONCAT(N'Material issue ', @Number, N' updated.');
    END TRY
    BEGIN CATCH
        IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
        SET @ReturnCode = -500;
        SET @Message = N'The material issue could not be updated.';
    END CATCH
END
GO

/* -----------------------------------------------------------------------------
   sp_DeleteInventoryIssue
   -------------------------------------------------------------------------- */
CREATE OR ALTER PROCEDURE dbo.sp_DeleteInventoryIssue
    @IssueId            INT,
    @CurrentUserId      INT             = 0,
    @ReturnCode         INT             OUTPUT,
    @Message            NVARCHAR(500)   OUTPUT,
    @NewId              INT             OUTPUT,
    @GeneratedNumber    NVARCHAR(30)    OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    SET @ReturnCode = 0; SET @Message = N''; SET @NewId = @IssueId; SET @GeneratedNumber = NULL;

    BEGIN TRY
        DECLARE @Status INT, @Number NVARCHAR(30);

        SELECT @Status = Status, @Number = IssueNumber
        FROM dbo.InventoryOutwardHeader WHERE Id = @IssueId AND IsDeleted = 0;

        IF @Status IS NULL
        BEGIN
            SET @ReturnCode = -404; SET @Message = N'The material issue was not found.'; RETURN;
        END

        IF @Status <> 1
        BEGIN
            SET @ReturnCode = -409; SET @Message = N'Only a draft material issue can be deleted.'; RETURN;
        END

        BEGIN TRANSACTION;

        UPDATE dbo.InventoryOutwardHeader
           SET IsDeleted = 1, IsActive = 0, Status = 7,
               ModifiedOn = SYSUTCDATETIME(), ModifiedBy = @CurrentUserId
         WHERE Id = @IssueId;

        INSERT INTO dbo.AuditLogs (Action, EntityName, EntityId, Description, UserId, Source)
        VALUES (6, N'Issue', CAST(@IssueId AS NVARCHAR(50)),
                CONCAT(N'Deleted draft material issue ', @Number), @CurrentUserId, N'sp_DeleteInventoryIssue');

        COMMIT TRANSACTION;

        SET @Message = N'Material issue deleted.';
    END TRY
    BEGIN CATCH
        IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
        SET @ReturnCode = -500;
        SET @Message = N'The material issue could not be deleted.';
    END CATCH
END
GO

/* -----------------------------------------------------------------------------
   sp_SubmitInventoryIssue  -  Draft -> Pending Approval, reserving the stock
   -------------------------------------------------------------------------- */
CREATE OR ALTER PROCEDURE dbo.sp_SubmitInventoryIssue
    @IssueId            INT,
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
    SET @ReturnCode = 0; SET @Message = N''; SET @NewId = @IssueId; SET @GeneratedNumber = NULL;

    BEGIN TRY
        DECLARE @Status INT, @Number NVARCHAR(30), @WarehouseId INT;

        SELECT @Status = Status, @Number = IssueNumber, @WarehouseId = WarehouseId
        FROM dbo.InventoryOutwardHeader WHERE Id = @IssueId AND IsDeleted = 0;

        IF @Status IS NULL
        BEGIN
            SET @ReturnCode = -404; SET @Message = N'The material issue was not found.'; RETURN;
        END

        IF @Status NOT IN (1, 4)
        BEGIN
            SET @ReturnCode = -409;
            SET @Message = CONCAT(N'A material issue in ''', dbo.fn_GetStatusName(@Status),
                                  N''' status cannot be submitted.');
            RETURN;
        END

        IF NOT EXISTS (SELECT 1 FROM dbo.InventoryOutwardDetails WHERE OutwardHeaderId = @IssueId)
        BEGIN
            SET @ReturnCode = -400; SET @Message = N'Add at least one item line before submitting.'; RETURN;
        END

        BEGIN TRANSACTION;

        /* Re-check availability now that we are inside the transaction. */
        DECLARE @ShortItem NVARCHAR(250);

        SELECT TOP (1) @ShortItem = i.ItemName
        FROM dbo.InventoryOutwardDetails AS d
        INNER JOIN dbo.Items AS i ON i.Id = d.ItemId
        WHERE d.OutwardHeaderId = @IssueId
          AND d.QuantityRequested > dbo.fn_GetStockBalance(d.ItemId, @WarehouseId)
                                    - dbo.fn_GetReservedQuantity(d.ItemId, @WarehouseId);

        IF @ShortItem IS NOT NULL
        BEGIN
            ROLLBACK TRANSACTION;
            SET @ReturnCode = -409;
            SET @Message = CONCAT(N'''', @ShortItem, N''' no longer has enough available stock.');
            RETURN;
        END

        /* Reserve: the reservation is what makes the availability check above
           meaningful for the next request. */
        UPDATE dbo.InventoryOutwardDetails
           SET QuantityReserved = QuantityRequested
         WHERE OutwardHeaderId = @IssueId;

        UPDATE dbo.InventoryOutwardHeader
           SET Status = 2, ModifiedOn = SYSUTCDATETIME(), ModifiedBy = @CurrentUserId
         WHERE Id = @IssueId;

        INSERT INTO dbo.ApprovalHistory
            (DocumentType, DocumentId, DocumentNumber, [Action], FromStatus, ToStatus, ActionBy, ActionByName, Remarks)
        SELECT 2, @IssueId, @Number, 1, @Status, 2, @CurrentUserId, u.FullName,
               ISNULL(@Remarks, N'Submitted for approval; stock reserved.')
        FROM dbo.Users AS u WHERE u.Id = @CurrentUserId;

        INSERT INTO dbo.AuditLogs (Action, EntityName, EntityId, Description, UserId, Source)
        VALUES (9, N'Issue', CAST(@IssueId AS NVARCHAR(50)),
                CONCAT(N'Submitted material issue ', @Number, N' for approval'),
                @CurrentUserId, N'sp_SubmitInventoryIssue');

        COMMIT TRANSACTION;

        SET @GeneratedNumber = @Number;
        SET @Message = N'Material issue submitted for approval. Stock has been reserved.';
    END TRY
    BEGIN CATCH
        IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
        SET @ReturnCode = -500;
        SET @Message = N'The material issue could not be submitted.';
    END CATCH
END
GO

/* -----------------------------------------------------------------------------
   sp_ApproveInventoryIssue
   Approves (deducting stock) or rejects (releasing the reservation).
   The approver may reduce a line's quantity through @Lines; an empty @Lines
   approves every line exactly as requested.
   -------------------------------------------------------------------------- */
CREATE OR ALTER PROCEDURE dbo.sp_ApproveInventoryIssue
    @IssueId            INT,
    @Approve            BIT,
    @Remarks            NVARCHAR(1000)  = NULL,
    @Lines              dbo.LineQuantityType READONLY,
    @CurrentUserId      INT             = 0,
    @ReturnCode         INT             OUTPUT,
    @Message            NVARCHAR(500)   OUTPUT,
    @NewId              INT             OUTPUT,
    @GeneratedNumber    NVARCHAR(30)    OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    SET @ReturnCode = 0; SET @Message = N''; SET @NewId = @IssueId; SET @GeneratedNumber = NULL;

    BEGIN TRY
        DECLARE @Status INT, @Number NVARCHAR(30), @WarehouseId INT, @IssueDate DATE;

        SELECT @Status = Status, @Number = IssueNumber, @WarehouseId = WarehouseId, @IssueDate = IssueDate
        FROM dbo.InventoryOutwardHeader WHERE Id = @IssueId AND IsDeleted = 0;

        IF @Status IS NULL
        BEGIN
            SET @ReturnCode = -404; SET @Message = N'The material issue was not found.'; RETURN;
        END

        IF @Status <> 2
        BEGIN
            SET @ReturnCode = -409;
            SET @Message = CONCAT(N'A material issue in ''', dbo.fn_GetStatusName(@Status),
                                  N''' status is not awaiting approval.');
            RETURN;
        END

        IF @Approve = 0 AND (@Remarks IS NULL OR LTRIM(RTRIM(@Remarks)) = N'')
        BEGIN
            SET @ReturnCode = -400; SET @Message = N'A reason is required when rejecting a document.'; RETURN;
        END

        BEGIN TRANSACTION;

        /* ---- Rejection: release the reservation, move no stock ---------- */
        IF @Approve = 0
        BEGIN
            UPDATE dbo.InventoryOutwardDetails
               SET QuantityReserved = 0, QuantityApproved = 0
             WHERE OutwardHeaderId = @IssueId;

            UPDATE dbo.InventoryOutwardHeader
               SET Status = 4, ApprovedBy = @CurrentUserId, ApprovedOn = SYSUTCDATETIME(),
                   ApprovalRemarks = @Remarks, ModifiedOn = SYSUTCDATETIME(), ModifiedBy = @CurrentUserId
             WHERE Id = @IssueId;

            INSERT INTO dbo.ApprovalHistory
                (DocumentType, DocumentId, DocumentNumber, [Action], FromStatus, ToStatus,
                 ActionBy, ActionByName, Remarks)
            SELECT 2, @IssueId, @Number, 3, 2, 4, @CurrentUserId, u.FullName, @Remarks
            FROM dbo.Users AS u WHERE u.Id = @CurrentUserId;

            INSERT INTO dbo.AuditLogs (Action, EntityName, EntityId, Description, UserId, Source)
            VALUES (8, N'Issue', CAST(@IssueId AS NVARCHAR(50)),
                    CONCAT(N'Rejected material issue ', @Number),
                    @CurrentUserId, N'sp_ApproveInventoryIssue');

            COMMIT TRANSACTION;

            SET @GeneratedNumber = @Number;
            SET @Message = N'Material issue rejected and the reservation released.';
            RETURN;
        END

        /* ---- Approval: set the approved quantities ---------------------- */
        IF EXISTS (SELECT 1 FROM @Lines)
        BEGIN
            /* A line the approver omitted is treated as approved in full. */
            UPDATE d
               SET d.QuantityApproved = ISNULL(l.Quantity, d.QuantityRequested)
            FROM dbo.InventoryOutwardDetails AS d
            LEFT JOIN @Lines AS l ON l.DetailId = d.Id
            WHERE d.OutwardHeaderId = @IssueId;
        END
        ELSE
        BEGIN
            UPDATE dbo.InventoryOutwardDetails
               SET QuantityApproved = QuantityRequested
             WHERE OutwardHeaderId = @IssueId;
        END

        IF EXISTS (SELECT 1 FROM dbo.InventoryOutwardDetails
                   WHERE OutwardHeaderId = @IssueId AND QuantityApproved > QuantityRequested)
        BEGIN
            ROLLBACK TRANSACTION;
            SET @ReturnCode = -400;
            SET @Message = N'An approved quantity cannot exceed the requested quantity.';
            RETURN;
        END

        IF NOT EXISTS (SELECT 1 FROM dbo.InventoryOutwardDetails
                       WHERE OutwardHeaderId = @IssueId AND QuantityApproved > 0)
        BEGIN
            ROLLBACK TRANSACTION;
            SET @ReturnCode = -400;
            SET @Message = N'At least one line must have an approved quantity greater than zero.';
            RETURN;
        END

        /* ---- Post the outward movements -------------------------------- */
        DECLARE @DetailId   INT,
                @ItemId     INT,
                @Approved   DECIMAL(18,4),
                @Batch      NVARCHAR(50),
                @Serial     NVARCHAR(100),
                @Balance    DECIMAL(18,4),
                @Reserved   DECIMAL(18,4),
                @AvgCost    DECIMAL(18,4),
                @NewBalance DECIMAL(18,4),
                @ItemName   NVARCHAR(250);

        DECLARE issue_cursor CURSOR LOCAL FAST_FORWARD FOR
            SELECT d.Id, d.ItemId, d.QuantityApproved, d.BatchNumber, d.SerialNumber, i.ItemName
            FROM dbo.InventoryOutwardDetails AS d
            INNER JOIN dbo.Items AS i ON i.Id = d.ItemId
            WHERE d.OutwardHeaderId = @IssueId AND d.QuantityApproved > 0
            ORDER BY d.LineNumber;

        OPEN issue_cursor;
        FETCH NEXT FROM issue_cursor INTO @DetailId, @ItemId, @Approved, @Batch, @Serial, @ItemName;

        WHILE @@FETCH_STATUS = 0
        BEGIN
            /* Lock the item row for the read-check-write sequence below, so a
               concurrent approval cannot consume the same units. */
            SELECT @AvgCost = AverageCost
            FROM dbo.Items WITH (UPDLOCK, ROWLOCK)
            WHERE Id = @ItemId;

            SET @Balance = dbo.fn_GetStockBalance(@ItemId, @WarehouseId);

            /* This document's own reservation is not an obstacle to itself. */
            SET @Reserved = dbo.fn_GetReservedQuantity(@ItemId, @WarehouseId)
                            - (SELECT ISNULL(SUM(dd.QuantityReserved), 0)
                               FROM dbo.InventoryOutwardDetails AS dd
                               WHERE dd.OutwardHeaderId = @IssueId AND dd.ItemId = @ItemId);

            IF @Approved > @Balance - @Reserved
            BEGIN
                CLOSE issue_cursor;
                DEALLOCATE issue_cursor;
                ROLLBACK TRANSACTION;

                SET @ReturnCode = -409;
                SET @Message = CONCAT(N'''', @ItemName, N''' has only ',
                                      CAST(CAST(@Balance - @Reserved AS DECIMAL(18,3)) AS NVARCHAR(30)),
                                      N' available; ',
                                      CAST(CAST(@Approved AS DECIMAL(18,3)) AS NVARCHAR(30)),
                                      N' cannot be approved.');
                RETURN;
            END

            SET @NewBalance = @Balance - @Approved;

            INSERT INTO dbo.StockLedger
                (ItemId, WarehouseId, TransactionDate, MovementType, DocumentType, DocumentId,
                 DocumentNumber, DocumentDetailId, InwardQuantity, OutwardQuantity, BalanceQuantity,
                 UnitCost, Value, BalanceAverageCost, BatchNumber, SerialNumber, Remarks, CreatedBy)
            VALUES
                (@ItemId, @WarehouseId, @IssueDate, 2 /* Outward */, 2 /* Issue */, @IssueId,
                 @Number, @DetailId, 0, @Approved, @NewBalance,
                 ISNULL(@AvgCost, 0), CAST(@Approved * ISNULL(@AvgCost, 0) AS DECIMAL(18,2)),
                 ISNULL(@AvgCost, 0), @Batch, @Serial,
                 CONCAT(N'Issue against ', @Number), @CurrentUserId);

            UPDATE dbo.InventoryOutwardDetails
               SET StockBefore      = @Balance,
                   StockAfter       = @NewBalance,
                   UnitCost         = ISNULL(@AvgCost, 0),
                   TotalCost        = CAST(@Approved * ISNULL(@AvgCost, 0) AS DECIMAL(18,2)),
                   QuantityReserved = 0    /* the reservation has become a real movement */
             WHERE Id = @DetailId;

            FETCH NEXT FROM issue_cursor INTO @DetailId, @ItemId, @Approved, @Batch, @Serial, @ItemName;
        END

        CLOSE issue_cursor;
        DEALLOCATE issue_cursor;

        /* Lines that were approved at zero release their reservation too. */
        UPDATE dbo.InventoryOutwardDetails
           SET QuantityReserved = 0
         WHERE OutwardHeaderId = @IssueId AND QuantityApproved = 0;

        UPDATE h
           SET h.Status = 3,
               h.ApprovedBy = @CurrentUserId,
               h.ApprovedOn = SYSUTCDATETIME(),
               h.ApprovalRemarks = @Remarks,
               h.TotalValue = ISNULL(t.TotalValue, 0),
               h.ModifiedOn = SYSUTCDATETIME(),
               h.ModifiedBy = @CurrentUserId
        FROM dbo.InventoryOutwardHeader AS h
        OUTER APPLY (SELECT SUM(d.TotalCost) AS TotalValue
                     FROM dbo.InventoryOutwardDetails AS d
                     WHERE d.OutwardHeaderId = h.Id) AS t
        WHERE h.Id = @IssueId;

        INSERT INTO dbo.ApprovalHistory
            (DocumentType, DocumentId, DocumentNumber, [Action], FromStatus, ToStatus,
             ActionBy, ActionByName, Remarks)
        SELECT 2, @IssueId, @Number, 2, 2, 3, @CurrentUserId, u.FullName,
               ISNULL(@Remarks, N'Approved; stock deducted.')
        FROM dbo.Users AS u WHERE u.Id = @CurrentUserId;

        INSERT INTO dbo.AuditLogs (Action, EntityName, EntityId, Description, UserId, Source)
        VALUES (7, N'Issue', CAST(@IssueId AS NVARCHAR(50)),
                CONCAT(N'Approved material issue ', @Number, N'; stock deducted'),
                @CurrentUserId, N'sp_ApproveInventoryIssue');

        COMMIT TRANSACTION;

        SET @GeneratedNumber = @Number;
        SET @Message = N'Material issue approved and stock deducted.';
    END TRY
    BEGIN CATCH
        IF CURSOR_STATUS('local', 'issue_cursor') >= 0
        BEGIN
            CLOSE issue_cursor;
            DEALLOCATE issue_cursor;
        END

        IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;

        SET @ReturnCode = -500;
        SET @Message = N'The approval could not be completed; no stock was changed.';

        INSERT INTO dbo.AuditLogs (Action, EntityName, EntityId, Description, UserId, IsSuccessful, ErrorMessage, Source)
        VALUES (15, N'Issue', CAST(@IssueId AS NVARCHAR(50)), N'sp_ApproveInventoryIssue failed',
                @CurrentUserId, 0, ERROR_MESSAGE(), N'sp_ApproveInventoryIssue');
    END CATCH
END
GO

/* -----------------------------------------------------------------------------
   sp_DispatchInventoryIssue  -  Approved -> Issued (and Closed when complete)
   Stock was already deducted at approval, so this step records the physical
   hand-over and the logistics detail only.
   -------------------------------------------------------------------------- */
CREATE OR ALTER PROCEDURE dbo.sp_DispatchInventoryIssue
    @IssueId            INT,
    @CourierId          INT             = NULL,
    @TrackingNumber     NVARCHAR(50)    = NULL,
    @DispatchDate       DATE            = NULL,
    @ReceiverName       NVARCHAR(150)   = NULL,
    @Remarks            NVARCHAR(1000)  = NULL,
    @Lines              dbo.LineQuantityType READONLY,
    @CurrentUserId      INT             = 0,
    @ReturnCode         INT             OUTPUT,
    @Message            NVARCHAR(500)   OUTPUT,
    @NewId              INT             OUTPUT,
    @GeneratedNumber    NVARCHAR(30)    OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    SET @ReturnCode = 0; SET @Message = N''; SET @NewId = @IssueId; SET @GeneratedNumber = NULL;

    BEGIN TRY
        DECLARE @Status INT, @Number NVARCHAR(30), @IssueDate DATE;

        SELECT @Status = Status, @Number = IssueNumber, @IssueDate = IssueDate
        FROM dbo.InventoryOutwardHeader WHERE Id = @IssueId AND IsDeleted = 0;

        IF @Status IS NULL
        BEGIN
            SET @ReturnCode = -404; SET @Message = N'The material issue was not found.'; RETURN;
        END

        IF @Status NOT IN (3, 5)
        BEGIN
            SET @ReturnCode = -409;
            SET @Message = CONCAT(N'A material issue in ''', dbo.fn_GetStatusName(@Status),
                                  N''' status cannot be dispatched.');
            RETURN;
        END

        IF @DispatchDate IS NOT NULL AND @DispatchDate < @IssueDate
        BEGIN
            SET @ReturnCode = -400;
            SET @Message = N'The dispatch date cannot be earlier than the issue date.';
            RETURN;
        END

        BEGIN TRANSACTION;

        IF EXISTS (SELECT 1 FROM @Lines)
        BEGIN
            UPDATE d
               SET d.QuantityIssued = ISNULL(l.Quantity, d.QuantityApproved)
            FROM dbo.InventoryOutwardDetails AS d
            LEFT JOIN @Lines AS l ON l.DetailId = d.Id
            WHERE d.OutwardHeaderId = @IssueId;
        END
        ELSE
        BEGIN
            UPDATE dbo.InventoryOutwardDetails
               SET QuantityIssued = QuantityApproved
             WHERE OutwardHeaderId = @IssueId;
        END

        IF EXISTS (SELECT 1 FROM dbo.InventoryOutwardDetails
                   WHERE OutwardHeaderId = @IssueId AND QuantityIssued > QuantityApproved)
        BEGIN
            ROLLBACK TRANSACTION;
            SET @ReturnCode = -400;
            SET @Message = N'An issued quantity cannot exceed the approved quantity.';
            RETURN;
        END

        /* A partially issued document stays open; a fully issued one closes. */
        DECLARE @IsComplete BIT =
            CASE WHEN EXISTS (SELECT 1 FROM dbo.InventoryOutwardDetails
                              WHERE OutwardHeaderId = @IssueId AND QuantityIssued < QuantityApproved)
                 THEN 0 ELSE 1 END;

        UPDATE dbo.InventoryOutwardHeader
           SET Status = CASE WHEN @IsComplete = 1 THEN 6 /* Closed */ ELSE 5 /* Issued */ END,
               IssuedBy = @CurrentUserId,
               IssuedOn = SYSUTCDATETIME(),
               CourierId = @CourierId,
               TrackingNumber = @TrackingNumber,
               DispatchDate = ISNULL(@DispatchDate, CAST(SYSDATETIME() AS DATE)),
               ReceiverName = @ReceiverName,
               ClosedOn = CASE WHEN @IsComplete = 1 THEN SYSUTCDATETIME() END,
               ModifiedOn = SYSUTCDATETIME(),
               ModifiedBy = @CurrentUserId
         WHERE Id = @IssueId;

        INSERT INTO dbo.ApprovalHistory
            (DocumentType, DocumentId, DocumentNumber, [Action], FromStatus, ToStatus,
             ActionBy, ActionByName, Remarks)
        SELECT 2, @IssueId, @Number,
               CASE WHEN @IsComplete = 1 THEN 7 ELSE 6 END,
               @Status,
               CASE WHEN @IsComplete = 1 THEN 6 ELSE 5 END,
               @CurrentUserId, u.FullName,
               ISNULL(@Remarks, CASE WHEN @IsComplete = 1
                                     THEN N'Fully issued and closed.'
                                     ELSE N'Partially issued.' END)
        FROM dbo.Users AS u WHERE u.Id = @CurrentUserId;

        INSERT INTO dbo.AuditLogs (Action, EntityName, EntityId, Description, UserId, Source)
        VALUES (10, N'Issue', CAST(@IssueId AS NVARCHAR(50)),
                CONCAT(N'Dispatched material issue ', @Number,
                       CASE WHEN @TrackingNumber IS NULL THEN N''
                            ELSE CONCAT(N' under tracking ', @TrackingNumber) END),
                @CurrentUserId, N'sp_DispatchInventoryIssue');

        COMMIT TRANSACTION;

        SET @GeneratedNumber = @Number;
        SET @Message = CASE WHEN @IsComplete = 1
                            THEN N'Material issue dispatched and closed.'
                            ELSE N'Material issue partially dispatched.' END;
    END TRY
    BEGIN CATCH
        IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
        SET @ReturnCode = -500;
        SET @Message = N'The dispatch could not be recorded.';

        INSERT INTO dbo.AuditLogs (Action, EntityName, EntityId, Description, UserId, IsSuccessful, ErrorMessage, Source)
        VALUES (15, N'Issue', CAST(@IssueId AS NVARCHAR(50)), N'sp_DispatchInventoryIssue failed',
                @CurrentUserId, 0, ERROR_MESSAGE(), N'sp_DispatchInventoryIssue');
    END CATCH
END
GO

/* -----------------------------------------------------------------------------
   sp_GetInventoryIssues  -  paged list
   -------------------------------------------------------------------------- */
CREATE OR ALTER PROCEDURE dbo.sp_GetInventoryIssues
    @PageNumber     INT             = 1,
    @PageSize       INT             = 25,
    @SearchTerm     NVARCHAR(200)   = NULL,
    @SortColumn     NVARCHAR(50)    = NULL,
    @SortDirection  NVARCHAR(4)     = 'DESC',
    @FromDate       DATE            = NULL,
    @ToDate         DATE            = NULL,
    @DepartmentId   INT             = NULL,
    @SiteId         INT             = NULL,
    @EngineerId     INT             = NULL,
    @WarehouseId    INT             = NULL,
    @Status         INT             = NULL,
    @TotalCount     INT             OUTPUT
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @Skip INT = (CASE WHEN @PageNumber < 1 THEN 0 ELSE @PageNumber - 1 END) * @PageSize;
    DECLARE @Search NVARCHAR(210) = CASE WHEN @SearchTerm IS NULL THEN NULL ELSE N'%' + @SearchTerm + N'%' END;

    SELECT @TotalCount = COUNT(*)
    FROM dbo.InventoryOutwardHeader AS h
    LEFT JOIN dbo.Engineers AS e ON e.Id = h.EngineerId
    WHERE h.IsDeleted = 0
      AND (@FromDate     IS NULL OR h.IssueDate >= @FromDate)
      AND (@ToDate       IS NULL OR h.IssueDate <= @ToDate)
      AND (@DepartmentId IS NULL OR h.DepartmentId = @DepartmentId)
      AND (@SiteId       IS NULL OR h.SiteId = @SiteId)
      AND (@EngineerId   IS NULL OR h.EngineerId = @EngineerId)
      AND (@WarehouseId  IS NULL OR h.WarehouseId = @WarehouseId)
      AND (@Status       IS NULL OR h.Status = @Status)
      AND (@Search       IS NULL OR h.IssueNumber LIKE @Search
                                 OR h.ProjectName LIKE @Search
                                 OR h.WorkOrderNumber LIKE @Search
                                 OR e.Name LIKE @Search);

    SELECT
        h.Id,
        h.IssueNumber,
        h.IssueDate,
        dep.Name                            AS DepartmentName,
        s.Name                              AS SiteName,
        e.Name                              AS EngineerName,
        h.ProjectName,
        w.Name                              AS WarehouseName,
        ISNULL(d.LineCount, 0)              AS LineCount,
        ISNULL(d.TotalQuantity, 0)          AS TotalQuantity,
        h.TotalValue,
        h.Status,
        dbo.fn_GetStatusName(h.Status)      AS StatusName,
        ru.FullName                         AS RequestedByName,
        au.FullName                         AS ApprovedByName,
        h.TrackingNumber
    FROM dbo.InventoryOutwardHeader AS h
    INNER JOIN dbo.Warehouses  AS w   ON w.Id = h.WarehouseId
    LEFT  JOIN dbo.Departments AS dep ON dep.Id = h.DepartmentId
    LEFT  JOIN dbo.Sites       AS s   ON s.Id = h.SiteId
    LEFT  JOIN dbo.Engineers   AS e   ON e.Id = h.EngineerId
    LEFT  JOIN dbo.Users       AS ru  ON ru.Id = h.RequestedBy
    LEFT  JOIN dbo.Users       AS au  ON au.Id = h.ApprovedBy
    OUTER APPLY
    (
        SELECT COUNT(*) AS LineCount,
               SUM(CASE WHEN h.Status IN (5, 6) THEN dd.QuantityIssued
                        WHEN h.Status = 3       THEN dd.QuantityApproved
                        ELSE dd.QuantityRequested END) AS TotalQuantity
        FROM dbo.InventoryOutwardDetails AS dd WHERE dd.OutwardHeaderId = h.Id
    ) AS d
    WHERE h.IsDeleted = 0
      AND (@FromDate     IS NULL OR h.IssueDate >= @FromDate)
      AND (@ToDate       IS NULL OR h.IssueDate <= @ToDate)
      AND (@DepartmentId IS NULL OR h.DepartmentId = @DepartmentId)
      AND (@SiteId       IS NULL OR h.SiteId = @SiteId)
      AND (@EngineerId   IS NULL OR h.EngineerId = @EngineerId)
      AND (@WarehouseId  IS NULL OR h.WarehouseId = @WarehouseId)
      AND (@Status       IS NULL OR h.Status = @Status)
      AND (@Search       IS NULL OR h.IssueNumber LIKE @Search
                                 OR h.ProjectName LIKE @Search
                                 OR h.WorkOrderNumber LIKE @Search
                                 OR e.Name LIKE @Search)
    ORDER BY
        CASE WHEN @SortDirection = 'ASC'  AND @SortColumn = 'IssueNumber' THEN h.IssueNumber END ASC,
        CASE WHEN @SortDirection = 'DESC' AND @SortColumn = 'IssueNumber' THEN h.IssueNumber END DESC,
        CASE WHEN @SortDirection = 'ASC'  AND @SortColumn = 'IssueDate'   THEN h.IssueDate   END ASC,
        CASE WHEN @SortDirection = 'DESC' AND @SortColumn = 'IssueDate'   THEN h.IssueDate   END DESC,
        CASE WHEN @SortDirection = 'ASC'  AND @SortColumn = 'TotalValue'  THEN h.TotalValue  END ASC,
        CASE WHEN @SortDirection = 'DESC' AND @SortColumn = 'TotalValue'  THEN h.TotalValue  END DESC,
        h.IssueDate DESC, h.Id DESC
    OFFSET @Skip ROWS FETCH NEXT @PageSize ROWS ONLY;
END
GO

/* -----------------------------------------------------------------------------
   sp_GetInventoryIssueById  -  four result sets: header, lines, attachments, trail
   -------------------------------------------------------------------------- */
CREATE OR ALTER PROCEDURE dbo.sp_GetInventoryIssueById
    @IssueId INT
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        h.Id, h.IssueNumber, h.IssueDate,
        h.RequestedBy, ru.FullName AS RequestedByName, h.RequestedOn,
        h.DepartmentId, dep.Name AS DepartmentName,
        h.SiteId, s.Name AS SiteName,
        h.EngineerId, e.Name AS EngineerName,
        h.WarehouseId, w.Name AS WarehouseName,
        h.ProjectName, h.WorkOrderNumber, h.Purpose,
        h.Status, dbo.fn_GetStatusName(h.Status) AS StatusName,
        h.ApprovedBy, au.FullName AS ApprovedByName, h.ApprovedOn, h.ApprovalRemarks,
        h.IssuedBy, iu.FullName AS IssuedByName, h.IssuedOn,
        h.CourierId, c.Name AS CourierName, h.TrackingNumber, h.DispatchDate,
        h.ReceiverName, h.ClosedOn, h.TotalValue, h.Remarks
    FROM dbo.InventoryOutwardHeader AS h
    INNER JOIN dbo.Warehouses  AS w   ON w.Id = h.WarehouseId
    LEFT  JOIN dbo.Departments AS dep ON dep.Id = h.DepartmentId
    LEFT  JOIN dbo.Sites       AS s   ON s.Id = h.SiteId
    LEFT  JOIN dbo.Engineers   AS e   ON e.Id = h.EngineerId
    LEFT  JOIN dbo.Couriers    AS c   ON c.Id = h.CourierId
    LEFT  JOIN dbo.Users       AS ru  ON ru.Id = h.RequestedBy
    LEFT  JOIN dbo.Users       AS au  ON au.Id = h.ApprovedBy
    LEFT  JOIN dbo.Users       AS iu  ON iu.Id = h.IssuedBy
    WHERE h.Id = @IssueId AND h.IsDeleted = 0;

    SELECT
        d.Id, d.LineNumber, d.ItemId, i.ItemCode, i.ItemName, u.Symbol AS UnitSymbol,
        d.PartNumber, d.QuantityRequested, d.QuantityApproved, d.QuantityIssued,
        d.QuantityReserved, d.QuantityReturned, d.BatchNumber, d.SerialNumber,
        d.UnitCost, d.TotalCost, d.StockBefore, d.StockAfter,
        ISNULL(cs.AvailableQuantity, 0) AS AvailableStock,
        d.Remarks
    FROM dbo.InventoryOutwardDetails AS d
    INNER JOIN dbo.Items AS i ON i.Id = d.ItemId
    INNER JOIN dbo.Units AS u ON u.Id = i.UnitId
    INNER JOIN dbo.InventoryOutwardHeader AS h ON h.Id = d.OutwardHeaderId
    LEFT  JOIN dbo.vw_CurrentStock AS cs ON cs.ItemId = d.ItemId AND cs.WarehouseId = h.WarehouseId
    WHERE d.OutwardHeaderId = @IssueId
    ORDER BY d.LineNumber;

    SELECT
        a.Id, a.DocumentType, a.DocumentId, a.FileName, a.ContentType, a.FileSizeBytes,
        a.Description, a.UploadedOn, u.FullName AS UploadedByName
    FROM dbo.Attachments AS a
    LEFT JOIN dbo.Users AS u ON u.Id = a.UploadedBy
    WHERE a.DocumentType = 2 AND a.DocumentId = @IssueId AND a.IsDeleted = 0
    ORDER BY a.UploadedOn DESC;

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
    WHERE a.DocumentType = 2 AND a.DocumentId = @IssueId
    ORDER BY a.ActionOn, a.Id;
END
GO

/* -----------------------------------------------------------------------------
   sp_GetPendingApprovals / sp_GetApprovalTrail
   -------------------------------------------------------------------------- */
CREATE OR ALTER PROCEDURE dbo.sp_GetPendingApprovals
    @CurrentUserId  INT = 0,
    @DocumentType   INT = NULL
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        p.DocumentId, p.DocumentType, p.DocumentTypeName, p.DocumentNumber, p.DocumentDate,
        p.PartyName, p.WarehouseName, p.LineCount, p.TotalValue, p.RequestedByName,
        ISNULL(p.SubmittedOn, SYSUTCDATETIME()) AS SubmittedOn,
        p.AgeInDays
    FROM dbo.vw_PendingApprovals AS p
    WHERE (@DocumentType IS NULL OR p.DocumentType = @DocumentType)
    ORDER BY p.AgeInDays DESC, p.DocumentDate;
END
GO

CREATE OR ALTER PROCEDURE dbo.sp_GetApprovalTrail
    @DocumentType   INT,
    @DocumentId     INT
AS
BEGIN
    SET NOCOUNT ON;

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
    WHERE a.DocumentType = @DocumentType AND a.DocumentId = @DocumentId
    ORDER BY a.ActionOn, a.Id;
END
GO

PRINT 'Material issue procedures created.';
GO
