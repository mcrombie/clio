using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Clio.Simulation;

namespace Clio.Desktop
{
    public sealed partial class GameForm
    {
        private bool reunionRouteVisible;
        private bool reunionRouteAvailable;
        private int reunionRouteCost, reunionRouteMinimumTurns;
        private string reunionRouteStatus = "Select a band to preview its reunion route.";
        private string reunionRouteKey;
        private Game reunionRouteGame;
        private int[] reunionRouteCells = new int[0];
        private bool ReunionRouteAvailable { get { return reunionRouteAvailable; } }
        private int ReunionRouteCost { get { return reunionRouteCost; } }
        private int ReunionRouteMinimumTurns { get { return reunionRouteMinimumTurns; } }
        private string ReunionRouteStatus { get { return reunionRouteStatus; } }

        private void NextReadyBand()
        {
            if (BlockingSheet || game == null) return;
            StopAutoplay(null);
            Band[] ready = game.ControlledBands.Where(b => game.ActionsFor(b.Id) > 0).OrderBy(b => b.Id).ToArray();
            if (ready.Length == 0)
            {
                if (!HasCommandBand)
                {
                    ArmMapCommandBand(-1);
                    if (inspectorPage == 1 && !game.CanControlBand(inspectedBandId)) inspectedBandId = -1;
                }
                status = game.IsOver ? "No living band remains to receive orders." : "Every band has finished its orders. End the turn when you are ready.";
                RefreshReunionRoute(); Invalidate(); return;
            }
            Band next = ready.FirstOrDefault(b => b.Id > commandedBandId) ?? ready[0];
            ClearMapTransient();
            ArmMapCommandBand(next.Id); selected = next.CellId; selectedAnimalId = -1;
            inspectedBandId = next.Id; inspectorPage = 1; page = 0;
            map.SelectedBandId = next.Id; map.SelectedAnimalId = -1;
            map.Focus(game.World.Cells[next.CellId]); RefreshReunionRoute();
            status = next.Name + " is ready: " + game.ActionsFor(next.Id).ToString(CultureInfo.InvariantCulture) + " action" + (game.ActionsFor(next.Id) == 1 ? "" : "s") + " remaining.";
            Invalidate();
        }

        private void ToggleReunionRoute()
        {
            if (BlockingSheet) return;
            if (reunionRouteVisible)
            {
                reunionRouteVisible = false; map.ClearReunionRoute();
                status = "Reunion route hidden."; Invalidate(); return;
            }
            RefreshReunionRoute();
            if (!reunionRouteAvailable) { status = reunionRouteStatus; Invalidate(); return; }
            reunionRouteVisible = true;
            map.SetReunionRoute(commandedBandId, game.TribeLeaderBand.Id, reunionRouteCells);
            status = "Reunion route preview. Give movement orders yourself; no journey has been queued.";
            Invalidate();
        }

        private void ClearReunionRoute()
        {
            reunionRouteVisible = false; reunionRouteAvailable = false; reunionRouteCost = 0; reunionRouteMinimumTurns = 0;
            reunionRouteStatus = "Select a band to preview its reunion route.";
            reunionRouteKey = null; reunionRouteGame = null; reunionRouteCells = new int[0];
            if (map != null) map.ClearReunionRoute();
        }

        private void RefreshReunionRoute()
        {
            Band actor = game == null || !HasCommandBand ? null : game.Bands.FirstOrDefault(b => b.Id == commandedBandId && game.CanControlBand(b.Id));
            Band leader = game == null ? null : game.TribeLeaderBand;
            string key = actor == null || leader == null ? "unselected" :
                actor.Id + "/" + actor.CellId + "/" + actor.Population + "/" + actor.Food.ToString("R", CultureInfo.InvariantCulture) + "/" +
                leader.Id + "/" + leader.CellId + "/" + game.Turn + "/" + game.ActionsFor(actor.Id) + "/" + game.Explored.Count + "/" +
                game.Encounters.Records.Count + "/" + game.TribesEnabled + "/" + game.TerrainTravelEnabled;
            if (!Object.ReferenceEquals(reunionRouteGame, game) || reunionRouteKey != key)
            {
                reunionRouteGame = game; reunionRouteKey = key; reunionRouteCells = new int[0];
                reunionRouteAvailable = false; reunionRouteCost = 0; reunionRouteMinimumTurns = 0;
                if (actor == null) reunionRouteStatus = "Select one of your living bands.";
                else if (leader == null) reunionRouteStatus = "No living leader remains.";
                else if (!game.TribesEnabled || actor.Id == leader.Id) reunionRouteStatus = "The leader marks the reunion place.";
                else if (actor.CellId == leader.CellId) reunionRouteStatus = "Already together with the leader.";
                else if (!game.Explored.Contains(actor.CellId) || !game.Explored.Contains(leader.CellId)) reunionRouteStatus = "The reunion route is outside the known map.";
                else
                {
                    reunionRouteCells = FindReunionRoute(actor, leader.CellId);
                    if (reunionRouteCells.Length < 2) reunionRouteStatus = "No safe known route to the leader.";
                    else
                    {
                        int remaining = game.ActionsFor(actor.Id), turns = 1;
                        for (int i = 1; i < reunionRouteCells.Length; i++)
                        {
                            int cost = TravelRules.MoveCost(game, actor, reunionRouteCells[i - 1], reunionRouteCells[i]);
                            reunionRouteCost += cost;
                            if (remaining < cost) { turns++; remaining = 2; }
                            remaining -= cost;
                        }
                        reunionRouteMinimumTurns = turns; reunionRouteAvailable = true;
                        reunionRouteStatus = reunionRouteCost.ToString(CultureInfo.InvariantCulture) + " action" + (reunionRouteCost == 1 ? "" : "s") +
                            " · " + turns.ToString(CultureInfo.InvariantCulture) + " turn" + (turns == 1 ? "" : "s") + " along this route";
                    }
                }
            }
            if (reunionRouteVisible && reunionRouteAvailable && actor != null && leader != null)
                map.SetReunionRoute(actor.Id, leader.Id, reunionRouteCells);
            else map.ClearReunionRoute();
        }

        private bool ReunionCellSafe(Band actor, int cell)
        {
            if (cell < 0 || cell >= game.World.Cells.Length || !game.Explored.Contains(cell)) return false;
            Cell place = game.World.Cells[cell];
            if (!place.IsLand || EncounterRules.HostileAt(game, cell, actor.Id)) return false;
            // A preview cannot promise passage through ice the band cannot
            // provision. Future food and hazards can still change along a route.
            return place.Terrain != Terrain.Ice || actor.Food >= Math.Ceiling(actor.Population * (place.Temperature < .24 ? 1.35 : 1)) * 3 + actor.Population * .15;
        }

        private int[] FindReunionRoute(Band actor, int target)
        {
            if (!ReunionCellSafe(actor, target)) return new int[0];
            HashSet<int> open = new HashSet<int> { actor.CellId }, closed = new HashSet<int>();
            Dictionary<int, int> cost = new Dictionary<int, int> { { actor.CellId, 0 } }, previous = new Dictionary<int, int>();
            while (open.Count > 0)
            {
                int current = open.OrderBy(id => cost[id]).ThenBy(id => id).First(); open.Remove(current); closed.Add(current);
                if (current == target)
                {
                    List<int> path = new List<int> { target };
                    while (current != actor.CellId) { current = previous[current]; path.Add(current); }
                    path.Reverse(); return path.ToArray();
                }
                foreach (int next in game.World.Cells[current].Neighbors.OrderBy(id => id))
                {
                    if (closed.Contains(next) || !ReunionCellSafe(actor, next)) continue;
                    int edge = TravelRules.MoveCost(game, actor, current, next); if (edge <= 0) continue;
                    int distance = cost[current] + edge, old;
                    if (!cost.TryGetValue(next, out old) || distance < old)
                    { cost[next] = distance; previous[next] = current; open.Add(next); }
                }
            }
            return new int[0];
        }
    }
}
