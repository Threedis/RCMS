package com.threedis.smartexpensemanager.data.local

import androidx.room.Database
import androidx.room.RoomDatabase
import androidx.room.TypeConverters
import com.threedis.smartexpensemanager.data.local.converter.Converters
import com.threedis.smartexpensemanager.data.local.dao.BudgetDao
import com.threedis.smartexpensemanager.data.local.dao.CategoryDao
import com.threedis.smartexpensemanager.data.local.dao.ExpenseDao
import com.threedis.smartexpensemanager.data.local.dao.SettingsDao
import com.threedis.smartexpensemanager.data.local.entity.AppSettings
import com.threedis.smartexpensemanager.data.local.entity.Budget
import com.threedis.smartexpensemanager.data.local.entity.Category
import com.threedis.smartexpensemanager.data.local.entity.Expense

@Database(
    entities = [Expense::class, Category::class, Budget::class, AppSettings::class],
    version = 1,
    exportSchema = true
)
@TypeConverters(Converters::class)
abstract class AppDatabase : RoomDatabase() {
    abstract fun expenseDao(): ExpenseDao
    abstract fun categoryDao(): CategoryDao
    abstract fun budgetDao(): BudgetDao
    abstract fun settingsDao(): SettingsDao

    companion object {
        const val DATABASE_NAME = "smart_expense_manager.db"
    }
}
