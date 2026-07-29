package com.threedis.smartexpensemanager.ui.expense

import androidx.compose.foundation.layout.*
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.verticalScroll
import androidx.compose.material3.*
import androidx.compose.runtime.*
import androidx.compose.ui.Modifier
import androidx.compose.ui.unit.dp
import androidx.hilt.navigation.compose.hiltViewModel
import androidx.lifecycle.compose.collectAsStateWithLifecycle
import com.threedis.smartexpensemanager.data.local.entity.PaymentMethod

@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun ExpenseEntryScreen(
    onDone: () -> Unit,
    viewModel: ExpenseEntryViewModel = hiltViewModel()
) {
    val form by viewModel.form.collectAsStateWithLifecycle()
    val categories by viewModel.categories.collectAsStateWithLifecycle()
    val saveResult by viewModel.saveResult.collectAsStateWithLifecycle()

    var categoryMenuExpanded by remember { mutableStateOf(false) }
    var paymentMenuExpanded by remember { mutableStateOf(false) }

    LaunchedEffect(saveResult) {
        if (saveResult is SaveResult.Success) {
            viewModel.resetSaveResult()
            onDone()
        }
    }

    Column(
        modifier = Modifier.fillMaxSize().verticalScroll(rememberScrollState()).padding(16.dp),
        verticalArrangement = Arrangement.spacedBy(12.dp)
    ) {
        Text("Add Expense", style = MaterialTheme.typography.titleLarge)

        OutlinedTextField(
            value = form.amountText,
            onValueChange = { value -> viewModel.updateForm { it.copy(amountText = value) } },
            label = { Text("Amount *") },
            modifier = Modifier.fillMaxWidth()
        )

        ExposedDropdownMenuBox(expanded = categoryMenuExpanded, onExpandedChange = { categoryMenuExpanded = it }) {
            OutlinedTextField(
                value = categories.firstOrNull { it.id == form.categoryId }?.name ?: "",
                onValueChange = {},
                readOnly = true,
                label = { Text("Category *") },
                modifier = Modifier.menuAnchor().fillMaxWidth()
            )
            ExposedDropdownMenu(expanded = categoryMenuExpanded, onDismissRequest = { categoryMenuExpanded = false }) {
                categories.forEach { category ->
                    DropdownMenuItem(
                        text = { Text(category.name) },
                        onClick = {
                            viewModel.updateForm { it.copy(categoryId = category.id) }
                            categoryMenuExpanded = false
                        }
                    )
                }
            }
        }

        OutlinedTextField(
            value = form.description,
            onValueChange = { value -> viewModel.updateForm { it.copy(description = value) } },
            label = { Text("Description") },
            modifier = Modifier.fillMaxWidth()
        )

        ExposedDropdownMenuBox(expanded = paymentMenuExpanded, onExpandedChange = { paymentMenuExpanded = it }) {
            OutlinedTextField(
                value = form.paymentMethod.label,
                onValueChange = {},
                readOnly = true,
                label = { Text("Payment Method") },
                modifier = Modifier.menuAnchor().fillMaxWidth()
            )
            ExposedDropdownMenu(expanded = paymentMenuExpanded, onDismissRequest = { paymentMenuExpanded = false }) {
                PaymentMethod.entries.forEach { method ->
                    DropdownMenuItem(
                        text = { Text(method.label) },
                        onClick = {
                            viewModel.updateForm { it.copy(paymentMethod = method) }
                            paymentMenuExpanded = false
                        }
                    )
                }
            }
        }

        OutlinedTextField(
            value = form.merchantName,
            onValueChange = { value -> viewModel.updateForm { it.copy(merchantName = value) } },
            label = { Text("Merchant Name") },
            modifier = Modifier.fillMaxWidth()
        )

        OutlinedTextField(
            value = form.billNumber,
            onValueChange = { value -> viewModel.updateForm { it.copy(billNumber = value) } },
            label = { Text("Bill Number") },
            modifier = Modifier.fillMaxWidth()
        )

        OutlinedTextField(
            value = form.location,
            onValueChange = { value -> viewModel.updateForm { it.copy(location = value) } },
            label = { Text("Location") },
            modifier = Modifier.fillMaxWidth()
        )

        OutlinedTextField(
            value = form.remarks,
            onValueChange = { value -> viewModel.updateForm { it.copy(remarks = value) } },
            label = { Text("Remarks") },
            modifier = Modifier.fillMaxWidth()
        )

        if (saveResult is SaveResult.ValidationError) {
            Text(
                (saveResult as SaveResult.ValidationError).message,
                color = MaterialTheme.colorScheme.error
            )
        }

        Button(
            onClick = { viewModel.save() },
            enabled = saveResult !is SaveResult.Saving,
            modifier = Modifier.fillMaxWidth()
        ) {
            Text(if (saveResult is SaveResult.Saving) "Saving..." else "Save Expense")
        }
    }
}
