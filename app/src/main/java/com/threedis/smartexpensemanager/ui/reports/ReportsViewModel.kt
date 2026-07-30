package com.threedis.smartexpensemanager.ui.reports

import androidx.lifecycle.ViewModel
import androidx.lifecycle.viewModelScope
import com.threedis.smartexpensemanager.data.local.entity.Category
import com.threedis.smartexpensemanager.data.local.entity.Expense
import com.threedis.smartexpensemanager.data.repository.CategoryRepository
import com.threedis.smartexpensemanager.data.repository.ExpenseRepository
import com.threedis.smartexpensemanager.export.ExportFormat
import com.threedis.smartexpensemanager.export.ExportManager
import com.threedis.smartexpensemanager.export.buildExportRows
import com.threedis.smartexpensemanager.util.DateUtils
import dagger.hilt.android.lifecycle.HiltViewModel
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.flow.first
import kotlinx.coroutines.launch
import java.time.LocalDate
import javax.inject.Inject

enum class ReportPeriod { DAILY, WEEKLY, MONTHLY, QUARTERLY, FINANCIAL_YEAR, YEARLY }

data class ReportUiState(
    val expenses: List<Expense> = emptyList(),
    val total: Double = 0.0,
    val average: Double = 0.0,
    val highest: Double = 0.0,
    val lowest: Double = 0.0,
    val count: Int = 0,
    val exportUri: android.net.Uri? = null,
    val exportError: String? = null
)

@HiltViewModel
class ReportsViewModel @Inject constructor(
    private val expenseRepository: ExpenseRepository,
    private val categoryRepository: CategoryRepository,
    private val exportManager: ExportManager
) : ViewModel() {

    private val _uiState = MutableStateFlow(ReportUiState())
    val uiState: StateFlow<ReportUiState> = _uiState.asStateFlow()

    fun loadReport(period: ReportPeriod, referenceDate: LocalDate = DateUtils.today()) {
        viewModelScope.launch {
            val (start, end) = when (period) {
                ReportPeriod.DAILY -> referenceDate to referenceDate
                ReportPeriod.WEEKLY -> DateUtils.weekRange(referenceDate)
                ReportPeriod.MONTHLY -> DateUtils.monthRange(referenceDate)
                ReportPeriod.QUARTERLY -> DateUtils.quarterRange(referenceDate)
                ReportPeriod.FINANCIAL_YEAR -> DateUtils.financialYearRange(referenceDate)
                ReportPeriod.YEARLY -> DateUtils.yearRange(referenceDate)
            }

            var expenses: List<Expense> = emptyList()
            expenseRepository.observeByEpochDayRange(start.toEpochDay(), end.toEpochDay()).first().let {
                expenses = it
            }

            val amounts = expenses.map { it.amount }
            _uiState.value = ReportUiState(
                expenses = expenses,
                total = amounts.sum(),
                average = if (amounts.isEmpty()) 0.0 else amounts.average(),
                highest = amounts.maxOrNull() ?: 0.0,
                lowest = amounts.minOrNull() ?: 0.0,
                count = expenses.size
            )
        }
    }

    fun export(format: ExportFormat) {
        viewModelScope.launch {
            try {
                val categories: List<Category> = categoryRepository.observeAllCategories().first()
                val rows = buildExportRows(_uiState.value.expenses, categories)
                val uri = exportManager.exportUri(format, rows)
                _uiState.value = _uiState.value.copy(exportUri = uri, exportError = null)
            } catch (e: Exception) {
                _uiState.value = _uiState.value.copy(exportError = e.message ?: "Export failed")
            }
        }
    }

    fun clearExportError() {
        _uiState.value = _uiState.value.copy(exportError = null)
    }
}
