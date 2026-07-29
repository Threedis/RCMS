package com.threedis.smartexpensemanager.util

import java.time.DayOfWeek
import java.time.LocalDate
import java.time.temporal.TemporalAdjusters

object DateUtils {

    fun today(): LocalDate = LocalDate.now()

    fun weekRange(date: LocalDate = today()): Pair<LocalDate, LocalDate> {
        val start = date.with(TemporalAdjusters.previousOrSame(DayOfWeek.MONDAY))
        val end = date.with(TemporalAdjusters.nextOrSame(DayOfWeek.SUNDAY))
        return start to end
    }

    fun monthRange(date: LocalDate = today()): Pair<LocalDate, LocalDate> {
        val start = date.withDayOfMonth(1)
        val end = date.with(TemporalAdjusters.lastDayOfMonth())
        return start to end
    }

    fun quarterRange(date: LocalDate = today()): Pair<LocalDate, LocalDate> {
        val quarterStartMonth = ((date.monthValue - 1) / 3) * 3 + 1
        val start = LocalDate.of(date.year, quarterStartMonth, 1)
        val end = start.plusMonths(3).minusDays(1)
        return start to end
    }

    fun yearRange(date: LocalDate = today()): Pair<LocalDate, LocalDate> {
        return LocalDate.of(date.year, 1, 1) to LocalDate.of(date.year, 12, 31)
    }

    /** Financial year range containing [date], starting on [fyStartMonth] (1-12). */
    fun financialYearRange(date: LocalDate = today(), fyStartMonth: Int = 4): Pair<LocalDate, LocalDate> {
        val startYear = if (date.monthValue >= fyStartMonth) date.year else date.year - 1
        val start = LocalDate.of(startYear, fyStartMonth, 1)
        val end = start.plusYears(1).minusDays(1)
        return start to end
    }

    fun last7DaysRange(date: LocalDate = today()): Pair<LocalDate, LocalDate> =
        date.minusDays(6) to date

    fun toEpochDay(date: LocalDate): Long = date.toEpochDay()
}
