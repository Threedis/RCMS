package com.threedis.smartexpensemanager.ui.dashboard

import androidx.compose.foundation.layout.*
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.items
import androidx.compose.material3.*
import androidx.compose.runtime.Composable
import androidx.compose.runtime.getValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp
import androidx.hilt.navigation.compose.hiltViewModel
import androidx.lifecycle.compose.collectAsStateWithLifecycle

@Composable
fun DashboardScreen(
    onAddExpense: () -> Unit,
    viewModel: DashboardViewModel = hiltViewModel()
) {
    val state by viewModel.uiState.collectAsStateWithLifecycle()

    LazyColumn(
        modifier = Modifier.fillMaxSize().padding(16.dp),
        verticalArrangement = Arrangement.spacedBy(12.dp)
    ) {
        item { Text("Dashboard", style = MaterialTheme.typography.titleLarge) }

        item {
            Row(horizontalArrangement = Arrangement.spacedBy(8.dp)) {
                SummaryCard("Today", state.todayTotal, Modifier.weight(1f))
                SummaryCard("This Week", state.weekTotal, Modifier.weight(1f))
            }
        }
        item {
            Row(horizontalArrangement = Arrangement.spacedBy(8.dp)) {
                SummaryCard("This Month", state.monthTotal, Modifier.weight(1f))
                SummaryCard("Financial Year", state.financialYearTotal, Modifier.weight(1f))
            }
        }

        item {
            Card(modifier = Modifier.fillMaxWidth()) {
                Column(Modifier.padding(16.dp)) {
                    Text("Monthly Budget", style = MaterialTheme.typography.titleMedium)
                    Spacer(Modifier.height(8.dp))
                    LinearProgressIndicator(
                        progress = { (state.monthlyBudgetUsedPercent / 100.0).toFloat().coerceIn(0f, 1f) },
                        modifier = Modifier.fillMaxWidth()
                    )
                    Spacer(Modifier.height(8.dp))
                    Text("${"%.1f".format(state.monthlyBudgetUsedPercent)}% used · Remaining: ${"%.2f".format(state.remainingBudget)}")
                    Text("Entries this month: ${state.entryCount}")
                }
            }
        }

        item {
            Card(modifier = Modifier.fillMaxWidth()) {
                Column(Modifier.padding(16.dp)) {
                    Text("Expenses by Category", style = MaterialTheme.typography.titleMedium)
                    Spacer(Modifier.height(8.dp))
                    if (state.categoryBreakdown.isEmpty()) {
                        Text("No expenses yet this month", style = MaterialTheme.typography.bodyMedium)
                    } else {
                        state.categoryBreakdown.sortedByDescending { it.amount }.forEach { slice ->
                            Row(
                                modifier = Modifier.fillMaxWidth().padding(vertical = 4.dp),
                                horizontalArrangement = Arrangement.SpaceBetween
                            ) {
                                Text(slice.category?.name ?: "Uncategorized")
                                Text("%.2f".format(slice.amount), fontWeight = FontWeight.Medium)
                            }
                        }
                    }
                }
            }
        }

        item {
            Text("Recent Transactions", style = MaterialTheme.typography.titleMedium)
        }
        if (state.recentTransactions.isEmpty()) {
            item { EmptyState("No transactions yet. Tap + to add your first expense.") }
        } else {
            items(state.recentTransactions) { expense ->
                Card(modifier = Modifier.fillMaxWidth()) {
                    Row(
                        modifier = Modifier.fillMaxWidth().padding(12.dp),
                        horizontalArrangement = Arrangement.SpaceBetween,
                        verticalAlignment = Alignment.CenterVertically
                    ) {
                        Column {
                            Text(expense.description.ifBlank { expense.merchantName.ifBlank { "Expense" } })
                            Text(expense.paymentMethod.label, style = MaterialTheme.typography.labelSmall)
                        }
                        Text("%.2f".format(expense.amount), fontWeight = FontWeight.Bold)
                    }
                }
            }
        }
    }
}

@Composable
private fun SummaryCard(label: String, amount: Double, modifier: Modifier = Modifier) {
    Card(modifier = modifier) {
        Column(Modifier.padding(12.dp)) {
            Text(label, style = MaterialTheme.typography.labelSmall)
            Spacer(Modifier.height(4.dp))
            Text("%.2f".format(amount), style = MaterialTheme.typography.titleMedium, fontWeight = FontWeight.Bold)
        }
    }
}

@Composable
fun EmptyState(message: String) {
    Box(modifier = Modifier.fillMaxWidth().padding(24.dp), contentAlignment = Alignment.Center) {
        Text(message, style = MaterialTheme.typography.bodyMedium)
    }
}
