package com.threedis.smartexpensemanager.ui.reports

import android.content.Intent
import androidx.compose.foundation.layout.*
import androidx.compose.material3.*
import androidx.compose.runtime.*
import androidx.compose.ui.Modifier
import androidx.compose.ui.platform.LocalContext
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
    val context = LocalContext.current
    val snackbarHostState = remember { SnackbarHostState() }

    LaunchedEffect(period) { viewModel.loadReport(period) }

    LaunchedEffect(state.exportUri) {
        val uri = state.exportUri ?: return@LaunchedEffect
        val mimeType = state.exportMimeType ?: "*/*"
        try {
            val viewIntent = Intent(Intent.ACTION_VIEW).apply {
                setDataAndType(uri, mimeType)
                addFlags(Intent.FLAG_GRANT_READ_URI_PERMISSION)
                addFlags(Intent.FLAG_ACTIVITY_NEW_TASK)
            }
            context.startActivity(viewIntent)
            snackbarHostState.showSnackbar("Report exported and opened")
        } catch (e: android.content.ActivityNotFoundException) {
            snackbarHostState.showSnackbar("Report exported. No app found to open it — use Share instead.")
        }
    }
    LaunchedEffect(state.exportError) {
        state.exportError?.let { message ->
            snackbarHostState.showSnackbar("Export failed: $message")
            viewModel.clearExportError()
        }
    }

    Scaffold(snackbarHost = { SnackbarHost(snackbarHostState) }) { padding ->
        Column(
            modifier = Modifier.fillMaxSize().padding(padding).padding(16.dp),
            verticalArrangement = Arrangement.spacedBy(12.dp)
        ) {
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

            if (state.count == 0) {
                Text(
                    "No expenses found for this period — export will produce an empty report.",
                    style = MaterialTheme.typography.bodySmall,
                    color = MaterialTheme.colorScheme.error
                )
            }

            Text("Export", style = MaterialTheme.typography.titleMedium)
            Row(horizontalArrangement = Arrangement.spacedBy(8.dp)) {
                Button(onClick = { viewModel.export(ExportFormat.EXCEL) }) { Text("Excel") }
                Button(onClick = { viewModel.export(ExportFormat.CSV) }) { Text("CSV") }
                Button(onClick = { viewModel.export(ExportFormat.PDF) }) { Text("PDF") }
            }

            state.exportUri?.let { uri ->
                OutlinedButton(onClick = {
                    val shareIntent = Intent(Intent.ACTION_SEND).apply {
                        type = "*/*"
                        putExtra(Intent.EXTRA_STREAM, uri)
                        addFlags(Intent.FLAG_GRANT_READ_URI_PERMISSION)
                    }
                    context.startActivity(Intent.createChooser(shareIntent, "Share report"))
                }) {
                    Text("Share Last Export")
                }
            }
        }
    }
}
