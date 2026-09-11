using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using Clio.Simulation;

namespace Clio.Desktop
{
    // Small field annotations: broken ink rules = wild, paired rules = household,
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
            float r = Math.Min(5, box.Height * .19f);
            using (GraphicsPath shape = CounterShape(box, r, hostile))
            using (Brush fill = new SolidBrush(selected ? Color.FromArgb(253, 249, 240, 220) : Color.FromArgb(236, MapPaper)))
            {
                g.FillPath(fill, shape);
                if (selected)
                {
                    using (Pen chosen = new Pen(Color.FromArgb(209, ink), 1.1f) { DashPattern = new float[] { 8, 4 }, LineJoin = LineJoin.Round })
                        g.DrawPath(chosen, shape);
                }
            }
            // Broken marginal rules suggest a field annotation, rather than an
            // enclosed interface card. Paired rules still identify domestic stock.
            using (Pen rule = new Pen(Color.FromArgb(selected ? 189 : 129, ink), .65f))
            {
                g.DrawLine(rule, box.X + r, box.Y + 1.1f, box.X + box.Width * .34f, box.Y + .75f);
                g.DrawLine(rule, box.X + box.Width * .64f, box.Y + .9f, box.Right - r, box.Y + 1.25f);
                if (animal.Domestic)
                {
                    g.DrawLine(rule, box.X + r + 1, box.Y + 3.1f, box.X + box.Width * .33f, box.Y + 2.75f);
                    g.DrawLine(rule, box.X + 1.1f, box.Y + r, box.X + .75f, box.Bottom - r);
                    g.DrawLine(rule, box.X + 3.1f, box.Y + r + 1, box.X + 2.75f, box.Bottom - r - 1);
                }
            }
            if (hostile)
                using (Pen danger = new Pen(Color.FromArgb(227, MapDanger), 1.1f) { LineJoin = LineJoin.Round })
                {
                    g.DrawLines(danger, new[] { new PointF(box.Left + r, box.Top + .5f), new PointF(box.Left + .5f, box.Top + r), new PointF(box.Left + .7f, box.Top + r + 3) });
                    g.DrawLines(danger, new[] { new PointF(box.Right - r, box.Top + .5f), new PointF(box.Right - .5f, box.Top + r), new PointF(box.Right - .7f, box.Top + r + 3) });
                    g.DrawLines(danger, new[] { new PointF(box.Right - .5f, box.Bottom - r - 2), new PointF(box.Right - .5f, box.Bottom - r), new PointF(box.Right - r, box.Bottom - .5f) });
                }
            if (detailed)
            {
                RectangleF icon = new RectangleF(box.X + 2, box.Y + 3, box.Width * .55f, box.Height - (health ? 9 : 6));
                IdentityArt.DrawAnimal(g, animal.Kind, icon, ink, animal.Domestic);
                Typography.Line(g, animal.Count.ToString(CultureInfo.InvariantCulture), new RectangleF(box.X + box.Width * .60f, box.Y + 2, box.Width * .37f - 1, box.Height - 10), 15, ink, TypeRole.Heading, true, StringAlignment.Center);
                if (!health && !animal.Domestic && animal.PositiveContacts > 0)
                    DrawTrust(g, animal.PositiveContacts, box, ink);
            }
            else
            {
                IdentityArt.DrawAnimal(g, animal.Kind, new RectangleF(box.X + 2, box.Y + 1, box.Width - 4, box.Height - (health ? 5 : 2)), ink, animal.Domestic);
            }
            if (health) DrawAnimalCondition(g, profile, box, detailed, ink);
            if (selected)
            {
                RectangleF label = new RectangleF(box.X - 20, box.Y - 19, box.Width + 40, 17);
                using (GraphicsPath paper = CounterShape(label, 2, false))
                using (Brush background = new SolidBrush(Color.FromArgb(243, MapPaper))) g.FillPath(background, paper);
                using (Pen rule = new Pen(Color.FromArgb(118, ink), .65f))
                {
                    g.DrawLine(rule, label.Left + 2, label.Bottom - 1, label.X + label.Width * .41f, label.Bottom - 1.4f);
                    g.DrawLine(rule, label.X + label.Width * .57f, label.Bottom - 1.2f, label.Right - 2, label.Bottom - 1);
                }
                string text = "#" + animal.Id.ToString(CultureInfo.InvariantCulture);
                if (health && detailed) text += "  /  STR " + profile.Strength.ToString("0", CultureInfo.InvariantCulture);
                Typography.Line(g, text, label, 11, ink, TypeRole.Annotation, true, StringAlignment.Center);
            }
        }

        private static void DrawAnimalCondition(Graphics g, UnitProfile profile, RectangleF box, bool detailed, Color ink)
        {
            float ratio = profile.MaxHealth <= 0 ? 0 : Math.Max(0, Math.Min(1, (float)profile.CurrentHealth / profile.MaxHealth));
            float inset = detailed ? 6 : 3, x = box.X + inset, y = box.Bottom - (detailed ? 4.2f : 2.3f), width = box.Width - inset * 2;
            using (Pen baseLine = new Pen(Color.FromArgb(89, ink), .7f)) g.DrawLine(baseLine, x, y, x + width, y - .2f);
            Color condition = profile.Wounds > 0 ? MapDanger : MapHealthy;
            if (ratio > 0)
                using (Pen remaining = new Pen(Color.FromArgb(221, condition), detailed ? 1.35f : 1.0f) { StartCap = LineCap.Round, EndCap = LineCap.Round })
                    g.DrawLine(remaining, x, y, x + width * ratio, y - .2f * ratio);
            int divisions = detailed ? 4 : 2;
            using (Pen tick = new Pen(Color.FromArgb(158, ink), .65f))
                for (int i = 0; i <= divisions; i++)
                {
                    float at = x + width * i / divisions;
                    g.DrawLine(tick, at, y - (detailed ? 1.2f : .75f), at + .15f, y + (detailed ? 1.1f : .7f));
                }
        }

        private static void DrawTrust(Graphics g, int contacts, RectangleF box, Color ink)
        {
            int marks = Math.Min(6, contacts);
            using (Pen tally = new Pen(Color.FromArgb(192, ink), .8f))
                for (int i = 0; i < marks; i++)
                {
                    float x = box.X + box.Width * .5f + (i - (marks - 1) * .5f) * 3.3f;
                    g.DrawLine(tally, x - .5f, box.Bottom - 3, x + .7f, box.Bottom - 6);
                }
        }
        private static GraphicsPath CounterShape(RectangleF box, float radius, bool pointed)
        {
            GraphicsPath path = new GraphicsPath();
            if (pointed)
                path.AddPolygon(new[] { new PointF(box.Left + radius, box.Top), new PointF(box.Right - radius, box.Top), new PointF(box.Right, box.Top + radius), new PointF(box.Right, box.Bottom - radius), new PointF(box.Right - radius, box.Bottom), new PointF(box.Left + radius, box.Bottom), new PointF(box.Left, box.Bottom - radius), new PointF(box.Left, box.Top + radius) });
            else
            {
                float nick = Math.Min(1.4f, radius * .45f);
                path.AddPolygon(new[] {
                    new PointF(box.Left + nick, box.Top + .4f), new PointF(box.Left + box.Width * .28f, box.Top),
                    new PointF(box.Left + box.Width * .66f, box.Top + .55f), new PointF(box.Right - nick, box.Top + .25f),
                    new PointF(box.Right, box.Top + nick + .7f), new PointF(box.Right - .35f, box.Top + box.Height * .54f),
                    new PointF(box.Right - .1f, box.Bottom - nick), new PointF(box.Right - nick, box.Bottom - .3f),
                    new PointF(box.Left + box.Width * .41f, box.Bottom), new PointF(box.Left + nick, box.Bottom - .5f),
                    new PointF(box.Left, box.Bottom - nick - .2f), new PointF(box.Left + .4f, box.Top + box.Height * .42f)
                });
            }
            return path;
        }
    }
}
