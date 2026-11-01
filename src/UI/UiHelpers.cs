using System.Drawing;
using System.Windows.Forms;
using ShieldBlock.Core;

namespace ShieldBlock.UI
{
    /// <summary>
    /// Helpers para aplicar o dark theme de forma consistente em controles padrão.
    /// </summary>
    public static class UiHelpers
    {
        public static void StyleGrid(DataGridView g)
        {
            g.BackgroundColor                  = Theme.Surface;
            g.GridColor                        = Theme.Border;
            g.BorderStyle                      = BorderStyle.None;
            g.RowHeadersVisible                = false;
            g.AllowUserToAddRows               = false;
            g.AllowUserToDeleteRows            = false;
            g.AllowUserToResizeRows            = false;
            g.ReadOnly                         = true;
            g.SelectionMode                    = DataGridViewSelectionMode.FullRowSelect;
            g.AutoSizeColumnsMode              = DataGridViewAutoSizeColumnsMode.Fill;
            g.EnableHeadersVisualStyles        = false;
            g.RowTemplate.Height               = Theme.RowHeight;

            g.DefaultCellStyle.BackColor             = Theme.Surface;
            g.DefaultCellStyle.ForeColor             = Theme.TextPrimary;
            g.DefaultCellStyle.SelectionBackColor    = Theme.SurfaceAlt;
            g.DefaultCellStyle.SelectionForeColor    = Theme.AccentBlue;
            g.DefaultCellStyle.Font                  = new Font(Theme.FontFamily, Theme.FontSizeBase);
            g.DefaultCellStyle.Padding               = new Padding(6, 0, 6, 0);

            g.AlternatingRowsDefaultCellStyle.BackColor = Theme.Background;
            g.AlternatingRowsDefaultCellStyle.ForeColor = Theme.TextPrimary;

            g.ColumnHeadersDefaultCellStyle.BackColor          = Theme.HeaderBar;
            g.ColumnHeadersDefaultCellStyle.ForeColor          = Theme.AccentBlue;
            g.ColumnHeadersDefaultCellStyle.SelectionBackColor = Theme.HeaderBar;
            g.ColumnHeadersDefaultCellStyle.SelectionForeColor = Theme.AccentBlue;
            g.ColumnHeadersDefaultCellStyle.Font               = new Font(Theme.FontFamily, Theme.FontSizeBase, FontStyle.Bold);
            g.ColumnHeadersDefaultCellStyle.Padding            = new Padding(6, 4, 6, 4);
            g.ColumnHeadersHeight                              = 32;
            g.ColumnHeadersBorderStyle                         = DataGridViewHeaderBorderStyle.Single;
        }

        public static Button MakeButton(string text, Color accent)
        {
            var b = new Button
            {
                Text           = text,
                BackColor      = accent,
                ForeColor      = Theme.HeaderBar,
                FlatStyle      = FlatStyle.Flat,
                Height         = 36,
                Font           = new Font(Theme.FontFamily, Theme.FontSizeBase, FontStyle.Bold),
                Cursor         = Cursors.Hand,
                UseVisualStyleBackColor = false
            };
            b.FlatAppearance.BorderSize = 0;
            b.FlatAppearance.MouseOverBackColor = LightenColor(accent, 0.15f);
            b.FlatAppearance.MouseDownBackColor = DarkenColor(accent, 0.15f);
            return b;
        }

        public static Label MakeLabel(string text, float size, bool bold, Color? color = null)
        {
            return new Label
            {
                Text      = text,
                AutoSize  = true,
                Font      = new Font(Theme.FontFamily, size,
                                     bold ? FontStyle.Bold : FontStyle.Regular),
                ForeColor = color ?? Theme.TextPrimary,
                BackColor = Color.Transparent
            };
        }

        public static Color LightenColor(Color c, float amount)
        {
            return Color.FromArgb(
                Lerp(c.R, 255, amount),
                Lerp(c.G, 255, amount),
                Lerp(c.B, 255, amount));
        }

        public static Color DarkenColor(Color c, float amount)
        {
            return Color.FromArgb(
                Lerp(c.R, 0, amount),
                Lerp(c.G, 0, amount),
                Lerp(c.B, 0, amount));
        }

        private static int Lerp(int from, int to, float t)
        {
            return (int)(from + (to - from) * t);
        }
    }
}
