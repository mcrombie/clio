using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using Clio.Simulation;

namespace Clio.Desktop
{
    internal sealed partial class MapRenderer
    {
        // Source badges resolve to their own terrain hex through PickOpportunity;
        // the marker never enters either unit target collection.
        private readonly List<int> saltMarkerCells = new List<int>();

        private void DrawSaltSources(Graphics g, Game game)
        {
            saltMarkerCells.Clear(); saltTargets.Clear();
            if (!game.SaltEnabled) return;
            HashSet<int> knownSources = new HashSet<int>(SaltEconomy.KnownSources(game));
            if (game.TerrainTravelEnabled)
            {
                Band actor = game.Bands.FirstOrDefault(b => b.Id == CommandedBandId && game.CanControlBand(b.Id)) ?? game.TribeLeaderBand ?? game.Player;
                knownSources.IntersectWith(Visible.Where(p => knownSources.Contains(p.Cell.Id))
                    .Where(p => !PlaceHasUnits(game, p.Cell.Id)).OrderByDescending(p => Vec3.Dot(game.World.Cells[actor.CellId].Center, p.Cell.Center)).ThenBy(p => p.Cell.Id).Take(6).Select(p => p.Cell.Id).ToArray());
            }
            foreach (ProjectedCell cell in Visible)
            {
                // Atlas mode does not disclose deposits beyond remembered land.
                // Source reads immutable geology; no hidden neighbor is inspected.
                if (!game.Explored.Contains(cell.Cell.Id) || !knownSources.Contains(cell.Cell.Id) || cell.Depth < .10 || cell.Radius < 4) continue;
                SaltSource source = SaltEconomy.Source(game, cell.Cell.Id);
                if (source == SaltSource.None) continue;
                bool detailed = Zoom > 1.7 && cell.Radius >= 19;
                float size = detailed ? System.Math.Min(28, System.Math.Max(19, cell.Radius * .34f)) : System.Math.Min(9, System.Math.Max(5, cell.Radius * .72f));
                PointF point = new PointF(cell.Center.X - cell.Radius * .52f, cell.Center.Y + cell.Radius * .22f);
                RectangleF box = new RectangleF(point.X - size * .5f, point.Y - size * .5f, size, size);
                if (!Bounds.IntersectsWith(box)) continue;
                GraphicsState state = g.Save();
                using (GraphicsPath knownHex = new GraphicsPath())
                {
                    knownHex.AddPolygon(cell.Polygon); g.SetClip(knownHex, CombineMode.Intersect);
                    bool beacon = game.TerrainTravelEnabled && InterestHighlights && detailed;
                    bool hot = RegisterOpportunity(cell, point, size, SaltOpportunity, beacon);
                    if (hot) DrawOpportunityEmphasis(g, cell, Color.FromArgb(218, 238, 220));
                    if (beacon) DrawOpportunityBeacon(g, point, size, Color.FromArgb(218, 238, 220), 1, source, hot);
                    else DrawSaltSourceIcon(g, source, box, detailed || hot);
                }
                g.Restore(state); saltMarkerCells.Add(cell.Cell.Id);
            }
        }

        internal static void DrawSaltSourceIcon(Graphics g, SaltSource source, RectangleF box, bool badge)
        {
            if (box.Width <= 0 || box.Height <= 0 || source == SaltSource.None) return;
            GraphicsState state = g.Save();
            try
            {
                g.TranslateTransform(box.X + box.Width * .5f, box.Y + box.Height * .5f);
                g.ScaleTransform(box.Width / 24, box.Height / 24);
                if (badge)
                {
                    using (Brush shadow = new SolidBrush(Color.FromArgb(70, 7, 20, 20))) g.FillEllipse(shadow, -10.5f, -9, 22, 22);
                    using (Brush ground = new SolidBrush(Color.FromArgb(207, 31, 49, 49))) g.FillEllipse(ground, -11, -11, 22, 22);
                    using (Pen rim = new Pen(Color.FromArgb(125, 204, 211, 186), .8f)) g.DrawEllipse(rim, -11, -11, 22, 22);
                }
                using (Pen water = new Pen(Color.FromArgb(189, 142, 192, 194), 1.1f))
                {
                    if (source == SaltSource.Coastal)
                    {
                        g.DrawBezier(water, -8, 6, -4, 3, 1, 9, 7, 6);
                        g.DrawBezier(water, -6, 9, -1, 7, 3, 10, 6, 8);
                    }
                    else g.DrawEllipse(water, -8, 3, 16, 5);
                }
                DrawSaltCrystal(g, -5, -2, 3.7f, 7);
                DrawSaltCrystal(g, 0, -7, 4.1f, 12);
                DrawSaltCrystal(g, 5, -3, 3.7f, 8);
            }
            finally { g.Restore(state); }
        }

        private static void DrawSaltCrystal(Graphics g, float x, float y, float width, float height)
        {
            PointF top = new PointF(x, y), left = new PointF(x - width * .7f, y + height * .33f);
            PointF right = new PointF(x + width * .7f, y + height * .33f), bottom = new PointF(x, y + height);
            using (Brush light = new SolidBrush(Color.FromArgb(242, 243, 226))) g.FillPolygon(light, new[] { top, left, bottom });
            using (Brush shade = new SolidBrush(Color.FromArgb(178, 203, 195))) g.FillPolygon(shade, new[] { top, right, bottom });
            using (Pen rim = new Pen(Color.FromArgb(140, 75, 104, 96), .55f)) g.DrawPolygon(rim, new[] { top, left, bottom, right });
        }
    }
}
