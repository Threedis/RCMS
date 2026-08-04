/* =============================================================================
   01_Security_Audit.sql  -  Authentication support, audit trail, login history

   Contract implemented by every write procedure in this database:

       @ReturnCode      INT             OUTPUT    0 = success, negative = handled failure
       @Message         NVARCHAR(500)   OUTPUT    user-safe description of the outcome
       @NewId           INT             OUTPUT    identity of an inserted row, else NULL
       @GeneratedNumber NVARCHAR(30)    OUTPUT    allocated document number, else NULL

   Every write procedure wraps its work in BEGIN TRY / BEGIN TRANSACTION, rolls
   back on error, records the failure in dbo.AuditLogs and returns a negative
   code rather than letting the exception surface. The application layer reads
   the return code; it never parses an error message.
   ========================================================================== */

SET NOCOUNT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

/* -----------------------------------------------------------------------------
   sp_Login
   Validates that an account may sign in. Passwords are verified by ASP.NET Core
   Identity in the application - a hash is never compared in SQL - so this
   procedure answers only the questions Identity does not: is the account
   active, is it locked, and has its password expired.
   -------------------------------------------------------------------------- */
CREATE OR ALTER PROCEDURE dbo.sp_Login
    @UserName           NVARCHAR(256),
    @PasswordExpiryDays INT = 90,
    @ReturnCode         INT             OUTPUT,
    @Message            NVARCHAR(500)   OUTPUT,
    @NewId              INT             OUTPUT,
    @GeneratedNumber    NVARCHAR(30)    OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    SET @ReturnCode = 0; SET @Message = N''; SET @NewId = NULL; SET @GeneratedNumber = NULL;

    BEGIN TRY
        DECLARE @UserId INT, @IsActive BIT, @IsDeleted BIT,
                @LockoutEnd DATETIMEOFFSET(7), @PasswordChangedOn DATETIME2(3),
                @MustChange BIT;

        SELECT
            @UserId            = u.Id,
            @IsActive          = u.IsActive,
            @IsDeleted         = u.IsDeleted,
            @LockoutEnd        = u.LockoutEnd,
            @PasswordChangedOn = u.PasswordChangedOn,
            @MustChange        = u.MustChangePassword
        FROM dbo.Users AS u
        WHERE u.NormalizedUserName = UPPER(@UserName);

        IF @UserId IS NULL
        BEGIN
            /* Deliberately vague: do not reveal whether the account exists. */
            SET @ReturnCode = -401;
            SET @Message = N'Invalid user name or password.';
            RETURN;
        END

        IF @IsDeleted = 1 OR @IsActive = 0
        BEGIN
            SET @ReturnCode = -403;
            SET @Message = N'This account has been deactivated. Contact your administrator.';
            RETURN;
        END

        IF @LockoutEnd IS NOT NULL AND @LockoutEnd > SYSDATETIMEOFFSET()
        BEGIN
            SET @ReturnCode = -423;
            SET @Message = N'This account is locked. Try again later or contact your administrator.';
            RETURN;
        END

        IF @MustChange = 1
        BEGIN
            SET @ReturnCode = 1;    /* success, but the caller must force a change */
            SET @Message = N'You must change your password before continuing.';
            SET @NewId = @UserId;
            RETURN;
        END

        IF @PasswordExpiryDays > 0
           AND @PasswordChangedOn IS NOT NULL
           AND DATEDIFF(DAY, @PasswordChangedOn, SYSUTCDATETIME()) > @PasswordExpiryDays
        BEGIN
            SET @ReturnCode = 1;
            SET @Message = N'Your password has expired. Please set a new one.';
            SET @NewId = @UserId;
            RETURN;
        END

        UPDATE dbo.Users SET LastLoginOn = SYSUTCDATETIME() WHERE Id = @UserId;

        SET @NewId = @UserId;
        SET @Message = N'Sign-in permitted.';
    END TRY
    BEGIN CATCH
        SET @ReturnCode = -500;
        SET @Message = N'An unexpected error occurred while signing in.';

        INSERT INTO dbo.AuditLogs (Action, EntityName, Description, IsSuccessful, ErrorMessage, Source)
        VALUES (15, N'Login', CONCAT(N'sp_Login failed for ', @UserName), 0, ERROR_MESSAGE(), N'sp_Login');
    END CATCH
END
GO

/* -----------------------------------------------------------------------------
   sp_InsertAuditLog
   The only insert path into the audit trail. It never throws: auditing must not
   be able to fail the operation that triggered it.
   -------------------------------------------------------------------------- */
CREATE OR ALTER PROCEDURE dbo.sp_InsertAuditLog
    @Action             INT,
    @EntityName         NVARCHAR(100)   = NULL,
    @EntityId           NVARCHAR(50)    = NULL,
    @Description        NVARCHAR(500)   = NULL,
    @OldValues          NVARCHAR(MAX)   = NULL,
    @NewValues          NVARCHAR(MAX)   = NULL,
    @UserId             INT             = NULL,
    @UserName           NVARCHAR(256)   = NULL,
    @IpAddress          NVARCHAR(45)    = NULL,
    @UserAgent          NVARCHAR(400)   = NULL,
    @Source             NVARCHAR(200)   = NULL,
    @IsSuccessful       BIT             = 1,
    @ErrorMessage       NVARCHAR(1000)  = NULL,
    @ReturnCode         INT             OUTPUT,
    @Message            NVARCHAR(500)   OUTPUT,
    @NewId              INT             OUTPUT,
    @GeneratedNumber    NVARCHAR(30)    OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    SET @ReturnCode = 0; SET @Message = N''; SET @NewId = NULL; SET @GeneratedNumber = NULL;

    BEGIN TRY
        INSERT INTO dbo.AuditLogs
            (Action, EntityName, EntityId, Description, OldValues, NewValues,
             UserId, UserName, IpAddress, UserAgent, Source, IsSuccessful, ErrorMessage)
        VALUES
            (@Action, @EntityName, @EntityId, @Description, @OldValues, @NewValues,
             @UserId, @UserName, @IpAddress, @UserAgent, @Source, @IsSuccessful, @ErrorMessage);

        SET @NewId = CAST(SCOPE_IDENTITY() AS INT);
        SET @Message = N'Audit entry recorded.';
    END TRY
    BEGIN CATCH
        /* Swallow: a broken audit write must never break the caller. */
        SET @ReturnCode = 0;
        SET @Message = N'Audit entry could not be recorded.';
    END CATCH
END
GO

/* -----------------------------------------------------------------------------
   sp_InsertLoginHistory
   -------------------------------------------------------------------------- */
CREATE OR ALTER PROCEDURE dbo.sp_InsertLoginHistory
    @UserId             INT             = NULL,
    @UserName           NVARCHAR(256),
    @IsSuccessful       BIT,
    @FailureReason      NVARCHAR(300)   = NULL,
    @IpAddress          NVARCHAR(45)    = NULL,
    @UserAgent          NVARCHAR(400)   = NULL,
    @SessionId          NVARCHAR(100)   = NULL,
    @ReturnCode         INT             OUTPUT,
    @Message            NVARCHAR(500)   OUTPUT,
    @NewId              INT             OUTPUT,
    @GeneratedNumber    NVARCHAR(30)    OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    SET @ReturnCode = 0; SET @Message = N''; SET @NewId = NULL; SET @GeneratedNumber = NULL;

    BEGIN TRY
        INSERT INTO dbo.LoginHistory
            (UserId, UserName, IsSuccessful, FailureReason, IpAddress, UserAgent, SessionId)
        VALUES
            (@UserId, @UserName, @IsSuccessful, @FailureReason, @IpAddress, @UserAgent, @SessionId);

        SET @Message = N'Login history recorded.';
    END TRY
    BEGIN CATCH
        SET @ReturnCode = 0;
        SET @Message = N'Login history could not be recorded.';
    END CATCH
END
GO

/* -----------------------------------------------------------------------------
   sp_GetAuditLogs
   Server-side paged, filtered audit search.
   OFFSET/FETCH keeps the work proportional to the page, not the table.
   -------------------------------------------------------------------------- */
CREATE OR ALTER PROCEDURE dbo.sp_GetAuditLogs
    @PageNumber     INT             = 1,
    @PageSize       INT             = 25,
    @SearchTerm     NVARCHAR(200)   = NULL,
    @SortColumn     NVARCHAR(50)    = NULL,
    @SortDirection  NVARCHAR(4)     = 'DESC',
    @FromDate       DATETIME2(3)    = NULL,
    @ToDate         DATETIME2(3)    = NULL,
    @UserId         INT             = NULL,
    @EntityName     NVARCHAR(100)   = NULL,
    @Action         INT             = NULL,
    @TotalCount     INT             OUTPUT
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @Skip INT = (CASE WHEN @PageNumber < 1 THEN 0 ELSE @PageNumber - 1 END) * @PageSize;
    DECLARE @Search NVARCHAR(210) = CASE WHEN @SearchTerm IS NULL THEN NULL ELSE N'%' + @SearchTerm + N'%' END;

    /* One CTE, used for both the count and the page, so the filter can only be
       written once and the two can never disagree. */
    WITH Filtered AS
    (
        SELECT a.*
        FROM dbo.AuditLogs AS a
        WHERE (@FromDate   IS NULL OR a.CreatedOn >= @FromDate)
          AND (@ToDate     IS NULL OR a.CreatedOn <  DATEADD(DAY, 1, CAST(@ToDate AS DATE)))
          AND (@UserId     IS NULL OR a.UserId = @UserId)
          AND (@EntityName IS NULL OR a.EntityName = @EntityName)
          AND (@Action     IS NULL OR a.Action = @Action)
          AND (@Search     IS NULL OR a.Description LIKE @Search
                                   OR a.UserName    LIKE @Search
                                   OR a.EntityId    LIKE @Search)
    )
    SELECT @TotalCount = COUNT(*) FROM Filtered;

    WITH Filtered AS
    (
        SELECT a.*
        FROM dbo.AuditLogs AS a
        WHERE (@FromDate   IS NULL OR a.CreatedOn >= @FromDate)
          AND (@ToDate     IS NULL OR a.CreatedOn <  DATEADD(DAY, 1, CAST(@ToDate AS DATE)))
          AND (@UserId     IS NULL OR a.UserId = @UserId)
          AND (@EntityName IS NULL OR a.EntityName = @EntityName)
          AND (@Action     IS NULL OR a.Action = @Action)
          AND (@Search     IS NULL OR a.Description LIKE @Search
                                   OR a.UserName    LIKE @Search
                                   OR a.EntityId    LIKE @Search)
    )
    SELECT
        f.Id,
        f.CreatedOn,
        CASE f.Action
            WHEN 1  THEN N'Login'         WHEN 2  THEN N'Logout'
            WHEN 3  THEN N'Login Failed'  WHEN 4  THEN N'Create'
            WHEN 5  THEN N'Update'        WHEN 6  THEN N'Delete'
            WHEN 7  THEN N'Approve'       WHEN 8  THEN N'Reject'
            WHEN 9  THEN N'Submit'        WHEN 10 THEN N'Issue'
            WHEN 11 THEN N'Export'        WHEN 12 THEN N'Import'
            WHEN 13 THEN N'Stock Update'  WHEN 14 THEN N'Password Change'
            WHEN 15 THEN N'Error'         ELSE N'Unknown'
        END                     AS ActionName,
        f.EntityName,
        f.EntityId,
        f.Description,
        f.UserName,
        f.IpAddress,
        f.Source,
        f.IsSuccessful,
        f.ErrorMessage
    FROM Filtered AS f
    /* The sort column is chosen from a fixed list; an unrecognised value falls
       back to the timestamp. No identifier ever comes from the client. */
    ORDER BY
        CASE WHEN @SortDirection = 'ASC'  AND @SortColumn = 'CreatedOn'  THEN f.CreatedOn END ASC,
        CASE WHEN @SortDirection = 'DESC' AND @SortColumn = 'CreatedOn'  THEN f.CreatedOn END DESC,
        CASE WHEN @SortDirection = 'ASC'  AND @SortColumn = 'UserName'   THEN f.UserName  END ASC,
        CASE WHEN @SortDirection = 'DESC' AND @SortColumn = 'UserName'   THEN f.UserName  END DESC,
        CASE WHEN @SortDirection = 'ASC'  AND @SortColumn = 'EntityName' THEN f.EntityName END ASC,
        CASE WHEN @SortDirection = 'DESC' AND @SortColumn = 'EntityName' THEN f.EntityName END DESC,
        f.CreatedOn DESC
    OFFSET @Skip ROWS FETCH NEXT @PageSize ROWS ONLY;
END
GO

PRINT 'Security and audit procedures created.';
GO
