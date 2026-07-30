# Smart Expense Manager

A professional, fully offline Android expense-tracking application built with Kotlin, Jetpack Compose, MVVM, Room, and Hilt.

## Features

- **Dashboard** — today/week/month/financial-year totals, budget utilization, recent transactions, category breakdown, monthly trend.
- **Categories** — 24 seeded default categories plus add/edit/delete with custom icon and color.
- **Expense Entry** — amount, category, description, date/time, location + GPS, payment method (Cash, Credit Card, Debit Card, UPI, Net Banking, Wallet), merchant, bill number, remarks, and a compressed camera/gallery receipt.
- **Budget Management** — daily/weekly/monthly/financial-year budgets, overall or per-category, with automatic 50/70/80/90/100%-and-exceeded notifications on every new expense.
- **Expense History** — filter by date range/category/payment method/location/amount range, full-text search, and four sort orders.
- **Reports** — daily/weekly/monthly/quarterly/financial-year/yearly reports with total, average, highest, lowest, and transaction count.
- **Export** — Excel (multi-sheet: details, category summary, budget summary, monthly summary), CSV, and PDF, saved via `FileProvider` and opened automatically for the user, with a Share action alongside.
- **Backup & Restore** — one-tap local database backup/restore.
- **Notifications** — budget alerts plus daily/weekly/monthly reminders via `AlarmManager`.
- **Settings** — theme (light/dark/system, Material You dynamic color), currency, date format, financial-year start month, app lock/biometric toggle.

## Architecture

```
app/src/main/java/com/threedis/smartexpensemanager/
├── data/
│   ├── local/            # Room entities, DAOs, TypeConverters, AppDatabase
│   └── repository/       # Repository pattern over the DAOs
├── di/                   # Hilt modules
├── notification/         # Budget alerts + reminder scheduling
├── export/               # Excel (hand-rolled OOXML writer), CSV, PDF exporters
├── camera/               # CameraX capture + receipt compression
├── ui/
│   ├── dashboard/ expense/ history/ budget/ reports/ settings/ category/
│   ├── navigation/       # NavHost + bottom navigation
│   └── theme/            # Material 3 theme
└── util/                 # Date-range helpers (week/month/quarter/FY)
```

- **MVVM**: each screen has a `@HiltViewModel` exposing `StateFlow` UI state; Compose screens collect it with `collectAsStateWithLifecycle()`.
- **Repository pattern**: `ExpenseRepository`, `CategoryRepository`, `BudgetRepository`, `SettingsRepository` are the only classes that touch DAOs.
- **Room**: `expenses`, `categories`, `budgets`, `app_settings` tables; `epochDay` indexing keeps date-range queries fast even at 100k+ rows.
- **Hilt**: `SmartExpenseApp` is the `@HiltAndroidApp` root; `DatabaseModule` provides the singleton `AppDatabase` and DAOs.

## Setup

1. Open the project root in Android Studio (Koala or newer).
2. Let Gradle sync (`compileSdk 34`, `minSdk 26`, Kotlin 1.9.24, AGP 8.5.2).
3. If `gradlew` is missing its wrapper jar, run `gradle wrapper --gradle-version 8.7` once from a machine with Gradle installed, or open directly in Android Studio which will regenerate it.
4. Run on a device/emulator running Android 8.0 (API 26) or later.

No backend or internet connection is required — all data lives in the local Room database and app-private storage (`getExternalFilesDir` for receipts/exports/backups).

## Testing

Unit tests under `app/src/test` cover the pure business logic that's safe to run on the JVM without Robolectric:

- `DateUtilsTest` — week/month/quarter/financial-year range calculations.
- `BudgetStatusTest` — utilization percent, exceeded/remaining edge cases (zero budget, overshoot).
- `DefaultCategoriesTest` — category seeding.

Run with `./gradlew test`.

## Security & Privacy

The app is built so no expense data can leave the device:

- **No `INTERNET` permission is declared anywhere in `AndroidManifest.xml`.** Android enforces this at the OS/kernel level — without it, the app cannot open a network socket at all, regardless of what any library inside it tries to do. There is no analytics SDK, crash reporter, or ad library in this project either.
- **`network_security_config.xml`** additionally forbids cleartext (HTTP) traffic app-wide as a second, independent layer of defense, in case a future dependency were ever mistakenly granted `INTERNET`.
- **`android:allowBackup="false"`** — disables both classic ADB/cloud backup and Android's Auto Backup to Google Drive, so the local database and receipts are never copied off the device by the OS either.
- **All data lives in a local Room (SQLite) database** (`smart_expense_manager.db`, app-private storage) and app-private folders (`getExternalFilesDir` for receipts/exports/backups) — not shared storage, not a content provider open to other apps.
- **`FileProvider` only exposes the specific `receipts/`, `exports/`, `backups/`, and camera-cache folders** (see `res/xml/file_paths.xml`) it needs to hand a file to the camera app or a share/view intent — never the whole filesystem or database file directly.
- **Release builds run R8/ProGuard minification** (`isMinifyEnabled = true`), which also strips unused code paths.

Not yet implemented, and worth doing before shipping to production: encrypting the Room database at rest (e.g. via SQLCipher) and gating it behind the biometric/PIN lock toggle already present in Settings — currently that toggle is UI-only. Ask if you'd like this added; it's a larger change since it introduces a native dependency and a passphrase-management flow.

## Notes on scope

This scaffold implements the full architecture and the core user flows end-to-end (entry → budget check → notification → dashboard/history/reports → export → backup). Some of the more exhaustive spec items (e.g. PIN-lock screen UI, WorkManager-based missed-entry detection) are wired for in the dependency/module layer and are the natural next slice of work on top of this foundation.
