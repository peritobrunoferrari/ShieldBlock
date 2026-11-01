using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using ShieldBlock.Core;

namespace ShieldBlock.UI
{
    /// <summary>
    /// Card de status com cantos arredondados, icone, titulo e valor.
    /// v6.1: modo FillBackground preenche o card todo com a cor de acento
    /// (verde = seguro, vermelho = perigo) para alta visibilidade.
    /// </summary>
    public class StatusCard : Panel
    {
        private string _title = "TITULO";
        private string _value = "-";
        private string _icon  = "*";
        private Color  _accent = Theme.AccentBlue;
        private bool   _fill   = false;

        public StatusCard()
        {
            DoubleBuffered = true;
            BackColor      = Theme.Surface;
            Height         = 80;
            SetStyle(ControlStyles.OptimizedDoubleBuffer
                   | ControlStyles.AllPaintingInWmPaint
                   | ControlStyles.UserPaint, true);
        }

        public string Title  { get { return _title;  } set { _title  = value; Invalidate(); } }
        public string Value  { get { return _value;  } set { _value  = value; Invalidate(); } }
        public string Icon   { get { return _icon;   } set { _icon   = value; Invalidate(); } }
        public Color  Accent { get { return _accent; } set { _accent = value; Invalidate(); } }

        public bool FillBackground
        {
            get { return _fill; }
            set { _fill = value; Invalidate(); }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            var rect = new Rectangle(0, 0, Width - 1, Height - 1);
            Color bgColor     = _fill ? _accent           : Theme.Surface;
            Color borderColor = _fill ? DarkerOf(_accent) : Theme.Border;
            Color titleColor  = _fill ? ContrastText()    : Theme.TextSecondary;
            Color valueColor  = _fill ? ContrastText()    : Theme.TextPrimary;
            Color iconColor   = _fill ? ContrastText()    : _accent;

            using (var path = RoundedRect(rect, Theme.CardRadius))
            using (var bg = new SolidBrush(bgColor))
            using (var border = new Pen(borderColor, 1))
            {
                g.FillPath(bg, path);
                g.DrawPath(border, path);
            }

            if (!_fill)
            {
                using (var accentBrush = new SolidBrush(_accent))
                {
                    var accentRect = new Rectangle(0, 8, 4, Height - 16);
                    g.FillRectangle(accentBrush, accentRect);
                }
            }

            using (var iconFont = new Font(Theme.FontFamily, 22f, FontStyle.Bold))
            using (var iconBrush = new SolidBrush(iconColor))
            {
                g.DrawString(_icon, iconFont, iconBrush, 14, 18);
            }

            using (var titleFont = new Font(Theme.FontFamily, Theme.FontSizeSmall, FontStyle.Bold))
            using (var titleBrush = new SolidBrush(titleColor))
            {
                g.DrawString(_title.ToUpperInvariant(), titleFont, titleBrush, 60, 14);
            }

            using (var valFont = new Font(Theme.FontFamily, Theme.FontSizeLarge, FontStyle.Bold))
            using (var valBrush = new SolidBrush(valueColor))
            {
                g.DrawString(_value, valFont, valBrush, 58, 34);
            }
        }

        private Color ContrastText()
        {
            int luma = (int)(0.299 * _accent.R + 0.587 * _accent.G + 0.114 * _accent.B);
            return luma > 160 ? Theme.HeaderBar : Color.White;
        }

        private static Color DarkerOf(Color c)
        {
            return Color.FromArgb(
                (int)(c.R * 0.7),
                (int)(c.G * 0.7),
                (int)(c.B * 0.7));
        }

        private static GraphicsPath RoundedRect(Rectangle bounds, int radius)
        {
            var path = new GraphicsPath();
            int d = radius * 2;
            path.AddArc(bounds.X, bounds.Y, d, d, 180, 90);
            path.AddArc(bounds.Right - d, bounds.Y, d, d, 270, 90);
            path.AddArc(bounds.Right - d, bounds.Bottom - d, d, d, 0, 90);
            path.AddArc(bounds.X, bounds.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }
    }
}
