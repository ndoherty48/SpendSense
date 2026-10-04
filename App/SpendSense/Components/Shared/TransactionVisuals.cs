using MudBlazor;

using SpendSense.Common.Interfaces;
using SpendSense.Common.Models.Enums;

namespace SpendSense.Components.Shared;

/// <summary>The icon, tone and amount sign used to show a transaction, so every list draws them the same way.</summary>
public static class TransactionVisuals
{
    public static string Icon(TransactionTypeEnum type) => type switch
    {
        TransactionTypeEnum.Income => Icons.Material.Filled.Payments,
        TransactionTypeEnum.Savings => Icons.Material.Filled.Savings,
        TransactionTypeEnum.Transfer => Icons.Material.Filled.SwapHoriz,
        _ => Icons.Material.Filled.Receipt
    };

    public static IconTone Tone(TransactionTypeEnum type) =>
        type == TransactionTypeEnum.Income ? IconTone.Accent : IconTone.Neutral;

    /// <summary>
    /// Money moved between your own accounts: a transfer, or savings paid into a savings account. Shown
    /// without a sign, because the money stayed yours.
    /// </summary>
    public static bool IsMove(IAccountMovement t) =>
        t.TransactionType == TransactionTypeEnum.Transfer
        || (t.TransactionType == TransactionTypeEnum.Savings && t.ToAccountId is not null);

    /// <summary>The amount as a list shows it: income positive, a move unsigned, everything else negative.</summary>
    public static double Amount(IAccountMovement t) =>
        t.TransactionType == TransactionTypeEnum.Income || IsMove(t) ? t.Amount : -t.Amount;

    /// <summary>Whether to print a + or − (never for a move).</summary>
    public static bool Signed(IAccountMovement t) => !IsMove(t);

    public static MoneyTone AmountTone(IAccountMovement t) =>
        t.TransactionType == TransactionTypeEnum.Income ? MoneyTone.Income
        : IsMove(t) ? MoneyTone.Muted
        : MoneyTone.Neutral;

    /// <summary>"Groceries · Everyday", or "Everyday → Visa card" for a move.</summary>
    public static string Subtitle(IAccountMovement t)
    {
        if (IsMove(t))
        {
            var route = $"{t.Account?.Name} → {t.ToAccount?.Name}";
            return t.TransactionType == TransactionTypeEnum.Savings ? $"Savings · {route}" : route;
        }

        var what = t.Category?.Name ?? (t.TransactionType == TransactionTypeEnum.Income ? "Income" : "Uncategorised");
        return t.Account is null ? what : $"{what} · {t.Account.Name}";
    }
}
