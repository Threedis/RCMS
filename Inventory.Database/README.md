# Inventory.Database

SQL Server 2022 schema, programmability and reference data for the Enterprise
Inventory Management System.

## Layout

| Folder | Contents | Run order |
|---|---|---|
| `01_Schema` | Tables, table types, functions, views, triggers | 1 |
| `02_Programmability` | Stored procedures | 2 |
| `03_Data` | Reference data (roles, units, categories, sites, sequences) | 3 |
| `04_Jobs` | SQL Server Agent jobs (run against `msdb`) | 4, optional |

Every script is **idempotent** — objects are created with `IF NOT EXISTS` or
`CREATE OR ALTER`, and seed data is merged — so the same scripts create a fresh
database and upgrade an existing one.

## Deploying

```powershell
# Developer machine, LocalDB or SQL Express
.\Deploy.ps1 -ServerInstance ".\SQLEXPRESS" -Database "InventoryManagement"

# Server, SQL authentication, including the scheduled jobs
.\Deploy.ps1 -ServerInstance "sql01,1433" -Database "InventoryManagement" `
             -Username "deployer" -Password "..." -IncludeAgentJobs
```

Or run the folders manually in order with `sqlcmd -i <file>`.

## The stored-procedure contract

Every **write** procedure declares exactly these four output parameters:

```sql
@ReturnCode      INT             OUTPUT   -- 0 = success, negative = handled failure
@Message         NVARCHAR(500)   OUTPUT   -- user-safe description
@NewId           INT             OUTPUT   -- identity of an inserted row, else NULL
@GeneratedNumber NVARCHAR(30)    OUTPUT   -- allocated document number, else NULL
```

`StoredProcedureExecutor.ExecuteAsync` adds all four automatically, so a
procedure that omits one will fail at run time with *"procedure has no parameter
named …"*. Return codes follow HTTP conventions:

| Code | Meaning |
|---|---|
| `0` | Success |
| `-400` | Validation failure |
| `-401` | Not authenticated |
| `-403` | Not permitted |
| `-404` | Not found |
| `-409` | Business rule conflict (duplicate, insufficient stock, wrong status) |
| `-423` | Account locked |
| `-500` | Unhandled error; already logged and rolled back |

Every **paged read** procedure declares `@TotalCount INT OUTPUT` and returns one
page as its result set.

## Design notes

**The stock ledger is the single source of truth.** No table stores a quantity
on hand. A balance is the `BalanceQuantity` of the most recent `StockLedger` row
for an item and warehouse, exposed through `vw_CurrentStock`. The ledger is
append-only, enforced by `trg_StockLedger_PreventChange`: corrections are posted
as reversal rows, never as updates, so history can never be rewritten.

**Stock cannot go negative.** The rule is enforced three times over: in the
business layer for a readable message, in `sp_ApproveInventoryIssue` under an
`UPDLOCK` on the item row so concurrent approvals cannot both pass the check,
and by `CK_Ledger_NonNegativeBalance` as a structural backstop.

**Reservations make availability meaningful.** Submitting an issue for approval
sets `QuantityReserved`; `fn_GetReservedQuantity` subtracts it from the issuable
balance, so two pending requests can never be approved against the same units.

**Document numbers are gap-free.** `sp_GetNextDocumentNumber` increments the
counter under `UPDLOCK, HOLDLOCK` inside the caller's transaction, so concurrent
inserts queue instead of colliding.

**Weighted average costing.** Approving a receipt recomputes
`(old quantity x old average + received quantity x receipt rate) / new quantity`
under a row lock on the item, and stamps the result onto both the item and the
ledger row, so a stock valuation at any past date can be reproduced.

**No dynamic SQL.** Every statement is static and parameterised. The one
exception is `sp_MaintainIndexes`, which builds `ALTER INDEX` statements from
`sys.indexes` with `QUOTENAME` — no user input is involved.

## Verifying an installation

```sql
-- Reconciles every running balance against the sum of its movements.
-- A healthy database returns no rows.
EXEC dbo.sp_VerifyStockIntegrity;
```
