package com.threedis.smartexpensemanager.ui.navigation

import androidx.compose.foundation.layout.padding
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.filled.Add
import androidx.compose.material3.FloatingActionButton
import androidx.compose.material3.Icon
import androidx.compose.material3.NavigationBar
import androidx.compose.material3.NavigationBarItem
import androidx.compose.material3.Scaffold
import androidx.compose.runtime.Composable
import androidx.compose.runtime.getValue
import androidx.navigation.NavGraph.Companion.findStartDestination
import androidx.navigation.compose.NavHost
import androidx.navigation.compose.composable
import androidx.navigation.compose.currentBackStackEntryAsState
import androidx.navigation.compose.rememberNavController
import com.threedis.smartexpensemanager.ui.budget.BudgetScreen
import com.threedis.smartexpensemanager.ui.category.CategoryScreen
import com.threedis.smartexpensemanager.ui.dashboard.DashboardScreen
import com.threedis.smartexpensemanager.ui.expense.ExpenseEntryScreen
import com.threedis.smartexpensemanager.ui.history.HistoryScreen
import com.threedis.smartexpensemanager.ui.reports.ReportsScreen
import com.threedis.smartexpensemanager.ui.settings.SettingsScreen

@Composable
fun AppNavHost() {
    val navController = rememberNavController()

    Scaffold(
        bottomBar = {
            val backStackEntry by navController.currentBackStackEntryAsState()
            val currentRoute = backStackEntry?.destination?.route
            NavigationBar {
                bottomNavDestinations.forEach { destination ->
                    NavigationBarItem(
                        selected = currentRoute == destination.route,
                        onClick = {
                            navController.navigate(destination.route) {
                                popUpTo(navController.graph.findStartDestination().id) { saveState = true }
                                launchSingleTop = true
                                restoreState = true
                            }
                        },
                        icon = { Icon(destination.icon, contentDescription = destination.label) },
                        label = { androidx.compose.material3.Text(destination.label) }
                    )
                }
            }
        },
        floatingActionButton = {
            FloatingActionButton(onClick = { navController.navigate(ROUTE_EXPENSE_ENTRY) }) {
                Icon(Icons.Filled.Add, contentDescription = "Add Expense")
            }
        }
    ) { padding ->
        NavHost(
            navController = navController,
            startDestination = AppDestination.Dashboard.route,
            modifier = androidx.compose.ui.Modifier.padding(padding)
        ) {
            composable(AppDestination.Dashboard.route) {
                DashboardScreen(onAddExpense = { navController.navigate(ROUTE_EXPENSE_ENTRY) })
            }
            composable(AppDestination.History.route) { HistoryScreen() }
            composable(AppDestination.Budget.route) { BudgetScreen() }
            composable(AppDestination.Reports.route) { ReportsScreen() }
            composable(AppDestination.Settings.route) {
                SettingsScreen(onManageCategories = { navController.navigate(ROUTE_CATEGORIES) })
            }
            composable(ROUTE_EXPENSE_ENTRY) {
                ExpenseEntryScreen(onDone = { navController.popBackStack() })
            }
            composable(ROUTE_CATEGORIES) {
                CategoryScreen(onBack = { navController.popBackStack() })
            }
        }
    }
}
