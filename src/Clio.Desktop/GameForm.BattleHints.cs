using System;
using System.Drawing;
using System.Linq;
using Clio.Simulation;

namespace Clio.Desktop
{
    public sealed partial class GameForm
    {
        private void DrawBattleHover(Graphics g)
        {
            if (!new RectangleF(0, 0, 1600, 960).Contains(hoverPoint)) return;
            UiButton control = buttons.LastOrDefault(b => b.Bounds.Contains(hoverPoint));
            string text = control == null ? BattleCellHint() : control.Tip;
            if (String.IsNullOrEmpty(text)) return;
            const float width = 360;
            float height;
            using (StringFormat format = new StringFormat { Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.LineLimit })
                height = (float)Math.Ceiling(g.MeasureString(text, Typography.Font(TypeRole.Body, 17), new SizeF(width - 32, 10000), format).Height) + 28;
            height = Math.Max(78, Math.Min(370, height));
            float x = Math.Max(25, Math.Min(1575 - width, hoverPoint.X + 22));
            float y = hoverPoint.Y > 590 ? hoverPoint.Y - height - 18 : hoverPoint.Y + 26;
            y = Math.Max(107, Math.Min(929 - height, y));
            RectangleF box = new RectangleF(x, y, width, height);
            Art.Panel(g, box, Color.FromArgb(24, 36, 39), false);
            Art.Line(g, Art.Gold, 1, box.X + 14, box.Y, box.Right - 14, box.Y);
            Typography.Draw(g, text, new RectangleF(box.X + 16, box.Y + 14, box.Width - 32, box.Height - 28), 17, Art.Ink, TypeRole.Body);
        }

        private string BattleCellHint()
        {
            if (!BattleOverlayActive || battleHoverCell < 0 || battleHelpOpen && BattleHelpBounds.Contains(hoverPoint)) return "";
            BattleState battle = game.Battle;
            Cell cell = game.World.Cells[battleHoverCell];
            BattleFormation selected = SelectedBattleFormation;
            BattleFormation target = battle.Formations.FirstOrDefault(f => f.Alive && f.CellId == cell.Id);
            string terrain = cell.Terrain.ToString();
            if (target != null)
            {
                string text = target.Name + "\n" + target.Count + (target.SourceKind == UnitKind.Animal ? " animals" : " people") + " · " + target.Strength.ToString("0.0") + " strength · " + terrain +
                    "\nHealth: " + target.CurrentHealth + " / " + target.MaxHealth;
                if (target.Side == 0) return text + "\n" + (battle.Phase == BattlePhase.Deployment ? "Click to select, then choose a gold starting hex." : target.Actions + " / 3 actions" + (target.Defending ? " · Defending" : "") + "\nClick to select this formation.");
                if (battle.Phase == BattlePhase.Deployment) return text + "\nEnemy starting position.";
                if (selected == null) return text + "\nSelect your formation to preview an attack.";
                var preview = BattleRules.StrikePreview(game, selected.Id, target.Id);
                if (!preview.Allowed) return text + "\n" + preview.Reason;
                text += "\nStrike: " + preview.Damage.ToString("0") + " damage · " + preview.Retaliation.ToString("0") + " retaliation";
                string advantages = "";
                if (preview.HighGround) advantages += "High ground +20%. ";
                if (preview.ForestCover) advantages += "Forest cover −20%. ";
                if (preview.Flanking) advantages += "Flanking +20%. ";
                if (target.Defending) advantages += "Defending −35%. ";
                if (preview.RiverCrossing) advantages += "River attack −20%. ";
                return text + (advantages.Length > 0 ? "\n" + advantages.Trim() : "") + "\nClick to strike · 1 action. Damage applies immediately.";
            }
            if (battle.Phase == BattlePhase.Deployment)
            {
                bool ours = battle.PlayerDeploymentCells.Contains(cell.Id);
                return terrain + "\n" + (ours ? "Your deployment zone. Click to position the selected formation." : "Outside your deployment zone.");
            }
            string detail = cell.Terrain == Terrain.Forest ? "Forest cover reduces incoming damage by 20%." : cell.Terrain == Terrain.Mountains ? "Passable mountains · 3 movement actions." : cell.Terrain == Terrain.Hills ? "Higher ground can strengthen an attack." : cell.Terrain == Terrain.Wetland ? "Wet ground · 2 movement actions." : cell.Terrain == Terrain.Ice ? "Ice · 2 movement actions." : "Open ground · 1 movement action.";
            if (selected != null && game.World.Cells[selected.CellId].Neighbors.Contains(cell.Id))
            {
                int cost = BattleRules.MoveCost(game, selected.CellId, cell.Id);
                string river = TravelRules.HasRiver(game, selected.CellId, cell.Id) ? "River crossing · " : "";
                return terrain + "\n" + river + "Move: " + cost + (cost == 1 ? " action" : " actions") + " · " + selected.Actions + " available\n" +
                    (BattleRules.CanMove(game, selected.Id, cell.Id) ? "Click or right-click to move here." : "Not enough actions to move here this round.");
            }
            return terrain + "\n" + detail + "\nMovement is one adjacent hex at a time.";
        }
    }
}
