package com.threedis.smartexpensemanager

import com.threedis.smartexpensemanager.data.local.entity.BudgetPeriod
import com.threedis.smartexpensemanager.data.repository.BudgetStatus
import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertTrue
import org.junit.Test

class BudgetStatusTest {

    @Test
    fun `utilization percent computed correctly`() {
        val status = BudgetStatus(BudgetPeriod.MONTHLY, categoryId = null, budgetAmount = 10000.0, spentAmount = 8000.0)
        assertEquals(80.0, status.utilizationPercent, 0.001)
        assertFalse(status.isExceeded)
    }

    @Test
    fun `exceeded when spend surpasses budget`() {
        val status = BudgetStatus(BudgetPeriod.MONTHLY, categoryId = null, budgetAmount = 10000.0, spentAmount = 11000.0)
        assertTrue(status.isExceeded)
        assertEquals(0.0, status.remaining, 0.001)
    }

    @Test
    fun `zero budget does not divide by zero`() {
        val status = BudgetStatus(BudgetPeriod.DAILY, categoryId = 1L, budgetAmount = 0.0, spentAmount = 50.0)
        assertEquals(0.0, status.utilizationPercent, 0.001)
    }

    @Test
    fun `remaining never negative`() {
        val status = BudgetStatus(BudgetPeriod.WEEKLY, categoryId = null, budgetAmount = 100.0, spentAmount = 150.0)
        assertEquals(0.0, status.remaining, 0.001)
    }
}
