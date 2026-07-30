package com.threedis.smartexpensemanager.ui.settings

import androidx.compose.foundation.clickable
import androidx.compose.foundation.layout.*
import androidx.compose.material3.*
import androidx.compose.runtime.*
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.unit.dp
import androidx.hilt.navigation.compose.hiltViewModel
import androidx.lifecycle.compose.collectAsStateWithLifecycle

@Composable
fun SettingsScreen(
    onManageCategories: () -> Unit,
    viewModel: SettingsViewModel = hiltViewModel()
) {
    val settings by viewModel.settings.collectAsStateWithLifecycle()

    Column(modifier = Modifier.fillMaxSize().padding(16.dp), verticalArrangement = Arrangement.spacedBy(16.dp)) {
        Text("Settings", style = MaterialTheme.typography.titleLarge)

        ListItem(
            headlineContent = { Text("Manage Categories") },
            modifier = Modifier.clickableCompat(onManageCategories)
        )

        Text("Theme", style = MaterialTheme.typography.titleMedium)
        Row {
            listOf("LIGHT", "DARK", "SYSTEM").forEach { mode ->
                FilterChip(
                    selected = settings.themeMode == mode,
                    onClick = { viewModel.setThemeMode(mode) },
                    label = { Text(mode) },
                    modifier = Modifier.padding(end = 8.dp)
                )
            }
        }

        Text("Security", style = MaterialTheme.typography.titleMedium)
        Row(verticalAlignment = Alignment.CenterVertically) {
            Text("App Lock")
            Switch(checked = settings.appLockEnabled, onCheckedChange = { viewModel.setAppLockEnabled(it) })
        }
        Row(verticalAlignment = Alignment.CenterVertically) {
            Text("Biometric Authentication")
            Switch(checked = settings.biometricEnabled, onCheckedChange = { viewModel.setBiometricEnabled(it) })
        }

        Text("Backup & Restore", style = MaterialTheme.typography.titleMedium)
        Row(horizontalArrangement = Arrangement.spacedBy(8.dp)) {
            Button(onClick = { viewModel.backup() }) { Text("Backup Now") }
        }
    }
}

private fun Modifier.clickableCompat(onClick: () -> Unit): Modifier =
    this.then(Modifier.clickable(onClick = onClick))
