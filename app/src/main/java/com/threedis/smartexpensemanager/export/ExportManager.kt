package com.threedis.smartexpensemanager.export

import android.content.Context
import androidx.core.content.FileProvider
import dagger.hilt.android.qualifiers.ApplicationContext
import java.io.File
import java.time.Instant
import java.time.ZoneId
import javax.inject.Inject
import javax.inject.Singleton

enum class ExportFormat { EXCEL, CSV, PDF }

@Singleton
class ExportManager @Inject constructor(
    @ApplicationContext private val context: Context,
    private val excelExporter: ExcelExporter,
    private val csvExporter: CsvExporter,
    private val pdfExporter: PdfExporter
) {
    fun exportUri(
        format: ExportFormat,
        rows: List<ExportRow>,
        title: String = "Expense Report"
    ): android.net.Uri {
        val exportsDir = File(context.getExternalFilesDir(null), "exports").apply { mkdirs() }
        val timestamp = System.currentTimeMillis()
        val file = when (format) {
            ExportFormat.EXCEL -> {
                val f = File(exportsDir, "expense_report_$timestamp.xlsx")
                excelExporter.export(f, rows, categorySummary(rows), emptyList(), monthlySummary(rows))
                f
            }
            ExportFormat.CSV -> {
                val f = File(exportsDir, "expense_report_$timestamp.csv")
                csvExporter.export(f, rows)
                f
            }
            ExportFormat.PDF -> {
                val f = File(exportsDir, "expense_report_$timestamp.pdf")
                pdfExporter.export(f, title, rows)
                f
            }
        }
        return FileProvider.getUriForFile(context, "${context.packageName}.fileprovider", file)
    }

    private fun categorySummary(rows: List<ExportRow>): List<CategorySummaryRow> =
        rows.groupBy { it.categoryName }
            .map { (name, items) -> CategorySummaryRow(name, items.sumOf { it.expense.amount }, items.size) }
            .sortedByDescending { it.total }

    private fun monthlySummary(rows: List<ExportRow>): List<MonthlySummaryRow> =
        rows.groupBy {
            val d = Instant.ofEpochMilli(it.expense.timestampMillis).atZone(ZoneId.systemDefault()).toLocalDate()
            "${d.year}-${d.monthValue.toString().padStart(2, '0')}"
        }.map { (month, items) -> MonthlySummaryRow(month, items.sumOf { it.expense.amount }) }
            .sortedBy { it.month }

    fun backupDatabase(): File {
        val dbFile = context.getDatabasePath("smart_expense_manager.db")
        val backupsDir = File(context.getExternalFilesDir(null), "backups").apply { mkdirs() }
        val backupFile = File(backupsDir, "backup_${System.currentTimeMillis()}.db")
        dbFile.copyTo(backupFile, overwrite = true)
        return backupFile
    }

    fun restoreDatabase(backupFile: File) {
        val dbFile = context.getDatabasePath("smart_expense_manager.db")
        backupFile.copyTo(dbFile, overwrite = true)
    }
}
