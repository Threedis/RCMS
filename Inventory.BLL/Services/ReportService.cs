using Inventory.BLL.Interfaces;
using Inventory.Common.Configuration;
using Inventory.Common.Constants;
using Inventory.Common.Enums;
using Inventory.Common.Extensions;
using Inventory.Entities.Dtos;
using Inventory.Repository.UnitOfWork;
using Inventory.Services.Export;
using Microsoft.Extensions.Options;

namespace Inventory.BLL.Services;

/// <summary>
/// Reporting service.
/// <para>
/// Every report is described once — its key, title, group and column layout —
/// and that single description drives the menu, the on-screen grid and all three
/// export formats. Adding a report therefore means writing one stored procedure
/// and one entry here; no controller, view or export code changes.
/// </para>
/// </summary>
public sealed class ReportService : IReportService
{
    // ---- Report keys ------------------------------------------------------
    public const string CurrentStock = "current-stock";
    public const string ItemWiseStock = "item-wise-stock";
    public const string SiteWiseStock = "site-wise-stock";
    public const string WarehouseStock = "warehouse-stock";
    public const string StockLedger = "stock-ledger";
    public const string LowStock = "low-stock";
    public const string VendorPurchase = "vendor-purchase";
    public const string EngineerIssue = "engineer-wise-issue";
    public const string DepartmentConsumption = "department-wise-consumption";
    public const string ReceiptRegister = "receipt-register";
    public const string IssueRegister = "issue-register";
    public const string MonthlyConsumption = "monthly-consumption";
    public const string QuarterlyConsumption = "quarterly-consumption";
    public const string HalfYearlyConsumption = "half-yearly-consumption";
    public const string YearlyConsumption = "yearly-consumption";
    public const string AbcAnalysis = "abc-analysis";
    public const string FastMoving = "fast-moving-items";
    public const string SlowMoving = "slow-moving-items";
    public const string DeadStock = "dead-stock";
    public const string PendingApprovals = "pending-approvals";

    private readonly IUnitOfWork _unitOfWork;
    private readonly IExportService _export;
    private readonly IAuditService _audit;
    private readonly ApplicationSettings _settings;

    public ReportService(
        IUnitOfWork unitOfWork,
        IExportService export,
        IAuditService audit,
        IOptions<ApplicationSettings> settings)
    {
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _export = export ?? throw new ArgumentNullException(nameof(export));
        _audit = audit ?? throw new ArgumentNullException(nameof(audit));
        _settings = settings?.Value ?? new ApplicationSettings();
    }

    /// <inheritdoc />
    public IReadOnlyList<ReportDescriptor> GetCatalog() => Catalog;

    /// <inheritdoc />
    public async Task<ReportOutput> RunAsync(
        string reportKey,
        ReportFilter filter,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(filter);

        var descriptor = Find(reportKey);
        var export = await BuildAsync(descriptor, filter, cancellationToken).ConfigureAwait(false);

        return export;
    }

    /// <inheritdoc />
    public async Task<FileExportResult> ExportAsync(
        string reportKey,
        ReportFilter filter,
        ExportFormat format,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(filter);

        var descriptor = Find(reportKey);
        var file = await RenderAsync(descriptor, filter, format, cancellationToken).ConfigureAwait(false);

        await _audit.LogAsync(
            AuditAction.Export,
            "Report",
            descriptor.Key,
            $"Exported the '{descriptor.Title}' report as {format}.",
            cancellationToken: cancellationToken).ConfigureAwait(false);

        return file;
    }

    // -----------------------------------------------------------------------
    // Catalog
    // -----------------------------------------------------------------------

    private static readonly ReportDescriptor[] Catalog =
    {
        new() { Key = CurrentStock, Title = "Current Stock", Group = "Stock", Icon = "bi-boxes",
            Description = "Balance, reserved quantity and value of every item in every warehouse.",
            Filters = new[] { "category", "warehouse", "site", "itemType" } },
        new() { Key = ItemWiseStock, Title = "Item-wise Stock", Group = "Stock", Icon = "bi-box-seam",
            Description = "Balance of a single item across all warehouses.",
            Filters = new[] { "item" } },
        new() { Key = SiteWiseStock, Title = "Site-wise Stock", Group = "Stock", Icon = "bi-geo-alt",
            Description = "Stock value summarised by project site.",
            Filters = new[] { "site" } },
        new() { Key = WarehouseStock, Title = "Warehouse Stock", Group = "Stock", Icon = "bi-building",
            Description = "Stock value summarised by warehouse.",
            Filters = new[] { "warehouse", "site" } },
        new() { Key = StockLedger, Title = "Stock Ledger", Group = "Stock", Icon = "bi-journal-text",
            Description = "Every movement of an item, with the running balance.",
            Filters = new[] { "item", "warehouse", "dateRange" } },
        new() { Key = LowStock, Title = "Low Stock", Group = "Stock", Icon = "bi-exclamation-triangle",
            Description = "Items at or below their reorder level.",
            Filters = new[] { "warehouse" } },

        new() { Key = ReceiptRegister, Title = "Material Receipt Register", Group = "Transactions", Icon = "bi-box-arrow-in-down",
            Description = "Every goods receipt line for the selected period.",
            Filters = new[] { "dateRange", "vendor", "warehouse" } },
        new() { Key = IssueRegister, Title = "Material Issue Register", Group = "Transactions", Icon = "bi-box-arrow-up",
            Description = "Every material issue line for the selected period.",
            Filters = new[] { "dateRange", "department", "site", "engineer", "warehouse" } },
        new() { Key = PendingApprovals, Title = "Pending Approvals", Group = "Transactions", Icon = "bi-hourglass-split",
            Description = "Documents waiting for an approval decision, with their age.",
            Filters = Array.Empty<string>() },

        new() { Key = VendorPurchase, Title = "Vendor Purchase", Group = "Analysis", Icon = "bi-truck",
            Description = "Purchase value, receipt count and rejection rate by vendor.",
            Filters = new[] { "dateRange", "vendor" } },
        new() { Key = EngineerIssue, Title = "Engineer-wise Issue", Group = "Analysis", Icon = "bi-person-gear",
            Description = "Material consumed by each engineer.",
            Filters = new[] { "dateRange", "engineer", "department" } },
        new() { Key = DepartmentConsumption, Title = "Department-wise Consumption", Group = "Analysis", Icon = "bi-diagram-3",
            Description = "Material consumed by each department.",
            Filters = new[] { "dateRange", "department" } },
        new() { Key = AbcAnalysis, Title = "ABC Analysis", Group = "Analysis", Icon = "bi-bar-chart-steps",
            Description = "Items ranked by consumption value and split into A, B and C classes.",
            Filters = new[] { "dateRange", "category" } },
        new() { Key = FastMoving, Title = "Fast Moving Items", Group = "Analysis", Icon = "bi-speedometer2",
            Description = "Items issued most frequently in the period.",
            Filters = new[] { "dateRange", "category", "warehouse" } },
        new() { Key = SlowMoving, Title = "Slow Moving Items", Group = "Analysis", Icon = "bi-hourglass",
            Description = "Items with little movement in the period.",
            Filters = new[] { "dateRange", "category", "warehouse" } },
        new() { Key = DeadStock, Title = "Dead Stock", Group = "Analysis", Icon = "bi-archive",
            Description = "Items held in stock with no outward movement in the configured window.",
            Filters = new[] { "category", "warehouse" } },

        new() { Key = MonthlyConsumption, Title = "Monthly Consumption", Group = "Consumption", Icon = "bi-calendar-month",
            Description = "Consumption and purchase totals by month.",
            Filters = new[] { "dateRange", "category", "item" } },
        new() { Key = QuarterlyConsumption, Title = "Quarterly Consumption", Group = "Consumption", Icon = "bi-calendar3",
            Description = "Consumption and purchase totals by quarter.",
            Filters = new[] { "dateRange", "category", "item" } },
        new() { Key = HalfYearlyConsumption, Title = "Half-Yearly Consumption", Group = "Consumption", Icon = "bi-calendar2-range",
            Description = "Consumption and purchase totals by half year.",
            Filters = new[] { "dateRange", "category", "item" } },
        new() { Key = YearlyConsumption, Title = "Yearly Consumption", Group = "Consumption", Icon = "bi-calendar4",
            Description = "Consumption and purchase totals by financial year.",
            Filters = new[] { "dateRange", "category", "item" } }
    };

    private static ReportDescriptor Find(string reportKey)
        => Catalog.FirstOrDefault(r => string.Equals(r.Key, reportKey, StringComparison.OrdinalIgnoreCase))
           ?? throw new Inventory.Common.Exceptions.EntityNotFoundException("Report", reportKey ?? "(null)");

    // -----------------------------------------------------------------------
    // Execution
    // -----------------------------------------------------------------------

    /// <summary>
    /// Runs the report and renders it to a file. The report's export definition
    /// is built once and reused, which is why the grid and the file always show
    /// the same columns in the same order.
    /// </summary>
    private async Task<FileExportResult> RenderAsync(
        ReportDescriptor descriptor,
        ReportFilter filter,
        ExportFormat format,
        CancellationToken cancellationToken)
    {
        var subTitle = $"{filter.EffectiveFrom.ToDisplayDate()} to {filter.EffectiveTo.ToDisplayDate()}";
        var reports = _unitOfWork.Reports;

        switch (descriptor.Key)
        {
            case CurrentStock:
            case ItemWiseStock:
            {
                var rows = await reports.GetCurrentStockAsync(filter, cancellationToken).ConfigureAwait(false);
                return _export.Export(BuildCurrentStock(descriptor, rows), format);
            }

            case StockLedger:
            {
                var rows = await reports.GetStockLedgerAsync(filter, cancellationToken).ConfigureAwait(false);
                return _export.Export(BuildStockLedger(descriptor, subTitle, rows), format);
            }

            case LowStock:
            {
                var rows = await reports.GetLowStockAsync(filter, cancellationToken).ConfigureAwait(false);
                return _export.Export(BuildLowStock(descriptor, rows), format);
            }

            case SiteWiseStock:
            {
                var rows = await reports.GetSiteWiseStockAsync(filter, cancellationToken).ConfigureAwait(false);
                return _export.Export(BuildLocationStock(descriptor, "Site", rows), format);
            }

            case WarehouseStock:
            {
                var rows = await reports.GetWarehouseStockAsync(filter, cancellationToken).ConfigureAwait(false);
                return _export.Export(BuildLocationStock(descriptor, "Warehouse", rows), format);
            }

            case VendorPurchase:
            {
                var rows = await reports.GetVendorPurchaseAsync(filter, cancellationToken).ConfigureAwait(false);
                return _export.Export(BuildVendorPurchase(descriptor, subTitle, rows), format);
            }

            case EngineerIssue:
            {
                var rows = await reports.GetEngineerWiseIssueAsync(filter, cancellationToken).ConfigureAwait(false);
                return _export.Export(BuildConsumption(descriptor, subTitle, "Engineer", rows), format);
            }

            case DepartmentConsumption:
            {
                var rows = await reports.GetDepartmentWiseConsumptionAsync(filter, cancellationToken).ConfigureAwait(false);
                return _export.Export(BuildConsumption(descriptor, subTitle, "Department", rows), format);
            }

            case ReceiptRegister:
            {
                var rows = await reports.GetReceiptRegisterAsync(filter, cancellationToken).ConfigureAwait(false);
                return _export.Export(BuildReceiptRegister(descriptor, subTitle, rows), format);
            }

            case IssueRegister:
            {
                var rows = await reports.GetIssueRegisterAsync(filter, cancellationToken).ConfigureAwait(false);
                return _export.Export(BuildIssueRegister(descriptor, subTitle, rows), format);
            }

            case MonthlyConsumption:
            case QuarterlyConsumption:
            case HalfYearlyConsumption:
            case YearlyConsumption:
            {
                var procedure = descriptor.Key switch
                {
                    QuarterlyConsumption => StoredProcedures.GetQuarterlyConsumption,
                    HalfYearlyConsumption => StoredProcedures.GetHalfYearlyConsumption,
                    YearlyConsumption => StoredProcedures.GetYearlyConsumption,
                    _ => StoredProcedures.GetMonthlyConsumption
                };

                var rows = await reports.GetPeriodConsumptionAsync(procedure, filter, cancellationToken)
                    .ConfigureAwait(false);

                return _export.Export(BuildPeriodConsumption(descriptor, subTitle, rows), format);
            }

            case AbcAnalysis:
            {
                var rows = await reports.GetAbcAnalysisAsync(filter, cancellationToken).ConfigureAwait(false);
                return _export.Export(BuildAbc(descriptor, subTitle, rows), format);
            }

            case FastMoving:
            case SlowMoving:
            {
                var category = descriptor.Key == FastMoving ? MovementCategory.Fast : MovementCategory.Slow;
                var rows = await reports.GetMovementAnalysisAsync(filter, category, cancellationToken)
                    .ConfigureAwait(false);

                return _export.Export(BuildMovement(descriptor, subTitle, rows), format);
            }

            case DeadStock:
            {
                var rows = await reports.GetDeadStockAsync(filter, cancellationToken).ConfigureAwait(false);
                return _export.Export(
                    BuildMovement(descriptor, $"No movement in the last {_settings.DeadStockDays} days", rows),
                    format);
            }

            case PendingApprovals:
            {
                var rows = await reports.GetPendingApprovalsAsync(filter, cancellationToken).ConfigureAwait(false);
                return _export.Export(BuildPendingApprovals(descriptor, rows), format);
            }

            default:
                throw new Inventory.Common.Exceptions.EntityNotFoundException("Report", descriptor.Key);
        }
    }

    /// <summary>
    /// Runs a report and flattens it into the grid shape. It reuses the export
    /// definition, so the grid can never disagree with the exported file.
    /// </summary>
    private async Task<ReportOutput> BuildAsync(
        ReportDescriptor descriptor,
        ReportFilter filter,
        CancellationToken cancellationToken)
    {
        // Render to CSV once and reuse the definition-driven cell values.
        var csv = await RenderAsync(descriptor, filter, ExportFormat.Csv, cancellationToken).ConfigureAwait(false);
        var text = System.Text.Encoding.UTF8.GetString(csv.Content).TrimStart('﻿');

        var lines = text.Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(l => l.TrimEnd('\r'))
            .ToList();

        if (lines.Count == 0)
        {
            return new ReportOutput
            {
                Title = descriptor.Title,
                Columns = Array.Empty<string>(),
                Rows = Array.Empty<IReadOnlyList<string>>()
            };
        }

        var columns = ParseCsvLine(lines[0]);
        var rows = lines.Skip(1).Select(ParseCsvLine).ToList();

        return new ReportOutput
        {
            Title = descriptor.Title,
            Columns = columns,
            Rows = rows,
            NumericColumns = columns
                .Select((c, i) => (Column: c, Index: i))
                .Where(x => x.Column.ContainsIgnoreCase("qty")
                            || x.Column.ContainsIgnoreCase("quantity")
                            || x.Column.ContainsIgnoreCase("value")
                            || x.Column.ContainsIgnoreCase("cost")
                            || x.Column.ContainsIgnoreCase("amount")
                            || x.Column.ContainsIgnoreCase("count")
                            || x.Column.ContainsIgnoreCase("%"))
                .Select(x => x.Index)
                .ToList()
        };
    }

    /// <summary>Minimal RFC 4180 reader for the CSV the export service produced.</summary>
    private static IReadOnlyList<string> ParseCsvLine(string line)
    {
        var cells = new List<string>();
        var current = new System.Text.StringBuilder();
        var inQuotes = false;

        for (var i = 0; i < line.Length; i++)
        {
            var c = line[i];

            if (inQuotes)
            {
                if (c == '"')
                {
                    if (i + 1 < line.Length && line[i + 1] == '"')
                    {
                        current.Append('"');
                        i++;
                    }
                    else
                    {
                        inQuotes = false;
                    }
                }
                else
                {
                    current.Append(c);
                }
            }
            else
            {
                switch (c)
                {
                    case '"':
                        inQuotes = true;
                        break;
                    case ',':
                        cells.Add(current.ToString());
                        current.Clear();
                        break;
                    default:
                        current.Append(c);
                        break;
                }
            }
        }

        cells.Add(current.ToString());
        return cells;
    }

    // -----------------------------------------------------------------------
    // Column layouts
    // -----------------------------------------------------------------------

    private static ExportDefinition<CurrentStockDto> BuildCurrentStock(
        ReportDescriptor descriptor,
        IReadOnlyList<CurrentStockDto> rows)
        => Exports.For<CurrentStockDto>()
            .WithTitle(descriptor.Title)
            .WithSubTitle($"As at {DateTime.Now.ToDisplayDateTime()}")
            .Column("Item Code", r => r.ItemCode, width: 0.9f)
            .Column("Item Name", r => r.ItemName, width: 2.4f)
            .Column("Part Number", r => r.PartNumber, width: 1f)
            .Column("Category", r => r.CategoryName, width: 1.1f)
            .Column("Warehouse", r => r.WarehouseName, width: 1.2f)
            .Column("Site", r => r.SiteName, width: 1f)
            .Column("UOM", r => r.UnitSymbol, width: 0.4f)
            .QuantityColumn("Quantity", r => r.Quantity)
            .QuantityColumn("Reserved", r => r.ReservedQuantity)
            .QuantityColumn("Available", r => r.AvailableQuantity)
            .MoneyColumn("Avg. Cost", r => r.AverageCost, summable: false)
            .MoneyColumn("Value", r => r.StockValue)
            .QuantityColumn("Reorder Level", r => r.ReorderLevel, summable: false)
            .Column("Alert", r => r.IsLowStock ? "LOW" : string.Empty, width: 0.4f)
            .Build(rows);

    private static ExportDefinition<StockLedgerDto> BuildStockLedger(
        ReportDescriptor descriptor,
        string subTitle,
        IReadOnlyList<StockLedgerDto> rows)
        => Exports.For<StockLedgerDto>()
            .WithTitle(descriptor.Title)
            .WithSubTitle(subTitle)
            .DateColumn("Date", r => r.TransactionDate)
            .Column("Item Code", r => r.ItemCode, width: 0.9f)
            .Column("Item Name", r => r.ItemName, width: 2.2f)
            .Column("Warehouse", r => r.WarehouseName, width: 1.1f)
            .Column("Type", r => r.MovementTypeName, width: 0.8f)
            .Column("Document", r => r.DocumentNumber, width: 1.1f)
            .Column("Batch", r => r.BatchNumber, width: 0.7f)
            .QuantityColumn("Inward", r => r.InwardQuantity)
            .QuantityColumn("Outward", r => r.OutwardQuantity)
            .QuantityColumn("Balance", r => r.BalanceQuantity, summable: false)
            .MoneyColumn("Rate", r => r.UnitCost, summable: false)
            .MoneyColumn("Value", r => r.Value)
            .Build(rows);

    private static ExportDefinition<LowStockDto> BuildLowStock(
        ReportDescriptor descriptor,
        IReadOnlyList<LowStockDto> rows)
        => Exports.For<LowStockDto>()
            .WithTitle(descriptor.Title)
            .WithSubTitle($"{rows.Count:N0} item(s) at or below the reorder level")
            .Column("Item Code", r => r.ItemCode, width: 0.9f)
            .Column("Item Name", r => r.ItemName, width: 2.4f)
            .Column("Category", r => r.CategoryName, width: 1.1f)
            .Column("Warehouse", r => r.WarehouseName, width: 1.1f)
            .Column("UOM", r => r.UnitSymbol, width: 0.4f)
            .QuantityColumn("Current Stock", r => r.CurrentStock)
            .QuantityColumn("Reorder Level", r => r.ReorderLevel, summable: false)
            .QuantityColumn("Shortfall", r => r.Shortfall)
            .QuantityColumn("Suggested Order", r => r.ReorderQuantity)
            .MoneyColumn("Avg. Cost", r => r.AverageCost, summable: false)
            .Build(rows);

    private static ExportDefinition<LocationStockDto> BuildLocationStock(
        ReportDescriptor descriptor,
        string locationLabel,
        IReadOnlyList<LocationStockDto> rows)
        => Exports.For<LocationStockDto>()
            .WithTitle(descriptor.Title)
            .WithSubTitle($"As at {DateTime.Now.ToDisplayDateTime()}")
            .Column(locationLabel, r => r.LocationName, width: 2f)
            .Column("Belongs To", r => r.ParentName, width: 1.5f)
            .Column("Items", r => r.ItemCount, alignRight: true, summable: true)
            .QuantityColumn("Total Quantity", r => r.TotalQuantity)
            .MoneyColumn("Stock Value", r => r.TotalValue)
            .Column("Low Stock Items", r => r.LowStockCount, alignRight: true, summable: true)
            .Landscape(false)
            .Build(rows);

    private static ExportDefinition<VendorPurchaseDto> BuildVendorPurchase(
        ReportDescriptor descriptor,
        string subTitle,
        IReadOnlyList<VendorPurchaseDto> rows)
        => Exports.For<VendorPurchaseDto>()
            .WithTitle(descriptor.Title)
            .WithSubTitle(subTitle)
            .Column("Vendor Code", r => r.VendorCode, width: 0.8f)
            .Column("Vendor Name", r => r.VendorName, width: 2.4f)
            .Column("Receipts", r => r.ReceiptCount, alignRight: true, summable: true)
            .Column("Items", r => r.ItemCount, alignRight: true, summable: true)
            .QuantityColumn("Quantity", r => r.TotalQuantity)
            .MoneyColumn("Purchase Value", r => r.TotalValue)
            .QuantityColumn("Rejected", r => r.RejectedQuantity)
            .Column("Rejection %", r => r.RejectionRate, "0.00", alignRight: true)
            .DateColumn("Last Supply", r => r.LastSupplyDate)
            .Build(rows);

    private static ExportDefinition<ConsumptionDto> BuildConsumption(
        ReportDescriptor descriptor,
        string subTitle,
        string entityLabel,
        IReadOnlyList<ConsumptionDto> rows)
        => Exports.For<ConsumptionDto>()
            .WithTitle(descriptor.Title)
            .WithSubTitle(subTitle)
            .Column("Code", r => r.EntityCode, width: 0.8f)
            .Column(entityLabel, r => r.EntityName, width: 2.2f)
            .Column("Group", r => r.GroupName, width: 1.4f)
            .Column("Issues", r => r.IssueCount, alignRight: true, summable: true)
            .Column("Items", r => r.ItemCount, alignRight: true, summable: true)
            .QuantityColumn("Quantity", r => r.TotalQuantity)
            .MoneyColumn("Value", r => r.TotalValue)
            .DateColumn("Last Issue", r => r.LastIssueDate)
            .Landscape(false)
            .Build(rows);

    private static ExportDefinition<ReceiptRegisterDto> BuildReceiptRegister(
        ReportDescriptor descriptor,
        string subTitle,
        IReadOnlyList<ReceiptRegisterDto> rows)
        => Exports.For<ReceiptRegisterDto>()
            .WithTitle(descriptor.Title)
            .WithSubTitle(subTitle)
            .DateColumn("Date", r => r.GrnDate)
            .Column("GRN Number", r => r.GrnNumber, width: 1.1f)
            .Column("Vendor", r => r.VendorName, width: 1.8f)
            .Column("PO Number", r => r.PurchaseOrderNumber, width: 0.9f)
            .Column("Item Code", r => r.ItemCode, width: 0.9f)
            .Column("Item Name", r => r.ItemName, width: 2f)
            .Column("UOM", r => r.UnitSymbol, width: 0.4f)
            .QuantityColumn("Received", r => r.QuantityReceived)
            .QuantityColumn("Accepted", r => r.QuantityAccepted)
            .QuantityColumn("Rejected", r => r.QuantityRejected)
            .MoneyColumn("Rate", r => r.UnitCost, summable: false)
            .MoneyColumn("Amount", r => r.TotalCost)
            .Column("Warehouse", r => r.WarehouseName, width: 1f)
            .Column("Status", r => r.StatusName, width: 0.8f)
            .Build(rows);

    private static ExportDefinition<IssueRegisterDto> BuildIssueRegister(
        ReportDescriptor descriptor,
        string subTitle,
        IReadOnlyList<IssueRegisterDto> rows)
        => Exports.For<IssueRegisterDto>()
            .WithTitle(descriptor.Title)
            .WithSubTitle(subTitle)
            .DateColumn("Date", r => r.IssueDate)
            .Column("Issue Number", r => r.IssueNumber, width: 1.1f)
            .Column("Department", r => r.DepartmentName, width: 1.2f)
            .Column("Site", r => r.SiteName, width: 1.1f)
            .Column("Engineer", r => r.EngineerName, width: 1.2f)
            .Column("Project", r => r.ProjectName, width: 1.3f)
            .Column("Item Code", r => r.ItemCode, width: 0.9f)
            .Column("Item Name", r => r.ItemName, width: 2f)
            .Column("UOM", r => r.UnitSymbol, width: 0.4f)
            .QuantityColumn("Requested", r => r.QuantityRequested)
            .QuantityColumn("Approved", r => r.QuantityApproved)
            .QuantityColumn("Issued", r => r.QuantityIssued)
            .MoneyColumn("Rate", r => r.UnitCost, summable: false)
            .MoneyColumn("Value", r => r.TotalCost)
            .Column("Status", r => r.StatusName, width: 0.8f)
            .Build(rows);

    private static ExportDefinition<PeriodConsumptionDto> BuildPeriodConsumption(
        ReportDescriptor descriptor,
        string subTitle,
        IReadOnlyList<PeriodConsumptionDto> rows)
        => Exports.For<PeriodConsumptionDto>()
            .WithTitle(descriptor.Title)
            .WithSubTitle(subTitle)
            .Column("Period", r => r.PeriodLabel, width: 1f)
            .Column("Item Code", r => r.ItemCode, width: 0.9f)
            .Column("Item Name", r => r.ItemName, width: 2.4f)
            .Column("Category", r => r.CategoryName, width: 1.2f)
            .Column("UOM", r => r.UnitSymbol, width: 0.4f)
            .QuantityColumn("Issued Qty", r => r.IssuedQuantity)
            .MoneyColumn("Issued Value", r => r.IssuedValue)
            .QuantityColumn("Received Qty", r => r.ReceivedQuantity)
            .MoneyColumn("Received Value", r => r.ReceivedValue)
            .Column("Transactions", r => r.TransactionCount, alignRight: true, summable: true)
            .Build(rows);

    private static ExportDefinition<AbcAnalysisDto> BuildAbc(
        ReportDescriptor descriptor,
        string subTitle,
        IReadOnlyList<AbcAnalysisDto> rows)
        => Exports.For<AbcAnalysisDto>()
            .WithTitle(descriptor.Title)
            .WithSubTitle($"{subTitle} · Class A to 70%, B to 90%, C to 100% of consumption value")
            .Column("Rank", r => r.Rank, alignRight: true, width: 0.4f)
            .Column("Item Code", r => r.ItemCode, width: 0.9f)
            .Column("Item Name", r => r.ItemName, width: 2.6f)
            .Column("Category", r => r.CategoryName, width: 1.2f)
            .QuantityColumn("Annual Qty", r => r.AnnualQuantity)
            .MoneyColumn("Annual Value", r => r.AnnualValue)
            .Column("Share %", r => r.ValuePercent, "0.00", alignRight: true)
            .Column("Cumulative %", r => r.CumulativePercent, "0.00", alignRight: true)
            .Column("Class", r => r.ClassificationName, width: 0.4f)
            .Landscape(false)
            .Build(rows);

    private static ExportDefinition<MovementAnalysisDto> BuildMovement(
        ReportDescriptor descriptor,
        string subTitle,
        IReadOnlyList<MovementAnalysisDto> rows)
        => Exports.For<MovementAnalysisDto>()
            .WithTitle(descriptor.Title)
            .WithSubTitle(subTitle)
            .Column("Item Code", r => r.ItemCode, width: 0.9f)
            .Column("Item Name", r => r.ItemName, width: 2.4f)
            .Column("Category", r => r.CategoryName, width: 1.2f)
            .Column("UOM", r => r.UnitSymbol, width: 0.4f)
            .QuantityColumn("Current Stock", r => r.CurrentStock)
            .MoneyColumn("Stock Value", r => r.StockValue)
            .Column("Issues", r => r.IssueCount, alignRight: true, summable: true)
            .QuantityColumn("Issued Qty", r => r.IssuedQuantity)
            .DateColumn("Last Issue", r => r.LastIssueDate)
            .Column("Idle Days", r => r.DaysSinceLastMovement, alignRight: true)
            .Column("Turnover", r => r.TurnoverRatio, "0.00", alignRight: true)
            .Column("Category", r => r.CategoryLabel, width: 0.6f)
            .Build(rows);

    private static ExportDefinition<PendingApprovalDto> BuildPendingApprovals(
        ReportDescriptor descriptor,
        IReadOnlyList<PendingApprovalDto> rows)
        => Exports.For<PendingApprovalDto>()
            .WithTitle(descriptor.Title)
            .WithSubTitle($"{rows.Count:N0} document(s) awaiting a decision")
            .Column("Type", r => r.DocumentTypeName, width: 1.1f)
            .Column("Document", r => r.DocumentNumber, width: 1.1f)
            .DateColumn("Date", r => r.DocumentDate)
            .Column("Party", r => r.PartyName, width: 1.8f)
            .Column("Warehouse", r => r.WarehouseName, width: 1.1f)
            .Column("Lines", r => r.LineCount, alignRight: true, summable: true)
            .MoneyColumn("Value", r => r.TotalValue)
            .Column("Raised By", r => r.RequestedByName, width: 1.2f)
            .DateColumn("Submitted", r => r.SubmittedOn)
            .Column("Age (days)", r => r.AgeInDays, alignRight: true)
            .Landscape(false)
            .Build(rows);
}
