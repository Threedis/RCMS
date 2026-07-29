package com.threedis.smartexpensemanager.ui.navigation

import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.filled.Dashboard
import androidx.compose.material.icons.filled.History
import androidx.compose.material.icons.filled.PieChart
import androidx.compose.material.icons.filled.Savings
import androidx.compose.material.icons.filled.Settings
import androidx.compose.ui.graphics.vector.ImageVector

sealed class AppDestination(val route: String, val label: String, val icon: ImageVector) {
    data object Dashboard : AppDestination("dashboard", "Dashboard", Icons.Filled.Dashboard)
    data object History : AppDestination("history", "History", Icons.Filled.History)
    data object Budget : AppDestination("budget", "Budget", Icons.Filled.Savings)
    data object Reports : AppDestination("reports", "Reports", Icons.Filled.PieChart)
    data object Settings : AppDestination("settings", "Settings", Icons.Filled.Settings)
}

const val ROUTE_EXPENSE_ENTRY = "expense_entry"
const val ROUTE_CATEGORIES = "categories"

val bottomNavDestinations = listOf(
    AppDestination.Dashboard,
    AppDestination.History,
    AppDestination.Budget,
    AppDestination.Reports,
    AppDestination.Settings
)
