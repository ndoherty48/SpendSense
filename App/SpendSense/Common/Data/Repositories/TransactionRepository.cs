using Microsoft.EntityFrameworkCore;

using SpendSense.Common.Models;

namespace SpendSense.Common.Data.Repositories;

public sealed class TransactionRepository(SpendSenseDbContext dbContext)
{
    public async Task<IReadOnlyCollection<Transaction>> GetAll()
    {
        return await dbContext.Transactions
            .Include(t => t.Category)
            .Include(t => t.Currency)
            .Include(t => t.Account)
            .Include(t => t.ToAccount)
            .OrderByDescending(t => t.TransactionDate)
            .ToListAsync();
    }

    public async Task<Transaction?> GetById(int id)
    {
        return await dbContext.Transactions
            .Include(t => t.Category)
            .Include(t => t.Currency)
            .FirstOrDefaultAsync(t => t.Id == id);
    }

    public async Task<int> Add(Transaction transaction)
    {
        dbContext.Transactions.Add(transaction);
        return await dbContext.SaveChangesAsync();
    }

    public async Task<int> Update(Transaction transaction)
    {
        dbContext.Transactions.Update(transaction);
        return await dbContext.SaveChangesAsync();
    }

    public async Task<int> Delete(Transaction transaction)
    {
        dbContext.Transactions.Remove(transaction);
        return await dbContext.SaveChangesAsync();
    }
}
