# SpendSense

A personal budgeting app: money moving into, out of and between your own accounts, measured against monthly budgets.

## Language

### Moving money

**Transfer**:
Money moved between two of your own accounts. Never counts as income, spending or savings.
_Avoid_: Move, payment

**Move**:
A transfer, or savings paid into one of your accounts: money that left one account and stayed yours.

**Amount**:
What left the source account, in the source account's currency. This is the figure budgets count.
_Avoid_: Amount sent, source amount

**Amount received**:
What arrived in the destination account of a move, in that account's currency. Differs from the amount only when the two accounts use different currencies.
_Avoid_: Converted amount, target amount

**Rate**:
Amount received divided by amount for one cross-currency move. Derived, never stored.
_Avoid_: Exchange rate (as a stored setting)
