package com.threedis.smartexpensemanager.data.local.converter

import androidx.room.TypeConverter
import com.threedis.smartexpensemanager.data.local.entity.BudgetPeriod
import com.threedis.smartexpensemanager.data.local.entity.PaymentMethod

class Converters {
    @TypeConverter
    fun fromPaymentMethod(value: PaymentMethod): String = value.name

    @TypeConverter
    fun toPaymentMethod(value: String): PaymentMethod = PaymentMethod.valueOf(value)

    @TypeConverter
    fun fromBudgetPeriod(value: BudgetPeriod): String = value.name

    @TypeConverter
    fun toBudgetPeriod(value: String): BudgetPeriod = BudgetPeriod.valueOf(value)
}
