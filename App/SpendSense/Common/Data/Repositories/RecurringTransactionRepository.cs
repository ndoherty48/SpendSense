using Microsoft.EntityFrameworkCore;

using SpendSense.Common.Models;

namespace SpendSense.Common.Data.Repositories;

public sealed class RecurringTransactionRepository(SpendSenseDbContext dbContext)
{
    public async Task<IReadOnlyCollection<RecurringTransaction>> GetAll()
    {
        return await dbContext.RecurringTransactions
            .Include(r => r.Category)
            .Include(r => r.Currency)
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync();
    }

    public async Task<int> Add(RecurringTransaction recurring)
    {
        dbContext.RecurringTransactions.Add(recurring);
        return await dbContext.SaveChangesAsync();
    }

    public async Task<int> Delete(RecurringTransaction recurring)
    {
        dbContext.RecurringTransactions.Remove(recurring);
        return await dbContext.SaveChangesAsync();
    }
}
