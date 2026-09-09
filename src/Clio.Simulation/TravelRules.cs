using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace Clio.Simulation
{
    public sealed class RiverEdge
    {
        // The river runs along this shared boundary. Crossing between these
        // neighboring cells crosses the river; the endpoints are map corners.
        public readonly int FromCell, ToCell, Flow;
        public readonly Vec3 Start, End;
        internal RiverEdge(int from, int to, Vec3 start, Vec3 end, int flow)
        { FromCell = from; ToCell = to; Start = start; End = end; Flow = flow; }
    }

    public static class TravelRules
    {
        public static int MoveCost(Game game, Band band, int from, int to)
        {
            if (game == null || band == null || from < 0 || to < 0 || from >= game.World.Cells.Length || to >= game.World.Cells.Length ||
                !game.World.Cells[from].IsLand || !game.World.Cells[to].IsLand || !game.World.Cells[from].Neighbors.Contains(to)) return 0;
            return game.TerrainTravelEnabled && (game.World.Cells[to].Terrain == Terrain.Mountains || HasRiver(game, from, to)) ? 2 : 1;
        }
        public static int EncounterCost(Game game, Band band, int target)
        { return game == null || band == null || target < 0 || target >= game.World.Cells.Length ? 0 : band.CellId == target ? 1 : MoveCost(game, band, band.CellId, target); }
        public static bool HasRiver(Game game, int first, int second)
        { return game != null && game.HasTravelRiver(first, second); }
        public static ReadOnlyCollection<RiverEdge> RiverEdges(Game game)
        { return game == null ? new List<RiverEdge>().AsReadOnly() : game.ReadTravelRivers(); }
        public static IEnumerable<RiverEdge> KnownRiverEdges(Game game)
        { return game == null ? new RiverEdge[0] : RiverEdges(game).Where(e => game.Explored.Contains(e.FromCell) && game.Explored.Contains(e.ToCell)).ToArray(); }

        // A two-cost edge must not look as short as an ordinary step. This is
        // intentionally supplied with the observer's remembered cells only.
        internal static int KnownStep(Game game, Band band, IEnumerable<int> remembered, Func<int, bool> destination)
        {
            HashSet<int> known = new HashSet<int>(remembered), open = new HashSet<int> { band.CellId };
            Dictionary<int, int> distance = new Dictionary<int, int> { { band.CellId, 0 } }, first = new Dictionary<int, int> { { band.CellId, band.CellId } };
            HashSet<int> closed = new HashSet<int>();
            while (open.Count > 0)
            {
                int cell = open.OrderBy(id => distance[id]).ThenBy(id => id).First(); open.Remove(cell); closed.Add(cell);
                if (destination(cell) && (cell == band.CellId || !EncounterRules.HostileAt(game, cell, band.Id))) return first[cell];
                foreach (int next in game.World.Cells[cell].Neighbors.OrderBy(id => id))
                {
                    if (!known.Contains(next) || closed.Contains(next) || !game.World.Cells[next].IsLand || game.World.Cells[next].Terrain == Terrain.Ice || EncounterRules.HostileAt(game, next, band.Id)) continue;
                    int cost = MoveCost(game, band, cell, next); if (cost <= 0) continue;
                    int candidate = distance[cell] + cost, previous;
                    if (!distance.TryGetValue(next, out previous) || candidate < previous)
                    { distance[next] = candidate; first[next] = cell == band.CellId ? next : first[cell]; open.Add(next); }
                }
            }
            return -1;
        }
    }

    internal sealed class TravelCorner
    {
        internal Vec3 Position;
        internal double Elevation, Moisture;
        internal bool Sea, Upland;
        internal readonly HashSet<int> Cells = new HashSet<int>();
        internal readonly List<TravelBoundary> Edges = new List<TravelBoundary>();
    }
    internal sealed class TravelBoundary
    {
        internal int FirstCell, SecondCell, FirstCorner, SecondCorner, Flow;
        internal int Other(int corner) { return corner == FirstCorner ? SecondCorner : FirstCorner; }
    }

    public sealed partial class Game
    {
        private bool travelEnabled;
        private List<RiverEdge> travelRivers;
        private HashSet<long> travelRiverPairs;
        private Dictionary<int, HashSet<int>> travelExplored;
        public bool TerrainTravelEnabled { get { return travelEnabled; } }
        private static long TravelPair(int first, int second) { return ((long)Math.Min(first, second) << 32) | (uint)Math.Max(first, second); }
        internal bool HasTravelRiver(int first, int second)
        { return travelEnabled && first >= 0 && second >= 0 && travelRiverPairs.Contains(TravelPair(first, second)); }
        internal ReadOnlyCollection<RiverEdge> ReadTravelRivers()
        { return (travelRivers ?? new List<RiverEdge>()).AsReadOnly(); }
        public string EnableTerrainTravel()
        {
            if (TerrainTravelEnabled) return "Mountains and river crossings already shape travel.";
            InitializeTerrainTravel(); return "Mountains and river crossings now take two actions to enter. All mountain routes remain passable.";
        }
        private void InitializeTerrainTravel()
        {
            travelEnabled = true; travelRivers = new List<RiverEdge>(); travelRiverPairs = new HashSet<long>(); travelExplored = new Dictionary<int, HashSet<int>>();
            List<TravelCorner> corners = new List<TravelCorner>(); Dictionary<Vec3, int> index = new Dictionary<Vec3, int>();
            foreach (Cell cell in World.Cells)
            foreach (Vec3 position in cell.Corners)
            {
                int id; if (!index.TryGetValue(position, out id)) { id = corners.Count; index.Add(position, id); corners.Add(new TravelCorner { Position = position }); }
                corners[id].Cells.Add(cell.Id);
            }
            foreach (TravelCorner corner in corners)
            {
                Cell[] around = corner.Cells.Select(id => World.Cells[id]).ToArray();
                corner.Elevation = around.Average(c => c.Elevation); corner.Moisture = around.Average(c => c.Moisture);
                corner.Sea = around.Any(c => !c.IsLand); corner.Upland = around.Any(c => c.Terrain == Terrain.Mountains || c.Terrain == Terrain.Hills);
            }
            List<TravelBoundary> boundaries = new List<TravelBoundary>();
            foreach (Cell cell in World.Cells)
            foreach (int neighbor in cell.Neighbors.Where(n => n > cell.Id))
            {
                int[] shared = cell.Corners.Where(p => World.Cells[neighbor].Corners.Contains(p)).Select(p => index[p]).ToArray();
                if (shared.Length != 2) continue;
                TravelBoundary edge = new TravelBoundary { FirstCell = cell.Id, SecondCell = neighbor, FirstCorner = shared[0], SecondCorner = shared[1] };
                boundaries.Add(edge); corners[shared[0]].Edges.Add(edge); corners[shared[1]].Edges.Add(edge);
            }
            HashSet<int> used = new HashSet<int>(); int sources = 0, limit = Math.Max(8, World.Cells.Length / 85);
            foreach (int source in Enumerable.Range(0, corners.Count).Where(id => corners[id].Upland && !corners[id].Sea && corners[id].Moisture >= .35)
                .OrderBy(id => TravelHash(Seed, id)).ThenBy(id => id))
            {
                if (sources >= limit) break;
                if (used.Contains(source) || corners[source].Edges.Any(e => used.Contains(e.Other(source)))) continue;
                int current = source; bool flowed = false;
                for (int step = 0; step < 120 && !corners[current].Sea; step++)
                {
                    TravelBoundary next = corners[current].Edges.Where(e => corners[e.Other(current)].Elevation < corners[current].Elevation - 1e-10)
                        .OrderBy(e => corners[e.Other(current)].Elevation).ThenBy(e => e.Other(current)).FirstOrDefault();
                    if (next == null) break;
                    next.Flow++; flowed = true; used.Add(current); current = next.Other(current);
                }
                if (flowed) { sources++; used.Add(current); }
            }
            foreach (TravelBoundary edge in boundaries.Where(e => e.Flow > 0).OrderBy(e => e.FirstCell).ThenBy(e => e.SecondCell))
            {
                travelRivers.Add(new RiverEdge(edge.FirstCell, edge.SecondCell, corners[edge.FirstCorner].Position, corners[edge.SecondCorner].Position, edge.Flow));
                travelRiverPairs.Add(TravelPair(edge.FirstCell, edge.SecondCell));
            }
            foreach (Band band in Bands.Where(b => b.Population > 0)) ObserveTravelPlaces(band);
        }
        private static uint TravelHash(int seed, int id)
        { unchecked { uint value = (uint)seed ^ (uint)id * 0x9e3779b9u ^ 0x729ab43du; value ^= value >> 16; value *= 0x7feb352du; value ^= value >> 15; value *= 0x846ca68bu; return value ^ (value >> 16); } }
        private void ObserveTravelPlaces(Band band)
        {
            if (!TerrainTravelEnabled || band == null || band.Population <= 0) return;
            HashSet<int> known; if (!travelExplored.TryGetValue(band.Id, out known)) { known = new HashSet<int>(); travelExplored.Add(band.Id, known); }
            known.Add(band.CellId);
            foreach (int n in World.Cells[band.CellId].Neighbors) { known.Add(n); foreach (int next in World.Cells[n].Neighbors) known.Add(next); }
        }
        private bool TravelPlaceKnown(Band band, int cell)
        {
            if (IsPlayerTribe(band.Id)) return Explored.Contains(cell);
            if (SaltEnabled) return SaltPlaceKnown(band, cell);
            HashSet<int> known; return travelExplored != null && travelExplored.TryGetValue(band.Id, out known) && known.Contains(cell);
        }
    }
}
