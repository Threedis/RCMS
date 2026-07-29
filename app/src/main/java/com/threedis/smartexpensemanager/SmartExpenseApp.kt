package com.threedis.smartexpensemanager

import android.app.Application
import androidx.work.Configuration
import com.threedis.smartexpensemanager.data.repository.CategoryRepository
import com.threedis.smartexpensemanager.notification.NotificationHelper
import dagger.hilt.android.HiltAndroidApp
import kotlinx.coroutines.CoroutineScope
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.SupervisorJob
import kotlinx.coroutines.launch
import javax.inject.Inject

@HiltAndroidApp
class SmartExpenseApp : Application() {

    @Inject lateinit var categoryRepository: CategoryRepository

    private val appScope = CoroutineScope(SupervisorJob() + Dispatchers.IO)

    override fun onCreate() {
        super.onCreate()
        NotificationHelper.createChannels(this)
        appScope.launch {
            categoryRepository.seedDefaultsIfEmpty()
        }
    }
}
