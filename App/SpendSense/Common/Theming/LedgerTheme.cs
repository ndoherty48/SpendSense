using MudBlazor;

namespace SpendSense.Common.Theming;

/// <summary>
/// Maps the Ledger Noir tokens onto MudBlazor so Mud components inherit the design system.
/// The hex values mirror wwwroot/css/tokens.css (MudColor cannot parse var()); change both together.
/// Contract and verified contrast: docs/design/tokens.md.
/// </summary>
public static class LedgerTheme
{
    static readonly string[] Display = ["Space Grotesk", "system-ui", "sans-serif"];
    static readonly string[] Body = ["IBM Plex Sans", "system-ui", "sans-serif"];

    public static MudTheme Theme { get; } = new()
    {
        PaletteDark = new PaletteDark
        {
            Primary = "#33D6A6",
            PrimaryContrastText = "#12151C",
            Secondary = "#9BA3B4",
            SecondaryContrastText = "#12151C",
            Tertiary = "#33D6A6",
            TertiaryContrastText = "#12151C",
            Info = "#33D6A6",
            InfoContrastText = "#12151C",
            Success = "#33D6A6",
            SuccessContrastText = "#12151C",
            Warning = "#F0B94E",
            WarningContrastText = "#12151C",
            Error = "#FF6B5E",
            ErrorContrastText = "#12151C",
            Dark = "#1B2029",

            Background = "#12151C",
            BackgroundGray = "#232A36",
            Surface = "#1B2029",
            AppbarBackground = "#1B2029",
            AppbarText = "#F4F6F8",
            DrawerBackground = "#1B2029",
            DrawerText = "#F4F6F8",
            DrawerIcon = "#9BA3B4",

            TextPrimary = "#F4F6F8",
            TextSecondary = "#9BA3B4",
            TextDisabled = "rgba(155,163,180,0.5)",
            ActionDefault = "#9BA3B4",
            ActionDisabled = "rgba(155,163,180,0.38)",
            ActionDisabledBackground = "rgba(155,163,180,0.12)",

            Divider = "#2E3644",
            DividerLight = "#2E3644",
            LinesDefault = "#2E3644",
            LinesInputs = "#6B7689",
            TableLines = "#2E3644",
            TableStriped = "rgba(35,42,54,0.5)",
            Skeleton = "#232A36",
        },
        PaletteLight = new PaletteLight
        {
            Primary = "#097754",
            PrimaryContrastText = "#FFFFFF",
            Secondary = "#5B6472",
            SecondaryContrastText = "#FFFFFF",
            Tertiary = "#097754",
            TertiaryContrastText = "#FFFFFF",
            Info = "#097754",
            InfoContrastText = "#FFFFFF",
            Success = "#097754",
            SuccessContrastText = "#FFFFFF",
            Warning = "#8A5A00",
            WarningContrastText = "#FFFFFF",
            Error = "#B83629",
            ErrorContrastText = "#FFFFFF",
            Dark = "#14181F",

            Background = "#F5F7FA",
            BackgroundGray = "#ECF0F5",
            Surface = "#FFFFFF",
            AppbarBackground = "#FFFFFF",
            AppbarText = "#14181F",
            DrawerBackground = "#FFFFFF",
            DrawerText = "#14181F",
            DrawerIcon = "#5B6472",

            TextPrimary = "#14181F",
            TextSecondary = "#5B6472",
            TextDisabled = "rgba(91,100,114,0.5)",
            ActionDefault = "#5B6472",
            ActionDisabled = "rgba(91,100,114,0.38)",
            ActionDisabledBackground = "rgba(91,100,114,0.12)",

            Divider = "#DFE4EC",
            DividerLight = "#DFE4EC",
            LinesDefault = "#DFE4EC",
            LinesInputs = "#7F8A9B",
            TableLines = "#DFE4EC",
            TableStriped = "rgba(236,240,245,0.6)",
            Skeleton = "#ECF0F5",
        },
        Typography = new Typography
        {
            Default = new DefaultTypography { FontFamily = Body },
            H1 = new H1Typography { FontFamily = Display, FontWeight = "700" },
            H2 = new H2Typography { FontFamily = Display, FontWeight = "700" },
            H3 = new H3Typography { FontFamily = Display, FontWeight = "600" },
            H4 = new H4Typography { FontFamily = Display, FontWeight = "600" },
            H5 = new H5Typography { FontFamily = Display, FontWeight = "600" },
            H6 = new H6Typography { FontFamily = Display, FontWeight = "600" },
            Subtitle1 = new Subtitle1Typography { FontFamily = Body, FontWeight = "600" },
            Subtitle2 = new Subtitle2Typography { FontFamily = Body, FontWeight = "600" },
            Body1 = new Body1Typography { FontFamily = Body },
            Body2 = new Body2Typography { FontFamily = Body },
            Button = new ButtonTypography { FontFamily = Body, FontWeight = "600", TextTransform = "none" },
            Caption = new CaptionTypography { FontFamily = Body },
            Overline = new OverlineTypography { FontFamily = Body, FontWeight = "600" },
        },
        LayoutProperties = new LayoutProperties
        {
            DefaultBorderRadius = "14px",
        },
    };
}
