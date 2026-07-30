package com.threedis.smartexpensemanager.ui.settings

import androidx.lifecycle.ViewModel
import androidx.lifecycle.viewModelScope
import com.threedis.smartexpensemanager.data.local.entity.AppSettings
import com.threedis.smartexpensemanager.data.repository.SettingsRepository
import com.threedis.smartexpensemanager.export.ExportManager
import dagger.hilt.android.lifecycle.HiltViewModel
import kotlinx.coroutines.flow.SharingStarted
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.stateIn
import kotlinx.coroutines.launch
import java.io.File
import javax.inject.Inject

@HiltViewModel
class SettingsViewModel @Inject constructor(
    private val settingsRepository: SettingsRepository,
    private val exportManager: ExportManager
) : ViewModel() {

    val settings: StateFlow<AppSettings> = settingsRepository.observeSettings()
        .stateIn(viewModelScope, SharingStarted.WhileSubscribed(5000), AppSettings())

    fun setThemeMode(mode: String) = update { it.copy(themeMode = mode) }
    fun setCurrency(code: String) = update { it.copy(currencyCode = code) }
    fun setDateFormat(format: String) = update { it.copy(dateFormat = format) }
    fun setFinancialYearStartMonth(month: Int) = update { it.copy(financialYearStartMonth = month) }
    fun setAppLockEnabled(enabled: Boolean) = update { it.copy(appLockEnabled = enabled) }
    fun setBiometricEnabled(enabled: Boolean) = update { it.copy(biometricEnabled = enabled) }
    fun setReminderPreferences(daily: Boolean, weekly: Boolean, monthly: Boolean) = update {
        it.copy(dailyReminderEnabled = daily, weeklyReminderEnabled = weekly, monthlyReminderEnabled = monthly)
    }

    private fun update(transform: (AppSettings) -> AppSettings) {
        viewModelScope.launch { settingsRepository.update(transform) }
    }

    fun backup(): File = exportManager.backupDatabase()
    fun restore(file: File) = exportManager.restoreDatabase(file)
}
