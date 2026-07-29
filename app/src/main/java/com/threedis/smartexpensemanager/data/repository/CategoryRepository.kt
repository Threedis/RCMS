package com.threedis.smartexpensemanager.data.repository

import com.threedis.smartexpensemanager.data.local.dao.CategoryDao
import com.threedis.smartexpensemanager.data.local.entity.Category
import com.threedis.smartexpensemanager.data.local.entity.DefaultCategories
import kotlinx.coroutines.flow.Flow
import javax.inject.Inject
import javax.inject.Singleton

@Singleton
class CategoryRepository @Inject constructor(
    private val categoryDao: CategoryDao
) {
    fun observeActiveCategories(): Flow<List<Category>> = categoryDao.observeActiveCategories()
    fun observeAllCategories(): Flow<List<Category>> = categoryDao.observeAllCategories()

    suspend fun getById(id: Long): Category? = categoryDao.getById(id)

    suspend fun add(category: Category): Long = categoryDao.insert(category)

    suspend fun update(category: Category) = categoryDao.update(category)

    suspend fun delete(id: Long) = categoryDao.softDelete(id)

    suspend fun seedDefaultsIfEmpty() {
        if (categoryDao.count() == 0) {
            categoryDao.insertAll(DefaultCategories.seed())
        }
    }
}
