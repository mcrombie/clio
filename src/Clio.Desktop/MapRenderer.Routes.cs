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
        private int reunionActorId = -1, reunionLeaderId = -1;
        private int[] reunionCells = new int[0];
        private readonly List<int> drawnReunionCells = new List<int>();
        public void SetReunionRoute(int actorId, int leaderId, int[] cells)
        {
            reunionActorId = actorId; reunionLeaderId = leaderId;
            reunionCells = cells == null ? new int[0] : (int[])cells.Clone();
        }
        public void ClearReunionRoute()
        { reunionActorId = -1; reunionLeaderId = -1; reunionCells = new int[0]; drawnReunionCells.Clear(); }
        private void DrawReunionRoute(Graphics g, Game game)
        {
            drawnReunionCells.Clear();
            if (IsNavigating || reunionCells.Length < 2 || !game.TribesEnabled || !game.CanControlBand(reunionActorId) || !game.CanControlBand(reunionLeaderId)) return;
            Band actor = game.Bands.First(b => b.Id == reunionActorId), leader = game.TribeLeaderBand;
            if (leader == null || leader.Id != reunionLeaderId || actor.CellId != reunionCells[0] || leader.CellId != reunionCells[reunionCells.Length - 1]) return;
            // Independently validate the whole preview before painting any part.
            // Atlas mode never turns an old route into a hidden-information hint.
            foreach (int id in reunionCells)
                if (id < 0 || id >= game.World.Cells.Length || !game.Explored.Contains(id) || !game.World.Cells[id].IsLand ||
                    id != actor.CellId && EncounterRules.HostileAt(game, id, actor.Id)) return;
            for (int i = 1; i < reunionCells.Length; i++)
                if (TravelRules.MoveCost(game, actor, reunionCells[i - 1], reunionCells[i]) <= 0) return;

            Color route = Color.FromArgb(215, 220, 185, 112);
            GraphicsState state = g.Save();
            try
            {
                g.SetClip(Bounds, CombineMode.Intersect);
                using (Pen shadow = new Pen(Color.FromArgb(145, 15, 32, 34), 5.5f) { StartCap = LineCap.Round, EndCap = LineCap.Round })
                using (Pen path = new Pen(route, 2f) { DashPattern = new[] { 2.4f, 3.1f }, StartCap = LineCap.Round, EndCap = LineCap.Round })
                using (Brush waypoint = new SolidBrush(route))
                for (int i = 1; i < reunionCells.Length; i++)
                {
                    ProjectedCell first, second;
                    if (!projected.TryGetValue(reunionCells[i - 1], out first) || !projected.TryGetValue(reunionCells[i], out second) || first.Depth < .12 || second.Depth < .12) continue;
                    g.DrawLine(shadow, first.Center, second.Center); g.DrawLine(path, first.Center, second.Center);
                    g.FillEllipse(waypoint, second.Center.X - 3, second.Center.Y - 3, 6, 6);
                    if (!drawnReunionCells.Contains(first.Cell.Id)) drawnReunionCells.Add(first.Cell.Id);
                    if (!drawnReunionCells.Contains(second.Cell.Id)) drawnReunionCells.Add(second.Cell.Id);
                }
                ProjectedCell finish;
                if (projected.TryGetValue(leader.CellId, out finish) && finish.Depth >= .12)
                {
                    using (Pen ring = new Pen(route, 2f)) g.DrawEllipse(ring, finish.Center.X - 14, finish.Center.Y - 14, 28, 28);
                    using (Pen inner = new Pen(Color.FromArgb(115, route), 1f)) g.DrawEllipse(inner, finish.Center.X - 19, finish.Center.Y - 19, 38, 38);
                }
            }
            finally { g.Restore(state); }
        }
    }
}
