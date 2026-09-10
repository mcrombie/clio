using System;
using System.Drawing;
using System.Linq;
using Clio.Simulation;

namespace Clio.Desktop
{
    public sealed partial class GameForm
    {
        private int economyPage, economyContactPage, economyLineagePage;

        private void OpenEconomy(int subpage)
        { ClearMapTransient(); page = 1; economyPage = Math.Max(0, Math.Min(4, subpage)); Invalidate(); }

        private void ResetEconomyPage()
        { economyPage = economyContactPage = economyLineagePage = economyResourcePage = saltSourcePage = 0; }

        private void DrawEconomy(Graphics g)
        {
            Surface(g, "The keeping of a people", "");
            string[] titles = { "Overview", "Demographics", "Resources", "Trade", "Finance" };
            float[] widths = { 145, 184, 149, 120, 133 };
            float x = 42;
            for (int i = 0; i < titles.Length; i++)
            {
                int tab = i;
                Button(g, titles[i], x, 262, widths[i], 32, delegate { OpenEconomy(tab); }, economyPage == i, false);
                x += widths[i] + 8;
            }
            if (economyPage == 2) DrawResourceTabs(g);
            else Typography.Line(g, (economyPage == 1 && game.TribesEnabled ? "Your tribe" : CurrentOrderBand.Name) + "  /  " + Timeline.Label(game, game.Turn), new RectangleF(1065, 262, 491, 31), 17, Art.Muted, TypeRole.Annotation, true, StringAlignment.Far);
            if (economyPage == 1) DrawEconomicDemographics(g);
            else if (economyPage == 2) DrawEconomicResources(g);
            else if (economyPage == 3) DrawEconomicTrade(g);
            else if (economyPage == 4) DrawEconomicFinance(g);
            else DrawEconomicOverview(g);
        }

        private bool EconomyActive { get { return CurrentOrderBand.Population > 0 && !game.IsOver; } }
        private double EconomyCurrentCapacity()
        { return game.TribesEnabled ? game.ControlledBands.Sum(b => b.Food) : game.Player.Food; }
        private double EconomyPeopleSupplied(EconomyForecast forecast)
        { return EconomyActive ? Math.Min(forecast.Upkeep, Math.Max(0, forecast.StartingFood + forecast.CampFood + forecast.CattleFood - forecast.ActualAnimalCare)) : 0; }
        private double EconomyRecordedBalance()
        { return (journal.Timeline.Count == 0 ? EconomyCurrentCapacity() : journal.Timeline[0].Food) + journal.Totals.FoodGained - journal.Totals.FoodSpent; }
        private int EconomyEncounterDeaths()
        { return Math.Max(0, journal.Totals.Deaths - journal.Totals.HungerDeaths - journal.Totals.ExposureDeaths - journal.Totals.SaltDeaths); }
        private Band[] EconomyKnownPartners()
        { return game.Bands.Where(b => b.Id != game.Player.Id && (!game.TribesEnabled || !game.CanControlBand(b.Id)) && b.Population > 0 && game.Explored.Contains(b.CellId)).OrderBy(b => b.Id).ToArray(); }
        private Beast[] EconomyCompanions()
        { return game.Beasts.Where(b => b.Domestic && b.OwnerId == CurrentOrderBand.Id && b.Count > 0).OrderBy(b => b.Kind).ThenBy(b => b.Id).ToArray(); }
        private string EconomySecurity()
        {
            Band own = CurrentOrderBand;
            double need = game.Upkeep(own) + BandEconomy.DomesticEffects(game, own).AnimalCare;
            return need <= 0 ? "\u2014" : (own.Food / need).ToString("0.0") + "\u00d7 needs";
        }
        private string EconomyForecastNote()
        { return !EconomyActive ? "This household's story has ended; no further upkeep is resolved." : game.TribesEnabled ? CurrentOrderBand.Name + ": current conditions before other units act. Further orders and encounters can change this forecast." : game.Rules == SimulationRules.MobileUnits ? "Current conditions, before other units act. Movement and encounters can change this forecast." : "Current conditions. Your remaining actions can change this forecast."; }

        private void EconomyPanel(Graphics g, float x, float width, string heading)
        {
            Art.Panel(g, new RectangleF(x, 411, width, 363), Panel, false);
            Typography.Label(g, heading, new RectangleF(x + 18, 424, width - 36, 24), 13, Art.Gold, .8f);
        }
        private void EconomyRow(Graphics g, float x, float y, float width, string label, string value, Color color)
        {
            Typography.Line(g, label, new RectangleF(x, y, width * .70f, 27), 16, Art.Muted, TypeRole.Body, true);
            Typography.Line(g, value, new RectangleF(x + width * .71f, y, width * .29f, 27), 21, color, TypeRole.Number, true, StringAlignment.Far);
        }
        private void EconomyAmount(Graphics g, float x, float y, float width, string label, double value, bool signed)
        { EconomyRow(g, x, y, width, label, (signed && value > 0 ? "+" : "") + value.ToString("0.0"), value > 0 && signed ? LedgerGreen : Art.Ink); }

        private void DrawEconomicOverview(Graphics g)
        {
            Band own = CurrentOrderBand; EconomyForecast forecast = BandEconomy.Forecast(game, own);
            DomesticEconomy domestic = BandEconomy.DomesticEffects(game, own);
            LedgerMetric(g, 42, "Living people", own.Population.ToString("N0"), "In the selected household", Art.Ink);
            LedgerMetric(g, 348, "Food reserves", own.Food.ToString("N1"), "Food held by the selected band", Art.Gold);
            LedgerMetric(g, 654, "Food security", EconomySecurity(), "People's needs and animal care", Art.Gold);
            LedgerMetric(g, 960, "Companions", EconomyCompanions().Sum(b => b.Count).ToString("N0"), EconomyCompanions().Length + " living domestic lineages", LedgerGreen);
            LedgerMetric(g, 1266, "Known households", EconomyKnownPartners().Length.ToString("N0"), "Other living bands in explored land", Art.Ink);

            EconomyPanel(g, 42, 492, "I  /  The next turn");
            Typography.Line(g, forecast.NetFood.ToString("+0.0;-0.0;0.0"), new RectangleF(60, 458, 453, 60), 44, forecast.NetFood >= 0 ? LedgerGreen : Art.Gold, TypeRole.Number, true);
            Typography.Line(g, "Projected change in food reserves", new RectangleF(63, 518, 448, 29), 18, Art.Muted, TypeRole.Annotation);
            EconomyAmount(g, 63, 568, 448, "Food after upkeep", forecast.EndingFood, false);
            EconomyRow(g, 63, 603, 448, "Projected upkeep losses", (forecast.HungerLosses + forecast.ExposureLosses + forecast.SaltLosses).ToString("N0"), Art.Ink);
            Typography.Draw(g, EconomyForecastNote(), new RectangleF(63, 649, 448, 61), 17, Art.Muted, TypeRole.Annotation);
            Button(g, "Read the accounts", 63, 726, 448, 32, delegate { OpenEconomy(4); }, false, false);

            EconomyPanel(g, 550, 492, "II  /  How the household is fed");
            EconomyAmount(g, 571, 464, 449, "Food gathered per action", game.ForageYield(own.CellId, own), true);
            EconomyAmount(g, 571, 504, 449, "Hearth production, per turn", EconomyActive ? forecast.CampFood : 0, true);
            EconomyAmount(g, 571, 544, 449, game.LivestockEnabled ? "Milk from livestock, per turn" : "Cattle produce, per turn", EconomyActive ? domestic.CattleFood : 0, true);
            Art.Rule(g, 571, 589, 448);
            Typography.Draw(g, own.Settled ? "Your hearth adds production at the end of each turn. Gathering still uses an action and depletes the ground." : "Your people are travelling. An established hearth can add production when the turn ends.", new RectangleF(571, 611, 448, 86), 19, Art.Ink, TypeRole.Annotation);
            Button(g, "Food and salt", 571, 726, 219, 32, delegate { economyResourcePage = 0; OpenEconomy(2); }, false, false);
            Button(g, "Wood and fire", 798, 726, 221, 32, OpenWoodEconomy, false, false);

            EconomyPanel(g, 1058, 500, "III  /  The living record");
            EconomyRow(g, 1079, 464, 458, "Recorded births", journal.Totals.Births.ToString("N0"), LedgerGreen);
            EconomyRow(g, 1079, 504, 458, "Recorded deaths", journal.Totals.Deaths.ToString("N0"), Art.Ink);
            EconomyRow(g, 1079, 544, 458, game.TribesEnabled ? "People departing the tribe" : "People founding daughter bands", journal.Totals.PopulationDeparted.ToString("N0"), Art.Ink);
            Art.Rule(g, 1079, 589, 458);
            Typography.Draw(g, game.TribesEnabled ? "The living record follows your whole tribe. The forecasts and resources beside it describe the selected household." : "These accounts follow your household. Independent peoples keep their own lives, provisions and choices.", new RectangleF(1079, 611, 458, 86), 20, Art.Ink, TypeRole.Annotation);
            Button(g, "Follow the population", 1079, 726, 458, 32, delegate { OpenEconomy(1); }, false, false);
        }

        private void DrawEconomicDemographics(Graphics g)
        {
            var totals = journal.Totals; EconomyForecast forecast = BandEconomy.Forecast(game, CurrentOrderBand);
            LedgerMetric(g, 42, "Living people", (game.TribesEnabled ? game.TribePopulation : game.Player.Population).ToString("N0"), game.TribesEnabled ? "Across all living bands in your tribe" : "Current, including this turn's actions", Art.Ink);
            LedgerMetric(g, 348, "Recorded births", totals.Births.ToString("N0"), "Members added through growth", LedgerGreen);
            LedgerMetric(g, 654, "Recorded deaths", totals.Deaths.ToString("N0"), "All recorded causes, counted once", Art.Ink);
            LedgerMetric(g, 960, "Departed alive", totals.PopulationDeparted.ToString("N0"), game.TribesEnabled ? "People who left tribal membership" : "People sent with daughter bands", Art.Gold);
            LedgerMetric(g, 1266, "Daughter bands", totals.DaughterBandsFounded.ToString("N0"), "New households you have founded", LedgerGreen);
            DrawPopulationLedger(g, true);

            EconomyPanel(g, 798, 355, "II  /  Causes and conditions");
            EconomyRow(g, 818, 456, 315, "Hunger deaths", totals.HungerDeaths.ToString("N0"), Art.Ink);
            EconomyRow(g, 818, 484, 315, "Exposure deaths", totals.ExposureDeaths.ToString("N0"), Art.Ink);
            EconomyRow(g, 818, 512, 315, "Encounter deaths", EconomyEncounterDeaths().ToString("N0"), Art.Ink);
            EconomyRow(g, 818, 540, 315, "Salt shortage deaths", totals.SaltDeaths.ToString("N0"), Art.Ink);
            Art.Rule(g, 818, 579, 314);
            Typography.Label(g, game.TribesEnabled ? "Selected household forecast" : "Current turn forecast", new RectangleF(818, 590, 315, 24), 12, Art.Gold, .5f);
            EconomyRow(g, 818, 621, 315, "New members", forecast.Births.ToString("N0"), LedgerGreen);
            EconomyRow(g, 818, 653, 315, "Projected upkeep losses", (forecast.HungerLosses + forecast.ExposureLosses + forecast.SaltLosses).ToString("N0"), Art.Ink);
            Typography.Draw(g, EconomyForecastNote(), new RectangleF(818, 704, 315, 59), 15.5f, Art.Muted, TypeRole.Annotation);

            EconomyPanel(g, 1169, 389, "III  /  Other known households");
            Band[] known = EconomyKnownPartners();
            Typography.Line(g, known.Sum(b => b.Population).ToString("N0") + " people observed", new RectangleF(1187, 460, 352, 36), 26, Art.Ink, TypeRole.Heading, true);
            Typography.Draw(g, "A current count in explored places; it is separate from your " + (game.TribesEnabled ? "tribe's" : "household's") + " population history.", new RectangleF(1187, 507, 352, 63), 17, Art.Muted, TypeRole.Annotation);
            economyContactPage = Math.Min(economyContactPage, Math.Max(0, (known.Length - 1) / 3));
            for (int i = 0; i < 3 && economyContactPage * 3 + i < known.Length; i++)
            {
                Band band = known[economyContactPage * 3 + i]; float y = 588 + i * 43;
                BandLink(g, band, new RectangleF(1187, y, 270, 33), true);
                Typography.Line(g, band.Population.ToString("N0"), new RectangleF(1462, y, 77, 33), 21, Art.Ink, TypeRole.Number, true, StringAlignment.Far);
            }
            if (known.Length > 3) LedgerPager(g, 1187, 732, 352, economyContactPage, known.Length, 3, delegate(int next) { economyContactPage = next; });
            else Typography.Line(g, known.Length == 0 ? "No other living household is in sight." : "Select a household to inspect its people.", new RectangleF(1187, 732, 352, 27), 16, Art.Muted, TypeRole.Annotation, true);
        }

        private void DrawEconomicResources(Graphics g)
        {
            if (economyResourcePage == 1) { DrawSaltResources(g); return; }
            if (economyResourcePage == 2) { DrawWoodResources(g); return; }
            Band own = CurrentOrderBand; Cell cell = game.World.Cells[own.CellId];
            DomesticEconomy effects = BandEconomy.DomesticEffects(game, own);
            EconomyForecast forecast = BandEconomy.Forecast(game, own); Beast[] companions = EconomyCompanions();
            LedgerMetric(g, 42, "Gather here", "+" + game.ForageYield(own.CellId, own).ToString("N0"), "Food gathered per action", Art.Gold);
            LedgerMetric(g, 348, "Ground recovery", ((1 - game.Depletion[own.CellId]) * 100).ToString("0") + "%", "At your household's current place", LedgerGreen);
            LedgerMetric(g, 654, "Hearth production", (EconomyActive ? forecast.CampFood : 0).ToString("0.0"), "Per turn, while a hearth is established", Art.Gold);
            LedgerMetric(g, 960, game.LivestockEnabled ? "Milk per turn" : "Cattle production", effects.CattleFood.ToString("0.0"), game.LivestockEnabled ? effects.Cattle + " cattle · " + effects.Goats + " goats" : effects.Cattle + " cattle across your lineages", LedgerGreen);
            LedgerMetric(g, 1266, "Animal care", effects.AnimalCare.ToString("0.0"), "Food per turn for this band's companions", Art.Ink);

            EconomyPanel(g, 42, 482, "I  /  The gathering calculation");
            EconomyRow(g, 62, 460, 441, "Ground's base yield", (12 + cell.Forage * 100).ToString("0.00"), Art.Ink);
            EconomyRow(g, 62, 488, 441, "Prevailing conditions", "\u00d7 " + game.SeasonFactor.ToString("0.00"), Art.Ink);
            EconomyRow(g, 62, 516, 441, "Ancestry's adaptation", "\u00d7 " + Game.Adaptation(own.Ancestry, cell).ToString("0.00"), Art.Ink);
            EconomyRow(g, 62, 544, 441, "Ground after depletion", "\u00d7 " + (1 - game.Depletion[own.CellId] * .8).ToString("0.00"), Art.Ink);
            EconomyRow(g, 62, 572, 441, "Gathering knowledge", game.Known("gathering") ? "\u00d7 1.15" : "\u00d7 1.00", Art.Ink);
            EconomyRow(g, 62, 600, 441, "People available (cap 2.50)", "\u00d7 " + Math.Min(2.5, own.Population / 50.0).ToString("0.00"), Art.Ink);
            EconomyRow(g, 62, 628, 441, "Salt supply", game.SaltEnabled ? "\u00d7 " + SaltEconomy.GatheringMultiplier(game, own).ToString("0.00") : "Not tracked", game.SaltEnabled && own.SaltShortageTurns > 0 ? SaltWarning : Art.Ink);
            EconomyRow(g, 62, 656, 441, "Companion assistance", "\u00d7 " + effects.GatheringMultiplier.ToString("0.00"), LedgerGreen);
            Art.Rule(g, 62, 691, 440);
            Typography.Draw(g, "Multiply the factors through salt and round; then apply companion aid and round again. Gathering depletes this ground.", new RectangleF(62, 709, 441, 53), 16, Art.Muted, TypeRole.Annotation);

            EconomyPanel(g, 540, 484, "II  /  Hearth and reserve");
            Typography.Line(g, own.Settled ? "An established hearth" : "A travelling household", new RectangleF(560, 463, 444, 40), 30, Art.Ink, TypeRole.Heading, true);
            Typography.Draw(g, "An established hearth produces " + (game.Known("gardens") ? "75%" : "30%") + " of the current gathering yield each turn. Gardens increase this share.", new RectangleF(560, 517, 444, 69), 19, Art.Ink, TypeRole.Annotation);
            Art.Rule(g, 560, 603, 444);
            EconomyRow(g, 560, 617, 444, "People's needs this turn", game.Upkeep(own).ToString("0.0"), Art.Ink);
            EconomyRow(g, 560, 650, 444, "Reserve retained after needs", game.Known("stores") ? "98%" : "95%", LedgerGreen);
            Typography.Draw(g, game.WoodEnabled ? "A supplied fire reduces people's food needs by 10%. Wood & fire shows the fuel account. Storage knowledge cuts spoilage from 5% to 2%." : "Cold places increase people's needs. Storage knowledge reduces the reserve lost after upkeep from 5% to 2%.", new RectangleF(560, 703, 444, 58), 17, Art.Muted, TypeRole.Annotation);

            EconomyPanel(g, 1040, 518, "III  /  Companion lineages");
            if (companions.Length == 0)
            {
                Art.Icon(g, "leaf", 1257, 489, 46, Art.Gold);
                Typography.Draw(g, "No domestic lineages yet.", new RectangleF(1061, 563, 476, 43), 29, Art.Ink, TypeRole.Heading);
                Typography.Draw(g, game.LivestockEnabled ? "Dogs support hunting. Cattle and goats provide milk, or meat when slaughtered. Deer cannot be domesticated. Inspect a group to read its benefits and care costs." : "Inspect a known animal group to learn what a peaceful approach requires. Established companions can add food, gathering aid or strength, with care paid each turn.", new RectangleF(1061, 626, 476, 105), 19, Art.Muted, TypeRole.Annotation);
            }
            else
            {
                economyLineagePage = Math.Min(economyLineagePage, (companions.Length - 1) / 3);
                for (int i = 0; i < 3 && economyLineagePage * 3 + i < companions.Length; i++)
                {
                    Beast herd = companions[economyLineagePage * 3 + i]; float y = 466 + i * 81;
                    IdentityArt.DrawAnimal(g, herd.Kind, new RectangleF(1061, y + 5, 36, 31), Art.Ink, true);
                    Typography.Line(g, herd.Count + "  " + (game.LivestockEnabled ? LivestockEconomy.DisplayName(game, herd) : herd.BreedName), new RectangleF(1112, y, 424, 31), 23, Art.Ink, TypeRole.Heading, true);
                    Typography.Line(g, DomesticLineageBenefit(herd), new RectangleF(1112, y + 33, 424, 29), 16, Art.Muted, TypeRole.Annotation, true);
                    Art.Line(g, Border, 1, 1061, y + 72, 1536, y + 72);
                    int herdId = herd.Id;
                    buttons.Add(new UiButton(new RectangleF(1061, y, 475, 72), delegate
                    {
                        Beast current = game.Beasts.FirstOrDefault(b => b.Id == herdId && b.Count > 0 && game.CanControlBand(b.OwnerId));
                        if (current == null || !UnitVisible(current.CellId)) return;
                        SelectAnimal(current); page = 0; map.Focus(game.World.Cells[current.CellId]); ShowMapSelection();
                    }) { Tip = game.LivestockEnabled && LivestockEconomy.IsLivestock(herd) ? "Inspect this herd's milk, care, and the food and animal cost of slaughter." : "Inspect this companion group's benefits and care." });
                }
                if (companions.Length > 3) LedgerPager(g, 1061, 731, 475, economyLineagePage, companions.Length, 3, delegate(int next) { economyLineagePage = next; });
                else Typography.Line(g, "Select a group to inspect its benefits and available actions.", new RectangleF(1061, 732, 475, 27), 17, Art.Muted, TypeRole.Annotation, true);
            }
        }

        private void DrawEconomicTrade(Graphics g)
        {
            Band own = CurrentOrderBand; Band[] partners = EconomyKnownPartners();
            double need = game.Upkeep(own) + BandEconomy.DomesticEffects(game, own).AnimalCare;
            LedgerMetric(g, 42, "Known other households", partners.Length.ToString("N0"), "Living bands in explored places", Art.Ink);
            LedgerMetric(g, 348, "Within reach", partners.Count(b => b.CellId == own.CellId || game.World.Cells[own.CellId].Neighbors.Contains(b.CellId)).ToString("N0"), "At your place or on adjacent ground", Art.Ink);
            LedgerMetric(g, 654, "Food security", EconomySecurity(), "Your household's needs and care", Art.Gold);
            LedgerMetric(g, 960, "Above one turn's needs", Math.Max(0, own.Food - need).ToString("0.0"), "Food reserves less people and animal care", LedgerGreen);
            LedgerMetric(g, 1266, game.TribesEnabled ? "Food leaving the tribe" : "Food sent with daughters", journal.Totals.FoodShared.ToString("0.0"), game.TribesEnabled ? "Food retained by seceding households" : "Food sent with newly founded bands", Art.Gold);
            EconomyPanel(g, 42, 960, "I  /  Peoples along the known paths");
            if (partners.Length == 0)
            {
                Typography.Line(g, "No other household is presently known", new RectangleF(64, 480, 913, 53), 33, Art.Ink, TypeRole.Heading, true);
                Typography.Draw(g, "Travel through the known land to meet other peoples. This ledger lists only living bands whose current places have been explored.", new RectangleF(64, 553, 856, 86), 22, Art.Muted, TypeRole.Annotation);
            }
            economyContactPage = Math.Min(economyContactPage, Math.Max(0, (partners.Length - 1) / 4));
            for (int i = 0; i < 4 && economyContactPage * 4 + i < partners.Length; i++)
            {
                Band band = partners[economyContactPage * 4 + i]; float y = 465 + i * 60;
                BandLink(g, band, new RectangleF(64, y, 446, 39), true);
                double understanding = LanguageGenerator.Intelligibility(game.Languages[own.LanguageId], game.Languages[band.LanguageId], 0);
                string relation = game.Rules == SimulationRules.MobileUnits && EncounterRules.BandsHostile(game, own.Id, band.Id) ? "In a feud" : "No exchange agreement";
                Typography.Line(g, band.Population + " people  /  " + (understanding * 100).ToString("0") + "% understanding", new RectangleF(533, y, 445, 29), 18, Art.Ink, TypeRole.Annotation, true);
                Typography.Line(g, relation, new RectangleF(533, y + 28, 445, 23), 15, Art.Muted, TypeRole.Annotation);
                Art.Line(g, Border, 1, 64, y + 55, 978, y + 55);
            }
            if (partners.Length > 4) LedgerPager(g, 64, 732, 914, economyContactPage, partners.Length, 4, delegate(int next) { economyContactPage = next; });
            else Typography.Line(g, "Select a known household to inspect its current people and provisions.", new RectangleF(64, 732, 914, 27), 17, Art.Muted, TypeRole.Annotation);

            if (game.GatheringsEnabled)
            {
                JournalTotals totals = journal.Totals;
                EconomyPanel(g, 1018, 540, "II  /  Gifts between independent hearths");
                Typography.Line(g, "Gathering accounts", new RectangleF(1040, 465, 496, 48), 34, Art.Ink, TypeRole.Heading, true);
                EconomyAmount(g, 1040, 527, 496, "Invitation food spent", totals.GatheringFoodSpent, false);
                EconomyAmount(g, 1040, 563, 496, "Food given at meetings", totals.GatheringFoodGiven, false);
                EconomyAmount(g, 1040, 599, 496, "Salt given at meetings", totals.GatheringSaltGiven, false);
                Typography.Draw(g, totals.GatheringsMet + " first meetings. " + totals.ReturnsFulfilled + " return promises kept. Food gifts and invitation costs are included once in Finance outflow. Gifts require both bands at the agreed place.", new RectangleF(1040, 646, 496, 79), 18, Art.Muted, TypeRole.Body);
                Button(g, "Open Diplomacy", 1040, 733, 496, 33, delegate { ClearMapTransient(); page = 5; ResetDiplomacy(); Invalidate(); }, false, false);
                return;
            }
            EconomyPanel(g, 1018, 540, "II  /  Exchange has yet to begin");
            Typography.Line(g, "No active trade system", new RectangleF(1040, 465, 496, 48), 34, Art.Ink, TypeRole.Heading, true);
            Typography.Draw(g, "Households sustain themselves. There are no trade orders, prices, routes or recurring deliveries in the current simulation.", new RectangleF(1040, 530, 496, 91), 21, Art.Ink, TypeRole.Annotation);
            Art.Rule(g, 1040, 638, 496);
            Typography.Draw(g, game.TribesEnabled ? "Forming a band divides the tribe's existing provisions. A band that becomes independent keeps its own supplies; those leave your tribal account." : "Founding a daughter band transfers provisions once. That is support for a new household, not a traded purchase.", new RectangleF(1040, 658, 496, 81), 18, Art.Muted, TypeRole.Body);
        }

        private void DrawEconomicFinance(Graphics g)
        {
            var totals = journal.Totals; EconomyForecast forecast = BandEconomy.Forecast(game, CurrentOrderBand);
            double opening = journal.Timeline.Count == 0 ? EconomyCurrentCapacity() : journal.Timeline[0].Food;
            LedgerMetric(g, 42, game.TribesEnabled ? "Tribe food reserves" : "Food reserves", EconomyCurrentCapacity().ToString("N1"), game.TribesEnabled ? "Food across all controlled bands" : "Food held by your household", Art.Gold);
            LedgerMetric(g, 348, "Food received", totals.FoodGained.ToString("N1"), "All recorded food gains since the start", LedgerGreen);
            LedgerMetric(g, 654, "Food spent or lost", totals.FoodSpent.ToString("N1"), "Food use, transfers, departures and losses", Art.Ink);
            LedgerMetric(g, 960, "Net food change", (totals.FoodGained - totals.FoodSpent).ToString("+0.0;-0.0;0.0"), "Food received less food spent or lost", Art.Gold);
            LedgerMetric(g, 1266, "Reserve retention", game.Known("stores") ? "98%" : "95%", game.TribesEnabled ? "Selected household, after its upkeep" : "Applied to the remainder after upkeep", LedgerGreen);

            EconomyPanel(g, 42, 484, "I  /  The food account");
            EconomyAmount(g, 63, 469, 442, "Opening balance", opening, false);
            EconomyAmount(g, 63, 508, 442, "All food received", totals.FoodGained, true);
            EconomyAmount(g, 63, 547, 442, "All food spent or lost", -totals.FoodSpent, true);
            Art.Rule(g, 63, 590, 441);
            EconomyAmount(g, 63, 604, 442, "Recorded closing balance", EconomyRecordedBalance(), false);
            double difference = EconomyCurrentCapacity() - EconomyRecordedBalance();
            string reconciliation = Math.Abs(difference) < .001 ? "The recorded balance matches your " + (game.TribesEnabled ? "tribe's" : "household's") + " food reserves." : "Food reserves differ from the record by " + difference.ToString("+0.0;-0.0;0.0") + ". Some changes are outside this ledger.";
            Typography.Draw(g, reconciliation, new RectangleF(63, 651, 441, 56), 18, Art.Ink, TypeRole.Annotation);
            Typography.Draw(g, "No coinage, taxation, borrowing or debt is simulated.", new RectangleF(63, 725, 441, 37), 16, Art.Muted, TypeRole.Annotation);

            EconomyPanel(g, 542, 486, "II  /  Recorded sources and uses");
            EconomyAmount(g, 563, 461, 444, "Gathered food", totals.FoodGathered, false);
            EconomyAmount(g, 563, 489, 444, game.LivestockEnabled ? "Camp and milk production" : "Hearth and cattle production", totals.CampFoodProduced + totals.CattleFoodProduced, false);
            EconomyAmount(g, 563, 517, 444, game.LivestockEnabled ? "Meat from slaughter" : "Hunt recoveries", game.LivestockEnabled ? totals.LivestockMeatProduced : totals.FoodHunted, false);
            EconomyAmount(g, 563, 545, 444, game.LivestockEnabled ? "Hunt recoveries" : "Encounter inflows", game.LivestockEnabled ? totals.FoodHunted : totals.CombatFoodGained, false);
            if (game.LivestockEnabled) EconomyAmount(g, 563, 573, 444, "Encounter inflows", totals.CombatFoodGained, false);
            EconomyAmount(g, 563, 602, 444, "People supplied", totals.FoodConsumed, false);
            EconomyAmount(g, 563, 630, 444, "Animal care paid", totals.AnimalCarePaid, false);
            EconomyAmount(g, 563, 658, 444, "Reserve lost after upkeep", totals.FoodSpoiled, false);
            EconomyAmount(g, 563, 686, 444, game.TribesEnabled ? "Food leaving the tribe" : "Food sent with daughters", totals.FoodShared, false);
            Typography.Draw(g, "Selected details, not additive totals: hunt recoveries can also be encounter inflows. All action costs enter the account at left.", new RectangleF(563, 722, 444, 43), 14.5f, Art.Muted, TypeRole.Annotation);

            EconomyPanel(g, 1044, 514, game.TribesEnabled ? "III  /  Selected household forecast" : "III  /  The next turn's account");
            EconomyAmount(g, 1065, 463, 472, "Starting food reserves", forecast.StartingFood, false);
            EconomyAmount(g, 1065, 493, 472, game.LivestockEnabled ? "Camp and milk production" : "Hearth and cattle produce", EconomyActive ? forecast.CampFood + forecast.CattleFood : 0, true);
            EconomyAmount(g, 1065, 523, 472, "Animal care actually paid", -forecast.ActualAnimalCare, true);
            EconomyAmount(g, 1065, 553, 472, "People actually supplied", -EconomyPeopleSupplied(forecast), true);
            EconomyAmount(g, 1065, 583, 472, "Reserve lost after upkeep", -forecast.Spoilage, true);
            Art.Rule(g, 1065, 623, 471);
            EconomyAmount(g, 1065, 634, 472, "Remaining food reserves", forecast.EndingFood, false);
            double unmet = EconomyActive ? Math.Max(0, forecast.Upkeep - EconomyPeopleSupplied(forecast)) : 0;
            EconomyAmount(g, 1065, 669, 472, "People's needs left unmet", unmet, false);
            Typography.Draw(g, EconomyForecastNote(), new RectangleF(1065, 719, 472, 44), 16, Art.Muted, TypeRole.Annotation);
        }
    }
}
