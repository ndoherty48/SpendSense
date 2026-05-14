using System.ComponentModel.DataAnnotations;

namespace SpendSense.Common.Models;

public class Currency
{
    [Key]
    public int Id { get; set; }
    [Required, MaxLength(3)]
    public required string Code { get; set; }
    [Required, MaxLength(50)]
    public required string Name { get; set; }
    [Required, MaxLength(5)]
    public required string Symbol { get; set; }
    public required double ExchangeRate { get; set; }
    public bool IsDefault { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation Properties
    public ICollection<Transaction> Transactions {get;set;} = [];
    public ICollection<MonthlyBudget> MonthlyBudgets {get;set;} = [];
    public ICollection<Goal> Goals {get;set;} = [];
    public ICollection<RecurringTransaction> RecurringTransactions {get;set;} = [];
}
