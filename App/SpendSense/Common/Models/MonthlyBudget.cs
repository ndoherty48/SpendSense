using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SpendSense.Common.Models;

public class MonthlyBudget
{
    [Key]
    public int Id { get; set; }
    public int Year { get; set; }
    [Range(1, 12)]
    public int Month { get; set; }
    public double BudgetedAmount { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public int CategoryId {get;set;}
    public int CurrencyId {get;set;}

    [ForeignKey(nameof(CategoryId))]
    public required Category Category { get; set; }
    [ForeignKey(nameof(CurrencyId))]
    public required Currency Currency { get; set; }
}
