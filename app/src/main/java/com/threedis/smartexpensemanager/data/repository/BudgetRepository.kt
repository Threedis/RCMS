package com.threedis.smartexpensemanager.data.repository

import com.threedis.smartexpensemanager.data.local.dao.BudgetDao
import com.threedis.smartexpensemanager.data.local.dao.ExpenseDao
import com.threedis.smartexpensemanager.data.local.entity.Budget
import com.threedis.smartexpensemanager.data.local.entity.BudgetPeriod
import com.threedis.smartexpensemanager.util.DateUtils
import kotlinx.coroutines.flow.Flow
import java.time.LocalDate
import javax.inject.Inject
import javax.inject.Singleton

data class BudgetStatus(
    val period: BudgetPeriod,
    val categoryId: Long?,
    val budgetAmount: Double,
    val spentAmount: Double
) {
    val utilizationPercent: Double
        get() = if (budgetAmount <= 0) 0.0 else (spentAmount / budgetAmount) * 100.0
    val remaining: Double get() = (budgetAmount - spentAmount).coerceAtLeast(0.0)
    val isExceeded: Boolean get() = spentAmount > budgetAmount
}

@Singleton
class BudgetRepository @Inject constructor(
    private val budgetDao: BudgetDao,
    private val expenseDao: ExpenseDao
) {
    fun observeActiveBudgets(): Flow<List<Budget>> = budgetDao.observeActiveBudgets()

    suspend fun upsert(budget: Budget): Long = budgetDao.upsert(budget)
    suspend fun delete(budget: Budget) = budgetDao.delete(budget)

    private fun rangeFor(period: BudgetPeriod, fyStartMonth: Int, date: LocalDate): Pair<LocalDate, LocalDate> =
        when (period) {
            BudgetPeriod.DAILY -> date to date
            BudgetPeriod.WEEKLY -> DateUtils.weekRange(date)
            BudgetPeriod.MONTHLY -> DateUtils.monthRange(date)
            BudgetPeriod.FINANCIAL_YEAR -> DateUtils.financialYearRange(date, fyStartMonth)
        }

    /** Status of the overall budget (all categories) for the given period. */
    suspend fun overallStatus(period: BudgetPeriod, fyStartMonth: Int = 4, date: LocalDate = DateUtils.today()): BudgetStatus? {
        val budget = budgetDao.getOverallBudget(period) ?: return null
        val (start, end) = rangeFor(period, fyStartMonth, date)
        val spent = expenseDao.sumByEpochDayRange(DateUtils.toEpochDay(start), DateUtils.toEpochDay(end))
        return BudgetStatus(period, null, budget.amount, spent)
    }

    /** Status of a category-specific budget for the given period. */
    suspend fun categoryStatus(
        categoryId: Long,
        period: BudgetPeriod,
        fyStartMonth: Int = 4,
        date: LocalDate = DateUtils.today()
    ): BudgetStatus? {
        val budget = budgetDao.getCategoryBudget(period, categoryId) ?: return null
        val (start, end) = rangeFor(period, fyStartMonth, date)
        val spent = expenseDao.sumByEpochDayRangeAndCategory(
            DateUtils.toEpochDay(start), DateUtils.toEpochDay(end), categoryId
        )
        return BudgetStatus(period, categoryId, budget.amount, spent)
    }

    /** All budget statuses (overall + category) impacted by an expense in [categoryId], across all periods. */
    suspend fun statusesAffectedBy(categoryId: Long?, fyStartMonth: Int = 4): List<BudgetStatus> {
        val statuses = mutableListOf<BudgetStatus>()
        for (period in BudgetPeriod.entries) {
            overallStatus(period, fyStartMonth)?.let { statuses.add(it) }
            if (categoryId != null) {
                categoryStatus(categoryId, period, fyStartMonth)?.let { statuses.add(it) }
            }
        }
        return statuses
    }
}
