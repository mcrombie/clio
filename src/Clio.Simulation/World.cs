using System;
using System.Collections.Generic;

namespace Clio.Simulation
{
    /// <summary>Presentation-independent, double-precision unit-sphere geometry. Y is north.</summary>
    public struct Vec3
    {
        public double X, Y, Z;
        public Vec3(double x, double y, double z) { X = x; Y = y; Z = z; }
        public double Length { get { return Math.Sqrt(X * X + Y * Y + Z * Z); } }
        public Vec3 Normalized() { double l = Length; return l > 1e-15 ? this / l : new Vec3(); }
        public static double Dot(Vec3 a, Vec3 b) { return a.X * b.X + a.Y * b.Y + a.Z * b.Z; }
        public static Vec3 Cross(Vec3 a, Vec3 b) { return new Vec3(a.Y * b.Z - a.Z * b.Y, a.Z * b.X - a.X * b.Z, a.X * b.Y - a.Y * b.X); }
        public static Vec3 Lerp(Vec3 a, Vec3 b, double t) { return a + (b - a) * t; }
        public static Vec3 operator +(Vec3 a, Vec3 b) { return new Vec3(a.X + b.X, a.Y + b.Y, a.Z + b.Z); }
        public static Vec3 operator -(Vec3 a, Vec3 b) { return new Vec3(a.X - b.X, a.Y - b.Y, a.Z - b.Z); }
        public static Vec3 operator -(Vec3 a) { return new Vec3(-a.X, -a.Y, -a.Z); }
        public static Vec3 operator *(Vec3 a, double t) { return new Vec3(a.X * t, a.Y * t, a.Z * t); }
        public static Vec3 operator *(double t, Vec3 a) { return a * t; }
        public static Vec3 operator /(Vec3 a, double t) { return new Vec3(a.X / t, a.Y / t, a.Z / t); }
    }

    public enum Terrain { Ocean, Coast, Grassland, Forest, Hills, Mountains, Desert, Tundra, Ice, Wetland }

    public sealed class Cell
    {
        public int Id;
        public Vec3 Center;
        public Vec3[] Corners;
        public int[] Neighbors;
        public Terrain Terrain;
        /// <summary>Signed normalized elevation; sea level is zero.</summary>
        public double Elevation;
        /// <summary>Ecological moisture, warmth and productivity, each normalized to [0,1].</summary>
        public double Moisture, Temperature, Forage;
        /// <summary>A contiguous natural region. Political claims belong to the simulation.</summary>
        public int RegionId;
        public bool IsLand { get { return Terrain != Terrain.Ocean && Terrain != Terrain.Coast; } }
    }

    /// <summary>A real dual icosphere. Twelve pentagons close an otherwise hexagonal sphere.</summary>
    public sealed class World
    {
        public Cell[] Cells;
        public int Seed;

        private struct Triangle
        {
            public int A, B, C;
            public Triangle(int a, int b, int c) { A = a; B = b; C = c; }
        }

        public static World Generate(int seed, int subdivisions)
        {
            if (subdivisions < 0 || subdivisions > 7)
                throw new ArgumentOutOfRangeException("subdivisions", "Use 0 to 7; 4 gives 2,562 cells.");

            double g = (1 + Math.Sqrt(5)) / 2;
            List<Vec3> vertices = new List<Vec3>(new Vec3[] {
                new Vec3(-1,g,0), new Vec3(1,g,0), new Vec3(-1,-g,0), new Vec3(1,-g,0),
                new Vec3(0,-1,g), new Vec3(0,1,g), new Vec3(0,-1,-g), new Vec3(0,1,-g),
                new Vec3(g,0,-1), new Vec3(g,0,1), new Vec3(-g,0,-1), new Vec3(-g,0,1)
            });
            for (int i = 0; i < vertices.Count; i++) vertices[i] = vertices[i].Normalized();
            int[] faceIndices = {
                0,11,5, 0,5,1, 0,1,7, 0,7,10, 0,10,11,
                1,5,9, 5,11,4, 11,10,2, 10,7,6, 7,1,8,
                3,9,4, 3,4,2, 3,2,6, 3,6,8, 3,8,9,
                4,9,5, 2,4,11, 6,2,10, 8,6,7, 9,8,1
            };
            List<Triangle> faces = new List<Triangle>();
            for (int i = 0; i < faceIndices.Length; i += 3)
                faces.Add(new Triangle(faceIndices[i], faceIndices[i + 1], faceIndices[i + 2]));
            for (int level = 0; level < subdivisions; level++)
            {
                Dictionary<long, int> midpoints = new Dictionary<long, int>();
                List<Triangle> next = new List<Triangle>(faces.Count * 4);
                foreach (Triangle f in faces)
                {
                    int ab = Midpoint(f.A, f.B, vertices, midpoints);
                    int bc = Midpoint(f.B, f.C, vertices, midpoints);
                    int ca = Midpoint(f.C, f.A, vertices, midpoints);
                    next.Add(new Triangle(f.A, ab, ca)); next.Add(new Triangle(f.B, bc, ab));
                    next.Add(new Triangle(f.C, ca, bc)); next.Add(new Triangle(ab, bc, ca));
                }
                faces = next;
            }

            List<Vec3>[] corners = new List<Vec3>[vertices.Count];
            HashSet<int>[] neighbors = new HashSet<int>[vertices.Count];
            for (int i = 0; i < vertices.Count; i++) { corners[i] = new List<Vec3>(6); neighbors[i] = new HashSet<int>(); }
            foreach (Triangle f in faces)
            {
                Vec3 corner = (vertices[f.A] + vertices[f.B] + vertices[f.C]).Normalized();
                corners[f.A].Add(corner); corners[f.B].Add(corner); corners[f.C].Add(corner);
                neighbors[f.A].Add(f.B); neighbors[f.A].Add(f.C);
                neighbors[f.B].Add(f.A); neighbors[f.B].Add(f.C);
                neighbors[f.C].Add(f.A); neighbors[f.C].Add(f.B);
            }
            World world = new World { Seed = seed, Cells = new Cell[vertices.Count] };
            for (int i = 0; i < vertices.Count; i++)
            {
                Vec3 center = vertices[i];
                Vec3 u = Vec3.Cross(Math.Abs(center.Y) < 0.9 ? new Vec3(0, 1, 0) : new Vec3(1, 0, 0), center).Normalized();
                Vec3 v = Vec3.Cross(center, u);
                corners[i].Sort(delegate(Vec3 a, Vec3 b) { return Angle(a, u, v).CompareTo(Angle(b, u, v)); });
                List<int> ordered = new List<int>(neighbors[i]);
                ordered.Sort(delegate(int a, int b) { return Angle(vertices[a], u, v).CompareTo(Angle(vertices[b], u, v)); });
                world.Cells[i] = new Cell { Id = i, Center = center, Corners = corners[i].ToArray(), Neighbors = ordered.ToArray(), RegionId = -1 };
            }
            world.GenerateClimate();
            world.GenerateRegions();
            return world;
        }

        public static World Generate(int seed) { return Generate(seed, 4); }

        /// <summary>Spherical polygon area in steradians. Multiply by radius squared for physical area.</summary>
        public double SphericalArea(int cellId)
        {
            Cell c = Cells[cellId]; double area = 0;
            for (int i = 0; i < c.Corners.Length; i++)
            {
                Vec3 a = c.Center, b = c.Corners[i], d = c.Corners[(i + 1) % c.Corners.Length];
                area += 2 * Math.Atan2(Math.Abs(Vec3.Dot(a, Vec3.Cross(b, d))), 1 + Vec3.Dot(a, b) + Vec3.Dot(b, d) + Vec3.Dot(d, a));
            }
            return area;
        }

        private static double Angle(Vec3 p, Vec3 u, Vec3 v) { return Math.Atan2(Vec3.Dot(p, v), Vec3.Dot(p, u)); }

        private static int Midpoint(int a, int b, List<Vec3> vertices, Dictionary<long, int> cache)
        {
            long key = ((long)Math.Min(a, b) << 32) | (uint)Math.Max(a, b); int id;
            if (cache.TryGetValue(key, out id)) return id;
            id = vertices.Count; vertices.Add((vertices[a] + vertices[b]).Normalized()); cache.Add(key, id); return id;
        }

        private void GenerateClimate()
        {
            double[] raw = new double[Cells.Length];
            double bearing = Hash(Seed, 19, 83, 11) * Math.PI;
            Vec3 cradle = new Vec3(Math.Sin(bearing), 0.24, Math.Cos(bearing)).Normalized();
            Vec3 offset = new Vec3(Hash(Seed, 1, 2, 3) * 30, Hash(Seed, 8, 9, 2) * 30, Hash(Seed, 6, 3, 8) * 30);
            for (int i = 0; i < Cells.Length; i++)
            {
                Vec3 p = Cells[i].Center;
                Vec3 warp = new Vec3(Noise(p * 2 + offset, Seed + 13), Noise(p * 2 + offset, Seed + 37), Noise(p * 2 + offset, Seed + 79)) * 0.32;
                Vec3 q = p + warp;
                double anchor = Math.Pow(Math.Max(0, (Vec3.Dot(p, cradle) - 0.55) / 0.45), 1.5);
                raw[i] = Fractal(q * 1.8 + offset, Seed) * 0.8 + anchor * 0.65;
            }
            double[] sorted = (double[])raw.Clone(); Array.Sort(sorted);
            double seaLevel = sorted[(int)(sorted.Length * 0.58)];
            double maxLand = Math.Max(0.01, sorted[sorted.Length - 1] - seaLevel);
            double maxDepth = Math.Max(0.01, seaLevel - sorted[0]);
            for (int i = 0; i < Cells.Length; i++)
            {
                Cell c = Cells[i]; Vec3 p = c.Center;
                double land = raw[i] - seaLevel;
                // Continental height and narrow, coherent uplift ridges are separate fields.
                double ridge = 1 - Math.Abs(Noise(p * 5.4 + offset, Seed + 119));
                double belt = Smooth(0.47, 0.7, Fractal(p * 2.7 + offset, Seed + 181) + 0.5);
                double uplift = Math.Pow(ridge, 10) * belt * 0.76;
                double height = land > 0 ? Math.Min(1, 0.025 + land / maxLand * 0.4 + uplift) : land / maxDepth;
                // Keep the cradle's lowland basin productive without imposing a rectangular start zone.
                double cradleCore = Smooth(0.90, 0.985, Vec3.Dot(p, cradle));
                if (land > 0) height *= 1 - cradleCore * 0.68;
                c.Elevation = height;
                c.Temperature = Clamp(1 - Math.Pow(Math.Abs(p.Y), 1.25) * 1.10 - Math.Max(0, height) * 0.38 + Noise(p * 4 + offset, Seed + 211) * 0.07);
                double rain = 0.52 + Fractal(p * 3.2 + offset, Seed + 307) * 0.65;
                double dryBelt = Math.Exp(-Math.Pow((Math.Abs(p.Y) - 0.48) / 0.17, 2));
                c.Moisture = Clamp(rain - dryBelt * 0.24 + (1 - Math.Abs(p.Y)) * 0.09 + cradleCore * 0.18);
                if (land <= 0) c.Terrain = Terrain.Ocean;
                else if (c.Temperature < 0.13) c.Terrain = Terrain.Ice;
                else if (height > 0.58) c.Terrain = Terrain.Mountains;
                else if (c.Temperature < 0.29) c.Terrain = Terrain.Tundra;
                else if (height > 0.34) c.Terrain = Terrain.Hills;
                else if (c.Moisture < 0.28 && c.Temperature > 0.50) c.Terrain = Terrain.Desert;
                else if (c.Moisture > 0.68 && height < 0.13) c.Terrain = Terrain.Wetland;
                else if (c.Moisture > 0.48) c.Terrain = Terrain.Forest;
                else c.Terrain = Terrain.Grassland;
            }
            for (int i = 0; i < Cells.Length; i++)
            {
                Cell c = Cells[i];
                if (!c.IsLand)
                {
                    bool shores = false;
                    foreach (int n in c.Neighbors) if (Cells[n].IsLand) { shores = true; break; }
                    if (shores) c.Terrain = Terrain.Coast;
                }
                switch (c.Terrain)
                {
                    case Terrain.Grassland: c.Forage = 0.76; break;
                    case Terrain.Forest: c.Forage = 0.84; break;
                    case Terrain.Wetland: c.Forage = 0.90; break;
                    case Terrain.Hills: c.Forage = 0.54; break;
                    case Terrain.Tundra: c.Forage = 0.26; break;
                    case Terrain.Desert: c.Forage = 0.12; break;
                    case Terrain.Mountains: c.Forage = 0.18; break;
                    case Terrain.Ice: c.Forage = 0.015; break;
                    case Terrain.Coast: c.Forage = 0.48; break;
                    default: c.Forage = 0.06; break;
                }
                c.Forage = Clamp(c.Forage * (0.82 + c.Moisture * 0.27) * (0.75 + c.Temperature * 0.25));
            }
        }

        private void GenerateRegions()
        {
            // Multi-source weighted growth guarantees contiguous regions on each landmass/ocean.
            bool[] visited = new bool[Cells.Length]; int region = 0;
            Queue<int> queue = new Queue<int>();
            double[] distance = new double[Cells.Length];
            for (int i = 0; i < Cells.Length; i++) distance[i] = double.MaxValue;
            for (int start = 0; start < Cells.Length; start++)
            {
                if (visited[start]) continue;
                List<int> component = new List<int>(); queue.Enqueue(start); visited[start] = true;
                while (queue.Count > 0)
                {
                    int current = queue.Dequeue(); component.Add(current);
                    foreach (int n in Cells[current].Neighbors)
                        if (!visited[n] && Cells[n].IsLand == Cells[start].IsLand) { visited[n] = true; queue.Enqueue(n); }
                }
                int count = Math.Max(1, (int)Math.Ceiling(component.Count / 48.0));
                // Farthest-point seeds spread regions evenly over the graph, including polar cells.
                int[] steps = new int[Cells.Length];
                foreach (int id in component) steps[id] = int.MaxValue;
                int next = component[0]; List<int> seeds = new List<int>();
                for (int s = 0; s < count; s++)
                {
                    seeds.Add(next); steps[next] = 0; queue.Enqueue(next);
                    while (queue.Count > 0)
                    {
                        int current = queue.Dequeue();
                        foreach (int n in Cells[current].Neighbors)
                            if (Cells[n].IsLand == Cells[start].IsLand && steps[n] > steps[current] + 1)
                            { steps[n] = steps[current] + 1; queue.Enqueue(n); }
                    }
                    int farthest = -1;
                    foreach (int id in component) if (steps[id] > farthest) { farthest = steps[id]; next = id; }
                }
                MinHeap frontier = new MinHeap();
                foreach (int id in seeds) { distance[id] = 0; Cells[id].RegionId = region++; frontier.Push(id, 0); }
                while (frontier.Count > 0)
                {
                    HeapItem item = frontier.Pop(); int current = item.Id;
                    if (item.Cost > distance[current]) continue;
                    foreach (int n in Cells[current].Neighbors)
                    {
                        if (Cells[n].IsLand != Cells[current].IsLand) continue;
                        double cost = item.Cost + 1 + Math.Abs(Cells[current].Elevation - Cells[n].Elevation) * 3 + (Cells[current].Terrain == Cells[n].Terrain ? 0 : 0.35);
                        if (cost < distance[n]) { distance[n] = cost; Cells[n].RegionId = Cells[current].RegionId; frontier.Push(n, cost); }
                    }
                }
            }
        }

        private struct HeapItem
        {
            public int Id; public double Cost;
            public HeapItem(int id, double cost) { Id = id; Cost = cost; }
        }
        private sealed class MinHeap
        {
            private readonly List<HeapItem> items = new List<HeapItem>();
            public int Count { get { return items.Count; } }
            public void Push(int id, double cost)
            {
                HeapItem item = new HeapItem(id, cost); items.Add(item); int i = items.Count - 1;
                while (i > 0) { int p = (i - 1) / 2; if (items[p].Cost <= cost) break; items[i] = items[p]; i = p; }
                items[i] = item;
            }
            public HeapItem Pop()
            {
                HeapItem root = items[0], tail = items[items.Count - 1]; items.RemoveAt(items.Count - 1);
                if (items.Count == 0) return root;
                int i = 0;
                while (i * 2 + 1 < items.Count)
                {
                    int child = i * 2 + 1;
                    if (child + 1 < items.Count && items[child + 1].Cost < items[child].Cost) child++;
                    if (items[child].Cost >= tail.Cost) break;
                    items[i] = items[child]; i = child;
                }
                items[i] = tail; return root;
            }
        }

        private static double Clamp(double v) { return Math.Max(0, Math.Min(1, v)); }
        private static double Smooth(double a, double b, double x) { double t = Clamp((x - a) / (b - a)); return t * t * (3 - 2 * t); }
        private static double Mix(double a, double b, double t) { return a + (b - a) * t; }
        private static double Fractal(Vec3 p, int seed)
        {
            return Noise(p, seed) * 0.62 + Noise(p * 2.07, unchecked(seed + 31)) * 0.26 + Noise(p * 4.17, unchecked(seed + 73)) * 0.12;
        }
        private static double Noise(Vec3 p, int seed)
        {
            int x = (int)Math.Floor(p.X), y = (int)Math.Floor(p.Y), z = (int)Math.Floor(p.Z);
            double u = Smooth(0, 1, p.X - x), v = Smooth(0, 1, p.Y - y), w = Smooth(0, 1, p.Z - z);
            return Mix(Mix(Mix(Hash(seed, x, y, z), Hash(seed, x + 1, y, z), u), Mix(Hash(seed, x, y + 1, z), Hash(seed, x + 1, y + 1, z), u), v),
                Mix(Mix(Hash(seed, x, y, z + 1), Hash(seed, x + 1, y, z + 1), u), Mix(Hash(seed, x, y + 1, z + 1), Hash(seed, x + 1, y + 1, z + 1), u), v), w);
        }
        private static double Hash(int seed, int x, int y, int z)
        {
            unchecked
            {
                uint h = (uint)seed ^ (uint)x * 374761393u ^ (uint)y * 668265263u ^ (uint)z * 2246822519u;
                h = (h ^ (h >> 13)) * 1274126177u; h ^= h >> 16;
                return (h / (double)uint.MaxValue) * 2 - 1;
            }
        }
    }
}
