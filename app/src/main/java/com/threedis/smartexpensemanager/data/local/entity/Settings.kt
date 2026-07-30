package com.threedis.smartexpensemanager.data.local.entity

import androidx.room.Entity
import androidx.room.PrimaryKey

@Entity(tableName = "app_settings")
data class AppSettings(
    @PrimaryKey val id: Int = 1,
    val themeMode: String = "SYSTEM", // LIGHT, DARK, SYSTEM
    val currencyCode: String = "INR",
    val dateFormat: String = "dd/MM/yyyy",
    val financialYearStartMonth: Int = 4, // April
    val appLockEnabled: Boolean = false,
    val biometricEnabled: Boolean = false,
    val pinHash: String? = null,
    val dailyReminderEnabled: Boolean = true,
    val weeklyReminderEnabled: Boolean = true,
    val monthlyReminderEnabled: Boolean = true
)
