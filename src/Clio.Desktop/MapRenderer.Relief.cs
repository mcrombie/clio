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
        private readonly List<long> reliefConnections = new List<long>();

        private void DrawConnectedRelief(Graphics g, Game game)
        {
            reliefConnections.Clear();
            ProjectedCell[] cells = Visible.Where(p => Known(game, p.Cell.Id) && p.Depth > .09 && p.Cell.IsLand).ToArray();
            using (Region riverClearance = KnownRiverClearance(game))
            {
                // Connections are inferred only between two observed mountains.
                // A peak beyond fog must never create a shoulder in known land.
                foreach (ProjectedCell cell in cells)
                {
                    if (!game.Explored.Contains(cell.Cell.Id) || cell.Cell.Terrain != Terrain.Mountains || cell.Radius < 13) continue;
                    foreach (int neighborId in cell.Cell.Neighbors)
                    {
                        if (neighborId < cell.Cell.Id || !game.Explored.Contains(neighborId)) continue;
                        ProjectedCell neighbor;
                        if (!projected.TryGetValue(neighborId, out neighbor) || neighbor.Depth <= .09 || neighbor.Cell.Terrain != Terrain.Mountains) continue;
                        if (!TravelRules.HasRiver(game, cell.Cell.Id, neighborId))
                            TerrainArt.DrawRangeSaddle(g, cell.Center, neighbor.Center, Math.Min(cell.Radius, neighbor.Radius) * .40f, game.Seed + cell.Cell.Id);
                        reliefConnections.Add(((long)cell.Cell.Id << 32) | (uint)neighborId);
                    }
                }
                foreach (ProjectedCell cell in cells.OrderBy(p => p.Center.Y))
                {
                    List<PointF> mountainNeighbors = new List<PointF>();
                    if (game.Explored.Contains(cell.Cell.Id))
                        foreach (int neighborId in cell.Cell.Neighbors)
                        {
                            if (!game.Explored.Contains(neighborId)) continue;
                            ProjectedCell neighbor;
                            if (projected.TryGetValue(neighborId, out neighbor) && neighbor.Depth > .09 && neighbor.Cell.Terrain == Terrain.Mountains)
                                mountainNeighbors.Add(neighbor.Center);
                        }
                    TerrainReliefContext context = new TerrainReliefContext { MountainNeighbors = mountainNeighbors.ToArray(), RiverClearance = riverClearance };
                    terrain.DrawRelief(g, cell, game.World.Seed, game.Season, IsNavigating, context);
                }
            }
        }

        private Region KnownRiverClearance(Game game)
        {
            Region corridor = new Region(); corridor.MakeEmpty();
            if (!game.TerrainTravelEnabled) return corridor;
            foreach (Tuple<PointF[], int> course in KnownRiverCourses(game))
                using (GraphicsPath path = RiverPath(course.Item1))
                using (Pen width = new Pen(Color.Black, RiverWidth(course.Item1, course.Item2) * 3.0f + 5) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round })
                {
                    path.Widen(width); corridor.Union(path);
                }
            return corridor;
        }
    }
}
