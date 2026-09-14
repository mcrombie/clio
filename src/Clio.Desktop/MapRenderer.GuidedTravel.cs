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
        private readonly List<Tuple<RectangleF, int>> guidedArrivalTargets = new List<Tuple<RectangleF, int>>();
        private Game guidedPathGame;
        private int guidedPathFrom = -1, guidedPathTo = -1, guidedPathKnownCount = -1;
        private readonly List<int> guidedKnownPath = new List<int>();

        public void ClearGuidedTravelPreview()
        { guidedArrivalTargets.Clear(); guidedPathGame = null; guidedKnownPath.Clear(); }

        public int PickGuidedArrival(PointF point)
        {
            for (int i = guidedArrivalTargets.Count - 1; i >= 0; i--)
                if (guidedArrivalTargets[i].Item1.Contains(point)) return guidedArrivalTargets[i].Item2;
            return -1;
        }

        // Fit only the starting point and offered entrance points. Camera fitting
        // never samples resources, units, terrain colors or unexplored boundaries.
        public void FitGuidedTravel(Game game, GuidedRegionDestination[] destinations, RectangleF freeArea)
        {
            if (destinations.Length == 0) return;
            Vec3[] locations = new[] { game.World.Cells[game.Player.CellId].Center }
                .Concat(destinations.Select(d => game.World.Cells[d.CellId].Center)).ToArray();
            Vec3 sum = new Vec3(); foreach (Vec3 location in locations) sum += location;
            Vec3 center = sum.Normalized();
            Longitude = Math.Atan2(center.X, center.Z); Latitude = Math.Asin(center.Y);
            RectangleF target = RectangleF.Inflate(freeArea, -67, -72);
            for (int iteration = 0; iteration < 8; iteration++)
            {
                PrepareGuidedTravelProjection();
                PointF[] points = locations.Select(Project).ToArray();
                float left = points.Min(p => p.X), rightmost = points.Max(p => p.X);
                float top = points.Min(p => p.Y), bottom = points.Max(p => p.Y);
                double fit = Math.Min(target.Width / Math.Max(1, rightmost - left), target.Height / Math.Max(1, bottom - top));
                if (fit < 1) { Zoom = Math.Max(.82, Zoom * fit * .95); continue; }
                float dx = target.X + target.Width / 2 - (left + rightmost) / 2;
                float dy = target.Y + target.Height / 2 - (top + bottom) / 2;
                if (Math.Abs(dx) < 2 && Math.Abs(dy) < 2) break;
                Longitude -= dx / (radius * Math.Max(.2, Math.Cos(Latitude)));
                Latitude = Math.Max(-1.45, Math.Min(1.45, Latitude + dy / (radius * RegionalVerticalScale)));
            }
            PrepareGuidedTravelProjection(); InvalidateTerrain();
        }

        private void PrepareGuidedTravelProjection()
        {
            radius = (float)(690 * .465 * Zoom);
            forward = new Vec3(Math.Sin(Longitude) * Math.Cos(Latitude), Math.Sin(Latitude), Math.Cos(Longitude) * Math.Cos(Latitude));
            right = new Vec3(Math.Cos(Longitude), 0, -Math.Sin(Longitude)); up = Vec3.Cross(forward, right);
        }

        public void DrawGuidedTravelPreview(Graphics g, Game game, GuidedRegionDestination[] destinations, int previewCell)
        {
            guidedArrivalTargets.Clear();
            if (destinations.Length == 0) return;
            GraphicsState state = g.Save(); g.SetClip(Bounds);
            GuidedRegionDestination selectedRoute = destinations.FirstOrDefault(d => d.CellId == previewCell);
            if (selectedRoute != null)
            {
                DrawGuidedDestinationBoundary(g, game, selectedRoute.RegionId);
                DrawGuidedTravelPath(g, game, selectedRoute.CellId);
            }
            for (int i = 0; i < destinations.Length; i++)
            {
                GuidedRegionDestination destination = destinations[i];
                Vec3 position = game.World.Cells[destination.CellId].Center;
                if (Vec3.Dot(position, forward) <= .03) continue;
                PointF point = Project(position);
                if (!RectangleF.Inflate(Bounds, -26, -28).Contains(point)) continue;
                bool highlighted = destination.CellId == previewCell;
                float r = highlighted ? 20 : 16;
                Color ink = highlighted ? MapPaper.Russet : MapPaper.MutedInk;
                PointF badge = new PointF(point.X, point.Y - r - 13);
                using (Brush paper = new SolidBrush(highlighted ? MapPaper.Paper : MapPaper.Ground))
                using (Pen outline = new Pen(ink, highlighted ? 1.8f : 1.05f))
                {
                    PointF[] stem = { new PointF(point.X - 7, point.Y - 17), point, new PointF(point.X + 7, point.Y - 17) };
                    g.FillPolygon(paper, stem); g.DrawLines(outline, stem);
                    g.FillEllipse(paper, badge.X - r, badge.Y - r, r * 2, r * 2);
                    g.DrawEllipse(outline, badge.X - r, badge.Y - r, r * 2, r * 2);
                    g.DrawEllipse(outline, point.X - 4, point.Y - 3, 8, 6);
                }
                Typography.Line(g, (i + 1).ToString(), new RectangleF(badge.X - r, badge.Y - r, r * 2, r * 2),
                    highlighted ? 22 : 18, ink, TypeRole.Number, false, StringAlignment.Center);
                RectangleF hit = new RectangleF(point.X - 27, badge.Y - r - 7, 54, r * 2 + 28);
                guidedArrivalTargets.Add(Tuple.Create(hit, destination.CellId));
                if (highlighted)
                {
                    RectangleF label = new RectangleF(point.X - 92, badge.Y - r - 36, 184, 26);
                    MapPaper.Surface(g, label, false);
                    Typography.Line(g, "Arrive here", label, 16, ink, TypeRole.Annotation, false, StringAlignment.Center);
                }
            }
            g.Restore(state);
        }

        private void DrawGuidedDestinationBoundary(Graphics g, Game game, int region)
        {
            using (GraphicsPath border = new GraphicsPath())
            using (GraphicsPath uncertain = new GraphicsPath())
            using (Brush wash = new SolidBrush(Color.FromArgb(28, MapPaper.Russet)))
            {
                foreach (ProjectedCell cell in Visible)
                {
                    if (!game.Explored.Contains(cell.Cell.Id) || cell.Cell.RegionId != region || !cell.Cell.IsLand) continue;
                    g.FillPolygon(wash, cell.Polygon);
                    for (int edge = 0; edge < cell.Polygon.Length; edge++)
                    {
                        int next = (edge + 1) % cell.Polygon.Length;
                        int neighbor = SharedCell(cell.Cell.Corners[edge], cell.Cell.Corners[next], cell.Cell.Id);
                        bool known = neighbor >= 0 && game.Explored.Contains(neighbor);
                        if (known && game.World.Cells[neighbor].IsLand && game.World.Cells[neighbor].RegionId == region) continue;
                        GraphicsPath path = known ? border : uncertain;
                        path.StartFigure(); path.AddLine(cell.Polygon[edge], cell.Polygon[next]);
                    }
                }
                using (Pen casing = new Pen(Color.FromArgb(190, MapPaper.Paper), 4))
                using (Pen ink = new Pen(Color.FromArgb(220, MapPaper.Russet), 1.7f))
                using (Pen rumor = new Pen(Color.FromArgb(160, MapPaper.Russet), 1.2f) { DashPattern = new[] { 3f, 5f } })
                {
                    if (border.PointCount > 0) { g.DrawPath(casing, border); g.DrawPath(ink, border); }
                    if (uncertain.PointCount > 0) { g.DrawPath(casing, uncertain); g.DrawPath(rumor, uncertain); }
                }
            }
        }

        private void CacheGuidedKnownPath(Game game, int destination)
        {
            if (guidedPathGame == game && guidedPathFrom == game.Player.CellId && guidedPathTo == destination && guidedPathKnownCount == game.Explored.Count) return;
            guidedPathGame = game; guidedPathFrom = game.Player.CellId; guidedPathTo = destination; guidedPathKnownCount = game.Explored.Count;
            guidedKnownPath.Clear();
            int region = game.World.Cells[guidedPathFrom].RegionId;
            Dictionary<int, int> parent = new Dictionary<int, int>(); Queue<int> pending = new Queue<int>();
            parent[guidedPathFrom] = -1; pending.Enqueue(guidedPathFrom);
            int closest = guidedPathFrom;
            double best = Vec3.Dot(game.World.Cells[closest].Center, game.World.Cells[destination].Center);
            while (pending.Count > 0)
            {
                int cell = pending.Dequeue();
                if (cell == destination) { closest = cell; break; }
                foreach (int neighbor in game.World.Cells[cell].Neighbors.OrderBy(n => n))
                {
                    // No hidden terrain is queried to plan the reliable part.
                    if (!game.Explored.Contains(neighbor) || parent.ContainsKey(neighbor)) continue;
                    Cell next = game.World.Cells[neighbor];
                    if (!next.IsLand || next.Terrain == Terrain.Ice || next.RegionId != region && neighbor != destination) continue;
                    parent[neighbor] = cell; pending.Enqueue(neighbor);
                    double near = Vec3.Dot(next.Center, game.World.Cells[destination].Center);
                    if (near > best) { closest = neighbor; best = near; }
                }
            }
            for (int cell = closest; cell >= 0; cell = parent[cell]) guidedKnownPath.Add(cell);
            guidedKnownPath.Reverse();
        }

        private void DrawGuidedTravelPath(Graphics g, Game game, int destination)
        {
            CacheGuidedKnownPath(game, destination);
            using (Pen casing = new Pen(Color.FromArgb(205, MapPaper.Paper), 5.2f) { LineJoin = LineJoin.Round, StartCap = LineCap.Round, EndCap = LineCap.Round })
            using (Pen known = new Pen(Color.FromArgb(230, MapPaper.Russet), 2) { LineJoin = LineJoin.Round, StartCap = LineCap.Round, EndCap = LineCap.Round })
            using (Pen rumor = new Pen(Color.FromArgb(220, MapPaper.Russet), 2) { DashPattern = new[] { 3f, 4f }, StartCap = LineCap.Round, EndCap = LineCap.Round })
            {
                for (int i = 1; i < guidedKnownPath.Count; i++)
                    DrawGuidedTravelArc(g, game.World.Cells[guidedKnownPath[i - 1]].Center, game.World.Cells[guidedKnownPath[i]].Center, casing, known);
                int last = guidedKnownPath.Count == 0 ? game.Player.CellId : guidedKnownPath[guidedKnownPath.Count - 1];
                if (last != destination)
                    DrawGuidedTravelArc(g, game.World.Cells[last].Center, game.World.Cells[destination].Center, casing, rumor);
                PointF start = Project(game.World.Cells[game.Player.CellId].Center);
                g.DrawEllipse(casing, start.X - 13, start.Y - 8, 26, 16);
                g.DrawEllipse(known, start.X - 13, start.Y - 8, 26, 16);
            }
        }

        private void DrawGuidedTravelArc(Graphics g, Vec3 from, Vec3 to, Pen casing, Pen ink)
        {
            List<PointF> points = new List<PointF>();
            for (int i = 0; i <= 24; i++)
            {
                Vec3 place = Vec3.Lerp(from, to, i / 24.0).Normalized();
                if (Vec3.Dot(place, forward) > .025) points.Add(Project(place));
                else if (points.Count > 1) { g.DrawLines(casing, points.ToArray()); g.DrawLines(ink, points.ToArray()); points.Clear(); }
                else points.Clear();
            }
            if (points.Count > 1) { g.DrawLines(casing, points.ToArray()); g.DrawLines(ink, points.ToArray()); }
        }
    }
}
