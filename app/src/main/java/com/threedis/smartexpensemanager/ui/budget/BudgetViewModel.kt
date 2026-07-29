package com.threedis.smartexpensemanager.ui.budget

import androidx.lifecycle.ViewModel
import androidx.lifecycle.viewModelScope
import com.threedis.smartexpensemanager.data.local.entity.Budget
import com.threedis.smartexpensemanager.data.local.entity.BudgetPeriod
import com.threedis.smartexpensemanager.data.local.entity.Category
import com.threedis.smartexpensemanager.data.repository.BudgetRepository
import com.threedis.smartexpensemanager.data.repository.BudgetStatus
import com.threedis.smartexpensemanager.data.repository.CategoryRepository
import dagger.hilt.android.lifecycle.HiltViewModel
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.launch
import javax.inject.Inject

data class BudgetUiState(
    val budgets: List<Budget> = emptyList(),
    val categories: List<Category> = emptyList(),
    val statuses: List<BudgetStatus> = emptyList()
)

@HiltViewModel
class BudgetViewModel @Inject constructor(
    private val budgetRepository: BudgetRepository,
    private val categoryRepository: CategoryRepository
) : ViewModel() {

    private val _uiState = MutableStateFlow(BudgetUiState())
    val uiState: StateFlow<BudgetUiState> = _uiState.asStateFlow()

    init {
        viewModelScope.launch {
            budgetRepository.observeActiveBudgets().collect { budgets ->
                val categories = categoryRepository.observeActiveCategories()
                val statuses = mutableListOf<BudgetStatus>()
                for (budget in budgets) {
                    val status = if (budget.categoryId != null) {
                        budgetRepository.categoryStatus(budget.categoryId, budget.period)
                    } else {
                        budgetRepository.overallStatus(budget.period)
                    }
                    status?.let { statuses.add(it) }
                }
                _uiState.value = _uiState.value.copy(budgets = budgets, statuses = statuses)
            }
        }
        viewModelScope.launch {
            categoryRepository.observeActiveCategories().collect { categories ->
                _uiState.value = _uiState.value.copy(categories = categories)
            }
        }
    }

    fun saveBudget(period: BudgetPeriod, amount: Double, categoryId: Long?) {
        viewModelScope.launch {
            budgetRepository.upsert(Budget(period = period, amount = amount, categoryId = categoryId))
        }
    }

    fun deleteBudget(budget: Budget) {
        viewModelScope.launch { budgetRepository.delete(budget) }
    }
}
