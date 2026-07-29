package com.threedis.smartexpensemanager.notification

import android.content.Context
import com.threedis.smartexpensemanager.data.repository.BudgetRepository
import com.threedis.smartexpensemanager.data.repository.BudgetStatus
import dagger.hilt.android.qualifiers.ApplicationContext
import javax.inject.Inject
import javax.inject.Singleton

/**
 * Evaluates budget status after every expense save and fires threshold notifications
 * at 50/70/80/90/100% and on overshoot, matching the RCMS budget-alert spec.
 */
@Singleton
class BudgetAlertManager @Inject constructor(
    @ApplicationContext private val context: Context,
    private val budgetRepository: BudgetRepository
) {
    private val thresholds = listOf(50, 70, 80, 90, 100)

    suspend fun evaluateAndNotify(categoryId: Long?, categoryName: String?, fyStartMonth: Int = 4) {
        val statuses = budgetRepository.statusesAffectedBy(categoryId, fyStartMonth)
        for (status in statuses) {
            notifyIfThresholdCrossed(status, categoryName)
        }
    }

    private fun notifyIfThresholdCrossed(status: BudgetStatus, categoryName: String?) {
        val percent = status.utilizationPercent
        val scope = if (status.categoryId != null) categoryName ?: "Category" else "Overall"
        val periodLabel = status.period.name.lowercase().replace('_', ' ')

        if (status.isExceeded) {
            NotificationHelper.notify(
                context, NotificationHelper.CHANNEL_BUDGET,
                "Budget Exceeded",
                "$scope $periodLabel budget exceeded: spent ${"%.2f".format(status.spentAmount)} of ${"%.2f".format(status.budgetAmount)}",
                android.R.drawable.ic_dialog_alert
            )
            return
        }

        val crossed = thresholds.lastOrNull { percent >= it }
        if (crossed != null) {
            NotificationHelper.notify(
                context, NotificationHelper.CHANNEL_BUDGET,
                "$crossed% Budget Used",
                "$scope $periodLabel budget: ${"%.0f".format(percent)}% used (${"%.2f".format(status.spentAmount)} of ${"%.2f".format(status.budgetAmount)})",
                android.R.drawable.ic_dialog_info
            )
        }
    }
}
