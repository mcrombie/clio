using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Linq;
using Clio.Simulation;

namespace Clio.Desktop
{
    internal sealed class TerrainReliefContext
    {
        internal PointF[] MountainNeighbors = new PointF[0];
        internal Region RiverClearance;
    }

    // Original procedural illustration. These assets are visual and never touch simulation RNG/state.
    internal sealed class TerrainArt : IDisposable
    {
        private static readonly Color PenInk = Color.FromArgb(91, 76, 55);
        private static readonly Color WaterInk = Color.FromArgb(90, 113, 116);
        private readonly Bitmap[] trees = new Bitmap[24];
        private readonly Bitmap[] peaks = new Bitmap[8];
        private readonly Bitmap[] hills = new Bitmap[8];
        private readonly Bitmap[] textures = new Bitmap[10];
        public TerrainArt()
        {
            for (int i = 0; i < trees.Length; i++) trees[i] = MakeTree(i);
            for (int i = 0; i < peaks.Length; i++) peaks[i] = MakeMountain(i);
            for (int i = 0; i < hills.Length; i++) hills[i] = MakeHill(i);
            for (int i = 0; i < textures.Length; i++) textures[i] = MakeTexture(i);
        }
        private static double Hash(int x, int y, int seed)
        { unchecked { uint n = (uint)(x * 374761393 + y * 668265263 + seed * 1274126177); n = (n ^ (n >> 13)) * 1274126177u; return (n ^ (n >> 16)) / 4294967295.0; } }
        private static double Noise(double x, double y, int seed)
        {
            int xx = (int)Math.Floor(x), yy = (int)Math.Floor(y); double dx = x - xx, dy = y - yy;
            dx = dx * dx * (3 - 2 * dx); dy = dy * dy * (3 - 2 * dy);
            double a = Hash(xx, yy, seed) * (1 - dx) + Hash(xx + 1, yy, seed) * dx;
            double b = Hash(xx, yy + 1, seed) * (1 - dx) + Hash(xx + 1, yy + 1, seed) * dx;
            return a * (1 - dy) + b * dy;
        }
        public static Bitmap CreateMist()
        {
            Bitmap image = new Bitmap(384, 384, PixelFormat.Format32bppPArgb);
            for (int y = 0; y < image.Height; y++) for (int x = 0; x < image.Width; x++)
            {
                // A quiet paper grain, seamless when the unknown land is tiled.
                double sx = Math.Cos(x * Math.PI * 2 / 384), sy = Math.Sin(y * Math.PI * 2 / 384);
                double n = Noise(3 + sx * 1.5, 3 + sy * 1.5, 19) * .60 + Noise(8 + sx * 5, 8 + sy * 5, 53) * .28 + Hash(x, y, 31) * .12;
                image.SetPixel(x, y, Color.FromArgb((int)(n * 22), 157, 133, 93));
            }
            return image;
        }
        private static Bitmap MakeTexture(int kind)
        {
            Bitmap image = new Bitmap(192, 192, PixelFormat.Format32bppPArgb); Random rng = new Random(kind * 7159 + 271);
            Terrain terrain = (Terrain)kind;
            Color lightColor = Color.FromArgb(248, 240, 217);
            Color darkColor = terrain == Terrain.Forest ? Color.FromArgb(118, 119, 83) : terrain == Terrain.Desert ? Color.FromArgb(177, 135, 76) :
                terrain == Terrain.Wetland || terrain == Terrain.Coast || terrain == Terrain.Ocean ? Color.FromArgb(120, 143, 141) :
                terrain == Terrain.Tundra || terrain == Terrain.Ice ? Color.FromArgb(139, 141, 135) : Color.FromArgb(159, 147, 106);
            using (Graphics g = Graphics.FromImage(image))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                for (int i = 0; i < 330; i++)
                {
                    float x = rng.Next(192), y = rng.Next(192), r = 3 + rng.Next(24);
                    bool light = rng.Next(3) == 0;
                    int alpha = 5 + rng.Next(20);
                    if (i % 6 != 0) continue;
                    using (Brush brush = new SolidBrush(Color.FromArgb(alpha / 2 + 2, light ? lightColor : darkColor)))
                        g.FillClosedCurve(brush, new[] { new PointF(x, y + r * .2f), new PointF(x + r * .45f, y),
                            new PointF(x + r * 1.6f, y + r * .16f), new PointF(x + r * 1.45f, y + r * .55f),
                            new PointF(x + r * .33f, y + r * .63f) });
                }
                for (int i = 0; i < 1550; i++)
                {
                    int alpha = rng.Next(9, 35); Color color = rng.Next(2) == 0 ? lightColor : PenInk;
                    float x = rng.Next(192), y = rng.Next(192);
                    float endX = x + rng.Next(1, 4), endY = y - rng.Next(2);
                    if (i % 8 == 0) using (Pen grain = new Pen(Color.FromArgb(alpha / 2, color), .6f)) g.DrawLine(grain, x, y, endX, endY);
                }
                if (terrain == Terrain.Forest)
                    for (int i = 0; i < 110; i++)
                    {
                        float x = rng.Next(192), y = rng.Next(192);
                        int alpha = rng.Next(12, 38);
                        if (i % 9 == 0) using (Pen litter = new Pen(Color.FromArgb(alpha, PenInk), .65f)) g.DrawLine(litter, x, y, x + 2, y + 1);
                    }
            }
            return image;
        }
        public void DrawGround(Graphics g, ProjectedCell p, int seed, string season)
        {
            GraphicsState state = g.Save();
            using (GraphicsPath path = new GraphicsPath()) { path.AddPolygon(p.Polygon); g.SetClip(path, CombineMode.Intersect); }
            float r = p.Radius;
            if (r > 21) g.DrawImage(textures[(int)p.Cell.Terrain], p.Center.X - r * 1.25f, p.Center.Y - r * 1.25f, r * 2.5f, r * 2.5f);
            Random random = new Random(unchecked(seed + p.Cell.Id * 7919));
            if (!p.Cell.IsLand)
            {
                if (r > 21)
                {
                    for (int i = 0; i < 11; i++)
                    {
                        PointF at = Scatter(p, random, .96f); float w = (float)(r * (.12 + random.NextDouble() * .22));
                        int alpha = 15 + random.Next(20);
                        if (i % 3 == 0) using (Pen wave = new Pen(Color.FromArgb(alpha + 34, WaterInk), .7f))
                            g.DrawBezier(wave, at.X - w, at.Y, at.X - w / 2, at.Y - 1.2f, at.X + w / 2, at.Y + 1.2f, at.X + w, at.Y);
                    }
                }
            }
            else if (p.Cell.Terrain == Terrain.Grassland || p.Cell.Terrain == Terrain.Wetland || p.Cell.Terrain == Terrain.Tundra || p.Cell.Terrain == Terrain.Forest)
            {
                if (p.Cell.Terrain == Terrain.Wetland && r > 18) DrawWetland(g, p, random);
                int count = r > 18 ? p.Cell.Terrain == Terrain.Forest ? 19 : 31 : 8;
                using (Pen shade = new Pen(Color.FromArgb(32, PenInk), .65f))
                using (Pen grass = new Pen(Color.FromArgb(90, PenInk), .7f))
                for (int i = 0; i < count; i++)
                {
                    PointF at = Scatter(p, random, .98f); float length = Math.Min(6.5f, r * (.035f + (float)random.NextDouble() * .035f));
                    if (i % 4 != 0) continue;
                    g.DrawBezier(shade, at.X - length, at.Y + 1, at.X, at.Y, at.X, at.Y + 2, at.X + length, at.Y + 1);
                    g.DrawLine(grass, at.X, at.Y, at.X - length * .45f, at.Y - length);
                    g.DrawLine(grass, at.X + 1, at.Y, at.X + length * .6f, at.Y - length * .75f);
                }
            }
            else if (p.Cell.Terrain == Terrain.Desert && r > 10)
            {
                for (int i = 0; i < 7; i++)
                {
                    PointF at = Scatter(p, random, .88f); float w = r * (.3f + (float)random.NextDouble() * .25f);
                    if (i % 2 != 0) continue;
                    using (Pen line = new Pen(Color.FromArgb(85, 132, 98, 58), .85f))
                        g.DrawBezier(line, at.X - w, at.Y, at.X - w / 2, at.Y - w / 3, at.X + w / 3, at.Y - w / 4, at.X + w, at.Y + 1);
                    using (Pen hatch = new Pen(Color.FromArgb(49, PenInk), .65f))
                        for (int mark = 0; mark < 4; mark++)
                        { float xx = at.X + mark * w * .15f; g.DrawLine(hatch, xx, at.Y - w * .15f, xx + w * .15f, at.Y + w * .07f); }
                }
            }
            g.Restore(state);
        }
        private static void DrawWetland(Graphics g, ProjectedCell p, Random random)
        {
            float r = p.Radius;
            for (int i = 0; i < 3; i++)
            {
                PointF at = Scatter(p, random, .69f); float w = r * (.23f + (float)random.NextDouble() * .21f), h = w * .31f;
                PointF[] rim = new PointF[9];
                for (int j = 0; j < rim.Length; j++)
                {
                    double angle = j * Math.PI * 2 / rim.Length;
                    float edge = .76f + (float)random.NextDouble() * .24f;
                    rim[j] = new PointF(at.X + (float)Math.Cos(angle) * w * edge, at.Y + (float)Math.Sin(angle) * h * edge);
                }
                using (GraphicsPath pool = new GraphicsPath())
                {
                    pool.AddClosedCurve(rim, .6f);
                    using (Brush water = new SolidBrush(Color.FromArgb(15, 120, 146, 144))) g.FillPath(water, pool);
                }
                using (Pen ripple = new Pen(Color.FromArgb(95, WaterInk), .7f))
                { g.DrawBezier(ripple, at.X - w * .8f, at.Y, at.X - w * .4f, at.Y - h * .45f, at.X + w * .2f, at.Y + h * .2f, at.X + w * .65f, at.Y);
                    g.DrawLine(ripple, at.X - w * .45f, at.Y + h * .5f, at.X + w * .22f, at.Y + h * .5f); }
                using (Pen reed = new Pen(Color.FromArgb(135, PenInk), .85f))
                using (Pen light = new Pen(Color.FromArgb(88, PenInk), .65f))
                for (int j = 0; j < 5; j++)
                {
                    float x = at.X - w * .85f + j * w * .35f, y = at.Y + h * (.65f + (float)random.NextDouble() * .45f);
                    float length = r * (.05f + (float)random.NextDouble() * .045f);
                    if (j % 2 != 0) continue;
                    g.DrawLine(reed, x, y, x - length * .25f, y - length); g.DrawLine(light, x + 1, y, x + length * .3f, y - length * .8f);
                    g.DrawLine(reed, x - length * .25f, y - length, x - length * .23f, y - length * .69f);
                }
            }
        }
        public void DrawRelief(Graphics g, ProjectedCell p, int seed, string season, bool navigating)
        { DrawRelief(g, p, seed, season, navigating, null); }

        internal void DrawRelief(Graphics g, ProjectedCell p, int seed, string season, bool navigating, TerrainReliefContext context)
        {
            float r = p.Radius; if (r < 7 || !p.Cell.IsLand) return;
            Random random = new Random(unchecked(seed * 11 + p.Cell.Id * 499));
            Terrain type = p.Cell.Terrain;
            if (type == Terrain.Forest)
            {
                // Keep the established placement samples and clearings, drawing
                // just a few pen symbols so the paper remains open between them.
                double woodland = Noise(p.Cell.Center.X * 8 + 13, p.Cell.Center.Z * 8 + 17, seed);
                int count = r > 25 ? 34 + (int)(woodland * 22) : r > 14 ? 24 : 13;
                PointF clearing = Scatter(p, random, .53f);
                float openX = r * (.27f + (float)random.NextDouble() * .19f), openY = r * .28f;
                PointF[] stands = { Scatter(p, random, .76f), Scatter(p, random, .76f), Scatter(p, random, .65f) };
                List<Tuple<PointF, float, int>> grove = new List<Tuple<PointF, float, int>>();
                for (int i = 0; i < count; i++)
                {
                    PointF at = Scatter(p, random, .96f);
                    float h = Math.Min(86, r * (.24f + (float)Math.Pow(random.NextDouble(), 1.35) * .48f));
                    if (i % 17 == 0) h = Math.Min(98, r * .84f);
                    bool evergreen = p.Cell.Temperature < .38 || random.NextDouble() < (p.Cell.Temperature < .64 ? .40 : .12);
                    int index = random.Next(8) + (evergreen ? 0 : 8);
                    if (season == "Autumn" && index >= 8) index += 8;
                    double xx = (at.X - clearing.X) / openX, yy = (at.Y - clearing.Y) / openY;
                    double proximity = stands.Min(s => Math.Pow((at.X - s.X) / r, 2) + Math.Pow((at.Y - s.Y) / r, 2));
                    if (xx * xx + yy * yy < 1 || proximity > .40 && i % 3 != 0 || i % 6 > 1 || navigating && i % 4 != 0 || InRiverClearing(context, at, h)) continue;
                    grove.Add(Tuple.Create(at, h, index));
                }
                DrawFoothills(g, p, context, seed);
                foreach (var tree in grove.OrderBy(t => t.Item1.Y)) DrawTree(g, tree.Item1, tree.Item2, tree.Item3);
            }
            else if (type == Terrain.Mountains || type == Terrain.Hills)
            {
                int count = type == Terrain.Mountains ? 4 : 3;
                List<Tuple<PointF, float, int>> ridges = new List<Tuple<PointF, float, int>>();
                bool snow = p.Cell.Temperature < .44 || p.Cell.Elevation > .76 || season == "Winter" && p.Cell.Temperature < .64;
                PointF axis = MountainAxis(p, context, seed);
                if (type == Terrain.Mountains)
                {
                    // A single contour beneath the summits joins the range.
                    // Neighboring ridge strokes are drawn by the renderer.
                    for (int i = 0; i < 3; i++)
                    {
                        float t = i - 1, h = r * (.61f + (float)random.NextDouble() * .15f);
                        PointF at = new PointF(p.Center.X + axis.X * r * t * .43f, p.Center.Y + axis.Y * r * t * .43f + r * .15f);
                        if (i == 1) g.DrawImage(hills[(p.Cell.Id + i) % hills.Length], at.X - h, at.Y - h * .61f, h * 2, h);
                    }
                }
                for (int i = 0; i < count; i++)
                {
                    float along = (i - (count - 1) * .5f) / Math.Max(1, count - 1);
                    PointF at = type == Terrain.Mountains ? new PointF(p.Center.X + axis.X * r * along * 1.20f + (float)(random.NextDouble() - .5) * r * .12f,
                        p.Center.Y + axis.Y * r * along * 1.20f + (float)(random.NextDouble() - .5) * r * .13f) : Scatter(p, random, .73f);
                    float h = r * (type == Terrain.Mountains ? .85f + (float)random.NextDouble() * .52f : .39f + (float)random.NextDouble() * .36f);
                    if (type == Terrain.Mountains && (i == 0 || i == count - 1)) h *= .74f;
                    int variant = type == Terrain.Mountains ? random.Next(4) + (snow ? 0 : 4) : random.Next(8);
                    if ((!navigating || i == 0) && (type != Terrain.Mountains || i < 3)) ridges.Add(Tuple.Create(at, h * .88f, variant));
                }
                foreach (var ridge in ridges.OrderBy(t => t.Item1.Y))
                    if (type == Terrain.Mountains) g.DrawImage(peaks[ridge.Item3], ridge.Item1.X - ridge.Item2 * .67f, ridge.Item1.Y - ridge.Item2 * .81f, ridge.Item2 * 1.34f, ridge.Item2);
                    else g.DrawImage(hills[ridge.Item3], ridge.Item1.X - ridge.Item2 * .93f, ridge.Item1.Y - ridge.Item2 * .75f, ridge.Item2 * 1.86f, ridge.Item2);
                if (type == Terrain.Hills && r > 20)
                {
                    for (int i = 0; i < (navigating ? 2 : 4); i++) { PointF at = Scatter(p, random, .84f); float h = Math.Min(53, r * .36f); if (!InRiverClearing(context, at, h)) DrawTree(g, at, h, random.Next(8)); }
                }
            }
            else if ((type == Terrain.Grassland || type == Terrain.Wetland) && r > 20)
            {
                int count = type == Terrain.Wetland ? 5 : 2;
                DrawFoothills(g, p, context, seed);
                List<Tuple<PointF, float, int>> grove = new List<Tuple<PointF, float, int>>();
                for (int i = 0; i < count; i++)
                {
                    PointF at = Scatter(p, random, .86f); float h = Math.Min(63, r * (.32f + (float)random.NextDouble() * .21f));
                    int variant = 8 + random.Next(8) + (season == "Autumn" ? 8 : 0);
                    if ((!navigating || i % 2 == 0) && !InRiverClearing(context, at, h)) grove.Add(Tuple.Create(at, h, variant));
                }
                foreach (var tree in grove.OrderBy(t => t.Item1.Y)) DrawTree(g, tree.Item1, tree.Item2, tree.Item3);
            }
        }
        private static bool InRiverClearing(TerrainReliefContext context, PointF at, float height)
        {
            return context != null && context.RiverClearance != null &&
                (context.RiverClearance.IsVisible(at) || context.RiverClearance.IsVisible(at.X, at.Y - height * .39f));
        }

        private static PointF MountainAxis(ProjectedCell p, TerrainReliefContext context, int seed)
        {
            double xx = 0, xy = 0, yy = 0;
            if (context != null)
                foreach (PointF neighbor in context.MountainNeighbors)
                { double dx = neighbor.X - p.Center.X, dy = neighbor.Y - p.Center.Y; xx += dx * dx; xy += dx * dy; yy += dy * dy; }
            double angle = xx + yy > .001 ? .5 * Math.Atan2(2 * xy, xx - yy) : (Hash(p.Cell.Id, 91, seed) - .5) * 1.25;
            return new PointF((float)Math.Cos(angle), (float)Math.Sin(angle));
        }

        private void DrawFoothills(Graphics g, ProjectedCell p, TerrainReliefContext context, int seed)
        {
            if (context == null || p.Radius < 19) return;
            int index = 0;
            foreach (PointF neighbor in context.MountainNeighbors.Take(3))
            {
                PointF at = new PointF(p.Center.X + (neighbor.X - p.Center.X) * .27f, p.Center.Y + (neighbor.Y - p.Center.Y) * .27f);
                float height = p.Radius * (.43f + (float)Hash(p.Cell.Id, index, seed) * .14f);
                if (!InRiverClearing(context, at, height)) g.DrawImage(hills[(p.Cell.Id + index * 3) % hills.Length], at.X - height, at.Y - height * .57f, height * 2, height);
                index++;
            }
        }

        internal static void DrawRangeSaddle(Graphics g, PointF first, PointF second, float width, int seed)
        {
            float dx = second.X - first.X, dy = second.Y - first.Y, length = (float)Math.Sqrt(dx * dx + dy * dy);
            if (length < 2 || width < 2) return;
            PointF midpoint = new PointF((first.X + second.X) * .5f, (first.Y + second.Y) * .5f);
            float nx = -dy / length * width, ny = dx / length * width;
            PointF[] basePoints = { new PointF(first.X - nx, first.Y - ny), new PointF(midpoint.X - nx * 1.13f, midpoint.Y - ny * 1.13f),
                new PointF(second.X - nx * .8f, second.Y - ny * .8f), new PointF(second.X + nx, second.Y + ny),
                new PointF(midpoint.X + nx * 1.07f, midpoint.Y + ny * 1.07f), new PointF(first.X + nx, first.Y + ny) };
            using (GraphicsPath landform = new GraphicsPath())
            {
                landform.AddClosedCurve(basePoints, .34f);
                using (Brush wash = new SolidBrush(Color.FromArgb(10, 149, 126, 87))) g.FillPath(wash, landform);
            }
            PointF[] crest = { new PointF(first.X, first.Y - width * .35f), new PointF(midpoint.X, midpoint.Y - width * .57f), new PointF(second.X, second.Y - width * .30f) };
            using (Pen line = new Pen(Color.FromArgb(79, PenInk), Math.Max(.65f, Math.Min(1.1f, width * .055f)))
                { StartCap = LineCap.Round, EndCap = LineCap.Round }) g.DrawCurve(line, crest, .35f);
            using (Pen hatch = new Pen(Color.FromArgb(54, PenInk), .65f))
                for (int mark = 1; mark < 7; mark++)
                {
                    float t = mark / 7f, taper = (float)(.32 + Hash(mark, 29, seed) * .3);
                    float x = first.X + dx * t, y = first.Y + dy * t - width * .4f;
                    g.DrawLine(hatch, x, y, x + nx * taper + dx * .025f, y + ny * taper + width * .24f);
                }
        }
        private void DrawTree(Graphics g, PointF at, float height, int variant)
        { g.DrawImage(trees[variant], at.X - height * .38f, at.Y - height * .90f, height * .76f, height); }
        private static PointF Scatter(ProjectedCell p, Random random, float extent)
        {
            int edge = random.Next(p.Polygon.Length); PointF a = p.Polygon[edge], b = p.Polygon[(edge + 1) % p.Polygon.Length];
            float t = (float)Math.Sqrt(random.NextDouble()) * extent, mix = (float)random.NextDouble();
            float x = a.X + (b.X - a.X) * mix, y = a.Y + (b.Y - a.Y) * mix;
            return new PointF(p.Center.X + (x - p.Center.X) * t, p.Center.Y + (y - p.Center.Y) * t);
        }
        private static Bitmap MakeTree(int variant)
        {
            Bitmap image = new Bitmap(128, 180, PixelFormat.Format32bppPArgb);
            Random rng = new Random(variant * 7411 + 37);
            using (Graphics g = Graphics.FromImage(image))
            using (Pen outline = new Pen(Color.FromArgb(195, PenInk), 2.4f) { LineJoin = LineJoin.Round, StartCap = LineCap.Round, EndCap = LineCap.Round })
            using (Pen fine = new Pen(Color.FromArgb(125, PenInk), 1.4f) { StartCap = LineCap.Round, EndCap = LineCap.Round })
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                float trunk = 61 + rng.Next(-3, 4);
                Color wash = variant < 8 ? Color.FromArgb(30, 114, 121, 89) : variant < 16 ? Color.FromArgb(24, 132, 133, 94) : Color.FromArgb(27, 160, 126, 77);
                g.DrawBezier(outline, trunk - 2, 105, trunk + 2, 123, trunk - 2, 140, trunk - 2, 156);
                g.DrawLine(fine, trunk + 3, 121, trunk + 2, 154);
                g.DrawLine(fine, trunk - 2, 153, trunk - 10, 158);
                g.DrawLine(fine, trunk + 2, 153, trunk + 10, 157);
                if (variant < 8)
                {
                    float lean = rng.Next(-5, 6), crown = trunk + lean;
                    PointF[] tree = {
                        new PointF(crown, 15), new PointF(crown - 15, 50), new PointF(crown - 7, 46),
                        new PointF(trunk - 26, 79), new PointF(trunk - 15, 74), new PointF(trunk - 38, 108),
                        new PointF(trunk - 24, 103), new PointF(trunk - 46, 134), new PointF(trunk - 16, 137),
                        new PointF(trunk, 132), new PointF(trunk + 21, 138), new PointF(trunk + 44, 133),
                        new PointF(trunk + 25, 106), new PointF(trunk + 36, 112), new PointF(trunk + 17, 77),
                        new PointF(trunk + 27, 84), new PointF(crown + 10, 48), new PointF(crown + 17, 52)
                    };
                    using (Brush tint = new SolidBrush(wash)) g.FillPolygon(tint, tree);
                    g.DrawPolygon(outline, tree);
                    for (int branch = 0; branch < 7; branch++)
                    {
                        float y = 49 + branch * 12, spread = 8 + branch * 4;
                        float jitter = rng.Next(-3, 4);
                        g.DrawLine(fine, trunk + jitter, y, trunk - spread, y + 12);
                        if (branch % 2 == 0) g.DrawLine(fine, trunk + 3, y + 3, trunk + spread * .8f, y + 13);
                    }
                }
                else
                {
                    float dx = rng.Next(-5, 6), dy = rng.Next(-4, 5);
                    PointF[] crown = {
                        new PointF(24 + dx, 112 + dy), new PointF(17 + dx, 95 + dy), new PointF(22 + dx, 82 + dy),
                        new PointF(15 + dx, 70 + dy), new PointF(29 + dx, 53 + dy), new PointF(39 + dx, 55 + dy),
                        new PointF(38 + dx, 36 + dy), new PointF(56 + dx, 30 + dy), new PointF(69 + dx, 38 + dy),
                        new PointF(82 + dx, 30 + dy), new PointF(98 + dx, 43 + dy), new PointF(96 + dx, 58 + dy),
                        new PointF(109 + dx, 65 + dy), new PointF(106 + dx, 82 + dy), new PointF(112 + dx, 96 + dy),
                        new PointF(101 + dx, 115 + dy), new PointF(80 + dx, 119 + dy), new PointF(65 + dx, 112 + dy),
                        new PointF(45 + dx, 122 + dy)
                    };
                    using (GraphicsPath canopy = new GraphicsPath())
                    {
                        canopy.AddClosedCurve(crown, .32f);
                        using (Brush tint = new SolidBrush(wash)) g.FillPath(tint, canopy);
                        g.DrawPath(outline, canopy);
                        GraphicsState clip = g.Save(); g.SetClip(canopy, CombineMode.Intersect);
                        for (int mark = 0; mark < 17; mark++)
                        {
                            float x = 30 + rng.Next(70), y = 62 + rng.Next(53);
                            g.DrawBezier(fine, x, y, x + 3, y - 5, x + 8, y - 4, x + 11, y - 1);
                            if (mark % 3 == 0) g.DrawLine(fine, x + 7, y + 4, x + 15, y + 12);
                        }
                        g.Restore(clip);
                    }
                    g.DrawBezier(fine, trunk, 132, trunk - 2, 114, 42 + dx, 112 + dy, 38 + dx, 99 + dy);
                    g.DrawBezier(fine, trunk + 1, 125, trunk + 4, 112, 82 + dx, 109 + dy, 91 + dx, 94 + dy);
                }
                using (Pen ground = new Pen(Color.FromArgb(82, PenInk), 1.3f))
                {
                    g.DrawBezier(ground, 28, 158, 38, 155, 45, 157, 50, 158);
                    g.DrawBezier(ground, 77, 159, 83, 157, 92, 158, 99, 155);
                }
            }
            return image;
        }

        private static Bitmap MakeMountain(int variant)
        {
            Bitmap image = new Bitmap(208, 164, PixelFormat.Format32bppPArgb);
            Random rng = new Random(variant * 911 + 101);
            using (Graphics g = Graphics.FromImage(image))
            using (Pen outline = new Pen(Color.FromArgb(205, PenInk), 2.3f) { LineJoin = LineJoin.Round, StartCap = LineCap.Round, EndCap = LineCap.Round })
            using (Pen fine = new Pen(Color.FromArgb(143, PenInk), 1.2f) { LineJoin = LineJoin.Round })
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                float peak = 85 + variant % 4 * 8, top = 14 + variant % 3 * 5;
                PointF[] skyline = {
                    new PointF(12, 141), new PointF(33, 116), new PointF(44, 118), new PointF(64, 80),
                    new PointF(peak - 15, 65), new PointF(peak, top), new PointF(peak + 20, 69),
                    new PointF(peak + 30, 59), new PointF(peak + 49, 105), new PointF(170, 117), new PointF(196, 142)
                };
                using (GraphicsPath mountain = new GraphicsPath())
                {
                    mountain.AddLines(skyline); mountain.CloseFigure();
                    using (Brush wash = new SolidBrush(Color.FromArgb(17, 143, 120, 86))) g.FillPath(wash, mountain);
                    GraphicsState clip = g.Save(); g.SetClip(mountain, CombineMode.Intersect);
                    // Slopes are described with parallel pen marks, without
                    // solid faces, cast shadows or directional 3-D lighting.
                    for (int mark = 0; mark < 21; mark++)
                    {
                        float t = .12f + mark * .039f;
                        float y = top + (140 - top) * t;
                        float x = peak + t * 11 + rng.Next(-3, 4);
                        float endX = x + 13 + t * 58, endY = y + 18 + rng.Next(8);
                        if (variant < 4 && y < 53) continue;
                        g.DrawLine(fine, x, y, endX, endY);
                    }
                    for (int mark = 0; mark < 7; mark++)
                    {
                        float x = 38 + rng.Next(126), y = 120 + rng.Next(25);
                        using (Pen broken = new Pen(Color.FromArgb(88, PenInk), 1)) g.DrawLine(broken, x, y, x + 8, y - 3);
                    }
                    g.Restore(clip);
                }
                g.DrawLines(outline, skyline);
                g.DrawLines(fine, new[] { new PointF(peak, top + 2), new PointF(peak - 4, 61),
                    new PointF(peak + 5, 83), new PointF(peak - 7, 105), new PointF(peak - 11, 132) });
                g.DrawLines(fine, new[] { new PointF(peak + 30, 62), new PointF(peak + 27, 97), new PointF(peak + 42, 120) });
                if (variant < 4)
                    g.DrawLines(fine, new[] { new PointF(peak - 14, 61), new PointF(peak - 5, 53),
                        new PointF(peak + 2, 62), new PointF(peak + 9, 52), new PointF(peak + 17, 65) });
                using (Pen ground = new Pen(Color.FromArgb(80, PenInk), 1))
                {
                    g.DrawBezier(ground, 19, 148, 39, 145, 58, 150, 74, 148);
                    g.DrawBezier(ground, 119, 149, 137, 146, 164, 151, 188, 147);
                }
            }
            return image;
        }

        private static Bitmap MakeHill(int variant)
        {
            Bitmap image = new Bitmap(192, 106, PixelFormat.Format32bppPArgb);
            Random rng = new Random(variant * 977 + 441);
            using (Graphics g = Graphics.FromImage(image))
            using (Pen outline = new Pen(Color.FromArgb(145, PenInk), 1.8f) { StartCap = LineCap.Round, EndCap = LineCap.Round })
            using (Pen fine = new Pen(Color.FromArgb(90, PenInk), 1.15f))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                float crest = 65 + variant * 6, top = 20 + variant % 3 * 6;
                using (GraphicsPath hill = new GraphicsPath())
                {
                    hill.AddBezier(10, 85, 32, 71, crest - 32, top, crest, top);
                    hill.AddBezier(crest, top, crest + 35, top - 1, 143, 67, 181, 86);
                    using (GraphicsPath wash = (GraphicsPath)hill.Clone())
                    {
                        wash.CloseFigure();
                        using (Brush tint = new SolidBrush(Color.FromArgb(12, 159, 137, 96))) g.FillPath(tint, wash);
                    }
                    g.DrawPath(outline, hill);
                }
                for (int mark = 0; mark < 8; mark++)
                {
                    float t = mark / 8f, x = crest + 9 + t * 49, y = top + 8 + t * 38;
                    float length = 13 + rng.Next(12);
                    g.DrawBezier(fine, x, y, x + 3, y + 6, x + 11, y + length, x + 18, y + length + 3);
                }
                using (Pen contour = new Pen(Color.FromArgb(64, PenInk), 1))
                    g.DrawBezier(contour, 24, 91, 59, 83, 132, 96, 166, 90);
            }
            return image;
        }
        public void Dispose() { foreach (Bitmap b in trees) b.Dispose(); foreach (Bitmap b in peaks) b.Dispose(); foreach (Bitmap b in hills) b.Dispose(); foreach (Bitmap b in textures) b.Dispose(); }
    }
}
