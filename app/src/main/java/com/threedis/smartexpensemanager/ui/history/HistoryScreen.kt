package com.threedis.smartexpensemanager.ui.history

import androidx.compose.foundation.layout.*
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.items
import androidx.compose.material3.*
import androidx.compose.runtime.Composable
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.compose.ui.Modifier
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp
import androidx.hilt.navigation.compose.hiltViewModel
import androidx.lifecycle.compose.collectAsStateWithLifecycle
import com.threedis.smartexpensemanager.data.repository.SortOrder
import com.threedis.smartexpensemanager.ui.dashboard.EmptyState

@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun HistoryScreen(viewModel: HistoryViewModel = hiltViewModel()) {
    val state by viewModel.uiState.collectAsStateWithLifecycle()
    val filter by viewModel.filter.collectAsStateWithLifecycle()

    Column(modifier = Modifier.fillMaxSize().padding(16.dp)) {
        Text("Expense History", style = MaterialTheme.typography.titleLarge)
        Spacer(Modifier.height(8.dp))

        OutlinedTextField(
            value = filter.searchText ?: "",
            onValueChange = { text -> viewModel.updateFilter { it.copy(searchText = text) } },
            label = { Text("Search description, merchant, location...") },
            modifier = Modifier.fillMaxWidth()
        )
        Spacer(Modifier.height(8.dp))

        var sortMenuExpanded by remember { mutableStateOf(false) }
        Box {
            OutlinedButton(onClick = { sortMenuExpanded = true }) {
                Text("Sort: ${filter.sortOrder.name.replace('_', ' ')}")
            }
            DropdownMenu(expanded = sortMenuExpanded, onDismissRequest = { sortMenuExpanded = false }) {
                SortOrder.entries.forEach { order ->
                    DropdownMenuItem(
                        text = { Text(order.name.replace('_', ' ')) },
                        onClick = {
                            viewModel.updateFilter { it.copy(sortOrder = order) }
                            sortMenuExpanded = false
                        }
                    )
                }
            }
        }
        Spacer(Modifier.height(8.dp))

        if (state.expenses.isEmpty()) {
            EmptyState("No expenses match your filters")
        } else {
            LazyColumn(verticalArrangement = Arrangement.spacedBy(8.dp)) {
                items(state.expenses) { expense ->
                    val categoryName = state.categories.firstOrNull { it.id == expense.categoryId }?.name ?: "Uncategorized"
                    Card(modifier = Modifier.fillMaxWidth()) {
                        Column(Modifier.padding(12.dp)) {
                            Row(
                                modifier = Modifier.fillMaxWidth(),
                                horizontalArrangement = Arrangement.SpaceBetween
                            ) {
                                Text(categoryName, fontWeight = FontWeight.Medium)
                                Text("%.2f".format(expense.amount), fontWeight = FontWeight.Bold)
                            }
                            if (expense.description.isNotBlank()) Text(expense.description)
                            Text(expense.paymentMethod.label, style = MaterialTheme.typography.labelSmall)
                        }
                    }
                }
            }
        }
    }
}
