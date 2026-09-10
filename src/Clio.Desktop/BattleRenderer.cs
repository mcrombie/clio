using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using Clio.Simulation;

namespace Clio.Desktop
{
    // Battle geometry is a local projection of campaign cells, never a second grid.
    internal sealed class BattleRenderer : IDisposable
    {
        private readonly TerrainArt terrain = new TerrainArt();
        private readonly Dictionary<int, ProjectedCell> cells = new Dictionary<int, ProjectedCell>();
        private World world;
        private string regionKey;
        private RectangleF bounds;
        private Bitmap ground;
        private Vec3 east, north;
        private float scale, originX, originY;
        internal IEnumerable<ProjectedCell> Cells { get { return cells.Values; } }

        internal void Prepare(Game game, IEnumerable<int> ids, int center, RectangleF area)
        {
            int[] ordered = ids.OrderBy(i => i).ToArray();
            string key = center + ":" + String.Join(",", ordered);
            if (world == game.World && regionKey == key && bounds == area) return;
            world = game.World; regionKey = key; bounds = area; cells.Clear();
            if (ground != null) { ground.Dispose(); ground = null; }
            if (ordered.Length == 0) return;
            Vec3 forward = world.Cells[center].Center.Normalized();
            east = Vec3.Cross(new Vec3(0, 1, 0), forward).Normalized();
            if (east.Length < .1) east = Vec3.Cross(new Vec3(1, 0, 0), forward).Normalized();
            north = Vec3.Cross(forward, east).Normalized();
            PointF[] all = ordered.SelectMany(id => world.Cells[id].Corners).Select(Raw).ToArray();
            float minX = all.Min(p => p.X), maxX = all.Max(p => p.X), minY = all.Min(p => p.Y), maxY = all.Max(p => p.Y);
            scale = Math.Min((area.Width - 100) / Math.Max(.001f, maxX - minX), (area.Height - 100) / Math.Max(.001f, maxY - minY));
            originX = area.X + area.Width / 2 - (minX + maxX) * scale / 2;
            originY = area.Y + area.Height / 2 - (minY + maxY) * scale / 2 + 12;
            foreach (int id in ordered)
            {
                Cell cell = world.Cells[id]; PointF at = Project(cell.Center);
                PointF[] polygon = cell.Corners.Select(Project).ToArray();
                float radius = polygon.Max(p => (float)Math.Sqrt((p.X - at.X) * (p.X - at.X) + (p.Y - at.Y) * (p.Y - at.Y)));
                cells[id] = new ProjectedCell { Cell = cell, Center = at, Polygon = polygon, Radius = radius, Depth = at.Y };
            }
        }

        private PointF Raw(Vec3 p) { return new PointF((float)Vec3.Dot(p, east), (float)-Vec3.Dot(p, north) * .80f); }
        private PointF Project(Vec3 p) { PointF raw = Raw(p); return new PointF(originX + raw.X * scale, originY + raw.Y * scale); }
        internal ProjectedCell Cell(int id) { ProjectedCell result; return cells.TryGetValue(id, out result) ? result : null; }
        internal int Pick(PointF point)
        {
            if (!bounds.Contains(point)) return -1;
            foreach (ProjectedCell cell in cells.Values)
                using (GraphicsPath path = new GraphicsPath()) { path.AddPolygon(cell.Polygon); if (path.IsVisible(point)) return cell.Cell.Id; }
            return -1;
        }

        internal void Draw(Graphics g, Game game)
        {
            // Terrain is stationary throughout an engagement; hovering and
            // selecting formations redraw only the tactical overlays.
            if (ground == null)
            {
                ground = new Bitmap(1600, 960);
                using (Graphics canvas = Graphics.FromImage(ground))
                { canvas.SmoothingMode = SmoothingMode.AntiAlias; DrawGround(canvas, game); }
            }
            g.DrawImage(ground, new RectangleF(0, 0, 1600, 960), new RectangleF(0, 0, 1600, 960), GraphicsUnit.Pixel);
        }

        private void DrawGround(Graphics g, Game game)
        {
            GraphicsState state = g.Save(); g.SetClip(bounds, CombineMode.Intersect);
            try
            {
                foreach (ProjectedCell cell in cells.Values)
                {
                    Color color = GroundColor(cell.Cell);
                    using (Brush brush = new SolidBrush(color)) g.FillPolygon(brush, cell.Polygon);
                    terrain.DrawGround(g, cell, game.World.Seed, "Spring");
                }
                RiverEdge[] rivers = TravelRules.RiverEdges(game).Where(r => cells.ContainsKey(r.FromCell) && cells.ContainsKey(r.ToCell)).ToArray();
                using (GraphicsPath riverLines = new GraphicsPath())
                {
                    foreach (RiverEdge river in rivers)
                    {
                        PointF start = Project(river.Start), end = Project(river.End);
                        riverLines.StartFigure(); riverLines.AddLine(start, end);
                        using (Pen bank = new Pen(Color.FromArgb(190, 191, 193, 141), 7) { StartCap = LineCap.Round, EndCap = LineCap.Round }) g.DrawLine(bank, start, end);
                        using (Pen water = new Pen(Color.FromArgb(88, 159, 175), 4) { StartCap = LineCap.Round, EndCap = LineCap.Round }) g.DrawLine(water, start, end);
                        using (Pen shine = new Pen(Color.FromArgb(145, 180, 220, 224), 1)) g.DrawLine(shine, start, end);
                    }
                    using (Region clearance = new Region())
                    {
                        clearance.MakeEmpty();
                        if (riverLines.PointCount > 0)
                        {
                            using (Pen width = new Pen(Color.Black, 17)) riverLines.Widen(width);
                            clearance.Union(riverLines);
                        }
                        foreach (ProjectedCell cell in cells.Values.OrderBy(c => c.Center.Y))
                            terrain.DrawRelief(g, cell, game.World.Seed, "Spring", true, new TerrainReliefContext { RiverClearance = clearance });
                    }
                }
                foreach (ProjectedCell cell in cells.Values)
                {
                    using (Pen grid = new Pen(Color.FromArgb(84, 225, 226, 193), .8f)) g.DrawPolygon(grid, cell.Polygon);
                    for (int i = 0; i < cell.Polygon.Length; i++)
                    {
                        Vec3 a = cell.Cell.Corners[i], b = cell.Cell.Corners[(i + 1) % cell.Polygon.Length];
                        bool shared = cell.Cell.Neighbors.Any(id => cells.ContainsKey(id) && world.Cells[id].Corners.Contains(a) && world.Cells[id].Corners.Contains(b));
                        if (!shared) using (Pen border = new Pen(Color.FromArgb(220, Art.Gold), 2) { DashPattern = new float[] { 4, 3 } })
                            g.DrawLine(border, cell.Polygon[i], cell.Polygon[(i + 1) % cell.Polygon.Length]);
                    }
                }
            }
            finally { g.Restore(state); }
        }

        internal void Highlight(Graphics g, int id, Color color, int alpha, bool dashed)
        {
            ProjectedCell cell = Cell(id); if (cell == null) return;
            using (Brush fill = new SolidBrush(Color.FromArgb(alpha, color))) g.FillPolygon(fill, cell.Polygon);
            using (Pen edge = new Pen(Color.FromArgb(220, color), 1.8f))
            { if (dashed) edge.DashPattern = new float[] { 3, 3 }; g.DrawPolygon(edge, cell.Polygon); }
        }

        private static Color GroundColor(Cell cell)
        {
            switch (cell.Terrain)
            {
                case Terrain.Forest: return Color.FromArgb(103, 139, 95);
                case Terrain.Grassland: return Color.FromArgb(160, 176, 117);
                case Terrain.Hills: return Color.FromArgb(156, 157, 116);
                case Terrain.Mountains: return Color.FromArgb(136, 150, 137);
                case Terrain.Desert: return Color.FromArgb(194, 171, 116);
                case Terrain.Wetland: return Color.FromArgb(113, 155, 126);
                case Terrain.Tundra: return Color.FromArgb(161, 173, 151);
                case Terrain.Ice: return Color.FromArgb(201, 214, 207);
                default: return Color.FromArgb(61, 116, 133);
            }
        }

        public void Dispose() { if (ground != null) ground.Dispose(); terrain.Dispose(); }
    }
}
