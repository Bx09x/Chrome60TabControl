using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

namespace Chrome60Tabs.Rendering
{
    /// <summary>
    /// Vector glyphs used by the tab strip. Everything here is generated procedurally so no
    /// proprietary Chrome bitmaps are needed or redistributed.
    /// </summary>
    public static class Chrome60Icons
    {
        /// <summary>
        /// Draws the Chrome 60 close "X": two thin diagonal strokes inside a 16x16 box, spanning
        /// from 4/16 to 12/16 of the box (the Material tab-close vector icon).
        /// </summary>
        public static void DrawCloseGlyph(Graphics g, Rectangle box, Color color, float scale)
        {
            float inset = box.Width * 0.25f;
            float x0 = box.X + inset;
            float y0 = box.Y + inset;
            float x1 = box.Right - inset;
            float y1 = box.Bottom - inset;

            using (var pen = new Pen(color, 1.25f * scale))
            {
                pen.StartCap = LineCap.Flat;
                pen.EndCap = LineCap.Flat;
                g.DrawLine(pen, x0, y0, x1, y1);
                g.DrawLine(pen, x0, y1, x1, y0);
            }
        }

        /// <summary>Draws the filled hover / pressed circle behind the close glyph.</summary>
        public static void DrawCloseCircle(Graphics g, Rectangle box, Color color)
        {
            using (var brush = new SolidBrush(color))
                g.FillEllipse(brush, box.X, box.Y, box.Width, box.Height);
        }

        /// <summary>
        /// Draws a plus glyph centred in <paramref name="box"/> (Chrome 60 new-tab "+" with round caps).
        /// </summary>
        public static void DrawPlusGlyph(Graphics g, Rectangle box, Color color, float scale)
        {
            // Chrome 60 touch-mode plus: radius 6 (12px span), 2px round-capped strokes.
            // Scale down to fit the 32x16 button: half-length 4px at 96 DPI.
            float half = 4f * scale;
            float cx = box.X + box.Width / 2f;
            float cy = box.Y + box.Height / 2f;

            using (var pen = new Pen(color, 1.5f * scale))
            {
                pen.StartCap = System.Drawing.Drawing2D.LineCap.Round;
                pen.EndCap = System.Drawing.Drawing2D.LineCap.Round;
                pen.LineJoin = System.Drawing.Drawing2D.LineJoin.Round;
                g.DrawLine(pen, cx - half, cy, cx + half, cy);
                g.DrawLine(pen, cx, cy - half, cx, cy + half);
            }
        }

        /// <summary>
        /// Draws a stand-in for Chrome's default favicon (IDR_DEFAULT_FAVICON): a grey page with a
        /// folded corner. Used when a tab has no <see cref="Chrome60Tab.Icon"/>.
        /// </summary>
        public static void DrawDefaultFavicon(Graphics g, Rectangle box, Color color, float scale)
        {
            float w = box.Width;
            float h = box.Height;
            float x = box.X + w * 0.1875f;   // 3/16
            float y = box.Y + h * 0.0625f;   // 1/16
            float pw = w * 0.625f;           // 10/16
            float ph = h * 0.875f;           // 14/16
            float fold = w * 0.25f;          // 4/16

            using (var path = new GraphicsPath())
            {
                path.AddLine(x, y, x + pw - fold, y);
                path.AddLine(x + pw - fold, y, x + pw, y + fold);
                path.AddLine(x + pw, y + fold, x + pw, y + ph);
                path.AddLine(x + pw, y + ph, x, y + ph);
                path.CloseFigure();

                using (var pen = new Pen(color, 1f * scale))
                {
                    pen.LineJoin = LineJoin.Round;
                    g.DrawPath(pen, path);
                    g.DrawLine(pen, x + pw - fold, y, x + pw - fold, y + fold);
                    g.DrawLine(pen, x + pw - fold, y + fold, x + pw, y + fold);
                }
            }
        }

        /// <summary>Draws an image scaled into <paramref name="box"/>, optionally at reduced opacity.</summary>
        public static void DrawImage(Graphics g, Image image, Rectangle box, float opacity)
        {
            var oldMode = g.InterpolationMode;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            try
            {
                if (opacity >= 1f)
                {
                    g.DrawImage(image, box);
                    return;
                }

                var matrix = new ColorMatrix { Matrix33 = opacity };
                using (var attributes = new ImageAttributes())
                {
                    attributes.SetColorMatrix(matrix, ColorMatrixFlag.Default, ColorAdjustType.Bitmap);
                    g.DrawImage(image, box, 0, 0, image.Width, image.Height, GraphicsUnit.Pixel, attributes);
                }
            }
            finally
            {
                g.InterpolationMode = oldMode;
            }
        }
    }
}
