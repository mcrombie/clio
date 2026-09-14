using System;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace Clio.Desktop
{
    // A restrained manuscript palette shared by campaign controls and their notes.
    internal static class MapPaper
    {
        internal static readonly Color Paper = Color.FromArgb(238, 224, 192);
        internal static readonly Color Ground = Color.FromArgb(215, 191, 145);
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
        private static readonly Bitmap patina = MakePatina();
        private static readonly Bitmap haze = MakeHaze();

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

        // These transparent layers are made once. Both the atlas and its small
        // paper notes carry the same subdued wear, without generating noise in
        // the paint loop or placing a dark stain directly behind every label.
        private static Bitmap MakePatina()
        {
            Bitmap image = new Bitmap(1024, 1024);
            Random random = new Random(42107);
            using (Graphics g = Graphics.FromImage(image))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                for (int i = 0; i < 42; i++)
                {
                    float x = random.Next(-100, 1024), y = random.Next(-100, 1024);
                    float width = random.Next(70, 310), height = random.Next(35, 180);
                    Blot(g, new RectangleF(x, y, width, height), Color.FromArgb(random.Next(7, 20), 126, 81, 33), i * .57);
                }
                for (int group = 0; group < 14; group++)
                {
                    float cx = random.Next(1024), cy = random.Next(1024);
                    for (int i = 0; i < 22; i++)
                    {
                        float x = cx + random.Next(-45, 46), y = cy + random.Next(-35, 36);
                        float r = (float)random.NextDouble() * 2.2f + .45f;
                        using (Brush foxing = new SolidBrush(Color.FromArgb(random.Next(7, 27), 113, 69, 30)))
                            g.FillEllipse(foxing, x, y, r * 1.2f, r);
                    }
                }
                // A broken tide line, as though an old spill dried into the sheet.
                for (int ring = 0; ring < 3; ring++)
                {
                    using (Pen rim = new Pen(Color.FromArgb(8 - ring * 2, 133, 87, 40), .75f + ring))
                    {
                        g.DrawArc(rim, 681 + ring, 713 + ring, 190 - ring * 2, 124 - ring, 18, 79);
                        g.DrawArc(rim, 681 + ring, 713 + ring, 190 - ring * 2, 124 - ring, 142, 104);
                    }
                }
            }
            return image;
        }

        private static Bitmap MakeHaze()
        {
            Bitmap image = new Bitmap(1024, 1024);
            Random random = new Random(73681);
            using (Graphics g = Graphics.FromImage(image))
            {
                g.Clear(Color.FromArgb(193, 183, 158));
                g.SmoothingMode = SmoothingMode.AntiAlias;
                // Broad overlapping washes conceal distant geography entirely;
                // this is ink-clouded paper, not the shape of undiscovered land.
                for (int i = 0; i < 95; i++)
                {
                    float x = random.Next(-280, 1000), y = random.Next(-280, 1000);
                    Color tint = i % 3 == 0 ? Color.FromArgb(56, 235, 222, 189) : Color.FromArgb(27, 118, 122, 113);
                    Blot(g, new RectangleF(x, y, random.Next(210, 520), random.Next(120, 380)), tint, i * .37);
                }
            }
            return image;
        }

        private static void Blot(Graphics g, RectangleF bounds, Color color, double phase)
        {
            PointF[] rim = new PointF[20];
            for (int i = 0; i < rim.Length; i++)
            {
                double angle = i * Math.PI * 2 / rim.Length;
                double swell = .87 + .07 * Math.Sin(angle * 3 + phase) + .06 * Math.Cos(angle * 5 - phase);
                rim[i] = new PointF(bounds.X + bounds.Width * (.5f + (float)(Math.Cos(angle) * swell * .5)),
                    bounds.Y + bounds.Height * (.5f + (float)(Math.Sin(angle) * swell * .5)));
            }
            using (GraphicsPath path = new GraphicsPath())
            {
                path.AddClosedCurve(rim, .4f);
                using (PathGradientBrush wash = new PathGradientBrush(path))
                {
                    wash.CenterColor = color;
                    wash.SurroundColors = new[] { Color.FromArgb(0, color.R, color.G, color.B) };
                    wash.FocusScales = new PointF(.2f, .2f);
                    g.FillPath(wash, path);
                }
            }
        }

        internal static void Texture(Graphics g, RectangleF bounds)
        {
            using (TextureBrush age = new TextureBrush(patina, WrapMode.Tile)) g.FillRectangle(age, bounds);
            using (TextureBrush grain = new TextureBrush(fibers, WrapMode.Tile)) g.FillRectangle(grain, bounds);
        }

        internal static void DistantHaze(Graphics g, RectangleF bounds)
        {
            using (TextureBrush cloud = new TextureBrush(haze, WrapMode.TileFlipXY))
            {
                cloud.ScaleTransform(1.6f, 1.6f);
                g.FillRectangle(cloud, bounds);
            }
            Texture(g, bounds);
        }

        internal static void AtlasWear(Graphics g, RectangleF bounds)
        {
            if (bounds.Width < 300 || bounds.Height < 300) return;
            float x = bounds.Left + bounds.Width * .343f, y = bounds.Top + bounds.Height * .61f;
            using (Pen shadow = new Pen(Color.FromArgb(12, 99, 70, 41), 3.8f))
            using (Pen crease = new Pen(Color.FromArgb(20, 107, 82, 50), .7f))
            using (Pen light = new Pen(Color.FromArgb(34, 252, 242, 211), 1.2f))
            {
                PointF[] vertical = { new PointF(x - 2, bounds.Top), new PointF(x, y - 180), new PointF(x - 1, y + 25), new PointF(x + 2, bounds.Bottom) };
                g.DrawLines(shadow, vertical); g.DrawLines(crease, vertical);
                g.DrawLine(light, x + 2, bounds.Top, x + 4, bounds.Bottom);
                g.DrawLine(shadow, bounds.Left, y, bounds.Right, y + 2);
                g.DrawLine(crease, bounds.Left, y - 1, bounds.Right, y + 1);
                g.DrawLine(light, bounds.Left, y + 2, bounds.Right, y + 4);
            }
        }

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
