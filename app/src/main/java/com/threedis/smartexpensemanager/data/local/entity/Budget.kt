package com.threedis.smartexpensemanager.data.local.entity

import androidx.room.Entity
import androidx.room.ForeignKey
import androidx.room.Index
import androidx.room.PrimaryKey

enum class BudgetPeriod { DAILY, WEEKLY, MONTHLY, FINANCIAL_YEAR }

@Entity(
    tableName = "budgets",
    foreignKeys = [
        ForeignKey(
            entity = Category::class,
            parentColumns = ["id"],
            childColumns = ["categoryId"],
            onDelete = ForeignKey.CASCADE
        )
    ],
    indices = [Index("categoryId"), Index("period")]
)
data class Budget(
    @PrimaryKey(autoGenerate = true) val id: Long = 0,
    /** Null categoryId means an overall (not category-specific) budget */
    val categoryId: Long? = null,
    val period: BudgetPeriod,
    val amount: Double,
    val isActive: Boolean = true
)
