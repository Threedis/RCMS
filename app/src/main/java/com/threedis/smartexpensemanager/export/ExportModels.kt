package com.threedis.smartexpensemanager.export

import com.threedis.smartexpensemanager.data.local.entity.Category
import com.threedis.smartexpensemanager.data.local.entity.Expense

data class ExportRow(
    val expense: Expense,
    val categoryName: String
)

data class CategorySummaryRow(val categoryName: String, val total: Double, val count: Int)
data class BudgetSummaryRow(val label: String, val budget: Double, val actual: Double)
data class MonthlySummaryRow(val month: String, val total: Double)

fun buildExportRows(expenses: List<Expense>, categories: List<Category>): List<ExportRow> {
    val categoryMap = categories.associateBy { it.id }
    return expenses.map { e -> ExportRow(e, categoryMap[e.categoryId]?.name ?: "Uncategorized") }
}
