using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace Clio.Desktop
{
    internal static class Art
    {
        public static bool PaperMode { get; set; }
        public static Color Ink { get { return PaperMode ? MapPaper.Ink : Color.FromArgb(234, 231, 215); } }
        public static Color Muted { get { return PaperMode ? MapPaper.MutedInk : Color.FromArgb(151, 163, 158); } }
        public static Color Gold { get { return PaperMode ? MapPaper.Russet : Color.FromArgb(216, 180, 112); } }
        public static Color Background { get { return PaperMode ? MapPaper.Ground : Color.FromArgb(13, 22, 26); } }
        public static Color PanelColor { get { return PaperMode ? MapPaper.Paper : Color.FromArgb(26, 37, 40); } }
        public static Color Border { get { return PaperMode ? MapPaper.Rule : Color.FromArgb(62, 76, 73); } }
        private static readonly Bitmap grain = MakeGrain();
        private static Font GetFont(float size, bool serif, bool bold)
        {
            return Typography.Font(serif ? TypeRole.Heading : TypeRole.Body, serif ? size * 1.12f : size, bold);
        }
        public static void Text(Graphics g, string text, float x, float y, float size, Color color, float width, bool serif, bool bold)
        {
            using (Brush brush = new SolidBrush(color))
            using (StringFormat format = new StringFormat { Trimming = StringTrimming.EllipsisCharacter })
                g.DrawString(text, GetFont(size, serif, bold), brush, new RectangleF(x, y, width, size * 2.8f), format);
        }
        public static void Text(Graphics g, string text, float x, float y, float size, Color color, float width)
        { Text(g, text, x, y, size, color, width, false, false); }
        public static void CenterText(Graphics g, string text, RectangleF bounds, float size, Color color, bool serif)
        {
            using (Brush brush = new SolidBrush(color))
            using (StringFormat format = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center, Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap })
                g.DrawString(text, GetFont(size, serif, false), brush, bounds, format);
        }
        public static Color Mix(Color a, Color b, double t)
        { t = Math.Max(0, Math.Min(1, t)); return Color.FromArgb((int)(a.R + (b.R - a.R) * t), (int)(a.G + (b.G - a.G) * t), (int)(a.B + (b.B - a.B) * t)); }
        public static void Fill(Graphics g, Color color, float x, float y, float w, float h)
        { using (Brush brush = new SolidBrush(color)) g.FillRectangle(brush, x, y, w, h); }
        public static void Line(Graphics g, Color color, float width, float x1, float y1, float x2, float y2)
        { using (Pen pen = new Pen(color, width)) g.DrawLine(pen, x1, y1, x2, y2); }
        private static Bitmap MakeGrain()
        {
            Bitmap bitmap = new Bitmap(96, 96); Random random = new Random(1729);
            for (int y = 0; y < 96; y++) for (int x = 0; x < 96; x++)
            { int light = random.Next(2) == 0 ? 255 : 0; bitmap.SetPixel(x, y, Color.FromArgb(random.Next(1, 4), light, light, light)); }
            return bitmap;
        }
        public static void Grain(Graphics g, RectangleF bounds)
        { using (TextureBrush texture = new TextureBrush(grain, WrapMode.Tile)) g.FillRectangle(texture, bounds); }
        public static void Panel(Graphics g, RectangleF bounds, Color color, bool ornament)
        {
            if (PaperMode) { MapPaper.Surface(g, bounds, ornament); return; }
            using (LinearGradientBrush brush = new LinearGradientBrush(bounds, Mix(color, Color.FromArgb(74, 82, 76), 0.10), Mix(color, Color.Black, 0.12), 90)) g.FillRectangle(brush, bounds);
            Grain(g, bounds);
            using (Pen pen = new Pen(Color.FromArgb(130, 67, 81, 75), 1)) g.DrawRectangle(pen, bounds.X + .5f, bounds.Y + .5f, bounds.Width - 1, bounds.Height - 1);
            if (ornament)
            {
                Color edge = Color.FromArgb(132, Gold); float x = bounds.Left, y = bounds.Top, w = bounds.Width, h = bounds.Height;
                Line(g, edge, 1, x + 8, y, x + 27, y); Line(g, edge, 1, x, y + 8, x, y + 27);
                Line(g, edge, 1, x + w - 27, y, x + w - 8, y); Line(g, edge, 1, x + w, y + 8, x + w, y + 27);
                Line(g, edge, 1, x + 8, y + h, x + 27, y + h); Line(g, edge, 1, x, y + h - 27, x, y + h - 8);
                Line(g, edge, 1, x + w - 27, y + h, x + w - 8, y + h); Line(g, edge, 1, x + w, y + h - 27, x + w, y + h - 8);
            }
        }
        public static void Rule(Graphics g, float x, float y, float width)
        {
            Line(g, Color.FromArgb(100, Gold), 0.8f, x, y, x + width, y);
            using (Brush brush = new SolidBrush(Gold)) g.FillPolygon(brush, new[] { new PointF(x + width / 2, y - 2), new PointF(x + width / 2 + 3, y), new PointF(x + width / 2, y + 2), new PointF(x + width / 2 - 3, y) });
        }
        // Icons use a 24-unit drawing square with their top-left at x,y.
        public static void Icon(Graphics g, string key, float x, float y, float size, Color color)
        {
            GraphicsState state = g.Save(); g.TranslateTransform(x, y); g.ScaleTransform(size / 24, size / 24);
            using (Pen pen = new Pen(color, 1.35f) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round })
            using (Brush brush = new SolidBrush(color))
            {
                if (key == "leaf")
                { g.DrawBezier(pen, 4, 21, 7, 9, 9, 5, 21, 3); g.DrawBezier(pen, 4, 21, 1, 9, 10, 1, 21, 3); g.DrawBezier(pen, 4, 21, 17, 23, 22, 12, 21, 3); g.DrawLine(pen, 8, 14, 8, 8); g.DrawLine(pen, 12, 10, 17, 11); }
                else if (key == "people")
                { g.DrawEllipse(pen, 9, 2, 6, 6); g.DrawArc(pen, 6, 10, 12, 16, 180, 180); g.DrawLine(pen, 6, 18, 18, 18); g.DrawEllipse(pen, 2, 5, 4, 4); g.DrawArc(pen, 0, 11, 8, 12, 180, 120); g.DrawEllipse(pen, 18, 5, 4, 4); g.DrawArc(pen, 16, 11, 8, 12, 240, 120); }
                else if (key == "cooperate")
                { g.DrawEllipse(pen, 1, 7, 14, 10); g.DrawEllipse(pen, 9, 7, 14, 10); }
                else if (key == "conflict")
                { g.DrawLines(pen, new[] { new PointF(3, 21), new PointF(20, 4), new PointF(22, 2), new PointF(20, 8) }); g.DrawLines(pen, new[] { new PointF(21, 21), new PointF(4, 4), new PointF(2, 2), new PointF(4, 8) }); g.DrawLine(pen, 2, 15, 9, 22); g.DrawLine(pen, 15, 22, 22, 15); }
                else if (key == "adviser")
                { g.DrawRectangle(pen, 2, 3, 20, 14); g.DrawLines(pen, new[] { new PointF(5, 17), new PointF(5, 22), new PointF(11, 17) }); g.DrawLine(pen, 6, 8, 18, 8); g.DrawLine(pen, 6, 12, 14, 12); }
                else if (key == "settings")
                { for (int i = 0; i < 3; i++) { float row = 5 + i * 7; g.DrawLine(pen, 2, row, 22, row); g.FillRectangle(brush, i == 1 ? 14 : 6, row - 3, 4, 6); } }
                else if (key == "locate")
                { g.DrawEllipse(pen, 5, 5, 14, 14); g.DrawEllipse(pen, 10, 10, 4, 4); g.DrawLine(pen, 12, 1, 12, 5); g.DrawLine(pen, 12, 19, 12, 23); g.DrawLine(pen, 1, 12, 5, 12); g.DrawLine(pen, 19, 12, 23, 12); }
                else if (key == "move")
                { g.DrawEllipse(pen, 3, 16, 4, 4); g.DrawBezier(pen, 7, 18, 24, 18, 4, 6, 20, 6); g.DrawLines(pen, new[] { new PointF(16, 3), new PointF(21, 6), new PointF(17, 10) }); }
                else if (key == "orders")
                { for (int i = 0; i < 3; i++) { float row = 5 + i * 7; g.DrawLines(pen, new[] { new PointF(3, row), new PointF(5, row + 2), new PointF(8, row - 2) }); g.DrawLine(pen, 12, row, 21, row); } }
                else if (key == "hunt")
                { g.DrawArc(pen, 1, 2, 16, 20, -85, 170); g.DrawLine(pen, 10, 2, 10, 22); g.DrawLine(pen, 4, 12, 23, 12); g.DrawLines(pen, new[] { new PointF(19, 8), new PointF(23, 12), new PointF(19, 16) }); }
                else if (key == "heart")
                { using (GraphicsPath p = new GraphicsPath()) { p.AddBezier(12, 21, -5, 10, 2, -1, 12, 7); p.AddBezier(12, 7, 22, -1, 29, 10, 12, 21); g.DrawPath(pen, p); } }
                else if (key == "camp")
                { g.DrawLines(pen, new[] { new PointF(2, 21), new PointF(12, 3), new PointF(22, 21), new PointF(2, 21) }); g.DrawLines(pen, new[] { new PointF(8, 21), new PointF(12, 12), new PointF(16, 21) }); g.DrawLine(pen, 10, 1, 14, 8); }
                else if (key == "branch")
                { g.DrawLine(pen, 12, 20, 12, 9); g.DrawBezier(pen, 12, 13, 8, 11, 5, 8, 5, 4); g.DrawBezier(pen, 12, 14, 17, 11, 19, 9, 19, 4); g.DrawEllipse(pen, 2, 1, 5, 5); g.DrawEllipse(pen, 16, 1, 5, 5); g.DrawEllipse(pen, 9, 18, 6, 5); }
                else if (key == "sun")
                { g.DrawEllipse(pen, 7, 7, 10, 10); for (int i = 0; i < 8; i++) { double angle = i * Math.PI / 4; g.DrawLine(pen, 12 + (float)Math.Cos(angle) * 8, 12 + (float)Math.Sin(angle) * 8, 12 + (float)Math.Cos(angle) * 11, 12 + (float)Math.Sin(angle) * 11); } }
                else if (key == "moon")
                { using (GraphicsPath p = new GraphicsPath()) { p.AddBezier(16, 2, -2, 3, -1, 23, 17, 22); p.AddBezier(17, 22, 5, 17, 8, 8, 16, 2); g.DrawPath(pen, p); } }
                else if (key == "snow")
                { for (int i = 0; i < 3; i++) { GraphicsState s = g.Save(); g.TranslateTransform(12, 12); g.RotateTransform(i * 60); g.DrawLine(pen, -10, 0, 10, 0); g.DrawLines(pen, new[] { new PointF(-7, -3), new PointF(-4, 0), new PointF(-7, 3) }); g.DrawLines(pen, new[] { new PointF(7, -3), new PointF(4, 0), new PointF(7, 3) }); g.Restore(s); } }
                else if (key == "quill")
                { g.DrawBezier(pen, 3, 23, 7, 11, 13, 3, 21, 1); g.DrawBezier(pen, 6, 16, 5, 5, 13, 2, 21, 1); g.DrawBezier(pen, 6, 16, 18, 17, 20, 8, 21, 1); g.DrawLine(pen, 10, 10, 16, 10); }
                else if (key == "globe")
                { g.DrawEllipse(pen, 2, 2, 20, 20); g.DrawEllipse(pen, 7, 2, 10, 20); g.DrawArc(pen, 2, 6, 20, 9, 0, 180); g.DrawLine(pen, 2, 12, 22, 12); }
                else if (key == "book" || key == "language" || key == "history")
                { g.DrawLines(pen, new[] { new PointF(12, 5), new PointF(3, 3), new PointF(3, 20), new PointF(12, 22), new PointF(21, 20), new PointF(21, 3), new PointF(12, 5), new PointF(12, 22) }); for (int i = 0; i < 3; i++) { g.DrawLine(pen, 5, 8 + i * 4, 9, 9 + i * 4); g.DrawLine(pen, 15, 9 + i * 4, 19, 8 + i * 4); } }
                else { g.FillEllipse(brush, 9, 9, 6, 6); }
            }
            g.Restore(state);
        }
    }
}
