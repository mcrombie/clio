using System;
using System.Collections.Generic;
using Clio.Simulation;

public static class WorldChecks
{
    private static int checks;
    private static void Require(bool condition, string message)
    {
        checks++; if (!condition) throw new Exception("World: " + message);
    }

    public static int Run()
    {
        checks = 0;
        foreach (int level in new int[] { 0, 1, 2, 3, 4 })
        {
            World world = World.Generate(2026, level);
            int expected = 10 * (int)Math.Pow(4, level) + 2;
            Require(world.Cells.Length == expected, "dual icosphere cell count at subdivision " + level);
            int pentagons = 0, edgesTwice = 0; double area = 0;
            foreach (Cell cell in world.Cells)
            {
                Require(cell.Id >= 0 && cell.Id < expected && Object.ReferenceEquals(world.Cells[cell.Id], cell), "stable contiguous IDs");
                Require(Math.Abs(cell.Center.Length - 1) < 1e-12, "normalized center");
                Require(cell.Neighbors.Length == 5 || cell.Neighbors.Length == 6, "pentagonal/hexagonal valence");
                Require(cell.Corners.Length == cell.Neighbors.Length, "one corner per edge");
                if (cell.Neighbors.Length == 5) pentagons++;
                edgesTwice += cell.Neighbors.Length;
                HashSet<int> unique = new HashSet<int>();
                foreach (int neighbor in cell.Neighbors)
                {
                    Require(neighbor != cell.Id && neighbor >= 0 && neighbor < expected && unique.Add(neighbor), "valid distinct adjacent cell");
                    Require(Array.IndexOf(world.Cells[neighbor].Neighbors, cell.Id) >= 0, "reciprocal adjacency");
                    int common = 0;
                    foreach (Vec3 a in cell.Corners) foreach (Vec3 b in world.Cells[neighbor].Corners)
                        if ((a - b).Length < 1e-12) common++;
                    Require(common == 2, "neighbors share exactly one geometric edge");
                }
                for (int i = 0; i < cell.Corners.Length; i++)
                {
                    Vec3 a = cell.Corners[i], b = cell.Corners[(i + 1) % cell.Corners.Length];
                    Require(Math.Abs(a.Length - 1) < 1e-12, "normalized corner");
                    Require(Vec3.Dot(Vec3.Cross(a - cell.Center, b - cell.Center), cell.Center) > 0, "outward polygon winding");
                }
                double cellArea = world.SphericalArea(cell.Id);
                Require(cellArea > 0 && cellArea < 2, "nondegenerate cell area"); area += cellArea;
            }
            Require(pentagons == 12, "exactly twelve pentagons");
            Require(edgesTwice / 2 == 30 * (int)Math.Pow(4, level), "edge count");
            Require(world.Cells.Length - edgesTwice / 2 + 20 * (int)Math.Pow(4, level) == 2, "Euler characteristic");
            Require(Math.Abs(area - 4 * Math.PI) < 1e-9, "tiles cover sphere without holes or overlap");
            Require(Reachable(world, 0, -1, false) == expected, "globe connected including poles and longitude seam");
            int north = 0, south = 0;
            foreach (Cell cell in world.Cells)
            {
                if (cell.Center.Y > world.Cells[north].Center.Y) north = cell.Id;
                if (cell.Center.Y < world.Cells[south].Center.Y) south = cell.Id;
            }
            Require(Reachable(world, north, -1, false) == expected && Reachable(world, south, -1, false) == expected, "poles are traversable graph vertices");
        }

        foreach (int seed in new int[] { 0, 1, 42, 2026, -500, int.MaxValue })
        {
            World a = World.Generate(seed, 4), b = World.Generate(seed, 4);
            int land = 0, productive = 0, mountains = 0, coast = 0;
            HashSet<Terrain> biomes = new HashSet<Terrain>();
            Dictionary<int, List<int>> regions = new Dictionary<int, List<int>>();
            foreach (Cell c in a.Cells)
            {
                Cell d = b.Cells[c.Id];
                Require(c.Terrain == d.Terrain && c.Elevation == d.Elevation && c.Moisture == d.Moisture && c.Temperature == d.Temperature && c.Forage == d.Forage && c.RegionId == d.RegionId, "same-seed complete climate/region determinism");
                Require(c.Elevation >= -1 && c.Elevation <= 1 && !double.IsNaN(c.Elevation), "normalized finite elevation");
                Require(c.Moisture >= 0 && c.Moisture <= 1 && c.Temperature >= 0 && c.Temperature <= 1 && c.Forage >= 0 && c.Forage <= 1, "normalized climate/productivity");
                Require(c.IsLand == (c.Elevation > 0), "sea level and land agree");
                Require(c.RegionId >= 0, "every cell has natural region");
                biomes.Add(c.Terrain);
                if (c.IsLand) land++;
                if (c.IsLand && c.Forage > 0.5 && c.Temperature > 0.35 && c.Temperature < 0.9 && Reachable(a, c.Id, -1, true) > 150) productive++;
                if (c.Terrain == Terrain.Mountains) mountains++;
                if (c.Terrain == Terrain.Coast)
                {
                    coast++; bool adjacentLand = false;
                    foreach (int n in c.Neighbors) if (a.Cells[n].IsLand) adjacentLand = true;
                    Require(!c.IsLand && adjacentLand, "coast is water adjoining land");
                }
                List<int> members;
                if (!regions.TryGetValue(c.RegionId, out members)) { members = new List<int>(); regions.Add(c.RegionId, members); }
                members.Add(c.Id);
            }
            Require(land > a.Cells.Length * 0.40 && land < a.Cells.Length * 0.44, "land coverage approximately 42 percent");
            Require(productive > 30, "many safe productive starting choices on a substantial mainland");
            Require(biomes.Count >= 7, "varied biomes");
            Require(mountains > 0 && coast > 0, "mountain chains and navigable coasts");
            foreach (KeyValuePair<int, List<int>> region in regions)
            {
                Require(Reachable(a, region.Value[0], region.Key, false) == region.Value.Count, "contiguous natural regions");
                bool isLand = a.Cells[region.Value[0]].IsLand;
                foreach (int id in region.Value) Require(a.Cells[id].IsLand == isLand, "natural region respects shoreline");
            }
        }
        World first = World.Generate(1, 3), second = World.Generate(2, 3); int differences = 0;
        for (int i = 0; i < first.Cells.Length; i++) if (first.Cells[i].Terrain != second.Cells[i].Terrain) differences++;
        Require(differences > first.Cells.Length / 4, "different seeds meaningfully alter geography");
        bool rejects = false; try { World.Generate(0, -1); } catch (ArgumentOutOfRangeException) { rejects = true; }
        Require(rejects, "reject negative subdivisions");
        return checks;
    }

    private static int Reachable(World world, int start, int regionId, bool landOnly)
    {
        HashSet<int> seen = new HashSet<int>(); Queue<int> queue = new Queue<int>();
        queue.Enqueue(start); seen.Add(start);
        while (queue.Count > 0)
        {
            int current = queue.Dequeue();
            foreach (int n in world.Cells[current].Neighbors)
                if (!seen.Contains(n) && (regionId < 0 || world.Cells[n].RegionId == regionId) && (!landOnly || world.Cells[n].IsLand))
                { seen.Add(n); queue.Enqueue(n); }
        }
        return seen.Count;
    }
}
