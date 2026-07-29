package com.threedis.smartexpensemanager.data.local.entity

enum class PaymentMethod(val label: String) {
    CASH("Cash"),
    CREDIT_CARD("Credit Card"),
    DEBIT_CARD("Debit Card"),
    UPI("UPI"),
    NET_BANKING("Net Banking"),
    WALLET("Wallet")
}
