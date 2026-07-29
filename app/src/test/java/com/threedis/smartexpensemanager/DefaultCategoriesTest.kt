package com.threedis.smartexpensemanager

import com.threedis.smartexpensemanager.data.local.entity.DefaultCategories
import org.junit.Assert.assertEquals
import org.junit.Assert.assertTrue
import org.junit.Test

class DefaultCategoriesTest {

    @Test
    fun `seed produces one category per default name`() {
        val seeded = DefaultCategories.seed()
        assertEquals(DefaultCategories.NAMES.size, seeded.size)
        assertEquals(DefaultCategories.NAMES.toSet(), seeded.map { it.name }.toSet())
    }

    @Test
    fun `seeded categories are marked default and active`() {
        val seeded = DefaultCategories.seed()
        assertTrue(seeded.all { it.isDefault && it.isActive })
    }
}
