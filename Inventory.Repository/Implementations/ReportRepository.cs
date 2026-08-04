using System.Data;
using Dapper;
using Inventory.Common.Constants;
using Inventory.Common.Enums;
using Inventory.DAL.StoredProcedures;
using Inventory.Entities.Dtos;
using Inventory.Repository.Interfaces;

namespace Inventory.Repository.Implementations;

/// <summary>
/// Read-only reporting repository. Each method is a thin, strongly typed wrapper
/// over one stored procedure; all the set logic lives in SQL where it belongs.
/// </summary>
public sealed class ReportRepository : IReportRepository
{
    private readonly IStoredProcedureExecutor _executor;

    public ReportRepository(IStoredProcedureExecutor executor)
    {
        _executor = executor ?? throw new ArgumentNullException(nameof(executor));
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<CurrentStockDto>> GetCurrentStockAsync(
        ReportFilter filter,
        CancellationToken cancellationToken = default)
        => _executor.QueryAsync<CurrentStockDto>(
            StoredProcedures.GetCurrentStock,
            BuildParameters(filter, includePaging: false),
            cancellationToken);

    /// <inheritdoc />
    public Task<IReadOnlyList<StockLedgerDto>> GetStockLedgerAsync(
        ReportFilter filter,
        CancellationToken cancellationToken = default)
        => _executor.QueryAsync<StockLedgerDto>(
            StoredProcedures.GetStockLedger,
            BuildParameters(filter, includePaging: false),
            cancellationToken);

    /// <inheritdoc />
    public Task<IReadOnlyList<LocationStockDto>> GetSiteWiseStockAsync(
        ReportFilter filter,
        CancellationToken cancellationToken = default)
        => _executor.QueryAsync<LocationStockDto>(
            StoredProcedures.GetSiteWiseStock,
            BuildParameters(filter, includePaging: false),
            cancellationToken);

    /// <inheritdoc />
    public Task<IReadOnlyList<LocationStockDto>> GetWarehouseStockAsync(
        ReportFilter filter,
        CancellationToken cancellationToken = default)
        => _executor.QueryAsync<LocationStockDto>(
            StoredProcedures.GetWarehouseStock,
            BuildParameters(filter, includePaging: false),
            cancellationToken);

    /// <inheritdoc />
    public Task<IReadOnlyList<VendorPurchaseDto>> GetVendorPurchaseAsync(
        ReportFilter filter,
        CancellationToken cancellationToken = default)
        => _executor.QueryAsync<VendorPurchaseDto>(
            StoredProcedures.GetVendorPurchase,
            BuildParameters(filter, includePaging: false),
            cancellationToken);

    /// <inheritdoc />
    public Task<IReadOnlyList<ConsumptionDto>> GetEngineerWiseIssueAsync(
        ReportFilter filter,
        CancellationToken cancellationToken = default)
        => _executor.QueryAsync<ConsumptionDto>(
            StoredProcedures.GetEngineerWiseIssue,
            BuildParameters(filter, includePaging: false),
            cancellationToken);

    /// <inheritdoc />
    public Task<IReadOnlyList<ConsumptionDto>> GetDepartmentWiseConsumptionAsync(
        ReportFilter filter,
        CancellationToken cancellationToken = default)
        => _executor.QueryAsync<ConsumptionDto>(
            StoredProcedures.GetDepartmentWiseConsumption,
            BuildParameters(filter, includePaging: false),
            cancellationToken);

    /// <inheritdoc />
    public Task<IReadOnlyList<PeriodConsumptionDto>> GetPeriodConsumptionAsync(
        string procedureName,
        ReportFilter filter,
        CancellationToken cancellationToken = default)
    {
        // Only the four period procedures may be selected here; anything else
        // is a programming error, never user input.
        var allowed = new[]
        {
            StoredProcedures.GetMonthlyConsumption,
            StoredProcedures.GetQuarterlyConsumption,
            StoredProcedures.GetHalfYearlyConsumption,
            StoredProcedures.GetYearlyConsumption
        };

        if (!allowed.Contains(procedureName, StringComparer.OrdinalIgnoreCase))
        {
            throw new ArgumentException(
                $"'{procedureName}' is not a recognised period consumption procedure.", nameof(procedureName));
        }

        return _executor.QueryAsync<PeriodConsumptionDto>(
            procedureName,
            BuildParameters(filter, includePaging: false),
            cancellationToken);
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<AbcAnalysisDto>> GetAbcAnalysisAsync(
        ReportFilter filter,
        CancellationToken cancellationToken = default)
        => _executor.QueryAsync<AbcAnalysisDto>(
            StoredProcedures.GetAbcAnalysis,
            BuildParameters(filter, includePaging: false),
            cancellationToken);

    /// <inheritdoc />
    public Task<IReadOnlyList<MovementAnalysisDto>> GetMovementAnalysisAsync(
        ReportFilter filter,
        MovementCategory? category = null,
        CancellationToken cancellationToken = default)
    {
        var parameters = BuildParameters(filter, includePaging: false);
        parameters.Add("@MovementCategory", (int?)category);

        return _executor.QueryAsync<MovementAnalysisDto>(
            StoredProcedures.GetMovementAnalysis, parameters, cancellationToken);
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<MovementAnalysisDto>> GetDeadStockAsync(
        ReportFilter filter,
        CancellationToken cancellationToken = default)
        => _executor.QueryAsync<MovementAnalysisDto>(
            StoredProcedures.GetDeadStock,
            BuildParameters(filter, includePaging: false),
            cancellationToken);

    /// <inheritdoc />
    public Task<IReadOnlyList<LowStockDto>> GetLowStockAsync(
        ReportFilter filter,
        CancellationToken cancellationToken = default)
    {
        var parameters = new DynamicParameters();
        parameters.Add("@WarehouseId", filter.WarehouseId);
        parameters.Add("@MaxRows", 1000);

        return _executor.QueryAsync<LowStockDto>(
            StoredProcedures.GetLowStockItems, parameters, cancellationToken);
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<ReceiptRegisterDto>> GetReceiptRegisterAsync(
        ReportFilter filter,
        CancellationToken cancellationToken = default)
        => _executor.QueryAsync<ReceiptRegisterDto>(
            StoredProcedures.GetReceiptRegister,
            BuildParameters(filter, includePaging: false),
            cancellationToken);

    /// <inheritdoc />
    public Task<IReadOnlyList<IssueRegisterDto>> GetIssueRegisterAsync(
        ReportFilter filter,
        CancellationToken cancellationToken = default)
        => _executor.QueryAsync<IssueRegisterDto>(
            StoredProcedures.GetIssueRegister,
            BuildParameters(filter, includePaging: false),
            cancellationToken);

    /// <inheritdoc />
    public Task<IReadOnlyList<PendingApprovalDto>> GetPendingApprovalsAsync(
        ReportFilter filter,
        CancellationToken cancellationToken = default)
    {
        var parameters = new DynamicParameters();
        parameters.Add("@CurrentUserId", 0);
        parameters.Add("@DocumentType", filter.Status);

        return _executor.QueryAsync<PendingApprovalDto>(
            StoredProcedures.GetPendingApprovals, parameters, cancellationToken);
    }

    /// <summary>
    /// Every report procedure accepts the same optional filter parameters, so
    /// one builder serves them all.
    /// </summary>
    private static DynamicParameters BuildParameters(ReportFilter filter, bool includePaging)
    {
        ArgumentNullException.ThrowIfNull(filter);

        var parameters = new DynamicParameters();
        parameters.Add("@FromDate", filter.EffectiveFrom, DbType.Date);
        parameters.Add("@ToDate", filter.EffectiveTo, DbType.Date);
        parameters.Add("@ItemId", filter.ItemId);
        parameters.Add("@CategoryId", filter.CategoryId);
        parameters.Add("@WarehouseId", filter.WarehouseId);
        parameters.Add("@SiteId", filter.SiteId);
        parameters.Add("@VendorId", filter.VendorId);
        parameters.Add("@DepartmentId", filter.DepartmentId);
        parameters.Add("@EngineerId", filter.EngineerId);
        parameters.Add("@ItemType", filter.ItemType);
        parameters.Add("@SearchTerm", filter.SearchTerm);

        if (includePaging)
        {
            parameters.Add("@PageNumber", 1);
            parameters.Add("@PageSize", int.MaxValue);
        }

        return parameters;
    }
}

/// <summary>Dashboard repository: one multi-result procedure fills the whole screen.</summary>
public sealed class DashboardRepository : IDashboardRepository
{
    private readonly IStoredProcedureExecutor _executor;

    public DashboardRepository(IStoredProcedureExecutor executor)
    {
        _executor = executor ?? throw new ArgumentNullException(nameof(executor));
    }

    /// <inheritdoc />
    public Task<DashboardDto> GetDashboardAsync(int userId, CancellationToken cancellationToken = default)
        => _executor.QueryMultipleAsync(
            StoredProcedures.GetDashboard,
            async reader =>
            {
                var dashboard = new DashboardDto
                {
                    Kpis = await reader.ReadSingleOrDefaultAsync<DashboardKpiDto>().ConfigureAwait(false)
                           ?? new DashboardKpiDto()
                };

                dashboard.StockByCategory = (await reader.ReadAsync<ChartPointDto>().ConfigureAwait(false)).ToList();
                dashboard.StockByWarehouse = (await reader.ReadAsync<ChartPointDto>().ConfigureAwait(false)).ToList();
                dashboard.MonthlyTrend = (await reader.ReadAsync<MonthlyTrendDto>().ConfigureAwait(false)).ToList();
                dashboard.TopVendors = (await reader.ReadAsync<ChartPointDto>().ConfigureAwait(false)).ToList();
                dashboard.FastMovingItems = (await reader.ReadAsync<ChartPointDto>().ConfigureAwait(false)).ToList();
                dashboard.SlowMovingItems = (await reader.ReadAsync<ChartPointDto>().ConfigureAwait(false)).ToList();
                dashboard.DocumentStatusSplit = (await reader.ReadAsync<ChartPointDto>().ConfigureAwait(false)).ToList();
                dashboard.LowStockItems = (await reader.ReadAsync<LowStockDto>().ConfigureAwait(false)).ToList();
                dashboard.PendingApprovals = (await reader.ReadAsync<PendingApprovalDto>().ConfigureAwait(false)).ToList();
                dashboard.RecentActivities = (await reader.ReadAsync<RecentActivityDto>().ConfigureAwait(false)).ToList();

                return dashboard;
            },
            new { CurrentUserId = userId },
            cancellationToken);

    /// <inheritdoc />
    public Task<IReadOnlyList<RecentActivityDto>> GetRecentActivitiesAsync(
        int maxRows = 15,
        CancellationToken cancellationToken = default)
        => _executor.QueryAsync<RecentActivityDto>(
            StoredProcedures.GetRecentActivities,
            new { MaxRows = maxRows },
            cancellationToken);
}
