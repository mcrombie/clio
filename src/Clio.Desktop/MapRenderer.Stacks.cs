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
            using (GraphicsPath paper = new GraphicsPath())
            {
                paper.AddPolygon(new[] {
                    new PointF(badge.X + 1, badge.Y + .5f), new PointF(badge.X + badge.Width * .43f, badge.Y),
                    new PointF(badge.Right - 1, badge.Y + .4f), new PointF(badge.Right, badge.Bottom - 1),
                    new PointF(badge.X + badge.Width * .55f, badge.Bottom - .3f), new PointF(badge.X, badge.Bottom)
                });
                using (Brush ground = new SolidBrush(Color.FromArgb(239, UnitArt.MapPaper))) g.FillPath(ground, paper);
            }
            using (Pen rule = new Pen(Color.FromArgb(154, UnitArt.MapAccent), .7f))
            {
                g.DrawLine(rule, badge.X + 2, badge.Bottom - 1.4f, badge.X + badge.Width * .37f, badge.Bottom - 1);
                g.DrawLine(rule, badge.X + badge.Width * .61f, badge.Bottom - 1.1f, badge.Right - 2, badge.Bottom - 1.6f);
                g.DrawLine(rule, badge.X + 2.5f, badge.Y + 5, badge.X + 1.8f, badge.Bottom - 5);
            }
            Typography.Line(g, label, new RectangleF(badge.X + 3, badge.Y, badge.Width - 6, badge.Height), cell.Radius >= 70 ? 14 : 13, UnitArt.MapInk, TypeRole.Annotation, true, StringAlignment.Center);
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
