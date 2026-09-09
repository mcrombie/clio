using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;

namespace Clio.Desktop
{
    internal enum TypeRole { Display, Heading, Body, Action, Label, Number, Annotation, Utility }

    // Book typography, with separate faces for reading, navigation and marginalia.
    // Fonts are installed families; the Windows fallbacks keep stories portable.
    internal static class Typography
    {
        private static readonly HashSet<string> installed = InstalledFamilies();
        private static readonly string display = Choose("EB Garamond", "Palatino Linotype", "Georgia");
        private static readonly string heading = Choose("EB Garamond SemiBold", "EB Garamond Medium", "Palatino Linotype", "Georgia");
        private static readonly string body = Choose("Constantia", "Cambria", "Georgia");
        private static readonly string action = Choose("Libre Baskerville", "Constantia", "Georgia");
        private static readonly string utility = Choose("Segoe UI", "Tahoma", "Arial");
        private static readonly Dictionary<string, Font> fonts = new Dictionary<string, Font>();

        private static HashSet<string> InstalledFamilies()
        {
            HashSet<string> names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (FontFamily family in FontFamily.Families)
            { names.Add(family.Name); family.Dispose(); }
            return names;
        }
        private static string Choose(params string[] candidates)
        {
            foreach (string candidate in candidates) if (installed.Contains(candidate)) return candidate;
            return FontFamily.GenericSerif.Name;
        }
        public static Font Font(TypeRole role, float size, bool bold = false)
        {
            string family = role == TypeRole.Body ? body : role == TypeRole.Action ? action :
                role == TypeRole.Utility ? utility : role == TypeRole.Heading || role == TypeRole.Label ? heading : display;
            bool fallbackHeading = (role == TypeRole.Heading || role == TypeRole.Label) && !heading.StartsWith("EB Garamond", StringComparison.Ordinal);
            FontStyle style = role == TypeRole.Annotation ? FontStyle.Italic : bold || fallbackHeading ? FontStyle.Bold : FontStyle.Regular;
            string key = family + ":" + size.ToString(CultureInfo.InvariantCulture) + ":" + style;
            Font font;
            if (!fonts.TryGetValue(key, out font))
            {
                using (FontFamily face = new FontFamily(family))
                {
                    if (!face.IsStyleAvailable(style)) style = face.IsStyleAvailable(FontStyle.Regular) ? FontStyle.Regular : FontStyle.Bold;
                    font = new Font(face, size, style, GraphicsUnit.Pixel);
                }
                fonts.Add(key, font);
            }
            return font;
        }
        public static string Description
        { get { return "Display: " + display + "; headings: " + heading + "; reading: " + body + "; actions: " + action + "; shortcuts: " + utility; } }

        public static void Draw(Graphics g, string text, RectangleF bounds, float size, Color color, TypeRole role)
        {
            if (String.IsNullOrEmpty(text) || bounds.Width <= 0 || bounds.Height <= 0) return;
            using (Brush brush = new SolidBrush(color))
            using (StringFormat format = new StringFormat { Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.LineLimit })
                g.DrawString(text, Font(role, size), brush, bounds, format);
        }
        public static void Line(Graphics g, string text, RectangleF bounds, float size, Color color, TypeRole role,
            bool fit = false, StringAlignment alignment = StringAlignment.Near)
        {
            if (String.IsNullOrEmpty(text) || bounds.Width <= 0 || bounds.Height <= 0) return;
            using (StringFormat format = new StringFormat { Alignment = alignment, LineAlignment = StringAlignment.Center,
                Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap })
            using (Brush brush = new SolidBrush(color))
            {
                if (fit)
                {
                    float minimum = Math.Min(size, 10);
                    while (size > minimum && g.MeasureString(text, Font(role, size), new SizeF(10000, 10000), format).Width > bounds.Width)
                        size = Math.Max(minimum, size - 0.5f);
                }
                g.DrawString(text, Font(role, size), brush, bounds, format);
            }
        }

        public static void Label(Graphics g, string text, RectangleF bounds, float size, Color color,
            float tracking = 0.7f, StringAlignment alignment = StringAlignment.Near)
        {
            if (String.IsNullOrEmpty(text) || bounds.Width <= 0 || bounds.Height <= 0) return;
            text = text.ToUpperInvariant();
            Font font = Font(TypeRole.Label, size);
            using (StringFormat format = (StringFormat)StringFormat.GenericTypographic.Clone())
            using (Brush brush = new SolidBrush(color))
            {
                format.FormatFlags |= StringFormatFlags.MeasureTrailingSpaces | StringFormatFlags.NoWrap;
                List<string> letters = new List<string>(); List<float> advances = new List<float>();
                TextElementEnumerator elements = StringInfo.GetTextElementEnumerator(text);
                float total = 0;
                while (elements.MoveNext())
                {
                    string element = elements.GetTextElement();
                    float width = g.MeasureString(element, font, new PointF(0, 0), format).Width;
                    letters.Add(element); advances.Add(width); total += width;
                }
                if (letters.Count > 1) tracking = Math.Max(0, Math.Min(tracking, (bounds.Width - total) / (letters.Count - 1)));
                total += tracking * Math.Max(0, letters.Count - 1);
                if (total > bounds.Width)
                {
                    float ellipsis = g.MeasureString("…", font, new PointF(0, 0), format).Width;
                    while (letters.Count > 0 && total + ellipsis > bounds.Width)
                    { int last = letters.Count - 1; total -= advances[last] + (last > 0 ? tracking : 0); letters.RemoveAt(last); advances.RemoveAt(last); }
                    letters.Add("…"); advances.Add(ellipsis); total += ellipsis;
                }
                float x = bounds.Left + (alignment == StringAlignment.Center ? (bounds.Width - total) / 2 : alignment == StringAlignment.Far ? bounds.Width - total : 0);
                float y = bounds.Top + (bounds.Height - font.GetHeight(g)) / 2;
                System.Drawing.Drawing2D.GraphicsState state = g.Save();
                g.SetClip(bounds, System.Drawing.Drawing2D.CombineMode.Intersect);
                for (int i = 0; i < letters.Count; i++)
                { g.DrawString(letters[i], font, brush, new PointF(x, y), format); x += advances[i] + tracking; }
                g.Restore(state);
            }
        }
    }
}
