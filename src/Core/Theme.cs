using System.Drawing;

namespace ShieldBlock.Core
{
    /// <summary>
    /// Tema Dark Moderno - Inspirado em Catppuccin Mocha.
    /// Centraliza todas as cores e fontes para consistência visual.
    /// </summary>
    public static class Theme
    {
        // ============= CORES BASE =============
        public static readonly Color Background     = Color.FromArgb(30, 30, 46);   // #1E1E2E (base)
        public static readonly Color Surface        = Color.FromArgb(49, 50, 68);   // #313244 (card)
        public static readonly Color SurfaceAlt     = Color.FromArgb(69, 71, 90);   // #45475A (hover)
        public static readonly Color Border         = Color.FromArgb(88, 91, 112);  // #585B70
        public static readonly Color HeaderBar      = Color.FromArgb(24, 24, 37);   // #181825

        // ============= TEXTO =============
        public static readonly Color TextPrimary    = Color.FromArgb(205, 214, 244); // #CDD6F4
        public static readonly Color TextSecondary  = Color.FromArgb(166, 173, 200); // #A6ADC8
        public static readonly Color TextMuted      = Color.FromArgb(127, 132, 156); // #7F849C

        // ============= ACENTOS =============
        public static readonly Color AccentBlue     = Color.FromArgb(137, 180, 250); // #89B4FA
        public static readonly Color AccentGreen    = Color.FromArgb(166, 227, 161); // #A6E3A1
        public static readonly Color AccentRed      = Color.FromArgb(243, 139, 168); // #F38BA8
        public static readonly Color AccentYellow   = Color.FromArgb(249, 226, 175); // #F9E2AF
        public static readonly Color AccentMauve    = Color.FromArgb(203, 166, 247); // #CBA6F7
        public static readonly Color AccentTeal     = Color.FromArgb(148, 226, 213); // #94E2D5

        // ============= STATUS =============
        public static readonly Color StatusActive   = Color.FromArgb(166, 227, 161); // verde
        public static readonly Color StatusInactive = Color.FromArgb(243, 139, 168); // vermelho
        public static readonly Color StatusWarning  = Color.FromArgb(249, 226, 175); // amarelo

        // ============= TERMINAL/LOGS =============
        public static readonly Color TerminalBg     = Color.FromArgb(17, 17, 27);   // #11111B
        public static readonly Color TerminalText   = Color.FromArgb(166, 227, 161); // verde
        public static readonly Color TerminalDim    = Color.FromArgb(108, 112, 134); // cinza claro

        // ============= FONTES =============
        public const string FontFamily      = "Segoe UI";
        public const string FontMono        = "Cascadia Mono";
        public const string FontMonoFallback = "Consolas";
        public const float  FontSizeSmall   = 8.25f;
        public const float  FontSizeBase    = 9.75f;
        public const float  FontSizeMedium  = 11f;
        public const float  FontSizeLarge   = 14f;
        public const float  FontSizeTitle   = 18f;

        // ============= ESPAÇAMENTO =============
        public const int Padding       = 12;
        public const int RowHeight     = 28;
        public const int HeaderHeight  = 56;
        public const int CardRadius    = 8;
    }
}
