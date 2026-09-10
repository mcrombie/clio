using System;
using System.Drawing;
using Clio.Simulation;

namespace Clio.Desktop
{
    public sealed partial class GameForm
    {
        private void OpenWoodEconomy()
        { ClearMapTransient(); page = 1; economyPage = 2; economyResourcePage = 2; Invalidate(); }

        private void GatherHouseholdWood()
        {
            if (SemiautomaticMode)
            {
                status = "Your bands gather supplies automatically. Choose a wood-focused decision to give fuel and shelter priority.";
                OpenWoodEconomy(); return;
            }
            Band band = CurrentOrderBand;
            if (!WoodEconomy.CanGather(game, band)) { OpenWoodEconomy(); return; }
            ArmMapCommandBand(band.Id); CloseMapMenus(); Command("wood");
        }

        private string WoodOrderTip()
        {
            if (!game.WoodEnabled) return "Wood resources [W]\nOpen Resources to add wood to this older story.";
            Band band = CurrentOrderBand;
            string tip = "Gather wood [W] / 1 action\nReserves: " + band.Wood.ToString("0.0") +
                " wood. Gather here: +" + WoodEconomy.GatherYield(game, band).ToString("0.0") +
                ". A fire uses " + WoodEconomy.FuelNeed(band).ToString("0.0") + " each turn.\n" +
                "A supplied fire reduces food needs by 10% and prevents exposure deaths. Building a camp costs " + WoodEconomy.CampCost + " wood.";
            if (!HasCommandBand) return tip + "\nSelect your band first. Click to open its wood account.";
            if (CurrentOrderActions <= 0) return tip + "\nNo actions left. Click to open the wood account.";
            if (WoodEconomy.GatherYield(game, band) <= 0) return tip + "\nNo wood can be gathered here. Click for details.";
            return tip;
        }

        private static void DrawWoodGlyph(Graphics g, RectangleF bounds, Color ink)
        {
            float scale = bounds.Width / 28;
            using (Pen edge = new Pen(ink, Math.Max(.8f, scale)))
            using (Brush wash = new SolidBrush(Color.FromArgb(35, ink)))
            {
                for (int i = 0; i < 3; i++)
                {
                    float x = bounds.X + (i == 1 ? 6 : 2) * scale;
                    float y = bounds.Y + (3 + i * 8) * scale;
                    float length = 19 * scale, height = 5 * scale;
                    g.FillRectangle(wash, x, y, length, height);
                    g.DrawLine(edge, x, y, x + length, y);
                    g.DrawLine(edge, x, y + height, x + length, y + height);
                    g.DrawArc(edge, x - 2 * scale, y, 4 * scale, height, 90, 180);
                    g.DrawEllipse(edge, x + length - 2 * scale, y, 4 * scale, height);
                    g.DrawLine(edge, x + 4 * scale, y + 2 * scale, x + 10 * scale, y + 2 * scale);
                }
            }
        }

        private void DrawWoodResources(Graphics g)
        {
            if (!game.WoodEnabled) { DrawUntrackedWood(g); return; }
            Band band = CurrentOrderBand;
            EconomyForecast forecast = BandEconomy.Forecast(game, band);
            double yield = WoodEconomy.GatherYield(game, band), need = WoodEconomy.FuelNeed(band);
            bool supplied = WoodEconomy.HasFuel(game, band);
            LedgerMetric(g, 42, "Wood reserves", band.Wood.ToString("N1"), "Carried by the selected band", Art.Gold);
            LedgerMetric(g, 348, "Gather per action", "+" + yield.ToString("0.0"), "At this band's current place", Art.Ink);
            LedgerMetric(g, 654, "Fuel each turn", need.ToString("0.0"), "Two wood for every fifty people", Art.Ink);
            LedgerMetric(g, 960, "Food saved this turn", forecast.FireFoodSaved.ToString("0.0"), "Supplied fire reduces food needs by 10%", supplied ? LedgerGreen : Art.Muted);
            LedgerMetric(g, 1266, "Wood after this turn", forecast.EndingWood.ToString("0.0"), "After the fire's fuel expense", supplied ? LedgerGreen : SaltWarning);

            EconomyPanel(g, 42, 484, "Wood account");
            EconomyAmount(g, 63, 465, 442, "Starting wood", forecast.StartingWood, false);
            EconomyAmount(g, 63, 501, 442, "Fuel used this turn", -forecast.WoodConsumed, true);
            EconomyAmount(g, 63, 537, 442, "Wood after this turn", forecast.EndingWood, false);
            Art.Rule(g, 63, 583, 441);
            Typography.Line(g, supplied ? "Enough fuel for a fire" : "Not enough fuel for a fire", new RectangleF(63, 600, 442, 37), 27,
                supplied ? LedgerGreen : SaltWarning, TypeRole.Heading, true);
            Typography.Draw(g, "Each band carries and uses its own wood. A new band takes its share of the reserve when it forms.", new RectangleF(63, 654, 442, 71), 19, Art.Ink, TypeRole.Body);
            Typography.Line(g, "No fuel is spent until the turn ends.", new RectangleF(63, 735, 442, 27), 16, Art.Muted, TypeRole.Annotation, true);

            EconomyPanel(g, 542, 486, "Gathering wood");
            DrawWoodGlyph(g, new RectangleF(563, 468, 39, 39), Art.Gold);
            Typography.Line(g, game.World.Cells[band.CellId].Terrain.ToString(), new RectangleF(619, 458, 387, 49), 29, Art.Ink, TypeRole.Heading, true);
            Typography.Draw(g, "Gather " + yield.ToString("0.0") + " wood for one action here. Forests provide more; bare ground provides less. Wood gathering is separate from food gathering.", new RectangleF(563, 521, 444, 92), 19, Art.Ink, TypeRole.Body);
            if (SemiautomaticMode)
                Typography.Draw(g, "Semiautomatic: your bands manage supplies. Choose a wood-focused story option to prioritize fuel and shelter.", new RectangleF(563, 630, 444, 89), 19, Art.Gold, TypeRole.Body);
            else if (WoodEconomy.CanGather(game, band))
            {
                Button(g, "Gather wood  +" + yield.ToString("0.0") + "  [W]", 563, 633, 444, 42, GatherHouseholdWood, true, false);
                MapTip("Use one action from " + band.Name + " to gather wood here. Fuel is used automatically when the turn ends.");
            }
            else Typography.Draw(g, game.IsOver ? "This band's story has ended." : yield <= 0 ? "Move to land with wood before gathering." : "This band has no actions left. End the turn or choose another band.", new RectangleF(563, 631, 444, 75), 19, Art.Muted, TypeRole.Body);
            Typography.Draw(g, "Wood travels with the band. You do not need a camp to use a fire.", new RectangleF(563, 721, 444, 45), 16, Art.Muted, TypeRole.Annotation);

            EconomyPanel(g, 1044, 514, "What wood does");
            EconomyRow(g, 1065, 465, 472, "Food needs before fire", forecast.BaseUpkeep.ToString("0.0"), Art.Ink);
            EconomyRow(g, 1065, 501, 472, "Food needs with current fuel", forecast.Upkeep.ToString("0.0"), supplied ? LedgerGreen : Art.Ink);
            EconomyRow(g, 1065, 537, 472, "Exposure protection", forecast.FireProtection ? "Active" : "No fire", forecast.FireProtection ? LedgerGreen : Art.Muted);
            Art.Rule(g, 1065, 583, 471);
            Typography.Label(g, "Building a camp", new RectangleF(1065, 600, 472, 25), 12, Art.Gold, .5f);
            Typography.Draw(g, "A camp costs 1 action, 30 food and " + WoodEconomy.CampCost + " wood. It produces food each turn while the band stays. Fuel is paid separately.", new RectangleF(1065, 637, 472, 82), 19, Art.Ink, TypeRole.Body);
            Typography.Draw(g, "Without wood, you lose fire benefits; wood shortage itself does not kill people.", new RectangleF(1065, 726, 472, 42), 16, Art.Muted, TypeRole.Annotation);
            Typography.Line(g, "Your people: gathered " + journal.Totals.WoodGathered.ToString("N1") + " wood  /  fuel " + journal.Totals.WoodConsumed.ToString("N1") + "  /  camps " + journal.Totals.CampWoodSpent.ToString("N1") + "  /  food saved " + journal.Totals.FireFoodSaved.ToString("N1"),
                new RectangleF(42, 782, 1516, 25), 16, Art.Muted, TypeRole.Utility, true);
        }

        private void DrawUntrackedWood(Graphics g)
        {
            Art.Panel(g, new RectangleF(42, 305, 1516, 469), Panel, false);
            DrawWoodGlyph(g, new RectangleF(107, 405, 148, 148), Art.Gold);
            Typography.Label(g, "Older story", new RectangleF(323, 363, 1124, 29), 13, Art.Gold, .8f);
            Typography.Line(g, "Wood is not tracked yet", new RectangleF(319, 410, 1129, 60), 42, Art.Ink, TypeRole.Heading, true);
            Typography.Draw(g, "Wood supplies fires and camp construction. A supplied fire reduces food needs by 10% and prevents exposure deaths. Forests offer the best gathering.", new RectangleF(323, 489, 1008, 89), 24, Art.Ink, TypeRole.Body);
            Button(g, "Enable wood", 325, 610, 299, 45, delegate { Command("enable-wood"); OpenWoodEconomy(); }, true, false);
            Typography.Draw(g, "Food and salt remain essential. Wood adds useful protection and efficiency; its shortage causes no separate death penalty.", new RectangleF(325, 693, 1116, 57), 18, Art.Muted, TypeRole.Body);
        }
    }
}
