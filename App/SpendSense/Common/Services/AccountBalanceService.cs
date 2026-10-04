using Microsoft.EntityFrameworkCore;

using SpendSense.Common.Data;
using SpendSense.Common.Data.Repositories;
using SpendSense.Common.Models;
using SpendSense.Common.Models.Enums;

namespace SpendSense.Common.Services;

/// <summary>An account and its computed balance. A card that owes £600 has a balance of −600.</summary>
public sealed record AccountBalance(Account Account, double Balance)
{
    public bool IsCard => Account.Type == AccountTypeEnum.CreditCard;

    /// <summary>What a card owes; zero for other accounts and for a card in credit.</summary>
    public double Owed => IsCard ? Math.Max(0, -Balance) : 0;

    /// <summary>Credit left on a card with a limit; null otherwise.</summary>
    public double? AvailableCredit => IsCard && Account.CreditLimit is { } limit ? Math.Max(0, limit - Owed) : null;

    /// <summary>Percentage of a card's limit in use; null without a limit.</summary>
    public double? CreditUsedPercent => IsCard && Account.CreditLimit is > 0 ? Owed / Account.CreditLimit.Value * 100 : null;

    public string Symbol => Account.Currency?.Symbol ?? "£";
}

/// <summary>A top-level account and the pots inside it.</summary>
public sealed record AccountGroup(AccountBalance Account, IReadOnlyList<AccountBalance> Pots)
{
    /// <summary>The account plus its pots, e.g. everything in Monzo.</summary>
    public double Total => Account.Balance + Pots.Sum(p => p.Balance);

    public bool HasPots => Pots.Count > 0;
}

/// <summary>A total in a currency other than the default one.</summary>
public sealed record CurrencyTotal(string Symbol, double Amount);

/// <summary>The dashboard's Available figure and its parts, in the default currency.</summary>
public sealed record AvailableSummary(
    double Available,
    double InAccounts,
    double OwedOnCards,
    double Savings,
    string Symbol,
    IReadOnlyList<CurrencyTotal> OtherCurrencies);

/// <summary>
/// Computes account balances from <see cref="Account.OpeningBalance"/> plus every transaction dated today
/// or earlier. Balances are never stored, so edits and deletes can't leave them out of sync.
/// </summary>
public class AccountBalanceService(SpendSenseDbContext db, SettingsService settings)
{
    /// <summary>How much <paramref name="transaction"/> changes <paramref name="accountId"/>'s balance.</summary>
    public static double Delta(Transaction transaction, int accountId)
    {
        var delta = 0.0;
        if (transaction.AccountId == accountId)
            delta += transaction.TransactionType == TransactionTypeEnum.Income ? transaction.Amount : -transaction.Amount;
        if (transaction.ToAccountId == accountId)
            delta += transaction.Amount;
        return delta;
    }

    public async Task<IReadOnlyList<AccountBalance>> GetBalances(bool includeArchived = false)
    {
        var accounts = await db.Accounts
            .Include(a => a.Currency)
            .Include(a => a.ParentAccount)
            .Where(a => includeArchived || !a.IsArchived)
            .ToListAsync();

        var movements = await GetMovements();
        return accounts
            .InDisplayOrder()
            .Select(a => new AccountBalance(a, a.OpeningBalance + movements.GetValueOrDefault(a.Id)))
            .ToList();
    }

    public async Task<AccountBalance?> GetBalance(int accountId)
    {
        var account = await db.Accounts
            .Include(a => a.Currency)
            .Include(a => a.ParentAccount)
            .FirstOrDefaultAsync(a => a.Id == accountId);
        if (account is null)
            return null;

        var movements = await GetMovements(accountId);
        return new AccountBalance(account, account.OpeningBalance + movements.GetValueOrDefault(account.Id));
    }

    /// <summary>
    /// Groups balances (already in display order) into top-level accounts with their pots. A pot whose parent
    /// isn't in <paramref name="balances"/> (e.g. the parent is archived) stands on its own.
    /// </summary>
    public static IReadOnlyList<AccountGroup> Group(IReadOnlyList<AccountBalance> balances)
    {
        var ids = balances.Select(b => b.Account.Id).ToHashSet();
        return balances
            .Where(b => AccountOrdering.IsTopLevel(b.Account, ids))
            .Select(top => new AccountGroup(top, balances.Where(b => b.Account.ParentAccountId == top.Account.Id).ToList()))
            .ToList();
    }

    /// <summary>
    /// Accounts whose balance has probably never been checked against the bank: they have transactions but
    /// still start from £0. That's every account the AddAccounts migration filled with history, so their
    /// balance is just the net of everything recorded. Set balance clears it (the opening balance changes).
    /// </summary>
    public async Task<IReadOnlyList<AccountBalance>> NeedingBalanceCheck(IReadOnlyList<AccountBalance> balances)
    {
        var candidates = balances.Where(b => !b.Account.IsArchived && b.Account.OpeningBalance == 0).Select(b => b.Account.Id).ToList();
        if (candidates.Count == 0)
            return [];

        var used = await db.Transactions
            .Where(t => candidates.Contains(t.AccountId) || (t.ToAccountId != null && candidates.Contains(t.ToAccountId.Value)))
            .Select(t => new { t.AccountId, t.ToAccountId })
            .Distinct()
            .ToListAsync();
        var ids = used.Select(u => u.AccountId)
            .Concat(used.Where(u => u.ToAccountId != null).Select(u => u.ToAccountId!.Value))
            .ToHashSet();
        return balances.Where(b => candidates.Contains(b.Account.Id) && ids.Contains(b.Account.Id)).ToList();
    }

    /// <summary>Totals for the dashboard and the accounts list.</summary>
    public async Task<AvailableSummary> GetSummary(IReadOnlyList<AccountBalance> balances)
    {
        var defaultCurrency = await db.Currencies.FirstOrDefaultAsync(c => c.IsDefault);
        return Summarise(balances, defaultCurrency?.Id, defaultCurrency?.Symbol ?? "£", settings.IncludeCreditInAvailable);
    }

    public static AvailableSummary Summarise(IReadOnlyList<AccountBalance> balances, int? defaultCurrencyId, string symbol, bool includeCredit)
    {
        var open = balances.Where(b => !b.Account.IsArchived).ToList();
        var counted = open.Where(b => b.Account.IncludeInAvailable).ToList();

        double Available(IEnumerable<AccountBalance> items) =>
            items.Sum(b => b.Balance + (includeCredit ? b.AvailableCredit ?? 0 : 0));

        var home = counted.Where(b => b.Account.CurrencyId == defaultCurrencyId).ToList();
        var others = counted
            .Where(b => b.Account.CurrencyId != defaultCurrencyId)
            .GroupBy(b => b.Symbol)
            .Select(g => new CurrencyTotal(g.Key, Available(g)))
            .ToList();

        return new AvailableSummary(
            Available: Available(home),
            InAccounts: home.Where(b => !b.IsCard).Sum(b => b.Balance),
            OwedOnCards: home.Sum(b => b.Owed),
            Savings: open.Where(b => b.Account.Type == AccountTypeEnum.Savings && b.Account.CurrencyId == defaultCurrencyId).Sum(b => b.Balance),
            Symbol: symbol,
            OtherCurrencies: others);
    }

    /// <summary>
    /// Reconciles an account with the bank: changes the opening balance so the computed balance equals
    /// <paramref name="target"/>. No transaction is added, so budgets aren't affected.
    /// </summary>
    public async Task SetBalance(Account account, double target)
    {
        var movements = await GetMovements(account.Id);
        account.OpeningBalance = target - movements.GetValueOrDefault(account.Id);
        db.Accounts.Update(account);
        await db.SaveChangesAsync();
    }

    // Net movement per account: income in, expenses/savings/transfers out, savings/transfers received.
    async Task<Dictionary<int, double>> GetMovements(int? accountId = null)
    {
        var tomorrow = DateTime.Today.AddDays(1);
        var dated = db.Transactions.Where(t => t.TransactionDate < tomorrow);

        var source = await dated
            .Where(t => accountId == null || t.AccountId == accountId)
            .GroupBy(t => new { t.AccountId, t.TransactionType })
            .Select(g => new { g.Key.AccountId, g.Key.TransactionType, Total = g.Sum(t => t.Amount) })
            .ToListAsync();

        var incoming = await dated
            .Where(t => t.ToAccountId != null && (accountId == null || t.ToAccountId == accountId))
            .GroupBy(t => t.ToAccountId!.Value)
            .Select(g => new { AccountId = g.Key, Total = g.Sum(t => t.Amount) })
            .ToListAsync();

        var result = new Dictionary<int, double>();
        foreach (var row in source)
            result[row.AccountId] = result.GetValueOrDefault(row.AccountId)
                + (row.TransactionType == TransactionTypeEnum.Income ? row.Total : -row.Total);
        foreach (var row in incoming)
            result[row.AccountId] = result.GetValueOrDefault(row.AccountId) + row.Total;
        return result;
    }
}
