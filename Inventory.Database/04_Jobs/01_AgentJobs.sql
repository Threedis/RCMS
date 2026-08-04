/* =============================================================================
   01_AgentJobs.sql  -  SQL Server Agent scheduled tasks

   Run against msdb on the SQL Server instance that hosts the database. Adjust
   @DatabaseName below if the database is not called InventoryManagement.

   Jobs created
     Inventory - Low Stock Alerts      nightly 07:00  raises low-stock notifications
     Inventory - Purge Audit Logs      monthly 01:00  trims audit history to 2 years
     Inventory - Index Maintenance     weekly  02:00  rebuilds fragmented indexes
   ========================================================================== */

USE msdb;
GO

SET NOCOUNT ON;
GO

DECLARE @DatabaseName SYSNAME = N'InventoryManagement';
DECLARE @JobId UNIQUEIDENTIFIER;

/* -----------------------------------------------------------------------------
   1. Low stock alerts - every morning at 07:00
   -------------------------------------------------------------------------- */
IF EXISTS (SELECT 1 FROM msdb.dbo.sysjobs WHERE name = N'Inventory - Low Stock Alerts')
BEGIN
    EXEC msdb.dbo.sp_delete_job @job_name = N'Inventory - Low Stock Alerts', @delete_unused_schedule = 1;
END

EXEC msdb.dbo.sp_add_job
     @job_name = N'Inventory - Low Stock Alerts',
     @enabled = 1,
     @description = N'Raises an in-app low stock notification for every item at or below its reorder level.',
     @category_name = N'[Uncategorized (Local)]',
     @job_id = @JobId OUTPUT;

EXEC msdb.dbo.sp_add_jobstep
     @job_id = @JobId,
     @step_name = N'Generate alerts',
     @subsystem = N'TSQL',
     @command = N'EXEC dbo.sp_GenerateLowStockAlerts;',
     @database_name = @DatabaseName,
     @retry_attempts = 2,
     @retry_interval = 5,
     @on_success_action = 1,
     @on_fail_action = 2;

EXEC msdb.dbo.sp_add_jobschedule
     @job_id = @JobId,
     @name = N'Daily 07:00',
     @freq_type = 4,                /* daily */
     @freq_interval = 1,
     @active_start_time = 070000;

EXEC msdb.dbo.sp_add_jobserver @job_id = @JobId, @server_name = N'(local)';
GO

/* -----------------------------------------------------------------------------
   2. Purge old audit logs - first day of every month at 01:00
   -------------------------------------------------------------------------- */
DECLARE @DatabaseName SYSNAME = N'InventoryManagement';
DECLARE @JobId UNIQUEIDENTIFIER;

IF EXISTS (SELECT 1 FROM msdb.dbo.sysjobs WHERE name = N'Inventory - Purge Audit Logs')
BEGIN
    EXEC msdb.dbo.sp_delete_job @job_name = N'Inventory - Purge Audit Logs', @delete_unused_schedule = 1;
END

EXEC msdb.dbo.sp_add_job
     @job_name = N'Inventory - Purge Audit Logs',
     @enabled = 1,
     @description = N'Deletes audit and login history older than the retention period, in batches.',
     @job_id = @JobId OUTPUT;

EXEC msdb.dbo.sp_add_jobstep
     @job_id = @JobId,
     @step_name = N'Purge',
     @subsystem = N'TSQL',
     @command = N'EXEC dbo.sp_PurgeOldAuditLogs @RetentionDays = 730;',
     @database_name = @DatabaseName,
     @on_success_action = 1,
     @on_fail_action = 2;

EXEC msdb.dbo.sp_add_jobschedule
     @job_id = @JobId,
     @name = N'Monthly 01:00',
     @freq_type = 16,               /* monthly */
     @freq_interval = 1,            /* on the 1st */
     @active_start_time = 010000;

EXEC msdb.dbo.sp_add_jobserver @job_id = @JobId, @server_name = N'(local)';
GO

/* -----------------------------------------------------------------------------
   3. Index maintenance - every Sunday at 02:00
   The stock ledger grows continuously and its covering index is on the hot path
   of every balance query, so keeping fragmentation down matters.
   -------------------------------------------------------------------------- */
DECLARE @DatabaseName SYSNAME = N'InventoryManagement';
DECLARE @JobId UNIQUEIDENTIFIER;

IF EXISTS (SELECT 1 FROM msdb.dbo.sysjobs WHERE name = N'Inventory - Index Maintenance')
BEGIN
    EXEC msdb.dbo.sp_delete_job @job_name = N'Inventory - Index Maintenance', @delete_unused_schedule = 1;
END

EXEC msdb.dbo.sp_add_job
     @job_name = N'Inventory - Index Maintenance',
     @enabled = 1,
     @description = N'Reorganises or rebuilds fragmented indexes and refreshes statistics.',
     @job_id = @JobId OUTPUT;

EXEC msdb.dbo.sp_add_jobstep
     @job_id = @JobId,
     @step_name = N'Rebuild indexes',
     @subsystem = N'TSQL',
     @command = N'EXEC dbo.sp_MaintainIndexes;',
     @database_name = @DatabaseName,
     @on_success_action = 1,
     @on_fail_action = 2;

EXEC msdb.dbo.sp_add_jobschedule
     @job_id = @JobId,
     @name = N'Weekly Sunday 02:00',
     @freq_type = 8,                /* weekly */
     @freq_interval = 1,            /* Sunday */
     @freq_recurrence_factor = 1,
     @active_start_time = 020000;

EXEC msdb.dbo.sp_add_jobserver @job_id = @JobId, @server_name = N'(local)';
GO

PRINT 'SQL Server Agent jobs created. Verify the database name and the Agent service account.';
GO
