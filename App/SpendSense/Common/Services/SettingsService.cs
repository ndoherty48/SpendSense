namespace SpendSense.Common.Services;

public enum ThemeMode
{
    System,
    Light,
    Dark
}

public class SettingsService
{
    const string LegacyIsDarkModeKey = "IsDarkMode";

    /// <summary>Raised after <see cref="ThemeMode"/> is changed, so the UI can apply it live.</summary>
    public event Action? ThemeModeChanged;

    public ThemeMode ThemeMode
    {
        get
        {
            if (Enum.TryParse<ThemeMode>(Preferences.Get(nameof(ThemeMode), ""), out var mode))
                return mode;

            // One-time migration from the old dark-mode switch: an explicit choice is kept,
            // and anyone who never touched it follows the system.
            if (Preferences.ContainsKey(LegacyIsDarkModeKey))
                return Preferences.Get(LegacyIsDarkModeKey, false) ? ThemeMode.Dark : ThemeMode.Light;

            return ThemeMode.System;
        }
        set
        {
            Preferences.Set(nameof(ThemeMode), value.ToString());
            ThemeModeChanged?.Invoke();
        }
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

    /// <summary>Add the credit left on cards to the dashboard's Available total.</summary>
    public bool IncludeCreditInAvailable
    {
        get => Preferences.Get(nameof(IncludeCreditInAvailable), false);
        set => Preferences.Set(nameof(IncludeCreditInAvailable), value);
    }
}
