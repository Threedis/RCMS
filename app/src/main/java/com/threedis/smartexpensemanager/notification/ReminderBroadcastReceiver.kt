package com.threedis.smartexpensemanager.notification

import android.content.BroadcastReceiver
import android.content.Context
import android.content.Intent

class ReminderBroadcastReceiver : BroadcastReceiver() {
    override fun onReceive(context: Context, intent: Intent) {
        val type = intent.getStringExtra(EXTRA_REMINDER_TYPE) ?: "daily"
        val (title, message) = when (type) {
            "weekly" -> "Weekly Reminder" to "Don't forget to log this week's expenses."
            "monthly" -> "Monthly Reminder" to "Review and log your monthly expenses."
            "missed" -> "Missed Entry" to "You haven't logged any expense in a while."
            else -> "Daily Reminder" to "Log today's expenses to stay on budget."
        }
        NotificationHelper.notify(context, NotificationHelper.CHANNEL_REMINDER, title, message, android.R.drawable.ic_dialog_info)
    }

    companion object {
        const val EXTRA_REMINDER_TYPE = "reminder_type"
    }
}
