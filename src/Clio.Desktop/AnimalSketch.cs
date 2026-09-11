using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using Clio.Simulation;

namespace Clio.Desktop
{
    // Small naturalist studies, drawn in the same 108 x 78 space as the old glyphs.
    // Paper-filled contours separate the animals from terrain without a token frame.
    internal static class AnimalSketch
    {
        public static void Draw(Graphics g, BeastKind kind, RectangleF box, Color color, bool domestic)
        {
            if (box.Width <= 0 || box.Height <= 0) return;
            float scale = Math.Min(box.Width / 108, box.Height / 78);
            float width = Math.Min(4.2f, Math.Max(1.25f, .82f / scale));
            bool detail = scale >= .48f;
            GraphicsState state = g.Save();
            try
            {
                g.TranslateTransform(box.X + (box.Width - 108 * scale) / 2, box.Y + (box.Height - 78 * scale) / 2);
                g.ScaleTransform(scale, scale);
                g.SmoothingMode = SmoothingMode.AntiAlias;
                using (Brush paper = new SolidBrush(MapPaper.Paper))
                using (Brush ink = new SolidBrush(color))
                using (Pen contour = new Pen(color, width) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round })
                using (Pen fine = new Pen(Color.FromArgb(205, color), width * .68f) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round })
                {
                    switch (kind)
                    {
                        case BeastKind.Wolves: Wolf(g, paper, ink, contour, fine, domestic, detail); break;
                        case BeastKind.Aurochs: Ox(g, paper, ink, contour, fine, domestic, detail); break;
                        case BeastKind.Mammoths: Mammoth(g, paper, ink, contour, fine, detail); break;
                        case BeastKind.Deer: Deer(g, paper, ink, contour, fine, detail); break;
                        case BeastKind.Goats: Goat(g, paper, ink, contour, fine, detail); break;
                        default: Dragon(g, paper, ink, contour, fine, detail); break;
                    }
                }
            }
            finally { g.Restore(state); }
        }

        private static void Wolf(Graphics g, Brush paper, Brush ink, Pen line, Pen fine, bool domestic, bool detail)
        {
            // A dog carries its curved tail up; the wolf's brush hangs behind its hocks.
            using (GraphicsPath tail = new GraphicsPath())
            {
                if (domestic)
                {
                    tail.AddBezier(24, 37, 7, 34, 4, 19, 13, 17);
                    tail.AddBezier(13, 17, 23, 14, 23, 27, 17, 25);
                    tail.AddBezier(17, 25, 16, 20, 12, 22, 16, 29);
                    tail.AddBezier(16, 29, 20, 34, 26, 30, 28, 35);
                }
                else
                {
                    tail.AddBezier(25, 35, 12, 35, 4, 46, 4, 61);
                    tail.AddBezier(4, 61, 10, 58, 17, 54, 20, 46);
                    tail.AddBezier(20, 46, 23, 42, 29, 41, 25, 35);
                }
                Fill(g, paper, line, tail);
            }
            Leg(g, paper, fine, 37, 43, 37, 56, 46, 69, 3.2f, false);
            Leg(g, paper, fine, 67, 43, 74, 56, 80, 68, 3, false);
            using (GraphicsPath body = new GraphicsPath())
            {
                body.AddBezier(19, 36, 29, 29, 45, 33, 60, 28);
                body.AddBezier(60, 28, 68, 27, 69, 22, 75, 21);
                if (domestic)
                {
                    body.AddBezier(75, 21, 78, 16, 84, 17, 88, 22);
                    body.AddBezier(88, 22, 91, 26, 98, 25, 100, 29);
                    body.AddBezier(100, 29, 101, 33, 96, 35, 91, 35);
                }
                else
                {
                    body.AddBezier(75, 21, 75, 18, 74, 14, 76, 11);
                    body.AddBezier(76, 11, 79, 12, 81, 18, 83, 19);
                    body.AddBezier(83, 19, 86, 17, 88, 17, 89, 21);
                    body.AddBezier(89, 21, 90, 25, 100, 26, 103, 29);
                    body.AddBezier(103, 29, 103, 33, 97, 35, 91, 35);
                }
                body.AddBezier(91, 35, 82, 36, 81, 43, 74, 47);
                body.AddBezier(74, 47, 68, 54, 58, 47, 49, 47);
                body.AddBezier(49, 47, 39, 46, 33, 52, 26, 48);
                body.AddBezier(26, 48, 19, 46, 16, 43, 19, 36);
                Fill(g, paper, line, body);
            }
            Leg(g, paper, line, 29, 44, 26, 57, 18, 71, 4.2f, false);
            Leg(g, paper, line, 70, 44, 67, 59, 68, 71, 3.8f, false);
            Curve(g, fine, 30, 36, 36, 38, 36, 43, 32, 47);
            Curve(g, fine, 71, 30, 64, 35, 65, 40, 68, 45);
            Eye(g, ink, 88, 26, detail ? 1.6f : 1.9f);
            Curve(g, fine, 93, 32, 96, 32, 98, 31, 100, 30);
            if (domestic)
            {
                Curve(g, line, 78, 20, 71, 23, 75, 31, 80, 30);
                Curve(g, line, 74, 34, 77, 37, 81, 39, 85, 38);
            }
            if (!detail) return;
            Curve(g, fine, 42, 35, 47, 37, 52, 37, 57, 35);
            Curve(g, fine, 81, 28, 80, 31, 82, 32, 86, 32);
            Hatch(g, fine, 59, 31, 3, 3, 4, 5);
            Hatch(g, fine, 21, 37, 2, 3, 3, 5);
            if (!domestic) Hatch(g, fine, 7, 50, 2, -3, 3, 3);
        }

        private static void Ox(Graphics g, Brush paper, Brush ink, Pen line, Pen fine, bool domestic, bool detail)
        {
            Curve(g, line, 20, 29, 4, 28, 14, 53, 6, 59);
            Curve(g, line, 6, 56, 4, 59, 4, 61, 6, 62);
            Leg(g, paper, fine, 35, 46, 36, 58, 41, 71, 4.6f, true);
            Leg(g, paper, fine, 65, 45, 73, 56, 78, 70, 4.8f, true);
            using (GraphicsPath body = new GraphicsPath())
            {
                body.AddBezier(17, 29, 28, 20, 43, 25, 56, 20);
                body.AddBezier(56, 20, 65, 16, 72, 24, 76, 28);
                body.AddBezier(76, 28, 80, 26, 86, 28, 89, 34);
                body.AddBezier(89, 34, 90, 39, 98, 41, 98, 47);
                body.AddBezier(98, 47, 95, 53, 89, 52, 84, 50);
                body.AddBezier(84, 50, 78, 49, 75, 53, 69, 54);
                body.AddBezier(69, 54, 63, 59, 57, 54, 53, 53);
                body.AddBezier(53, 53, 43, 54, 29, 53, 20, 48);
                body.AddBezier(20, 48, 14, 43, 15, 36, 17, 29);
                Fill(g, paper, line, body);
            }
            Leg(g, paper, line, 25, 45, 23, 58, 21, 72, 5.5f, true);
            Leg(g, paper, line, 66, 47, 65, 59, 66, 72, 5.3f, true);
            Horn(g, paper, line, domestic ? 79 : 78, 30, domestic ? 67 : 62, domestic ? 19 : 16, domestic ? 69 : 67, domestic ? 14 : 4, 4);
            Horn(g, paper, line, 85, 30, domestic ? 99 : 104, domestic ? 22 : 18, domestic ? 96 : 96, domestic ? 15 : 4, 3.4f);
            Curve(g, line, 80, 32, 73, 29, 72, 33, 77, 36);
            Curve(g, fine, 66, 28, 62, 35, 62, 42, 67, 48);
            Eye(g, ink, 87, 37, detail ? 1.5f : 1.8f);
            Curve(g, fine, 91, 47, 93, 47, 95, 47, 96, 46);
            if (!detail) return;
            Eye(g, ink, 94, 44, 1.1f);
            Curve(g, fine, 25, 29, 33, 32, 36, 41, 31, 48);
            Curve(g, fine, 45, 33, 42, 39, 44, 44, 48, 47);
            Hatch(g, fine, 57, 25, 2, 4, -2, 5);
            Hatch(g, fine, 72, 42, 3, 0, -1, 6);
            Hatch(g, fine, 35, 48, 4, 1, 2, 3);
        }

        private static void Mammoth(Graphics g, Brush paper, Brush ink, Pen line, Pen fine, bool detail)
        {
            Curve(g, line, 16, 38, 8, 38, 10, 47, 6, 50);
            Leg(g, paper, fine, 35, 44, 35, 58, 37, 72, 9, false);
            Leg(g, paper, fine, 66, 42, 68, 59, 72, 70, 8, false);
            using (GraphicsPath body = new GraphicsPath())
            {
                body.AddBezier(14, 39, 14, 22, 23, 15, 36, 18);
                body.AddBezier(36, 18, 48, 17, 52, 25, 58, 23);
                body.AddBezier(58, 23, 66, 10, 81, 10, 86, 24);
                body.AddBezier(86, 24, 90, 33, 89, 41, 86, 49);
                body.AddBezier(86, 49, 83, 59, 88, 67, 96, 61);
                body.AddBezier(96, 61, 101, 60, 99, 68, 95, 71);
                body.AddBezier(95, 71, 83, 78, 76, 67, 77, 55);
                body.AddBezier(77, 55, 78, 50, 76, 47, 72, 47);
                body.AddBezier(72, 47, 66, 53, 63, 52, 57, 53);
                body.AddBezier(57, 53, 44, 58, 26, 54, 19, 50);
                body.AddBezier(19, 50, 15, 49, 12, 45, 14, 39);
                Fill(g, paper, line, body);
            }
            Leg(g, paper, line, 24, 47, 24, 60, 24, 72, 10, false);
            Leg(g, paper, line, 56, 49, 57, 59, 57, 72, 9, false);
            using (GraphicsPath ear = new GraphicsPath())
            {
                ear.AddBezier(71, 28, 61, 21, 58, 33, 64, 41);
                ear.AddBezier(64, 41, 71, 46, 75, 35, 71, 28);
                Fill(g, paper, fine, ear);
            }
            // Two narrow, upturned tusks read independently from the hanging trunk.
            using (GraphicsPath tusk = new GraphicsPath())
            {
                tusk.AddBezier(78, 42, 86, 58, 98, 59, 99, 43);
                tusk.AddBezier(99, 43, 103, 61, 85, 65, 75, 46);
                Fill(g, paper, fine, tusk);
            }
            using (GraphicsPath tusk = new GraphicsPath())
            {
                tusk.AddBezier(81, 43, 91, 62, 103, 59, 105, 39);
                tusk.AddBezier(105, 39, 108, 64, 88, 71, 77, 47);
                Fill(g, paper, line, tusk);
            }
            Eye(g, ink, 82, 32, detail ? 1.5f : 1.9f);
            if (!detail) return;
            Hatch(g, fine, 20, 29, 3, -1, -2, 9);
            Hatch(g, fine, 33, 26, 4, 1, 0, 11);
            Hatch(g, fine, 24, 43, 4, 1, 1, 7);
            Hatch(g, fine, 43, 40, 4, -1, 0, 10);
            Hatch(g, fine, 69, 17, 3, 0, -1, 5);
            Curve(g, fine, 80, 52, 82, 53, 83, 53, 85, 53);
            Curve(g, fine, 80, 58, 81, 60, 83, 60, 85, 59);
        }

        private static void Deer(Graphics g, Brush paper, Brush ink, Pen line, Pen fine, bool detail)
        {
            Leg(g, paper, fine, 37, 44, 34, 60, 43, 72, 2.8f, true);
            Leg(g, paper, fine, 62, 42, 65, 60, 74, 71, 2.6f, true);
            using (GraphicsPath body = new GraphicsPath())
            {
                body.AddBezier(18, 38, 26, 31, 42, 34, 53, 34);
                body.AddBezier(53, 34, 60, 32, 61, 24, 68, 20);
                body.AddBezier(68, 20, 73, 15, 79, 19, 83, 22);
                body.AddBezier(83, 22, 87, 24, 93, 25, 96, 28);
                body.AddBezier(96, 28, 95, 32, 89, 31, 83, 30);
                body.AddBezier(83, 30, 75, 30, 76, 42, 66, 47);
                body.AddBezier(66, 47, 59, 52, 54, 48, 49, 47);
                body.AddBezier(49, 47, 41, 48, 32, 52, 24, 48);
                body.AddBezier(24, 48, 18, 46, 17, 43, 18, 38);
                Fill(g, paper, line, body);
            }
            using (GraphicsPath tail = new GraphicsPath())
            {
                tail.AddBezier(20, 40, 14, 38, 12, 35, 12, 32);
                tail.AddBezier(12, 32, 18, 32, 22, 35, 23, 38);
                Fill(g, paper, line, tail);
            }
            Leg(g, paper, line, 29, 45, 25, 59, 22, 73, 3.2f, true);
            Leg(g, paper, line, 62, 44, 61, 59, 61, 73, 3, true);
            using (GraphicsPath ear = new GraphicsPath())
            {
                ear.AddBezier(70, 23, 61, 23, 57, 19, 58, 16);
                ear.AddBezier(58, 16, 65, 16, 69, 18, 70, 23);
                Fill(g, paper, fine, ear);
            }
            Curve(g, line, 74, 21, 71, 14, 64, 11, 62, 4);
            Curve(g, line, 78, 21, 81, 13, 87, 10, 88, 3);
            Curve(g, fine, 66, 12, 61, 11, 57, 9, 55, 7);
            Curve(g, fine, 70, 16, 69, 11, 71, 8, 69, 5);
            Curve(g, fine, 84, 13, 88, 14, 93, 10, 95, 7);
            Curve(g, fine, 80, 17, 77, 12, 79, 8, 77, 5);
            Eye(g, ink, 82, 25, detail ? 1.4f : 1.8f);
            Curve(g, fine, 70, 30, 66, 35, 66, 40, 63, 44);
            if (!detail) return;
            Curve(g, fine, 26, 36, 33, 38, 35, 43, 31, 47);
            Curve(g, fine, 45, 38, 49, 41, 50, 44, 50, 46);
            Hatch(g, fine, 56, 33, 3, -1, -2, 5);
            Hatch(g, fine, 32, 48, 3, 0, 2, 3);
            Curve(g, fine, 86, 28, 89, 29, 92, 29, 94, 28);
        }

        private static void Goat(Graphics g, Brush paper, Brush ink, Pen line, Pen fine, bool detail)
        {
            Leg(g, paper, fine, 38, 46, 38, 59, 44, 71, 3.5f, true);
            Leg(g, paper, fine, 65, 46, 71, 56, 77, 68, 3.2f, true);
            using (GraphicsPath body = new GraphicsPath())
            {
                body.AddBezier(18, 36, 27, 28, 44, 33, 55, 31);
                body.AddBezier(55, 31, 63, 32, 64, 25, 71, 24);
                body.AddBezier(71, 24, 79, 21, 83, 27, 86, 31);
                body.AddBezier(86, 31, 89, 34, 96, 34, 98, 37);
                body.AddBezier(98, 37, 97, 42, 91, 41, 86, 42);
                body.AddBezier(86, 42, 79, 46, 76, 47, 70, 48);
                body.AddBezier(70, 48, 63, 56, 54, 52, 49, 51);
                body.AddBezier(49, 51, 38, 55, 22, 53, 19, 46);
                body.AddBezier(19, 46, 16, 42, 16, 39, 18, 36);
                Fill(g, paper, line, body);
            }
            using (GraphicsPath tail = new GraphicsPath())
            {
                tail.AddBezier(21, 36, 13, 37, 11, 28, 13, 24);
                tail.AddBezier(13, 24, 16, 28, 22, 28, 24, 33);
                Fill(g, paper, line, tail);
            }
            Leg(g, paper, line, 28, 47, 24, 59, 23, 72, 4, true);
            Leg(g, paper, line, 63, 46, 62, 59, 65, 72, 3.8f, true);
            Horn(g, paper, fine, 78, 26, 71, 2, 57, 13, 3.3f);
            Horn(g, paper, line, 72, 26, 65, 1, 48, 16, 4.2f);
            using (GraphicsPath ear = new GraphicsPath())
            {
                ear.AddBezier(74, 29, 66, 25, 62, 29, 66, 34);
                ear.AddBezier(66, 34, 71, 35, 74, 33, 74, 29);
                Fill(g, paper, fine, ear);
            }
            using (GraphicsPath beard = new GraphicsPath())
            {
                beard.AddBezier(83, 42, 89, 43, 88, 49, 84, 55);
                beard.AddBezier(84, 55, 85, 49, 79, 47, 80, 43);
                Fill(g, paper, fine, beard);
            }
            Eye(g, ink, 84, 33, detail ? 1.5f : 1.8f);
            Curve(g, fine, 63, 36, 57, 39, 57, 45, 61, 48);
            if (!detail) return;
            Hatch(g, fine, 26, 35, 4, 1, -1, 6);
            Hatch(g, fine, 31, 46, 4, 0, -1, 7);
            Hatch(g, fine, 60, 28, 3, 2, 1, 5);
            Curve(g, fine, 26, 38, 32, 40, 34, 43, 30, 48);
            Curve(g, fine, 88, 39, 92, 40, 94, 39, 96, 38);
        }

        private static void Dragon(Graphics g, Brush paper, Brush ink, Pen line, Pen fine, bool detail)
        {
            using (GraphicsPath tail = new GraphicsPath())
            {
                tail.AddBezier(38, 45, 25, 51, 22, 70, 10, 66);
                tail.AddBezier(10, 66, 2, 63, 5, 55, 8, 51);
                tail.AddBezier(8, 51, 7, 59, 10, 63, 14, 59);
                tail.AddBezier(14, 59, 24, 51, 19, 44, 31, 40);
                Fill(g, paper, line, tail);
            }
            Leg(g, paper, fine, 43, 47, 48, 57, 56, 70, 3.8f, false);
            Leg(g, paper, fine, 69, 43, 80, 53, 86, 64, 3.5f, false);
            using (GraphicsPath body = new GraphicsPath())
            {
                body.AddBezier(28, 43, 39, 37, 55, 43, 65, 34);
                body.AddBezier(65, 34, 71, 29, 69, 22, 77, 20);
                body.AddBezier(77, 20, 83, 18, 87, 23, 91, 25);
                body.AddBezier(91, 25, 96, 27, 102, 27, 103, 31);
                body.AddBezier(103, 31, 102, 37, 93, 35, 86, 34);
                body.AddBezier(86, 34, 79, 34, 82, 44, 72, 49);
                body.AddBezier(72, 49, 62, 56, 43, 52, 31, 53);
                body.AddBezier(31, 53, 24, 52, 23, 47, 28, 43);
                Fill(g, paper, line, body);
            }
            Leg(g, paper, line, 36, 47, 32, 60, 25, 72, 4.8f, false);
            Leg(g, paper, line, 65, 48, 65, 60, 74, 72, 4.4f, false);
            using (GraphicsPath wing = new GraphicsPath())
            {
                wing.AddBezier(53, 43, 46, 32, 46, 15, 31, 6);
                wing.AddBezier(31, 6, 46, 9, 63, 13, 73, 23);
                wing.AddBezier(73, 23, 62, 20, 58, 24, 61, 29);
                wing.AddBezier(61, 29, 49, 24, 48, 31, 53, 43);
                Fill(g, paper, line, wing);
                Curve(g, fine, 33, 8, 48, 15, 52, 19, 61, 29);
                Curve(g, fine, 33, 8, 45, 23, 44, 29, 53, 43);
            }
            Horn(g, paper, fine, 78, 22, 71, 14, 70, 11, 2.7f);
            Horn(g, paper, line, 85, 24, 92, 17, 95, 15, 2.8f);
            Eye(g, ink, 88, 27, detail ? 1.4f : 1.8f);
            Curve(g, fine, 91, 32, 95, 33, 98, 32, 100, 31);
            Curve(g, fine, 75, 36, 72, 44, 64, 47, 58, 48);
            if (!detail) return;
            Hatch(g, fine, 71, 37, -2, 3, 4, 2);
            Hatch(g, fine, 42, 47, 4, 0, 1, 3);
            Curve(g, fine, 28, 57, 27, 60, 25, 62, 23, 63);
            g.DrawLine(fine, 26, 72, 24, 75); g.DrawLine(fine, 30, 72, 29, 75);
            g.DrawLine(fine, 76, 72, 77, 75); g.DrawLine(fine, 79, 72, 81, 74);
        }

        private static void Fill(Graphics g, Brush paper, Pen pen, GraphicsPath path)
        {
            path.CloseFigure(); g.FillPath(paper, path); g.DrawPath(pen, path);
        }

        private static void Curve(Graphics g, Pen pen, float x, float y, float a, float b, float c, float d, float endX, float endY)
        { g.DrawBezier(pen, x, y, a, b, c, d, endX, endY); }

        private static void Eye(Graphics g, Brush ink, float x, float y, float radius)
        { g.FillEllipse(ink, x - radius, y - radius, radius * 2, radius * 2); }

        private static void Leg(Graphics g, Brush paper, Pen pen, float hipX, float hipY, float kneeX, float kneeY, float footX, float footY, float width, bool hoof)
        {
            using (GraphicsPath limb = new GraphicsPath())
            {
                limb.AddBezier(hipX - width / 2, hipY, hipX - width, hipY + 5, kneeX - width / 2, kneeY - 3, kneeX - width / 2, kneeY);
                limb.AddBezier(kneeX - width / 2, kneeY, kneeX - width / 2, kneeY + 5, footX - width / 2, footY - 4, footX - width / 2, footY);
                limb.AddBezier(footX - width / 2, footY, footX, footY + 1, footX + width + 2, footY + 1, footX + width + 2, footY - 1);
                limb.AddBezier(footX + width + 2, footY - 1, footX + width, footY - 3, footX + width / 2, footY - 3, footX + width / 2, footY - 4);
                limb.AddBezier(footX + width / 2, footY - 4, kneeX + width / 2, kneeY + 4, kneeX + width / 2, kneeY + 1, kneeX + width / 2, kneeY);
                limb.AddBezier(kneeX + width / 2, kneeY, hipX + width, hipY + 6, hipX + width / 2, hipY + 2, hipX + width / 2, hipY);
                Fill(g, paper, pen, limb);
            }
            if (hoof && pen.Width < 2.5f) g.DrawLine(pen, footX + width / 2, footY - 2, footX + width / 2, footY + .5f);
        }

        private static void Horn(Graphics g, Brush paper, Pen pen, float x, float y, float bendX, float bendY, float tipX, float tipY, float width)
        {
            using (GraphicsPath horn = new GraphicsPath())
            {
                horn.AddBezier(x - width / 2, y, bendX, bendY + width, bendX, bendY, tipX, tipY);
                horn.AddBezier(tipX, tipY, bendX + width, bendY, bendX + width, bendY + width, x + width / 2, y);
                Fill(g, paper, pen, horn);
            }
        }

        private static void Hatch(Graphics g, Pen pen, float x, float y, float stepX, float stepY, float dx, float dy)
        {
            for (int i = 0; i < 3; i++)
                g.DrawLine(pen, x + stepX * i, y + stepY * i, x + stepX * i + dx, y + stepY * i + dy - i * .7f);
        }
    }
}
