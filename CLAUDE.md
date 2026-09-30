# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What this is

SpendSense is a personal budgeting app built with .NET MAUI Blazor Hybrid (.NET 10) and Aspire for
multi-device dev orchestration. It's a local-first app — all data lives in an on-device SQLite
database via EF Core; there is no backend (yet — see `ROADMAP.md` for planned cloud sync).

## Commands

There are no test projects in this repo — verification is build + manual run.

### Run with Aspire (orchestrates iOS simulator, Android emulator/device, Mac Catalyst)

Use the Aspire CLI from the repo root — `aspire.config.json` points it at the AppHost project, so it
does not need a `cd`:

```bash
aspire run
```

(`cd Infra/SpendSense.AppHost && dotnet run` also launches the AppHost, but skips the Aspire CLI's
dashboard/orchestration tooling that `aspire run` provides — prefer `aspire run`.)

### Run standalone (Mac Catalyst — fastest inner loop on macOS)

```bash
cd App/SpendSense
dotnet build -f net10.0-maccatalyst
dotnet run -f net10.0-maccatalyst
```

Other target frameworks: `net10.0-android`, `net10.0-ios`, `net10.0-windows10.0.19041.0` (Windows host only).

### EF Core migrations

```bash
cd App/SpendSense
dotnet ef migrations add <MigrationName> --framework net10.0
```

Migrations run automatically on app startup (`app.RunDatabaseMigrations()` in `MauiProgram.cs`), not via `dotnet ef database update`.
`Program.cs` at the app root exists solely to give `dotnet ef` a design-time entry point (`MyDbContextFactory`) — it's not the app's real entry point.

## Architecture

### Startup pipeline (`MauiProgram.cs`)

DI registration happens through the `AddSpendSenseDb()` extension (`Common/Data/SpendSenseDbExtensions.cs`),
which wires up the SQLite `DbContext` (with `AddEntityInterceptor`/`ModifyEntityInterceptor` for
timestamps), all repositories, and points EF at `{FileSystem.AppDataDirectory}/SpendSense/SpendSense.db`.
After `builder.Build()`, three extension methods run in sequence and each swallows its own exceptions
(logged via `Console.WriteLine`, never crashes startup):
1. `RunDatabaseMigrations()` — applies pending EF migrations
2. `GenerateRecurringTransactions()` — materializes due recurring transactions (salary, rent, etc.)
3. `CheckNotifications()` — evaluates budget threshold / goal milestone alerts and fires local notifications

### Data layer conventions

- **Repository pattern**: one repository per aggregate under `Common/Data/Repositories/`, injected directly
  into Razor pages via `@inject`. Repositories are thin — `GetAll`/`GetById`/`Add`/`Update`/`Delete` with
  `.Include()` for the relations a page needs. Pages that need ad-hoc queries (e.g. grouping, suggestions)
  inject `SpendSenseDbContext` directly rather than growing the repository — see the description-based
  category-suggestion query in `Components/Pages/Transactions/Add.razor`.
- **Timestamps**: entities implement `ITimestamped` (`CreatedAt`/`UpdatedAt`); `AddEntityInterceptor` and
  `ModifyEntityInterceptor` (SaveChanges interceptors) set these automatically — never set them manually.
- **Enums stored as strings**: every enum property (`TransactionTypeEnum`, `FrequencyEnum`, `PriorityEnum`,
  `StatusEnum`) uses `.HasConversion(v => v.ToString(), v => Enum.Parse<T>(v))` in
  `SpendSenseDbContext.OnModelCreating`, for SQLite debuggability. Follow this pattern for new enums.
- **Unique indexes** are declared in `OnModelCreating`, not via attributes (e.g. `MonthlyBudget` on
  `Year+Month+CategoryId+CurrencyId`, `Category.Name`, `Currency.Code`, `Tag.Name`) — check there before
  adding a field that should be unique.
- Default currency (GBP) is seeded via `HasData` in `OnModelCreating`.

### UI layer

- Blazor pages live under `Components/Pages/<Entity>/{Listing,Add,Edit}.razor`, following the same
  three-file CRUD shape per entity (Transactions, Categories, Currencies, Goals, MonthlyBudgets,
  RecurringTransactions). New CRUD entities should follow this shape.
- MudBlazor (v9) is the component library — forms use `EditForm` + `MudTextField`/`MudSelect`/etc.,
  injected repositories are called directly from `@code` blocks (no separate view-model/service layer
  between pages and repositories).
- `SettingsService` wraps MAUI `Preferences` (not the database) for device-local settings: dark mode,
  active budget year/month, income-attribution toggle, hide-amounts privacy mode. Use this — not the
  DB — for anything that's a per-device UI preference rather than budgeting data.
- `Home.razor` is the dashboard; `Trends.razor` is the separate spending-trends page.

### Cross-cutting services (`Common/Services/`)

- `RecurringTransactionGenerator` — generates due transactions from `RecurringTransaction` records on
  every app launch; also invoked manually where recurring transactions are edited.
- `NotificationService` — evaluates budget threshold (80%/100%) and goal milestone (50%/100%) alerts
  using `Plugin.LocalNotification`, and persists "already notified" state via `Preferences` so alerts
  don't repeat. Called both at startup and after any transaction add (see `Transactions/Add.razor`).

### Aspire / Infra

`Infra/SpendSense.AppHost` is only a dev-orchestration host (`AppHost.cs` wires up iOS simulator,
Android emulator/device, Mac Catalyst targets via `AddMauiProject`) — it has no runtime role in the
shipped app. `Infra/SpendSense.ServiceDefaults` wires OpenTelemetry/service-discovery/resilience for
both AppHost-style and MAUI builders; it's referenced by the MAUI app project itself, not just AppHost.

## Roadmap context

See `ROADMAP.md` for planned work (CSV/PDF bank import, receipt attachments, cloud sync via API
Gateway + Lambda + DynamoDB + Cognito). The Architecture Decisions section of `README.md` documents
why choices like SQLite-local-first and repository pattern were made.
