/* =============================================================================
   02_Masters.sql  -  CRUD for the seven simple "code + name" masters

   One set of procedures serves Category, Unit, Department, Site, Warehouse,
   Manufacturer, Courier and Engineer. The @MasterType discriminator selects a
   branch, and every branch is a static, fully parameterised statement - there
   is no dynamic SQL anywhere, so the shared shape costs nothing in safety.
   ========================================================================== */

SET NOCOUNT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

/* -----------------------------------------------------------------------------
   sp_InsertMaster
   -------------------------------------------------------------------------- */
CREATE OR ALTER PROCEDURE dbo.sp_InsertMaster
    @MasterType             NVARCHAR(30),
    @Id                     INT             = 0,
    @Code                   NVARCHAR(30),
    @Name                   NVARCHAR(150),
    @Description            NVARCHAR(500)   = NULL,
    @DisplayOrder           INT             = 0,
    @IsActive               BIT             = 1,
    @ParentId               INT             = NULL,
    @SecondaryParentId      INT             = NULL,
    @Symbol                 NVARCHAR(10)    = NULL,
    @DecimalPlaces          INT             = NULL,
    @Address                NVARCHAR(400)   = NULL,
    @City                   NVARCHAR(100)   = NULL,
    @State                  NVARCHAR(100)   = NULL,
    @PinCode                NVARCHAR(20)    = NULL,
    @ContactPerson          NVARCHAR(150)   = NULL,
    @ContactNumber          NVARCHAR(20)    = NULL,
    @Email                  NVARCHAR(150)   = NULL,
    @Country                NVARCHAR(100)   = NULL,
    @Website                NVARCHAR(200)   = NULL,
    @TrackingUrlTemplate    NVARCHAR(300)   = NULL,
    @IsDefault              BIT             = 0,
    @EmployeeCode           NVARCHAR(30)    = NULL,
    @Designation            NVARCHAR(150)   = NULL,
    @MobileNumber           NVARCHAR(20)    = NULL,
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
        IF @Code IS NULL OR LTRIM(RTRIM(@Code)) = N''
        BEGIN
            SET @ReturnCode = -400; SET @Message = N'Code is required.'; RETURN;
        END

        IF @Name IS NULL OR LTRIM(RTRIM(@Name)) = N''
        BEGIN
            SET @ReturnCode = -400; SET @Message = N'Name is required.'; RETURN;
        END

        SET @Code = LTRIM(RTRIM(@Code));
        SET @Name = LTRIM(RTRIM(@Name));

        BEGIN TRANSACTION;

        IF @MasterType = N'Category'
        BEGIN
            IF EXISTS (SELECT 1 FROM dbo.Categories WHERE Code = @Code AND IsDeleted = 0)
            BEGIN
                ROLLBACK TRANSACTION;
                SET @ReturnCode = -409; SET @Message = N'A category with this code already exists.'; RETURN;
            END

            INSERT INTO dbo.Categories (Code, Name, Description, ParentCategoryId, DisplayOrder, IsActive, CreatedBy)
            VALUES (@Code, @Name, @Description, @ParentId, @DisplayOrder, @IsActive, @CurrentUserId);

            SET @NewId = CAST(SCOPE_IDENTITY() AS INT);
        END
        ELSE IF @MasterType = N'Unit'
        BEGIN
            IF EXISTS (SELECT 1 FROM dbo.Units WHERE Code = @Code AND IsDeleted = 0)
            BEGIN
                ROLLBACK TRANSACTION;
                SET @ReturnCode = -409; SET @Message = N'A unit with this code already exists.'; RETURN;
            END

            INSERT INTO dbo.Units (Code, Name, Description, Symbol, DecimalPlaces, DisplayOrder, IsActive, CreatedBy)
            VALUES (@Code, @Name, @Description, ISNULL(@Symbol, N''), ISNULL(@DecimalPlaces, 0),
                    @DisplayOrder, @IsActive, @CurrentUserId);

            SET @NewId = CAST(SCOPE_IDENTITY() AS INT);
        END
        ELSE IF @MasterType = N'Department'
        BEGIN
            IF EXISTS (SELECT 1 FROM dbo.Departments WHERE Code = @Code AND IsDeleted = 0)
            BEGIN
                ROLLBACK TRANSACTION;
                SET @ReturnCode = -409; SET @Message = N'A department with this code already exists.'; RETURN;
            END

            INSERT INTO dbo.Departments (Code, Name, Description, HeadOfDepartment, Email, DisplayOrder, IsActive, CreatedBy)
            VALUES (@Code, @Name, @Description, @ContactPerson, @Email, @DisplayOrder, @IsActive, @CurrentUserId);

            SET @NewId = CAST(SCOPE_IDENTITY() AS INT);
        END
        ELSE IF @MasterType = N'Site'
        BEGIN
            IF EXISTS (SELECT 1 FROM dbo.Sites WHERE Code = @Code AND IsDeleted = 0)
            BEGIN
                ROLLBACK TRANSACTION;
                SET @ReturnCode = -409; SET @Message = N'A site with this code already exists.'; RETURN;
            END

            INSERT INTO dbo.Sites (Code, Name, Description, Address, City, [State], PinCode,
                                   ContactPerson, ContactNumber, DisplayOrder, IsActive, CreatedBy)
            VALUES (@Code, @Name, @Description, @Address, @City, @State, @PinCode,
                    @ContactPerson, @ContactNumber, @DisplayOrder, @IsActive, @CurrentUserId);

            SET @NewId = CAST(SCOPE_IDENTITY() AS INT);
        END
        ELSE IF @MasterType = N'Warehouse'
        BEGIN
            IF EXISTS (SELECT 1 FROM dbo.Warehouses WHERE Code = @Code AND IsDeleted = 0)
            BEGIN
                ROLLBACK TRANSACTION;
                SET @ReturnCode = -409; SET @Message = N'A warehouse with this code already exists.'; RETURN;
            END

            IF NOT EXISTS (SELECT 1 FROM dbo.Sites WHERE Id = @ParentId AND IsDeleted = 0)
            BEGIN
                ROLLBACK TRANSACTION;
                SET @ReturnCode = -400; SET @Message = N'Select a valid site for the warehouse.'; RETURN;
            END

            /* Only one default warehouse per site. */
            IF @IsDefault = 1
            BEGIN
                UPDATE dbo.Warehouses SET IsDefault = 0 WHERE SiteId = @ParentId AND IsDefault = 1;
            END

            INSERT INTO dbo.Warehouses (Code, Name, Description, SiteId, InchargeName, ContactNumber,
                                        IsDefault, DisplayOrder, IsActive, CreatedBy)
            VALUES (@Code, @Name, @Description, @ParentId, @ContactPerson, @ContactNumber,
                    @IsDefault, @DisplayOrder, @IsActive, @CurrentUserId);

            SET @NewId = CAST(SCOPE_IDENTITY() AS INT);
        END
        ELSE IF @MasterType = N'Manufacturer'
        BEGIN
            IF EXISTS (SELECT 1 FROM dbo.Manufacturers WHERE Code = @Code AND IsDeleted = 0)
            BEGIN
                ROLLBACK TRANSACTION;
                SET @ReturnCode = -409; SET @Message = N'A manufacturer with this code already exists.'; RETURN;
            END

            INSERT INTO dbo.Manufacturers (Code, Name, Description, Country, Website, DisplayOrder, IsActive, CreatedBy)
            VALUES (@Code, @Name, @Description, @Country, @Website, @DisplayOrder, @IsActive, @CurrentUserId);

            SET @NewId = CAST(SCOPE_IDENTITY() AS INT);
        END
        ELSE IF @MasterType = N'Courier'
        BEGIN
            IF EXISTS (SELECT 1 FROM dbo.Couriers WHERE Code = @Code AND IsDeleted = 0)
            BEGIN
                ROLLBACK TRANSACTION;
                SET @ReturnCode = -409; SET @Message = N'A courier with this code already exists.'; RETURN;
            END

            INSERT INTO dbo.Couriers (Code, Name, Description, ContactPerson, ContactNumber,
                                      TrackingUrlTemplate, DisplayOrder, IsActive, CreatedBy)
            VALUES (@Code, @Name, @Description, @ContactPerson, @ContactNumber,
                    @TrackingUrlTemplate, @DisplayOrder, @IsActive, @CurrentUserId);

            SET @NewId = CAST(SCOPE_IDENTITY() AS INT);
        END
        ELSE IF @MasterType = N'Engineer'
        BEGIN
            IF EXISTS (SELECT 1 FROM dbo.Engineers WHERE Code = @Code AND IsDeleted = 0)
            BEGIN
                ROLLBACK TRANSACTION;
                SET @ReturnCode = -409; SET @Message = N'An engineer with this code already exists.'; RETURN;
            END

            INSERT INTO dbo.Engineers (Code, Name, Description, EmployeeCode, DepartmentId, SiteId,
                                       Email, MobileNumber, Designation, DisplayOrder, IsActive, CreatedBy)
            VALUES (@Code, @Name, @Description, @EmployeeCode, @ParentId, @SecondaryParentId,
                    @Email, @MobileNumber, @Designation, @DisplayOrder, @IsActive, @CurrentUserId);

            SET @NewId = CAST(SCOPE_IDENTITY() AS INT);
        END
        ELSE
        BEGIN
            ROLLBACK TRANSACTION;
            SET @ReturnCode = -400;
            SET @Message = CONCAT(N'Unknown master type ''', @MasterType, N'''.');
            RETURN;
        END

        INSERT INTO dbo.AuditLogs (Action, EntityName, EntityId, Description, UserId, Source)
        VALUES (4, @MasterType, CAST(@NewId AS NVARCHAR(50)),
                CONCAT(N'Created ', @MasterType, N' ', @Code, N' - ', @Name),
                @CurrentUserId, N'sp_InsertMaster');

        COMMIT TRANSACTION;

        SET @GeneratedNumber = @Code;
        SET @Message = CONCAT(@MasterType, N' created successfully.');
    END TRY
    BEGIN CATCH
        IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;

        SET @ReturnCode = -500;
        SET @Message = N'The record could not be saved. Please contact support if this continues.';

        INSERT INTO dbo.AuditLogs (Action, EntityName, Description, UserId, IsSuccessful, ErrorMessage, Source)
        VALUES (15, @MasterType, CONCAT(N'sp_InsertMaster failed for ', @Code),
                @CurrentUserId, 0, ERROR_MESSAGE(), N'sp_InsertMaster');
    END CATCH
END
GO

/* -----------------------------------------------------------------------------
   sp_UpdateMaster
   -------------------------------------------------------------------------- */
CREATE OR ALTER PROCEDURE dbo.sp_UpdateMaster
    @MasterType             NVARCHAR(30),
    @Id                     INT,
    @Code                   NVARCHAR(30),
    @Name                   NVARCHAR(150),
    @Description            NVARCHAR(500)   = NULL,
    @DisplayOrder           INT             = 0,
    @IsActive               BIT             = 1,
    @ParentId               INT             = NULL,
    @SecondaryParentId      INT             = NULL,
    @Symbol                 NVARCHAR(10)    = NULL,
    @DecimalPlaces          INT             = NULL,
    @Address                NVARCHAR(400)   = NULL,
    @City                   NVARCHAR(100)   = NULL,
    @State                  NVARCHAR(100)   = NULL,
    @PinCode                NVARCHAR(20)    = NULL,
    @ContactPerson          NVARCHAR(150)   = NULL,
    @ContactNumber          NVARCHAR(20)    = NULL,
    @Email                  NVARCHAR(150)   = NULL,
    @Country                NVARCHAR(100)   = NULL,
    @Website                NVARCHAR(200)   = NULL,
    @TrackingUrlTemplate    NVARCHAR(300)   = NULL,
    @IsDefault              BIT             = 0,
    @EmployeeCode           NVARCHAR(30)    = NULL,
    @Designation            NVARCHAR(150)   = NULL,
    @MobileNumber           NVARCHAR(20)    = NULL,
    @CurrentUserId          INT             = 0,
    @ReturnCode             INT             OUTPUT,
    @Message                NVARCHAR(500)   OUTPUT,
    @NewId                  INT             OUTPUT,
    @GeneratedNumber        NVARCHAR(30)    OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    SET @ReturnCode = 0; SET @Message = N''; SET @NewId = @Id; SET @GeneratedNumber = NULL;

    BEGIN TRY
        IF @Id IS NULL OR @Id <= 0
        BEGIN
            SET @ReturnCode = -400; SET @Message = N'A valid record id is required.'; RETURN;
        END

        SET @Code = LTRIM(RTRIM(@Code));
        SET @Name = LTRIM(RTRIM(@Name));

        BEGIN TRANSACTION;

        IF @MasterType = N'Category'
        BEGIN
            IF EXISTS (SELECT 1 FROM dbo.Categories WHERE Code = @Code AND Id <> @Id AND IsDeleted = 0)
            BEGIN
                ROLLBACK TRANSACTION;
                SET @ReturnCode = -409; SET @Message = N'Another category already uses this code.'; RETURN;
            END

            /* Guard against a category becoming its own ancestor. */
            IF @ParentId = @Id
            BEGIN
                ROLLBACK TRANSACTION;
                SET @ReturnCode = -400; SET @Message = N'A category cannot be its own parent.'; RETURN;
            END

            UPDATE dbo.Categories
               SET Code = @Code, Name = @Name, Description = @Description,
                   ParentCategoryId = @ParentId, DisplayOrder = @DisplayOrder, IsActive = @IsActive,
                   ModifiedOn = SYSUTCDATETIME(), ModifiedBy = @CurrentUserId
             WHERE Id = @Id AND IsDeleted = 0;
        END
        ELSE IF @MasterType = N'Unit'
        BEGIN
            IF EXISTS (SELECT 1 FROM dbo.Units WHERE Code = @Code AND Id <> @Id AND IsDeleted = 0)
            BEGIN
                ROLLBACK TRANSACTION;
                SET @ReturnCode = -409; SET @Message = N'Another unit already uses this code.'; RETURN;
            END

            UPDATE dbo.Units
               SET Code = @Code, Name = @Name, Description = @Description,
                   Symbol = ISNULL(@Symbol, N''), DecimalPlaces = ISNULL(@DecimalPlaces, 0),
                   DisplayOrder = @DisplayOrder, IsActive = @IsActive,
                   ModifiedOn = SYSUTCDATETIME(), ModifiedBy = @CurrentUserId
             WHERE Id = @Id AND IsDeleted = 0;
        END
        ELSE IF @MasterType = N'Department'
        BEGIN
            IF EXISTS (SELECT 1 FROM dbo.Departments WHERE Code = @Code AND Id <> @Id AND IsDeleted = 0)
            BEGIN
                ROLLBACK TRANSACTION;
                SET @ReturnCode = -409; SET @Message = N'Another department already uses this code.'; RETURN;
            END

            UPDATE dbo.Departments
               SET Code = @Code, Name = @Name, Description = @Description,
                   HeadOfDepartment = @ContactPerson, Email = @Email,
                   DisplayOrder = @DisplayOrder, IsActive = @IsActive,
                   ModifiedOn = SYSUTCDATETIME(), ModifiedBy = @CurrentUserId
             WHERE Id = @Id AND IsDeleted = 0;
        END
        ELSE IF @MasterType = N'Site'
        BEGIN
            IF EXISTS (SELECT 1 FROM dbo.Sites WHERE Code = @Code AND Id <> @Id AND IsDeleted = 0)
            BEGIN
                ROLLBACK TRANSACTION;
                SET @ReturnCode = -409; SET @Message = N'Another site already uses this code.'; RETURN;
            END

            UPDATE dbo.Sites
               SET Code = @Code, Name = @Name, Description = @Description,
                   Address = @Address, City = @City, [State] = @State, PinCode = @PinCode,
                   ContactPerson = @ContactPerson, ContactNumber = @ContactNumber,
                   DisplayOrder = @DisplayOrder, IsActive = @IsActive,
                   ModifiedOn = SYSUTCDATETIME(), ModifiedBy = @CurrentUserId
             WHERE Id = @Id AND IsDeleted = 0;
        END
        ELSE IF @MasterType = N'Warehouse'
        BEGIN
            IF EXISTS (SELECT 1 FROM dbo.Warehouses WHERE Code = @Code AND Id <> @Id AND IsDeleted = 0)
            BEGIN
                ROLLBACK TRANSACTION;
                SET @ReturnCode = -409; SET @Message = N'Another warehouse already uses this code.'; RETURN;
            END

            IF NOT EXISTS (SELECT 1 FROM dbo.Sites WHERE Id = @ParentId AND IsDeleted = 0)
            BEGIN
                ROLLBACK TRANSACTION;
                SET @ReturnCode = -400; SET @Message = N'Select a valid site for the warehouse.'; RETURN;
            END

            /* Moving a warehouse that already holds stock would orphan the
               ledger's site attribution, so refuse it. */
            IF EXISTS (SELECT 1 FROM dbo.Warehouses WHERE Id = @Id AND SiteId <> @ParentId)
               AND EXISTS (SELECT 1 FROM dbo.StockLedger WHERE WarehouseId = @Id)
            BEGIN
                ROLLBACK TRANSACTION;
                SET @ReturnCode = -409;
                SET @Message = N'This warehouse holds stock history and cannot be moved to another site.';
                RETURN;
            END

            IF @IsDefault = 1
            BEGIN
                UPDATE dbo.Warehouses SET IsDefault = 0 WHERE SiteId = @ParentId AND Id <> @Id AND IsDefault = 1;
            END

            UPDATE dbo.Warehouses
               SET Code = @Code, Name = @Name, Description = @Description, SiteId = @ParentId,
                   InchargeName = @ContactPerson, ContactNumber = @ContactNumber, IsDefault = @IsDefault,
                   DisplayOrder = @DisplayOrder, IsActive = @IsActive,
                   ModifiedOn = SYSUTCDATETIME(), ModifiedBy = @CurrentUserId
             WHERE Id = @Id AND IsDeleted = 0;
        END
        ELSE IF @MasterType = N'Manufacturer'
        BEGIN
            IF EXISTS (SELECT 1 FROM dbo.Manufacturers WHERE Code = @Code AND Id <> @Id AND IsDeleted = 0)
            BEGIN
                ROLLBACK TRANSACTION;
                SET @ReturnCode = -409; SET @Message = N'Another manufacturer already uses this code.'; RETURN;
            END

            UPDATE dbo.Manufacturers
               SET Code = @Code, Name = @Name, Description = @Description,
                   Country = @Country, Website = @Website,
                   DisplayOrder = @DisplayOrder, IsActive = @IsActive,
                   ModifiedOn = SYSUTCDATETIME(), ModifiedBy = @CurrentUserId
             WHERE Id = @Id AND IsDeleted = 0;
        END
        ELSE IF @MasterType = N'Courier'
        BEGIN
            IF EXISTS (SELECT 1 FROM dbo.Couriers WHERE Code = @Code AND Id <> @Id AND IsDeleted = 0)
            BEGIN
                ROLLBACK TRANSACTION;
                SET @ReturnCode = -409; SET @Message = N'Another courier already uses this code.'; RETURN;
            END

            UPDATE dbo.Couriers
               SET Code = @Code, Name = @Name, Description = @Description,
                   ContactPerson = @ContactPerson, ContactNumber = @ContactNumber,
                   TrackingUrlTemplate = @TrackingUrlTemplate,
                   DisplayOrder = @DisplayOrder, IsActive = @IsActive,
                   ModifiedOn = SYSUTCDATETIME(), ModifiedBy = @CurrentUserId
             WHERE Id = @Id AND IsDeleted = 0;
        END
        ELSE IF @MasterType = N'Engineer'
        BEGIN
            IF EXISTS (SELECT 1 FROM dbo.Engineers WHERE Code = @Code AND Id <> @Id AND IsDeleted = 0)
            BEGIN
                ROLLBACK TRANSACTION;
                SET @ReturnCode = -409; SET @Message = N'Another engineer already uses this code.'; RETURN;
            END

            UPDATE dbo.Engineers
               SET Code = @Code, Name = @Name, Description = @Description, EmployeeCode = @EmployeeCode,
                   DepartmentId = @ParentId, SiteId = @SecondaryParentId, Email = @Email,
                   MobileNumber = @MobileNumber, Designation = @Designation,
                   DisplayOrder = @DisplayOrder, IsActive = @IsActive,
                   ModifiedOn = SYSUTCDATETIME(), ModifiedBy = @CurrentUserId
             WHERE Id = @Id AND IsDeleted = 0;
        END
        ELSE
        BEGIN
            ROLLBACK TRANSACTION;
            SET @ReturnCode = -400;
            SET @Message = CONCAT(N'Unknown master type ''', @MasterType, N'''.');
            RETURN;
        END

        IF @@ROWCOUNT = 0
        BEGIN
            ROLLBACK TRANSACTION;
            SET @ReturnCode = -404; SET @Message = N'The record was not found or has been deleted.'; RETURN;
        END

        INSERT INTO dbo.AuditLogs (Action, EntityName, EntityId, Description, UserId, Source)
        VALUES (5, @MasterType, CAST(@Id AS NVARCHAR(50)),
                CONCAT(N'Updated ', @MasterType, N' ', @Code, N' - ', @Name),
                @CurrentUserId, N'sp_UpdateMaster');

        COMMIT TRANSACTION;

        SET @GeneratedNumber = @Code;
        SET @Message = CONCAT(@MasterType, N' updated successfully.');
    END TRY
    BEGIN CATCH
        IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;

        SET @ReturnCode = -500;
        SET @Message = N'The record could not be updated. Please contact support if this continues.';

        INSERT INTO dbo.AuditLogs (Action, EntityName, EntityId, Description, UserId, IsSuccessful, ErrorMessage, Source)
        VALUES (15, @MasterType, CAST(@Id AS NVARCHAR(50)), N'sp_UpdateMaster failed',
                @CurrentUserId, 0, ERROR_MESSAGE(), N'sp_UpdateMaster');
    END CATCH
END
GO

/* -----------------------------------------------------------------------------
   sp_DeleteMaster
   Soft delete. Each branch first checks for dependent rows: referential
   integrity is enforced here with a readable message rather than surfacing a
   foreign key violation to the user.
   -------------------------------------------------------------------------- */
CREATE OR ALTER PROCEDURE dbo.sp_DeleteMaster
    @MasterType         NVARCHAR(30),
    @Id                 INT,
    @CurrentUserId      INT             = 0,
    @ReturnCode         INT             OUTPUT,
    @Message            NVARCHAR(500)   OUTPUT,
    @NewId              INT             OUTPUT,
    @GeneratedNumber    NVARCHAR(30)    OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    SET @ReturnCode = 0; SET @Message = N''; SET @NewId = @Id; SET @GeneratedNumber = NULL;

    BEGIN TRY
        DECLARE @Dependents INT = 0;

        BEGIN TRANSACTION;

        IF @MasterType = N'Category'
        BEGIN
            SELECT @Dependents = COUNT(*) FROM dbo.Items WHERE CategoryId = @Id AND IsDeleted = 0;

            IF @Dependents > 0
            BEGIN
                ROLLBACK TRANSACTION;
                SET @ReturnCode = -409;
                SET @Message = CONCAT(N'This category is used by ', @Dependents, N' item(s) and cannot be deleted.');
                RETURN;
            END

            IF EXISTS (SELECT 1 FROM dbo.Categories WHERE ParentCategoryId = @Id AND IsDeleted = 0)
            BEGIN
                ROLLBACK TRANSACTION;
                SET @ReturnCode = -409;
                SET @Message = N'This category has sub-categories and cannot be deleted.';
                RETURN;
            END

            UPDATE dbo.Categories
               SET IsDeleted = 1, IsActive = 0, ModifiedOn = SYSUTCDATETIME(), ModifiedBy = @CurrentUserId
             WHERE Id = @Id AND IsDeleted = 0;
        END
        ELSE IF @MasterType = N'Unit'
        BEGIN
            SELECT @Dependents = COUNT(*) FROM dbo.Items WHERE UnitId = @Id AND IsDeleted = 0;

            IF @Dependents > 0
            BEGIN
                ROLLBACK TRANSACTION;
                SET @ReturnCode = -409;
                SET @Message = CONCAT(N'This unit is used by ', @Dependents, N' item(s) and cannot be deleted.');
                RETURN;
            END

            UPDATE dbo.Units
               SET IsDeleted = 1, IsActive = 0, ModifiedOn = SYSUTCDATETIME(), ModifiedBy = @CurrentUserId
             WHERE Id = @Id AND IsDeleted = 0;
        END
        ELSE IF @MasterType = N'Department'
        BEGIN
            IF EXISTS (SELECT 1 FROM dbo.InventoryOutwardHeader WHERE DepartmentId = @Id AND IsDeleted = 0)
               OR EXISTS (SELECT 1 FROM dbo.Engineers WHERE DepartmentId = @Id AND IsDeleted = 0)
            BEGIN
                ROLLBACK TRANSACTION;
                SET @ReturnCode = -409;
                SET @Message = N'This department is referenced by engineers or issues and cannot be deleted.';
                RETURN;
            END

            UPDATE dbo.Departments
               SET IsDeleted = 1, IsActive = 0, ModifiedOn = SYSUTCDATETIME(), ModifiedBy = @CurrentUserId
             WHERE Id = @Id AND IsDeleted = 0;
        END
        ELSE IF @MasterType = N'Site'
        BEGIN
            IF EXISTS (SELECT 1 FROM dbo.Warehouses WHERE SiteId = @Id AND IsDeleted = 0)
            BEGIN
                ROLLBACK TRANSACTION;
                SET @ReturnCode = -409;
                SET @Message = N'This site has warehouses and cannot be deleted.';
                RETURN;
            END

            UPDATE dbo.Sites
               SET IsDeleted = 1, IsActive = 0, ModifiedOn = SYSUTCDATETIME(), ModifiedBy = @CurrentUserId
             WHERE Id = @Id AND IsDeleted = 0;
        END
        ELSE IF @MasterType = N'Warehouse'
        BEGIN
            IF EXISTS (SELECT 1 FROM dbo.StockLedger WHERE WarehouseId = @Id)
            BEGIN
                ROLLBACK TRANSACTION;
                SET @ReturnCode = -409;
                SET @Message = N'This warehouse has stock history and cannot be deleted. Deactivate it instead.';
                RETURN;
            END

            UPDATE dbo.Warehouses
               SET IsDeleted = 1, IsActive = 0, ModifiedOn = SYSUTCDATETIME(), ModifiedBy = @CurrentUserId
             WHERE Id = @Id AND IsDeleted = 0;
        END
        ELSE IF @MasterType = N'Manufacturer'
        BEGIN
            SELECT @Dependents = COUNT(*) FROM dbo.Items WHERE ManufacturerId = @Id AND IsDeleted = 0;

            IF @Dependents > 0
            BEGIN
                ROLLBACK TRANSACTION;
                SET @ReturnCode = -409;
                SET @Message = CONCAT(N'This manufacturer is used by ', @Dependents, N' item(s) and cannot be deleted.');
                RETURN;
            END

            UPDATE dbo.Manufacturers
               SET IsDeleted = 1, IsActive = 0, ModifiedOn = SYSUTCDATETIME(), ModifiedBy = @CurrentUserId
             WHERE Id = @Id AND IsDeleted = 0;
        END
        ELSE IF @MasterType = N'Courier'
        BEGIN
            IF EXISTS (SELECT 1 FROM dbo.InventoryInwardHeader WHERE CourierId = @Id AND IsDeleted = 0)
               OR EXISTS (SELECT 1 FROM dbo.InventoryOutwardHeader WHERE CourierId = @Id AND IsDeleted = 0)
            BEGIN
                ROLLBACK TRANSACTION;
                SET @ReturnCode = -409;
                SET @Message = N'This courier is referenced by inventory documents and cannot be deleted.';
                RETURN;
            END

            UPDATE dbo.Couriers
               SET IsDeleted = 1, IsActive = 0, ModifiedOn = SYSUTCDATETIME(), ModifiedBy = @CurrentUserId
             WHERE Id = @Id AND IsDeleted = 0;
        END
        ELSE IF @MasterType = N'Engineer'
        BEGIN
            IF EXISTS (SELECT 1 FROM dbo.InventoryOutwardHeader WHERE EngineerId = @Id AND IsDeleted = 0)
            BEGIN
                ROLLBACK TRANSACTION;
                SET @ReturnCode = -409;
                SET @Message = N'This engineer has material issues and cannot be deleted. Deactivate instead.';
                RETURN;
            END

            UPDATE dbo.Engineers
               SET IsDeleted = 1, IsActive = 0, ModifiedOn = SYSUTCDATETIME(), ModifiedBy = @CurrentUserId
             WHERE Id = @Id AND IsDeleted = 0;
        END
        ELSE
        BEGIN
            ROLLBACK TRANSACTION;
            SET @ReturnCode = -400;
            SET @Message = CONCAT(N'Unknown master type ''', @MasterType, N'''.');
            RETURN;
        END

        IF @@ROWCOUNT = 0
        BEGIN
            ROLLBACK TRANSACTION;
            SET @ReturnCode = -404; SET @Message = N'The record was not found or is already deleted.'; RETURN;
        END

        INSERT INTO dbo.AuditLogs (Action, EntityName, EntityId, Description, UserId, Source)
        VALUES (6, @MasterType, CAST(@Id AS NVARCHAR(50)),
                CONCAT(N'Deleted ', @MasterType, N' id ', @Id), @CurrentUserId, N'sp_DeleteMaster');

        COMMIT TRANSACTION;

        SET @Message = CONCAT(@MasterType, N' deleted successfully.');
    END TRY
    BEGIN CATCH
        IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;

        SET @ReturnCode = -500;
        SET @Message = N'The record could not be deleted.';

        INSERT INTO dbo.AuditLogs (Action, EntityName, EntityId, Description, UserId, IsSuccessful, ErrorMessage, Source)
        VALUES (15, @MasterType, CAST(@Id AS NVARCHAR(50)), N'sp_DeleteMaster failed',
                @CurrentUserId, 0, ERROR_MESSAGE(), N'sp_DeleteMaster');
    END CATCH
END
GO

/* -----------------------------------------------------------------------------
   sp_GetMasters
   Active lookup rows for one master, ready for a drop-down.
   -------------------------------------------------------------------------- */
CREATE OR ALTER PROCEDURE dbo.sp_GetMasters
    @MasterType NVARCHAR(30),
    @ParentId   INT = NULL
AS
BEGIN
    SET NOCOUNT ON;

    IF @MasterType = N'Category'
        SELECT Id, Name AS Text, Code, ParentCategoryId AS ParentId, CAST(NULL AS NVARCHAR(150)) AS Extra
        FROM dbo.Categories
        WHERE IsActive = 1 AND IsDeleted = 0
          AND (@ParentId IS NULL OR ParentCategoryId = @ParentId)
        ORDER BY DisplayOrder, Name;

    ELSE IF @MasterType = N'Unit'
        SELECT Id, Name AS Text, Code, CAST(NULL AS INT) AS ParentId, Symbol AS Extra
        FROM dbo.Units
        WHERE IsActive = 1 AND IsDeleted = 0
        ORDER BY DisplayOrder, Name;

    ELSE IF @MasterType = N'Department'
        SELECT Id, Name AS Text, Code, CAST(NULL AS INT) AS ParentId, HeadOfDepartment AS Extra
        FROM dbo.Departments
        WHERE IsActive = 1 AND IsDeleted = 0
        ORDER BY DisplayOrder, Name;

    ELSE IF @MasterType = N'Site'
        SELECT Id, Name AS Text, Code, CAST(NULL AS INT) AS ParentId, City AS Extra
        FROM dbo.Sites
        WHERE IsActive = 1 AND IsDeleted = 0
        ORDER BY DisplayOrder, Name;

    ELSE IF @MasterType = N'Warehouse'
        SELECT w.Id, w.Name AS Text, w.Code, w.SiteId AS ParentId, s.Name AS Extra
        FROM dbo.Warehouses AS w
        INNER JOIN dbo.Sites AS s ON s.Id = w.SiteId
        WHERE w.IsActive = 1 AND w.IsDeleted = 0
          AND (@ParentId IS NULL OR w.SiteId = @ParentId)
        ORDER BY w.DisplayOrder, w.Name;

    ELSE IF @MasterType = N'Manufacturer'
        SELECT Id, Name AS Text, Code, CAST(NULL AS INT) AS ParentId, Country AS Extra
        FROM dbo.Manufacturers
        WHERE IsActive = 1 AND IsDeleted = 0
        ORDER BY DisplayOrder, Name;

    ELSE IF @MasterType = N'Courier'
        SELECT Id, Name AS Text, Code, CAST(NULL AS INT) AS ParentId, ContactNumber AS Extra
        FROM dbo.Couriers
        WHERE IsActive = 1 AND IsDeleted = 0
        ORDER BY DisplayOrder, Name;

    ELSE IF @MasterType = N'Engineer'
        SELECT Id, Name AS Text, ISNULL(EmployeeCode, Code) AS Code, DepartmentId AS ParentId, Designation AS Extra
        FROM dbo.Engineers
        WHERE IsActive = 1 AND IsDeleted = 0
          AND (@ParentId IS NULL OR DepartmentId = @ParentId)
        ORDER BY Name;

    ELSE IF @MasterType = N'Vendor'
        SELECT Id, Name AS Text, Code, CAST(NULL AS INT) AS ParentId, City AS Extra
        FROM dbo.Vendors
        WHERE IsActive = 1 AND IsDeleted = 0 AND IsBlacklisted = 0
        ORDER BY Name;

    ELSE
        SELECT CAST(0 AS INT) AS Id, CAST(N'' AS NVARCHAR(150)) AS Text,
               CAST(N'' AS NVARCHAR(30)) AS Code, CAST(NULL AS INT) AS ParentId,
               CAST(NULL AS NVARCHAR(150)) AS Extra
        WHERE 1 = 0;
END
GO

PRINT 'Master data procedures created.';
GO
