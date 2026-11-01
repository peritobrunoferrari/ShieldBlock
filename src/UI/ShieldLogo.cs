using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Windows.Forms;
using ShieldBlock.Core;

namespace ShieldBlock.UI
{
    /// <summary>
    /// Exibe o LOGO REAL do Shield Pericias (PNG da pasta Resources).
    /// Tenta carregar shield_logo_large.png ou shield_logo_small.png automaticamente.
    /// Se nao encontrar o PNG, exibe apenas o texto SHIELD como fallback.
    /// </summary>
    public class ShieldLogo : Panel
    {
        private bool _showText = true;
        private bool _showSubtitle = true;
        private Color _textColor = Color.White;
        private Image _logoImage;
        private static Image _cachedLarge;
        private static Image _cachedSmall;

        public ShieldLogo()
        {
            DoubleBuffered = true;
            BackColor = Color.Transparent;
            SetStyle(ControlStyles.OptimizedDoubleBuffer
                   | ControlStyles.AllPaintingInWmPaint
                   | ControlStyles.UserPaint
                   | ControlStyles.SupportsTransparentBackColor, true);
        }

        public bool ShowText
        {
            get { return _showText; }
            set { _showText = value; Invalidate(); }
        }

        public bool ShowSubtitle
        {
            get { return _showSubtitle; }
            set { _showSubtitle = value; Invalidate(); }
        }

        public Color LogoColor
        {
            get { return _textColor; }
            set { _textColor = value; Invalidate(); }
        }

        protected override void OnSizeChanged(EventArgs e)
        {
            base.OnSizeChanged(e);
            _logoImage = null; // re-seleciona o tamanho apropriado
            Invalidate();
        }

        private static Image LoadCached(string filename, ref Image cache)
        {
            if (cache != null) return cache;
            try
            {
                string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                string[] candidates = new[]
                {
                    Path.Combine(baseDir, "Resources", filename),
                    Path.Combine(baseDir, filename),
                    Path.Combine(baseDir, "..", "..", "Resources", filename),
                    Path.Combine(baseDir, "..", "Resources", filename)
                };
                foreach (var p in candidates)
                {
                    if (File.Exists(p))
                    {
                        using (var fs = new FileStream(p, FileMode.Open, FileAccess.Read))
                        {
                            cache = Image.FromStream(fs);
                            return cache;
                        }
                    }
                }
            }
            catch { }
            return null;
        }

        private Image PickImage()
        {
            int side = Math.Min(Width, Height);
            if (side >= 140)
            {
                return LoadCached("shield_logo_large.png", ref _cachedLarge)
                    ?? LoadCached("shield_logo_small.png", ref _cachedSmall);
            }
            return LoadCached("shield_logo_small.png", ref _cachedSmall)
                ?? LoadCached("shield_logo_large.png", ref _cachedLarge);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAlias;

            if (_logoImage == null) _logoImage = PickImage();

            int w = Width;
            int h = Height;
            int textBlockH = 0;
            if (_showText) textBlockH = _showSubtitle ? (h / 4) : (h / 6);
            int imgArea = h - textBlockH - 4;

            if (_logoImage != null)
            {
                // Desenha a imagem mantendo aspect ratio, centralizada
                float aspect = (float)_logoImage.Width / _logoImage.Height;
                int targetH = imgArea;
                int targetW = (int)(targetH * aspect);
                if (targetW > w) { targetW = w; targetH = (int)(targetW / aspect); }
                int x = (w - targetW) / 2;
                int y = 2 + (imgArea - targetH) / 2;
                g.DrawImage(_logoImage, new Rectangle(x, y, targetW, targetH));
            }
            else
            {
                // Fallback: caixa com texto
                DrawFallback(g, 0, 0, w, imgArea);
            }

            if (_showText)
            {
                int textY = 2 + imgArea + 4;
                DrawTitleAndSubtitle(g, 0, textY, w, h - textY);
            }
        }

        private void DrawFallback(Graphics g, int x, int y, int w, int h)
        {
            using (var pen = new Pen(_textColor, 3))
            {
                int side = Math.Min(w, h) - 8;
                int cx = x + (w - side) / 2;
                int cy = y + (h - side) / 2;
                g.DrawRectangle(pen, cx, cy, side, side);
                using (var font = new Font(Theme.FontFamily, side * 0.4f,
                                            FontStyle.Bold, GraphicsUnit.Pixel))
                using (var brush = new SolidBrush(_textColor))
                {
                    var sf = new StringFormat
                    {
                        Alignment = StringAlignment.Center,
                        LineAlignment = StringAlignment.Center
                    };
                    g.DrawString("SB", font, brush,
                        new RectangleF(cx, cy, side, side), sf);
                }
            }
        }

        private void DrawTitleAndSubtitle(Graphics g, int x, int y, int w, int h)
        {
            string title = "SHIELD";
            string subtitle = "PERICIAS DIGITAIS";

            float titleSize = h * (_showSubtitle ? 0.55f : 0.85f);
            if (titleSize < 8) titleSize = 8;

            using (var titleFont = new Font(Theme.FontFamily, titleSize,
                                            FontStyle.Bold, GraphicsUnit.Pixel))
            using (var brush = new SolidBrush(_textColor))
            {
                var sf = new StringFormat
                {
                    Alignment = StringAlignment.Center,
                    LineAlignment = StringAlignment.Near
                };
                g.DrawString(title, titleFont, brush,
                    new RectangleF(x, y, w, titleSize + 4), sf);

                if (_showSubtitle)
                {
                    float subSize = titleSize * 0.32f;
                    if (subSize < 7) subSize = 7;
                    using (var subFont = new Font(Theme.FontFamily, subSize,
                                                  FontStyle.Regular, GraphicsUnit.Pixel))
                    {
                        g.DrawString(subtitle, subFont, brush,
                            new RectangleF(x, y + titleSize + 4, w, subSize + 4), sf);
                    }
                }
            }
        }
    }
}
