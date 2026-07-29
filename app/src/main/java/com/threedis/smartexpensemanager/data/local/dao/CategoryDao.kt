package com.threedis.smartexpensemanager.data.local.dao

import androidx.room.*
import com.threedis.smartexpensemanager.data.local.entity.Category
import kotlinx.coroutines.flow.Flow

@Dao
interface CategoryDao {
    @Query("SELECT * FROM categories WHERE isActive = 1 ORDER BY name ASC")
    fun observeActiveCategories(): Flow<List<Category>>

    @Query("SELECT * FROM categories ORDER BY name ASC")
    fun observeAllCategories(): Flow<List<Category>>

    @Query("SELECT * FROM categories WHERE id = :id")
    suspend fun getById(id: Long): Category?

    @Insert(onConflict = OnConflictStrategy.IGNORE)
    suspend fun insertAll(categories: List<Category>): List<Long>

    @Insert
    suspend fun insert(category: Category): Long

    @Update
    suspend fun update(category: Category)

    @Query("UPDATE categories SET isActive = 0 WHERE id = :id")
    suspend fun softDelete(id: Long)

    @Query("SELECT COUNT(*) FROM categories")
    suspend fun count(): Int
}
