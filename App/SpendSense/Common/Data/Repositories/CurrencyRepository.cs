using Microsoft.EntityFrameworkCore;

using SpendSense.Common.Models;

namespace SpendSense.Common.Data.Repositories;

public sealed class CurrencyRepository(SpendSenseDbContext dbContext)
{
    public async Task<IReadOnlyCollection<Currency>> GetAll()
    {
        return await dbContext.Currencies.ToListAsync();
    }

    public async Task<int> Add(Currency currency)
    {
        dbContext.Currencies.Add(currency);
        return await dbContext.SaveChangesAsync();
    }

    public async Task<int> SetDefault(Currency currency)
    {
        dbContext.Currencies.FirstOrDefault(x=>x.IsDefault)?.IsDefault = false;
        dbContext.Currencies.FirstOrDefault(x=>x.Name == currency.Name)?.IsDefault = true;

        if(dbContext.ChangeTracker.Entries().Count() is 2)
            return await dbContext.SaveChangesAsync();
        dbContext.ChangeTracker.Clear();
        return 0;
    }

    public async Task<bool> HasDependencies(int currencyId)
    {
        return await dbContext.Transactions.AnyAsync(t => t.CurrencyId == currencyId)
            || await dbContext.RecurringTransactions.AnyAsync(r => r.CurrencyId == currencyId)
            || await dbContext.MonthlyBudgets.AnyAsync(mb => mb.CurrencyId == currencyId)
            || await dbContext.Goals.AnyAsync(g => g.CurrencyId == currencyId);
    }

    public async Task<int> Delete(Currency currency)
    {
        dbContext.Currencies.Remove(currency);
        return await dbContext.SaveChangesAsync();
    }
}