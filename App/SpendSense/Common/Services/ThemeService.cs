using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Controls;

namespace SpendSense.Common.Services;

/// <summary>
/// Resolves the effective light/dark theme from <see cref="SettingsService.ThemeMode"/> and the OS,
/// pushes it to the native layer (<see cref="Application.UserAppTheme"/>) and tells the UI when it changes.
/// </summary>
/// <remarks>
/// Native-first: for <see cref="ThemeMode.System"/> UserAppTheme is left unspecified so
/// <see cref="Application.RequestedTheme"/> reports the OS theme; for an explicit mode UserAppTheme
/// overrides it, so RequestedTheme is always the theme actually in effect.
/// <see cref="Changed"/> can fire on the UI thread, so Blazor consumers must marshal with InvokeAsync.
/// </remarks>
public sealed class ThemeService : IDisposable
{
    readonly SettingsService settings;
    Application? subscribedTo;

    public ThemeService(SettingsService settings)
    {
        this.settings = settings;
        settings.ThemeModeChanged += OnThemeModeChanged;
        ApplyNativeTheme();
    }

    public event Action? Changed;

    public ThemeMode Mode => settings.ThemeMode;

    /// <summary>True when the dark theme is in effect. Dark is the design's home theme, so it wins when unknown.</summary>
    public bool ResolvedDark => Application.Current?.RequestedTheme != AppTheme.Light;

    /// <summary>Applies the persisted mode to the native app. Safe to call repeatedly; call it early at startup.</summary>
    public void ApplyNativeTheme()
    {
        var app = Application.Current;
        if (app is null)
            return;

        if (!ReferenceEquals(subscribedTo, app))
        {
            if (subscribedTo is not null)
                subscribedTo.RequestedThemeChanged -= OnRequestedThemeChanged;
            app.RequestedThemeChanged += OnRequestedThemeChanged;
            subscribedTo = app;
        }

        var target = settings.ThemeMode switch
        {
            ThemeMode.Light => AppTheme.Light,
            ThemeMode.Dark => AppTheme.Dark,
            _ => AppTheme.Unspecified
        };

        if (MainThread.IsMainThread)
            app.UserAppTheme = target;
        else
            MainThread.BeginInvokeOnMainThread(() => app.UserAppTheme = target);
    }

    void OnThemeModeChanged()
    {
        ApplyNativeTheme();
        Changed?.Invoke();
    }

    void OnRequestedThemeChanged(object? sender, AppThemeChangedEventArgs e) => Changed?.Invoke();

    public void Dispose()
    {
        settings.ThemeModeChanged -= OnThemeModeChanged;
        if (subscribedTo is not null)
            subscribedTo.RequestedThemeChanged -= OnRequestedThemeChanged;
    }
}
