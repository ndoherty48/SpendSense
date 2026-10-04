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
            .Include(a => a.ParentAccount)
            .Where(a => includeArchived || !a.IsArchived)
            .ToListAsync();
        return accounts.InDisplayOrder().ToList();
    }

    public async Task<Account?> GetById(int id)
    {
        return await dbContext.Accounts
            .Include(a => a.Currency)
            .Include(a => a.ParentAccount)
            .FirstOrDefaultAsync(a => a.Id == id);
    }

    /// <summary>The account pre-selected on new transactions; falls back to the first open account.</summary>
    public async Task<Account?> GetDefault()
    {
        return await dbContext.Accounts.FirstOrDefaultAsync(a => a.IsDefault && !a.IsArchived)
            ?? await dbContext.Accounts.Where(a => !a.IsArchived).OrderBy(a => a.Id).FirstOrDefaultAsync();
    }

    /// <summary>Account names are unique (case-insensitively, so "Visa" and "visa" don't both exist).</summary>
    public async Task<bool> NameExists(string name, int? exceptId = null)
    {
        var trimmed = name.Trim().ToLower();
        return await dbContext.Accounts.AnyAsync(a => a.Name.ToLower() == trimmed && a.Id != exceptId);
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

    /// <summary>True when transactions, recurring rules (as source or destination) or pots reference the account.</summary>
    public async Task<bool> HasDependencies(int accountId)
    {
        return await dbContext.Transactions.AnyAsync(t => t.AccountId == accountId || t.ToAccountId == accountId)
            || await dbContext.RecurringTransactions.AnyAsync(r => r.AccountId == accountId || r.ToAccountId == accountId)
            || await dbContext.Accounts.AnyAsync(a => a.ParentAccountId == accountId);
    }

    /// <summary>Any pots inside the account, archived or not (it then can't become a pot itself).</summary>
    public async Task<bool> HasPots(int accountId)
    {
        return await dbContext.Accounts.AnyAsync(a => a.ParentAccountId == accountId);
    }

    /// <summary>Open pots inside the account (it then can't be archived).</summary>
    public async Task<bool> HasOpenPots(int accountId)
    {
        return await dbContext.Accounts.AnyAsync(a => a.ParentAccountId == accountId && !a.IsArchived);
    }

    /// <summary>
    /// Accounts that can hold pots: open, top-level, not a card, and not <paramref name="exceptId"/>. The form
    /// further limits them to the pot's currency.
    /// </summary>
    public async Task<IReadOnlyList<Account>> GetParentOptions(int? exceptId = null)
    {
        var accounts = await dbContext.Accounts
            .Where(a => !a.IsArchived && a.ParentAccountId == null && a.Type != AccountTypeEnum.CreditCard && a.Id != exceptId)
            .ToListAsync();
        return accounts.InDisplayOrder().ToList();
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
    /// name. Pots follow straight after their parent. Done in memory: the type is stored as text, so SQL
    /// would sort it alphabetically.
    /// </summary>
    public static IEnumerable<Account> InDisplayOrder(this IEnumerable<Account> accounts)
    {
        var list = accounts.ToList();
        var ids = list.Select(a => a.Id).ToHashSet();
        foreach (var top in Sort(list.Where(a => IsTopLevel(a, ids))))
        {
            yield return top;
            foreach (var pot in Sort(list.Where(a => a.ParentAccountId == top.Id)))
                yield return pot;
        }
    }

    /// <summary>
    /// Top-level within <paramref name="ids"/>: no parent, or a parent that isn't in the set (e.g. archived),
    /// in which case the pot stands on its own.
    /// </summary>
    public static bool IsTopLevel(Account account, IReadOnlySet<int> ids) =>
        account.ParentAccountId is not { } parent || !ids.Contains(parent);

    static IOrderedEnumerable<Account> Sort(IEnumerable<Account> accounts) =>
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
