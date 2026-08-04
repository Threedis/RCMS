namespace Inventory.Entities.Dtos;

/// <summary>
/// Everything the executive dashboard renders. Produced by a single stored
/// procedure (<c>sp_GetDashboard</c>) returning multiple result sets, so one
/// round trip populates the whole screen.
/// </summary>
public class DashboardDto
{
    public DashboardKpiDto Kpis { get; set; } = new();

    /// <summary>Stock value split by item category (donut chart).</summary>
    public List<ChartPointDto> StockByCategory { get; set; } = new();

    /// <summary>Stock value split by warehouse (bar chart).</summary>
    public List<ChartPointDto> StockByWarehouse { get; set; } = new();

    /// <summary>Last twelve months of inward and outward value (line chart).</summary>
    public List<MonthlyTrendDto> MonthlyTrend { get; set; } = new();

    /// <summary>Highest value suppliers for the current financial year (bar chart).</summary>
    public List<ChartPointDto> TopVendors { get; set; } = new();

    /// <summary>Most frequently issued items (bar chart).</summary>
    public List<ChartPointDto> FastMovingItems { get; set; } = new();

    /// <summary>Items with no movement in the configured window.</summary>
    public List<ChartPointDto> SlowMovingItems { get; set; } = new();

    /// <summary>Document status distribution (pie chart).</summary>
    public List<ChartPointDto> DocumentStatusSplit { get; set; } = new();

    /// <summary>Items at or below their reorder level.</summary>
    public List<LowStockDto> LowStockItems { get; set; } = new();

    /// <summary>Documents waiting for the signed-in user's approval.</summary>
    public List<PendingApprovalDto> PendingApprovals { get; set; } = new();

    /// <summary>Latest audit entries, newest first.</summary>
    public List<RecentActivityDto> RecentActivities { get; set; } = new();
}

/// <summary>The KPI cards across the top of the dashboard.</summary>
public class DashboardKpiDto
{
    public decimal TotalInventoryValue { get; set; }

    public decimal TotalStockQuantity { get; set; }

    public int TotalItems { get; set; }

    public int TotalSpareParts { get; set; }

    public int TotalConsumables { get; set; }

    public int TotalVendors { get; set; }

    public int TotalWarehouses { get; set; }

    public int TodayInwardCount { get; set; }

    public decimal TodayInwardValue { get; set; }

    public int TodayOutwardCount { get; set; }

    public decimal TodayOutwardValue { get; set; }

    public int PendingApprovalCount { get; set; }

    public int LowStockCount { get; set; }

    public int DeadStockCount { get; set; }

    public decimal DeadStockValue { get; set; }

    public int FastMovingCount { get; set; }

    public int SlowMovingCount { get; set; }

    public decimal MonthConsumptionValue { get; set; }

    public decimal MonthPurchaseValue { get; set; }

    /// <summary>Month-on-month change in consumption, in percent.</summary>
    public decimal ConsumptionChangePercent { get; set; }

    /// <summary>Month-on-month change in purchase value, in percent.</summary>
    public decimal PurchaseChangePercent { get; set; }
}

/// <summary>Generic label/value pair consumed by every Chart.js data set.</summary>
public class ChartPointDto
{
    public string Label { get; set; } = string.Empty;

    public decimal Value { get; set; }

    /// <summary>Secondary measure, e.g. quantity alongside value.</summary>
    public decimal SecondaryValue { get; set; }

    /// <summary>Optional identifier so a click can drill through to a filtered report.</summary>
    public int? EntityId { get; set; }
}

/// <summary>One month of the inward vs outward trend line.</summary>
public class MonthlyTrendDto
{
    public int Year { get; set; }

    public int Month { get; set; }

    /// <summary>Display label, e.g. <c>Apr 2026</c>.</summary>
    public string MonthLabel { get; set; } = string.Empty;

    public decimal InwardValue { get; set; }

    public decimal OutwardValue { get; set; }

    public decimal InwardQuantity { get; set; }

    public decimal OutwardQuantity { get; set; }
}

/// <summary>A recent-activity feed entry.</summary>
public class RecentActivityDto
{
    public DateTime ActivityOn { get; set; }

    public string ActionName { get; set; } = string.Empty;

    public string? EntityName { get; set; }

    public string? Description { get; set; }

    public string? UserName { get; set; }

    /// <summary>Bootstrap icon class chosen by the view helper.</summary>
    public string? Icon { get; set; }

    public string? ActionUrl { get; set; }
}
