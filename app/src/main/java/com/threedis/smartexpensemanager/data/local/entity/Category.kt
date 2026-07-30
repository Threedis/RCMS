package com.threedis.smartexpensemanager.data.local.entity

import androidx.room.Entity
import androidx.room.PrimaryKey

@Entity(tableName = "categories")
data class Category(
    @PrimaryKey(autoGenerate = true) val id: Long = 0,
    val name: String,
    val iconName: String = "category",
    val colorHex: String = "#6750A4",
    val isDefault: Boolean = false,
    val isActive: Boolean = true
)

object DefaultCategories {
    val NAMES = listOf(
        "Grocery", "Banking Charges", "Petrol", "Diesel", "Travel", "Taxi", "Food",
        "Restaurant", "Medical", "Electricity", "Water Bill", "Gas", "Mobile Recharge",
        "Internet", "Online Shopping", "Clothing", "Entertainment", "Education", "EMI",
        "Rent", "Insurance", "Investment", "Gifts", "Miscellaneous"
    )

    fun seed(): List<Category> = NAMES.mapIndexed { index, name ->
        Category(
            name = name,
            iconName = "category",
            colorHex = PALETTE[index % PALETTE.size],
            isDefault = true,
            isActive = true
        )
    }

    private val PALETTE = listOf(
        "#6750A4", "#7D5260", "#386A20", "#B3261E", "#00696C", "#984061",
        "#785900", "#31628C", "#8C4A00", "#4A6D24"
    )
}
