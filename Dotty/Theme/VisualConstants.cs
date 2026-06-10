using Avalonia.Media;

namespace Dotty.Theme;

public static class VisualConstants
{
    // Spacing (4px grid)
    public const double SpaceXs = 4, SpaceSm = 8, SpaceMd = 12, SpaceLg = 16,
                        SpaceXl = 20, SpaceXxl = 24, Space3Xl = 32;

    // Typography
    public const string UiFont = "Inter, San Francisco, Segoe UI, sans-serif";
    public const string MonoFont = "JetBrains Mono, Cascadia Code, Consolas, monospace";
    public const double FontXs = 11, FontSm = 12, FontMd = 13, FontBase = 14, FontLg = 16, FontXl = 20;

    // Border radii
    public const double RadiusSm = 2, RadiusMd = 2, RadiusLg = 2;

    // Animation
    public const int DurationFast = 120;   // hover states
    public const int DurationNormal = 200; // open transitions

    // Multi-layer shadows
    public const string ShadowElevation1 = "0 1 3 0 #18000000, 0 1 2 0 #12000000";
    public const string ShadowElevation2 = "0 4 16 0 #28000000, 0 2 4 0 #14000000";
    public const string ShadowElevation3 = "0 12 40 0 #38000000, 0 4 12 0 #18000000";

    // Layout
    public const double PaletteWidth = 520, PaletteTop = 72;
    public const double SettingsWidth = 380, SettingsTop = 0;
    public const double ChatExpanded = 340, ChatCollapsed = 40;
    public const double ThemeCardWidth = 148;

    // HMI Status Colors (ISA-101)
    public static readonly (byte R, byte G, byte B) StatusNormal = (0x4C, 0xAF, 0x50);
    public static readonly (byte R, byte G, byte B) StatusCaution = (0xFF, 0xEA, 0x00);
    public static readonly (byte R, byte G, byte B) StatusCritical = (0xFF, 0x17, 0x44);
    public static readonly (byte R, byte G, byte B) StatusAdvisory = (0x00, 0xE5, 0xFF);
    public static readonly (byte R, byte G, byte B) StatusInactive = (0x55, 0x55, 0x55);
    public static readonly (byte R, byte G, byte B) StatusManual = (0x7C, 0x4D, 0xFF);

    // Semantic status colors (ISA-101) — always use these for status indicators
    public static Color StatusNormalColor => Color.FromRgb(StatusNormal.R, StatusNormal.G, StatusNormal.B);
    public static Color StatusCautionColor => Color.FromRgb(StatusCaution.R, StatusCaution.G, StatusCaution.B);
    public static Color StatusCriticalColor => Color.FromRgb(StatusCritical.R, StatusCritical.G, StatusCritical.B);
    public static Color StatusAdvisoryColor => Color.FromRgb(StatusAdvisory.R, StatusAdvisory.G, StatusAdvisory.B);
    public static Color StatusInactiveColor => Color.FromRgb(StatusInactive.R, StatusInactive.G, StatusInactive.B);

    // HMI Surface palette (dark cockpit baseline)
    public static readonly (byte R, byte G, byte B) HmiSurface0 = (0x14, 0x14, 0x14);
    public static readonly (byte R, byte G, byte B) HmiSurface1 = (0x1E, 0x1E, 0x1E);
    public static readonly (byte R, byte G, byte B) HmiSurface2 = (0x25, 0x25, 0x25);
    public static readonly (byte R, byte G, byte B) HmiSurface3 = (0x2F, 0x2F, 0x2F);
    public static readonly (byte R, byte G, byte B) HmiBorder = (0x2F, 0x2F, 0x2F);
    public static readonly (byte R, byte G, byte B) HmiText = (0xE8, 0xE8, 0xE8);
    public static readonly (byte R, byte G, byte B) HmiTextDim = (0x77, 0x77, 0x77);
    public static readonly (byte R, byte G, byte B) HmiTextBright = (0xFF, 0xFF, 0xFF);

    // HMI Typography
    public const string HmiUiFont = "fonts:Dotty#Overpass, Inter, sans-serif";
    public const string HmiDataFont = "fonts:DottyTerminal#Overpass Mono, JetBrains Mono, monospace";
    public const double FontHmiLabel = 10;
    public const double FontHmiData = 12;

    // LED indicator sizing
    public const double LedSize = 6;
    public const double LedGlowRadius = 4;

    // Layout
    public const double StatusStripHeight = 40;
    public const double StatusBarHeight = 32;

    // Panel system
    public const double ActivityRailWidth = 48;
    public const double PanelMinWidth = 280;
    public const double PanelMaxWidth = 700;
    public const double PanelDefaultWidth = 360;
    public const double PanelTabStripHeight = 36;
    public const double RailItemSize = 36;
    public const double NotifDotSize = 5;
    public const double GridSplitterWidth = 4;

    // HMI Button palette (prototype CSS variables)
    // Action buttons: gray-700 bg + gray-500 border + gray-200 text
    public static readonly Color BtnBackground = Color.FromRgb(0x22, 0x22, 0x22);       // --gray-700
    public static readonly Color BtnBorder = Color.FromRgb(0x33, 0x33, 0x33);           // --gray-500
    public static readonly Color BtnForeground = Color.FromRgb(0xB0, 0xB0, 0xB0);       // --gray-200
    public static readonly Color BtnHoverBg = Color.FromRgb(0x2A, 0x2A, 0x2A);          // --gray-600
    public static readonly Color BtnHoverFg = Color.FromRgb(0xE8, 0xE8, 0xE8);          // --gray-100
    // Confirm/send: subtle cyan outline
    public static readonly Color BtnConfirmBg = Color.FromArgb(0x1A, 0x00, 0xE5, 0xFF); // rgba(0,229,255,0.1)
    public static readonly Color BtnConfirmBorder = Color.FromArgb(0x4D, 0x00, 0xE5, 0xFF); // rgba(0,229,255,0.3)
    public static readonly Color BtnConfirmFg = Color.FromRgb(0x00, 0xE5, 0xFF);        // --alarm-advisory
    // Stop/cancel: subtle red outline
    public static readonly Color BtnDangerBg = Color.FromArgb(0x0F, 0xFF, 0x17, 0x44);  // rgba(255,23,68,0.06)
    public static readonly Color BtnDangerBorder = Color.FromArgb(0x4D, 0xFF, 0x17, 0x44); // rgba(255,23,68,0.3)
    public static readonly Color BtnDangerFg = Color.FromRgb(0xFF, 0x17, 0x44);         // --alarm-critical
    // Input fields: canvas bg + gray-500 border
    public static readonly Color InputBackground = Color.FromRgb(0x1E, 0x1E, 0x1E);     // --canvas
    public static readonly Color InputBorder = Color.FromRgb(0x33, 0x33, 0x33);         // --gray-500
    public static readonly Color InputForeground = Color.FromRgb(0xE8, 0xE8, 0xE8);     // --gray-100

    // Glass helpers — simulate glassmorphism with high-alpha semi-transparency
    public static Color GlassBackground((byte R, byte G, byte B) c, bool isDark)
        => Color.FromArgb(isDark ? (byte)0xE8 : (byte)0xF0, c.R, c.G, c.B);

    public static Color GlassHighlight(bool isDark)
        => isDark ? Color.FromArgb(0x14, 0xFF, 0xFF, 0xFF) : Color.FromArgb(0x14, 0, 0, 0);

    public static Color BackdropColor(bool isDark)
        => isDark ? Color.FromArgb(0x8C, 0, 0, 0) : Color.FromArgb(0x66, 0, 0, 0);
}
