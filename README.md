# SpendSense

A personal budgeting app built with .NET MAUI Blazor Hybrid and Aspire.

## Features

- **Accounts** — current accounts, savings, credit cards and cash, each with a live balance; every transaction is paid from (or into) an account, cards show what's owed and the credit left, and Set balance reconciles with your bank; pots (e.g. Monzo pots) nest inside their account
- **Transactions** — record income, expenses, savings and transfers between your accounts, with categories and notes
- **Recurring Transactions** — auto-generates transactions on app launch (salary, subscriptions, rent)
- **Monthly Budgets** — set spending limits per category per month, copy budgets to next month
- **Goals** — savings targets with auto-calculated progress from linked category transactions
- **Dashboard** — monthly summary with balance, income/expenses/savings, an accounts strip with the total available, a spending-by-category donut with budget progress, overspend warnings
- **Preferences** — System/Light/Dark theme, income attribution toggle (income funds next month's budget), hide-amounts privacy mode
- **Filtering** — search transactions by description/notes, filter by category, type or account
- **Backup & export** — back up the whole database to Files/iCloud/Drive via the share sheet, restore it (checked, upgraded, with a safety copy of current data), export transactions as CSV; a dashboard reminder after 30 days without a backup
- **Delete protection** — prevents deletion of categories, currencies and accounts that are in use (accounts can be archived instead)

## Tech Stack

- **.NET 10** / C#
- **MAUI Blazor Hybrid** — cross-platform (iOS, Android, Mac Catalyst, Windows)
- **MudBlazor 9** — behaviour layer (inputs, dialogs, charts) under the Ledger Noir design system (`docs/`)
- **Entity Framework Core** — SQLite local database with migrations
- **Aspire** — orchestration for multi-device development (AppHost with dev tunnels)

## Project Structure

```
SpendSense/
├── App/SpendSense/              # MAUI Blazor app
│   ├── Common/
│   │   ├── Data/                # DbContext, repositories, interceptors
│   │   ├── Models/              # Entity models and enums
│   │   ├── Interfaces/          # Shared interfaces
│   │   └── Services/            # SettingsService, RecurringTransactionGenerator
│   ├── Components/
│   │   ├── Layout/              # MainLayout, TabsLayout, FocusLayout, nav
│   │   ├── Shared/              # Design-system components (Money, ListRow, FormShell, …)
│   │   └── Pages/               # Page components (CRUD, Dashboard, Trends, Preferences)
│   ├── Migrations/              # EF Core migrations
│   └── MauiProgram.cs           # App startup and DI
├── docs/                        # ADRs and the design-token contract
├── Infra/
│   ├── SpendSense.AppHost/      # Aspire orchestration
│   └── SpendSense.ServiceDefaults/ # Shared service configuration
└── ROADMAP.md                   # Future plans
```

## Getting Started

### Prerequisites

- .NET 10 SDK
- MAUI workload (`dotnet workload install maui`)
- For iOS: Xcode (Mac only)
- For Android: Android SDK

### Run with Aspire

```bash
aspire start
```

Run from the repo root — `aspire.config.json` points the Aspire CLI at the AppHost project.

### Run standalone (Mac Catalyst)

```bash
cd App/SpendSense
dotnet build -f net10.0-maccatalyst
dotnet run -f net10.0-maccatalyst
```

### Add a migration

```bash
cd App/SpendSense
dotnet ef migrations add <MigrationName> --framework net10.0
```

## Architecture Decisions

- **SQLite** — local-first, works offline, no server dependency
- **Repository pattern** — thin layer over DbContext for testability
- **SaveChanges interceptors** — auto-set CreatedAt/UpdatedAt timestamps
- **Preferences API** — persists settings (theme, active budget period) via platform-native storage
- **Recurring transaction generation** — runs synchronously on app startup after migrations
- **Enum-to-string storage** — enums stored as readable strings in SQLite for debuggability
- **Computed account balances** — opening balance plus transactions, never stored, so edits can't leave a balance out of sync; transfers never count toward budgets ([ADR 0002](docs/adr/0002-accounts.md))

## Roadmap

See [ROADMAP.md](ROADMAP.md) for planned features including bank statement import, spending trends, cloud sync, and more.
