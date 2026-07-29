package com.threedis.smartexpensemanager.notification

import android.app.AlarmManager
import android.app.PendingIntent
import android.content.Context
import android.content.Intent
import java.util.Calendar

object ReminderScheduler {

    fun scheduleDaily(context: Context, hour: Int = 21, minute: Int = 0) {
        schedule(context, "daily", requestCode = 1) {
            set(Calendar.HOUR_OF_DAY, hour)
            set(Calendar.MINUTE, minute)
            set(Calendar.SECOND, 0)
        }
    }

    fun scheduleWeekly(context: Context, dayOfWeek: Int = Calendar.SUNDAY, hour: Int = 20) {
        schedule(context, "weekly", requestCode = 2) {
            set(Calendar.DAY_OF_WEEK, dayOfWeek)
            set(Calendar.HOUR_OF_DAY, hour)
            set(Calendar.MINUTE, 0)
        }
    }

    fun scheduleMonthly(context: Context, dayOfMonth: Int = 1, hour: Int = 10) {
        schedule(context, "monthly", requestCode = 3) {
            set(Calendar.DAY_OF_MONTH, dayOfMonth)
            set(Calendar.HOUR_OF_DAY, hour)
            set(Calendar.MINUTE, 0)
        }
    }

    fun cancel(context: Context, requestCode: Int) {
        val alarmManager = context.getSystemService(Context.ALARM_SERVICE) as AlarmManager
        val intent = Intent(context, ReminderBroadcastReceiver::class.java)
        val pendingIntent = PendingIntent.getBroadcast(
            context, requestCode, intent,
            PendingIntent.FLAG_UPDATE_CURRENT or PendingIntent.FLAG_IMMUTABLE
        )
        alarmManager.cancel(pendingIntent)
    }

    private inline fun schedule(context: Context, type: String, requestCode: Int, configure: Calendar.() -> Unit) {
        val alarmManager = context.getSystemService(Context.ALARM_SERVICE) as AlarmManager
        val intent = Intent(context, ReminderBroadcastReceiver::class.java).apply {
            putExtra(ReminderBroadcastReceiver.EXTRA_REMINDER_TYPE, type)
        }
        val pendingIntent = PendingIntent.getBroadcast(
            context, requestCode, intent,
            PendingIntent.FLAG_UPDATE_CURRENT or PendingIntent.FLAG_IMMUTABLE
        )
        val calendar = Calendar.getInstance().apply {
            configure()
            if (before(Calendar.getInstance())) add(Calendar.DATE, 1)
        }
        val interval = if (type == "weekly") AlarmManager.INTERVAL_DAY * 7 else AlarmManager.INTERVAL_DAY
        alarmManager.setRepeating(
            AlarmManager.RTC_WAKEUP,
            calendar.timeInMillis,
            interval,
            pendingIntent
        )
    }
}
