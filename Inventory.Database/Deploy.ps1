<#
.SYNOPSIS
    Creates or upgrades the Enterprise Inventory Management System database.

.DESCRIPTION
    Runs every script under Inventory.Database in dependency order:

        01_Schema           tables, table types, functions, views, triggers
        02_Programmability  stored procedures
        03_Data             reference data
        04_Jobs             SQL Server Agent jobs (optional)

    All scripts are idempotent, so the same command creates a fresh database or
    upgrades an existing one.

.PARAMETER ServerInstance
    SQL Server instance, e.g. "localhost", ".\SQLEXPRESS" or "sql01.corp,1433".

.PARAMETER Database
    Target database name. Created if it does not exist.

.PARAMETER UseIntegratedSecurity
    Connect with the current Windows account (the default).

.PARAMETER Username / .PARAMETER Password
    SQL authentication credentials, used when -UseIntegratedSecurity is omitted.

.PARAMETER IncludeAgentJobs
    Also create the SQL Server Agent jobs. Skip on a developer machine.

.EXAMPLE
    .\Deploy.ps1 -ServerInstance ".\SQLEXPRESS" -Database "InventoryManagement"

.EXAMPLE
    .\Deploy.ps1 -ServerInstance "sql01,1433" -Database "InventoryManagement" `
                 -Username "deployer" -Password "..." -IncludeAgentJobs

.NOTES
    Requires sqlcmd (bundled with SQL Server / the SqlServer PowerShell module,
    or installable with "winget install Microsoft.Sqlcmd").
#>

[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string] $ServerInstance,

    [Parameter(Mandatory = $false)]
    [string] $Database = 'InventoryManagement',

    [Parameter(Mandatory = $false)]
    [switch] $UseIntegratedSecurity = $true,

    [Parameter(Mandatory = $false)]
    [string] $Username,

    [Parameter(Mandatory = $false)]
    [string] $Password,

    [Parameter(Mandatory = $false)]
    [switch] $IncludeAgentJobs
)

$ErrorActionPreference = 'Stop'
$scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path

function Get-SqlCmdArguments {
    param([string] $DatabaseName)

    $arguments = @('-S', $ServerInstance, '-b', '-V', '16')

    if ($DatabaseName) {
        $arguments += @('-d', $DatabaseName)
    }

    if ($Username) {
        $arguments += @('-U', $Username, '-P', $Password)
    }
    else {
        $arguments += '-E'
    }

    return $arguments
}

function Invoke-SqlFile {
    param(
        [string] $Path,
        [string] $DatabaseName
    )

    $relative = $Path.Substring($scriptRoot.Length).TrimStart('\', '/')
    Write-Host ("  -> {0}" -f $relative) -ForegroundColor DarkGray

    $arguments = Get-SqlCmdArguments -DatabaseName $DatabaseName
    $arguments += @('-i', $Path)

    & sqlcmd @arguments

    if ($LASTEXITCODE -ne 0) {
        throw "Script failed: $relative (sqlcmd exit code $LASTEXITCODE)"
    }
}

function Invoke-SqlFolder {
    param(
        [string] $Folder,
        [string] $DatabaseName
    )

    $path = Join-Path $scriptRoot $Folder

    if (-not (Test-Path $path)) {
        Write-Host ("Skipping {0}: folder not found." -f $Folder) -ForegroundColor Yellow
        return
    }

    Write-Host ("{0}" -f $Folder) -ForegroundColor Cyan

    Get-ChildItem -Path $path -Filter '*.sql' | Sort-Object Name | ForEach-Object {
        Invoke-SqlFile -Path $_.FullName -DatabaseName $DatabaseName
    }
}

# ---------------------------------------------------------------------------

if (-not (Get-Command sqlcmd -ErrorAction SilentlyContinue)) {
    throw 'sqlcmd was not found on PATH. Install the SQL Server command line tools and try again.'
}

Write-Host ''
Write-Host '===============================================================' -ForegroundColor Cyan
Write-Host ' Enterprise Inventory Management System - database deployment' -ForegroundColor Cyan
Write-Host '===============================================================' -ForegroundColor Cyan
Write-Host (" Server   : {0}" -f $ServerInstance)
Write-Host (" Database : {0}" -f $Database)
Write-Host (" Auth     : {0}" -f $(if ($Username) { "SQL login '$Username'" } else { 'Windows integrated' }))
Write-Host ''

# 1. Create the database if it does not exist (connect to master for this).
Write-Host 'Ensuring the database exists' -ForegroundColor Cyan

$createDatabase = @"
IF DB_ID(N'$Database') IS NULL
BEGIN
    PRINT 'Creating database $Database';
    EXEC('CREATE DATABASE [$Database]');
END
ELSE
BEGIN
    PRINT 'Database $Database already exists.';
END
"@

$masterArguments = Get-SqlCmdArguments -DatabaseName 'master'
$masterArguments += @('-Q', $createDatabase)
& sqlcmd @masterArguments

if ($LASTEXITCODE -ne 0) {
    throw "Could not create or reach the database (sqlcmd exit code $LASTEXITCODE)."
}

# 2. Schema, programmability and data, in order.
Invoke-SqlFolder -Folder '01_Schema'          -DatabaseName $Database
Invoke-SqlFolder -Folder '02_Programmability' -DatabaseName $Database
Invoke-SqlFolder -Folder '03_Data'            -DatabaseName $Database

# 3. Agent jobs are opt-in: they need a running SQL Server Agent.
if ($IncludeAgentJobs) {
    Invoke-SqlFolder -Folder '04_Jobs' -DatabaseName 'msdb'
}
else {
    Write-Host '04_Jobs skipped (pass -IncludeAgentJobs to create the scheduled jobs).' -ForegroundColor Yellow
}

Write-Host ''
Write-Host 'Deployment completed successfully.' -ForegroundColor Green
Write-Host ''
Write-Host 'Next steps:' -ForegroundColor Cyan
Write-Host '  1. Set the ConnectionStrings:DefaultConnection value in Inventory.Web.'
Write-Host '  2. Set Seed:AdminUserName / Seed:AdminEmail / Seed:AdminPassword in user secrets.'
Write-Host '  3. Start Inventory.Web; roles and the bootstrap administrator are created on first run.'
Write-Host ''
