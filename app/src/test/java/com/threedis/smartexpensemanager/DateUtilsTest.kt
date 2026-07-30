package com.threedis.smartexpensemanager

import com.threedis.smartexpensemanager.util.DateUtils
import org.junit.Assert.assertEquals
import org.junit.Test
import java.time.LocalDate

class DateUtilsTest {

    @Test
    fun `month range covers full calendar month`() {
        val date = LocalDate.of(2026, 2, 15)
        val (start, end) = DateUtils.monthRange(date)
        assertEquals(LocalDate.of(2026, 2, 1), start)
        assertEquals(LocalDate.of(2026, 2, 28), end)
    }

    @Test
    fun `week range starts monday ends sunday`() {
        val wednesday = LocalDate.of(2026, 7, 29)
        val (start, end) = DateUtils.weekRange(wednesday)
        assertEquals(LocalDate.of(2026, 7, 27), start)
        assertEquals(LocalDate.of(2026, 8, 2), end)
    }

    @Test
    fun `financial year starting April rolls to previous year before April`() {
        val januaryDate = LocalDate.of(2026, 1, 10)
        val (start, end) = DateUtils.financialYearRange(januaryDate, fyStartMonth = 4)
        assertEquals(LocalDate.of(2025, 4, 1), start)
        assertEquals(LocalDate.of(2026, 3, 31), end)
    }

    @Test
    fun `financial year after start month uses current year`() {
        val augustDate = LocalDate.of(2026, 8, 1)
        val (start, end) = DateUtils.financialYearRange(augustDate, fyStartMonth = 4)
        assertEquals(LocalDate.of(2026, 4, 1), start)
        assertEquals(LocalDate.of(2027, 3, 31), end)
    }

    @Test
    fun `quarter range groups months of three`() {
        val date = LocalDate.of(2026, 11, 5)
        val (start, end) = DateUtils.quarterRange(date)
        assertEquals(LocalDate.of(2026, 10, 1), start)
        assertEquals(LocalDate.of(2026, 12, 31), end)
    }
}
