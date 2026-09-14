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
        private void DrawGuidedRegionBoundary(Graphics g, Game game)
        {
            if (game.Player == null || game.Player.Population <= 0) return;
            int currentRegion = game.World.Cells[game.Player.CellId].RegionId;
            using (GraphicsPath border = new GraphicsPath())
            using (GraphicsPath frontier = new GraphicsPath())
            {
                foreach (ProjectedCell cell in Visible)
                {
                    if (!game.Explored.Contains(cell.Cell.Id) || !cell.Cell.IsLand ||
                        cell.Cell.RegionId != currentRegion || cell.Depth < .08) continue;
                    for (int edge = 0; edge < cell.Polygon.Length; edge++)
                    {
                        int next = (edge + 1) % cell.Polygon.Length;
                        int other = SharedCell(cell.Cell.Corners[edge], cell.Cell.Corners[next], cell.Cell.Id);
                        bool remembered = other >= 0 && game.Explored.Contains(other);
                        // Read a neighbor's region only if it is already known.
                        // An unknown edge marks the end of this drawing, never
                        // the undiscovered extent of the actual region.
                        if (remembered && game.World.Cells[other].IsLand &&
                            game.World.Cells[other].RegionId == currentRegion) continue;
                        GraphicsPath line = remembered ? border : frontier;
                        line.StartFigure(); line.AddLine(cell.Polygon[edge], cell.Polygon[next]);
                    }
                }
                using (Pen paper = new Pen(Color.FromArgb(159, MapPaper.Paper), 3.5f) { LineJoin = LineJoin.Round, StartCap = LineCap.Round, EndCap = LineCap.Round })
                using (Pen ink = new Pen(Color.FromArgb(164, MapPaper.Russet), 1.1f) { LineJoin = LineJoin.Round, StartCap = LineCap.Round, EndCap = LineCap.Round })
                using (Pen uncertain = new Pen(Color.FromArgb(132, MapPaper.MutedInk), 1f) { DashPattern = new[] { 3f, 4f }, StartCap = LineCap.Round, EndCap = LineCap.Round })
                {
                    if (border.PointCount > 0) { g.DrawPath(paper, border); g.DrawPath(ink, border); }
                    if (frontier.PointCount > 0) { g.DrawPath(paper, frontier); g.DrawPath(uncertain, frontier); }
                }
            }
        }

        // This is a distance to the visible ink, not a distance to any hidden
        // terrain feature. Three/four chamfer weights approximate a round margin
        // in two linear passes; it is generated with the cached landscape only.
        private static int[] UnknownDistance(byte[] known, int width, int height)
        {
            int[] distance = new int[known.Length];
            for (int i = 0; i < distance.Length; i++) distance[i] = known[i] > 230 ? 0 : 100000;
            for (int y = 0; y < height; y++) for (int x = 0; x < width; x++)
            {
                int i = y * width + x, nearest = distance[i];
                if (x > 0) nearest = Math.Min(nearest, distance[i - 1] + 3);
                if (y > 0)
                {
                    nearest = Math.Min(nearest, distance[i - width] + 3);
                    if (x > 0) nearest = Math.Min(nearest, distance[i - width - 1] + 4);
                    if (x + 1 < width) nearest = Math.Min(nearest, distance[i - width + 1] + 4);
                }
                distance[i] = nearest;
            }
            for (int y = height - 1; y >= 0; y--) for (int x = width - 1; x >= 0; x--)
            {
                int i = y * width + x, nearest = distance[i];
                if (x + 1 < width) nearest = Math.Min(nearest, distance[i + 1] + 3);
                if (y + 1 < height)
                {
                    nearest = Math.Min(nearest, distance[i + width] + 3);
                    if (x > 0) nearest = Math.Min(nearest, distance[i + width - 1] + 4);
                    if (x + 1 < width) nearest = Math.Min(nearest, distance[i + width + 1] + 4);
                }
                distance[i] = nearest;
            }
            return distance;
        }

        private sealed class FrontierRumor
        {
            internal PointF Center;
            internal RectangleF Bounds;
            internal uint Seed;
        }

        private void DrawFrontierRumors(Graphics g, Game game)
        {
            if (!Fog || IsNavigating || Zoom < 2.8 || Layer != 0) return;
            List<FrontierRumor> candidates = new List<FrontierRumor>();
            using (GraphicsPath remembered = new GraphicsPath(FillMode.Winding))
            {
                foreach (ProjectedCell cell in Visible)
                {
                    if (!game.Explored.Contains(cell.Cell.Id)) continue;
                    remembered.AddPolygon(cell.Polygon);
                    if (!cell.Cell.IsLand || cell.Depth < .2 || cell.Radius < 28) continue;
                    for (int edge = 0; edge < cell.Polygon.Length; edge++)
                    {
                        int next = (edge + 1) % cell.Polygon.Length;
                        int other = SharedCell(cell.Cell.Corners[edge], cell.Cell.Corners[next], cell.Cell.Id);
                        if (other < 0 || game.Explored.Contains(other)) continue;
                        PointF border = Lerp(cell.Polygon[edge], cell.Polygon[next], .5f);
                        float dx = border.X - cell.Center.X, dy = border.Y - cell.Center.Y;
                        float length = Math.Max(1, (float)Math.Sqrt(dx * dx + dy * dy));
                        float margin = Math.Max(112, Math.Min(132, cell.Radius * 1.02f));
                        PointF center = new PointF(border.X + dx / length * margin, border.Y + dy / length * margin);
                        RectangleF box = new RectangleF(center.X - 86, center.Y - 27, 172, 88);
                        if (box.Left < Bounds.Left + 66 || box.Right > Bounds.Right - 66 ||
                            box.Top < Bounds.Top + 135 || box.Bottom > Bounds.Bottom - 135) continue;
                        // Leave the usual adviser corner quiet. This is ambient
                        // marginalia, never a competing call to action.
                        if (box.Right > Bounds.Right - 365 && box.Top < Bounds.Top + 340) continue;
                        uint seed = unchecked((uint)(game.World.Seed * 31 + cell.Cell.Id * 7919 + edge * 1543));
                        seed ^= seed >> 16; seed *= 2246822519u; seed ^= seed >> 13;
                        candidates.Add(new FrontierRumor { Center = center, Bounds = box, Seed = seed });
                    }
                }
                if (remembered.PointCount == 0 || candidates.Count == 0) return;
                using (Region known = new Region(remembered))
                {
                    GraphicsState state = g.Save();
                    try
                    {
                        // No mark crosses back onto real mapped terrain. The
                        // sketches intentionally consult no unknown cell data,
                        // and never register an opportunity or a hit target.
                        g.ExcludeClip(known);
                        List<RectangleF> occupied = new List<RectangleF>();
                        foreach (FrontierRumor rumor in candidates.OrderBy(r => r.Seed))
                        {
                            if (known.IsVisible(rumor.Bounds) || occupied.Any(r => r.IntersectsWith(rumor.Bounds))) continue;
                            DrawRumor(g, rumor);
                            RectangleF spacing = rumor.Bounds; spacing.Inflate(70, 50); occupied.Add(spacing);
                            if (occupied.Count == 3) break;
                        }
                    }
                    finally { g.Restore(state); }
                }
            }
        }

        private static void DrawRumor(Graphics g, FrontierRumor rumor)
        {
            float x = rumor.Center.X, y = rumor.Center.Y;
            int kind = (int)(rumor.Seed % 3);
            Color ink = Color.FromArgb(91, MapPaper.MutedInk);
            using (Pen pencil = new Pen(ink, .8f) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round })
            {
                if (kind == 0)
                {
                    // A tentative pass: two incomplete contour strokes, with
                    // a broken path between. Nothing claims a real mountain.
                    g.DrawLines(pencil, new[] { new PointF(x - 49, y + 10), new PointF(x - 33, y - 10), new PointF(x - 24, y - 19), new PointF(x - 10, y + 5) });
                    g.DrawLines(pencil, new[] { new PointF(x + 2, y + 5), new PointF(x + 19, y - 16), new PointF(x + 29, y - 5), new PointF(x + 43, y + 12) });
                    for (int i = 0; i < 4; i++) g.DrawLine(pencil, x - 29 + i * 4, y - 8 + i * 4, x - 36 + i * 5, y + 6 + i);
                    pencil.Color = Color.FromArgb(72, MapPaper.MutedInk); pencil.DashPattern = new[] { 2f, 5f };
                    g.DrawCurve(pencil, new[] { new PointF(x - 9, y + 17), new PointF(x - 3, y + 1), new PointF(x + 3, y - 9) }, .4f);
                }
                else if (kind == 1)
                {
                    pencil.DashPattern = new[] { 2f, 5f };
                    g.DrawCurve(pencil, new[] { new PointF(x - 48, y + 9), new PointF(x - 24, y + 3), new PointF(x - 5, y - 10), new PointF(x + 18, y - 3), new PointF(x + 42, y - 15) }, .35f);
                    Typography.Line(g, "?", new RectangleF(x + 40, y - 29, 18, 23), 19, ink, TypeRole.Annotation);
                }
                else
                {
                    pencil.Color = Color.FromArgb(84, MapPaper.Blue);
                    for (int line = 0; line < 2; line++)
                        g.DrawCurve(pencil, new[] { new PointF(x - 49, y + 9 + line * 3), new PointF(x - 16, y - 8 + line * 3), new PointF(x + 17, y + 3 + line * 3), new PointF(x + 45, y - 12 + line * 3) }, .55f);
                    Typography.Line(g, "?", new RectangleF(x + 41, y - 28, 18, 23), 19, ink, TypeRole.Annotation);
                }
            }
            string phrase = kind == 0 ? "A pass, they say..." : kind == 1 ? "Paths unconfirmed" : "Water, they say...";
            Typography.Line(g, phrase, new RectangleF(x - 85, y + 20, 170, 21), 14, Color.FromArgb(121, MapPaper.Ink), TypeRole.Annotation, true, StringAlignment.Center);
            Typography.Line(g, "rumor", new RectangleF(x - 48, y + 40, 96, 15), 10, Color.FromArgb(109, MapPaper.MutedInk), TypeRole.Annotation, false, StringAlignment.Center);
        }
    }
}
