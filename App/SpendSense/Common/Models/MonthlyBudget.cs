
using System.ComponentModel.DataAnnotations;

namespace SpendSense.Common.Models;

public class MonthlyBudget
{
    [Key]
    public int Id { get; set; }
    public DateOnly PeriodStartDate { get; set; }
    public DateOnly PeriodEndDate { get; set; }
    public IEnumerable<Income> Incomes { get; set; } = [];
    public IEnumerable<Expense> Expenses { get; set; } = [];
    public IEnumerable<Savings> Savings { get; set; } = [];
}
