using Microsoft.EntityFrameworkCore;

using SpendSense.Common.Models;
using SpendSense.Common.Models.Enums;

namespace SpendSense.Common.Data.Repositories;

public sealed class AccountRepository(SpendSenseDbContext dbContext)
{
    /// <summary>All accounts in display order (see <see cref="AccountOrdering"/>).</summary>
    public async Task<IReadOnlyCollection<Account>> GetAll(bool includeArchived = true)
    {
        var accounts = await dbContext.Accounts
            .Include(a => a.Currency)
            .Where(a => includeArchived || !a.IsArchived)
            .ToListAsync();
        return accounts.InDisplayOrder().ToList();
    }

    public async Task<Account?> GetById(int id)
    {
        return await dbContext.Accounts
            .Include(a => a.Currency)
            .FirstOrDefaultAsync(a => a.Id == id);
    }

    /// <summary>The account pre-selected on new transactions; falls back to the first open account.</summary>
    public async Task<Account?> GetDefault()
    {
        return await dbContext.Accounts.FirstOrDefaultAsync(a => a.IsDefault && !a.IsArchived)
            ?? await dbContext.Accounts.Where(a => !a.IsArchived).OrderBy(a => a.Id).FirstOrDefaultAsync();
    }

    public async Task<int> Add(Account account)
    {
        if (account.IsDefault)
            await ClearDefault();
        dbContext.Accounts.Add(account);
        return await dbContext.SaveChangesAsync();
    }

    public async Task<int> Update(Account account)
    {
        if (account.IsDefault)
            await ClearDefault(except: account.Id);
        dbContext.Accounts.Update(account);
        return await dbContext.SaveChangesAsync();
    }

    /// <summary>True when transactions or recurring rules reference the account, as source or destination.</summary>
    public async Task<bool> HasDependencies(int accountId)
    {
        return await dbContext.Transactions.AnyAsync(t => t.AccountId == accountId || t.ToAccountId == accountId)
            || await dbContext.RecurringTransactions.AnyAsync(r => r.AccountId == accountId || r.ToAccountId == accountId);
    }

    public async Task<int> Delete(Account account)
    {
        dbContext.Accounts.Remove(account);
        return await dbContext.SaveChangesAsync();
    }

    async Task ClearDefault(int? except = null)
    {
        var current = await dbContext.Accounts.Where(a => a.IsDefault && a.Id != except).ToListAsync();
        foreach (var account in current)
            account.IsDefault = false;
    }
}

public static class AccountOrdering
{
    /// <summary>
    /// Default account first, then everyday money (current, cash), cards, savings; then the user's order and
    /// name. Done in memory: the type is stored as text, so SQL would sort it alphabetically.
    /// </summary>
    public static IEnumerable<Account> InDisplayOrder(this IEnumerable<Account> accounts) =>
        accounts
            .OrderByDescending(a => a.IsDefault)
            .ThenBy(a => Rank(a.Type))
            .ThenBy(a => a.SortOrder)
            .ThenBy(a => a.Name, StringComparer.CurrentCultureIgnoreCase);

    static int Rank(AccountTypeEnum type) => type switch
    {
        AccountTypeEnum.Current => 0,
        AccountTypeEnum.Cash => 1,
        AccountTypeEnum.CreditCard => 2,
        _ => 3
    };
}
