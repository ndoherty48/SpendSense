
using System.ComponentModel.DataAnnotations;

namespace SpendSense.Common.Models;

public class Savings
{
    [Key]
    public int Id { get; set; }
    public string? Name { get; set; }
    public string? Description { get; set; }
    public double? Amount { get; set; }

    public required MonthlyBudget MonthlyBudget { get; set; }
    public IEnumerable<SavingsTransaction> Transactions { get; set; } = [];
}

public class SavingsTransaction
{
    [Key]
    public int Id { get; set; }
    public string? Name { get; set; }
    public string? Description { get; set; }
    public double? Amount { get; set; }
    public DateOnly TransactionDate { get; set; }

    public required Savings Savings { get; set; }
}