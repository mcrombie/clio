using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using Clio.Simulation;

namespace Clio.Desktop
{
    // Small atlas counters: open ring = wild, gold double edge = household,
    // pointed red corners = hostile. Shape remains useful without color.
    internal static class UnitArt
    {
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
                Color identity = IdentityArt.ColorFor(identityId), dark = Color.FromArgb(28, 39, 35);
                using (Brush shadow = new SolidBrush(Color.FromArgb(69, 9, 22, 19))) g.FillEllipse(shadow, -25, -5, 59, 13);
                using (Brush shade = new SolidBrush(Color.FromArgb(62, 11, 25, 20))) g.FillEllipse(shade, -19, -3, 48, 8);
                if (selected || commanded)
                {
                    Color ringColor = commanded ? Art.Gold : identity;
                    using (Pen halo = new Pen(Color.FromArgb(46, ringColor), 5)) g.DrawEllipse(halo, -25, -7, 59, 15);
                    using (Pen ring = new Pen(Color.FromArgb(210, ringColor), 1.4f)) g.DrawEllipse(ring, -25, -7, 59, 15);
                    using (Pen front = new Pen(Color.FromArgb(244, Art.Mix(ringColor, Art.Ink, .35)), 1.4f)) g.DrawArc(front, -25, -7, 59, 15, 16, 149);
                }
                if (band.Settled)
                {
                    using (Brush wall = new SolidBrush(Color.FromArgb(165, 143, 108))) g.FillPolygon(wall, PartyPoints(-22, -7, -22, -17, -12, -17, -12, -5));
                    using (Brush side = new SolidBrush(Color.FromArgb(108, 101, 74))) g.FillPolygon(side, PartyPoints(-12, -17, -3, -13, -3, -4, -12, -5));
                    using (Brush roof = new SolidBrush(Color.FromArgb(191, 171, 124))) g.FillPolygon(roof, PartyPoints(-25, -17, -15, -27, -1, -14, -11, -12));
                    using (Pen seam = new Pen(Color.FromArgb(89, 80, 55), .75f)) g.DrawLine(seam, -15, -26, -11, -13);
                    using (Brush doorway = new SolidBrush(dark)) g.FillRectangle(doorway, -19, -12, 4, 6);
                }
                // A scout behind the standard and two companions in front give
                // the party depth without making the symbol a population count.
                if (band.Population >= 3)
                    DrawTraveler(g, -15, -3, .83f, band.Ancestry, Art.Mix(identity, Color.FromArgb(132, 125, 90), .48), 0, moving);
                DrawPartyStandard(g, identityId, identity, commanded);
                DrawTraveler(g, -5, 1, 1.02f, band.Ancestry, Art.Mix(identity, Color.FromArgb(174, 151, 101), .40), 1, moving);
                if (band.Population >= 2)
                    DrawTraveler(g, 23, 0, .88f, band.Ancestry, Art.Mix(identity, Color.FromArgb(102, 121, 111), .56), 2, moving);
                if (commanded)
                {
                    using (Brush fill = new SolidBrush(Art.Gold)) g.FillPolygon(fill, PartyPoints(-17, -47, -12.7f, -42.7f, -17, -38.4f, -21.3f, -42.7f));
                    using (Pen border = new Pen(Color.FromArgb(241, 240, 224, 179), .75f)) g.DrawPolygon(border, PartyPoints(-17, -47, -12.7f, -42.7f, -17, -38.4f, -21.3f, -42.7f));
                    using (Brush center = new SolidBrush(dark)) g.FillEllipse(center, -18.3f, -44, 2.6f, 2.6f);
                }
            }
            finally { g.Restore(state); }
        }

        private static void DrawPartyStandard(Graphics g, int id, Color identity, bool commanded)
        {
            using (Pen shade = new Pen(Color.FromArgb(125, 12, 24, 21), 3.4f)) g.DrawLine(shade, 1, -49, 1, 0);
            using (Pen wood = new Pen(Color.FromArgb(198, 177, 130), 1.8f)) g.DrawLine(wood, 0, -49, 0, 0);
            using (Pen edge = new Pen(Color.FromArgb(227, 213, 169), .65f)) g.DrawLine(edge, -.6f, -48, -.6f, -1);
            using (GraphicsPath cloth = new GraphicsPath())
            {
                cloth.AddBezier(1, -46, 10, -50, 24, -41, 34, -46);
                cloth.AddLine(34, -46, 31, -34); cloth.AddLine(31, -34, 34, -24);
                cloth.AddBezier(34, -24, 23, -18, 12, -27, 1, -24); cloth.CloseFigure();
                using (LinearGradientBrush fabric = new LinearGradientBrush(new RectangleF(0, -50, 35, 32), Art.Mix(identity, Art.Ink, .13), Art.Mix(identity, Color.FromArgb(36, 58, 47), .36), 0f)) g.FillPath(fabric, cloth);
                using (Pen rim = new Pen(Art.Mix(identity, Art.Ink, commanded ? .48 : .24), .85f)) g.DrawPath(rim, cloth);
                using (Pen fold = new Pen(Color.FromArgb(95, 245, 231, 189), .75f)) g.DrawBezier(fold, 4, -44, 12, -46, 24, -38, 30, -43);
                using (Pen hem = new Pen(Color.FromArgb(70, 32, 49, 36), .7f)) g.DrawBezier(hem, 4, -27, 15, -29, 23, -23, 30, -26);
            }
            IdentityArt.DrawEmblem(g, id, new RectangleF(8, -44, 18, 18), false);
            using (Brush tip = new SolidBrush(commanded ? Art.Ink : Art.Mix(identity, Art.Ink, .4)))
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
                Color skin = ancestry == Ancestry.Goblin ? Color.FromArgb(154, 169, 119) : pose == 0 ? Color.FromArgb(186, 141, 103) : pose == 1 ? Color.FromArgb(212, 172, 126) : Color.FromArgb(160, 117, 84);
                Color dark = Color.FromArgb(47, 45, 34), shade = Art.Mix(cloth, dark, .40);
                using (Brush shadow = new SolidBrush(Color.FromArgb(107, 16, 27, 22))) g.FillEllipse(shadow, -6, -1, 13, 3.6f);
                float stride = moving ? 3.6f : 2.5f;
                using (Pen leg = new Pen(Color.FromArgb(77, 65, 47), 2.6f) { StartCap = LineCap.Round, EndCap = LineCap.Round })
                {
                    g.DrawLine(leg, -1.5f, -7, -stride, -.8f);
                    g.DrawLine(leg, 1.9f, -7, stride + 1, -.3f);
                }
                using (Pen boot = new Pen(dark, 1.7f) { StartCap = LineCap.Round, EndCap = LineCap.Round })
                { g.DrawLine(boot, -stride - .5f, -.5f, -stride + 1.7f, -.5f); g.DrawLine(boot, stride + .2f, 0, stride + 2.5f, 0); }
                if (pose == 2)
                {
                    using (Brush pack = new SolidBrush(Color.FromArgb(130, 104, 69))) g.FillEllipse(pack, 1, -18, 7, 12);
                    using (Pen bind = new Pen(Color.FromArgb(198, 176, 130), .65f)) g.DrawLine(bind, 2, -12, 6, -11);
                }
                using (Brush tunic = new SolidBrush(cloth)) g.FillPolygon(tunic, PartyPoints(-3, -18, 2.5f, -18, 4.8f, -8, 1.4f, -5.5f, -4.7f, -7));
                using (Brush side = new SolidBrush(shade)) g.FillPolygon(side, PartyPoints(1.8f, -17.5f, 4.8f, -8, 1.4f, -5.5f, .1f, -10));
                using (Pen seam = new Pen(Art.Mix(cloth, Art.Ink, .32), .8f)) g.DrawLine(seam, -2.8f, -17, -3.7f, -8);
                using (Pen belt = new Pen(Color.FromArgb(80, 67, 45), 1.3f)) g.DrawLine(belt, -3.7f, -10.8f, 3.5f, -10.1f);
                using (Pen arm = new Pen(skin, 2.2f) { StartCap = LineCap.Round, EndCap = LineCap.Round })
                {
                    g.DrawLines(arm, new[] { new PointF(-3, -16), new PointF(-5.3f, -12), new PointF(pose == 0 ? -5.5f : -3.5f, -9.5f) });
                    g.DrawLines(arm, new[] { new PointF(3, -16), new PointF(5, -12), new PointF(pose == 1 ? 6.5f : 4.6f, -10) });
                }
                if (pose == 0 || pose == 1)
                {
                    float staffX = pose == 0 ? -6 : 6.8f;
                    using (Pen staff = new Pen(Color.FromArgb(116, 92, 58), 1.25f)) g.DrawLine(staff, staffX, -27, staffX + .8f, -.5f);
                    using (Pen light = new Pen(Color.FromArgb(197, 179, 136), .45f)) g.DrawLine(light, staffX - .3f, -25, staffX + .4f, -2);
                    if (pose == 0) using (Brush point = new SolidBrush(Color.FromArgb(196, 194, 162))) g.FillPolygon(point, PartyPoints(staffX, -31, staffX - 1.5f, -26, staffX + 1.6f, -26));
                }
                using (Brush neck = new SolidBrush(Art.Mix(skin, dark, .2))) g.FillRectangle(neck, -1.4f, -20, 2.7f, 3.8f);
                using (Brush face = new SolidBrush(skin)) g.FillEllipse(face, -3, -25, 6, 7);
                using (Brush hair = new SolidBrush(Color.FromArgb(67, 54, 36)))
                {
                    g.FillPie(hair, -3.2f, -25.7f, 6.5f, 6.4f, 177, 189);
                    if (pose == 2) g.FillEllipse(hair, 1.5f, -22.8f, 2.4f, 5.3f);
                }
                using (Pen brow = new Pen(Art.Mix(skin, Art.Ink, .28), .6f)) g.DrawLine(brow, -1.9f, -21.9f, -.4f, -22.2f);
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
            Color ink = hostile ? Color.FromArgb(236, 150, 124) : animal.Domestic ? Art.Gold : Color.FromArgb(229, 230, 199);
            float r = Math.Min(7, box.Height * .24f);
            using (GraphicsPath shape = CounterShape(box, r, hostile))
            using (Brush fill = new SolidBrush(selected ? Color.FromArgb(249, 35, 55, 55) : Color.FromArgb(227, 22, 39, 42)))
            using (Pen edge = new Pen(Color.FromArgb(selected ? 255 : 177, ink), selected ? 2 : 1))
            {
                if (selected)
                    using (Pen halo = new Pen(Color.FromArgb(60, ink), 7)) g.DrawPath(halo, shape);
                g.FillPath(fill, shape); g.DrawPath(edge, shape);
                if (animal.Domestic)
                {
                    RectangleF inner = box; inner.Inflate(-2.5f, -2.5f);
                    using (GraphicsPath inset = CounterShape(inner, Math.Max(2, r - 2), false))
                    using (Pen line = new Pen(Color.FromArgb(84, ink), .7f)) g.DrawPath(line, inset);
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
                    using (Brush full = new SolidBrush(profile.Wounds > 0 ? Color.FromArgb(229, 159, 120) : Color.FromArgb(155, 195, 164))) g.FillRectangle(full, bar.X, bar.Y, bar.Width * ratio, bar.Height);
                }
                else if (!animal.Domestic && animal.PositiveContacts > 0)
                    DrawTrust(g, animal.PositiveContacts, box, ink);
            }
            else
            {
                IdentityArt.DrawAnimal(g, animal.Kind, new RectangleF(box.X + 2, box.Y + 1, box.Width - 4, box.Height - 2), ink, animal.Domestic);
                if (health && profile.Wounds > 0)
                    using (Pen wound = new Pen(Color.FromArgb(229, 159, 120), 1.4f)) g.DrawLine(wound, box.X + 3, box.Bottom - 3, box.Right - 3, box.Y + 3);
            }
            if (selected)
            {
                RectangleF label = new RectangleF(box.X - 20, box.Y - 19, box.Width + 40, 17);
                using (Brush background = new SolidBrush(Color.FromArgb(242, 20, 36, 40))) g.FillRectangle(background, label);
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
