using MudBlazor;

namespace SpendSense.Common.Theming;

/// <summary>
/// Chart colours for the resolved theme. MudChart takes its series colours from ChartOptions rather than
/// the MudTheme palette, so this is where the design system reaches the charts. The light set is
/// darker so each slice keeps contrast against a white surface; charts also carry a legend and values
/// so colour is never the only signal. Documented in docs/design/tokens.md.
/// </summary>
public static class ChartTheme
{
    static readonly string[] Dark =
    [
        "#33D6A6", "#6EA8FF", "#FF9A6E", "#B08CFF", "#F0B94E", "#4FD1D9", "#7FDB8F", "#FF6B5E"
    ];

    static readonly string[] Light =
    [
        "#097754", "#2F6FD6", "#C25A1E", "#7A4FD6", "#8A5A00", "#0F7F8A", "#3B8A4A", "#B83629"
    ];

    public static string[] Palette(bool dark) => dark ? Dark : Light;

    public static ChartOptions Options(bool dark) => new() { ChartPalette = Palette(dark) };
}
