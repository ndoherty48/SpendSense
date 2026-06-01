namespace SpendSense.Common.Services;

public class SettingsService
{
    public bool IsDarkMode
    {
        get => Preferences.Get(nameof(IsDarkMode), false);
        set => Preferences.Set(nameof(IsDarkMode), value);
    }

    public int ActiveBudgetYear
    {
        get => Preferences.Get(nameof(ActiveBudgetYear), DateTime.Now.Year);
        set => Preferences.Set(nameof(ActiveBudgetYear), value);
    }

    public int ActiveBudgetMonth
    {
        get => Preferences.Get(nameof(ActiveBudgetMonth), DateTime.Now.Month);
        set => Preferences.Set(nameof(ActiveBudgetMonth), value);
    }

    public bool IncomeAppliesNextMonth
    {
        get => Preferences.Get(nameof(IncomeAppliesNextMonth), true);
        set => Preferences.Set(nameof(IncomeAppliesNextMonth), value);
    }

    public bool HideAmounts
    {
        get => Preferences.Get(nameof(HideAmounts), false);
        set => Preferences.Set(nameof(HideAmounts), value);
    }

    public string FormatAmount(string symbol, double amount)
    {
        return HideAmounts ? $"{symbol}•••" : $"{symbol}{amount:F2}";
    }
}
