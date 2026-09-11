using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using Clio.Simulation;

namespace Clio.Desktop
{
    internal sealed partial class MapRenderer
    {
        private readonly List<Tuple<RectangleF, int>> unitStackTargets = new List<Tuple<RectangleF, int>>();
        public int PickUnitStack(float x, float y)
        {
            if (!Bounds.Contains(x, y) || unitClip != null && !unitClip.IsVisible(x, y)) return -1;
            for (int i = unitStackTargets.Count - 1; i >= 0; i--)
                if (unitStackTargets[i].Item1.Contains(x, y)) return unitStackTargets[i].Item2;
            return -1;
        }

        private void DrawUnitStackBadge(Graphics g, Game game, ProjectedCell cell)
        {
            int bands = game.Bands.Count(b => b.Population > 0 && b.CellId == cell.Cell.Id);
            int animals = game.Beasts.Count(b => b.Count > 0 && b.CellId == cell.Cell.Id);
            string label = (bands + animals).ToString() + " groups";
            float width = cell.Radius >= 70 ? 84 : 39, height = cell.Radius >= 70 ? 23 : 20;
            if (cell.Radius < 70) label = "+" + (bands + animals - 1);
            RectangleF badge = new RectangleF(cell.Center.X + Math.Min(36, cell.Radius * .37f), cell.Center.Y - 22, width, height);
            using (Brush ground = new SolidBrush(Color.FromArgb(248, UnitArt.MapPaper))) g.FillRectangle(ground, badge);
            using (Pen edge = new Pen(Color.FromArgb(210, UnitArt.MapAccent), 1)) g.DrawRectangle(edge, badge.X, badge.Y, badge.Width, badge.Height);
            Typography.Line(g, label, new RectangleF(badge.X + 3, badge.Y, badge.Width - 6, badge.Height), cell.Radius >= 70 ? 14 : 12, UnitArt.MapInk, TypeRole.Utility, true, StringAlignment.Center);
            AddTarget(unitStackTargets, badge, cell.Cell.Id);
        }

        // Unit identities have visual priority. Occupied places retain their
        // resource details in hover/cards, while map beacons mark open ground.
        private bool PlaceHasUnits(Game game, int cell)
        {
            return game.Explored.Contains(cell) && (game.Bands.Any(b => b.Population > 0 && b.CellId == cell) || game.Beasts.Any(b => b.Count > 0 && b.CellId == cell));
        }
    }
}
