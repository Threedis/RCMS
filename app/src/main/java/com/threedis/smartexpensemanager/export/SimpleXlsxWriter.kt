package com.threedis.smartexpensemanager.export

import java.io.File
import java.io.OutputStreamWriter
import java.util.zip.ZipEntry
import java.util.zip.ZipOutputStream

/**
 * Minimal, dependency-free XLSX (OOXML) writer.
 *
 * Apache POI depends on java.awt.* and javax.xml.* APIs that either don't exist or behave
 * differently on the Android runtime, which made every Excel export crash on-device even though
 * it compiled fine on the JVM unit-test target. This writer only touches java.util.zip and plain
 * string building, both of which are part of the Android runtime, so it works reliably on-device.
 */
sealed class XlsxCell {
    data class Str(val value: String, val bold: Boolean = false) : XlsxCell()
    data class Num(val value: Double, val bold: Boolean = false) : XlsxCell()
}

fun str(value: String, bold: Boolean = false) = XlsxCell.Str(value, bold)
fun num(value: Double, bold: Boolean = false) = XlsxCell.Num(value, bold)

class XlsxSheetBuilder(val name: String) {
    val rows = mutableListOf<List<XlsxCell>>()
    fun row(vararg cells: XlsxCell) {
        rows.add(cells.toList())
    }
}

class XlsxWorkbookBuilder {
    val sheets = mutableListOf<XlsxSheetBuilder>()
    fun sheet(name: String, block: XlsxSheetBuilder.() -> Unit) {
        sheets.add(XlsxSheetBuilder(name).apply(block))
    }
}

object SimpleXlsxWriter {

    fun write(outputFile: File, block: XlsxWorkbookBuilder.() -> Unit) {
        val workbook = XlsxWorkbookBuilder().apply(block)

        ZipOutputStream(outputFile.outputStream()).use { zip ->
            writeEntry(zip, "[Content_Types].xml", contentTypesXml(workbook.sheets.size))
            writeEntry(zip, "_rels/.rels", rootRelsXml())
            writeEntry(zip, "xl/workbook.xml", workbookXml(workbook.sheets))
            writeEntry(zip, "xl/_rels/workbook.xml.rels", workbookRelsXml(workbook.sheets.size))
            writeEntry(zip, "xl/styles.xml", stylesXml())
            workbook.sheets.forEachIndexed { index, sheet ->
                writeEntry(zip, "xl/worksheets/sheet${index + 1}.xml", sheetXml(sheet))
            }
        }
    }

    private fun writeEntry(zip: ZipOutputStream, path: String, content: String) {
        zip.putNextEntry(ZipEntry(path))
        val writer = OutputStreamWriter(zip, Charsets.UTF_8)
        writer.write(content)
        writer.flush()
        zip.closeEntry()
    }

    private fun contentTypesXml(sheetCount: Int): String {
        val overrides = (1..sheetCount).joinToString("") {
            "<Override PartName=\"/xl/worksheets/sheet$it.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml\"/>"
        }
        return """<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types">
<Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/>
<Default Extension="xml" ContentType="application/xml"/>
<Override PartName="/xl/workbook.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml"/>
<Override PartName="/xl/styles.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml"/>
$overrides
</Types>"""
    }

    private fun rootRelsXml(): String = """<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
<Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="xl/workbook.xml"/>
</Relationships>"""

    private fun workbookXml(sheets: List<XlsxSheetBuilder>): String {
        val sheetEntries = sheets.mapIndexed { index, sheet ->
            "<sheet name=\"${escapeXml(sheet.name)}\" sheetId=\"${index + 1}\" r:id=\"rId${index + 1}\"/>"
        }.joinToString("")
        return """<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<workbook xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships">
<sheets>$sheetEntries</sheets>
</workbook>"""
    }

    private fun workbookRelsXml(sheetCount: Int): String {
        val rels = (1..sheetCount).joinToString("") {
            "<Relationship Id=\"rId$it\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet\" Target=\"worksheets/sheet$it.xml\"/>"
        }
        val stylesRel = "<Relationship Id=\"rId${sheetCount + 1}\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles\" Target=\"styles.xml\"/>"
        return """<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
$rels$stylesRel
</Relationships>"""
    }

    private fun stylesXml(): String = """<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<styleSheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main">
<fonts count="2">
<font><sz val="11"/><name val="Calibri"/></font>
<font><b/><sz val="11"/><color rgb="FFFFFFFF"/><name val="Calibri"/></font>
</fonts>
<fills count="2">
<fill><patternFill patternType="none"/></fill>
<fill><patternFill patternType="solid"><fgColor rgb="FF00696C"/><bgColor indexed="64"/></patternFill></fill>
</fills>
<borders count="1"><border><left/><right/><top/><bottom/><diagonal/></border></borders>
<cellStyleXfs count="1"><xf numFmtId="0" fontId="0" fillId="0" borderId="0"/></cellStyleXfs>
<cellXfs count="2">
<xf numFmtId="0" fontId="0" fillId="0" borderId="0" xfId="0"/>
<xf numFmtId="0" fontId="1" fillId="1" borderId="0" xfId="0" applyFont="1" applyFill="1"/>
</cellXfs>
</styleSheet>"""

    private fun sheetXml(sheet: XlsxSheetBuilder): String {
        val rowsXml = sheet.rows.mapIndexed { rowIndex, cells ->
            val rowNumber = rowIndex + 1
            val cellsXml = cells.mapIndexed { colIndex, cell ->
                val ref = "${columnLetter(colIndex)}$rowNumber"
                when (cell) {
                    is XlsxCell.Str -> {
                        val style = if (cell.bold) " s=\"1\"" else ""
                        "<c r=\"$ref\"$style t=\"inlineStr\"><is><t xml:space=\"preserve\">${escapeXml(cell.value)}</t></is></c>"
                    }
                    is XlsxCell.Num -> {
                        val style = if (cell.bold) " s=\"1\"" else ""
                        "<c r=\"$ref\"$style><v>${formatNumber(cell.value)}</v></c>"
                    }
                }
            }.joinToString("")
            "<row r=\"$rowNumber\">$cellsXml</row>"
        }.joinToString("")

        return """<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<worksheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main">
<sheetData>$rowsXml</sheetData>
</worksheet>"""
    }

    private fun formatNumber(value: Double): String =
        if (value == value.toLong().toDouble()) value.toLong().toString() else value.toString()

    private fun columnLetter(index: Int): String {
        var i = index
        val sb = StringBuilder()
        while (true) {
            sb.insert(0, ('A' + (i % 26)))
            i = i / 26 - 1
            if (i < 0) break
        }
        return sb.toString()
    }

    private fun escapeXml(value: String): String = value
        .replace("&", "&amp;")
        .replace("<", "&lt;")
        .replace(">", "&gt;")
        .replace("\"", "&quot;")
        .replace("'", "&apos;")
}
