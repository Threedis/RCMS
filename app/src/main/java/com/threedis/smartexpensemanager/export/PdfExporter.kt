package com.threedis.smartexpensemanager.export

import android.graphics.Paint
import android.graphics.pdf.PdfDocument
import java.io.File
import java.io.FileOutputStream
import java.time.Instant
import java.time.ZoneId
import javax.inject.Inject
import javax.inject.Singleton

@Singleton
class PdfExporter @Inject constructor() {

    private val pageWidth = 842 // A4 landscape points
    private val pageHeight = 595
    private val margin = 24f
    private val rowHeight = 18f

    fun export(outputFile: File, title: String, rows: List<ExportRow>) {
        val document = PdfDocument()
        val titlePaint = Paint().apply { textSize = 16f; isFakeBoldText = true }
        val headerPaint = Paint().apply { textSize = 10f; isFakeBoldText = true }
        val textPaint = Paint().apply { textSize = 9f }

        val columns = listOf("Date", "Amount", "Category", "Payment", "Description", "Merchant", "Location")
        val columnWidths = listOf(70f, 60f, 100f, 80f, 200f, 120f, 120f)

        var page = document.startPage(PdfDocument.PageInfo.Builder(pageWidth, pageHeight, document.pages.size + 1).create())
        var canvas = page.canvas
        var y = margin

        canvas.drawText(title, margin, y + 16f, titlePaint)
        y += 40f
        y = drawHeaderRow(canvas, columns, columnWidths, y, headerPaint)

        var total = 0.0
        for (item in rows) {
            if (y > pageHeight - margin) {
                document.finishPage(page)
                page = document.startPage(PdfDocument.PageInfo.Builder(pageWidth, pageHeight, document.pages.size + 1).create())
                canvas = page.canvas
                y = margin
                y = drawHeaderRow(canvas, columns, columnWidths, y, headerPaint)
            }
            val e = item.expense
            val date = Instant.ofEpochMilli(e.timestampMillis).atZone(ZoneId.systemDefault()).toLocalDate().toString()
            val values = listOf(
                date, "%.2f".format(e.amount), item.categoryName, e.paymentMethod.label,
                e.description, e.merchantName, e.location
            )
            drawRow(canvas, values, columnWidths, y, textPaint)
            total += e.amount
            y += rowHeight
        }

        y += rowHeight
        canvas.drawText("Total: ${"%.2f".format(total)}", margin, y, headerPaint)

        document.finishPage(page)
        FileOutputStream(outputFile).use { out -> document.writeTo(out) }
        document.close()
    }

    private fun drawHeaderRow(canvas: android.graphics.Canvas, columns: List<String>, widths: List<Float>, y: Float, paint: Paint): Float {
        var x = margin
        columns.forEachIndexed { i, col ->
            canvas.drawText(col, x, y, paint)
            x += widths[i]
        }
        return y + rowHeight
    }

    private fun drawRow(canvas: android.graphics.Canvas, values: List<String>, widths: List<Float>, y: Float, paint: Paint) {
        var x = margin
        values.forEachIndexed { i, value ->
            canvas.drawText(value.take(30), x, y, paint)
            x += widths[i]
        }
    }
}
