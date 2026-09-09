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
                // Periodic noise keeps the cloth-like mist texture seamless when tiled.
                double sx = Math.Cos(x * Math.PI * 2 / 384), sy = Math.Sin(y * Math.PI * 2 / 384);
                double n = Noise(3 + sx * 1.5, 3 + sy * 1.5, 19) * .60 + Noise(8 + sx * 5, 8 + sy * 5, 53) * .28 + Hash(x, y, 31) * .12;
                image.SetPixel(x, y, Color.FromArgb((int)(n * 39), 137, 159, 156));
            }
            return image;
        }
        private static Bitmap MakeTexture(int kind)
        {
            Bitmap image = new Bitmap(192, 192, PixelFormat.Format32bppPArgb); Random rng = new Random(kind * 7159 + 271);
            Terrain terrain = (Terrain)kind;
            Color lightColor = terrain == Terrain.Desert ? Color.FromArgb(249, 220, 161) : terrain == Terrain.Wetland ? Color.FromArgb(177, 191, 131) :
                terrain == Terrain.Tundra || terrain == Terrain.Ice ? Color.FromArgb(217, 224, 209) : Color.FromArgb(202, 205, 140);
            Color darkColor = terrain == Terrain.Forest ? Color.FromArgb(40, 61, 36) : terrain == Terrain.Desert ? Color.FromArgb(122, 86, 51) :
                terrain == Terrain.Wetland ? Color.FromArgb(38, 88, 81) : Color.FromArgb(53, 72, 48);
            using (Graphics g = Graphics.FromImage(image))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                for (int i = 0; i < 330; i++)
                {
                    float x = rng.Next(192), y = rng.Next(192), r = 3 + rng.Next(24);
                    bool light = rng.Next(3) == 0;
                    using (Brush brush = new SolidBrush(Color.FromArgb(5 + rng.Next(20), light ? lightColor : darkColor))) g.FillEllipse(brush, x, y, r * 1.7f, r * .61f);
                }
                for (int i = 0; i < 1550; i++)
                {
                    int alpha = rng.Next(9, 35); Color color = Color.FromArgb(alpha, rng.Next(2) == 0 ? lightColor : darkColor);
                    float x = rng.Next(192), y = rng.Next(192);
                    using (Pen grain = new Pen(color, .7f)) g.DrawLine(grain, x, y, x + rng.Next(1, 4), y - rng.Next(2));
                }
                if (terrain == Terrain.Forest)
                    for (int i = 0; i < 110; i++)
                    {
                        float x = rng.Next(192), y = rng.Next(192);
                        using (Brush litter = new SolidBrush(Color.FromArgb(rng.Next(12, 38), 153, 132, 73))) g.FillEllipse(litter, x, y, 3.5f, 1.5f);
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
                        using (Pen wave = new Pen(Color.FromArgb(15 + random.Next(20), 191, 224, 216), .65f)) g.DrawBezier(wave, at.X - w, at.Y, at.X - w / 2, at.Y - 1.5f, at.X + w / 2, at.Y + 1.5f, at.X + w, at.Y);
                    }
                }
            }
            else if (p.Cell.Terrain == Terrain.Grassland || p.Cell.Terrain == Terrain.Wetland || p.Cell.Terrain == Terrain.Tundra || p.Cell.Terrain == Terrain.Forest)
            {
                if (p.Cell.Terrain == Terrain.Wetland && r > 18) DrawWetland(g, p, random);
                int count = r > 18 ? p.Cell.Terrain == Terrain.Forest ? 19 : 31 : 8;
                using (Pen shade = new Pen(Color.FromArgb(42, 46, 70, 38), 1))
                using (Pen grass = new Pen(Color.FromArgb(88, 191, 198, 127), .75f))
                for (int i = 0; i < count; i++)
                {
                    PointF at = Scatter(p, random, .98f); float length = Math.Min(6.5f, r * (.035f + (float)random.NextDouble() * .035f));
                    g.DrawLine(shade, at.X - length * .7f, at.Y + 1, at.X + length, at.Y + 1);
                    g.DrawLine(grass, at.X, at.Y, at.X - length * .45f, at.Y - length);
                    g.DrawLine(grass, at.X + 1, at.Y, at.X + length * .6f, at.Y - length * .75f);
                }
            }
            else if (p.Cell.Terrain == Terrain.Desert && r > 10)
            {
                for (int i = 0; i < 7; i++)
                {
                    PointF at = Scatter(p, random, .88f); float w = r * (.3f + (float)random.NextDouble() * .25f);
                    using (Pen dark = new Pen(Color.FromArgb(42, 116, 93, 60), 3)) g.DrawBezier(dark, at.X - w, at.Y + 1, at.X - w / 2, at.Y - w / 3, at.X + w / 3, at.Y - w / 4, at.X + w, at.Y + 2);
                    using (Pen light = new Pen(Color.FromArgb(95, 240, 216, 161), .9f)) g.DrawBezier(light, at.X - w, at.Y, at.X - w / 2, at.Y - w / 3, at.X + w / 3, at.Y - w / 4, at.X + w, at.Y + 1);
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
                    using (Pen earth = new Pen(Color.FromArgb(136, 104, 119, 68), Math.Max(2, r * .06f))) g.DrawPath(earth, pool);
                    using (LinearGradientBrush water = new LinearGradientBrush(new RectangleF(at.X - w, at.Y - h, w * 2, h * 2), Color.FromArgb(218, 45, 97, 101), Color.FromArgb(220, 101, 155, 147), 90)) g.FillPath(water, pool);
                    using (Pen bank = new Pen(Color.FromArgb(110, 180, 190, 128), .8f)) g.DrawPath(bank, pool);
                }
                using (Pen reflection = new Pen(Color.FromArgb(105, 193, 217, 187), .8f))
                { g.DrawLine(reflection, at.X - w * .42f, at.Y, at.X + w * .1f, at.Y); g.DrawLine(reflection, at.X - w * .12f, at.Y + h * .37f, at.X + w * .45f, at.Y + h * .37f); }
                using (Pen reed = new Pen(Color.FromArgb(160, 98, 115, 49), .9f))
                using (Pen light = new Pen(Color.FromArgb(150, 189, 180, 98), .7f))
                for (int j = 0; j < 5; j++)
                {
                    float x = at.X - w * .85f + j * w * .35f, y = at.Y + h * (.65f + (float)random.NextDouble() * .45f);
                    float length = r * (.05f + (float)random.NextDouble() * .045f);
                    g.DrawLine(reed, x, y, x - length * .25f, y - length); g.DrawLine(light, x + 1, y, x + length * .3f, y - length * .8f);
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
                // Several uneven stands, meadow openings and occasional older
                // trees give each forest a silhouette instead of an even carpet.
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
                    if (xx * xx + yy * yy < 1 || proximity > .40 && i % 3 != 0 || navigating && i % 4 != 0 || InRiverClearing(context, at, h)) continue;
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
                    // A broad, shared piedmont ties the individual summits to the
                    // same landform. Neighbor saddles are drawn by the renderer.
                    for (int i = 0; i < 3; i++)
                    {
                        float t = i - 1, h = r * (.61f + (float)random.NextDouble() * .15f);
                        PointF at = new PointF(p.Center.X + axis.X * r * t * .43f, p.Center.Y + axis.Y * r * t * .43f + r * .15f);
                        g.DrawImage(hills[(p.Cell.Id + i) % hills.Length], at.X - h, at.Y - h * .61f, h * 2, h);
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
                    if (!navigating || i == 0) ridges.Add(Tuple.Create(at, h, variant));
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
                using (PathGradientBrush earth = new PathGradientBrush(landform))
                { earth.CenterPoint = midpoint; earth.CenterColor = Color.FromArgb(187, 112, 134, 105); earth.SurroundColors = new[] { Color.FromArgb(0, 116, 139, 101) }; g.FillPath(earth, landform); }
            }
            PointF[] crest = { new PointF(first.X, first.Y - width * .35f), new PointF(midpoint.X, midpoint.Y - width * .57f), new PointF(second.X, second.Y - width * .30f) };
            using (Pen shade = new Pen(Color.FromArgb(70, 42, 72, 64), width * .68f) { StartCap = LineCap.Round, EndCap = LineCap.Round }) g.DrawCurve(shade, crest, .35f);
            using (Pen light = new Pen(Color.FromArgb(121, 173, 181, 136), Math.Max(1.2f, width * .13f))) g.DrawCurve(light, crest, .35f);
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
            Bitmap image = new Bitmap(128, 180, PixelFormat.Format32bppPArgb); Random rng = new Random(variant * 7411 + 37);
            using (Graphics g = Graphics.FromImage(image))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                for (int i = 4; i >= 0; i--)
                    using (Brush shadow = new SolidBrush(Color.FromArgb(10 + (4 - i) * 3, 13, 34, 27))) g.FillEllipse(shadow, 42 - i * 2, 145 - i, 72 + i * 3, 15 + i * 2);
                using (Brush contact = new SolidBrush(Color.FromArgb(77, 17, 32, 23))) g.FillEllipse(contact, 49, 153, 30, 8);
                float trunkX = 61 + rng.Next(-3, 4);
                using (Pen bark = new Pen(Color.FromArgb(61, 62, 38), 6) { StartCap = LineCap.Round, EndCap = LineCap.Round }) g.DrawLine(bark, trunkX, 102, trunkX - 2, 155);
                using (Pen wood = new Pen(Color.FromArgb(149, 131, 75), 2.2f)) g.DrawLine(wood, trunkX - 1.5f, 114, trunkX - 3.5f, 154);
                using (Pen root = new Pen(Color.FromArgb(104, 98, 57), 1.5f))
                { g.DrawLine(root, trunkX - 2, 151, trunkX - 12, 157); g.DrawLine(root, trunkX - 2, 151, trunkX + 8, 156); }
                Color baseColor = variant < 8 ? Color.FromArgb(33 + variant * 2, 77 + variant * 3, 54 + variant) :
                    variant < 16 ? Color.FromArgb(64 + (variant - 8) * 3, 103 + (variant - 8) * 3, 48 + (variant - 8) * 2) :
                    Color.FromArgb(140 + (variant - 16) * 7, 108 + (variant - 16) * 3, 40 + (variant - 16));
                if (variant < 8)
                {
                    for (int layer = 0; layer < 4; layer++)
                    {
                        float cy = 127 - layer * 24, width = 42 - layer * 9 + rng.Next(-3, 4), lean = rng.Next(-3, 4);
                        float tipX = trunkX + lean;
                        PointF[] branch = { new PointF(tipX, cy - 43), new PointF(tipX - width * .27f, cy - 23), new PointF(tipX - width * .2f, cy - 24),
                            new PointF(trunkX - width * .69f, cy - 8), new PointF(trunkX - width * .54f, cy - 10), new PointF(trunkX - width, cy + 5),
                            new PointF(trunkX - width * .57f, cy + 3), new PointF(trunkX - width * .47f, cy + 8), new PointF(trunkX - width * .22f, cy + 5),
                            new PointF(trunkX + 3, cy + 11), new PointF(trunkX + width * .39f, cy + 5), new PointF(trunkX + width * .71f, cy + 8),
                            new PointF(trunkX + width, cy + 3), new PointF(trunkX + width * .63f, cy - 12), new PointF(trunkX + width * .73f, cy - 9),
                            new PointF(tipX + width * .22f, cy - 28) };
                        using (LinearGradientBrush canopy = new LinearGradientBrush(new RectangleF(trunkX - width, cy - 45, width * 2, 58),
                            Art.Mix(baseColor, Color.FromArgb(174, 190, 112), .35), Art.Mix(baseColor, Color.FromArgb(17, 45, 39), .35), 24)) g.FillPolygon(canopy, branch);
                        PointF[] shade = { new PointF(tipX, cy - 40), new PointF(trunkX + 5, cy - 9), new PointF(trunkX - 1, cy + 10),
                            new PointF(trunkX + width * .39f, cy + 5), new PointF(trunkX + width * .71f, cy + 8), new PointF(trunkX + width, cy + 3), new PointF(trunkX + width * .25f, cy - 24) };
                        using (Brush side = new SolidBrush(Color.FromArgb(51, 16, 48, 38))) g.FillPolygon(side, shade);
                        using (Pen rim = new Pen(Color.FromArgb(112, 171, 186, 113), 1.1f)) g.DrawLines(rim, branch.Take(6).ToArray());
                        using (Pen needle = new Pen(Color.FromArgb(80, 158, 177, 104), .9f))
                        for (int i = 0; i < 10; i++)
                        {
                            float yy = cy - 23 + rng.Next(25), xx = trunkX - (float)rng.NextDouble() * width * .55f;
                            g.DrawLine(needle, xx, yy, xx - 4 - rng.Next(5), yy + 3);
                        }
                    }
                }
                else
                {
                    using (Pen limb = new Pen(Color.FromArgb(78, 73, 42), 4.5f) { EndCap = LineCap.Round })
                    { g.DrawBezier(limb, trunkX, 136, trunkX - 3, 112, 40, 111, 35, 87); g.DrawBezier(limb, trunkX, 133, trunkX + 4, 114, 82, 111, 91, 86); }
                    using (Pen wood = new Pen(Color.FromArgb(157, 140, 80), 1.6f))
                    { g.DrawBezier(wood, trunkX - 2, 132, trunkX - 5, 116, 40, 108, 38, 95); g.DrawLine(wood, trunkX + 2, 125, 79, 108); }
                    float crownX = trunkX + rng.Next(-6, 7), crownY = 76 + rng.Next(-4, 5);
                    PointF[] contour = new PointF[20];
                    for (int i = 0; i < contour.Length; i++)
                    {
                        double angle = i * Math.PI * 2 / contour.Length;
                        float width = 39 + rng.Next(12), height = 43 + rng.Next(12);
                        contour[i] = new PointF(crownX + (float)Math.Cos(angle) * width, crownY + (float)Math.Sin(angle) * height);
                    }
                    using (GraphicsPath crown = new GraphicsPath())
                    {
                        crown.AddClosedCurve(contour, .64f);
                        using (PathGradientBrush volume = new PathGradientBrush(crown))
                        {
                            volume.CenterPoint = new PointF(crownX - 16, crownY - 20);
                            volume.CenterColor = Art.Mix(baseColor, Color.FromArgb(199, 211, 122), .48);
                            volume.SurroundColors = new[] { Art.Mix(baseColor, Color.FromArgb(21, 54, 37), .37) };
                            volume.FocusScales = new PointF(.13f, .19f); g.FillPath(volume, crown);
                        }
                        GraphicsState leaves = g.Save(); g.SetClip(crown, CombineMode.Intersect);
                        for (int i = 0; i < 22; i++)
                        {
                            float xx = crownX + rng.Next(-39, 36), yy = crownY + rng.Next(-43, 36), size = 17 + rng.Next(18);
                            Color lit = Art.Mix(baseColor, Color.FromArgb(198, 209, 121), .30 + Math.Max(0, (crownY - yy) / 130));
                            using (LinearGradientBrush lobe = new LinearGradientBrush(new RectangleF(xx - size / 2, yy - size / 2, size, size), Color.FromArgb(134, lit), Color.FromArgb(35, 20, 52, 34), 62))
                                g.FillEllipse(lobe, xx - size / 2, yy - size / 2, size, size * .91f);
                        }
                        for (int i = 0; i < 180; i++)
                        {
                            float xx = crownX + rng.Next(-48, 49), yy = crownY + rng.Next(-52, 53);
                            bool lit = rng.Next(3) != 0;
                            using (Brush leaf = new SolidBrush(lit ? Color.FromArgb(30 + rng.Next(63), 199, 211, 128) : Color.FromArgb(45, 19, 48, 33)))
                                g.FillEllipse(leaf, xx, yy, 2 + rng.Next(3), 1.5f + rng.Next(2));
                        }
                        g.Restore(leaves);
                    }
                }
            }
            return image;
        }
        private static Bitmap MakeMountain(int variant)
        {
            Bitmap image = new Bitmap(208, 164, PixelFormat.Format32bppPArgb); Random rng = new Random(variant * 911 + 101);
            using (Graphics g = Graphics.FromImage(image))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                for (int i = 5; i >= 0; i--) using (Brush shadow = new SolidBrush(Color.FromArgb(10, 21, 35, 28))) g.FillEllipse(shadow, 29 - i * 2, 127 - i, 158 + i * 3, 24 + i * 2);
                float peak = 85 + variant % 4 * 8, top = 11 + variant % 3 * 5;
                PointF summit = new PointF(peak, top);
                PointF[] outline = { new PointF(9, 138), new PointF(28, 111), new PointF(40, 112), new PointF(51, 83), new PointF(60, 93),
                    new PointF(peak - 25, 49), new PointF(peak - 16, 60), summit, new PointF(peak + 14, 59), new PointF(peak + 31, 49),
                    new PointF(peak + 45, 87), new PointF(peak + 57, 98), new PointF(183, 121), new PointF(199, 140),
                    new PointF(153, 151), new PointF(101, 155), new PointF(47, 150) };
                using (GraphicsPath massif = new GraphicsPath())
                {
                    massif.AddPolygon(outline);
                    using (LinearGradientBrush rock = new LinearGradientBrush(new RectangleF(10, 10, 190, 145), Color.FromArgb(179, 180, 154), Color.FromArgb(77, 103, 92), 37)) g.FillPath(rock, massif);
                    PointF[] mainRidge = { summit, new PointF(peak - 5, 45), new PointF(peak + 3, 64), new PointF(peak - 6, 82), new PointF(peak + 4, 105), new PointF(peak - 12, 150) };
                    PointF[] rightFace = { summit, new PointF(peak + 14, 59), new PointF(peak + 31, 49), new PointF(peak + 45, 87), new PointF(peak + 57, 98),
                        new PointF(199, 140), new PointF(153, 151), new PointF(peak - 12, 150), new PointF(peak + 4, 105), new PointF(peak - 6, 82), new PointF(peak + 3, 64), new PointF(peak - 5, 45) };
                    using (LinearGradientBrush slope = new LinearGradientBrush(new RectangleF(peak - 12, 10, 128, 146), Color.FromArgb(106, 129, 119), Color.FromArgb(44, 73, 73), 19)) g.FillPolygon(slope, rightFace);
                    using (Brush lit = new SolidBrush(Color.FromArgb(74, 202, 197, 157))) g.FillPolygon(lit, new[] { new PointF(peak - 25, 49), new PointF(peak - 31, 100), new PointF(40, 143), new PointF(51, 83), new PointF(60, 93) });
                    using (Brush spur = new SolidBrush(Color.FromArgb(85, 29, 61, 55))) g.FillPolygon(spur, new[] { new PointF(peak + 31, 49), new PointF(peak + 25, 88), new PointF(peak + 43, 121), new PointF(177, 143), new PointF(peak + 45, 87) });
                    GraphicsState clipping = g.Save(); g.SetClip(massif, CombineMode.Intersect);
                    for (int i = 0; i < 29; i++)
                    {
                        float y = 45 + rng.Next(89), x = peak + rng.Next(-64, 67), length = 12 + rng.Next(26);
                        PointF[] seam = { new PointF(x, y), new PointF(x - 5, y + length * .42f), new PointF(x - 3, y + length * .56f), new PointF(x - 13, y + length) };
                        using (Pen dark = new Pen(Color.FromArgb(29 + rng.Next(36), 24, 48, 42), 1.0f + (float)rng.NextDouble())) g.DrawLines(dark, seam);
                        using (Pen light = new Pen(Color.FromArgb(43, 223, 215, 177), .85f)) g.DrawLine(light, x - 1, y, x - 7, y + length * .5f);
                    }
                    for (int i = 0; i < 190; i++)
                    {
                        float x = rng.Next(15, 199), y = rng.Next(97, 155), size = 1 + (float)rng.NextDouble() * 3;
                        using (Brush scree = new SolidBrush(rng.Next(3) == 0 ? Color.FromArgb(96, 188, 188, 154) : Color.FromArgb(78, 47, 72, 61)))
                            g.FillPolygon(scree, new[] { new PointF(x, y), new PointF(x - size, y + size * .8f), new PointF(x + size, y + size * .7f) });
                    }
                    g.Restore(clipping);
                    using (Pen ridge = new Pen(Color.FromArgb(174, 211, 211, 177), 1.6f)) g.DrawLines(ridge, mainRidge);
                    using (Pen ridge = new Pen(Color.FromArgb(115, 204, 205, 168), 1.1f))
                    { g.DrawLines(ridge, new[] { new PointF(peak - 25, 49), new PointF(peak - 31, 90), new PointF(peak - 45, 123), new PointF(40, 143) }); g.DrawLines(ridge, new[] { new PointF(peak + 31, 49), new PointF(peak + 25, 88), new PointF(peak + 43, 121) }); }
                    if (variant < 4)
                    {
                        PointF[] snow = { summit, new PointF(peak + 11, 49), new PointF(peak + 3, 43), new PointF(peak + 6, 67), new PointF(peak - 4, 57),
                            new PointF(peak - 16, 74), new PointF(peak - 12, 51), new PointF(peak - 25, 61), new PointF(peak - 13, 35) };
                        using (LinearGradientBrush cap = new LinearGradientBrush(new RectangleF(peak - 25, top, 40, 64), Color.FromArgb(241, 239, 217), Color.FromArgb(175, 196, 188), 25)) g.FillPolygon(cap, snow);
                        using (Brush bright = new SolidBrush(Color.FromArgb(242, 242, 220))) g.FillPolygon(bright, new[] { summit, new PointF(peak - 5, 43), new PointF(peak - 15, 58), new PointF(peak - 11, 38) });
                        using (Pen snowline = new Pen(Color.FromArgb(169, 216, 224, 206), 2.3f))
                        { g.DrawLine(snowline, peak - 13, 72, peak - 23, 92); g.DrawLine(snowline, peak + 9, 70, peak + 5, 88); }
                    }
                }
            }
            return image;
        }
        private static Bitmap MakeHill(int variant)
        {
            Bitmap image = new Bitmap(192, 106, PixelFormat.Format32bppPArgb); Random rng = new Random(variant * 977 + 441);
            using (Graphics g = Graphics.FromImage(image))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                for (int i = 4; i >= 0; i--) using (Brush shadow = new SolidBrush(Color.FromArgb(10, 24, 46, 28))) g.FillEllipse(shadow, 19 - i, 71 - i, 155 + i * 2, 24 + i);
                float crest = 65 + variant * 6, top = 17 + variant % 3 * 6;
                PointF[] outline = { new PointF(8, 84), new PointF(33, 56), new PointF(crest - 19, top + 6), new PointF(crest, top), new PointF(crest + 25, top + 13),
                    new PointF(153, 65), new PointF(182, 84), new PointF(139, 97), new PointF(63, 99) };
                using (GraphicsPath hill = new GraphicsPath())
                {
                    hill.AddClosedCurve(outline, .38f);
                    using (LinearGradientBrush earth = new LinearGradientBrush(new RectangleF(5, 13, 179, 86), Color.FromArgb(164, 170, 110), Color.FromArgb(72, 103, 66), 36)) g.FillPath(earth, hill);
                    GraphicsState clipping = g.Save(); g.SetClip(hill, CombineMode.Intersect);
                    PointF[] slope = { new PointF(crest, top), new PointF(crest + 25, top + 13), new PointF(153, 65), new PointF(182, 84), new PointF(131, 95), new PointF(crest - 4, 92), new PointF(crest + 8, 59) };
                    using (LinearGradientBrush shade = new LinearGradientBrush(new RectangleF(crest - 4, top, 119, 89), Color.FromArgb(46, 68, 94, 61), Color.FromArgb(125, 38, 72, 54), 42)) g.FillClosedCurve(shade, slope, FillMode.Winding, .32f);
                    for (int i = 0; i < 115; i++)
                    {
                        float x = rng.Next(15, 181), y = rng.Next(29, 101), length = 2 + rng.Next(5);
                        using (Pen grass = new Pen(Color.FromArgb(rng.Next(18, 75), 214, 208, 143), .8f)) g.DrawLine(grass, x, y, x - length, y + length * .48f);
                    }
                    g.Restore(clipping);
                    using (Pen ridge = new Pen(Color.FromArgb(128, 199, 200, 134), 1.5f)) g.DrawBezier(ridge, crest, top + 1, crest - 5, top + 20, crest + 11, 53, crest - 8, 82);
                }
            }
            return image;
        }
        public void Dispose() { foreach (Bitmap b in trees) b.Dispose(); foreach (Bitmap b in peaks) b.Dispose(); foreach (Bitmap b in hills) b.Dispose(); foreach (Bitmap b in textures) b.Dispose(); }
    }
}
