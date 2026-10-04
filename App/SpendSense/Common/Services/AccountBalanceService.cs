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
        var account = await db.Accounts.Include(a => a.Currency).FirstOrDefaultAsync(a => a.Id == accountId);
        if (account is null)
            return null;

        var movements = await GetMovements(accountId);
        return new AccountBalance(account, account.OpeningBalance + movements.GetValueOrDefault(account.Id));
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
