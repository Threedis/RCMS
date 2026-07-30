package com.threedis.smartexpensemanager.export

import java.io.File
import java.time.Instant
import java.time.ZoneId
import javax.inject.Inject
import javax.inject.Singleton

@Singleton
class CsvExporter @Inject constructor() {

    fun export(outputFile: File, rows: List<ExportRow>) {
        outputFile.bufferedWriter().use { writer ->
            writer.appendLine(
                listOf(
                    "Date", "Time", "Amount", "Category", "Payment Method", "Description",
                    "Merchant", "Bill No", "Location", "Remarks", "Receipt Path"
                ).joinToString(",")
            )
            for (item in rows) {
                val e = item.expense
                val instant = Instant.ofEpochMilli(e.timestampMillis).atZone(ZoneId.systemDefault())
                val values = listOf(
                    instant.toLocalDate().toString(),
                    instant.toLocalTime().toString(),
                    e.amount.toString(),
                    item.categoryName,
                    e.paymentMethod.label,
                    e.description,
                    e.merchantName,
                    e.billNumber,
                    e.location,
                    e.remarks,
                    e.receiptPath ?: ""
                ).map { escape(it) }
                writer.appendLine(values.joinToString(","))
            }
        }
    }

    private fun escape(value: String): String {
        return if (value.contains(",") || value.contains("\"") || value.contains("\n")) {
            "\"" + value.replace("\"", "\"\"") + "\""
        } else value
    }
}
