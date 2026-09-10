using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using Clio.Simulation;

namespace Clio.Desktop
{
    public sealed partial class GameForm
    {
        // The unit receiving orders is independent of the place being inspected.
        // Companion groups belong to this household but have no independent orders.
        private int commandedBandId = -1;
        private bool HasCommandBand
        { get { return game != null && game.CanControlBand(commandedBandId) && !game.IsOver; } }

        private void ArmMapCommandBand(int id)
        {
            int previous = commandedBandId;
            commandedBandId = game.CanControlBand(id) && !game.IsOver ? id : -1;
            map.CommandedBandId = commandedBandId;
            map.OrderPreviewCell = -1;
            if (previous != commandedBandId && journal != null) RefreshAdvisers();
        }
        private void SyncMapCommandSelection()
        {
            if (!game.TribesEnabled)
            {
                if (inspectorPage == 1) ArmMapCommandBand(inspectedBandId);
                else if (inspectorPage == 2) ArmMapCommandBand(-1);
                return;
            }
            if (inspectorPage == 1 && game.CanControlBand(inspectedBandId)) ArmMapCommandBand(inspectedBandId);
            else if (!HasCommandBand) ArmMapCommandBand(-1);
        }
        private void OnRightDown(MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Right || !GameViewport.Contains(e.Location)) return;
            RightMoveTo(Virtual(e.Location));
        }
        private string RightMoveReason(int cell)
        {
            if (semiautomatic) return "Semiautomatic: your choices guide the bands. Switch to Manual to order a move yourself.";
            if (game.IsOver) return "Your people's story has ended.";
            if (!HasCommandBand)
            {
                Beast companion = inspectorPage == 2 ? game.Beasts.FirstOrDefault(b => b.Id == selectedAnimalId && b.Count > 0 && b.Domestic && game.CanControlBand(b.OwnerId)) : null;
                return companion != null ? "Companions travel with your band. Select your people's banner to order movement." : "Select your people's banner, or press Home, to give movement orders.";
            }
            if (cell < 0 || cell >= game.World.Cells.Length) return "Choose a hex on the map.";
            if (!game.Explored.Contains(cell)) return "That place is still unknown to your people. Choose a hex within the known land.";
            Band actor = CurrentOrderBand;
            if (cell == actor.CellId) return "Your selected band is already here.";
            if (!game.World.Cells[cell].IsLand) return "Your band needs a land route; it cannot cross open water.";
            if (!game.World.Cells[actor.CellId].Neighbors.Contains(cell)) return "Move one adjacent hex at a time. The outlined land marks your next steps.";
            if (game.ActionsFor(actor.Id) <= 0) return "This band has no actions left. Select another band or end the turn.";
            int cost = MapTravelCost(actor, cell);
            if (cost > game.ActionsFor(actor.Id)) return MapTravelKind(actor.CellId, cell) + " needs " + cost + " actions. This band has " + game.ActionsFor(actor.Id) + " left.";
            if (game.World.Cells[cell].Terrain == Terrain.Ice && actor.Food < game.Upkeep(actor) * 3)
                return "Crossing polar ice needs provisions equal to three times your people's needs.";
            return "";
        }
        private bool OrderEncounterAt(int cell)
        {
            return game.Rules == SimulationRules.MobileUnits && game.Explored.Contains(cell) &&
                (game.Beasts.Any(b => b.CellId == cell && b.Count > 0 && (!b.Domestic || !game.CanControlBand(b.OwnerId))) ||
                 game.Bands.Any(b => b.CellId == cell && b.Population > 0 && !game.CanControlBand(b.Id)));
        }
        private void RightMoveTo(PointF point)
        {
            if (page != 0 || BlockingSheet || dragging || map.IsNavigating || !map.Bounds.Contains(point) ||
                MapOverlayContains(point) || buttons.Any(b => b.Bounds.Contains(point))) return;
            int destination = map.PickUnitStack(point.X, point.Y);
            int opportunityKind;
            if (destination < 0) destination = map.PickOpportunity(game, point.X, point.Y, out opportunityKind);
            if (destination < 0) destination = map.Pick(point.X, point.Y);
            if (destination < 0) return;
            HideMapHover(); StopAutoplay(null);
            string reason = RightMoveReason(destination);
            if (reason.Length > 0) { status = reason; map.OrderPreviewCell = -1; Invalidate(); return; }

            // Resolve the clicked hex, never a previously inspected animal/band.
            // MoveSelection retains the game's existing occupied-place review.
            ClearMapTransient(); selected = destination; selectedAnimalId = -1;
            inspectedBandId = -1; inspectorPage = 0;
            MoveSelection();
            if (!BlockingSheet)
            {
                selected = CurrentOrderBand.CellId; inspectedBandId = CurrentOrderBand.Id;
                inspectorPage = 1; selectedAnimalId = -1;
            }
            map.OrderPreviewCell = -1; buttons.Clear(); Invalidate();
        }
        private void UpdateOrderPreview(PointF point)
        {
            int cell = -1;
            if (page == 0 && HasCommandBand && !BlockingSheet && !dragging && !map.IsNavigating &&
                map.Bounds.Contains(point) && !MapOverlayContains(point) && !buttons.Any(b => b.Bounds.Contains(point)))
            {
                int candidate = map.PickUnitStack(point.X, point.Y);
                int opportunityKind;
                if (candidate < 0) candidate = map.PickOpportunity(game, point.X, point.Y, out opportunityKind);
                if (candidate < 0) candidate = map.Pick(point.X, point.Y);
                if (RightMoveReason(candidate).Length == 0) cell = candidate;
            }
            if (map.OrderPreviewCell != cell) { map.OrderPreviewCell = cell; Invalidate(); }
        }
        private string MapOrderHint(int cell)
        {
            if (HasCommandBand && game.TerrainTravelEnabled && MapCardKnown(cell) && game.World.Cells[cell].IsLand && game.World.Cells[CurrentOrderBand.CellId].Neighbors.Contains(cell))
            {
                int cost = MapTravelCost(CurrentOrderBand, cell);
                if (cost > game.ActionsFor(CurrentOrderBand.Id)) return "Needs " + cost + " actions  ·  " + game.ActionsFor(CurrentOrderBand.Id) + " left";
            }
            if (!HasCommandBand || RightMoveReason(cell).Length > 0) return "Click for the place record";
            return OrderEncounterAt(cell) ? "Right-click to review an encounter" : "Right-click to move  /  " + MapTravelCost(CurrentOrderBand, cell) + (MapTravelCost(CurrentOrderBand, cell) == 1 ? " action" : " actions");
        }

        private int MapTravelCost(Band actor, int cell)
        { return game.TerrainTravelEnabled ? TravelRules.MoveCost(game, actor, actor.CellId, cell) : 1; }

        private string MapTravelKind(int from, int to)
        {
            if (!game.TerrainTravelEnabled || !MapCardKnown(from) || !MapCardKnown(to)) return "Travel";
            bool mountain = game.World.Cells[to].Terrain == Terrain.Mountains, river = TravelRules.HasRiver(game, from, to);
            return mountain && river ? "Mountain and river crossing" : mountain ? "Mountain travel" : river ? "River crossing" : "Land travel";
        }
        private void DrawCommandedBand(Graphics g)
        {
            RectangleF box = new RectangleF(495, 868, 328, 43);
            if (HasCommandBand)
            {
                Band actor = CurrentOrderBand;
                IdentityArt.DrawEmblem(g, game.TribeOf(actor.Id), new RectangleF(500, 876, 26, 26), false);
                Typography.Line(g, actor.Name, new RectangleF(538, 872, 227, 35), 22, Art.Ink, TypeRole.Heading, true);
                for (int i = 0; i < 2; i++)
                {
                    float x = 788 + i * 17;
                    using (Brush fill = new SolidBrush(i < game.ActionsFor(actor.Id) ? Art.Gold : Border))
                        g.FillPolygon(fill, new[] { new PointF(x, 883), new PointF(x + 4, 889), new PointF(x, 895), new PointF(x - 4, 889) });
                }
            }
            else Typography.Line(g, "Select a band", new RectangleF(506, 871, 295, 35), 21, Art.Muted, TypeRole.Heading, true);
            buttons.Add(new UiButton(box, ToggleBandDetails) { Tip = "Selected band / " + CurrentOrderBand.Name + "\n" + CurrentOrderActions + " actions remain (gold diamonds). Click for supplies, personality, Hold and reunion details. Right-click adjacent land to move." });
            MapIconButton(g, "branch", new RectangleF(841, 868, 43, 43), NextReadyBand, false, false, "Next ready band [N]\nSelect a household with actions remaining.");
            MapIconButton(g, "locate", new RectangleF(897, 868, 43, 43), FocusCommandBandQuietly, false, false, "Find selected band\nCenter the map without opening another panel.");
        }
    }
}
