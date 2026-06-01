using Microsoft.EntityFrameworkCore;

using SpendSense.Common.Models;

namespace SpendSense.Common.Data.Repositories;

public sealed class MonthlyBudgetRepository(SpendSenseDbContext dbContext)
{
    public async Task<IReadOnlyCollection<MonthlyBudget>> GetAll()
    {
        return await dbContext.MonthlyBudgets
            .Include(mb => mb.Category)
            .Include(mb => mb.Currency)
            .OrderByDescending(mb => mb.Year)
            .ThenByDescending(mb => mb.Month)
            .ToListAsync();
    }

    public async Task<MonthlyBudget?> GetById(int id)
    {
        return await dbContext.MonthlyBudgets
            .Include(mb => mb.Category)
            .Include(mb => mb.Currency)
            .FirstOrDefaultAsync(mb => mb.Id == id);
    }

    public async Task<int> Add(MonthlyBudget budget)
    {
        dbContext.MonthlyBudgets.Add(budget);
        return await dbContext.SaveChangesAsync();
    }

    public async Task<int> Update(MonthlyBudget budget)
    {
        dbContext.MonthlyBudgets.Update(budget);
        return await dbContext.SaveChangesAsync();
    }

    public async Task<int> Delete(MonthlyBudget budget)
    {
        dbContext.MonthlyBudgets.Remove(budget);
        return await dbContext.SaveChangesAsync();
    }

    public async Task<int> CopyMonth(int fromYear, int fromMonth, int toYear, int toMonth)
    {
        var source = await dbContext.MonthlyBudgets
            .Where(mb => mb.Year == fromYear && mb.Month == fromMonth)
            .ToListAsync();

        var existing = await dbContext.MonthlyBudgets
            .Where(mb => mb.Year == toYear && mb.Month == toMonth)
            .Select(mb => mb.CategoryId)
            .ToListAsync();

        foreach (var budget in source.Where(b => !existing.Contains(b.CategoryId)))
        {
            dbContext.MonthlyBudgets.Add(new MonthlyBudget
            {
                Year = toYear,
                Month = toMonth,
                CategoryId = budget.CategoryId,
                CurrencyId = budget.CurrencyId,
                BudgetedAmount = budget.BudgetedAmount
            });
        }

        return await dbContext.SaveChangesAsync();
    }
}
