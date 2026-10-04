using SpendSense.Common.Services;
using SpendSense.Tests.TestDoubles;

namespace SpendSense.Tests.Unit;

public class SettingsServiceTests
{
    readonly InMemoryPreferences preferences = new();

    SettingsService Settings => new(preferences);

    [Fact]
    public void Theme_defaults_to_system()
    {
        Assert.Equal(ThemeMode.System, Settings.ThemeMode);
    }

    [Theory]
    [InlineData(true, ThemeMode.Dark)]
    [InlineData(false, ThemeMode.Light)]
    public void Legacy_dark_mode_choice_is_kept(bool wasDark, ThemeMode expected)
    {
        preferences.Set("IsDarkMode", wasDark);

        Assert.Equal(expected, Settings.ThemeMode);
    }

    [Fact]
    public void Explicit_theme_wins_over_the_legacy_switch()
    {
        preferences.Set("IsDarkMode", true);
        var settings = Settings;

        settings.ThemeMode = ThemeMode.Light;

        Assert.Equal(ThemeMode.Light, settings.ThemeMode);
    }

    [Fact]
    public void Changing_theme_raises_ThemeModeChanged()
    {
        var settings = Settings;
        var raised = 0;
        settings.ThemeModeChanged += () => raised++;

        settings.ThemeMode = ThemeMode.Dark;

        Assert.Equal(1, raised);
    }

    [Fact]
    public void Defaults_match_the_documented_behaviour()
    {
        var settings = Settings;

        Assert.True(settings.IncomeAppliesNextMonth);
        Assert.False(settings.HideAmounts);
        Assert.False(settings.IncludeCreditInAvailable);
        Assert.False(settings.BalanceCheckDismissed);
        Assert.Equal(DateTime.Now.Year, settings.ActiveBudgetYear);
        Assert.Equal(DateTime.Now.Month, settings.ActiveBudgetMonth);
    }

    [Fact]
    public void Settings_round_trip_through_preferences()
    {
        Settings.HideAmounts = true;
        Settings.ActiveBudgetMonth = 3;

        // A fresh service reads what the last one wrote.
        Assert.True(Settings.HideAmounts);
        Assert.Equal(3, Settings.ActiveBudgetMonth);
    }
}
