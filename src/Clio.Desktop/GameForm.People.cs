using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using Clio.Simulation;

namespace Clio.Desktop
{
    public sealed partial class GameForm
    {
        private int peopleBandPage, peopleAnimalPage, inspectorAnimalPage;
        private static readonly Color LedgerGreen = Color.FromArgb(146, 182, 157);

        private void DrawPeople(Graphics g)
        {
            Surface(g, "The keeping of a people", "A household ledger: the lives, provisions and companions that make a history.");
            Band own = game.Player;
            double upkeep = game.Upkeep(own) + BandEconomy.DomesticEffects(game, own).AnimalCare;
            Band[] knownBands = game.Bands.Where(b => b.Population > 0 && game.Explored.Contains(b.CellId)).ToArray();
            Beast[] domestic = game.Beasts.Where(b => b.Domestic && b.OwnerId == own.Id && b.Count > 0).ToArray();
            LedgerMetric(g, 42, "Living people", own.Population.ToString("N0"), own.Settled ? "A settled household" : "A people on the paths", Art.Ink);
            LedgerMetric(g, 348, "Food security", upkeep <= 0 ? "\u2014" : (own.Food / upkeep).ToString("0.0") + "\u00d7 needs", "Current people and animal care", Art.Gold);
            LedgerMetric(g, 654, "Known peoples", knownBands.Length.ToString("N0"), "Living bands in explored land", Art.Ink);
            LedgerMetric(g, 960, "Remembered land", game.Explored.Count.ToString("N0"), "Places charted by your people", Art.Ink);
            LedgerMetric(g, 1266, "Shared knowledge", game.Knowledge.Count(k => k.Known) + " / " + game.Knowledge.Count, "Practices that endure", LedgerGreen);

            DrawPopulationLedger(g);
            DrawHouseholdLedger(g);
            DrawLineageLedger(g, domestic, knownBands);
        }

        private void LedgerMetric(Graphics g, float x, string label, string value, string note, Color color)
        {
            Art.Panel(g, new RectangleF(x, 305, 292, 90), Panel, false);
            Typography.Label(g, label, new RectangleF(x + 15, 311, 262, 21), 12, Art.Muted, .7f);
            Typography.Line(g, value, new RectangleF(x + 12, 329, 268, 42), 33, color, TypeRole.Number, true);
            Typography.Line(g, note, new RectangleF(x + 15, 368, 262, 21), 14, Art.Muted, TypeRole.Annotation, true);
        }

        private void DrawPopulationLedger(Graphics g, bool demographics = false)
        {
            Art.Panel(g, new RectangleF(42, 411, 740, 363), Panel, false);
            Typography.Label(g, "I  /  Lives through time", new RectangleF(60, 420, 458, 25), 13, Art.Gold, .9f);
            var points = journal.Timeline;
            if (points.Count > 0)
            {
                Typography.Line(g, points[0].Population + " \u2192 " + points[points.Count - 1].Population, new RectangleF(575, 417, 185, 32), 23, Art.Ink, TypeRole.Number, true, StringAlignment.Far);
                int maximum = Math.Max(10, points.Max(p => p.Population));
                int scale = Math.Max(10, (int)Math.Ceiling(maximum / 5.0) * 5);
                RectangleF plot = new RectangleF(105, 460, 652, 119);
                for (int i = 0; i <= 2; i++)
                {
                    float y = plot.Bottom - plot.Height * i / 2;
                    Art.Line(g, Color.FromArgb(55, 70, 70), .8f, plot.Left, y, plot.Right, y);
                    Typography.Line(g, (scale * i / 2).ToString("N0"), new RectangleF(54, y - 12, 42, 24), 13, Art.Muted, TypeRole.Number, true, StringAlignment.Far);
                }
                List<PointF> path = new List<PointF>();
                int step = Math.Max(1, (points.Count - 1) / 200);
                int firstTurn = points[0].Turn, lastTurn = points[points.Count - 1].Turn;
                for (int i = 0; i < points.Count; i += step)
                    path.Add(new PointF(plot.Left + plot.Width * (points[i].Turn - firstTurn) / Math.Max(1, lastTurn - firstTurn), plot.Bottom - plot.Height * points[i].Population / scale));
                if ((points.Count - 1) % step != 0)
                    path.Add(new PointF(plot.Right, plot.Bottom - plot.Height * points[points.Count - 1].Population / scale));
                if (path.Count > 1)
                {
                    List<PointF> area = new List<PointF>(path);
                    area.Add(new PointF(path[path.Count - 1].X, plot.Bottom)); area.Add(new PointF(path[0].X, plot.Bottom));
                    using (LinearGradientBrush fill = new LinearGradientBrush(plot, Color.FromArgb(56, Art.Gold), Color.FromArgb(2, Art.Gold), 90))
                        g.FillPolygon(fill, area.ToArray());
                    using (Pen line = new Pen(Art.Gold, 2)) { line.LineJoin = LineJoin.Round; g.DrawLines(line, path.ToArray()); }
                }
                foreach (PointF point in new[] { path[0], path[path.Count - 1] })
                    using (Brush dot = new SolidBrush(Art.Gold)) g.FillEllipse(dot, point.X - 3, point.Y - 3, 6, 6);
                Typography.Line(g, Timeline.Label(game, firstTurn), new RectangleF(104, 584, 294, 25), 14, Art.Muted, TypeRole.Annotation);
                Typography.Line(g, points.Count == 1 ? "The opening record" : Timeline.Label(game, lastTurn), new RectangleF(425, 584, 334, 25), 14, Art.Muted, TypeRole.Annotation, true, StringAlignment.Far);
            }
            else Typography.Line(g, "The first count will begin this record.", new RectangleF(80, 493, 650, 38), 20, Art.Muted, TypeRole.Annotation);
            Art.Rule(g, 62, 620, 697);
            var totals = journal.Totals;
            LedgerTotal(g, 65, 633, "New members", totals.Births.ToString("N0"), LedgerGreen);
            LedgerTotal(g, 240, 633, "Lives lost", totals.Deaths.ToString("N0"), Art.Ink);
            LedgerTotal(g, 415, 633, demographics ? "Departed" : "Food gathered", demographics ? totals.PopulationDeparted.ToString("N0") : totals.FoodGathered.ToString("N0"), Art.Gold);
            LedgerTotal(g, 590, 633, demographics ? "Net change" : "Food hunted", demographics ? totals.NetPopulationChange.ToString("+0;-0;0") : totals.FoodHunted.ToString("N0"), Art.Gold);
            string footnote = totals.Moves + " journeys  \u00b7  " + totals.SuccessfulHunts + " successful hunts  \u00b7  " + totals.DaughterBandsFounded + " daughter bands";
            if (game.Rules == SimulationRules.MobileUnits) footnote = totals.Moves + " journeys  \u00b7  " + totals.Battles + " battles  \u00b7  " + EconomyEncounterDeaths() + " encounter deaths  \u00b7  " + totals.DaughterBandsFounded + " daughter bands";
            if (totals.UnresolvedPopulationChanges > 0 || totals.UnresolvedEconomyChapters > 0) footnote += "  /  Some changes could not be itemized";
            Typography.Line(g, footnote, new RectangleF(65, 727, 695, 27), 16, Art.Muted, TypeRole.Annotation, true);
        }

        private void LedgerTotal(Graphics g, float x, float y, string label, string value, Color color)
        {
            Typography.Label(g, label, new RectangleF(x, y, 161, 23), 11.5f, Art.Muted, .5f);
            Typography.Line(g, value, new RectangleF(x - 2, y + 22, 165, 48), 35, color, TypeRole.Number, true);
        }

        private void DrawHouseholdLedger(Graphics g)
        {
            Art.Panel(g, new RectangleF(798, 411, 355, 363), Panel, false);
            Typography.Label(g, "II  /  The household", new RectangleF(817, 420, 318, 25), 13, Art.Gold, .9f);
            Typography.Line(g, "If the turn ends now", new RectangleF(817, 448, 318, 25), 17, Art.Muted, TypeRole.Annotation);
            EconomyForecast economy = BandEconomy.Forecast(game, game.Player);
            LedgerBudgetRow(g, "Food reserves", economy.StartingFood, 480, Art.Ink, false);
            LedgerBudgetRow(g, "Food from camp", economy.CampFood, 508, LedgerGreen, true);
            LedgerBudgetRow(g, game.LivestockEnabled ? "Milk from livestock" : "Cattle produce", economy.CattleFood, 536, LedgerGreen, true);
            LedgerBudgetRow(g, "Animal care", -economy.ActualAnimalCare, 564, Art.Muted, true);
            double supplied = Math.Min(economy.Upkeep, Math.Max(0, economy.StartingFood + economy.CampFood + economy.CattleFood - economy.ActualAnimalCare));
            LedgerBudgetRow(g, supplied < economy.Upkeep ? "People supplied" : "People's needs", -supplied, 592, Art.Muted, true);
            LedgerBudgetRow(g, "Food lost to spoilage", -economy.Spoilage, 620, Art.Muted, true);
            Art.Rule(g, 817, 654, 316);
            LedgerBudgetRow(g, "Remaining food reserves", economy.EndingFood, 665, Art.Gold, false);
            int losses = economy.HungerLosses + economy.ExposureLosses;
            Typography.Draw(g, losses > 0 ? losses + " lives at risk. " + (supplied < economy.Upkeep ? (economy.Upkeep - supplied).ToString("0.0") + " needs unmet; gather before closing." : "Seek shelter before closing.") : "A forecast of current conditions. Actions can change the outcome.", new RectangleF(818, 706, 313, 56), 15, losses > 0 ? Art.Gold : Art.Muted, TypeRole.Annotation);
        }

        private void LedgerBudgetRow(Graphics g, string label, double value, float y, Color color, bool signed)
        {
            Typography.Line(g, label, new RectangleF(818, y, 210, 27), 16, Art.Muted, TypeRole.Body);
            Typography.Line(g, (signed && value > 0 ? "+" : "") + value.ToString("0.0"), new RectangleF(1035, y, 97, 27), 20, color, TypeRole.Number, true, StringAlignment.Far);
        }

        private void DrawLineageLedger(Graphics g, Beast[] domestic, Band[] knownBands)
        {
            Art.Panel(g, new RectangleF(1169, 411, 389, 363), Panel, false);
            Typography.Label(g, "III  /  Beside the hearth", new RectangleF(1186, 420, 355, 25), 13, Art.Gold, .8f);
            DomesticEconomy effects = BandEconomy.DomesticEffects(game, game.Player);
            int otherCompanions = domestic.Where(b => b.Kind != BeastKind.Wolves && b.Kind != BeastKind.Aurochs).Sum(b => b.Count);
            Typography.Line(g, effects.Dogs + " dogs  ·  " + effects.Cattle + " cattle" + (game.LivestockEnabled ? "  ·  " + effects.Goats + " goats" : otherCompanions > 0 ? "  ·  " + otherCompanions + " other" : ""), new RectangleF(1186, 448, 352, 25), 18, Art.Ink, TypeRole.Heading, true);
            if (domestic.Length == 0)
                Typography.Draw(g, game.Rules == SimulationRules.MobileUnits ? "No domestic lineages yet. Select a moving animal group and inspect its temperament before a peaceful approach." : "No domestic lineages yet. Repeated peaceful encounters with wolves or aurochs can begin a shared life.", new RectangleF(1187, 485, 348, 68), 16, Art.Muted, TypeRole.Annotation);
            else
            {
                peopleAnimalPage = Math.Min(peopleAnimalPage, (domestic.Length - 1) / 2);
                for (int i = 0; i < 2 && peopleAnimalPage * 2 + i < domestic.Length; i++)
                {
                    Beast herd = domestic[peopleAnimalPage * 2 + i]; float y = 484 + i * 40;
                    IdentityArt.DrawAnimal(g, herd.Kind, new RectangleF(1187, y, 30, 27), Art.Ink, true);
                    Typography.Line(g, herd.Count + "  " + (game.LivestockEnabled ? LivestockEconomy.DisplayName(game, herd) : herd.BreedName), new RectangleF(1227, y - 3, 305, 25), 17, Art.Ink, TypeRole.Heading, true);
                    string benefit = DomesticLineageBenefit(herd);
                    if (herd.Kind == BeastKind.Wolves && !game.LivestockEnabled)
                        benefit = (game.Rules == SimulationRules.MobileUnits ? "Fighting aid; household +" : "Household: up to +" + (effects.HuntingBonus * 100).ToString("0") + "pt hunts, +") + ((effects.GatheringMultiplier - 1) * 100).ToString("0") + "% gather; care " + BandEconomy.AnimalCare(game, herd).ToString("0.0") + "/turn";
                    Typography.Line(g, benefit, new RectangleF(1227, y + 19, 305, 21), 13.5f, Art.Muted, TypeRole.Annotation, true);
                }
                if (domestic.Length > 2) LedgerPager(g, 1187, 569, 348, peopleAnimalPage, domestic.Length, 2, delegate(int next) { peopleAnimalPage = next; });
            }
            Art.Rule(g, 1187, 605, 350);
            Typography.Label(g, "Peoples of the known land", new RectangleF(1187, 615, 350, 22), 12, Art.Gold, .6f);
            peopleBandPage = Math.Min(peopleBandPage, Math.Max(0, (knownBands.Length - 1) / 2));
            for (int i = 0; i < 2 && peopleBandPage * 2 + i < knownBands.Length; i++)
            {
                Band band = knownBands[peopleBandPage * 2 + i]; float y = 645 + i * 39;
                BandLink(g, band, new RectangleF(1187, y, 348, 34), true);
            }
            if (knownBands.Length > 2) LedgerPager(g, 1187, 732, 348, peopleBandPage, knownBands.Length, 2, delegate(int next) { peopleBandPage = next; });
            else Typography.Line(g, "Select a people to visit their place.", new RectangleF(1187, 734, 350, 24), 14, Art.Muted, TypeRole.Annotation);
        }

        private void DrawInspector(Graphics g)
        {
            Art.Panel(g, new RectangleF(1274, 188, 310, 607), Panel, true);
            string[] tabs = { "Place", "Band", "Animals", "Names" };
            for (int i = 0; i < tabs.Length; i++)
            {
                int tab = i;
                Button(g, tabs[i], 1291 + i * 70, 205, 67, 34, delegate { SelectInspectorTab(tab); }, inspectorPage == i, false);
            }
            bool known = selected >= 0 && selected < game.World.Cells.Length && (!map.Fog || game.Explored.Contains(selected));
            if (!known)
            {
                Typography.Label(g, "Unexplored", new RectangleF(1294, 260, 269, 26), 12, Art.Gold);
                FittedTitle(g, "Beyond the paths", new RectangleF(1292, 298, 272, 84), 34, Art.Ink);
                Art.Icon(g, "globe", 1396, 429, 61, Art.Muted);
                Typography.Draw(g, "No account of this place has reached your people.", new RectangleF(1302, 526, 254, 78), 24, Art.Ink, TypeRole.Annotation);
                Typography.Draw(g, "Travel toward the edge of the known land to learn what lies beyond.", new RectangleF(1302, 624, 254, 77), 16, Art.Muted, TypeRole.Body);
                return;
            }
            if (inspectorPage == 1) DrawBandInspector(g);
            else if (inspectorPage == 2) DrawAnimalInspector(g);
            else if (inspectorPage == 3) DrawPlaceNameInspector(g);
            else DrawPlaceInspector(g);
        }

        private void DrawPlaceInspector(Graphics g)
        {
            Cell cell = game.World.Cells[selected];
            FittedTitle(g, game.Place(selected), new RectangleF(1291, 256, 274, 64), 32, Art.Ink);
            Typography.Line(g, cell.Terrain + "  /  Region " + cell.RegionId, new RectangleF(1295, 322, 268, 26), 17, Art.Muted, TypeRole.Annotation);
            Art.Rule(g, 1294, 355, 269);
            Typography.Label(g, "Gathering here", new RectangleF(1294, 365, 267, 22), 12, Art.Gold);
            Typography.Line(g, cell.IsLand ? "+" + game.ForageYield(selected, game.Player).ToString("0") : "Open water", new RectangleF(1291, 385, 272, 59), cell.IsLand ? 43 : 30, Art.Ink, cell.IsLand ? TypeRole.Number : TypeRole.Heading, true);
            Typography.Line(g, cell.IsLand ? "Food gathered per action" : "Water travel has yet to be learned", new RectangleF(1295, 441, 268, 25), 15, Art.Muted, TypeRole.Annotation, true);
            InspectorPair(g, "Climate", cell.Temperature < .25 ? "Cold" : cell.Temperature > .72 ? "Hot" : "Temperate", 478);
            InspectorPair(g, "Moisture", (cell.Moisture * 100).ToString("0") + "%", 508);
            InspectorPair(g, "Ground recovery", ((1 - game.Depletion[selected]) * 100).ToString("0") + "%", 538);
            Meter(g, 1295, 570, 267, 1 - game.Depletion[selected], LedgerGreen);
            int knownLand = cell.Neighbors.Count(n => game.Explored.Contains(n) && game.World.Cells[n].IsLand);
            int unknown = cell.Neighbors.Count(n => !game.Explored.Contains(n));
            Typography.Line(g, knownLand + " known land routes" + (unknown > 0 ? "  \u00b7  " + unknown + " uncharted" : ""), new RectangleF(1295, 585, 268, 25), 15, Art.Muted, TypeRole.Annotation, true);
            Typography.Line(g, selected == game.Player.CellId ? "Your people are here" : game.CanMove(selected) ? "Adjacent land  /  one action to enter" : "Follow the land to reach this place", new RectangleF(1295, 613, 268, 25), 15, Art.Gold, TypeRole.Annotation, true);
            Art.Rule(g, 1294, 648, 269);
            Band[] present = PresentBands();
            Typography.Label(g, present.Length == 0 ? "No people observed" : present.Length + " " + (present.Length == 1 ? "band" : "bands") + " here", new RectangleF(1295, 657, 267, 24), 12, Art.Gold);
            for (int i = 0; i < Math.Min(2, present.Length); i++) BandLink(g, present[i], new RectangleF(1294, 687 + i * 35, 268, 31), false);
            if (present.Length > 2) Button(g, "See all " + present.Length + " peoples", 1295, 756, 267, 27, delegate { SelectInspectorTab(1); }, false, false);
            else if (present.Length == 0) Typography.Draw(g, "A place may be shared or passed through. No borders claim this ground.", new RectangleF(1295, 692, 268, 66), 16, Art.Muted, TypeRole.Annotation);
        }

        private void DrawBandInspector(Graphics g)
        {
            Band[] present = PresentBands();
            Typography.Label(g, "People in this place", new RectangleF(1294, 258, 269, 25), 12, Art.Gold);
            if (present.Length == 0 || inspectedBandId == -2)
            {
                Typography.Draw(g, inspectedBandId == -2 ? "Their path is lost to sight." : "No hearths are seen here.", new RectangleF(1293, 316, 271, 90), 31, Art.Ink, TypeRole.Heading);
                Typography.Draw(g, "Choose a band on the map or visit the Economy ledger to inspect a known household.", new RectangleF(1295, 453, 267, 102), 17, Art.Muted, TypeRole.Body);
                Button(g, "Open Economy ledger", 1295, 601, 267, 39, delegate { OpenEconomy(1); }, false, false);
                if (present.Length > 0) Button(g, "Inspect remaining bands", 1295, 660, 267, 39, delegate { SelectInspectorTab(1); }, false, false);
                return;
            }
            int index = Array.FindIndex(present, b => b.Id == inspectedBandId);
            if (index < 0) { index = 0; inspectedBandId = present[0].Id; }
            Band band = present[index];
            if (present.Length > 1)
            {
                int current = index;
                Button(g, "Previous", 1294, 291, 88, 29, delegate { inspectedBandId = present[(current + present.Length - 1) % present.Length].Id; Invalidate(); }, false, false);
                Typography.Line(g, (index + 1) + " / " + present.Length, new RectangleF(1389, 291, 80, 29), 16, Art.Muted, TypeRole.Number, true, StringAlignment.Center);
                Button(g, "Next", 1477, 291, 85, 29, delegate { inspectedBandId = present[(current + 1) % present.Length].Id; Invalidate(); }, false, false);
            }
            else Typography.Line(g, band.Id == game.Player.Id ? "Your household" : "An independent household", new RectangleF(1294, 287, 268, 30), 18, Art.Muted, TypeRole.Annotation);
            IdentityArt.DrawEmblem(g, band.Id, new RectangleF(1294, 341, 49, 49), false);
            FittedTitle(g, band.Name, new RectangleF(1353, 337, 210, 76), 29, IdentityArt.ColorFor(band.Id));
            Typography.Line(g, band.Ancestry + "  /  " + (band.Settled ? (game.Pace == HistoryPace.LegacySeasons ? "Seasonal camp" : "Established hearth") : "Wandering band"), new RectangleF(1295, 420, 268, 28), 16, Art.Muted, TypeRole.Annotation, true);
            Art.Rule(g, 1295, 459, 266);
            Typography.Line(g, band.Population.ToString("N0"), new RectangleF(1292, 469, 123, 55), 38, Art.Ink, TypeRole.Number, true);
            Typography.Line(g, band.Food.ToString("N0"), new RectangleF(1430, 469, 134, 55), 38, Art.Gold, TypeRole.Number, true);
            Typography.Label(g, "People", new RectangleF(1295, 523, 124, 23), 11.5f, Art.Muted);
            Typography.Label(g, "Food reserves", new RectangleF(1433, 523, 130, 23), 11.5f, Art.Muted);
            if (game.Rules == SimulationRules.MobileUnits) { DrawFightingBandDetails(g, band); return; }
            InspectorPair(g, "Cohesion", (band.Cohesion * 100).ToString("0") + "%", 562);
            Meter(g, 1295, 592, 267, band.Cohesion, LedgerGreen);
            LanguageProfile language = game.Languages[band.LanguageId];
            Typography.Label(g, "Spoken language", new RectangleF(1295, 609, 267, 23), 12, Art.Muted);
            Typography.Line(g, language.Name, new RectangleF(1293, 634, 269, 31), 23, Art.Ink, TypeRole.Heading, true);
            if (band.Id == game.Player.Id)
            {
                DomesticEconomy effects = BandEconomy.DomesticEffects(game, band);
                Typography.Line(g, effects.Dogs + " dogs  \u00b7  " + effects.Cattle + " cattle" + (game.LivestockEnabled ? "  \u00b7  " + effects.Goats + " goats" : ""), new RectangleF(1295, 681, 267, 27), 17, Art.Muted, TypeRole.Annotation);
                Button(g, "Open household ledger", 1295, 735, 267, 39, delegate { OpenEconomy(0); }, false, false);
            }
            else
            {
                double intelligibility = LanguageGenerator.Intelligibility(language, game.Languages[game.Player.LanguageId], 0);
                Typography.Line(g, (intelligibility * 100).ToString("0") + "% shared understanding", new RectangleF(1295, 676, 267, 28), 16, Art.Muted, TypeRole.Annotation, true);
                Typography.Draw(g, "A known people making their own choices. This view observes their current household.", new RectangleF(1295, 721, 267, 58), 15.5f, Art.Muted, TypeRole.Annotation);
            }
        }

        private void DrawAnimalInspector(Graphics g)
        {
            if (game.Rules == SimulationRules.MobileUnits) { DrawAnimalUnitInspector(g); return; }
            Beast[] animals = game.Beasts.Where(b => b.CellId == selected && b.Count > 0).OrderByDescending(b => b.Domestic).ThenBy(b => b.Kind).ThenBy(b => b.Id).ToArray();
            Typography.Label(g, "Life in this place", new RectangleF(1294, 258, 269, 25), 12, Art.Gold);
            Typography.Line(g, animals.Length + " " + (animals.Length == 1 ? "group observed" : "groups observed"), new RectangleF(1292, 287, 271, 34), 27, Art.Ink, TypeRole.Heading);
            if (animals.Length == 0)
            {
                Art.Icon(g, "leaf", 1399, 409, 52, Art.Muted);
                Typography.Draw(g, "No large animals are observed here. Wild herds can move as the story advances.", new RectangleF(1301, 506, 253, 108), 19, Art.Muted, TypeRole.Annotation);
                return;
            }
            inspectorAnimalPage = Math.Min(inspectorAnimalPage, (animals.Length - 1) / 3);
            for (int i = 0; i < 3 && inspectorAnimalPage * 3 + i < animals.Length; i++)
            {
                Beast animal = animals[inspectorAnimalPage * 3 + i]; float y = 334 + i * 130;
                Art.Line(g, Border, 1, 1295, y - 5, 1562, y - 5);
                IdentityArt.DrawAnimal(g, animal.Kind, new RectangleF(1295, y + 4, 31, 28), Art.Ink, animal.Domestic);
                Typography.Line(g, game.LivestockEnabled ? LivestockEconomy.DisplayName(game, animal) : animal.Domestic ? animal.BreedName : animal.Kind.ToString(), new RectangleF(1337, y, 226, 29), 19, Art.Ink, TypeRole.Heading);
                Typography.Line(g, animal.Count + (animal.Domestic ? " animals  /  domestic lineage" : " animals  /  wild"), new RectangleF(1338, y + 29, 224, 23), 14, Art.Muted, TypeRole.Annotation, true);
                if (animal.Domestic)
                {
                    Typography.Line(g, DomesticLineageBenefit(animal), new RectangleF(1295, y + 61, 267, 25), 15, Art.Gold, TypeRole.Annotation, true);
                    Band owner = game.Bands.FirstOrDefault(b => b.Id == animal.OwnerId);
                    string detail = owner == null ? "A companion lineage" : owner.Id == game.Player.Id ? "Travels with your people" : "Companions of " + owner.Name;
                    if (animal.Kind == BeastKind.Wolves && owner != null)
                        detail = game.LivestockEnabled ? "Hunting only; no food or gathering bonus" : "Gathering +" + ((BandEconomy.DomesticEffects(game, owner).GatheringMultiplier - 1) * 100).ToString("0") + "% \u00b7 care " + BandEconomy.AnimalCare(game, animal).ToString("0.0") + " / turn";
                    Typography.Line(g, detail, new RectangleF(1295, y + 88, 267, 24), 14, Art.Muted, TypeRole.Annotation, true);
                }
                else if (animal.Kind == BeastKind.Wolves || animal.Kind == BeastKind.Aurochs)
                {
                    Typography.Line(g, "Trust  " + animal.PositiveContacts + " / 10", new RectangleF(1295, y + 57, 267, 25), 15, Art.Gold, TypeRole.Body);
                    Meter(g, 1295, y + 87, 267, animal.PositiveContacts / 10.0, Art.Gold);
                    Typography.Line(g, animal.LastContactTurn == game.Turn ? "Already approached this turn" : "Peaceful approach: 18 provisions", new RectangleF(1295, y + 94, 267, 24), 14, Art.Muted, TypeRole.Annotation, true);
                }
                else
                {
                    Typography.Line(g, "Hunting chance  " + (BandEconomy.HuntChance(game, animal) * 100).ToString("0") + "%", new RectangleF(1295, y + 60, 267, 25), 15, Art.Gold, TypeRole.Body);
                    Typography.Line(g, game.LivestockEnabled && animal.Kind == BeastKind.Deer ? "Deer cannot be domesticated" : "No domestication practice is known", new RectangleF(1295, y + 89, 267, 24), 14, Art.Muted, TypeRole.Annotation, true);
                }
            }
            if (animals.Length > 3) LedgerPager(g, 1295, 747, 267, inspectorAnimalPage, animals.Length, 3, delegate(int next) { inspectorAnimalPage = next; });
            else Typography.Line(g, selected == game.Player.CellId ? "Encounters use the current band" : "Bring your band here to make contact", new RectangleF(1295, 751, 267, 26), 14, Art.Muted, TypeRole.Annotation, true);
        }

        private string DomesticLineageBenefit(Beast herd)
        {
            // Food and care are per lineage; hunting is the owning household's combined bonus.
            string care = BandEconomy.AnimalCare(game, herd).ToString("0.0");
            if (game.LivestockEnabled)
            {
                if (LivestockEconomy.IsLivestock(herd)) return "+" + LivestockEconomy.MilkFood(game, herd).ToString("0.0") + " milk food · " + care + " care / turn";
                if (herd.Kind == BeastKind.Wolves)
                {
                    Band dogOwner = game.Bands.FirstOrDefault(b => b.Id == herd.OwnerId);
                    string hunting = dogOwner != null && game.CanControlBand(dogOwner.Id) ? "Band hunts +" + (BandEconomy.DomesticEffects(game, dogOwner).HuntingBonus * 100).ToString("0") +
                        (game.Rules == SimulationRules.MobileUnits ? "% strength" : " points") : "Hunting support";
                    return hunting + " · " + care + " care / turn";
                }
                if (herd.Kind == BeastKind.Deer) return "Wild deer; cannot be domesticated";
            }
            if (herd.Kind == BeastKind.Aurochs) return "+" + BandEconomy.CattleFood(herd).ToString("0.0") + " food \u00b7 " + care + " care / turn";
            if (game.Rules == SimulationRules.MobileUnits)
            {
                string aid = herd.Kind == BeastKind.Deer ? "Gathering aid" : herd.Kind == BeastKind.Dragon ? "Fighting aid" : "Fighting and gathering aid";
                return aid + " · " + care + " care / turn";
            }
            if (herd.Kind != BeastKind.Wolves) return herd.Kind + " companions \u00b7 " + care + " care / turn";
            Band owner = game.Bands.FirstOrDefault(b => b.Id == herd.OwnerId);
            double bonus = owner == null ? 0 : BandEconomy.DomesticEffects(game, owner).HuntingBonus;
            return "Household: hunts up to +" + (bonus * 100).ToString("0") + " points";
        }

        private Band[] PresentBands()
        { return game.Bands.Where(b => b.CellId == selected && b.Population > 0).OrderBy(b => b.Id).ToArray(); }

        private void InspectorPair(Graphics g, string label, string value, float y)
        {
            Typography.Line(g, label, new RectangleF(1295, y, 160, 26), 15, Art.Muted, TypeRole.Body);
            Typography.Line(g, value, new RectangleF(1450, y, 112, 26), 17, Art.Ink, TypeRole.Number, true, StringAlignment.Far);
        }

        private void BandLink(Graphics g, Band band, RectangleF bounds, bool visit)
        {
            bool hover = bounds.Contains(hoverPoint);
            if (hover) Art.Fill(g, Color.FromArgb(36, 48, 48), bounds.X, bounds.Y, bounds.Width, bounds.Height);
            IdentityArt.DrawEmblem(g, band.Id, new RectangleF(bounds.X + 2, bounds.Y + 3, 25, 25), false);
            Typography.Line(g, band.Name, new RectangleF(bounds.X + 37, bounds.Y, bounds.Width - 63, bounds.Height), 17, IdentityArt.ColorFor(band.Id), TypeRole.Heading, true);
            Typography.Line(g, "\u203a", new RectangleF(bounds.Right - 23, bounds.Y, 21, bounds.Height), 22, Art.Gold, TypeRole.Heading, false, StringAlignment.Center);
            buttons.Add(new UiButton(bounds, delegate
            {
                selectedAnimalId = -1; inspectedBandId = band.Id; inspectorPage = 1;
                if (visit) { selected = band.CellId; page = 0; map.Focus(game.World.Cells[selected]); ShowMapSelection(); }
                Invalidate();
            }));
        }

        private void LedgerPager(Graphics g, float x, float y, float width, int index, int count, int perPage, Action<int> change)
        {
            int pages = (count + perPage - 1) / perPage;
            Button(g, "Previous", x, y, 88, 28, delegate { change((index + pages - 1) % pages); Invalidate(); }, false, false);
            Typography.Line(g, (index + 1) + " / " + pages, new RectangleF(x + 93, y, width - 186, 28), 15, Art.Muted, TypeRole.Number, true, StringAlignment.Center);
            Button(g, "Next", x + width - 88, y, 88, 28, delegate { change((index + 1) % pages); Invalidate(); }, false, false);
        }
    }
}
