using System;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace Clio.Desktop
{
    // A restrained manuscript palette shared by campaign controls and their notes.
    internal static class MapPaper
    {
        internal static readonly Color Paper = Color.FromArgb(243, 233, 209);
        internal static readonly Color Ground = Color.FromArgb(226, 211, 178);
        internal static readonly Color Ink = Color.FromArgb(65, 48, 35);
        internal static readonly Color MutedInk = Color.FromArgb(120, 99, 73);
        internal static readonly Color Russet = Color.FromArgb(139, 89, 44);
        internal static readonly Color Rule = Color.FromArgb(159, 134, 98);
        internal static readonly Color DisabledInk = Color.FromArgb(163, 148, 120);
        internal static readonly Color Warning = Color.FromArgb(158, 67, 50);
        internal static readonly Color Blue = Color.FromArgb(67, 96, 112);
        internal static readonly Color Green = Color.FromArgb(79, 102, 66);
        internal static readonly Color HoverWash = Color.FromArgb(227, 209, 171);
        internal static readonly Color SelectedWash = Color.FromArgb(221, 198, 153);
        private static readonly Bitmap fibers = MakeFibers();

        private static Bitmap MakeFibers()
        {
            Bitmap image = new Bitmap(192, 192);
            Random random = new Random(61841);
            using (Graphics g = Graphics.FromImage(image))
            {
                for (int i = 0; i < 2100; i++)
                {
                    int x = random.Next(192), y = random.Next(192);
                    using (Brush speck = new SolidBrush(Color.FromArgb(random.Next(2, 10), 111, 84, 45)))
                        g.FillEllipse(speck, x, y, random.Next(1, 3), 1);
                }
                for (int i = 0; i < 220; i++)
                {
                    float x = random.Next(192), y = random.Next(192);
                    using (Pen fiber = new Pen(Color.FromArgb(random.Next(3, 9), 123, 94, 55), .5f))
                        g.DrawLine(fiber, x, y, x + random.Next(2, 9), y + random.Next(-2, 3));
                }
            }
            return image;
        }

        internal static void Texture(Graphics g, RectangleF bounds)
        { using (TextureBrush grain = new TextureBrush(fibers, WrapMode.Tile)) g.FillRectangle(grain, bounds); }

        internal static void Surface(Graphics g, RectangleF bounds, bool ornament, bool active = false)
        {
            if (bounds.Width <= 0 || bounds.Height <= 0) return;
            Color paper = active ? SelectedWash : Paper;
            using (LinearGradientBrush wash = new LinearGradientBrush(bounds, Art.Mix(paper, Color.White, .055), Art.Mix(paper, Ground, .14), 90)) g.FillRectangle(wash, bounds);
            Texture(g, bounds);
            Frame(g, bounds, ornament);
        }

        internal static void Shape(Graphics g, GraphicsPath path, RectangleF bounds, bool active = false, bool hover = false)
        {
            Color paper = active ? SelectedWash : hover ? HoverWash : Paper;
            using (Brush wash = new SolidBrush(paper)) g.FillPath(wash, path);
            GraphicsState saved = g.Save();
            try { g.SetClip(path, CombineMode.Intersect); Texture(g, bounds); }
            finally { g.Restore(saved); }
            using (Pen edge = new Pen(Color.FromArgb(active || hover ? 215 : 149, active ? Russet : Rule), active ? 1.2f : .8f)) g.DrawPath(edge, path);
        }

        internal static void Frame(Graphics g, RectangleF bounds, bool ornament)
        {
            float left = bounds.Left + .6f, right = bounds.Right - .6f, top = bounds.Top + .6f, bottom = bounds.Bottom - .6f;
            PointF[] outline = {
                new PointF(left, top + .3f), new PointF(left + bounds.Width * .27f, top),
                new PointF(left + bounds.Width * .68f, top + .55f), new PointF(right, top + .1f),
                new PointF(right - .25f, top + bounds.Height * .37f), new PointF(right, bottom),
                new PointF(left + bounds.Width * .61f, bottom - .4f), new PointF(left + bounds.Width * .21f, bottom + .05f),
                new PointF(left + .2f, bottom - .3f), new PointF(left, top + bounds.Height * .44f), new PointF(left, top + .3f)
            };
            using (Pen ink = new Pen(Color.FromArgb(162, Rule), .8f) { LineJoin = LineJoin.Round }) g.DrawLines(ink, outline);
            if (!ornament) return;
            Art.Line(g, Color.FromArgb(113, Russet), .7f, left + 11, top + 4, left + 40, top + 4.3f);
            Art.Line(g, Color.FromArgb(113, Russet), .7f, right - 40, bottom - 4.3f, right - 11, bottom - 4);
        }
    }
}
