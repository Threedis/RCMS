package com.threedis.smartexpensemanager.ui.expense

import androidx.lifecycle.ViewModel
import androidx.lifecycle.viewModelScope
import com.threedis.smartexpensemanager.data.local.entity.Category
import com.threedis.smartexpensemanager.data.local.entity.Expense
import com.threedis.smartexpensemanager.data.local.entity.PaymentMethod
import com.threedis.smartexpensemanager.data.repository.CategoryRepository
import com.threedis.smartexpensemanager.data.repository.ExpenseRepository
import com.threedis.smartexpensemanager.notification.BudgetAlertManager
import dagger.hilt.android.lifecycle.HiltViewModel
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.flow.stateIn
import kotlinx.coroutines.launch
import java.time.LocalDate
import java.time.LocalTime
import javax.inject.Inject

data class ExpenseEntryForm(
    val amountText: String = "",
    val categoryId: Long? = null,
    val description: String = "",
    val date: LocalDate = LocalDate.now(),
    val time: LocalTime = LocalTime.now(),
    val location: String = "",
    val latitude: Double? = null,
    val longitude: Double? = null,
    val paymentMethod: PaymentMethod = PaymentMethod.CASH,
    val merchantName: String = "",
    val billNumber: String = "",
    val remarks: String = "",
    val receiptPath: String? = null,
    val receiptThumbnailPath: String? = null
)

sealed interface SaveResult {
    data object Idle : SaveResult
    data object Saving : SaveResult
    data object Success : SaveResult
    data class ValidationError(val message: String) : SaveResult
}

@HiltViewModel
class ExpenseEntryViewModel @Inject constructor(
    private val expenseRepository: ExpenseRepository,
    private val categoryRepository: CategoryRepository,
    private val budgetAlertManager: BudgetAlertManager
) : ViewModel() {

    private val _form = MutableStateFlow(ExpenseEntryForm())
    val form: StateFlow<ExpenseEntryForm> = _form.asStateFlow()

    private val _saveResult = MutableStateFlow<SaveResult>(SaveResult.Idle)
    val saveResult: StateFlow<SaveResult> = _saveResult.asStateFlow()

    val categories: StateFlow<List<Category>> = categoryRepository.observeActiveCategories()
        .stateIn(viewModelScope, kotlinx.coroutines.flow.SharingStarted.Eagerly, emptyList())

    fun updateForm(transform: (ExpenseEntryForm) -> ExpenseEntryForm) {
        _form.value = transform(_form.value)
    }

    fun save() {
        val current = _form.value
        val amount = current.amountText.toDoubleOrNull()

        if (amount == null || amount <= 0.0) {
            _saveResult.value = SaveResult.ValidationError("Amount must be greater than zero")
            return
        }
        if (current.categoryId == null) {
            _saveResult.value = SaveResult.ValidationError("Please select a category")
            return
        }

        viewModelScope.launch {
            _saveResult.value = SaveResult.Saving
            val timestamp = current.date.atTime(current.time)
                .atZone(java.time.ZoneId.systemDefault()).toInstant().toEpochMilli()

            val expense = Expense(
                amount = amount,
                categoryId = current.categoryId,
                description = current.description.trim(),
                epochDay = current.date.toEpochDay(),
                timeOfDayMillis = current.time.toSecondOfDay() * 1000L,
                timestampMillis = timestamp,
                location = current.location.trim(),
                latitude = current.latitude,
                longitude = current.longitude,
                paymentMethod = current.paymentMethod,
                merchantName = current.merchantName.trim(),
                billNumber = current.billNumber.trim(),
                remarks = current.remarks.trim(),
                receiptPath = current.receiptPath,
                receiptThumbnailPath = current.receiptThumbnailPath
            )
            expenseRepository.add(expense)

            val category = current.categoryId?.let { categoryRepository.getById(it) }
            budgetAlertManager.evaluateAndNotify(current.categoryId, category?.name)

            _saveResult.value = SaveResult.Success
            _form.value = ExpenseEntryForm()
        }
    }

    fun resetSaveResult() {
        _saveResult.value = SaveResult.Idle
    }
}
