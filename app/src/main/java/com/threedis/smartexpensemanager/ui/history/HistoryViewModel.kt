package com.threedis.smartexpensemanager.ui.history

import androidx.lifecycle.ViewModel
import androidx.lifecycle.viewModelScope
import com.threedis.smartexpensemanager.data.local.entity.Category
import com.threedis.smartexpensemanager.data.local.entity.Expense
import com.threedis.smartexpensemanager.data.repository.CategoryRepository
import com.threedis.smartexpensemanager.data.repository.ExpenseFilter
import com.threedis.smartexpensemanager.data.repository.ExpenseRepository
import dagger.hilt.android.lifecycle.HiltViewModel
import kotlinx.coroutines.ExperimentalCoroutinesApi
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.SharingStarted
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.flow.combine
import kotlinx.coroutines.flow.flatMapLatest
import kotlinx.coroutines.flow.stateIn
import javax.inject.Inject

data class HistoryUiState(
    val expenses: List<Expense> = emptyList(),
    val categories: List<Category> = emptyList()
)

@OptIn(ExperimentalCoroutinesApi::class)
@HiltViewModel
class HistoryViewModel @Inject constructor(
    private val expenseRepository: ExpenseRepository,
    categoryRepository: CategoryRepository
) : ViewModel() {

    private val _filter = MutableStateFlow(ExpenseFilter())
    val filter: StateFlow<ExpenseFilter> = _filter.asStateFlow()

    private val expenses = _filter.flatMapLatest { expenseRepository.filter(it) }
    private val categories = categoryRepository.observeActiveCategories()

    val uiState: StateFlow<HistoryUiState> = combine(expenses, categories) { e, c ->
        HistoryUiState(e, c)
    }.stateIn(viewModelScope, SharingStarted.WhileSubscribed(5000), HistoryUiState())

    fun updateFilter(transform: (ExpenseFilter) -> ExpenseFilter) {
        _filter.value = transform(_filter.value)
    }
}
