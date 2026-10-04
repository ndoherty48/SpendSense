using System.Text.RegularExpressions;

using MudBlazor;

using SpendSense.Common.Models.Enums;

namespace SpendSense.Components.Shared;

/// <summary>The icon, label and colour used to show an account, so every screen draws them the same way.</summary>
public static partial class AccountVisuals
{
    public static string Icon(AccountTypeEnum type) => type switch
    {
        AccountTypeEnum.Savings => Icons.Material.Filled.Savings,
        AccountTypeEnum.CreditCard => Icons.Material.Filled.CreditCard,
        AccountTypeEnum.Cash => Icons.Material.Filled.Money,
        _ => Icons.Material.Filled.AccountBalance
    };

    public static string Label(AccountTypeEnum type) => type switch
    {
        AccountTypeEnum.Savings => "Savings",
        AccountTypeEnum.CreditCard => "Credit card",
        AccountTypeEnum.Cash => "Cash",
        _ => "Current account"
    };

    /// <summary>Short label for segmented controls.</summary>
    public static string ShortLabel(AccountTypeEnum type) => type switch
    {
        AccountTypeEnum.CreditCard => "Card",
        AccountTypeEnum.Current => "Current",
        _ => Label(type)
    };

    /// <summary>The colour a new account of this type starts with (account colours are user data).</summary>
    public static string DefaultColor(AccountTypeEnum type) => type switch
    {
        AccountTypeEnum.Savings => "#33D6A6",
        AccountTypeEnum.CreditCard => "#FF9A6E",
        AccountTypeEnum.Cash => "#F0B94E",
        _ => "#6EA8FF"
    };

    /// <summary>The account's colour if it is a valid hex colour, otherwise null (draw the neutral tile).</summary>
    public static string? SafeColor(string? color) =>
        color is not null && HexColor().IsMatch(color) ? color : null;

    [GeneratedRegex("^#[0-9A-Fa-f]{3,8}$")]
    private static partial Regex HexColor();
}
