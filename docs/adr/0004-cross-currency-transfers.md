# ADR 0004: Cross-currency transfers

- Status: Accepted
- Date: 2026-10-28
- Supersedes: [ADR 0002](0002-accounts.md), decision 4 (in part)

## Context

ADR 0002 required both accounts in a transfer to use the same currency, and listed cross-currency transfers
as out of scope. A transaction had a single `Amount`, which the balance calculation added to the destination
as well as taking it from the source, so £100 sent to a euro account would have arrived as €100.

People with a euro or dollar account do move money into it, and the bank statements show two different
figures: £100 left one account, €115.20 arrived in the other.

## Decisions

### 1. Store the amount received, not the rate

`Transaction.AmountReceived` and `RecurringTransaction.AmountReceived` (nullable) hold what arrived in the
destination account, in its currency. `Amount` is unchanged: what left the source account, in its
currency, and the figure budgets count.

The rate is never stored. Storing it and multiplying would introduce rounding (€115.199… against the
€115.20 the bank credited), and the two amounts are what both statements show, so balances match the bank
to the penny. The rate is derived as `AmountReceived / Amount` for display and input.

`AmountReceived` is null unless the two accounts' currencies differ, so existing rows need no data
migration. Anything that sums money arriving uses `AmountReceived ?? Amount`
(`AccountBalanceService.Delta` and the grouped balance query).

### 2. Transfers and savings with a destination

Both `Transfer` and `Savings` with a `ToAccountId` can cross currencies. Savings still count toward the
budget at `Amount`, in the source currency, as before.

### 3. The form: rate and amount received, linked

When From and To differ in currency, the transaction and recurring forms show two linked fields: **Rate**,
read as "1 GBP = 1.1520 EUR" (per one unit of the source currency, up to 4 decimal places), and **Amount
received** (2 decimal places). Editing either updates the other. Editing Amount keeps the rate and works
out the amount received again. Saving without an amount received shows "Enter the amount received or the
rate."

The rate starts from the **most recent move between the same two currencies**, in either direction
(inverted if it went the other way), and starts blank when there hasn't been one. Swapping the accounts
swaps the two amounts, so the same money goes the other way and the rate inverts.

### 4. `Currency.ExchangeRate` is removed

It was editable on the Add Currency page and shown in the currency list but used in no calculation, and had
no stated base. A rate typed once goes stale, and the last transfer's rate is a better suggestion. Totals
still don't convert between currencies (ADR 0002, decision 5); doing so would need a live rate source,
not a stored setting.

### 5. Recurring rules copy a fixed amount received

A recurring transfer into another currency stores a fixed amount received, which each generated transaction
copies, the same way it copies a fixed `Amount`. The real figure varies with the rate, so the user corrects
it when the statement arrives. The app never makes up a rate.

### 6. Display and export

- Activity, the dashboard and the recurring list show both sides: "£100.00 → €115.20".
- An account's detail page shows only that account's side, in its own currency, and uses it for the
  running balance and the month's in/out.
- The CSV export gains `Amount received` and `Received currency` as its last two columns, filled in only for
  moves between currencies (ADR 0003).

## Consequences

- Any new code that totals money arriving in an account must use `AmountReceived ?? Amount`, never `Amount`
  alone.
- Removing `ExchangeRate` drops a column. Downgrading the migration restores it with the default 1.0 for GBP
  and 0 for the others; nothing reads it.
- Out of scope: converted totals across currencies, live exchange rates, fees as a separate amount (a fee
  is the difference between the two amounts, or a separate expense).
