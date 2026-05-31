# SpendSense Roadmap

## Near Term

### Bank Statement CSV Import
- Import transactions from CSV files exported from banking apps
- Map CSV columns to transaction fields (date, description, amount)
- Auto-categorize based on description pattern matching against existing categories
- Review & confirm screen before committing imported transactions

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

### Spending Trends
- Dedicated "Trends" page (keeps dashboard clean)
  - Month-over-month bar chart: spending per category across months, spot categories creeping up
  - Total spending trend line over 6-12 months, one data point per month
- Dashboard one-liner: "You spent X% more/less than last month" — quick context without clutter
- All derived from existing transaction data, no new collection needed

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

### Category Auto-Suggestion
- As user types a transaction description, query past transactions for the most-used category with similar descriptions
- Pre-select the suggested category in the dropdown (user can override)
- No ML — simple frequency-based lookup from transaction history
- Gets smarter as the user adds more transactions
- Especially useful after bank statement imports (repetitive merchant names)

### Predictive Overspend Warnings
- Linear projection: `(spentSoFar / daysElapsed) * daysInMonth`
- Recalculates on every dashboard load — adjusts as spending pace changes
- Show warning on dashboard when projected spend exceeds budget by >10%
- Warning disappears if spending slows down and projection drops below budget
- Only show after first few days of the month (early projections are noisy)
- Only for categories with a monthly budget set

## Nice to Have

### Push Notifications
- Budget threshold alerts (80% spent, 100% spent)
- Recurring transaction reminders
- Weekly spending summary ("You spent £320 this week across 14 transactions")
- Goal milestones ("You're 50% of the way to Holiday Fund! 🎉")
- Requires background service or scheduled local notifications

### Onboarding Wizard
- First-launch flow: set up categories, default currency, first monthly budget, income
- Pre-populate common categories (Groceries, Bills, Transport, Entertainment, Eating Out, Subscriptions)

### Web Companion App
- Share Blazor components via Blazor Server/WASM
- View-only dashboard accessible from desktop browser
- Requires backend (above)

### Widgets (iOS/Android)
- Home screen widget showing current month balance
- Quick-add transaction from widget

### Data Export
- CSV export of transactions for a date range
- Filter by category, type, or date range before export
