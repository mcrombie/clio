using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using Clio.Simulation;

namespace Clio.Desktop
{
    // Small marginal illustrations. Every mark uses local drawing coordinates;
    // no world sampling, resource creation or simulation randomness belongs here.
    internal static class MapSymbols
    {
        internal static void PaperWash(Graphics g, RectangleF bounds, bool highlighted)
        {
            using (GraphicsPath shape = new GraphicsPath())
            {
                shape.AddClosedCurve(new[] {
                    new PointF(bounds.Left, bounds.Top + bounds.Height * .42f),
                    new PointF(bounds.Left + bounds.Width * .22f, bounds.Top + bounds.Height * .08f),
                    new PointF(bounds.Left + bounds.Width * .65f, bounds.Top),
                    new PointF(bounds.Right, bounds.Top + bounds.Height * .34f),
                    new PointF(bounds.Right - bounds.Width * .06f, bounds.Bottom - bounds.Height * .2f),
                    new PointF(bounds.Left + bounds.Width * .52f, bounds.Bottom),
                    new PointF(bounds.Left + bounds.Width * .1f, bounds.Bottom - bounds.Height * .08f)
                }, .5f);
                using (PathGradientBrush wash = new PathGradientBrush(shape))
                {
                    wash.CenterColor = Color.FromArgb(highlighted ? 250 : 226, MapPaper.Paper);
                    wash.SurroundColors = new[] { Color.FromArgb(0, MapPaper.Paper) };
                    wash.FocusScales = new PointF(.52f, .52f);
                    g.FillPath(wash, shape);
                }
            }
        }

        private static GraphicsState Begin(Graphics g, RectangleF box)
        {
            GraphicsState state = g.Save();
            float scale = Math.Min(box.Width, box.Height) / 32;
            g.TranslateTransform(box.X + (box.Width - scale * 32) / 2, box.Y + (box.Height - scale * 32) / 2);
            g.ScaleTransform(scale, scale);
            return state;
        }

        private static Pen Ink(Color color, float width)
        { return new Pen(color, width) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round }; }

        internal static void Forage(Graphics g, RectangleF box, Color color)
        {
            if (box.Width <= 0 || box.Height <= 0) return;
            GraphicsState state = Begin(g, box);
            try
            {
                using (Pen outline = Ink(color, 1.15f))
                using (Pen fine = Ink(Color.FromArgb(180, color), .7f))
                {
                    // A gathered sprig: open leaf contours, berries and a cut stem.
                    // It suggests foraging without implying cultivated cereal fields.
                    g.DrawBezier(outline, 9, 29, 17, 20, 16, 10, 23, 3);
                    using (GraphicsPath leaf = new GraphicsPath())
                    {
                        leaf.AddBezier(15, 20, 7, 22, 3, 16, 4, 9);
                        leaf.AddBezier(4, 9, 12, 10, 17, 13, 15, 20);
                        using (Brush wash = new SolidBrush(Color.FromArgb(26, color))) g.FillPath(wash, leaf);
                        g.DrawPath(outline, leaf);
                    }
                    using (GraphicsPath leaf = new GraphicsPath())
                    {
                        leaf.AddBezier(18, 14, 18, 8, 24, 8, 29, 9);
                        leaf.AddBezier(29, 9, 27, 16, 23, 18, 18, 14);
                        using (Brush wash = new SolidBrush(Color.FromArgb(19, color))) g.FillPath(wash, leaf);
                        g.DrawPath(outline, leaf);
                    }
                    g.DrawBezier(fine, 6, 12, 9, 16, 12, 17, 15, 20);
                    g.DrawBezier(fine, 18, 14, 21, 13, 24, 12, 27, 11);
                    g.DrawLine(fine, 9, 16, 8, 12); g.DrawLine(fine, 11, 18, 7, 17);
                    g.DrawLine(fine, 23, 12, 23, 10);
                    g.DrawLine(outline, 14, 23, 21, 21);
                    g.DrawEllipse(outline, 21, 18, 5.5f, 5.5f);
                    g.DrawEllipse(outline, 18, 23, 5, 5);
                    g.DrawArc(fine, 22, 19, 3.5f, 3.5f, 210, 120);
                    g.DrawLine(fine, 11, 29, 13, 27);
                }
            }
            finally { g.Restore(state); }
        }

        internal static void Salt(Graphics g, RectangleF box, SaltSource source, Color color)
        {
            if (box.Width <= 0 || box.Height <= 0 || source == SaltSource.None) return;
            GraphicsState state = Begin(g, box);
            try
            {
                using (Pen water = Ink(Color.FromArgb(195, MapPaper.Blue), .85f))
                {
                    if (source == SaltSource.Coastal)
                    {
                        g.DrawBezier(water, 2, 24, 8, 20, 10, 27, 17, 24);
                        g.DrawBezier(water, 17, 24, 23, 21, 26, 27, 30, 23);
                        g.DrawBezier(water, 6, 28, 12, 25, 16, 30, 22, 27);
                    }
                    else
                    {
                        g.DrawArc(water, 2, 20, 28, 8, 4, 172);
                        g.DrawArc(water, 6, 23, 18, 7, 10, 142);
                        g.DrawBezier(water, 3, 20, 6, 17, 10, 18, 12, 19);
                    }
                }
                // Salt is shown as squat cubic crystals, with fine face hatching.
                Crystal(g, 6, 12, 8, color);
                Crystal(g, 15, 5, 10, color);
                Crystal(g, 23, 15, 6, color);
                using (Pen grains = Ink(Color.FromArgb(155, color), .85f))
                { g.DrawLine(grains, 11, 23, 13, 22); g.DrawLine(grains, 20, 25, 22, 24); }
            }
            finally { g.Restore(state); }
        }

        private static void Crystal(Graphics g, float x, float y, float size, Color color)
        {
            PointF top = new PointF(x, y), right = new PointF(x + size * .6f, y + size * .28f);
            PointF middle = new PointF(x + size * .07f, y + size * .57f), left = new PointF(x - size * .48f, y + size * .29f);
            PointF lowLeft = new PointF(left.X + .4f, left.Y + size * .63f), bottom = new PointF(middle.X, middle.Y + size * .66f);
            PointF lowRight = new PointF(right.X - .2f, right.Y + size * .65f);
            PointF[] edge = { top, right, lowRight, bottom, lowLeft, left };
            using (Brush paper = new SolidBrush(MapPaper.Paper)) g.FillPolygon(paper, edge);
            using (Pen outline = Ink(color, 1.0f))
            using (Pen hatch = Ink(Color.FromArgb(125, color), .6f))
            {
                g.DrawPolygon(outline, edge);
                g.DrawLines(outline, new[] { left, middle, right }); g.DrawLine(outline, middle, bottom);
                for (int i = 1; i <= 3; i++)
                {
                    float t = i / 4f;
                    PointF a = new PointF(middle.X + (right.X - middle.X) * t, middle.Y + (right.Y - middle.Y) * t + 1.4f);
                    g.DrawLine(hatch, a.X, a.Y, a.X - .4f, a.Y + size * .37f);
                }
            }
        }

        internal static void Explore(Graphics g, RectangleF box, Color color)
        {
            if (box.Width <= 0 || box.Height <= 0) return;
            GraphicsState state = Begin(g, box);
            try
            {
                using (Pen outline = Ink(color, 1.05f))
                using (Pen fine = Ink(Color.FromArgb(155, color), .65f))
                {
                    // An engraved compass rose, drawn directly into the chart.
                    g.DrawArc(fine, 6, 7, 20, 19, 13, 138);
                    g.DrawArc(fine, 6.3f, 6.8f, 19.8f, 19.4f, 173, 155);
                    PointF[] rose = { new PointF(16, 1), new PointF(19, 12), new PointF(29, 16), new PointF(19, 19),
                        new PointF(16, 30), new PointF(13, 19), new PointF(2, 16), new PointF(13, 13) };
                    using (Brush paper = new SolidBrush(MapPaper.Paper)) g.FillPolygon(paper, rose);
                    g.DrawPolygon(outline, rose);
                    g.DrawLine(fine, 16, 2, 16, 29); g.DrawLine(fine, 3, 16, 28, 16);
                    for (int i = 0; i < 4; i++)
                        g.DrawLine(fine, 16.2f, 5 + i * 2, 16.7f + i * .5f, 7 + i * 2);
                    g.DrawEllipse(outline, 14, 14, 4, 4);
                    g.DrawLine(fine, 8, 8, 5, 5); g.DrawLine(fine, 24, 24, 27, 27);
                }
            }
            finally { g.Restore(state); }
        }
    }
}
