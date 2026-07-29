package com.threedis.smartexpensemanager.ui.category

import androidx.lifecycle.ViewModel
import androidx.lifecycle.viewModelScope
import com.threedis.smartexpensemanager.data.local.entity.Category
import com.threedis.smartexpensemanager.data.repository.CategoryRepository
import dagger.hilt.android.lifecycle.HiltViewModel
import kotlinx.coroutines.flow.SharingStarted
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.stateIn
import kotlinx.coroutines.launch
import javax.inject.Inject

@HiltViewModel
class CategoryViewModel @Inject constructor(
    private val categoryRepository: CategoryRepository
) : ViewModel() {

    val categories: StateFlow<List<Category>> = categoryRepository.observeAllCategories()
        .stateIn(viewModelScope, SharingStarted.WhileSubscribed(5000), emptyList())

    fun addCategory(name: String, iconName: String, colorHex: String) {
        viewModelScope.launch {
            categoryRepository.add(Category(name = name, iconName = iconName, colorHex = colorHex))
        }
    }

    fun updateCategory(category: Category) {
        viewModelScope.launch { categoryRepository.update(category) }
    }

    fun deleteCategory(id: Long) {
        viewModelScope.launch { categoryRepository.delete(id) }
    }
}
