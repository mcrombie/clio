using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using Clio.Simulation;

namespace Clio.Desktop
{
    // Screen-space cartographic marks. Identity comes only from the persistent band ID.
    // No fonts, external images, runtime randomness, or simulation state are required.
    internal static class IdentityArt
    {
        private static readonly Color Dark = Color.FromArgb(26, 43, 43);
        private static readonly Color Parchment = Color.FromArgb(243, 225, 181);
        private static readonly Color[] Colors = {
            Color.FromArgb(225, 188, 113), Color.FromArgb(103, 193, 183),
            Color.FromArgb(217, 130, 103), Color.FromArgb(150, 169, 221),
            Color.FromArgb(174, 193, 128), Color.FromArgb(119, 185, 217),
            Color.FromArgb(208, 158, 206), Color.FromArgb(230, 174, 121),
            Color.FromArgb(205, 210, 161), Color.FromArgb(125, 197, 156),
            Color.FromArgb(221, 158, 164), Color.FromArgb(176, 158, 212),
            Color.FromArgb(192, 196, 103), Color.FromArgb(142, 184, 190),
            Color.FromArgb(224, 203, 170), Color.FromArgb(182, 164, 145)
        };
        private static readonly string[] Sigils = {
            "Sun", "Crescent", "Stag", "Twin peaks", "Leaf", "Three rivers", "Star", "Hearth"
        };
        private static int Index(int id) { return id & Int32.MaxValue; }
        public static Color ColorFor(int id) { return Colors[Index(id) % Colors.Length]; }
        public static string SigilName(int id)
        { return Sigils[Index(id) % Sigils.Length] + ((Index(id) / 8) % 2 == 1 ? " shield" : " seal"); }

        public static void DrawEmblem(Graphics g, int id, RectangleF box, bool filled)
        {
            if (box.Width <= 0 || box.Height <= 0) return;
            float size = Math.Min(box.Width, box.Height);
            GraphicsState state = g.Save();
            try
            {
                g.TranslateTransform(box.X + (box.Width - size) / 2, box.Y + (box.Height - size) / 2);
                g.ScaleTransform(size / 100, size / 100);
                Color color = ColorFor(id);
                bool shield = (Index(id) / 8) % 2 == 1;
                using (GraphicsPath seal = Seal(shield, 4))
                using (Brush field = new SolidBrush(filled ? color : Color.FromArgb(238, Dark)))
                using (Pen edge = new Pen(color, 2.6f))
                {
                    using (Brush shadow = new SolidBrush(Color.FromArgb(60, 0, 0, 0)))
                    {
                        g.TranslateTransform(0, 3); g.FillPath(shadow, seal); g.TranslateTransform(0, -3);
                    }
                    g.FillPath(field, seal); g.DrawPath(edge, seal);
                    if (size >= 27)
                        using (GraphicsPath inner = Seal(shield, 10))
                        using (Pen line = new Pen(filled ? Color.FromArgb(100, Dark) : Color.FromArgb(95, color), 1.1f))
                            g.DrawPath(line, inner);
                }
                GraphicsState symbolState = g.Save();
                g.TranslateTransform(22, shield ? 18 : 22); g.ScaleTransform(.56f, .56f);
                Glyph(g, id, filled ? Dark : color);
                g.Restore(symbolState);
                if (!shield && size >= 42)
                    using (Brush dots = new SolidBrush(color))
                    {
                        g.FillEllipse(dots, 47.8f, 12, 4.4f, 4.4f);
                        g.FillEllipse(dots, 47.8f, 83.6f, 4.4f, 4.4f);
                        g.FillEllipse(dots, 12, 47.8f, 4.4f, 4.4f);
                        g.FillEllipse(dots, 83.6f, 47.8f, 4.4f, 4.4f);
                    }
            }
            finally { g.Restore(state); }
        }

        private static GraphicsPath Seal(bool shield, float inset)
        {
            GraphicsPath p = new GraphicsPath();
            if (!shield) p.AddEllipse(inset, inset, 100 - 2 * inset, 100 - 2 * inset);
            else
            {
                p.AddLines(new[] { new PointF(inset + 5, inset + 5), new PointF(50, inset), new PointF(95 - inset, inset + 5), new PointF(92 - inset, 55) });
                p.AddBezier(92 - inset, 55, 87 - inset, 75, 65, 86 - inset, 50, 100 - inset);
                p.AddBezier(50, 100 - inset, 35, 86 - inset, 13 + inset, 75, 8 + inset, 55);
                p.CloseFigure();
            }
            return p;
        }

        // Glyphs have a 100 x 100 design box and remain legible at banner size.
        private static void Glyph(Graphics g, int id, Color color)
        {
            using (Brush b = new SolidBrush(color))
            using (Pen pen = new Pen(color, 5) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round })
            {
                switch (Index(id) % 8)
                {
                    case 0:
                        g.FillEllipse(b, 31, 31, 38, 38);
                        for (int i = 0; i < 8; i++)
                        {
                            double a = i * Math.PI / 4;
                            float x = (float)Math.Cos(a), y = (float)Math.Sin(a);
                            g.FillPolygon(b, new[] { new PointF(50 + x * 46, 50 + y * 46), new PointF(50 + x * 27 - y * 4, 50 + y * 27 + x * 4), new PointF(50 + x * 27 + y * 4, 50 + y * 27 - x * 4) });
                        }
                        break;
                    case 1:
                        using (GraphicsPath moon = new GraphicsPath())
                        {
                            moon.AddBezier(71, 8, 19, -3, -1, 49, 28, 80);
                            moon.AddBezier(28, 80, 44, 97, 67, 94, 83, 78);
                            moon.AddBezier(83, 78, 33, 82, 25, 28, 71, 8);
                            moon.CloseFigure(); g.FillPath(b, moon);
                        }
                        g.FillPolygon(b, Star(78, 33, 11, 3.7f, 4));
                        break;
                    case 2:
                        g.FillPolygon(b, Points(35, 43, 44, 48, 50, 43, 56, 48, 65, 43, 61, 63, 57, 70, 55, 87, 45, 87, 43, 70, 39, 63));
                        g.DrawLines(pen, Points(43, 52, 32, 35, 23, 29, 20, 10));
                        g.DrawLines(pen, Points(57, 52, 68, 35, 77, 29, 80, 10));
                        g.DrawLines(pen, Points(33, 36, 33, 20, 28, 10));
                        g.DrawLines(pen, Points(67, 36, 67, 20, 72, 10));
                        g.DrawLines(pen, Points(25, 30, 11, 24, 8, 16));
                        g.DrawLines(pen, Points(75, 30, 89, 24, 92, 16));
                        g.DrawLine(pen, 33, 24, 44, 15); g.DrawLine(pen, 67, 24, 56, 15);
                        break;
                    case 3:
                        g.FillPolygon(b, Points(5, 82, 37, 24, 68, 82, 56, 82, 36, 47, 18, 82));
                        g.FillPolygon(b, Points(49, 69, 69, 13, 99, 82, 85, 82, 69, 42, 57, 78));
                        g.DrawLine(pen, 12, 89, 91, 89);
                        break;
                    case 4:
                        using (GraphicsPath leaf = new GraphicsPath())
                        {
                            leaf.AddBezier(24, 77, 0, 26, 65, 24, 84, 9);
                            leaf.AddBezier(84, 9, 89, 58, 81, 91, 32, 84);
                            leaf.AddBezier(32, 84, 53, 62, 63, 46, 68, 32);
                            leaf.AddBezier(68, 32, 48, 53, 33, 67, 24, 77);
                            g.FillPath(b, leaf);
                        }
                        g.DrawLine(pen, 15, 93, 41, 64);
                        break;
                    case 5:
                        pen.Width = 7;
                        for (int i = 0; i < 3; i++)
                        {
                            float x = 22 + i * 27;
                            g.DrawBezier(pen, x + 7, 11, x - 28, 33, x + 26, 60, x - 7, 90);
                        }
                        break;
                    case 6:
                        g.FillPolygon(b, Star(50, 50, 45, 17, 8));
                        break;
                    default:
                        using (GraphicsPath flame = new GraphicsPath())
                        {
                            flame.AddBezier(49, 4, 67, 34, 37, 42, 61, 61);
                            flame.AddBezier(61, 61, 65, 49, 73, 43, 79, 40);
                            flame.AddBezier(79, 40, 93, 82, 66, 93, 48, 93);
                            flame.AddBezier(48, 93, 13, 94, 10, 64, 26, 44);
                            flame.AddBezier(26, 44, 18, 66, 37, 74, 35, 55);
                            flame.AddBezier(35, 55, 28, 33, 54, 26, 49, 4);
                            flame.CloseFigure(); g.FillPath(b, flame);
                        }
                        break;
                }
            }
        }

        // The anchor is the foot of the pole, and scale=1 makes a 48px-high banner.
        public static void DrawBanner(Graphics g, int id, float x, float y, float scale, bool player, bool settled)
        {
            if (scale <= 0) return;
            GraphicsState state = g.Save();
            try
            {
                g.TranslateTransform(x, y); g.ScaleTransform(scale, scale);
                Color color = ColorFor(id);
                using (Brush shadow = new SolidBrush(Color.FromArgb(75, 7, 19, 18))) g.FillEllipse(shadow, -21, -3, 51, 10);
                using (Brush shade = new SolidBrush(Color.FromArgb(58, 72, 63)))
                using (Brush canvas = new SolidBrush(Color.FromArgb(209, 196, 159)))
                using (Pen seams = new Pen(Color.FromArgb(120, 71, 75, 59), .65f))
                {
                    if (settled)
                    {
                        g.FillRectangle(canvas, -17, -9, 17, 10); g.FillPolygon(shade, Points(-21, -8, -9, -21, 4, -8));
                        g.FillRectangle(shade, -11, -6, 5, 7); g.DrawLine(seams, -20, -8, -9, -19);
                        g.FillRectangle(canvas, 7, -6, 11, 7); g.FillPolygon(shade, Points(4, -6, 13, -14, 22, -6));
                    }
                    else
                    {
                        g.FillPolygon(canvas, Points(-23, 0, -12, -17, 2, 0));
                        g.FillPolygon(shade, Points(-12, -17, -7, 0, 2, 0));
                        g.FillPolygon(shade, Points(-16, 0, -13, -7, -10, 0));
                        g.DrawLine(seams, -23, 0, -12, -17);
                        g.DrawLine(seams, -12, -17, -7, 0);
                    }
                }
                using (Pen poleShadow = new Pen(Color.FromArgb(130, 13, 27, 24), 3.2f)) g.DrawLine(poleShadow, 1, -44, 1, 2);
                using (Pen pole = new Pen(Color.FromArgb(226, 212, 174), 1.6f)) g.DrawLine(pole, 0, -44, 0, 1);
                using (GraphicsPath flag = new GraphicsPath())
                {
                    flag.AddBezier(1, -43, 10, -47, 21, -39, 34, -43);
                    if ((Index(id) / 8) % 2 == 0)
                    {
                        flag.AddLine(34, -43, 30, -32);
                        flag.AddLine(30, -32, 34, -22);
                    }
                    else flag.AddLine(34, -43, 34, -22);
                    flag.AddBezier(34, -22, 21, -17, 11, -25, 1, -22);
                    flag.CloseFigure();
                    using (LinearGradientBrush fabric = new LinearGradientBrush(new RectangleF(1, -46, 34, 26), color, Mix(color, Dark, .24), 0f)) g.FillPath(fabric, flag);
                    using (Pen rim = new Pen(player ? Parchment : Mix(color, Parchment, .25), .7f)) g.DrawPath(rim, flag);
                    using (Pen fold = new Pen(Color.FromArgb(60, Parchment), .8f)) g.DrawBezier(fold, 3, -41, 11, -43, 21, -37, 29, -40);
                    GraphicsState flagState = g.Save();
                    g.TranslateTransform(8, -41); g.ScaleTransform(.175f, .175f); Glyph(g, id, Dark); g.Restore(flagState);
                }
                using (Brush finial = new SolidBrush(player ? Parchment : color)) g.FillPolygon(finial, Points(0, -50, 2.5f, -46, 0, -42, -2.5f, -46));
                if (player)
                    using (Pen ring = new Pen(Color.FromArgb(175, color), 1.1f)) g.DrawArc(ring, -24, -6, 50, 13, 10, 162);
            }
            finally { g.Restore(state); }
        }

        public static void DrawAnimal(Graphics g, BeastKind kind, RectangleF box, Color color, bool domestic)
        {
            if (box.Width <= 0 || box.Height <= 0) return;
            float scale = Math.Min(box.Width / 108, box.Height / 78);
            GraphicsState state = g.Save();
            try
            {
                g.TranslateTransform(box.X + (box.Width - 108 * scale) / 2, box.Y + (box.Height - 78 * scale) / 2);
                g.ScaleTransform(scale, scale);
                using (Brush b = new SolidBrush(color))
                using (Pen line = new Pen(color, 4.5f) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round })
                {
                    if (domestic)
                        using (Pen ring = new Pen(Color.FromArgb(180, color), 2.5f)) g.DrawEllipse(ring, 3, 2, 100, 74);
                    switch (kind)
                    {
                        case BeastKind.Wolves:
                            FillAnimal(g, b, Points(9, 48, 21, 35, 34, 36, 55, 32, 65, 20, 68, 8, 75, 19, 83, 13, 84, 27, 100, 34, 96, 41, 82, 42, 75, 48, 73, 68, 67, 68, 66, 47, 56, 50, 39, 48, 32, 59, 29, 70, 21, 70, 25, 54, 29, 45, 20, 43, 10, 57, 3, 54));
                            g.FillPolygon(b, Points(38, 45, 44, 51, 42, 65, 48, 70, 39, 70, 35, 65));
                            break;
                        case BeastKind.Aurochs:
                            using (GraphicsPath ox = new GraphicsPath())
                            {
                                ox.AddBezier(16, 31, 31, 17, 55, 20, 66, 27);
                                ox.AddLines(Points(66, 27, 75, 24, 83, 28, 94, 33, 94, 46, 85, 50, 74, 46, 70, 63, 73, 70, 64, 70, 61, 48, 38, 51, 28, 47, 25, 66, 30, 71, 20, 71, 18, 49));
                                ox.CloseFigure(); g.FillPath(b, ox);
                            }
                            g.DrawBezier(line, 20, 34, 6, 32, 19, 60, 7, 59);
                            g.DrawBezier(line, 76, 31, 62, 21, 69, 12, 66, 8);
                            g.DrawBezier(line, 83, 29, 95, 22, 88, 13, 94, 9);
                            g.FillPolygon(b, Points(33, 45, 38, 49, 34, 65, 40, 70, 31, 70));
                            break;
                        case BeastKind.Mammoths:
                            using (GraphicsPath mammoth = new GraphicsPath())
                            {
                                mammoth.AddBezier(13, 37, 11, 12, 38, 12, 52, 22);
                                mammoth.AddBezier(52, 22, 62, 10, 88, 14, 89, 34);
                                mammoth.AddBezier(89, 34, 94, 47, 82, 63, 99, 59);
                                mammoth.AddBezier(99, 59, 96, 75, 79, 74, 79, 55);
                                mammoth.AddLines(Points(79, 55, 73, 47, 67, 53, 65, 69, 55, 69, 53, 51, 40, 50, 35, 69, 25, 69, 25, 47, 17, 47));
                                mammoth.CloseFigure(); g.FillPath(b, mammoth);
                            }
                            g.DrawBezier(line, 14, 36, 5, 35, 9, 46, 6, 49);
                            using (Pen tusk = new Pen(color, 3.7f) { StartCap = LineCap.Round, EndCap = LineCap.Round })
                                g.DrawBezier(tusk, 77, 39, 79, 57, 102, 58, 101, 39);
                            break;
                        case BeastKind.Deer:
                            FillAnimal(g, b, Points(17, 39, 33, 34, 55, 36, 63, 24, 68, 14, 76, 17, 81, 21, 93, 24, 91, 29, 78, 30, 73, 42, 65, 48, 67, 69, 61, 69, 58, 46, 38, 46, 28, 58, 28, 69, 22, 69, 23, 54, 26, 45, 18, 44, 10, 34));
                            g.DrawLines(line, Points(72, 19, 66, 10, 59, 5, 58, 1));
                            g.DrawLines(line, Points(73, 19, 76, 10, 82, 5, 81, 1));
                            line.Width = 3;
                            g.DrawLine(line, 64, 10, 66, 2); g.DrawLine(line, 77, 10, 74, 3);
                            g.DrawLine(line, 59, 5, 52, 6); g.DrawLine(line, 82, 5, 89, 6);
                            g.DrawLines(line, Points(39, 45, 35, 59, 43, 68));
                            break;
                        default:
                            using (GraphicsPath wing = new GraphicsPath())
                            {
                                wing.AddLines(Points(51, 47, 33, 6, 73, 20));
                                wing.AddBezier(73, 20, 58, 18, 60, 29, 63, 33);
                                wing.AddBezier(63, 33, 48, 28, 48, 38, 51, 47);
                                wing.CloseFigure(); g.FillPath(b, wing);
                            }
                            FillAnimal(g, b, Points(25, 50, 43, 43, 60, 44, 68, 35, 75, 17, 80, 21, 90, 18, 85, 25, 99, 31, 94, 39, 80, 37, 76, 48, 67, 52, 72, 65, 81, 69, 66, 70, 59, 54, 43, 54, 33, 66, 40, 70, 26, 70, 29, 57));
                            g.DrawBezier(line, 37, 49, 17, 76, -2, 58, 10, 42);
                            g.FillPolygon(b, Points(10, 35, 17, 45, 5, 46));
                            break;
                    }
                    if (domestic)
                    {
                        using (Pen collar = new Pen(Dark, 3.8f)) g.DrawLine(collar, 71, 37, 80, 42);
                        using (Brush tag = new SolidBrush(Parchment)) g.FillEllipse(tag, 75, 41, 5, 5);
                    }
                }
            }
            finally { g.Restore(state); }
        }

        private static void FillAnimal(Graphics g, Brush b, PointF[] points) { g.FillPolygon(b, points); }
        private static Color Mix(Color a, Color b, double t)
        { return Color.FromArgb((int)(a.R + (b.R - a.R) * t), (int)(a.G + (b.G - a.G) * t), (int)(a.B + (b.B - a.B) * t)); }
        private static PointF[] Points(params float[] xy)
        {
            PointF[] p = new PointF[xy.Length / 2];
            for (int i = 0; i < p.Length; i++) p[i] = new PointF(xy[i * 2], xy[i * 2 + 1]);
            return p;
        }
        private static PointF[] Star(float x, float y, float outer, float inner, int rays)
        {
            PointF[] p = new PointF[rays * 2];
            for (int i = 0; i < p.Length; i++)
            {
                double a = i * Math.PI / rays - Math.PI / 2;
                float r = i % 2 == 0 ? outer : inner;
                p[i] = new PointF(x + (float)Math.Cos(a) * r, y + (float)Math.Sin(a) * r);
            }
            return p;
        }
    }
}
