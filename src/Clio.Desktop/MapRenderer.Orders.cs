using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using Clio.Simulation;

namespace Clio.Desktop
{
    internal sealed partial class MapRenderer
    {
        private void DrawOrderPreview(Graphics g, Game game)
        {
            ProjectedCell origin, target;
            int destination = OrderPreviewCell;
            Band actor = game.Bands.FirstOrDefault(b => b.Id == CommandedBandId && game.CanControlBand(b.Id));
            if (IsNavigating || actor == null || game.IsOver || game.ActionsFor(actor.Id) <= 0 ||
                destination < 0 || destination >= game.World.Cells.Length || !game.Explored.Contains(destination) ||
                !game.World.Cells[destination].IsLand || !game.World.Cells[actor.CellId].Neighbors.Contains(destination) ||
                !projected.TryGetValue(actor.CellId, out origin) || !projected.TryGetValue(destination, out target) ||
                origin.Depth < .1 || target.Depth < .1) return;
            int cost = game.TerrainTravelEnabled ? TravelRules.MoveCost(game, actor, actor.CellId, destination) : 1;
            if (cost <= 0 || game.ActionsFor(actor.Id) < cost) return;
            Color ink = EncounterRules.HostileAt(game, destination, actor.Id) ? UnitArt.MapDanger : UnitArt.MapRoute;
            using (Brush light = new SolidBrush(Color.FromArgb(18, ink))) g.FillPolygon(light, target.Polygon);
            using (Pen edge = new Pen(Color.FromArgb(220, ink), 1.8f)) g.DrawPolygon(edge, target.Polygon);
            PointF from = Lerp(origin.Center, target.Center, .26f), to = Lerp(origin.Center, target.Center, .75f);
            using (Pen under = new Pen(Color.FromArgb(205, UnitArt.MapPaper), 4.5f)) g.DrawLine(under, from, to);
            using (Pen path = new Pen(ink, 1.8f) { DashPattern = new[] { 3f, 3f } }) g.DrawLine(path, from, to);
            double angle = Math.Atan2(to.Y - from.Y, to.X - from.X);
            PointF left = new PointF(to.X - (float)Math.Cos(angle - .5) * 9, to.Y - (float)Math.Sin(angle - .5) * 9);
            PointF rightEdge = new PointF(to.X - (float)Math.Cos(angle + .5) * 9, to.Y - (float)Math.Sin(angle + .5) * 9);
            using (Pen arrow = new Pen(ink, 2)) g.DrawLines(arrow, new[] { left, to, rightEdge });
            if (game.TerrainTravelEnabled && Zoom >= 2)
            {
                PointF midpoint = Lerp(from, to, .5f);
                RectangleF badge = new RectangleF(midpoint.X - 12, midpoint.Y - 11, 24, 22);
                using (Brush shade = new SolidBrush(Color.FromArgb(245, UnitArt.MapPaper))) g.FillEllipse(shade, badge);
                using (Pen rim = new Pen(Color.FromArgb(170, ink), 1)) g.DrawEllipse(rim, badge);
                Typography.Line(g, cost.ToString(), badge, 15, ink, TypeRole.Number, true, StringAlignment.Center);
            }
        }
    }
}
