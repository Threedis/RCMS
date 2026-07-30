package com.threedis.smartexpensemanager.data.repository

import com.threedis.smartexpensemanager.data.local.dao.SettingsDao
import com.threedis.smartexpensemanager.data.local.entity.AppSettings
import kotlinx.coroutines.flow.Flow
import kotlinx.coroutines.flow.map
import javax.inject.Inject
import javax.inject.Singleton

@Singleton
class SettingsRepository @Inject constructor(
    private val settingsDao: SettingsDao
) {
    fun observeSettings(): Flow<AppSettings> = settingsDao.observeSettings().map { it ?: AppSettings() }

    suspend fun getSettings(): AppSettings = settingsDao.getSettings() ?: AppSettings()

    suspend fun update(transform: (AppSettings) -> AppSettings) {
        val current = getSettings()
        settingsDao.upsert(transform(current))
    }
}
