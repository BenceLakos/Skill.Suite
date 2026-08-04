using MudBlazor;
using MudBlazor.Utilities;

namespace Skill.Suite.Services.Theme;

/// <summary>
/// MudBlazor theme using the WorldSkills brand palette — vibrant red primary on a
/// deep navy secondary, with a warm gold accent for highlights. Both light and dark
/// variants are tuned so the brand red stays accessible against the surface.
/// </summary>
public static class WorldSkillsTheme
{
    // Core brand colours.
    private const string BrandRed = "#E2231A";        // WorldSkills Red (PMS 485-ish)
    private const string BrandRedDeep = "#B81720";    // Darker red for hover / dark-mode primary contrast
    private const string BrandRedLight = "#FF3B33";   // Lifted red for dark-mode primary
    private const string BrandNavy = "#003C71";       // WorldSkills Navy (PMS 281-ish)
    private const string BrandNavyLight = "#1F5B95";  // For dark-mode secondary
    private const string BrandGold = "#FFD200";       // Accent gold
    private const string BrandGoldDeep = "#E0B800";   // Accent gold for dark-mode contrast

    // Neutrals.
    private const string Ink = "#1A1B22";
    private const string InkMuted = "#5A5E6A";
    private const string Snow = "#FFFFFF";
    private const string Mist = "#F5F6FA";
    private const string Fog = "#E6E8EE";
    private const string Carbon = "#0E0F12";
    private const string CarbonElev = "#16181C";
    private const string CarbonHigh = "#1F2127";

    public static MudTheme Build() => new()
    {
        PaletteLight = new PaletteLight
        {
            Primary = BrandRed,
            PrimaryContrastText = Snow,
            Secondary = BrandNavy,
            SecondaryContrastText = Snow,
            Tertiary = BrandGold,
            TertiaryContrastText = Ink,

            Info = "#0066B3",
            Success = "#2E8B57",
            Warning = "#F5A623",
            Error = BrandRedDeep,

            Background = Mist,
            Surface = Snow,
            DrawerBackground = Snow,
            DrawerText = Ink,
            DrawerIcon = InkMuted,
            AppbarBackground = Snow,
            AppbarText = Ink,

            TextPrimary = Ink,
            TextSecondary = InkMuted,
            TextDisabled = "rgba(26, 27, 34, 0.38)",

            ActionDefault = InkMuted,
            ActionDisabled = "rgba(26, 27, 34, 0.26)",
            ActionDisabledBackground = "rgba(26, 27, 34, 0.12)",

            LinesDefault = Fog,
            LinesInputs = "#CFD3DC",
            TableLines = Fog,
            TableHover = "rgba(226, 35, 26, 0.06)",
            TableStriped = "rgba(0, 60, 113, 0.03)",
            Divider = Fog,
            DividerLight = "rgba(26, 27, 34, 0.08)",

            HoverOpacity = 0.06,
            RippleOpacity = 0.10,
            RippleOpacitySecondary = 0.20,
        },

        PaletteDark = new PaletteDark
        {
            Primary = BrandRedLight,
            PrimaryContrastText = Snow,
            Secondary = BrandNavyLight,
            SecondaryContrastText = Snow,
            Tertiary = BrandGoldDeep,
            TertiaryContrastText = Carbon,

            Info = "#4DA3FF",
            Success = "#5FCB85",
            Warning = "#FFC04D",
            Error = "#FF6B5C",

            Background = Carbon,
            Surface = CarbonElev,
            DrawerBackground = CarbonElev,
            DrawerText = "#E6E8EE",
            DrawerIcon = "#9AA0AE",
            AppbarBackground = CarbonHigh,
            AppbarText = Snow,

            TextPrimary = "#F4F5F8",
            TextSecondary = "#A8ADBA",
            TextDisabled = "rgba(244, 245, 248, 0.38)",

            ActionDefault = "#A8ADBA",
            ActionDisabled = "rgba(244, 245, 248, 0.26)",
            ActionDisabledBackground = "rgba(244, 245, 248, 0.10)",

            LinesDefault = "#2A2D36",
            LinesInputs = "#3A3E49",
            TableLines = "#2A2D36",
            TableHover = "rgba(255, 59, 51, 0.10)",
            TableStriped = "rgba(31, 91, 149, 0.08)",
            Divider = "#2A2D36",
            DividerLight = "rgba(244, 245, 248, 0.08)",

            HoverOpacity = 0.10,
            RippleOpacity = 0.16,
            RippleOpacitySecondary = 0.24,
        },

        LayoutProperties = new LayoutProperties
        {
            DefaultBorderRadius = "8px",
            DrawerWidthLeft = "240px",
        },

        Typography = new Typography
        {
            Default = new DefaultTypography
            {
                FontFamily = new[] { "Inter", "Segoe UI", "Roboto", "Helvetica Neue", "Arial", "sans-serif" },
                FontSize = "0.875rem",
                LineHeight = "1.5",
            },
            H1 = new H1Typography { FontWeight = "700", LetterSpacing = "-0.02em" },
            H2 = new H2Typography { FontWeight = "700", LetterSpacing = "-0.015em" },
            H3 = new H3Typography { FontWeight = "700" },
            H4 = new H4Typography { FontWeight = "600" },
            H5 = new H5Typography { FontWeight = "600" },
            H6 = new H6Typography { FontWeight = "600" },
            Button = new ButtonTypography { FontWeight = "600", TextTransform = "none" },
        },
    };

    // Re-exported so other places can grab the literals (e.g. wwwroot CSS variables).
    public static class Brand
    {
        public static readonly MudColor Red = BrandRed;
        public static readonly MudColor Navy = BrandNavy;
        public static readonly MudColor Gold = BrandGold;
    }
}
