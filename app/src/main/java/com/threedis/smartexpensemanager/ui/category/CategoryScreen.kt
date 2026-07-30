package com.threedis.smartexpensemanager.ui.category

import androidx.compose.foundation.background
import androidx.compose.foundation.clickable
import androidx.compose.foundation.layout.*
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.items
import androidx.compose.foundation.shape.CircleShape
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.filled.Add
import androidx.compose.material.icons.filled.ArrowBack
import androidx.compose.material.icons.filled.Delete
import androidx.compose.material.icons.filled.Edit
import androidx.compose.material3.*
import androidx.compose.runtime.*
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.unit.dp
import androidx.hilt.navigation.compose.hiltViewModel
import androidx.lifecycle.compose.collectAsStateWithLifecycle
import com.threedis.smartexpensemanager.data.local.entity.Category

private val categoryColorPalette = listOf(
    "#6750A4", "#7D5260", "#386A20", "#B3261E", "#00696C", "#984061",
    "#785900", "#31628C", "#8C4A00", "#4A6D24", "#9C4146", "#006874"
)

/** Master form for the expense-category catalog: add new categories, edit name/color, or remove custom ones. */
@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun CategoryScreen(onBack: () -> Unit, viewModel: CategoryViewModel = hiltViewModel()) {
    val categories by viewModel.categories.collectAsStateWithLifecycle()
    var editingCategory by remember { mutableStateOf<Category?>(null) }
    var showAddDialog by remember { mutableStateOf(false) }

    Scaffold(
        topBar = {
            TopAppBar(
                title = { Text("Categories") },
                navigationIcon = {
                    IconButton(onClick = onBack) { Icon(Icons.Filled.ArrowBack, contentDescription = "Back") }
                }
            )
        },
        floatingActionButton = {
            FloatingActionButton(onClick = { showAddDialog = true }) {
                Icon(Icons.Filled.Add, contentDescription = "Add Category")
            }
        }
    ) { padding ->
        LazyColumn(
            modifier = Modifier.fillMaxSize().padding(padding).padding(horizontal = 16.dp, vertical = 8.dp),
            verticalArrangement = Arrangement.spacedBy(4.dp)
        ) {
            items(categories, key = { it.id }) { category ->
                ListItem(
                    modifier = Modifier.clickable { editingCategory = category },
                    leadingContent = {
                        Box(
                            modifier = Modifier
                                .size(20.dp)
                                .background(parseCategoryColor(category.colorHex), CircleShape)
                        )
                    },
                    headlineContent = { Text(category.name) },
                    supportingContent = if (category.isDefault) ({ Text("Default") }) else null,
                    trailingContent = {
                        Row {
                            IconButton(onClick = { editingCategory = category }) {
                                Icon(Icons.Filled.Edit, contentDescription = "Edit")
                            }
                            IconButton(onClick = { viewModel.deleteCategory(category.id) }) {
                                Icon(Icons.Filled.Delete, contentDescription = "Delete")
                            }
                        }
                    }
                )
            }
        }
    }

    if (showAddDialog) {
        CategoryFormDialog(
            title = "Add Category",
            initialName = "",
            initialColorHex = categoryColorPalette.first(),
            onDismiss = { showAddDialog = false },
            onConfirm = { name, colorHex ->
                viewModel.addCategory(name, "category", colorHex)
                showAddDialog = false
            }
        )
    }

    editingCategory?.let { category ->
        CategoryFormDialog(
            title = "Edit Category",
            initialName = category.name,
            initialColorHex = category.colorHex,
            onDismiss = { editingCategory = null },
            onConfirm = { name, colorHex ->
                viewModel.updateCategory(category.copy(name = name, colorHex = colorHex))
                editingCategory = null
            }
        )
    }
}

@Composable
private fun CategoryFormDialog(
    title: String,
    initialName: String,
    initialColorHex: String,
    onDismiss: () -> Unit,
    onConfirm: (name: String, colorHex: String) -> Unit
) {
    var name by remember { mutableStateOf(initialName) }
    var colorHex by remember { mutableStateOf(initialColorHex) }

    AlertDialog(
        onDismissRequest = onDismiss,
        title = { Text(title) },
        text = {
            Column(verticalArrangement = Arrangement.spacedBy(12.dp)) {
                OutlinedTextField(
                    value = name,
                    onValueChange = { name = it },
                    label = { Text("Category name") },
                    modifier = Modifier.fillMaxWidth()
                )
                Text("Colour")
                FlowRowColorPicker(
                    colors = categoryColorPalette,
                    selected = colorHex,
                    onSelect = { colorHex = it }
                )
            }
        },
        confirmButton = {
            TextButton(onClick = { if (name.isNotBlank()) onConfirm(name.trim(), colorHex) }) { Text("Save") }
        },
        dismissButton = { TextButton(onClick = onDismiss) { Text("Cancel") } }
    )
}

@Composable
private fun FlowRowColorPicker(colors: List<String>, selected: String, onSelect: (String) -> Unit) {
    val rows = colors.chunked(6)
    Column(verticalArrangement = Arrangement.spacedBy(8.dp)) {
        rows.forEach { row ->
            Row(horizontalArrangement = Arrangement.spacedBy(8.dp)) {
                row.forEach { hex ->
                    val isSelected = hex.equals(selected, ignoreCase = true)
                    Box(
                        modifier = Modifier
                            .size(32.dp)
                            .background(parseCategoryColor(hex), CircleShape)
                            .then(
                                if (isSelected) Modifier.background(Color.Transparent) else Modifier
                            )
                            .clickable { onSelect(hex) },
                        contentAlignment = Alignment.Center
                    ) {
                        if (isSelected) {
                            Box(modifier = Modifier.size(12.dp).background(Color.White, CircleShape))
                        }
                    }
                }
            }
        }
    }
}

private fun parseCategoryColor(hex: String): Color = try {
    Color(android.graphics.Color.parseColor(hex))
} catch (e: IllegalArgumentException) {
    Color(0xFF6750A4)
}
