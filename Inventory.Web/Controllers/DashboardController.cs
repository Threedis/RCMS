using Inventory.BLL.Interfaces;
using Inventory.Common.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Inventory.Web.Controllers;

/// <summary>
/// Executive dashboard. The whole screen — KPI cards, five charts and three
/// panels — comes from one service call, which is one stored procedure
/// returning eleven result sets.
/// </summary>
[Authorize(Policy = Policies.ReadOnly)]
public sealed class DashboardController : BaseController
{
    private readonly IDashboardService _dashboard;
    private readonly IStockService _stock;

    public DashboardController(IDashboardService dashboard, IStockService stock)
    {
        _dashboard = dashboard;
        _stock = stock;
    }

    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var model = await _dashboard.GetDashboardAsync(cancellationToken).ConfigureAwait(false);
        return View(model);
    }

    /// <summary>Chart and KPI refresh for the auto-updating dashboard.</summary>
    [HttpGet]
    public async Task<IActionResult> Data(CancellationToken cancellationToken)
    {
        var model = await _dashboard.GetDashboardAsync(cancellationToken).ConfigureAwait(false);

        return JsonOk(new
        {
            model.Kpis,
            model.StockByCategory,
            model.StockByWarehouse,
            model.MonthlyTrend,
            model.TopVendors,
            model.FastMovingItems,
            model.SlowMovingItems,
            model.DocumentStatusSplit
        }, "Dashboard refreshed.");
    }

    /// <summary>Recent activity feed, polled by the dashboard.</summary>
    [HttpGet]
    public async Task<IActionResult> RecentActivity(int rows = 15, CancellationToken cancellationToken = default)
    {
        var activities = await _dashboard
            .GetRecentActivitiesAsync(Math.Clamp(rows, 5, 50), cancellationToken)
            .ConfigureAwait(false);

        return JsonOk(activities, "Loaded.");
    }

    /// <summary>Low-stock panel, also used by the alerts badge.</summary>
    [HttpGet]
    public async Task<IActionResult> LowStock(int? warehouseId, CancellationToken cancellationToken)
    {
        var items = await _stock.GetLowStockAsync(warehouseId, cancellationToken).ConfigureAwait(false);
        return JsonOk(items, $"{items.Count} item(s) at or below the reorder level.");
    }
}
