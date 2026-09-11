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
        private readonly List<long> drawnRiverEdges = new List<long>();

        private void DrawRivers(Graphics g, Game game)
        {
            drawnRiverEdges.Clear();
            if (!game.TerrainTravelEnabled) return;
            foreach (RiverEdge river in TravelRules.KnownRiverEdges(game))
                if (projected.ContainsKey(river.FromCell) && projected.ContainsKey(river.ToCell) &&
                    projected[river.FromCell].Depth >= .035 && projected[river.ToCell].Depth >= .035)
                    drawnRiverEdges.Add(((long)Math.Min(river.FromCell, river.ToCell) << 32) | (uint)Math.Max(river.FromCell, river.ToCell));
            List<Tuple<PointF[], int>> courses = KnownRiverCourses(game);
            if (courses.Count == 0) return;
            GraphicsState state = g.Save();
            using (GraphicsPath known = new GraphicsPath(FillMode.Winding))
            {
                foreach (ProjectedCell cell in Visible) if (game.Explored.Contains(cell.Cell.Id)) known.AddPolygon(cell.Polygon);
                g.SetClip(known, CombineMode.Intersect);
                foreach (Tuple<PointF[], int> course in courses) DrawRiverCourse(g, course.Item1, course.Item2);
            }
            g.Restore(state);
        }

        private List<Tuple<PointF[], int>> KnownRiverCourses(Game game)
        {
            List<Tuple<PointF[], int>> courses = new List<Tuple<PointF[], int>>();
            if (!game.TerrainTravelEnabled) return courses;
            List<RiverEdge> visible = new List<RiverEdge>();
            foreach (RiverEdge river in TravelRules.KnownRiverEdges(game))
            {
                ProjectedCell first, second;
                if (!projected.TryGetValue(river.FromCell, out first) || !projected.TryGetValue(river.ToCell, out second) ||
                    first.Depth < .035 || second.Depth < .035) continue;
                visible.Add(river);
            }
            if (visible.Count == 0) return courses;
            Dictionary<Vec3, List<int>> junctions = new Dictionary<Vec3, List<int>>();
            for (int i = 0; i < visible.Count; i++)
                foreach (Vec3 corner in new[] { visible[i].Start, visible[i].End })
                {
                    List<int> edges;
                    if (!junctions.TryGetValue(corner, out edges)) { edges = new List<int>(); junctions.Add(corner, edges); }
                    edges.Add(i);
                }
            HashSet<int> visited = new HashSet<int>();
            for (int pass = 0; pass < 2; pass++)
                for (int i = 0; i < visible.Count; i++)
                {
                    if (visited.Contains(i)) continue;
                    RiverEdge edge = visible[i];
                    bool firstEnd = junctions[edge.Start].Count != 2, secondEnd = junctions[edge.End].Count != 2;
                    if (pass == 0 && !firstEnd && !secondEnd) continue;
                    Vec3 start = firstEnd || !secondEnd ? edge.Start : edge.End;
                    int flow;
                    PointF[] course = RiverCourse(visible, junctions, visited, i, start, out flow).Select(Project).ToArray();
                    courses.Add(Tuple.Create(course, flow));
                }
            return courses;
        }

        private static List<Vec3> RiverCourse(List<RiverEdge> edges, Dictionary<Vec3, List<int>> junctions, HashSet<int> visited, int index, Vec3 start, out int flow)
        {
            List<Vec3> course = new List<Vec3> { start }; flow = 1;
            while (!visited.Contains(index))
            {
                RiverEdge edge = edges[index]; visited.Add(index); flow = Math.Max(flow, edge.Flow);
                Vec3 next = edge.Start.Equals(start) ? edge.End : edge.Start;
                course.Add(next);
                if (junctions[next].Count != 2) break;
                int nextEdge = junctions[next].FirstOrDefault(i => i != index);
                if (visited.Contains(nextEdge)) break;
                start = next; index = nextEdge;
            }
            return course;
        }

        private static float RiverWidth(PointF[] points, int flowCount)
        {
            if (points.Length < 2) return 1;
            double meanLength = Enumerable.Range(1, points.Length - 1).Average(i => Math.Sqrt(Math.Pow(points[i].X - points[i - 1].X, 2) + Math.Pow(points[i].Y - points[i - 1].Y, 2)));
            float flow = (float)Math.Min(1.6, 1 + Math.Log(Math.Max(1, flowCount)) * .15);
            return Math.Max(1.15f, Math.Min(8.5f, (float)meanLength * .092f * flow));
        }

        private static GraphicsPath RiverPath(PointF[] points)
        {
            GraphicsPath path = new GraphicsPath();
            if (points.Length == 2) path.AddLine(points[0], points[1]);
            else if (points.Length > 2) path.AddCurve(points, .22f);
            return path;
        }

        private static void DrawRiverCourse(Graphics g, PointF[] points, int flowCount)
        {
            if (points.Length < 2) return;
            float width = RiverWidth(points, flowCount);
            using (GraphicsPath path = RiverPath(points))
            {
                // The course still passes through each true shared corner. Low
                // tension softens angular joins without moving it into hex centers.
                DrawRiverBank(g, path, width * 1.6f, Color.FromArgb(50, 174, 190, 156), .55f);
                DrawRiverBank(g, path, width * .76f, Color.FromArgb(178, 235, 225, 199), .22f);
                using (Pen banks = new Pen(Color.FromArgb(193, 72, 92, 87), width + 1.1f) { StartCap = LineCap.Round, EndCap = LineCap.Round }) g.DrawPath(banks, path);
                using (Pen water = new Pen(Color.FromArgb(235, 157, 187, 191), width) { StartCap = LineCap.Round, EndCap = LineCap.Round }) g.DrawPath(water, path);
                using (Pen current = new Pen(Color.FromArgb(104, 227, 233, 219), Math.Max(.5f, width * .22f))) g.DrawPath(current, path);
                if (width > 2.4f)
                    for (int i = 1; i < points.Length; i++)
                    {
                        PointF a = points[i - 1], b = points[i]; float dx = b.X - a.X, dy = b.Y - a.Y;
                        float length = (float)Math.Sqrt(dx * dx + dy * dy); if (length < 3) continue;
                        PointF at = new PointF(a.X + dx * .46f, a.Y + dy * .46f);
                        using (Pen ripple = new Pen(Color.FromArgb(80, 64, 100, 109), .65f))
                        {
                            g.DrawLine(ripple, at.X - dx / length * width * .8f - dy / length * width * .18f, at.Y - dy / length * width * .8f + dx / length * width * .18f,
                                at.X + dx / length * width * .6f - dy / length * width * .18f, at.Y + dy / length * width * .6f + dx / length * width * .18f);
                        }
                    }
            }
        }

        private static void DrawRiverBank(Graphics g, GraphicsPath course, float halfWidth, Color color, float irregularity)
        {
            // The stream follows the real crossing boundary, while its meadow
            // margin widens and narrows like a valley instead of a parallel road.
            using (GraphicsPath flat = (GraphicsPath)course.Clone())
            {
                flat.Flatten(null, .65f); PointF[] knots = flat.PathPoints;
                if (knots.Length < 2) return;
                List<PointF> samples = new List<PointF> { knots[0] };
                for (int j = 1; j < knots.Length; j++)
                {
                    float dx = knots[j].X - knots[j - 1].X, dy = knots[j].Y - knots[j - 1].Y;
                    int steps = Math.Max(1, (int)Math.Ceiling(Math.Sqrt(dx * dx + dy * dy) / 13));
                    for (int k = 1; k <= steps; k++) samples.Add(new PointF(knots[j - 1].X + dx * k / steps, knots[j - 1].Y + dy * k / steps));
                }
                PointF[] points = samples.ToArray();
                PointF[] rim = new PointF[points.Length * 2]; float distance = 0;
                for (int i = 0; i < points.Length; i++)
                {
                    PointF previous = points[Math.Max(0, i - 1)], next = points[Math.Min(points.Length - 1, i + 1)];
                    if (i > 0) { float sx = points[i].X - previous.X, sy = points[i].Y - previous.Y; distance += (float)Math.Sqrt(sx * sx + sy * sy); }
                    float dx = next.X - previous.X, dy = next.Y - previous.Y, length = (float)Math.Sqrt(dx * dx + dy * dy);
                    if (length < .01f) length = 1;
                    float left = halfWidth * (1 + irregularity * ((float)Math.Sin(distance * .037 + .3) * .24f + (float)Math.Cos(distance * .073) * .18f));
                    float rightBank = halfWidth * (1 + irregularity * ((float)Math.Cos(distance * .049 + 1.2) * .25f + (float)Math.Sin(distance * .081) * .17f));
                    rim[i] = new PointF(points[i].X - dy / length * left, points[i].Y + dx / length * left);
                    rim[rim.Length - i - 1] = new PointF(points[i].X + dy / length * rightBank, points[i].Y - dx / length * rightBank);
                }
                using (Brush meadow = new SolidBrush(color)) g.FillPolygon(meadow, rim);
            }
        }
    }
}
