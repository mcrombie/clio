using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using Clio.Simulation;

namespace Clio.Desktop
{
    internal sealed partial class MapRenderer
    {
        // Geographic regions already exist in World. Draw only shared, observed
        // land edges: a remembered region never reveals its extent into fog.
        private void DrawRegionBorders(Graphics g, Game game)
        {
            using (Pen shadow = new Pen(Color.FromArgb(180, 20, 37, 37), 4.5f))
            using (Pen line = new Pen(Color.FromArgb(230, 235, 221, 176), 1.8f))
            {
                shadow.StartCap = shadow.EndCap = line.StartCap = line.EndCap = LineCap.Round;
                foreach (ProjectedCell p in Visible)
                {
                    if (!p.Cell.IsLand || !Known(game, p.Cell.Id)) continue;
                    for (int i = 0; i < p.Polygon.Length; i++)
                    {
                        int next = (i + 1) % p.Polygon.Length;
                        int other = SharedCell(p.Cell.Corners[i], p.Cell.Corners[next], p.Cell.Id);
                        if (other < 0 || !Known(game, other)) continue;
                        Cell neighbor = game.World.Cells[other];
                        if (!neighbor.IsLand || neighbor.RegionId == p.Cell.RegionId ||
                            other < p.Cell.Id && projected.ContainsKey(other)) continue;
                        PointF a = p.Polygon[i], b = p.Polygon[next];
                        if (Math.Abs(a.X - b.X) + Math.Abs(a.Y - b.Y) < .1f) continue;
                        g.DrawLine(shadow, a, b); g.DrawLine(line, a, b);
                    }
                }
            }
        }

        private void DrawRegionLabels(Graphics g, Game game)
        {
            if (IsNavigating) return;
            using (GraphicsPath path = new GraphicsPath(FillMode.Winding))
            {
                foreach (ProjectedCell p in Visible)
                    if (p.Cell.IsLand && Known(game, p.Cell.Id)) path.AddPolygon(p.Polygon);
                if (path.PointCount == 0) return;
                using (Region knownLand = new Region(path)) DrawRegionLabelsOnKnownLand(g, game, knownLand);
            }
        }

        private void DrawRegionLabelsOnKnownLand(Graphics g, Game game, Region knownLand)
        {
            List<RectangleF> occupied = new List<RectangleF>();
            foreach (Tuple<RectangleF, int> target in animalTargets.Concat(bandTargets))
            { RectangleF box = target.Item1; box.Inflate(12, 12); occupied.Add(box); }
            foreach (OpportunityTarget target in saltTargets)
            { RectangleF box = target.Bounds; box.Inflate(8, 8); occupied.Add(box); }
            foreach (Band band in game.Bands.Where(b => b.Population > 0 && Known(game, b.CellId)))
            {
                ProjectedCell p;
                if (projected.TryGetValue(band.CellId, out p))
                    occupied.Add(new RectangleF(p.Center.X - 117, p.Center.Y - 58, 234, 114));
            }
            var regions = Visible.Where(p => p.Cell.IsLand && Known(game, p.Cell.Id) && p.Depth > .25 && p.Radius > 18)
                .GroupBy(p => p.Cell.RegionId).OrderByDescending(group => group.Count());
            foreach (var group in regions)
            {
                float centerX = group.Average(p => p.Center.X), centerY = group.Average(p => p.Center.Y);
                foreach (ProjectedCell p in group.OrderBy(p => Math.Abs(p.Center.X - centerX) + Math.Abs(p.Center.Y - centerY)))
                {
                    const float width = 126, height = 27;
                    RectangleF box = new RectangleF(p.Center.X - width / 2, p.Center.Y - height / 2, width, height);
                    if (box.Left < Bounds.Left + 12 || box.Right > Bounds.Right - 76 ||
                        box.Top < Bounds.Top + 66 || box.Bottom > Bounds.Bottom - 65 || occupied.Any(r => r.IntersectsWith(box))) continue;
                    // The entire label must fit observed land, including at the
                    // atlas scale where it can span several small hexes.
                    using (Region outside = new Region(box))
                    { outside.Exclude(knownLand); if (!outside.IsEmpty(g)) continue; }
                    using (Brush backing = new SolidBrush(Color.FromArgb(175, 20, 37, 37))) g.FillRectangle(backing, box);
                    Art.CenterText(g, "Region " + group.Key, box, 17, Color.FromArgb(242, 232, 208), true);
                    box.Inflate(18, 16); occupied.Add(box);
                    break;
                }
            }
        }
    }
}
