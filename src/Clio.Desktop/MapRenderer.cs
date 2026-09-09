using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Linq;
using System.Runtime.InteropServices;
using Clio.Simulation;

namespace Clio.Desktop
{
    internal sealed class ProjectedCell
    {
        public Cell Cell;
        public PointF[] Polygon;
        public PointF Center;
        public double Depth;
        public float Radius;
    }
    internal sealed partial class MapRenderer : IDisposable
    {
        internal const double RegionalZoom = 7.0, MaximumZoom = 9.0;
        public double Longitude, Latitude, Zoom = RegionalZoom;
        // A closer, gently oblique regional view keeps the land broad enough
        // to read. Planetary views retain the globe's original projection.
        internal double RegionalVerticalScale { get { return 1 - .30 * Math.Max(0, Math.Min(1, (Zoom - 1.8) / 3.2)); } }
        public bool Grid, PlaceLabels, Fog = true;
        public bool IsNavigating;
        public int Layer;
        public int OrderPreviewCell = -1;
        public readonly RectangleF Bounds = new RectangleF(16, 158, 1568, 690);
        public readonly List<ProjectedCell> Visible = new List<ProjectedCell>();
        private readonly List<Tuple<RectangleF, int>> bandTargets = new List<Tuple<RectangleF, int>>();
        private Vec3 right, up, forward;
        private float radius;
        private World cachedWorld;
        private readonly Dictionary<Vec3, List<int>> cornerCells = new Dictionary<Vec3, List<int>>();
        private readonly Dictionary<Vec3, Color> vertexColors = new Dictionary<Vec3, Color>();
        private readonly Dictionary<int, ProjectedCell> projected = new Dictionary<int, ProjectedCell>();
        private readonly TerrainArt terrain = new TerrainArt();
        private readonly Bitmap mist = TerrainArt.CreateMist();
        private Bitmap cachedTerrain;
        private Bitmap backdrop, fogImage;
        private byte[] backdropPixels;
        private string lastTerrainKey;
        public void Focus(Cell cell) { Longitude = Math.Atan2(cell.Center.X, cell.Center.Z); Latitude = Math.Asin(cell.Center.Y); }
        public int PickBand(float x, float y)
        {
            if (!Bounds.Contains(x, y) || (unitClip != null && !unitClip.IsVisible(x, y))) return -1;
            for (int i = bandTargets.Count - 1; i >= 0; i--) if (bandTargets[i].Item1.Contains(x, y)) return bandTargets[i].Item2;
            return -1;
        }
        private PointF Project(Vec3 p)
        { return new PointF(Bounds.X + Bounds.Width * .5f + (float)Vec3.Dot(p, right) * radius, Bounds.Y + Bounds.Height * .51f - (float)(Vec3.Dot(new Vec3(p.X, p.Y * .996647, p.Z), up) * RegionalVerticalScale) * radius); }
        public int Pick(float x, float y)
        {
            if (!Bounds.Contains(x, y)) return -1;
            for (int i = Visible.Count - 1; i >= 0; i--)
            {
                PointF[] p = Visible[i].Polygon; bool inside = false;
                for (int a = 0, b = p.Length - 1; a < p.Length; b = a++)
                    if ((p[a].Y > y) != (p[b].Y > y) && x < (p[b].X - p[a].X) * (y - p[a].Y) / (p[b].Y - p[a].Y) + p[a].X) inside = !inside;
                if (inside) return Visible[i].Cell.Id;
            }
            return -1;
        }
        private bool Known(Game game, int id) { return !Fog || game.Explored.Contains(id); }
        public void InvalidateTerrain() { lastTerrainKey = null; }
        private void BuildGeometry(Game game)
        {
            if (cachedWorld != game.World)
            {
                cachedWorld = game.World; cornerCells.Clear(); lastTerrainKey = null;
                foreach (Cell cell in game.World.Cells) foreach (Vec3 corner in cell.Corners)
                {
                    List<int> owners;
                    if (!cornerCells.TryGetValue(corner, out owners)) { owners = new List<int>(3); cornerCells[corner] = owners; }
                    owners.Add(cell.Id);
                }
            }
            radius = (float)(Bounds.Height * .465 * Zoom);
            forward = new Vec3(Math.Sin(Longitude) * Math.Cos(Latitude), Math.Sin(Latitude), Math.Cos(Longitude) * Math.Cos(Latitude));
            right = new Vec3(Math.Cos(Longitude), 0, -Math.Sin(Longitude)); up = Vec3.Cross(forward, right);
            Visible.Clear(); projected.Clear();
            foreach (Cell cell in game.World.Cells)
            {
                double depth = Vec3.Dot(cell.Center, forward); if (depth < .015) continue;
                PointF center = Project(cell.Center); PointF[] polygon = cell.Corners.Select(Project).ToArray();
                float r = (float)polygon.Average(p => Math.Sqrt((p.X - center.X) * (p.X - center.X) + (p.Y - center.Y) * (p.Y - center.Y)));
                if (center.X + r * 2 < Bounds.Left || center.X - r * 2 > Bounds.Right || center.Y + r * 2 < Bounds.Top || center.Y - r * 2 > Bounds.Bottom) continue;
                ProjectedCell pc = new ProjectedCell { Cell = cell, Polygon = polygon, Center = center, Radius = r, Depth = depth };
                Visible.Add(pc); projected[cell.Id] = pc;
            }
            Visible.Sort((a, b) => a.Depth.CompareTo(b.Depth));
        }
        public void Draw(Graphics g, Game game, int selected)
        {
            BuildGeometry(game);
            GraphicsState state = g.Save(); g.SetClip(Bounds);
            string key = Longitude.ToString("R") + "/" + Latitude.ToString("R") + "/" + Zoom.ToString("R") + "/" + Fog + "/" + Grid + "/" + Layer + "/" + game.Turn + "/" + game.Actions + "/" + game.Explored.Count + "/" + game.Bands.Count + "/" + IsNavigating + "/" + CommandedBandId + "/" + game.TribesEnabled + "/" + game.TerrainTravelEnabled;
            if (cachedTerrain == null || lastTerrainKey != key)
            {
                if (cachedTerrain == null) cachedTerrain = new Bitmap((int)Bounds.Width, (int)Bounds.Height, PixelFormat.Format32bppPArgb);
                using (Graphics bg = Graphics.FromImage(cachedTerrain))
                { bg.SmoothingMode = SmoothingMode.AntiAlias; bg.TranslateTransform(-Bounds.X, -Bounds.Y); DrawLandscape(bg, game); }
                lastTerrainKey = key;
            }
            g.DrawImage(cachedTerrain, Bounds);
            DrawSelection(g, game, selected); DrawOrderPreview(g, game); DrawReunionRoute(g, game); DrawMapInterests(g, game); DrawSaltSources(g, game); DrawLife(g, game, selected); DrawLabels(g, game); DrawCompass(g);
            using (Pen pen = new Pen(Color.FromArgb(125, 79, 99, 99), 1)) g.DrawRectangle(pen, Bounds.X + .5f, Bounds.Y + .5f, Bounds.Width - 1, Bounds.Height - 1);
            g.Restore(state);
        }
        private void DrawLandscape(Graphics g, Game game)
        {
            if (backdrop == null)
            {
                backdrop = new Bitmap((int)Bounds.Width, (int)Bounds.Height, PixelFormat.Format32bppArgb);
                using (Graphics bg = Graphics.FromImage(backdrop))
                {
                    bg.TranslateTransform(-Bounds.X, -Bounds.Y);
                    using (LinearGradientBrush wash = new LinearGradientBrush(Bounds, Color.FromArgb(20, 33, 43), Color.FromArgb(8, 17, 26), 75)) bg.FillRectangle(wash, Bounds);
                    using (TextureBrush clouds = new TextureBrush(mist, WrapMode.Tile)) { clouds.ScaleTransform(2.5f, 2.5f); bg.FillRectangle(clouds, Bounds); }
                    DrawChartLines(bg);
                }
                BitmapData data = backdrop.LockBits(new Rectangle(0, 0, backdrop.Width, backdrop.Height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
                backdropPixels = new byte[data.Stride * data.Height]; Marshal.Copy(data.Scan0, backdropPixels, 0, backdropPixels.Length); backdrop.UnlockBits(data);
            }
            g.DrawImage(backdrop, Bounds); vertexColors.Clear();
            List<Band> knownBands = game.Bands.Where(b => b.Population > 0 && Known(game, b.CellId)).ToList();
            Dictionary<int, Color> cellColors = new Dictionary<int, Color>();
            foreach (ProjectedCell p in Visible) if (Known(game, p.Cell.Id)) cellColors[p.Cell.Id] = Shade(p.Cell, game, knownBands);
            foreach (ProjectedCell p in Visible)
            {
                if (!Known(game, p.Cell.Id)) continue;
                Cell cell = p.Cell; Color color = cellColors[cell.Id]; Color[] edges = new Color[cell.Corners.Length];
                for (int i = 0; i < edges.Length; i++)
                {
                    Vec3 corner = cell.Corners[i]; Color vertex;
                    if (!vertexColors.TryGetValue(corner, out vertex))
                    {
                        int rr = 0, gg = 0, bb = 0, count = 0;
                        foreach (int id in cornerCells[corner])
                        {
                            if (!Known(game, id)) continue;
                            Color neighbor;
                            if (!cellColors.TryGetValue(id, out neighbor)) neighbor = Shade(game.World.Cells[id], game, knownBands);
                            rr += neighbor.R; gg += neighbor.G; bb += neighbor.B; count++;
                        }
                        vertex = count == 0 ? color : Color.FromArgb(rr / count, gg / count, bb / count); vertexColors[corner] = vertex;
                    }
                    edges[i] = vertex;
                }
                using (GraphicsPath path = new GraphicsPath())
                {
                    path.AddPolygon(p.Polygon);
                    if (IsNavigating) { using (Brush wash = new SolidBrush(color)) g.FillPath(wash, path); }
                    else using (PathGradientBrush wash = new PathGradientBrush(path))
                    { wash.CenterPoint = p.Center; wash.CenterColor = color; wash.SurroundColors = edges; wash.FocusScales = new PointF(.1f, .1f); g.FillPath(wash, path); }
                    if (Layer == 0 && !IsNavigating) terrain.DrawGround(g, p, game.World.Seed, game.Season);
                }
            }
            // Repair raster cracks after every polygon has been filled. This is part of the shared
            // ground color field, not a grid outline; later polygons cannot cut these strokes in half.
            foreach (ProjectedCell p in Visible)
            {
                if (!Known(game, p.Cell.Id)) continue;
                for (int i = 0; i < p.Polygon.Length; i++)
                {
                    Vec3 ca = p.Cell.Corners[i], cb = p.Cell.Corners[(i + 1) % p.Polygon.Length];
                    int other = SharedCell(ca, cb, p.Cell.Id);
                    if (IsNavigating || (other >= 0 && other < p.Cell.Id && Known(game, other) && projected.ContainsKey(other))) continue;
                    PointF a = p.Polygon[i], b = p.Polygon[(i + 1) % p.Polygon.Length];
                    if (Math.Abs(a.X - b.X) + Math.Abs(a.Y - b.Y) < .1) continue;
                    using (LinearGradientBrush edge = new LinearGradientBrush(a, b, vertexColors[ca], vertexColors[cb]))
                    using (Pen pen = new Pen(edge, 1.8f) { StartCap = LineCap.Round, EndCap = LineCap.Round }) g.DrawLine(pen, a, b);
                }
            }
            if (Layer == 0)
            {
                GraphicsState landState = g.Save();
                using (GraphicsPath mask = new GraphicsPath(FillMode.Winding))
                {
                    foreach (ProjectedCell p in Visible) if (Known(game, p.Cell.Id)) mask.AddPolygon(p.Polygon);
                    if (mask.PointCount > 0)
                    {
                        g.SetClip(mask, CombineMode.Intersect); DrawShorelines(g, game); DrawRivers(g, game);
                        DrawConnectedRelief(g, game);
                    }
                }
                g.Restore(landState);
            }
            if (Layer != 0) DrawRivers(g, game);
            if (Grid) using (Pen grid = new Pen(Color.FromArgb(83, 233, 226, 187), .8f)) foreach (ProjectedCell p in Visible) if (Known(game, p.Cell.Id)) g.DrawPolygon(grid, p.Polygon);
            if (Fog) DrawMistBoundary(g, game); else DrawAtmosphere(g);
            using (LinearGradientBrush fade = new LinearGradientBrush(new RectangleF(Bounds.X, Bounds.Bottom - 104, Bounds.Width, 105), Color.Transparent, Color.FromArgb(194, 12, 24, 31), 90)) g.FillRectangle(fade, Bounds.X, Bounds.Bottom - 104, Bounds.Width, 104);
        }
        private Color Shade(Cell cell, Game game, List<Band> bands)
        {
            Color color = TerrainColor(cell.Terrain);
            double warm = .5 + .5 * Math.Sin(cell.Center.X * 13 + cell.Center.Z * 7 + game.Seed * .001);
            color = Art.Mix(color, Color.FromArgb(215, 194, 123), warm * .08);
            if (cell.IsLand)
            {
                if (game.Season == "Autumn") color = Art.Mix(color, Color.FromArgb(177, 137, 68), cell.Terrain == Terrain.Forest ? .2 : .1);
                if (game.Season == "Winter" && cell.Temperature < .52) color = Art.Mix(color, Color.FromArgb(198, 213, 207), .22 + (.52 - cell.Temperature) * .6);
                // Low relief is lit from the upper left. Only observed neighbors
                // contribute: an unseen mountain must not cast a revealing shade.
                double slope = 0; int samples = 0;
                Vec3 light = (right * -.65 + up * .75).Normalized();
                foreach (int id in cell.Neighbors)
                {
                    if (!Known(game, id)) continue;
                    Cell neighbor = game.World.Cells[id];
                    Vec3 direction = (neighbor.Center - cell.Center).Normalized();
                    slope += (cell.Elevation - neighbor.Elevation) * Vec3.Dot(direction, light); samples++;
                }
                if (samples > 0 && Layer == 0)
                {
                    double exposure = Math.Max(-.18, Math.Min(.20, slope / samples * 2.4));
                    color = exposure > 0 ? Art.Mix(color, Color.FromArgb(236, 222, 156), exposure) : Art.Mix(color, Color.FromArgb(35, 65, 48), -exposure);
                }
            }
            else if (Layer == 0 && cell.Terrain == Terrain.Ocean)
                color = Art.Mix(color, Color.FromArgb(16, 49, 82), Math.Min(.48, Math.Max(0, -cell.Elevation) * .65));
            Band orderBand = game.Bands.FirstOrDefault(b => b.Id == CommandedBandId && game.CanControlBand(b.Id)) ?? game.TribeLeaderBand ?? game.Player;
            if (Layer == 1 && cell.IsLand) color = Art.Mix(Color.FromArgb(142, 84, 58), Color.FromArgb(141, 182, 106), Math.Min(1, game.ForageYield(cell.Id, orderBand) / 125));
            if (Layer == 2 && cell.IsLand) color = Art.Mix(color, RegionColor(cell.RegionId), game.TerrainTravelEnabled ? .32 : .68);
            if ((Layer == 3 || Layer == 4) && cell.IsLand)
            {
                Band band = bands.OrderByDescending(b => Vec3.Dot(cell.Center, game.World.Cells[b.CellId].Center)).FirstOrDefault();
                double distance = band == null ? 2 : 1 - Vec3.Dot(cell.Center, game.World.Cells[band.CellId].Center);
                double range = Layer == 4 ? .014 : .045;
                Color ink = band == null ? color : Layer == 4 ? IdentityArt.ColorFor(game.TribeOf(band.Id)) : RegionColor(band.LanguageId + 4);
                color = distance < range ? Art.Mix(color, ink, .2 + .5 * (1 - distance / range)) : Art.Mix(color, Color.FromArgb(46, 66, 65), .48);
            }
            double depth = Math.Max(0, Vec3.Dot(cell.Center, forward));
            return Art.Mix(color, Color.FromArgb(18, 40, 50), Math.Pow(1 - depth, 2) * .56);
        }
        private void DrawShorelines(Graphics g, Game game)
        {
            foreach (ProjectedCell p in Visible)
            {
                if (!Known(game, p.Cell.Id) || !p.Cell.IsLand) continue;
                for (int i = 0; i < p.Cell.Corners.Length; i++)
                {
                    Vec3 a = p.Cell.Corners[i], b = p.Cell.Corners[(i + 1) % p.Cell.Corners.Length]; int adjacent = SharedCell(a, b, p.Cell.Id);
                    if (adjacent < 0 || !Known(game, adjacent) || game.World.Cells[adjacent].IsLand) continue;
                    PointF pa = Project(a), pb = Project(b); float dx = pb.X - pa.X, dy = pb.Y - pa.Y, len = (float)Math.Sqrt(dx * dx + dy * dy); if (len < 2) continue;
                    float bend = Math.Min(6, len * .09f) * (float)Math.Sin(p.Cell.Id * 5.3 + i);
                    PointF[] coast = { pa, new PointF(pa.X + dx * .33f - dy / len * bend, pa.Y + dy * .33f + dx / len * bend), new PointF(pa.X + dx * .66f + dy / len * bend * .6f, pa.Y + dy * .66f - dx / len * bend * .6f), pb };
                    using (Pen shelf = new Pen(Color.FromArgb(48, 78, 168, 183), Math.Min(27, len * .42f)) { LineJoin = LineJoin.Round, StartCap = LineCap.Round, EndCap = LineCap.Round }) g.DrawCurve(shelf, coast, .45f);
                    using (Pen shallows = new Pen(Color.FromArgb(78, 126, 197, 195), Math.Min(16, len * .25f)) { LineJoin = LineJoin.Round, StartCap = LineCap.Round, EndCap = LineCap.Round }) g.DrawCurve(shallows, coast, .45f);
                    using (Pen beach = new Pen(Color.FromArgb(134, 220, 209, 159), Math.Min(7, len * .10f)) { LineJoin = LineJoin.Round }) g.DrawCurve(beach, coast, .45f);
                    using (Pen wetSand = new Pen(Color.FromArgb(140, 190, 189, 143), Math.Min(3.8f, len * .055f)) { LineJoin = LineJoin.Round }) g.DrawCurve(wetSand, coast, .45f);
                    using (Pen foam = new Pen(Color.FromArgb(168, 237, 239, 214), .75f) { DashPattern = new[] { 7f, 2f, 3f, 2f } }) g.DrawCurve(foam, coast, .45f);
                }
            }
        }
        private int SharedCell(Vec3 a, Vec3 b, int current)
        { foreach (int id in cornerCells[a]) if (id != current && cornerCells[b].Contains(id)) return id; return -1; }
        private void DrawMistBoundary(Graphics g, Game game)
        {
            const int scale = 4;
            int w = (int)Bounds.Width / scale, h = (int)Bounds.Height / scale;
            byte[] values = new byte[w * h];
            using (Bitmap mask = new Bitmap(w, h, PixelFormat.Format32bppArgb))
            {
                using (Graphics mg = Graphics.FromImage(mask))
                {
                    mg.Clear(Color.Black); mg.SmoothingMode = SmoothingMode.None;
                    mg.Transform = new Matrix(1f / scale, 0, 0, 1f / scale, -Bounds.X / scale, -Bounds.Y / scale);
                    using (Brush brush = new SolidBrush(Color.White))
                    using (Pen edge = new Pen(Color.White, 2))
                        foreach (ProjectedCell p in Visible) if (game.Explored.Contains(p.Cell.Id)) { mg.FillPolygon(brush, p.Polygon); mg.DrawPolygon(edge, p.Polygon); }
                }
                BitmapData data = mask.LockBits(new Rectangle(0, 0, w, h), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
                byte[] raw = new byte[data.Stride * h]; Marshal.Copy(data.Scan0, raw, 0, raw.Length); mask.UnlockBits(data);
                for (int i = 0; i < values.Length; i++) values[i] = raw[i * 4];
            }
            values = Blur(Blur(values, w, h, 3), w, h, 2);
            if (fogImage == null) fogImage = new Bitmap(w, h, PixelFormat.Format32bppArgb);
            byte[] pixels = new byte[w * h * 4];
            for (int y = 0; y < h; y++) for (int x = 0; x < w; x++)
            {
                int to = (y * w + x) * 4, from = ((y * scale + 2) * (int)Bounds.Width + x * scale + 2) * 4;
                pixels[to] = backdropPixels[from]; pixels[to + 1] = backdropPixels[from + 1]; pixels[to + 2] = backdropPixels[from + 2];
                float known = values[y * w + x];
                float opacity = 1 - Math.Max(0, Math.Min(1, (known - 38) / 217));
                pixels[to + 3] = (byte)(255 * opacity);
            }
            BitmapData target = fogImage.LockBits(new Rectangle(0, 0, fogImage.Width, fogImage.Height), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
            Marshal.Copy(pixels, 0, target.Scan0, pixels.Length); fogImage.UnlockBits(target);
            InterpolationMode interpolation = g.InterpolationMode; g.InterpolationMode = InterpolationMode.HighQualityBilinear;
            g.DrawImage(fogImage, Bounds); g.InterpolationMode = interpolation;
        }
        private static byte[] Blur(byte[] source, int w, int h, int r)
        {
            byte[] middle = new byte[source.Length], output = new byte[source.Length]; int diameter = r * 2 + 1;
            for (int y = 0; y < h; y++) for (int x = 0; x < w; x++)
            { int sum = 0; for (int k = -r; k <= r; k++) sum += source[y * w + Math.Max(0, Math.Min(w - 1, x + k))]; middle[y * w + x] = (byte)(sum / diameter); }
            for (int y = 0; y < h; y++) for (int x = 0; x < w; x++)
            { int sum = 0; for (int k = -r; k <= r; k++) sum += middle[Math.Max(0, Math.Min(h - 1, y + k)) * w + x]; output[y * w + x] = (byte)(sum / diameter); }
            return output;
        }
        private void DrawAtmosphere(Graphics g)
        {
            if (Zoom > 1.5) return;
            float cx = Bounds.X + Bounds.Width * .5f, cy = Bounds.Y + Bounds.Height * .51f;
            for (int i = 0; i < 8; i++) using (Pen pen = new Pen(Color.FromArgb(3 + i, 131, 183, 191), 1.5f)) g.DrawEllipse(pen, cx - radius - i, cy - radius * .996647f - i, radius * 2 + i * 2, radius * 1.993294f + i * 2);
        }
        private void DrawChartLines(Graphics g)
        {
            using (Pen pen = new Pen(Color.FromArgb(12, 156, 189, 191), .7f) { DashPattern = new[] { 2f, 7f } })
            {
                for (int y = 178; y < Bounds.Bottom; y += 90) g.DrawLine(pen, Bounds.Left, y, Bounds.Right, y);
                for (int x = 320; x < Bounds.Right; x += 90) g.DrawLine(pen, x, Bounds.Top, x, Bounds.Bottom);
            }
        }
        private void DrawSelection(Graphics g, Game game, int selected)
        {
            Band actor = game.Bands.FirstOrDefault(b => b.Id == CommandedBandId && game.CanControlBand(b.Id));
            if (actor != null && game.ActionsFor(actor.Id) > 0 && !game.IsOver) foreach (int id in game.World.Cells[actor.CellId].Neighbors)
            {
                ProjectedCell pc; if (!projected.TryGetValue(id, out pc) || !game.Explored.Contains(id) || !pc.Cell.IsLand || pc.Depth < .1 || game.TerrainTravelEnabled && TravelRules.MoveCost(game, actor, actor.CellId, id) > game.ActionsFor(actor.Id)) continue;
                using (Pen line = new Pen(Color.FromArgb(87, 216, 230, 185), 1) { DashPattern = new[] { 2f, 5f } }) g.DrawPolygon(line, pc.Polygon);
            }
            ProjectedCell p; if (!projected.TryGetValue(selected, out p) || !Known(game, selected)) return;
            using (Brush glow = new SolidBrush(Color.FromArgb(14, 254, 227, 158))) g.FillPolygon(glow, p.Polygon);
            using (Pen halo = new Pen(Color.FromArgb(45, Art.Gold), 6)) g.DrawPolygon(halo, p.Polygon);
            using (Pen line = new Pen(Color.FromArgb(231, 218, 181, 111), 1.6f)) g.DrawPolygon(line, p.Polygon);
            foreach (PointF point in p.Polygon) using (Brush dot = new SolidBrush(Color.FromArgb(232, 226, 195, 143))) g.FillEllipse(dot, point.X - 1.5f, point.Y - 1.5f, 3, 3);
        }
        private void DrawLabels(Graphics g, Game game)
        {
            if (Layer == 4 || !PlaceLabels) return;
            List<RectangleF> occupied = new List<RectangleF>();
            foreach (Tuple<RectangleF, int> target in animalTargets) { RectangleF box = target.Item1; box.Inflate(8, 8); occupied.Add(box); }
            foreach (Tuple<RectangleF, int> target in bandTargets) { RectangleF box = target.Item1; box.Inflate(6, 6); occupied.Add(box); }
            foreach (Band band in game.Bands.Where(b => b.Population > 0 && Known(game, b.CellId)))
            { ProjectedCell p; if (projected.TryGetValue(band.CellId, out p)) occupied.Add(new RectangleF(p.Center.X - 117, p.Center.Y - 58, 234, 114)); }
            var candidates = Visible.Where(p => Known(game, p.Cell.Id) && p.Depth > .5 && p.Radius > 14);
            // The atlas never coins names on the player's behalf. At close range,
            // label individual remembered hexes rather than one arbitrary region representative.
            var labels = game.CulturalPlaceNames ? candidates.Where(p => game.ObserverKnownPlace(p.Cell.Id) != null && p.Radius > 27)
                .OrderByDescending(p => p.Depth).ThenBy(p => p.Cell.Id).ToList() :
                candidates.Where(p => p.Cell.IsLand).GroupBy(p => p.Cell.RegionId).Select(group => group.OrderByDescending(p => p.Depth).First()).ToList();
            foreach (ProjectedCell p in labels)
            {
                float width = game.CulturalPlaceNames ? Math.Max(92, Math.Min(176, p.Radius * 2.2f)) : 202;
                if (p.Center.X < Bounds.Left + width / 2 + 8 || p.Center.X > Bounds.Right - width / 2 - 8 || p.Center.Y < Bounds.Top + 55 || p.Center.Y > Bounds.Bottom - 80) continue;
                RectangleF bounds = new RectangleF(p.Center.X - width / 2, p.Center.Y + Math.Min(25, p.Radius * .45f), width, 26);
                if (occupied.Any(r => r.IntersectsWith(bounds))) continue;
                occupied.Add(new RectangleF(bounds.X - 9, bounds.Y - 9, bounds.Width + 18, bounds.Height + 18));
                RectangleF shadow = bounds; shadow.Offset(0, 1);
                Art.CenterText(g, game.Place(p.Cell.Id), shadow, Zoom < 1.8 ? 13 : 16, Color.FromArgb(175, 17, 33, 31), true);
                Art.CenterText(g, game.Place(p.Cell.Id), bounds, Zoom < 1.8 ? 13 : 16, Color.FromArgb(228, 228, 217, 179), true);
            }
        }
        private void DrawCompass(Graphics g)
        {
            float x = Bounds.Right - 48, y = Bounds.Top + 91;
            using (Pen pen = new Pen(Color.FromArgb(110, Art.Gold), .8f)) { g.DrawEllipse(pen, x - 22, y - 22, 44, 44); g.DrawEllipse(pen, x - 18, y - 18, 36, 36); }
            using (Brush light = new SolidBrush(Art.Gold)) g.FillPolygon(light, new[] { new PointF(x, y - 31), new PointF(x - 4, y), new PointF(x, y + 6) });
            using (Brush shade = new SolidBrush(Color.FromArgb(123, 151, 151))) g.FillPolygon(shade, new[] { new PointF(x, y - 31), new PointF(x + 4, y), new PointF(x, y + 6) });
            Art.Line(g, Color.FromArgb(120, Art.Gold), .8f, x - 28, y, x + 28, y); Art.Line(g, Color.FromArgb(120, Art.Gold), .8f, x, y, x, y + 27);
            Art.CenterText(g, Math.Cos(Latitude) >= 0 ? "N" : "S", new RectangleF(x - 14, y - 55, 28, 20), 13, Art.Gold, true);
        }
        private static PointF Lerp(PointF a, PointF b, float t) { return new PointF(a.X + (b.X - a.X) * t, a.Y + (b.Y - a.Y) * t); }
        public static Color TerrainColor(Terrain terrain)
        {
            switch (terrain)
            {
                case Terrain.Ocean: return Color.FromArgb(35, 91, 125);
                case Terrain.Coast: return Color.FromArgb(73, 149, 164);
                case Terrain.Grassland: return Color.FromArgb(157, 173, 106);
                case Terrain.Forest: return Color.FromArgb(100, 138, 79);
                case Terrain.Hills: return Color.FromArgb(160, 165, 115);
                case Terrain.Mountains: return Color.FromArgb(148, 153, 134);
                case Terrain.Desert: return Color.FromArgb(194, 165, 114);
                case Terrain.Tundra: return Color.FromArgb(158, 176, 161);
                case Terrain.Ice: return Color.FromArgb(205, 222, 217);
                default: return Color.FromArgb(117, 150, 108);
            }
        }
        public static Color RegionColor(int region)
        {
            Color[] colors = { Color.FromArgb(180, 164, 107), Color.FromArgb(110, 161, 140), Color.FromArgb(189, 127, 102), Color.FromArgb(113, 150, 176), Color.FromArgb(162, 144, 187), Color.FromArgb(185, 174, 111), Color.FromArgb(100, 164, 157) };
            return colors[(int)((uint)region % colors.Length)];
        }
        public void Dispose() { if (cachedTerrain != null) cachedTerrain.Dispose(); if (backdrop != null) backdrop.Dispose(); if (fogImage != null) fogImage.Dispose(); if (unitClip != null) unitClip.Dispose(); mist.Dispose(); terrain.Dispose(); }
    }
}
