using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using Clio.Simulation;

namespace Clio.Desktop
{
    // Small atlas counters: open ring = wild, ochre double edge = household,
    // pointed red corners = hostile. Shape remains useful without color.
    internal static class UnitArt
    {
        internal static readonly Color MapPaper = Color.FromArgb(243, 233, 209);
        internal static readonly Color MapInk = Color.FromArgb(65, 48, 35);
        internal static readonly Color MapAccent = Color.FromArgb(139, 89, 44);
        internal static readonly Color MapDanger = Color.FromArgb(158, 67, 50);
        internal static readonly Color MapRoute = Color.FromArgb(67, 96, 112);
        internal static readonly Color MapHealthy = Color.FromArgb(79, 102, 66);
        internal static Color MapIdentity(int identity) { return Art.PaperMode ? IdentityArt.ColorFor(identity) : Art.Mix(IdentityArt.ColorFor(identity), MapInk, .43); }
        // Original miniature figures share the same standard and identity as the
        // atlas emblem. Coordinates fit the existing regional band pick box.
        internal static void DrawBandParty(Graphics g, Band band, PointF anchor, float scale, bool selected, bool commanded, bool moving)
        { DrawBandParty(g, band, anchor, scale, selected, commanded, moving, band.Id); }

        internal static void DrawBandParty(Graphics g, Band band, PointF anchor, float scale, bool selected, bool commanded, bool moving, int identityId)
        {
            if (scale <= 0 || band.Population <= 0) return;
            GraphicsState state = g.Save();
            try
            {
                g.TranslateTransform(anchor.X, anchor.Y); g.ScaleTransform(scale, scale);
                Color identity = MapIdentity(identityId), dark = MapInk;
                using (Brush paper = new SolidBrush(Color.FromArgb(190, MapPaper))) g.FillEllipse(paper, -25, -5, 59, 13);
                using (Pen ground = new Pen(Color.FromArgb(125, MapInk), .75f))
                { g.DrawArc(ground, -25, -5, 59, 13, 0, 165); g.DrawLine(ground, -22, 7, -8, 8); g.DrawLine(ground, 18, 8, 31, 5); }
                if (selected || commanded)
                {
                    Color ringColor = commanded ? MapAccent : identity;
                    using (Pen ring = new Pen(Color.FromArgb(210, ringColor), 1.4f)) g.DrawEllipse(ring, -25, -7, 59, 15);
                    using (Pen front = new Pen(Color.FromArgb(220, ringColor), .8f)) g.DrawArc(front, -28, -9, 65, 19, 16, 149);
                }
                if (band.Settled)
                {
                    PointF[] tent = PartyPoints(-25, -5, -15, -28, 1, -7, -25, -5);
                    using (Brush canvas = new SolidBrush(MapPaper)) g.FillPolygon(canvas, tent);
                    using (Pen seam = new Pen(MapInk, 1.1f))
                    { g.DrawPolygon(seam, tent); g.DrawLine(seam, -15, -27, -10, -6); g.DrawLine(seam, -16, -18, -20, -6); }
                    using (Pen hatch = new Pen(Color.FromArgb(140, MapInk), .65f))
                        for (int i = 0; i < 4; i++) g.DrawLine(hatch, -13 + i * 2, -21 + i * 3, -10 + i * 2, -9);
                }
                // A scout behind the standard and two companions in front give
                // the party depth without making the symbol a population count.
                if (band.Population >= 3)
                    DrawTraveler(g, -15, -3, .83f, band.Ancestry, identity, 0, moving);
                DrawPartyStandard(g, identityId, identity, commanded);
                DrawTraveler(g, -5, 1, 1.02f, band.Ancestry, identity, 1, moving);
                if (band.Population >= 2)
                    DrawTraveler(g, 23, 0, .88f, band.Ancestry, identity, 2, moving);
                if (commanded)
                {
                    using (Brush fill = new SolidBrush(MapPaper)) g.FillPolygon(fill, PartyPoints(-17, -47, -12.7f, -42.7f, -17, -38.4f, -21.3f, -42.7f));
                    using (Pen border = new Pen(MapAccent, 1.2f)) g.DrawPolygon(border, PartyPoints(-17, -47, -12.7f, -42.7f, -17, -38.4f, -21.3f, -42.7f));
                    using (Brush center = new SolidBrush(dark)) g.FillEllipse(center, -18.3f, -44, 2.6f, 2.6f);
                }
            }
            finally { g.Restore(state); }
        }

        private static void DrawPartyStandard(Graphics g, int id, Color identity, bool commanded)
        {
            using (Pen wood = new Pen(MapInk, 1.3f)) g.DrawLine(wood, 0, -49, 0, 0);
            using (Pen edge = new Pen(Color.FromArgb(100, MapInk), .55f)) g.DrawLine(edge, 1.5f, -46, 1.1f, -1);
            using (GraphicsPath cloth = new GraphicsPath())
            {
                cloth.AddBezier(1, -46, 10, -50, 24, -41, 34, -46);
                cloth.AddLine(34, -46, 31, -34); cloth.AddLine(31, -34, 34, -24);
                cloth.AddBezier(34, -24, 23, -18, 12, -27, 1, -24); cloth.CloseFigure();
                using (Brush fabric = new SolidBrush(Art.Mix(MapPaper, identity, .10))) g.FillPath(fabric, cloth);
                using (Pen rim = new Pen(identity, commanded ? 1.5f : 1f)) g.DrawPath(rim, cloth);
                using (Pen fold = new Pen(Color.FromArgb(140, identity), .65f)) g.DrawBezier(fold, 4, -44, 12, -46, 24, -38, 30, -43);
                using (Pen hem = new Pen(Color.FromArgb(140, identity), .65f)) g.DrawBezier(hem, 4, -27, 15, -29, 23, -23, 30, -26);
            }
            IdentityArt.DrawEmblem(g, id, new RectangleF(8, -44, 18, 18), false);
            using (Brush tip = new SolidBrush(commanded ? MapInk : identity))
                g.FillPolygon(tip, PartyPoints(0, -54, 2.3f, -50, 0, -47, -2.3f, -50));
        }

        private static void DrawTraveler(Graphics g, float x, float y, float scale, Ancestry ancestry, Color cloth, int pose, bool moving)
        {
            GraphicsState state = g.Save();
            try
            {
                g.TranslateTransform(x, y);
                float stature = ancestry == Ancestry.Dwarf || ancestry == Ancestry.Goblin ? .83f : ancestry == Ancestry.Elf ? 1.08f : 1;
                g.ScaleTransform(scale, scale * stature);
                Color skin = MapPaper, dark = MapInk;
                using (Pen ground = new Pen(Color.FromArgb(120, MapInk), .6f)) g.DrawLine(ground, -6, 1, 7, 1.8f);
                float stride = moving ? 3.6f : 2.5f;
                using (Pen leg = new Pen(MapInk, 1.15f) { StartCap = LineCap.Round, EndCap = LineCap.Round })
                {
                    g.DrawLine(leg, -1.5f, -7, -stride, -.8f);
                    g.DrawLine(leg, 1.9f, -7, stride + 1, -.3f);
                }
                using (Pen boot = new Pen(dark, 1f) { StartCap = LineCap.Round, EndCap = LineCap.Round })
                { g.DrawLine(boot, -stride - .5f, -.5f, -stride + 1.7f, -.5f); g.DrawLine(boot, stride + .2f, 0, stride + 2.5f, 0); }
                if (pose == 2)
                {
                    using (Brush pack = new SolidBrush(MapPaper)) g.FillEllipse(pack, 1, -18, 7, 12);
                    using (Pen bind = new Pen(MapInk, .8f)) { g.DrawEllipse(bind, 1, -18, 7, 12); g.DrawLine(bind, 2, -12, 6, -11); }
                }
                PointF[] tunicShape = PartyPoints(-3, -18, 2.5f, -18, 4.8f, -8, 1.4f, -5.5f, -4.7f, -7);
                using (Brush tunic = new SolidBrush(MapPaper)) g.FillPolygon(tunic, tunicShape);
                using (Pen seam = new Pen(MapInk, .9f))
                { g.DrawPolygon(seam, tunicShape); g.DrawLine(seam, -1.8f, -16, -2.7f, -8); g.DrawLine(seam, 1.2f, -14, 2.7f, -7); }
                using (Pen belt = new Pen(cloth, 1.2f)) g.DrawLine(belt, -3.7f, -10.8f, 3.5f, -10.1f);
                using (Pen arm = new Pen(MapInk, 1.1f) { StartCap = LineCap.Round, EndCap = LineCap.Round })
                {
                    g.DrawLines(arm, new[] { new PointF(-3, -16), new PointF(-5.3f, -12), new PointF(pose == 0 ? -5.5f : -3.5f, -9.5f) });
                    g.DrawLines(arm, new[] { new PointF(3, -16), new PointF(5, -12), new PointF(pose == 1 ? 6.5f : 4.6f, -10) });
                }
                if (pose == 0 || pose == 1)
                {
                    float staffX = pose == 0 ? -6 : 6.8f;
                    using (Pen staff = new Pen(MapInk, 1f)) g.DrawLine(staff, staffX, -27, staffX + .8f, -.5f);
                    if (pose == 0) using (Pen point = new Pen(MapInk, .65f)) g.DrawPolygon(point, PartyPoints(staffX, -31, staffX - 1.5f, -26, staffX + 1.6f, -26));
                }
                using (Brush neck = new SolidBrush(skin)) g.FillRectangle(neck, -1.4f, -20, 2.7f, 3.8f);
                using (Brush face = new SolidBrush(skin)) g.FillEllipse(face, -3, -25, 6, 7);
                using (Pen hair = new Pen(MapInk, .8f))
                {
                    g.DrawEllipse(hair, -3, -25, 6, 7);
                    g.DrawArc(hair, -3.2f, -25.7f, 6.5f, 6.4f, 177, 189);
                    g.DrawLine(hair, -2, -24, 1, -25); g.DrawLine(hair, -1, -23, 2, -24);
                    if (pose == 2) g.DrawArc(hair, 1.5f, -22.8f, 2.4f, 5.3f, -80, 240);
                }
                using (Pen brow = new Pen(MapInk, .6f)) g.DrawLine(brow, -1.9f, -21.9f, -.4f, -22.2f);
            }
            finally { g.Restore(state); }
        }

        private static PointF[] PartyPoints(params float[] coordinates)
        {
            PointF[] points = new PointF[coordinates.Length / 2];
            for (int i = 0; i < points.Length; i++) points[i] = new PointF(coordinates[i * 2], coordinates[i * 2 + 1]);
            return points;
        }

        internal static void DrawAnimalCounter(Graphics g, Beast animal, UnitProfile profile, RectangleF box, bool selected, bool detailed, bool health)
        {
            bool hostile = health && profile.Hostile;
            Color ink = hostile ? MapDanger : animal.Domestic ? MapAccent : MapInk;
            float r = Math.Min(7, box.Height * .24f);
            using (GraphicsPath shape = CounterShape(box, r, hostile))
            using (Brush fill = new SolidBrush(selected ? Color.FromArgb(255, 249, 240, 220) : Color.FromArgb(242, MapPaper)))
            using (Pen edge = new Pen(Color.FromArgb(selected ? 255 : 177, ink), selected ? 2 : 1))
            {
                g.FillPath(fill, shape); g.DrawPath(edge, shape);
                if (animal.Domestic)
                {
                    RectangleF inner = box; inner.Inflate(-2.5f, -2.5f);
                    using (GraphicsPath inset = CounterShape(inner, Math.Max(2, r - 2), false))
                    using (Pen line = new Pen(Color.FromArgb(155, ink), .7f)) g.DrawPath(line, inset);
                }
            }
            if (detailed)
            {
                RectangleF icon = new RectangleF(box.X + 3, box.Y + 3, box.Width * .52f, box.Height - (health ? 10 : 6));
                IdentityArt.DrawAnimal(g, animal.Kind, icon, ink, animal.Domestic);
                Typography.Line(g, animal.Count.ToString(CultureInfo.InvariantCulture), new RectangleF(box.X + box.Width * .53f, box.Y + 2, box.Width * .43f - 2, box.Height - 10), 14, ink, TypeRole.Utility, true, StringAlignment.Center);
                if (health)
                {
                    float ratio = profile.MaxHealth <= 0 ? 0 : Math.Max(0, Math.Min(1, (float)profile.CurrentHealth / profile.MaxHealth));
                    RectangleF bar = new RectangleF(box.X + 6, box.Bottom - 6, box.Width - 12, 2.5f);
                    using (Brush empty = new SolidBrush(Color.FromArgb(95, ink))) g.FillRectangle(empty, bar);
                    using (Brush full = new SolidBrush(profile.Wounds > 0 ? MapDanger : MapHealthy)) g.FillRectangle(full, bar.X, bar.Y, bar.Width * ratio, bar.Height);
                }
                else if (!animal.Domestic && animal.PositiveContacts > 0)
                    DrawTrust(g, animal.PositiveContacts, box, ink);
            }
            else
            {
                IdentityArt.DrawAnimal(g, animal.Kind, new RectangleF(box.X + 2, box.Y + 1, box.Width - 4, box.Height - 2), ink, animal.Domestic);
                if (health && profile.Wounds > 0)
                    using (Pen wound = new Pen(MapDanger, 1.4f)) g.DrawLine(wound, box.X + 3, box.Bottom - 3, box.Right - 3, box.Y + 3);
            }
            if (selected)
            {
                RectangleF label = new RectangleF(box.X - 20, box.Y - 19, box.Width + 40, 17);
                using (Brush background = new SolidBrush(Color.FromArgb(247, MapPaper))) g.FillRectangle(background, label);
                using (Pen rule = new Pen(Color.FromArgb(130, ink), .7f)) g.DrawLine(rule, label.Left, label.Bottom - 1, label.Right, label.Bottom - 1);
                string text = "#" + animal.Id.ToString(CultureInfo.InvariantCulture);
                if (health && detailed) text += "  /  STR " + profile.Strength.ToString("0", CultureInfo.InvariantCulture);
                Typography.Line(g, text, label, 10, ink, TypeRole.Utility, true, StringAlignment.Center);
            }
        }

        private static void DrawTrust(Graphics g, int contacts, RectangleF box, Color ink)
        {
            int dots = Math.Min(6, contacts);
            using (Brush brush = new SolidBrush(ink))
                for (int i = 0; i < dots; i++) g.FillEllipse(brush, box.X + box.Width * .5f + (i - (dots - 1) * .5f) * 4 - 1, box.Bottom - 5, 2, 2);
        }
        private static GraphicsPath CounterShape(RectangleF box, float radius, bool pointed)
        {
            GraphicsPath path = new GraphicsPath();
            if (pointed)
                path.AddPolygon(new[] { new PointF(box.Left + radius, box.Top), new PointF(box.Right - radius, box.Top), new PointF(box.Right, box.Top + radius), new PointF(box.Right, box.Bottom - radius), new PointF(box.Right - radius, box.Bottom), new PointF(box.Left + radius, box.Bottom), new PointF(box.Left, box.Bottom - radius), new PointF(box.Left, box.Top + radius) });
            else
            {
                path.AddArc(box.Left, box.Top, radius * 2, radius * 2, 180, 90);
                path.AddArc(box.Right - radius * 2, box.Top, radius * 2, radius * 2, 270, 90);
                path.AddArc(box.Right - radius * 2, box.Bottom - radius * 2, radius * 2, radius * 2, 0, 90);
                path.AddArc(box.Left, box.Bottom - radius * 2, radius * 2, radius * 2, 90, 90); path.CloseFigure();
            }
            return path;
        }
    }
}
