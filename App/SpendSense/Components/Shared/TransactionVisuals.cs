using MudBlazor;

using SpendSense.Common.Models.Enums;

namespace SpendSense.Components.Shared;

/// <summary>The icon and tone used to show a transaction type, so every list draws them the same way.</summary>
public static class TransactionVisuals
{
    public static string Icon(TransactionTypeEnum type) => type switch
    {
        TransactionTypeEnum.Income => Icons.Material.Filled.Payments,
        TransactionTypeEnum.Savings => Icons.Material.Filled.Savings,
        _ => Icons.Material.Filled.Receipt
    };

    public static IconTone Tone(TransactionTypeEnum type) =>
        type == TransactionTypeEnum.Income ? IconTone.Accent : IconTone.Neutral;
}
