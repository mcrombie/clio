using System;
using System.Drawing;
using System.Linq;
using Clio.Simulation;

namespace Clio.Desktop
{
    public sealed partial class GameForm
    {
        private void DrawOverviewBar(Graphics g)
        {
            Art.Panel(g, new RectangleF(16, 102, 1568, 75), Panel, false);
            IdentityArt.DrawEmblem(g, game.TribeOf(CurrentOrderBand.Id), new RectangleF(31, 115, 46, 46), false);
            Typography.Line(g, CurrentOrderBand.Name, new RectangleF(93, 110, 325, 36), 31, Art.Ink, TypeRole.Heading, true);
            Typography.Line(g, CurrentOrderBand.Ancestry + " · " + (CurrentOrderBand.Settled ? "Established hearth" : "Wandering people"), new RectangleF(96, 146, 319, 23), 15, Art.Muted, TypeRole.Annotation);
            Art.Line(g, Border, 1, 433, 118, 433, 162);
            double needs = game.Upkeep(CurrentOrderBand) + BandEconomy.DomesticEffects(game, CurrentOrderBand).AnimalCare;
            double security = needs <= 0 ? 0 : CurrentOrderBand.Food / needs;
            OverviewMetric(g, "People", CurrentOrderBand.Population.ToString("N0"), 452, 127, Art.Ink);
            OverviewMetric(g, "Food reserves", Math.Floor(CurrentOrderBand.Food).ToString("N0"), 596, 151, Art.Gold);
            buttons.Add(new UiButton(new RectangleF(589, 110, 160, 57), delegate { OpenEconomy(4); }) { Tip = "Food held by the selected band. Its people and companions need " + needs.ToString("0.#") + " food each turn. Open Finance for its forecast and the tribe's total reserves." });
            OverviewMetric(g, "Food security", security.ToString("0.0") + "× needs", 765, 144, security < 1 ? Color.FromArgb(221, 145, 113) : Art.Ink);
            OverviewMetric(g, "Cohesion", (CurrentOrderBand.Cohesion * 100).ToString("0") + "%", 928, 125, Art.Ink);
            int lineages = game.Beasts.Count(b => b.Domestic && b.OwnerId == CurrentOrderBand.Id && b.Count > 0);
            OverviewMetric(g, "Domestic lineages", lineages.ToString(), 1070, 146, lineages > 0 ? Art.Gold : Art.Muted);
            Button(g, "Events  " + UnreadNoticeCount(), 1240, 122, 142, 34, delegate { page = 4; storyChoicesVisible = false; chronicleEvents = true; noticeOffset = 0; Invalidate(); }, false, false);
            DrawAdviserEntry(g, new RectangleF(1400, 122, 166, 34));
        }
        private void OverviewMetric(Graphics g, string label, string value, float x, float width, Color color)
        {
            Typography.Label(g, label, new RectangleF(x, 111, width, 23), 10.5f, Art.Muted, 0.6f);
            Typography.Line(g, value, new RectangleF(x - 2, 132, width + 2, 34), 29, color, TypeRole.Number, true);
        }
        private void DrawCommandDock(Graphics g)
        {
            if (semiautomatic) { DrawSemiautomaticPageDock(g); return; }
            Art.Panel(g, new RectangleF(16, 813, 1568, 106), Panel, true);
            Typography.Label(g, game.IsOver ? "The story remains in your history" : "Orders for " + CurrentOrderBand.Name + "  ·  " + CurrentOrderActions + " priorities remaining", new RectangleF(34, 822, 905, 23), 11.5f, Art.Gold, 0.65f);
            ActionButton(g, "Gather food", "F", "leaf", 32, 857, 160, delegate { Command("forage"); }, false);
            ActionButton(g, "Move / meet", "M", "move", 200, 857, 164, MoveSelection, false);
            ActionButton(g, game.Rules == SimulationRules.MobileUnits ? "Attack" : "Hunt", "A", "hunt", 372, 857, 108, ChooseAttack, false);
            ActionButton(g, "Befriend", "B", "heart", 488, 857, 128, ChooseBefriend, false);
            ActionButton(g, "Make camp", "", "camp", 624, 857, 132, delegate { Command("camp"); }, false);
            ActionButton(g, game.TribesEnabled ? "Form a new band" : "Daughter band", "", "branch", 764, 857, 178, delegate { Command("split"); }, false);
            Art.Line(g, Border, 1, 960, 830, 960, 902);
            Button(g, AutomaticMode ? "Automatic / Mode" : "Manual / Mode", 977, 826, 183, 34, OpenPlayMode, AutomaticMode, false);
            for (int i = 0; i < AutoplaySpeeds.Length; i++)
            {
                int speed = i;
                Button(g, AutoplaySpeeds[i], 977 + i * 64, 870, 55, 29, delegate { SetAutoplaySpeed(speed); }, autoplaySpeed == i, false);
            }
            Button(g, pauseOnEvents ? "Autoplay events: pause" : "Autoplay events: flow", 1178, 826, 193, 34, delegate { pauseOnEvents = !pauseOnEvents; Invalidate(); }, pauseOnEvents, false);
            Typography.Line(g, pauseOnEvents ? "Stop for major developments" : "Keep watching; review any event", new RectangleF(1178, 868, 193, 32), 14, Art.Muted, TypeRole.Annotation, true);
            Button(g, AutomaticMode ? "Take control [P]" : "End turn  [Space]", 1388, 826, 180, 73,
                delegate { if (AutomaticMode) ToggleAutoplay(); else Command("end"); }, true, false);
        }
    }
}
