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
        public bool InterestHighlights = true;
        public int HoverOpportunityKind = -1, HoverOpportunityCell = -1;
        internal const int FoodOpportunity = 4, SaltOpportunity = 5, ExplorationOpportunity = 6;
        private sealed class OpportunityTarget
        {
            public int Kind, CellId;
            public RectangleF Bounds;
            public PointF[] Polygon;
        }
        private readonly List<OpportunityTarget> interestTargets = new List<OpportunityTarget>(), saltTargets = new List<OpportunityTarget>();
        internal const double StrongGatheringValue = 2.0;
        private readonly List<int> foodInterestCells = new List<int>(), frontierInterestCells = new List<int>();
        internal static Color FoodInterestInk { get { return Art.PaperMode ? UnitArt.MapAccent : Color.FromArgb(232, 205, 130); } }
        internal static Color FrontierInterestInk { get { return Art.PaperMode ? UnitArt.MapRoute : Color.FromArgb(168, 219, 229); } }

        // Opportunities describe real actions, not items that are collected by movement.
        internal static double GatheringValue(Game game, Band band, int cell)
        {
            if (!game.TerrainTravelEnabled || band == null || cell < 0 || cell >= game.World.Cells.Length ||
                !game.Explored.Contains(cell) || !game.World.Cells[cell].IsLand || band.Population <= 0) return 0;
            return game.ForageYield(cell, band) / Math.Max(1, game.Upkeep(band));
        }

        internal static int FrontierUnknownNeighbors(Game game, int cell)
        {
            if (!game.TerrainTravelEnabled || cell < 0 || cell >= game.World.Cells.Length ||
                !game.Explored.Contains(cell) || !game.World.Cells[cell].IsLand) return 0;
            // Neighbor IDs are geometry. Never inspect terrain, inhabitants or deposits in fog.
            return game.World.Cells[cell].Neighbors.Count(id => !game.Explored.Contains(id));
        }

        // A raised badge has its own hit area, clipped exactly to remembered
        // ground. It resolves to a place; it never joins the unit target lists.
        public int PickOpportunity(Game game, float x, float y, out int kind)
        {
            kind = -1;
            if (!Bounds.Contains(x, y) || IsNavigating) return -1;
            foreach (OpportunityTarget target in saltTargets.Concat(interestTargets).Reverse())
            {
                if (!target.Bounds.Contains(x, y) || !IsOpportunityVisible(game, target.Kind, target.CellId)) continue;
                using (GraphicsPath polygon = new GraphicsPath())
                {
                    polygon.AddPolygon(target.Polygon);
                    if (!polygon.IsVisible(x, y)) continue;
                }
                kind = target.Kind; return target.CellId;
            }
            return -1;
        }

        public bool IsOpportunityVisible(Game game, int kind, int cell)
        {
            if (IsNavigating || cell < 0 || cell >= game.World.Cells.Length || !game.Explored.Contains(cell) ||
                !game.World.Cells[cell].IsLand || game.TerrainTravelEnabled && PlaceHasUnits(game, cell)) return false;
            if (kind == SaltOpportunity) return game.SaltEnabled && saltTargets.Any(t => t.CellId == cell) && SaltEconomy.Source(game, cell) != SaltSource.None;
            if (!game.TerrainTravelEnabled || !InterestHighlights || Layer > 1 || !interestTargets.Any(t => t.CellId == cell && t.Kind == kind)) return false;
            if (kind == ExplorationOpportunity) return Layer == 0 && FrontierUnknownNeighbors(game, cell) > 0;
            Band actor = game.Bands.FirstOrDefault(b => b.Id == CommandedBandId && game.CanControlBand(b.Id)) ?? game.TribeLeaderBand ?? game.Player;
            return kind == FoodOpportunity && GatheringValue(game, actor, cell) >= StrongGatheringValue;
        }

        private bool RegisterOpportunity(ProjectedCell cell, PointF ground, float size, int kind, bool beacon)
        {
            RectangleF bounds = beacon ? new RectangleF(ground.X - size * .92f, ground.Y - size * 1.07f, size * 1.84f, size * 1.65f) :
                new RectangleF(ground.X - size * .60f, ground.Y - size * .60f, size * 1.2f, size * 1.2f);
            if (!Bounds.IntersectsWith(bounds)) return false;
            (kind == SaltOpportunity ? saltTargets : interestTargets).Add(new OpportunityTarget { Kind = kind, CellId = cell.Cell.Id, Bounds = bounds, Polygon = cell.Polygon });
            return HoverOpportunityKind == kind && HoverOpportunityCell == cell.Cell.Id;
        }

        private static void DrawOpportunityEmphasis(Graphics g, ProjectedCell cell, Color ink)
        {
            using (Brush wash = new SolidBrush(Color.FromArgb(17, ink))) g.FillPolygon(wash, cell.Polygon);
            using (Pen edge = new Pen(Color.FromArgb(220, ink), 1.8f)) g.DrawPolygon(edge, cell.Polygon);
        }

        private void DrawMapInterests(Graphics g, Game game)
        {
            foodInterestCells.Clear(); frontierInterestCells.Clear(); interestTargets.Clear();
            if (!game.TerrainTravelEnabled || !InterestHighlights || Layer > 1 || IsNavigating) return;
            Band actor = game.Bands.FirstOrDefault(b => b.Id == CommandedBandId && game.CanControlBand(b.Id)) ?? game.TribeLeaderBand ?? game.Player;
            ProjectedCell[] candidates = Visible.Where(p => game.Explored.Contains(p.Cell.Id) && p.Cell.IsLand && p.Depth >= .18 && p.Radius >= 17 && !PlaceHasUnits(game, p.Cell.Id) && SaltEconomy.Source(game, p.Cell.Id) == SaltSource.None).ToArray();
            HashSet<int> food = new HashSet<int>(candidates.Where(p => GatheringValue(game, actor, p.Cell.Id) >= StrongGatheringValue)
                .OrderByDescending(p => GatheringValue(game, actor, p.Cell.Id)).ThenByDescending(p => p.Depth).Take(4).Select(p => p.Cell.Id));
            HashSet<int> paths = new HashSet<int>(candidates.Where(p => !food.Contains(p.Cell.Id) && FrontierUnknownNeighbors(game, p.Cell.Id) > 0)
                .OrderByDescending(p => Vec3.Dot(game.World.Cells[actor.CellId].Center, p.Cell.Center)).ThenBy(p => p.Cell.Id).Take(4).Select(p => p.Cell.Id));
            foreach (ProjectedCell cell in Visible)
            {
                int id = cell.Cell.Id;
                if (!game.Explored.Contains(id) || !cell.Cell.IsLand || cell.Depth < .18 || cell.Radius < 17) continue;
                int frontier = FrontierUnknownNeighbors(game, id);
                GraphicsState state = g.Save();
                using (GraphicsPath hex = new GraphicsPath())
                {
                    hex.AddPolygon(cell.Polygon); g.SetClip(hex, CombineMode.Intersect);
                    float size = Math.Min(30, Math.Max(19, cell.Radius * .34f));
                    if (food.Contains(id))
                    {
                        PointF at = new PointF(cell.Center.X + cell.Radius * .49f, cell.Center.Y + cell.Radius * .22f);
                        bool hot = RegisterOpportunity(cell, at, size, FoodOpportunity, true);
                        if (hot) DrawOpportunityEmphasis(g, cell, FoodInterestInk);
                        DrawOpportunityBeacon(g, at, size, FoodInterestInk, 0, SaltSource.None, hot);
                        foodInterestCells.Add(id);
                    }
                    if (paths.Contains(id) && Layer == 0)
                    {
                        for (int edge = 0; edge < cell.Cell.Corners.Length; edge++)
                        {
                            int next = (edge + 1) % cell.Cell.Corners.Length;
                            int other = SharedCell(cell.Cell.Corners[edge], cell.Cell.Corners[next], id);
                            if (other < 0 || game.Explored.Contains(other)) continue;
                            PointF a = Lerp(cell.Polygon[edge], cell.Center, .035f), b = Lerp(cell.Polygon[next], cell.Center, .035f);
                            using (Pen edgeInk = new Pen(Color.FromArgb(162, FrontierInterestInk), 1.2f) { DashPattern = new[] { 2f, 5f } }) g.DrawLine(edgeInk, a, b);
                        }
                        if (frontier >= 2 && cell.Radius >= 29)
                        {
                            PointF at = new PointF(cell.Center.X - cell.Radius * .43f, cell.Center.Y - cell.Radius * .21f);
                            bool hot = RegisterOpportunity(cell, at, size * .87f, ExplorationOpportunity, true);
                            if (hot) DrawOpportunityEmphasis(g, cell, FrontierInterestInk);
                            DrawOpportunityBeacon(g, at, size * .87f, FrontierInterestInk, 2, SaltSource.None, hot);
                        }
                        frontierInterestCells.Add(id);
                    }
                }
                g.Restore(state);
            }
        }

        // A paper symbol and a fine ground ring mark a useful place. Its exact
        // former pick rectangle remains intact; there is no light shaft or glow.
        private static void DrawOpportunityBeacon(Graphics g, PointF ground, float size, Color ink, int kind, SaltSource source, bool highlighted = false)
        {
            PointF light = new PointF(ground.X, ground.Y + size * .24f);
            using (Brush paper = new SolidBrush(Color.FromArgb(200, UnitArt.MapPaper))) g.FillEllipse(paper, ground.X - size * .87f, light.Y - size * .31f, size * 1.74f, size * .62f);
            using (Pen outer = new Pen(Color.FromArgb(highlighted ? 230 : 150, ink), highlighted ? 1.5f : .9f)) g.DrawEllipse(outer, ground.X - size * .87f, light.Y - size * .31f, size * 1.74f, size * .62f);
            using (Pen inner = new Pen(Color.FromArgb(130, ink), .65f)) g.DrawArc(inner, ground.X - size * .61f, light.Y - size * .23f, size * 1.22f, size * .46f, 15, 150);
            PointF badge = new PointF(ground.X, ground.Y - size * .47f);
            float half = size * .56f;
            PointF[] rim = { new PointF(badge.X, badge.Y - half), new PointF(badge.X + half * .86f, badge.Y - half * .5f), new PointF(badge.X + half * .86f, badge.Y + half * .5f), new PointF(badge.X, badge.Y + half), new PointF(badge.X - half * .86f, badge.Y + half * .5f), new PointF(badge.X - half * .86f, badge.Y - half * .5f) };
            using (Brush paper = new SolidBrush(UnitArt.MapPaper)) g.FillPolygon(paper, rim);
            using (Pen edge = new Pen(ink, highlighted ? 2f : 1.1f)) g.DrawPolygon(edge, rim);
            RectangleF icon = new RectangleF(badge.X - size * .43f, badge.Y - size * .43f, size * .86f, size * .86f);
            if (kind == 0) DrawFoodInterestIcon(g, icon);
            else if (kind == 1) DrawSaltSourceIcon(g, source, icon, false);
            else DrawFrontierInterestIcon(g, icon);
        }

        internal static void DrawFoodInterestIcon(Graphics g, RectangleF box)
        {
            GraphicsState state = g.Save();
            g.TranslateTransform(box.X + box.Width * .5f, box.Y + box.Height * .5f); g.ScaleTransform(box.Width / 24, box.Height / 24);
            using (Brush shadow = new SolidBrush(Art.PaperMode ? Color.FromArgb(210, UnitArt.MapPaper) : Color.FromArgb(148, 34, 49, 32))) g.FillEllipse(shadow, -10, -10, 20, 21);
            using (Pen stem = new Pen(FoodInterestInk, 1.25f))
            {
                g.DrawBezier(stem, -2, 9, -2, 3, 2, -2, 2, -10);
                g.DrawLine(stem, -2, 7, -6, 4);
            }
            using (Brush grain = new SolidBrush(FoodInterestInk))
            {
                for (int i = 0; i < 3; i++)
                {
                    float y = -7 + i * 4, x = 1 - i * .7f;
                    g.FillPolygon(grain, new[] { new PointF(x, y + 2), new PointF(x - 5, y), new PointF(x - 5, y - 3), new PointF(x - 1, y - 1) });
                    g.FillPolygon(grain, new[] { new PointF(x + 1, y + 3), new PointF(x + 6, y), new PointF(x + 6, y - 3), new PointF(x + 2, y - 1) });
                }
            }
            g.Restore(state);
        }

        internal static void DrawFrontierInterestIcon(Graphics g, RectangleF box)
        {
            GraphicsState state = g.Save();
            g.TranslateTransform(box.X + box.Width * .5f, box.Y + box.Height * .5f); g.ScaleTransform(box.Width / 24, box.Height / 24);
            using (Brush shadow = new SolidBrush(Art.PaperMode ? Color.FromArgb(210, UnitArt.MapPaper) : Color.FromArgb(156, 28, 47, 48))) g.FillEllipse(shadow, -9, -9, 18, 18);
            using (Pen orbit = new Pen(Color.FromArgb(149, FrontierInterestInk), .8f)) g.DrawEllipse(orbit, -8, -8, 16, 16);
            using (Brush light = new SolidBrush(FrontierInterestInk)) g.FillPolygon(light, new[] { new PointF(0, -11), new PointF(2, -2), new PointF(9, 0), new PointF(2, 2), new PointF(0, 9), new PointF(-2, 2), new PointF(-9, 0), new PointF(-2, -2) });
            using (Brush center = new SolidBrush(Art.PaperMode ? UnitArt.MapPaper : Color.FromArgb(50, 90, 94))) g.FillEllipse(center, -1.5f, -1.5f, 3, 3);
            g.Restore(state);
        }
    }
}
