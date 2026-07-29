package com.threedis.smartexpensemanager.export

import org.apache.poi.ss.usermodel.*
import org.apache.poi.xssf.usermodel.XSSFWorkbook
import java.io.File
import java.io.FileOutputStream
import java.time.Instant
import java.time.ZoneId
import java.time.format.DateTimeFormatter
import javax.inject.Inject
import javax.inject.Singleton

@Singleton
class ExcelExporter @Inject constructor() {

    private val dateFmt = DateTimeFormatter.ofPattern("dd/MM/yyyy HH:mm")

    fun export(
        outputFile: File,
        rows: List<ExportRow>,
        categorySummary: List<CategorySummaryRow>,
        budgetSummary: List<BudgetSummaryRow>,
        monthlySummary: List<MonthlySummaryRow>
    ) {
        XSSFWorkbook().use { workbook ->
            val headerStyle = headerStyle(workbook)
            writeExpenseDetails(workbook, rows, headerStyle)
            writeCategorySummary(workbook, categorySummary, headerStyle)
            writeBudgetSummary(workbook, budgetSummary, headerStyle)
            writeMonthlySummary(workbook, monthlySummary, headerStyle)

            FileOutputStream(outputFile).use { out -> workbook.write(out) }
        }
    }

    private fun headerStyle(workbook: Workbook): CellStyle {
        val style = workbook.createCellStyle()
        val font = workbook.createFont()
        font.bold = true
        font.color = IndexedColors.WHITE.index
        style.setFont(font)
        style.fillForegroundColor = IndexedColors.DARK_TEAL.index
        style.fillPattern = FillPatternType.SOLID_FOREGROUND
        return style
    }

    private fun Row.headerCell(index: Int, text: String, style: CellStyle) {
        createCell(index).apply {
            setCellValue(text)
            cellStyle = style
        }
    }

    private fun writeExpenseDetails(workbook: Workbook, rows: List<ExportRow>, headerStyle: CellStyle) {
        val sheet = workbook.createSheet("Expense Details")
        val headers = listOf(
            "Date", "Time", "Amount", "Category", "Payment Method", "Description",
            "Merchant", "Bill No", "Location", "Remarks", "Receipt Path"
        )
        val headerRow = sheet.createRow(0)
        headers.forEachIndexed { i, h -> headerRow.headerCell(i, h, headerStyle) }

        var rowIndex = 1
        var total = 0.0
        for (item in rows) {
            val e = item.expense
            val instant = Instant.ofEpochMilli(e.timestampMillis).atZone(ZoneId.systemDefault())
            val row = sheet.createRow(rowIndex++)
            row.createCell(0).setCellValue(instant.toLocalDate().toString())
            row.createCell(1).setCellValue(instant.toLocalTime().toString())
            row.createCell(2).setCellValue(e.amount)
            row.createCell(3).setCellValue(item.categoryName)
            row.createCell(4).setCellValue(e.paymentMethod.label)
            row.createCell(5).setCellValue(e.description)
            row.createCell(6).setCellValue(e.merchantName)
            row.createCell(7).setCellValue(e.billNumber)
            row.createCell(8).setCellValue(e.location)
            row.createCell(9).setCellValue(e.remarks)
            row.createCell(10).setCellValue(e.receiptPath ?: "")
            total += e.amount
        }

        val totalRow = sheet.createRow(rowIndex)
        totalRow.createCell(1).setCellValue("Total")
        totalRow.createCell(2).setCellValue(total)

        for (i in headers.indices) sheet.autoSizeColumn(i)
    }

    private fun writeCategorySummary(workbook: Workbook, summary: List<CategorySummaryRow>, headerStyle: CellStyle) {
        val sheet = workbook.createSheet("Category Summary")
        val headerRow = sheet.createRow(0)
        listOf("Category", "Total Amount", "Transaction Count").forEachIndexed { i, h -> headerRow.headerCell(i, h, headerStyle) }
        summary.forEachIndexed { idx, row ->
            val r = sheet.createRow(idx + 1)
            r.createCell(0).setCellValue(row.categoryName)
            r.createCell(1).setCellValue(row.total)
            r.createCell(2).setCellValue(row.count.toDouble())
        }
        for (i in 0..2) sheet.autoSizeColumn(i)
    }

    private fun writeBudgetSummary(workbook: Workbook, summary: List<BudgetSummaryRow>, headerStyle: CellStyle) {
        val sheet = workbook.createSheet("Budget Summary")
        val headerRow = sheet.createRow(0)
        listOf("Budget", "Approved", "Actual Spend", "Variance").forEachIndexed { i, h -> headerRow.headerCell(i, h, headerStyle) }
        summary.forEachIndexed { idx, row ->
            val r = sheet.createRow(idx + 1)
            r.createCell(0).setCellValue(row.label)
            r.createCell(1).setCellValue(row.budget)
            r.createCell(2).setCellValue(row.actual)
            r.createCell(3).setCellValue(row.budget - row.actual)
        }
        for (i in 0..3) sheet.autoSizeColumn(i)
    }

    private fun writeMonthlySummary(workbook: Workbook, summary: List<MonthlySummaryRow>, headerStyle: CellStyle) {
        val sheet = workbook.createSheet("Monthly Summary")
        val headerRow = sheet.createRow(0)
        listOf("Month", "Total Amount").forEachIndexed { i, h -> headerRow.headerCell(i, h, headerStyle) }
        summary.forEachIndexed { idx, row ->
            val r = sheet.createRow(idx + 1)
            r.createCell(0).setCellValue(row.month)
            r.createCell(1).setCellValue(row.total)
        }
        for (i in 0..1) sheet.autoSizeColumn(i)
    }
}
