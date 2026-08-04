/* =============================================================================
   02_Types.sql  -  User-defined table types

   The document procedures accept their line items as table-valued parameters.
   That gives one round trip per document and lets the whole posting - header,
   lines, ledger, average cost, approval trail - run inside a single SQL
   transaction. The column order here must match the DataTable built by
   InventoryRepository.
   ========================================================================== */

SET NOCOUNT ON;
GO

/* Goods Receipt Note lines. */
IF TYPE_ID(N'dbo.GrnDetailType') IS NULL
BEGIN
    CREATE TYPE dbo.GrnDetailType AS TABLE
    (
        LineNumber          INT             NOT NULL,
        ItemId              INT             NOT NULL,
        PartNumber          NVARCHAR(100)   NULL,
        QuantityReceived    DECIMAL(18,4)   NOT NULL,
        QuantityAccepted    DECIMAL(18,4)   NOT NULL,
        QuantityRejected    DECIMAL(18,4)   NOT NULL,
        RejectionReason     NVARCHAR(500)   NULL,
        BatchNumber         NVARCHAR(50)    NULL,
        SerialNumber        NVARCHAR(100)   NULL,
        ManufacturingDate   DATE            NULL,
        ExpiryDate          DATE            NULL,
        UnitCost            DECIMAL(18,4)   NOT NULL,
        DiscountPercent     DECIMAL(5,2)    NOT NULL,
        TaxRate             DECIMAL(5,2)    NOT NULL,
        ShelfLocation       NVARCHAR(50)    NULL,
        Remarks             NVARCHAR(500)   NULL,
        PRIMARY KEY CLUSTERED (LineNumber)
    );
END
GO

/* Material Issue lines. */
IF TYPE_ID(N'dbo.IssueDetailType') IS NULL
BEGIN
    CREATE TYPE dbo.IssueDetailType AS TABLE
    (
        LineNumber          INT             NOT NULL,
        ItemId              INT             NOT NULL,
        PartNumber          NVARCHAR(100)   NULL,
        QuantityRequested   DECIMAL(18,4)   NOT NULL,
        BatchNumber         NVARCHAR(50)    NULL,
        SerialNumber        NVARCHAR(100)   NULL,
        Remarks             NVARCHAR(500)   NULL,
        PRIMARY KEY CLUSTERED (LineNumber)
    );
END
GO

/* Per-line quantities supplied at approval and at dispatch. */
IF TYPE_ID(N'dbo.LineQuantityType') IS NULL
BEGIN
    CREATE TYPE dbo.LineQuantityType AS TABLE
    (
        DetailId    INT             NOT NULL,
        Quantity    DECIMAL(18,4)   NOT NULL,
        PRIMARY KEY CLUSTERED (DetailId)
    );
END
GO

PRINT 'Table types created.';
GO
