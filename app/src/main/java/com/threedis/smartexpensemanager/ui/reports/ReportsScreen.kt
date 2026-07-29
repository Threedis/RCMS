package com.threedis.smartexpensemanager.ui.reports

import androidx.compose.foundation.layout.*
import androidx.compose.material3.*
import androidx.compose.runtime.*
import androidx.compose.ui.Modifier
import androidx.compose.ui.unit.dp
import androidx.hilt.navigation.compose.hiltViewModel
import androidx.lifecycle.compose.collectAsStateWithLifecycle
import com.threedis.smartexpensemanager.export.ExportFormat

@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun ReportsScreen(viewModel: ReportsViewModel = hiltViewModel()) {
    val state by viewModel.uiState.collectAsStateWithLifecycle()
    var period by remember { mutableStateOf(ReportPeriod.MONTHLY) }
    var periodMenuExpanded by remember { mutableStateOf(false) }

    LaunchedEffect(period) { viewModel.loadReport(period) }

    Column(modifier = Modifier.fillMaxSize().padding(16.dp), verticalArrangement = Arrangement.spacedBy(12.dp)) {
        Text("Reports", style = MaterialTheme.typography.titleLarge)

        Box {
            OutlinedButton(onClick = { periodMenuExpanded = true }) { Text(period.name.replace('_', ' ')) }
            DropdownMenu(expanded = periodMenuExpanded, onDismissRequest = { periodMenuExpanded = false }) {
                ReportPeriod.entries.forEach { p ->
                    DropdownMenuItem(text = { Text(p.name.replace('_', ' ')) }, onClick = { period = p; periodMenuExpanded = false })
                }
            }
        }

        Card(modifier = Modifier.fillMaxWidth()) {
            Column(Modifier.padding(16.dp), verticalArrangement = Arrangement.spacedBy(4.dp)) {
                Text("Total: ${"%.2f".format(state.total)}")
                Text("Average: ${"%.2f".format(state.average)}")
                Text("Highest: ${"%.2f".format(state.highest)}")
                Text("Lowest: ${"%.2f".format(state.lowest)}")
                Text("Transactions: ${state.count}")
            }
        }

        Text("Export", style = MaterialTheme.typography.titleMedium)
        Row(horizontalArrangement = Arrangement.spacedBy(8.dp)) {
            Button(onClick = { viewModel.export(ExportFormat.EXCEL) }) { Text("Excel") }
            Button(onClick = { viewModel.export(ExportFormat.CSV) }) { Text("CSV") }
            Button(onClick = { viewModel.export(ExportFormat.PDF) }) { Text("PDF") }
        }

        state.exportUri?.let {
            Text("Exported: $it", style = MaterialTheme.typography.labelSmall)
        }
    }
}
