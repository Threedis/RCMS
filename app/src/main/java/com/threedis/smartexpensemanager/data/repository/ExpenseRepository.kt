package com.threedis.smartexpensemanager.data.repository

import androidx.sqlite.db.SimpleSQLiteQuery
import com.threedis.smartexpensemanager.data.local.dao.CategorySpend
import com.threedis.smartexpensemanager.data.local.dao.DailySpend
import com.threedis.smartexpensemanager.data.local.dao.ExpenseDao
import com.threedis.smartexpensemanager.data.local.entity.Expense
import kotlinx.coroutines.flow.Flow
import javax.inject.Inject
import javax.inject.Singleton

@Singleton
class ExpenseRepository @Inject constructor(
    private val expenseDao: ExpenseDao
) {
    suspend fun add(expense: Expense): Long = expenseDao.insert(expense)
    suspend fun update(expense: Expense) = expenseDao.update(expense)
    suspend fun delete(expense: Expense) = expenseDao.delete(expense)
    suspend fun getById(id: Long): Expense? = expenseDao.getById(id)

    fun observeRecent(limit: Int = 10): Flow<List<Expense>> = expenseDao.observeRecent(limit)

    fun observeByEpochDayRange(startEpochDay: Long, endEpochDay: Long): Flow<List<Expense>> =
        expenseDao.observeByEpochDayRange(startEpochDay, endEpochDay)

    suspend fun sumByEpochDayRange(startEpochDay: Long, endEpochDay: Long): Double =
        expenseDao.sumByEpochDayRange(startEpochDay, endEpochDay)

    suspend fun countByEpochDayRange(startEpochDay: Long, endEpochDay: Long): Int =
        expenseDao.countByEpochDayRange(startEpochDay, endEpochDay)

    suspend fun categoryBreakdown(startEpochDay: Long, endEpochDay: Long): List<CategorySpend> =
        expenseDao.categoryBreakdown(startEpochDay, endEpochDay)

    suspend fun dailyTrend(startEpochDay: Long, endEpochDay: Long): List<DailySpend> =
        expenseDao.dailyTrend(startEpochDay, endEpochDay)

    fun search(query: String): Flow<List<Expense>> = expenseDao.searchAll(query)

    fun filter(filter: ExpenseFilter): Flow<List<Expense>> {
        val where = mutableListOf<String>()
        val args = mutableListOf<Any?>()

        filter.startEpochDay?.let { where += "epochDay >= ?"; args += it }
        filter.endEpochDay?.let { where += "epochDay <= ?"; args += it }
        filter.categoryId?.let { where += "categoryId = ?"; args += it }
        filter.paymentMethod?.let { where += "paymentMethod = ?"; args += it.name }
        filter.location?.takeIf { it.isNotBlank() }?.let { where += "location LIKE ?"; args += "%$it%" }
        filter.minAmount?.let { where += "amount >= ?"; args += it }
        filter.maxAmount?.let { where += "amount <= ?"; args += it }
        filter.searchText?.takeIf { it.isNotBlank() }?.let {
            where += "(description LIKE ? OR merchantName LIKE ? OR remarks LIKE ? OR location LIKE ?)"
            repeat(4) { _ -> args += "%$it%" }
        }

        val whereClause = if (where.isEmpty()) "" else "WHERE " + where.joinToString(" AND ")
        val orderBy = when (filter.sortOrder) {
            SortOrder.LATEST_FIRST -> "timestampMillis DESC"
            SortOrder.OLDEST_FIRST -> "timestampMillis ASC"
            SortOrder.HIGHEST_AMOUNT -> "amount DESC"
            SortOrder.LOWEST_AMOUNT -> "amount ASC"
        }

        val sql = "SELECT * FROM expenses $whereClause ORDER BY $orderBy"
        return expenseDao.filterExpenses(SimpleSQLiteQuery(sql, args.toTypedArray()))
    }

    suspend fun totalCount(): Int = expenseDao.totalCount()
}
