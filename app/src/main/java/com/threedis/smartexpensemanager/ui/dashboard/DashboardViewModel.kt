package com.threedis.smartexpensemanager.ui.dashboard

import androidx.lifecycle.ViewModel
import androidx.lifecycle.viewModelScope
import com.threedis.smartexpensemanager.data.local.entity.BudgetPeriod
import com.threedis.smartexpensemanager.data.local.entity.Category
import com.threedis.smartexpensemanager.data.local.entity.Expense
import com.threedis.smartexpensemanager.data.repository.BudgetRepository
import com.threedis.smartexpensemanager.data.repository.CategoryRepository
import com.threedis.smartexpensemanager.data.repository.ExpenseRepository
import com.threedis.smartexpensemanager.util.DateUtils
import dagger.hilt.android.lifecycle.HiltViewModel
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.flow.first
import kotlinx.coroutines.launch
import javax.inject.Inject

enum class ChartPeriod { WEEKLY, MONTHLY, YEARLY }

data class CategorySlice(val category: Category?, val amount: Double)
data class TrendPoint(val epochDay: Long, val amount: Double)

data class DashboardUiState(
    val isLoading: Boolean = true,
    val todayTotal: Double = 0.0,
    val weekTotal: Double = 0.0,
    val monthTotal: Double = 0.0,
    val financialYearTotal: Double = 0.0,
    val entryCount: Int = 0,
    val monthlyBudget: Double = 0.0,
    val monthlyBudgetUsedPercent: Double = 0.0,
    val remainingBudget: Double = 0.0,
    val recentTransactions: List<Expense> = emptyList(),
    val chartPeriod: ChartPeriod = ChartPeriod.MONTHLY,
    val categoryBreakdown: List<CategorySlice> = emptyList(),
    val monthlyTrend: List<TrendPoint> = emptyList()
)

@HiltViewModel
class DashboardViewModel @Inject constructor(
    private val expenseRepository: ExpenseRepository,
    private val categoryRepository: CategoryRepository,
    private val budgetRepository: BudgetRepository
) : ViewModel() {

    private val _uiState = MutableStateFlow(DashboardUiState())
    val uiState: StateFlow<DashboardUiState> = _uiState.asStateFlow()

    init {
        refresh()
    }

    fun selectChartPeriod(period: ChartPeriod) {
        if (_uiState.value.chartPeriod == period) return
        _uiState.value = _uiState.value.copy(chartPeriod = period)
        viewModelScope.launch { loadCategoryBreakdown(period) }
    }

    fun refresh() {
        viewModelScope.launch {
            _uiState.value = _uiState.value.copy(isLoading = true)

            val today = DateUtils.today()
            val (weekStart, weekEnd) = DateUtils.weekRange(today)
            val (monthStart, monthEnd) = DateUtils.monthRange(today)
            val (fyStart, fyEnd) = DateUtils.financialYearRange(today)

            val todayTotal = expenseRepository.sumByEpochDayRange(today.toEpochDay(), today.toEpochDay())
            val weekTotal = expenseRepository.sumByEpochDayRange(weekStart.toEpochDay(), weekEnd.toEpochDay())
            val monthTotal = expenseRepository.sumByEpochDayRange(monthStart.toEpochDay(), monthEnd.toEpochDay())
            val fyTotal = expenseRepository.sumByEpochDayRange(fyStart.toEpochDay(), fyEnd.toEpochDay())
            val entryCount = expenseRepository.countByEpochDayRange(monthStart.toEpochDay(), monthEnd.toEpochDay())

            val trend = expenseRepository.dailyTrend(monthStart.toEpochDay(), monthEnd.toEpochDay())
                .map { TrendPoint(it.epochDay, it.total) }

            val monthlyBudgetStatus = budgetRepository.overallStatus(BudgetPeriod.MONTHLY)
            val monthlyBudget = monthlyBudgetStatus?.budgetAmount ?: 0.0
            val usedPercent = monthlyBudgetStatus?.utilizationPercent ?: 0.0
            val remaining = monthlyBudgetStatus?.remaining ?: 0.0

            val recent: List<Expense> = expenseRepository.observeRecent(10).first()

            _uiState.value = _uiState.value.copy(
                isLoading = false,
                todayTotal = todayTotal,
                weekTotal = weekTotal,
                monthTotal = monthTotal,
                financialYearTotal = fyTotal,
                entryCount = entryCount,
                monthlyBudget = monthlyBudget,
                monthlyBudgetUsedPercent = usedPercent,
                remainingBudget = remaining,
                recentTransactions = recent,
                monthlyTrend = trend
            )

            loadCategoryBreakdown(_uiState.value.chartPeriod)
        }
    }

    private suspend fun loadCategoryBreakdown(period: ChartPeriod) {
        val today = DateUtils.today()
        val (start, end) = when (period) {
            ChartPeriod.WEEKLY -> DateUtils.weekRange(today)
            ChartPeriod.MONTHLY -> DateUtils.monthRange(today)
            ChartPeriod.YEARLY -> DateUtils.yearRange(today)
        }

        val categoryList: List<Category> = categoryRepository.observeActiveCategories().first()
        val breakdown = expenseRepository.categoryBreakdown(start.toEpochDay(), end.toEpochDay())
        val categoryMap = categoryList.associateBy { it.id }
        val slices = breakdown.map { CategorySlice(it.categoryId?.let { id -> categoryMap[id] }, it.total) }
            .sortedByDescending { it.amount }

        _uiState.value = _uiState.value.copy(chartPeriod = period, categoryBreakdown = slices)
    }
}
