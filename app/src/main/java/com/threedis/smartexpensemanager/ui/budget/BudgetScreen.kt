package com.threedis.smartexpensemanager.ui.budget

import androidx.compose.foundation.layout.*
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.items
import androidx.compose.material3.*
import androidx.compose.runtime.*
import androidx.compose.ui.Modifier
import androidx.compose.ui.unit.dp
import androidx.hilt.navigation.compose.hiltViewModel
import androidx.lifecycle.compose.collectAsStateWithLifecycle
import com.threedis.smartexpensemanager.data.local.entity.BudgetPeriod
import com.threedis.smartexpensemanager.ui.dashboard.EmptyState

@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun BudgetScreen(viewModel: BudgetViewModel = hiltViewModel()) {
    val state by viewModel.uiState.collectAsStateWithLifecycle()
    var showAddDialog by remember { mutableStateOf(false) }

    Column(modifier = Modifier.fillMaxSize().padding(16.dp)) {
        Row(
            modifier = Modifier.fillMaxWidth(),
            horizontalArrangement = Arrangement.SpaceBetween
        ) {
            Text("Budgets", style = MaterialTheme.typography.titleLarge)
            Button(onClick = { showAddDialog = true }) { Text("Add Budget") }
        }
        Spacer(Modifier.height(12.dp))

        if (state.statuses.isEmpty()) {
            EmptyState("No budgets configured yet")
        } else {
            LazyColumn(verticalArrangement = Arrangement.spacedBy(8.dp)) {
                items(state.statuses) { status ->
                    val categoryName = status.categoryId?.let { id -> state.categories.firstOrNull { it.id == id }?.name }
                    Card(modifier = Modifier.fillMaxWidth()) {
                        Column(Modifier.padding(12.dp)) {
                            Text("${categoryName ?: "Overall"} · ${status.period.name}")
                            Spacer(Modifier.height(4.dp))
                            LinearProgressIndicator(
                                progress = { (status.utilizationPercent / 100.0).toFloat().coerceIn(0f, 1f) },
                                modifier = Modifier.fillMaxWidth()
                            )
                            Spacer(Modifier.height(4.dp))
                            Text("${"%.2f".format(status.spentAmount)} of ${"%.2f".format(status.budgetAmount)} (${"%.0f".format(status.utilizationPercent)}%)")
                        }
                    }
                }
            }
        }
    }

    if (showAddDialog) {
        AddBudgetDialog(
            categories = state.categories,
            onDismiss = { showAddDialog = false },
            onConfirm = { period, amount, categoryId ->
                viewModel.saveBudget(period, amount, categoryId)
                showAddDialog = false
            }
        )
    }
}

@Composable
private fun AddBudgetDialog(
    categories: List<com.threedis.smartexpensemanager.data.local.entity.Category>,
    onDismiss: () -> Unit,
    onConfirm: (BudgetPeriod, Double, Long?) -> Unit
) {
    var amountText by remember { mutableStateOf("") }
    var period by remember { mutableStateOf(BudgetPeriod.MONTHLY) }
    var categoryId by remember { mutableStateOf<Long?>(null) }
    var periodMenuExpanded by remember { mutableStateOf(false) }
    var categoryMenuExpanded by remember { mutableStateOf(false) }

    AlertDialog(
        onDismissRequest = onDismiss,
        title = { Text("Add Budget") },
        text = {
            Column(verticalArrangement = Arrangement.spacedBy(8.dp)) {
                OutlinedTextField(value = amountText, onValueChange = { amountText = it }, label = { Text("Amount") })

                Box {
                    OutlinedButton(onClick = { periodMenuExpanded = true }) { Text(period.name) }
                    DropdownMenu(expanded = periodMenuExpanded, onDismissRequest = { periodMenuExpanded = false }) {
                        BudgetPeriod.entries.forEach { p ->
                            DropdownMenuItem(text = { Text(p.name) }, onClick = { period = p; periodMenuExpanded = false })
                        }
                    }
                }

                Box {
                    OutlinedButton(onClick = { categoryMenuExpanded = true }) {
                        Text(categories.firstOrNull { it.id == categoryId }?.name ?: "Overall (All Categories)")
                    }
                    DropdownMenu(expanded = categoryMenuExpanded, onDismissRequest = { categoryMenuExpanded = false }) {
                        DropdownMenuItem(text = { Text("Overall (All Categories)") }, onClick = { categoryId = null; categoryMenuExpanded = false })
                        categories.forEach { category ->
                            DropdownMenuItem(text = { Text(category.name) }, onClick = { categoryId = category.id; categoryMenuExpanded = false })
                        }
                    }
                }
            }
        },
        confirmButton = {
            TextButton(onClick = {
                amountText.toDoubleOrNull()?.let { onConfirm(period, it, categoryId) }
            }) { Text("Save") }
        },
        dismissButton = { TextButton(onClick = onDismiss) { Text("Cancel") } }
    )
}
