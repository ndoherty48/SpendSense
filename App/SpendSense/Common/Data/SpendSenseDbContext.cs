
using Microsoft.EntityFrameworkCore;
using SpendSense.Common.Models;

namespace SpendSense.Common.Data;

public class SpendSenseDbContext(DbContextOptions options) : DbContext(options)
{
    public DbSet<MonthlyBudget> Budgets { get; set; }
    public DbSet<Income> Incomes { get; set; }
    public DbSet<Expense> Expenses { get; set; }
    public DbSet<Savings> Savings { get; set; }
    public DbSet<SavingsTransaction> SavingsTransactions { get; set; }
    public DbSet<ExpenseTransaction> ExpenseTransactions { get; set; }
}