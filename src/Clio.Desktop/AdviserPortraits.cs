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
            GraphicsState state = g.Save();
            try
            {
                using (Brush shadow = new SolidBrush(Color.FromArgb(105, 0, 0, 0))) g.FillEllipse(shadow, bounds.X + 3, bounds.Y + 4, bounds.Width, bounds.Height);
                using (GraphicsPath circle = new GraphicsPath())
                {
                    circle.AddEllipse(bounds); g.SetClip(circle, CombineMode.Intersect);
                    using (Brush backing = new SolidBrush(Color.FromArgb(13, 25, 28))) g.FillRectangle(backing, bounds);
                    if (portrait != null)
                    {
                        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                        g.DrawImage(portrait, bounds);
                    }
                    else
                    {
                        string name = CouncilPerspectives.Profile(id).Name;
                        Typography.Line(g, name.Substring(0, 1), new RectangleF(bounds.X, bounds.Y + bounds.Height * .19f, bounds.Width, bounds.Height * .62f), bounds.Height * .45f,
                            ink, TypeRole.Display, true, StringAlignment.Center);
                    }
                }
            }
            finally { g.Restore(state); }
            using (Pen rim = new Pen(Color.FromArgb(220, ink), 1.3f)) g.DrawEllipse(rim, bounds);
            using (Pen rim = new Pen(Color.FromArgb(65, ink), .8f)) g.DrawEllipse(rim, bounds.X - 3, bounds.Y - 3, bounds.Width + 6, bounds.Height + 6);
        }
    }
}
