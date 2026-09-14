using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;

namespace Clio.Desktop
{
    // The introductory sage is an original pencil study carried inside the
    // executable. Decode and scale it once, including for desktop-shortcut runs.
    internal static class FirstAdviserArt
    {
        private static readonly object sync = new object();
        private static Bitmap portrait;
        private static bool loaded;

        private static Bitmap Portrait()
        {
            lock (sync)
            {
                if (loaded) return portrait;
                using (Stream source = typeof(FirstAdviserArt).Assembly.GetManifestResourceStream("Clio.Advisers.first-adviser.png"))
                {
                    if (source != null)
                    using (Image original = Image.FromStream(source))
                    {
                        portrait = new Bitmap(320, 320, PixelFormat.Format32bppPArgb);
                        using (Graphics g = Graphics.FromImage(portrait))
                        {
                            g.Clear(Color.FromArgb(243, 235, 215));
                            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                            float scale = Math.Min(320f / original.Width, 320f / original.Height);
                            float width = original.Width * scale, height = original.Height * scale;
                            g.DrawImage(original, new RectangleF((320 - width) / 2, (320 - height) / 2, width, height));
                        }
                    }
                }
                // A source-only build can omit optional art without making its
                // opening report unusable. Release builds embed the full study.
                loaded = true;
                return portrait;
            }
        }

        internal static void Draw(Graphics g, RectangleF bounds)
        {
            if (bounds.Width <= 0 || bounds.Height <= 0) return;
            float side = Math.Min(bounds.Width, bounds.Height);
            RectangleF paper = new RectangleF(bounds.X + (bounds.Width - side) / 2,
                bounds.Y + (bounds.Height - side) / 2, side, side);
            float inset = Math.Max(1, side * .025f);
            RectangleF drawing = RectangleF.Inflate(paper, -inset, -inset);
            Bitmap image = Portrait();
            GraphicsState state = g.Save();
            try
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                using (GraphicsPath sheet = PaperOutline(paper))
                {
                    using (GraphicsPath shade = (GraphicsPath)sheet.Clone())
                    using (Matrix offset = new Matrix())
                    using (Brush shadow = new SolidBrush(Color.FromArgb(25, 98, 72, 42)))
                    {
                        offset.Translate(Math.Min(2, side * .015f), Math.Min(3, side * .025f));
                        shade.Transform(offset); g.FillPath(shadow, shade);
                    }
                    using (Brush backing = new SolidBrush(Color.FromArgb(242, 231, 206))) g.FillPath(backing, sheet);
                    g.SetClip(sheet, CombineMode.Intersect);
                    if (image != null)
                    {
                        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                        g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                        g.DrawImage(image, drawing);
                    }
                    else
                        Typography.Line(g, "I", drawing, Math.Max(12, side * .4f), MapPaper.MutedInk,
                            TypeRole.Display, true, StringAlignment.Center);
                    using (Pen edge = new Pen(Color.FromArgb(110, MapPaper.Rule), .75f)) g.DrawPath(edge, sheet);
                }
            }
            finally { g.Restore(state); }
        }

        private static GraphicsPath PaperOutline(RectangleF paper)
        {
            float cut = Math.Max(.6f, Math.Min(3, paper.Width * .018f));
            GraphicsPath path = new GraphicsPath();
            path.AddPolygon(new[] {
                new PointF(paper.Left + cut, paper.Top + cut * .15f),
                new PointF(paper.Left + paper.Width * .43f, paper.Top),
                new PointF(paper.Right - cut, paper.Top + cut * .3f),
                new PointF(paper.Right, paper.Top + cut),
                new PointF(paper.Right - cut * .2f, paper.Bottom - cut),
                new PointF(paper.Right - cut, paper.Bottom),
                new PointF(paper.Left + paper.Width * .37f, paper.Bottom - cut * .2f),
                new PointF(paper.Left + cut * .6f, paper.Bottom),
                new PointF(paper.Left, paper.Bottom - cut),
                new PointF(paper.Left + cut * .15f, paper.Top + cut)
            });
            return path;
        }
    }
}
