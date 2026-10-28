using Microsoft.EntityFrameworkCore;

using SpendSense.Common.Data;

namespace SpendSense.Common.Services;

/// <summary>
/// Moves between accounts in different currencies (ADR 0004). The amount received is what's stored; the rate is
/// only ever derived from it, quoted per one unit of the source currency.
/// </summary>
public static class CrossCurrency
{
    /// <summary>The rate as shown and typed: "1 GBP = 1.1520 EUR".</summary>
    public const int RateDecimals = 4;

    /// <summary>Amount received for <paramref name="amount"/> at <paramref name="rate"/>, rounded to pennies.</summary>
    public static double Received(double amount, double rate) => Math.Round(amount * rate, 2, MidpointRounding.AwayFromZero);

    /// <summary>The rate implied by the two amounts; null until both are positive.</summary>
    public static double? Rate(double amount, double? received) =>
        amount > 0 && received is > 0 ? Math.Round(received.Value / amount, RateDecimals, MidpointRounding.AwayFromZero) : null;

    /// <summary>
    /// The rate from the most recent move between the two currencies, in either direction (inverted if it went
    /// the other way). Null when there hasn't been one.
    /// </summary>
    public static async Task<double?> LastRate(SpendSenseDbContext db, int fromCurrencyId, int toCurrencyId)
    {
        var last = await db.Transactions
            .Where(t => t.AmountReceived > 0 && t.Amount > 0 && t.ToAccount != null)
            .Where(t => (t.Account!.CurrencyId == fromCurrencyId && t.ToAccount!.CurrencyId == toCurrencyId)
                || (t.Account!.CurrencyId == toCurrencyId && t.ToAccount!.CurrencyId == fromCurrencyId))
            .OrderByDescending(t => t.TransactionDate)
            .ThenByDescending(t => t.Id)
            .Select(t => new { t.Amount, Received = t.AmountReceived!.Value, Forward = t.Account!.CurrencyId == fromCurrencyId })
            .FirstOrDefaultAsync();
        if (last is null)
            return null;
        var rate = last.Forward ? last.Received / last.Amount : last.Amount / last.Received;
        return Math.Round(rate, RateDecimals, MidpointRounding.AwayFromZero);
    }
}
