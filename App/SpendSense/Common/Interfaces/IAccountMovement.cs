using SpendSense.Common.Models;
using SpendSense.Common.Models.Enums;

namespace SpendSense.Common.Interfaces;

/// <summary>
/// Money moving into, out of or between accounts: a transaction or a recurring rule. Lets the account
/// pickers, validation and list visuals work on both.
/// </summary>
public interface IAccountMovement
{
    public TransactionTypeEnum TransactionType { get; set; }
    public double Amount { get; set; }
    public int AccountId { get; set; }
    public int? ToAccountId { get; set; }
    public Account? Account { get; }
    public Account? ToAccount { get; }
    public Category? Category { get; }
}
