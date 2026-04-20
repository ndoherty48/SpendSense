
using System.ComponentModel.DataAnnotations;

namespace SpendSense.Common.Models;

public class Income
{
    [Key]
    public int Id { get; set; }
    public string? Name { get; set; }
    public string? Description { get; set; }
    public double? Amount { get; set; }
    public DateOnly IncomeDate { get; set; }

    public required MonthlyBudget MonthlyBudget { get; set; }
}
