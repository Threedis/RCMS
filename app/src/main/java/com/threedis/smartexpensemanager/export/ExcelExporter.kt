package com.threedis.smartexpensemanager.export

import java.io.File
import java.time.Instant
import java.time.ZoneId
import javax.inject.Inject
import javax.inject.Singleton

/**
 * Builds the multi-sheet Excel report using [SimpleXlsxWriter] instead of Apache POI.
 *
 * POI's XSSFWorkbook implementation depends on javax.xml/java.awt classes that are absent or
 * incompatible on the Android runtime, so every export using it crashed on-device with a
 * NoClassDefFoundError as soon as the user tapped "Excel" on the Reports screen, despite the
 * project building and its JVM unit tests passing. Writing the OOXML zip by hand avoids that
 * dependency entirely.
 */
@Singleton
class ExcelExporter @Inject constructor() {

    fun export(
        outputFile: File,
        rows: List<ExportRow>,
        categorySummary: List<CategorySummaryRow>,
        budgetSummary: List<BudgetSummaryRow>,
        monthlySummary: List<MonthlySummaryRow>
    ) {
        SimpleXlsxWriter.write(outputFile) {
            sheet("Expense Details") {
                row(
                    str("Date", bold = true), str("Time", bold = true), str("Amount", bold = true),
                    str("Category", bold = true), str("Payment Method", bold = true), str("Description", bold = true),
                    str("Merchant", bold = true), str("Bill No", bold = true), str("Location", bold = true),
                    str("Remarks", bold = true), str("Receipt Path", bold = true)
                )
                var total = 0.0
                for (item in rows) {
                    val e = item.expense
                    val instant = Instant.ofEpochMilli(e.timestampMillis).atZone(ZoneId.systemDefault())
                    row(
                        str(instant.toLocalDate().toString()),
                        str(instant.toLocalTime().toString()),
                        num(e.amount),
                        str(item.categoryName),
                        str(e.paymentMethod.label),
                        str(e.description),
                        str(e.merchantName),
                        str(e.billNumber),
                        str(e.location),
                        str(e.remarks),
                        str(e.receiptPath ?: "")
                    )
                    total += e.amount
                }
                row(str(""), str("Total", bold = true), num(total, bold = true))
            }

            sheet("Category Summary") {
                row(str("Category", bold = true), str("Total Amount", bold = true), str("Transaction Count", bold = true))
                for (item in categorySummary) {
                    row(str(item.categoryName), num(item.total), num(item.count.toDouble()))
                }
            }

            sheet("Budget Summary") {
                row(str("Budget", bold = true), str("Approved", bold = true), str("Actual Spend", bold = true), str("Variance", bold = true))
                for (item in budgetSummary) {
                    row(str(item.label), num(item.budget), num(item.actual), num(item.budget - item.actual))
                }
            }

            sheet("Monthly Summary") {
                row(str("Month", bold = true), str("Total Amount", bold = true))
                for (item in monthlySummary) {
                    row(str(item.month), num(item.total))
                }
            }
        }
    }
}
