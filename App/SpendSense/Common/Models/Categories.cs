using System.ComponentModel.DataAnnotations;

using SpendSense.Common.Interfaces;
using SpendSense.Common.Models.Enums;

namespace SpendSense.Common.Models;

public class Category : ITimestamped
{
    [Key]
    public int Id { get; set; }
    [Required, MaxLength(100)]
    public required string Name { get; set; }
    [Required]
    public required TransactionTypeEnum Type { get; set; }
    [MaxLength(9)]
    public string? Color { get; set; }
    [MaxLength(50)]
    public string? Icon { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    // Navigation Properties
    public ICollection<Transaction> Transactions {get;set;} = [];
    public ICollection<MonthlyBudget> MonthlyBudgets {get;set;} = [];
    public ICollection<Goal> Goals {get;set;} = [];
    public ICollection<RecurringTransaction> RecurringTransactions {get;set;} = [];
    public ICollection<Alert> Alerts {get;set;} = [];
}
