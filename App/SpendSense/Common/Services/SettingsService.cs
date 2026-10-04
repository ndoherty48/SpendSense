namespace SpendSense.Common.Services;

public enum ThemeMode
{
    System,
    Light,
    Dark
}

/// <summary>
/// Device-local settings, stored in MAUI <see cref="IPreferences"/> (injected, so tests can use an in-memory store).
/// </summary>
public class SettingsService(IPreferences preferences)
{
    const string LegacyIsDarkModeKey = "IsDarkMode";

    /// <summary>Raised after <see cref="ThemeMode"/> is changed, so the UI can apply it live.</summary>
    public event Action? ThemeModeChanged;

    public ThemeMode ThemeMode
    {
        get
        {
            if (Enum.TryParse<ThemeMode>(preferences.Get(nameof(ThemeMode), ""), out var mode))
                return mode;

            // One-time migration from the old dark-mode switch: an explicit choice is kept,
            // and anyone who never touched it follows the system.
            if (preferences.ContainsKey(LegacyIsDarkModeKey))
                return preferences.Get(LegacyIsDarkModeKey, false) ? ThemeMode.Dark : ThemeMode.Light;

            return ThemeMode.System;
        }
        set
        {
            preferences.Set(nameof(ThemeMode), value.ToString());
            ThemeModeChanged?.Invoke();
        }
    }

    public int ActiveBudgetYear
    {
        get => preferences.Get(nameof(ActiveBudgetYear), DateTime.Now.Year);
        set => preferences.Set(nameof(ActiveBudgetYear), value);
    }

    public int ActiveBudgetMonth
    {
        get => preferences.Get(nameof(ActiveBudgetMonth), DateTime.Now.Month);
        set => preferences.Set(nameof(ActiveBudgetMonth), value);
    }

    public bool IncomeAppliesNextMonth
    {
        get => preferences.Get(nameof(IncomeAppliesNextMonth), true);
        set => preferences.Set(nameof(IncomeAppliesNextMonth), value);
    }

    public bool HideAmounts
    {
        get => preferences.Get(nameof(HideAmounts), false);
        set => preferences.Set(nameof(HideAmounts), value);
    }

    /// <summary>Add the credit left on cards to the dashboard's Available total.</summary>
    public bool IncludeCreditInAvailable
    {
        get => preferences.Get(nameof(IncludeCreditInAvailable), false);
        set => preferences.Set(nameof(IncludeCreditInAvailable), value);
    }

    /// <summary>The dashboard's "check your account balances" banner was dismissed on this device.</summary>
    public bool BalanceCheckDismissed
    {
        get => preferences.Get(nameof(BalanceCheckDismissed), false);
        set => preferences.Set(nameof(BalanceCheckDismissed), value);
    }
}
