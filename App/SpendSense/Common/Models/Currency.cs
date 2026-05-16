using System.ComponentModel.DataAnnotations;

namespace SpendSense.Common.Models;

public class Currency
{
    [Key]
    public int Id { get; set; }
    [Required, MaxLength(3)]
    public string? Code { get; set; }
    [Required, MaxLength(50)]
    public string? Name { get; set; }
    [Required, MaxLength(5)]
    public string? Symbol { get; set; }
    public double ExchangeRate { get; set; } = 1.0;
    public bool IsDefault { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    // Navigation Properties
    public ICollection<Transaction> Transactions {get;set;} = [];
    public ICollection<MonthlyBudget> MonthlyBudgets {get;set;} = [];
    public ICollection<Goal> Goals {get;set;} = [];
    public ICollection<RecurringTransaction> RecurringTransactions {get;set;} = [];
}
