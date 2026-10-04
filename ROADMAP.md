# SpendSense Roadmap

## Completed

### Spending Trends ✅
- Dedicated "Trends" page with configurable range (3/6/12 months)
  - Total spending line chart
  - Spending by category bar chart
- Dashboard one-liner: "You spent X% more/less than last month"

### Local Notifications ✅
- Budget threshold alerts (80% and 100% spent) — triggered after adding transactions and on app launch
- Goal milestones (50% and 100%) — triggered after adding transactions and on app launch
- Uses Plugin.LocalNotification for on-device notifications (no backend needed)
- Persists notified state via Preferences to avoid repeat notifications

### Category Auto-Suggestion ✅
- As user types a transaction description, queries past transactions for the most-used category
- Pre-selects the suggested category in the dropdown (user can override)
- Debounced at 500ms, triggers after 3+ characters

### Predictive Overspend Warnings ✅
- Linear projection on dashboard: `(spentSoFar / daysElapsed) * daysInMonth`
- Shows warning when projected spend exceeds budget by >10%
- Only after day 5 of the month (avoids noisy early projections)
- Recalculates on every dashboard load — adjusts as spending pace changes

### Accounts ✅
- Current accounts, savings, credit cards and cash, each with a computed balance
- Every transaction is paid from (or into) an account; transfers move money between accounts without touching budgets
- Credit cards show what's owed and the credit left against an optional limit
- Dashboard accounts strip with an Available total; Set balance reconciles an account with the bank
- Design: [ADR 0002](docs/adr/0002-accounts.md)

## Near Term

### Bank Statement CSV Import
- Import transactions from CSV files exported from banking apps
- Map CSV columns to transaction fields (date, description, amount)
- Auto-categorize based on description pattern matching against existing categories
- Review & confirm screen before committing imported transactions
- Import into a chosen account (the statement's account), then reconcile with Set balance

### Bank Statement PDF Import
- Extract transaction data from PDF bank statements using text extraction (PdfPig)
- Pattern matching/regex for dates, descriptions, amounts
- Start with a single bank format, expand as needed
- Falls back to manual entry if parsing fails

### Receipt Attachments
- Attach photos (camera or gallery) to transactions
- Store images in local app storage (`FileSystem.AppDataDirectory`)
- Display thumbnail on transaction listing/detail
- Use MAUI `MediaPicker` for capture/selection
- Future: OCR to auto-fill amount/description from receipt photo

### Monthly Summary Report
- In-app summary view for a completed month
- Total income, expenses, savings, balance
- Top spending categories
- Budget adherence (which categories were over/under)
- Optional: export as PDF or share

## Medium Term

### API Backend & Cloud Sync
- API Gateway + Lambda (ASP.NET minimal API via `Amazon.Lambda.AspNetCoreServer`)
- DynamoDB single-table design, partitioned by user (`PK: USER#<id>`, `SK: ENTITY#<id>`)
- S3 for receipt image storage (SSE-S3 encryption)
- Cognito for authentication (sign-up/sign-in, JWT validation at API Gateway)
- SQLite remains on-device for offline-first, syncs to DynamoDB when connected
- Sync strategy: last-write-wins using `UpdatedAt` timestamp
- Data protection: DynamoDB encryption at rest (default), HTTPS in transit, Cognito scopes user data access, avoid logging sensitive data to CloudWatch

### Multi-User / Household (Stretch Goal)
- Shared budgets between household members
- Per-user transactions that roll up into shared categories
- Household entity with invitation/join flow
- Requires backend and careful sync/permissions design
- Not planned for near/medium term

### Accounts follow-ups
- Link a goal to a savings account so its progress is the account balance (instead of a manual current amount)
- Cross-currency transfers (an amount on each side) and converted totals using `ExchangeRate`
- Credit card statement cycles and payment-due reminders

## Nice to Have

### Onboarding Wizard
- First-launch flow: set up categories, default currency, first monthly budget, income
- Pre-populate common categories (Groceries, Bills, Transport, Entertainment, Eating Out, Subscriptions)

### Web Companion App
- Share Blazor components via Blazor Server/WASM
- View-only dashboard accessible from desktop browser
- Requires backend (above)

### Data Export
- CSV export of transactions for a date range
- Filter by category, type, or date range before export
