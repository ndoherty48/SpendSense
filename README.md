# SpendSense

A personal budgeting app built with .NET MAUI Blazor Hybrid and Aspire.

## Features

- **Transactions** — record income, expenses, and savings with categories, currencies, and notes
- **Recurring Transactions** — auto-generates transactions on app launch (salary, subscriptions, rent)
- **Monthly Budgets** — set spending limits per category per month, copy budgets to next month
- **Goals** — savings targets with auto-calculated progress from linked category transactions
- **Dashboard** — monthly summary with income/expenses/savings/balance cards, pie chart, spending by category table, overspend warnings
- **Settings** — dark mode, income attribution toggle (income funds next month's budget)
- **Filtering** — search transactions by description/notes, filter by category or type
- **Delete protection** — prevents deletion of categories/currencies that are in use

## Tech Stack

- **.NET 10** / C#
- **MAUI Blazor Hybrid** — cross-platform (iOS, Android, Mac Catalyst, Windows)
- **MudBlazor 9** — UI component library
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
│   │   ├── Layout/              # MainLayout, NavMenu
│   │   └── Pages/               # All page components (CRUD, Dashboard, Settings)
│   ├── Migrations/              # EF Core migrations
│   └── MauiProgram.cs           # App startup and DI
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

## Roadmap

See [ROADMAP.md](ROADMAP.md) for planned features including bank statement import, spending trends, cloud sync, and more.
