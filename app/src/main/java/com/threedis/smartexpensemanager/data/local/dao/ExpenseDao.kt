package com.threedis.smartexpensemanager.data.local.dao

import androidx.room.*
import androidx.sqlite.db.SupportSQLiteQuery
import com.threedis.smartexpensemanager.data.local.entity.Expense
import kotlinx.coroutines.flow.Flow

data class CategorySpend(val categoryId: Long?, val total: Double)
data class DailySpend(val epochDay: Long, val total: Double)

@Dao
interface ExpenseDao {

    @Insert
    suspend fun insert(expense: Expense): Long

    @Update
    suspend fun update(expense: Expense)

    @Delete
    suspend fun delete(expense: Expense)

    @Query("SELECT * FROM expenses WHERE id = :id")
    suspend fun getById(id: Long): Expense?

    @Query("SELECT * FROM expenses ORDER BY timestampMillis DESC LIMIT :limit")
    fun observeRecent(limit: Int = 10): Flow<List<Expense>>

    @Query("SELECT * FROM expenses WHERE epochDay BETWEEN :start AND :end ORDER BY timestampMillis DESC")
    fun observeByEpochDayRange(start: Long, end: Long): Flow<List<Expense>>

    @Query("SELECT COALESCE(SUM(amount), 0) FROM expenses WHERE epochDay BETWEEN :start AND :end")
    suspend fun sumByEpochDayRange(start: Long, end: Long): Double

    @Query("SELECT COALESCE(SUM(amount), 0) FROM expenses WHERE epochDay BETWEEN :start AND :end AND categoryId = :categoryId")
    suspend fun sumByEpochDayRangeAndCategory(start: Long, end: Long, categoryId: Long): Double

    @Query("SELECT COUNT(*) FROM expenses WHERE epochDay BETWEEN :start AND :end")
    suspend fun countByEpochDayRange(start: Long, end: Long): Int

    @Query("SELECT categoryId, SUM(amount) as total FROM expenses WHERE epochDay BETWEEN :start AND :end GROUP BY categoryId")
    suspend fun categoryBreakdown(start: Long, end: Long): List<CategorySpend>

    @Query("SELECT epochDay, SUM(amount) as total FROM expenses WHERE epochDay BETWEEN :start AND :end GROUP BY epochDay ORDER BY epochDay ASC")
    suspend fun dailyTrend(start: Long, end: Long): List<DailySpend>

    @Query("""
        SELECT * FROM expenses
        WHERE (:query = '' OR description LIKE '%' || :query || '%'
            OR merchantName LIKE '%' || :query || '%'
            OR location LIKE '%' || :query || '%'
            OR remarks LIKE '%' || :query || '%')
        ORDER BY timestampMillis DESC
    """)
    fun searchAll(query: String): Flow<List<Expense>>

    @RawQuery(observedEntities = [Expense::class])
    fun filterExpenses(query: SupportSQLiteQuery): Flow<List<Expense>>

    @Query("SELECT COUNT(*) FROM expenses")
    suspend fun totalCount(): Int
}
