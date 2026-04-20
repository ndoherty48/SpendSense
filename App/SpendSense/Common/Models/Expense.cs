
using System.ComponentModel.DataAnnotations;

namespace SpendSense.Common.Models;

public class Expense
{
    [Key]
    public int Id { get; set; }
    public string? Name { get; set; }
    public string? Description { get; set; }
    public double? Amount { get; set; }

    public required MonthlyBudget MonthlyBudget { get; set; }
    public IEnumerable<ExpenseTransaction> Transactions { get; set; } = [];
}

public class ExpenseTransaction
{
    [Key]
    public int Id { get; set; }
    public string? Name { get; set; }
    public string? Description { get; set; }
    public double? Amount { get; set; }
    public DateOnly TransactionDate { get; set; }

    public required Expense Expense { get; set; }
}