package com.threedis.smartexpensemanager

import com.threedis.smartexpensemanager.export.SimpleXlsxWriter
import com.threedis.smartexpensemanager.export.num
import com.threedis.smartexpensemanager.export.str
import org.junit.Assert.assertEquals
import org.junit.Assert.assertTrue
import org.junit.Test
import java.io.File
import java.util.zip.ZipFile

class SimpleXlsxWriterTest {

    @Test
    fun `writes a valid zip with expected OOXML parts and sheet count`() {
        val file = File.createTempFile("report", ".xlsx")
        file.deleteOnExit()

        SimpleXlsxWriter.write(file) {
            sheet("Expense Details") {
                row(str("Date", bold = true), str("Amount", bold = true))
                row(str("2026-07-29"), num(125.5))
            }
            sheet("Category Summary") {
                row(str("Category", bold = true), str("Total", bold = true))
                row(str("Grocery"), num(500.0))
            }
        }

        ZipFile(file).use { zip ->
            val names = zip.entries().asSequence().map { it.name }.toSet()
            assertTrue(names.contains("[Content_Types].xml"))
            assertTrue(names.contains("xl/workbook.xml"))
            assertTrue(names.contains("xl/styles.xml"))
            assertTrue(names.contains("xl/worksheets/sheet1.xml"))
            assertTrue(names.contains("xl/worksheets/sheet2.xml"))
            assertEquals(false, names.contains("xl/worksheets/sheet3.xml"))
        }
    }

    @Test
    fun `escapes special characters in cell text`() {
        val file = File.createTempFile("escape", ".xlsx")
        file.deleteOnExit()

        SimpleXlsxWriter.write(file) {
            sheet("Sheet1") {
                row(str("Tom & Jerry <3>"))
            }
        }

        ZipFile(file).use { zip ->
            val entry = zip.getEntry("xl/worksheets/sheet1.xml")
            val content = zip.getInputStream(entry).bufferedReader().readText()
            assertTrue(content.contains("Tom &amp; Jerry &lt;3&gt;"))
            assertTrue(!content.contains("Tom & Jerry <3>"))
        }
    }
}
