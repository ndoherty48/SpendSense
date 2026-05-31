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

    public async Task<int> Add(MonthlyBudget budget)
    {
        dbContext.MonthlyBudgets.Add(budget);
        return await dbContext.SaveChangesAsync();
    }

    public async Task<int> Delete(MonthlyBudget budget)
    {
        dbContext.MonthlyBudgets.Remove(budget);
        return await dbContext.SaveChangesAsync();
    }
}
