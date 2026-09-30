using System.Globalization;

namespace SpendSense.Components.Shared;

/// <summary>How a <see cref="Money"/> amount is coloured.</summary>
public enum MoneyTone
{
    Neutral,
    /// <summary>Positive money: income, savings, under budget.</summary>
    Income,
    /// <summary>Negative or over-budget money.</summary>
    Error,
    Muted,
    /// <summary>Accent when positive, error when negative, neutral at zero.</summary>
    Auto
}

public enum MoneySize
{
    Small,
    Body,
    Large
}

public enum IconTone
{
    /// <summary>Muted icon on a neutral tile.</summary>
    Neutral,
    /// <summary>Accent icon on a neutral tile (category rows).</summary>
    Brand,
    /// <summary>Accent icon on a soft accent tile (income, positive).</summary>
    Accent,
    /// <summary>Error icon on a soft error tile (over budget).</summary>
    Error
}

public enum BadgeTone
{
    Neutral,
    Accent,
    Error,
    Warn
}

/// <summary>One option of a <see cref="SegmentedControl{T}"/>. A segment with an <paramref name="Href"/> navigates.</summary>
public sealed record Segment<T>(T Value, string Label, string? Href = null);

/// <summary>
/// Formats money for text contexts (chart labels, "spent / budget" strings) with the same rules as
/// <see cref="Money"/>: thousands separators, a real minus sign, and the privacy mask.
/// </summary>
public static class MoneyFormat
{
    public const string HiddenMask = "•••";

    public static string Format(double amount, string symbol = "£", bool hidden = false, bool signed = false, int decimals = 2)
    {
        if (hidden)
            return $"{symbol}{HiddenMask}";

        var magnitude = Math.Abs(amount).ToString($"N{decimals}", CultureInfo.CurrentCulture);
        var sign = amount < 0 ? "−" : signed && amount > 0 ? "+" : "";
        return $"{sign}{symbol}{magnitude}";
    }
}
