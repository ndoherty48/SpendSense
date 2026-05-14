using System.ComponentModel.DataAnnotations;

namespace SpendSense.Common.Models;

public class Category
{
    [Key]
    public int Id { get; set; }
    [Required, MaxLength(100)]
    public required string Name { get; set; }
    [Required]
    public required TransactionTypeEnum Type { get; set; }
    [MaxLength(7)]
    public string? Color { get; set; }
    [MaxLength(50)]
    public string? Icon { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation Properties
    public ICollection<Transaction> Transactions {get;set;} = [];
    public ICollection<MonthlyBudget> MonthlyBudgets {get;set;} = [];
    public ICollection<Goal> Goals {get;set;} = [];
    public ICollection<RecurringTransaction> RecurringTransactions {get;set;} = [];
    public ICollection<Alert> Alerts {get;set;} = [];
}
