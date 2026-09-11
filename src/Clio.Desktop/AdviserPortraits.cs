using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;

namespace Clio.Desktop
{
    // Portraits travel inside the executable, including when a review or the
    // stable launcher runs it from a different working directory.
    internal static class AdviserPortraits
    {
        private static readonly Dictionary<CounsellorId, Bitmap> portraits = new Dictionary<CounsellorId, Bitmap>();
        private static readonly object sync = new object();

        internal static bool HasPortrait(CounsellorId id) { return Portrait(id) != null; }

        private static Bitmap Portrait(CounsellorId id)
        {
            lock (sync)
            {
                Bitmap cached;
                if (portraits.TryGetValue(id, out cached)) return cached;
                string key = CouncilPerspectives.Profile(id).PortraitKey;
                using (Stream source = typeof(AdviserPortraits).Assembly.GetManifestResourceStream("Clio.Advisers." + key + ".png"))
                {
                    if (source != null)
                    using (Image original = Image.FromStream(source))
                    {
                        cached = new Bitmap(320, 320, PixelFormat.Format32bppPArgb);
                        using (Graphics g = Graphics.FromImage(cached))
                        {
                            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                            float side = Math.Min(original.Width, original.Height);
                            g.DrawImage(original, new RectangleF(0, 0, 320, 320), new RectangleF((original.Width - side) / 2, (original.Height - side) / 2, side, side), GraphicsUnit.Pixel);
                        }
                    }
                }
                // The release build validates all four assets. This quiet
                // fallback also keeps source-only development builds usable.
                portraits[id] = cached; return cached;
            }
        }

        internal static void Draw(Graphics g, RectangleF bounds, CounsellorId id, Color ink)
        {
            if (bounds.Width <= 0 || bounds.Height <= 0) return;
            Bitmap portrait = Portrait(id);
            float side = Math.Min(bounds.Width, bounds.Height);
            RectangleF paper = new RectangleF(bounds.X + (bounds.Width - side) / 2,
                bounds.Y + (bounds.Height - side) / 2, side, side);
            float inset = Math.Max(1, side * .035f);
            RectangleF drawing = RectangleF.Inflate(paper, -inset, -inset);
            GraphicsState state = g.Save();
            try
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                using (GraphicsPath sheet = PaperOutline(paper))
                {
                    // A small unpolished sheet, not a badge. The image keeps its
                    // square composition and its own drawn-paper background.
                    using (GraphicsPath shade = (GraphicsPath)sheet.Clone())
                    using (Matrix offset = new Matrix())
                    using (Brush shadow = new SolidBrush(Color.FromArgb(48, 0, 0, 0)))
                    {
                        offset.Translate(Math.Min(2, side * .025f), Math.Min(3, side * .04f));
                        shade.Transform(offset); g.FillPath(shadow, shade);
                    }
                    using (Brush backing = new SolidBrush(Color.FromArgb(243, 238, 223))) g.FillPath(backing, sheet);
                    if (portrait != null)
                    {
                        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                        g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                        g.DrawImage(portrait, drawing);
                    }
                    else
                    {
                        string name = CouncilPerspectives.Profile(id).Name;
                        Typography.Line(g, name.Substring(0, 1), new RectangleF(paper.X, paper.Y + side * .19f, side, side * .62f), side * .45f,
                            Color.FromArgb(77, 68, 54), TypeRole.Display, true, StringAlignment.Center);
                    }
                    using (Pen edge = new Pen(Color.FromArgb(125, 84, 73, 57), .8f)) g.DrawPath(edge, sheet);
                }
            }
            finally { g.Restore(state); }
        }

        private static GraphicsPath PaperOutline(RectangleF paper)
        {
            float cut = Math.Max(1, Math.Min(4, paper.Width * .025f));
            GraphicsPath path = new GraphicsPath();
            path.AddPolygon(new[] {
                new PointF(paper.Left + cut, paper.Top),
                new PointF(paper.Right - cut * .65f, paper.Top),
                new PointF(paper.Right, paper.Top + cut * .8f),
                new PointF(paper.Right, paper.Bottom - cut),
                new PointF(paper.Right - cut * .85f, paper.Bottom),
                new PointF(paper.Left + cut * .65f, paper.Bottom),
                new PointF(paper.Left, paper.Bottom - cut * .75f),
                new PointF(paper.Left, paper.Top + cut)
            });
            return path;
        }
    }
}
