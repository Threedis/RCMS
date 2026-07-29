package com.threedis.smartexpensemanager.data.repository

import com.threedis.smartexpensemanager.data.local.entity.PaymentMethod

enum class SortOrder { LATEST_FIRST, OLDEST_FIRST, HIGHEST_AMOUNT, LOWEST_AMOUNT }

data class ExpenseFilter(
    val startEpochDay: Long? = null,
    val endEpochDay: Long? = null,
    val categoryId: Long? = null,
    val paymentMethod: PaymentMethod? = null,
    val location: String? = null,
    val minAmount: Double? = null,
    val maxAmount: Double? = null,
    val searchText: String? = null,
    val sortOrder: SortOrder = SortOrder.LATEST_FIRST
)
