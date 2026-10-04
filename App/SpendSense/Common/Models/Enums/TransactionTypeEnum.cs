namespace SpendSense.Common.Models.Enums;

public enum TransactionTypeEnum
{
    Income,
    Expense,
    Savings,
    /// <summary>Moves money between two of your own accounts; never counts toward budgets.</summary>
    Transfer
}
