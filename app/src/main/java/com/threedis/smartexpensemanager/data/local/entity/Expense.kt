package com.threedis.smartexpensemanager.data.local.entity

import androidx.room.Entity
import androidx.room.ForeignKey
import androidx.room.Index
import androidx.room.PrimaryKey

@Entity(
    tableName = "expenses",
    foreignKeys = [
        ForeignKey(
            entity = Category::class,
            parentColumns = ["id"],
            childColumns = ["categoryId"],
            onDelete = ForeignKey.SET_NULL
        )
    ],
    indices = [Index("categoryId"), Index("epochDay"), Index("paymentMethod")]
)
data class Expense(
    @PrimaryKey(autoGenerate = true) val id: Long = 0,
    val amount: Double,
    val categoryId: Long?,
    val description: String = "",
    /** Days since epoch (LocalDate.toEpochDay) for fast range queries */
    val epochDay: Long,
    /** Millis-of-day for the time component */
    val timeOfDayMillis: Long,
    /** Full timestamp in epoch millis, used for sorting/display */
    val timestampMillis: Long,
    val location: String = "",
    val latitude: Double? = null,
    val longitude: Double? = null,
    val paymentMethod: PaymentMethod = PaymentMethod.CASH,
    val merchantName: String = "",
    val billNumber: String = "",
    val remarks: String = "",
    val receiptPath: String? = null,
    val receiptThumbnailPath: String? = null,
    val createdAt: Long = System.currentTimeMillis(),
    val updatedAt: Long = System.currentTimeMillis()
)
