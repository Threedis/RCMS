package com.threedis.smartexpensemanager.data.local.dao

import androidx.room.*
import com.threedis.smartexpensemanager.data.local.entity.Budget
import com.threedis.smartexpensemanager.data.local.entity.BudgetPeriod
import kotlinx.coroutines.flow.Flow

@Dao
interface BudgetDao {
    @Query("SELECT * FROM budgets WHERE isActive = 1 ORDER BY period ASC")
    fun observeActiveBudgets(): Flow<List<Budget>>

    @Query("SELECT * FROM budgets WHERE period = :period AND categoryId IS NULL AND isActive = 1 LIMIT 1")
    suspend fun getOverallBudget(period: BudgetPeriod): Budget?

    @Query("SELECT * FROM budgets WHERE period = :period AND categoryId = :categoryId AND isActive = 1 LIMIT 1")
    suspend fun getCategoryBudget(period: BudgetPeriod, categoryId: Long): Budget?

    @Query("SELECT * FROM budgets WHERE categoryId = :categoryId AND isActive = 1")
    fun observeBudgetsForCategory(categoryId: Long): Flow<List<Budget>>

    @Insert(onConflict = OnConflictStrategy.REPLACE)
    suspend fun upsert(budget: Budget): Long

    @Update
    suspend fun update(budget: Budget)

    @Delete
    suspend fun delete(budget: Budget)
}
