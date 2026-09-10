using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using Clio.Simulation;

namespace Clio.Desktop
{
    public sealed partial class GameForm
    {
        private static readonly RectangleF BattleHelpBounds = new RectangleF(41, 132, 360, 345);

        private void DrawBattle(Graphics g)
        {
            if (!BattleOverlayActive) return;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
            PrepareBattleView(); buttons.Clear(); g.Clear(Art.Background);
            Art.Panel(g, BattleMapBounds, Color.FromArgb(27, 46, 51), false);
            battleRenderer.Draw(g, game);
            DrawBattleHighlights(g);
            foreach (BattleFormation formation in game.Battle.Formations.Where(f => f.Alive).OrderBy(f => battleRenderer.Cell(f.CellId) == null ? 0 : battleRenderer.Cell(f.CellId).Center.Y))
                DrawBattleFormation(g, formation);
            DrawBattleHeader(g);
            DrawBattleDock(g);
            if (battleHelpOpen && game.Battle.Phase != BattlePhase.Finished) DrawBattleHelp(g);
            if (game.Battle.Phase == BattlePhase.Finished) DrawBattleResult(g);
            else DrawBattleHover(g);
        }

        private void DrawBattleHeader(Graphics g)
        {
            BattleState battle = game.Battle;
            Art.Fill(g, Art.Background, 0, 0, 1600, 104);
            Art.Icon(g, "conflict", 30, 24, 37, Art.Gold);
            Typography.Label(g, "Regional battle / Turn " + battle.Turn, new RectangleF(81, 13, 530, 23), 12, Art.Gold, .8f);
            Typography.Line(g, game.Place(battle.CenterCellId), new RectangleF(79, 36, 521, 43), 31, Art.Ink, TypeRole.Heading, true);
            string phase = battle.Phase == BattlePhase.Deployment ? "Deployment" : battle.Phase == BattlePhase.Finished ? "Battle concluded" : "Round " + battle.Round + " / " + battle.RoundLimit;
            Typography.Line(g, phase, new RectangleF(595, 18, 408, 34), 25, Art.Gold, TypeRole.Heading, true, StringAlignment.Center);
            Typography.Line(g, battle.PlayerAttacking ? "Your people are attacking" : "Your people are defending", new RectangleF(595, 57, 408, 25), 16, Art.Muted, TypeRole.Body, true, StringAlignment.Center);
            if (battle.Phase != BattlePhase.Finished)
            {
                Button(g, battleHelpOpen ? "Hide help" : "How to play [H]", 1080, 26, 165, 38, delegate { battleHelpOpen = !battleHelpOpen; Invalidate(); }, battleHelpOpen, false);
                MapTip("Read deployment, terrain, combat and retreat rules. No action is spent.");
            }
            if (autoplay)
            {
                Button(g, "Take control [P]", 1258, 26, 174, 38, delegate { StopAutoplay("You command the battle."); Invalidate(); }, true, false);
                MapTip("Pause automatic play and command the battle yourself.");
            }
            Button(g, "Save", 1445, 26, 105, 38, SaveStory, false, false);
            MapTip("Save the story with this exact battle state, including deployment and tactical orders [Ctrl+S].");
            Art.Line(g, Art.Border, 1, 24, 97, 1576, 97);
            DrawBattleSideLabel(g, battle.PlayerName, 39, 121, BattleFriendly, 0);
            DrawBattleSideLabel(g, battle.EnemyName, 1188, 121, BattleEnemy, 1);
        }

        private void DrawBattleSideLabel(Graphics g, string name, float x, float y, Color ink, int side)
        {
            BattleFormation[] formations = game.Battle.Formations.Where(f => f.Side == side).ToArray();
            Art.Fill(g, Color.FromArgb(225, Art.Background), x, y, 374, 34);
            Art.Icon(g, side == 0 ? "people" : "conflict", x + 10, y + 7, 20, ink);
            Typography.Line(g, name, new RectangleF(x + 38, y + 2, 255, 30), 19, ink, TypeRole.Heading, true);
            Typography.Line(g, formations.Sum(f => f.Count).ToString("N0"), new RectangleF(x + 300, y + 2, 65, 30), 22, Art.Ink, TypeRole.Number, true, StringAlignment.Far);
        }

        private void DrawBattleHighlights(Graphics g)
        {
            BattleState battle = game.Battle; BattleFormation selected = SelectedBattleFormation;
            if (battle.Phase == BattlePhase.Finished) return;
            if (battle.Phase == BattlePhase.Deployment)
            {
                foreach (int cell in battle.PlayerDeploymentCells) battleRenderer.Highlight(g, cell, BattleFriendly, 34, true);
                foreach (int cell in battle.EnemyDeploymentCells) battleRenderer.Highlight(g, cell, BattleEnemy, 13, true);
            }
            else if (selected != null)
            {
                foreach (int cell in battle.Cells.Where(c => BattleRules.CanMove(game, selected.Id, c)))
                    battleRenderer.Highlight(g, cell, Color.FromArgb(143, 211, 214), 38, false);
                foreach (BattleFormation target in battle.Formations.Where(f => f.Alive && f.Side == 1 && BattleRules.CanStrike(game, selected.Id, f.Id)))
                    battleRenderer.Highlight(g, target.CellId, BattleEnemy, 45, false);
            }
            if (selected != null) battleRenderer.Highlight(g, selected.CellId, BattleFriendly, 29, false);
            if (battleHoverCell >= 0) battleRenderer.Highlight(g, battleHoverCell, Art.Ink, 16, false);
        }

        private void DrawBattleFormation(Graphics g, BattleFormation formation)
        {
            ProjectedCell cell = battleRenderer.Cell(formation.CellId); if (cell == null) return;
            PointF center = cell.Center; Color ink = formation.Side == 0 ? BattleFriendly : BattleEnemy;
            bool selected = formation.Id == battleSelectedFormation;
            float size = Math.Max(.42f, Math.Min(.93f, cell.Radius / 71));
            using (Brush shade = new SolidBrush(Color.FromArgb(85, 6, 17, 15))) g.FillEllipse(shade, center.X - 30 * size, center.Y - 7 * size, 60 * size, 18 * size);
            using (Pen ring = new Pen(ink, selected ? 2.5f : 1.2f))
            { if (formation.Side == 1) ring.DashPattern = new float[] { 3, 2 }; g.DrawEllipse(ring, center.X - 30 * size, center.Y - 8 * size, 60 * size, 18 * size); }
            if (formation.SourceKind == UnitKind.Band)
            {
                Band band = game.Bands.FirstOrDefault(b => b.Id == formation.SourceId);
                if (band != null) UnitArt.DrawBandParty(g, band, center, size, selected, false, false, game.TribeOf(band.Id));
            }
            else
            {
                Beast animal = game.Beasts.FirstOrDefault(b => b.Id == formation.SourceId);
                if (animal != null) IdentityArt.DrawAnimal(g, animal.Kind, new RectangleF(center.X - 25 * size, center.Y - 34 * size, 50 * size, 32 * size), ink, animal.Domestic);
            }
            RectangleF badge = new RectangleF(center.X - 29, center.Y + 12, 58, 24);
            Art.Fill(g, Color.FromArgb(241, Art.Background), badge.X, badge.Y, badge.Width, badge.Height);
            Art.Line(g, ink, selected ? 2 : 1, badge.X, badge.Y, badge.Right, badge.Y);
            Typography.Line(g, formation.Count.ToString(), new RectangleF(badge.X + 5, badge.Y, 31, 24), 18, ink, TypeRole.Number, true);
            for (int i = 0; i < 3; i++) using (Brush dot = new SolidBrush(i < formation.Actions ? ink : Art.Border)) g.FillEllipse(dot, badge.Right - 14, badge.Y + 4 + i * 6, 4, 4);
            if (formation.Defending) Art.Icon(g, "camp", center.X - 9, center.Y - 62 * size, 18, ink);
        }

        private void DrawBattleDock(Graphics g)
        {
            BattleState battle = game.Battle;
            string last = battleLocalHint.Length > 0 ? battleLocalHint : battle.Phase == BattlePhase.Deployment ? "Select a formation, then click a gold deployment hex. Begin when ready." : battle.Log.LastOrDefault() ?? "Select a formation. Click a blue hex to move or an adjacent enemy to attack.";
            Typography.Line(g, last, new RectangleF(35, 796, 1530, 26), 16, Art.Muted, TypeRole.Body, true);
            Art.Panel(g, new RectangleF(24, 834, 1552, 106), Art.PanelColor, false);
            Typography.Label(g, "Your formations", new RectangleF(38, 839, 624, 20), 11, Art.Muted, .55f);
            BattleFormation[] own = battle.Formations.Where(f => f.Side == 0).OrderBy(f => f.Id).ToArray();
            float step = Math.Min(70, 622f / Math.Max(1, own.Length));
            for (int i = 0; i < own.Length; i++) DrawBattleRosterSlot(g, own[i], new RectangleF(38 + i * step, 866, step - 6, 61));
            BattleFormation selected = SelectedBattleFormation;
            if (selected != null)
            {
                Typography.Line(g, selected.Name, new RectangleF(685, 850, 281, 31), 23, Art.Ink, TypeRole.Heading, true);
                Typography.Line(g, selected.Count + (selected.SourceKind == UnitKind.Animal ? " animals" : " people") + " · " + selected.Strength.ToString("0.0") + " strength", new RectangleF(685, 884, 281, 24), 16, Art.Muted, TypeRole.Body, true);
                Typography.Line(g, battle.Phase == BattlePhase.Deployment ? "Choose a starting position" : selected.Actions + " / 3 actions" + (selected.Defending ? " · Defending" : ""), new RectangleF(685, 910, 281, 22), 15, Art.Gold, TypeRole.Body, true);
            }
            bool fighting = battle.Phase == BattlePhase.Fighting;
            BattleIconControl(g, "camp", new RectangleF(990, 866, 51, 51), fighting && selected != null && selected.Actions > 0,
                delegate { if (SelectedBattleFormation != null) IssueBattleOrder("battle-defend:" + SelectedBattleFormation.Id); },
                "Defend [D]. Spend this formation's remaining actions; incoming damage is reduced by 35% until its next round.");
            bool canRetreat = game.CanRetreatBattle();
            string retreatTip = battle.Phase == BattlePhase.Finished ? "This battle has ended." : canRetreat ?
                "Retreat. Concede the battle and move surviving units to safe campaign hexes. Retreat uses normal travel food; the resulting losses and positions apply immediately." :
                "Retreat unavailable: one or more surviving units have no safe, open route on the campaign map. Continue fighting or use Auto-resolve.";
            BattleIconControl(g, "move", new RectangleF(1051, 866, 51, 51), canRetreat,
                delegate { IssueBattleOrder("battle-retreat"); }, retreatTip);
            BattleTextControl(g, "Auto-resolve [A]", new RectangleF(1114, 866, 190, 51), battle.Phase != BattlePhase.Finished,
                delegate { IssueBattleOrder("battle-auto"); }, "Let the computer finish this battle using the same tactical rules. Casualties and the outcome are applied immediately.");
            BattleTextControl(g, battle.Phase == BattlePhase.Deployment ? "Begin battle" : battle.Phase == BattlePhase.Finished ? "View result" : "End round", new RectangleF(1320, 866, 236, 51), battle.Phase != BattlePhase.Finished,
                delegate { IssueBattleOrder(game.Battle.Phase == BattlePhase.Deployment ? "battle-start" : "battle-round"); },
                battle.Phase == BattlePhase.Deployment ? "Keep these positions and begin combat [Enter / Space]." : "Yield any unused actions. The enemy acts, then surviving formations receive 3 fresh tactical actions [Enter / Space].");
        }

        private void DrawBattleRosterSlot(Graphics g, BattleFormation formation, RectangleF box)
        {
            bool selected = formation.Id == battleSelectedFormation;
            Art.Panel(g, box, selected ? Color.FromArgb(67, 70, 49) : Art.PanelColor, false);
            if (selected) using (Pen edge = new Pen(Art.Gold, 1.4f)) g.DrawRectangle(edge, box.X, box.Y, box.Width, box.Height);
            Color ink = formation.Alive ? BattleFriendly : Art.Muted;
            Art.Icon(g, formation.SourceKind == UnitKind.Band ? "people" : "hunt", box.X + box.Width / 2 - 11, box.Y + 5, 22, ink);
            Typography.Line(g, formation.Count.ToString(), new RectangleF(box.X + 3, box.Y + 30, box.Width - 6, 25), 21, ink, TypeRole.Number, true, StringAlignment.Center);
            int id = formation.Id;
            buttons.Add(new UiButton(box, delegate { if (formation.Alive) SelectBattleFormation(id); }) { Tip = formation.Name + ". " + formation.Count + " of " + formation.StartingCount +
                " remain. " + (formation.Alive ? formation.Actions + " tactical actions available. Click to select. [N] selects the next ready formation." : "This formation has been lost.") });
        }

        private void BattleIconControl(Graphics g, string icon, RectangleF box, bool enabled, Action action, string tip)
        {
            Art.Panel(g, box, enabled && box.Contains(hoverPoint) ? Color.FromArgb(60, 67, 53) : Art.PanelColor, false);
            Art.Icon(g, icon, box.X + 13, box.Y + 13, 25, enabled ? Art.Gold : Art.Muted);
            buttons.Add(new UiButton(box, delegate { if (enabled) action(); }) { Tip = tip });
        }

        private void BattleTextControl(Graphics g, string label, RectangleF box, bool enabled, Action action, string tip)
        {
            EncounterAction(g, label, box, enabled, action, enabled);
            if (!enabled) buttons.Add(new UiButton(box, delegate { }));
            MapTip(tip);
        }

        private void DrawBattleHelp(Graphics g)
        {
            Art.Panel(g, BattleHelpBounds, Art.PanelColor, true);
            Typography.Line(g, "Battle essentials", new RectangleF(59, 145, 270, 35), 28, Art.Ink, TypeRole.Heading, true);
            Button(g, "×", 355, 146, 27, 27, delegate { battleHelpOpen = false; Invalidate(); }, false, false);
            string text = "Deployment: select your formation and click an empty gold hex.\n\nEach formation gets 3 actions per round. Open land costs 1; forest, wetland and ice 2; mountains and river crossings 3.\n\nAdjacent attacks cost 1 action. Higher ground, forest, flanking and defending affect damage. Hover before striking.";
            Typography.Draw(g, text, new RectangleF(59, 188, 324, 271), 17, Art.Ink, TypeRole.Body);
        }

        private void DrawBattleResult(Graphics g)
        {
            BattleState battle = game.Battle; var result = battle.Result;
            if (result == null) return;
            UiButton[] headerButtons = buttons.Where(b => b.Bounds.Bottom <= 97).ToArray();
            buttons.Clear(); buttons.AddRange(headerButtons);
            using (Brush shade = new SolidBrush(Color.FromArgb(161, 4, 13, 17))) g.FillRectangle(shade, 0, 100, 1600, 860);
            RectangleF panel = new RectangleF(350, 228, 900, 497);
            Art.Panel(g, panel, Art.PanelColor, true);
            Color ink = result.WinnerSide == 0 ? Art.Gold : result.WinnerSide == 1 ? BattleEnemy : Art.Ink;
            Art.Icon(g, result.Retreated ? "move" : "conflict", 775, 253, 48, ink);
            Typography.Line(g, result.Outcome.ToString(), new RectangleF(386, 313, 828, 52), 40, ink, TypeRole.Heading, true, StringAlignment.Center);
            Typography.Draw(g, result.Summary, new RectangleF(390, 377, 820, 74), 20, Art.Ink, TypeRole.Body);
            Art.Rule(g, 391, 468, 818);
            DrawBattleResultMetric(g, 390, "Your losses", result.PlayerLosses.ToString("N0"), BattleEnemy);
            DrawBattleResultMetric(g, 672, "Enemy losses", result.EnemyLosses.ToString("N0"), Art.Ink);
            DrawBattleResultMetric(g, 954, result.FoodRecipientSide == 1 ? "Enemy food recovered" : "Your food recovered",
                "+" + result.FoodRecovered.ToString("0"), result.FoodRecipientSide == 1 ? BattleEnemy : Art.Gold);
            Typography.Line(g, result.Rounds + (result.Rounds == 1 ? " tactical round" : " tactical rounds") + " · Campaign consequences remain in History", new RectangleF(393, 572, 814, 26), 16, Art.Muted, TypeRole.Body, true, StringAlignment.Center);
            Button(g, "Save", 391, 646, 142, 45, SaveStory, false, false);
            Button(g, "Return to campaign", 553, 646, 655, 45, delegate { IssueBattleOrder("battle-close"); }, true, false);
        }

        private void DrawBattleResultMetric(Graphics g, float x, string label, string value, Color ink)
        {
            Typography.Label(g, label, new RectangleF(x, 485, 253, 23), 12, Art.Muted, .5f, StringAlignment.Center);
            Typography.Line(g, value, new RectangleF(x, 513, 253, 45), 36, ink, TypeRole.Number, true, StringAlignment.Center);
        }
    }
}
