using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using Clio.Simulation;

namespace Clio.Desktop
{
    public sealed partial class GameForm
    {
        private BattleRenderer battleRenderer;
        private BattleState battleViewState;
        private int battleSelectedFormation = -1, battleHoverCell = -1;
        private bool battleHelpOpen;
        private string battleLocalHint = "";
        private static readonly RectangleF BattleMapBounds = new RectangleF(24, 109, 1552, 683);
        private static readonly Color BattleFriendly = Color.FromArgb(225, 191, 115), BattleEnemy = Color.FromArgb(219, 132, 111);
        private bool BattleOverlayActive { get { return game != null && game.Battle != null; } }

        private void PrepareBattleView()
        {
            if (!BattleOverlayActive) return;
            if (battleRenderer == null) battleRenderer = new BattleRenderer();
            if (!Object.ReferenceEquals(battleViewState, game.Battle))
            {
                battleViewState = game.Battle; battleSelectedFormation = -1; battleHoverCell = -1;
                battleHelpOpen = false; battleLocalHint = "";
            }
            if (!game.Battle.Formations.Any(f => f.Id == battleSelectedFormation && f.Alive && f.Side == 0))
            {
                BattleFormation first = game.Battle.Formations.Where(f => f.Side == 0 && f.Alive).OrderByDescending(f => f.Actions > 0).ThenBy(f => f.Id).FirstOrDefault();
                battleSelectedFormation = first == null ? -1 : first.Id;
            }
            battleRenderer.Prepare(game, game.Battle.Cells, game.Battle.CenterCellId, BattleMapBounds);
        }

        private BattleFormation SelectedBattleFormation
        { get { return !BattleOverlayActive ? null : game.Battle.Formations.FirstOrDefault(f => f.Id == battleSelectedFormation && f.Side == 0 && f.Alive); } }

        private void SelectBattleFormation(int id)
        {
            if (!BattleOverlayActive || !game.Battle.Formations.Any(f => f.Id == id && f.Side == 0 && f.Alive)) return;
            battleSelectedFormation = id; battleLocalHint = ""; Invalidate();
        }

        private void IssueBattleOrder(string command)
        {
            if (!BattleOverlayActive) return;
            battleLocalHint = ""; BattleCommand(command); battleLocalHint = status ?? ""; buttons.Clear(); Invalidate();
        }

        private bool HandleBattlePointerDown(PointF point, MouseButtons button)
        {
            if (!BattleOverlayActive) return false;
            PrepareBattleView(); hoverPoint = point;
            if (button != MouseButtons.Left && button != MouseButtons.Right) return true;
            if (button == MouseButtons.Left)
                for (int i = buttons.Count - 1; i >= 0; i--)
                    if (buttons[i].Bounds.Contains(point)) { buttons[i].Click(); return true; }
            if (game.Battle.Phase == BattlePhase.Finished) return true;
            if (BattleFieldObscured(point)) return true;
            int cellId = battleRenderer.Pick(point);
            if (cellId < 0) { battleHelpOpen = false; Invalidate(); return true; }
            BattleFormation at = game.Battle.Formations.FirstOrDefault(f => f.Alive && f.CellId == cellId);
            BattleFormation selected = SelectedBattleFormation;
            if (at != null && at.Side == 0 && button == MouseButtons.Left) { SelectBattleFormation(at.Id); return true; }
            if (selected == null) { battleLocalHint = "Select one of your formations first."; Invalidate(); return true; }
            if (game.Battle.Phase == BattlePhase.Deployment)
            {
                if (BattleRules.CanDeploy(game, selected.Id, cellId)) IssueBattleOrder("battle-deploy:" + selected.Id + ":" + cellId);
                else { battleLocalHint = "Choose an unoccupied hex inside your deployment zone."; Invalidate(); }
            }
            else if (at != null && at.Side != 0)
            {
                var preview = BattleRules.StrikePreview(game, selected.Id, at.Id);
                if (preview.Allowed) IssueBattleOrder("battle-strike:" + selected.Id + ":" + at.Id);
                else { battleLocalHint = preview.Reason; Invalidate(); }
            }
            else if (at == null && BattleRules.CanMove(game, selected.Id, cellId)) IssueBattleOrder("battle-move:" + selected.Id + ":" + cellId);
            else { battleLocalHint = "Choose an adjacent highlighted hex, or select another formation."; Invalidate(); }
            return true;
        }

        private void UpdateBattleHover(PointF point)
        {
            if (!BattleOverlayActive) return;
            PrepareBattleView(); hoverPoint = point;
            int cell = game.Battle.Phase == BattlePhase.Finished || buttons.Any(b => b.Bounds.Contains(point)) || BattleFieldObscured(point) ? -1 : battleRenderer.Pick(point);
            battleHoverCell = cell;
            Cursor = buttons.Any(b => b.Bounds.Contains(point)) || cell >= 0 ? Cursors.Hand : Cursors.Default;
            Invalidate();
        }

        private bool HandleBattleKey(KeyEventArgs e)
        {
            if (!BattleOverlayActive) return false;
            PrepareBattleView();
            if (e.Control && e.KeyCode == Keys.S) SaveStory();
            else if (e.KeyCode == Keys.P) { StopAutoplay("You command the battle."); Invalidate(); }
            else if (e.KeyCode == Keys.Escape) { battleHelpOpen = false; battleLocalHint = game.Battle.Phase == BattlePhase.Finished ? "Select Return to campaign to continue." : "Use Retreat to withdraw, or finish the battle before returning to the campaign."; Invalidate(); }
            else if (e.KeyCode == Keys.H || e.KeyCode == Keys.F1) { battleHelpOpen = !battleHelpOpen; Invalidate(); }
            else if (e.KeyCode == Keys.Enter || e.KeyCode == Keys.Space)
                IssueBattleOrder(game.Battle.Phase == BattlePhase.Finished ? "battle-close" : game.Battle.Phase == BattlePhase.Deployment ? "battle-start" : "battle-round");
            else if (game.Battle.Phase != BattlePhase.Finished)
            {
                if (e.KeyCode == Keys.N || e.KeyCode == Keys.Tab) NextBattleFormation();
                else if (e.KeyCode == Keys.D && game.Battle.Phase == BattlePhase.Fighting && SelectedBattleFormation != null && SelectedBattleFormation.Actions > 0)
                    IssueBattleOrder("battle-defend:" + SelectedBattleFormation.Id);
                else if (e.KeyCode == Keys.A) IssueBattleOrder("battle-auto");
            }
            e.Handled = true; e.SuppressKeyPress = true; return true;
        }

        private void NextBattleFormation()
        {
            if (!BattleOverlayActive) return;
            BattleFormation[] ready = game.Battle.Formations.Where(f => f.Side == 0 && f.Alive && (game.Battle.Phase == BattlePhase.Deployment || f.Actions > 0)).OrderBy(f => f.Id).ToArray();
            if (ready.Length == 0) { battleLocalHint = "Your formations have finished. End the round."; Invalidate(); return; }
            int current = Array.FindIndex(ready, f => f.Id == battleSelectedFormation);
            SelectBattleFormation(ready[(current + 1) % ready.Length].Id);
        }

        private void DisposeBattleView()
        { if (battleRenderer != null) { battleRenderer.Dispose(); battleRenderer = null; } battleViewState = null; }

        private bool BattleFieldObscured(PointF point)
        { return battleHelpOpen && BattleHelpBounds.Contains(point) || new RectangleF(39, 121, 374, 34).Contains(point) || new RectangleF(1188, 121, 374, 34).Contains(point); }
    }
}
