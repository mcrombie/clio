using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using Clio.Simulation;

namespace Clio.Desktop
{
    public sealed partial class GameForm
    {
        private string guidedBriefTitle = "The tribe's position", guidedBriefText = "";
        private string guidedLastConcern = "";
        private int guidedLastConcernTurn = -10, guidedAnimalCell = -1, guidedAnimalIndex;
        private bool guidedAnimalLesson;

        private sealed class GuidedObservation
        {
            internal double Food, Salt, Wood;
            internal int Population;
            internal Dictionary<int, int> Animals;
        }

        private Beast[] GuidedObservedAnimals()
        { return game.Beasts.Where(b => b.Count > 0 && game.Explored.Contains(b.CellId)).OrderBy(b => b.Id).ToArray(); }

        private GuidedObservation CaptureGuidedObservation()
        {
            return new GuidedObservation { Food = game.Player.Food, Salt = game.Player.Salt, Wood = game.Player.Wood,
                Population = game.Player.Population, Animals = GuidedObservedAnimals().ToDictionary(b => b.Id, b => b.CellId) };
        }

        private void ResetGuidedReports()
        {
            guidedAnimalCell = -1; guidedAnimalIndex = 0; guidedAnimalLesson = game.Turn > 3;
            guidedLastConcern = ""; guidedLastConcernTurn = -10;
            guidedBriefTitle = "The tribe's position";
            guidedBriefText = GuidedReserveSummary() + "\n\n" + GuidedAnimalSummary(GuidedObservedAnimals());
        }

        private string GuidedReserveSummary()
        {
            return GuidedAmount(game.Player.Food) + " food in reserve, enough for about " +
                (game.Player.Food / Math.Max(1, game.Upkeep(game.Player))).ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) +
                " turns before spoilage. Salt: " + SaltEconomy.ReserveTurns(game.Player).ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) +
                " turns. Wood: " + WoodEconomy.ReserveTurns(game.Player).ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + " turns.";
        }

        private static string GuidedAnimalSummary(Beast[] animals)
        {
            if (animals.Length == 0) return "No animal groups are currently observed on your mapped land.";
            string[] groups = animals.GroupBy(b => b.Kind).OrderByDescending(g => g.Sum(b => b.Count)).Take(3)
                .Select(g => g.Sum(b => b.Count) + " " + g.Key.ToString().ToLowerInvariant()).ToArray();
            return "Observed on mapped land: " + String.Join(", ", groups) +
                (animals.Select(b => b.Kind).Distinct().Count() > 3 ? ", and other wildlife." : ".");
        }

        private string GuidedConcern(out string key)
        {
            double foodTurns = game.Player.Food / Math.Max(1, game.Upkeep(game.Player));
            double saltTurns = SaltEconomy.ReserveTurns(game.Player);
            if (foodTurns < 2)
            {
                key = foodTurns < 1 ? "food-critical" : "food-low";
                return foodTurns < 1 ? "There is not enough food for the next turn. Gather before ending it." : "Food will last fewer than two turns. Gather before a long journey.";
            }
            if (saltTurns < 2)
            {
                key = saltTurns < 1 ? "salt-critical" : "salt-low";
                return game.GuidedGatherForecast().Salt > 0 ? "Salt is low. Gather here to replenish it with food and wood." : "Salt is low, and this region has no source. Seek a new region before reserves run out.";
            }
            if (WoodEconomy.ReserveTurns(game.Player) < 2)
            { key = "wood-low"; return "Firewood is low. Gathering replenishes wood; cooking fires help food go further."; }
            key = ""; return "";
        }

        private void UpdateGuidedReport(GuidedObservation before, string command)
        {
            // Receipts and observations change after a command, never while a
            // frame is painted. Routine turn accounting is available on demand.
            Beast[] animals = GuidedObservedAnimals();
            Beast[] arrivals = animals.Where(b => !before.Animals.ContainsKey(b.Id)).ToArray();
            int departures = before.Animals.Keys.Count(id => !animals.Any(b => b.Id == id));
            int movedAnimals = animals.Count(b => before.Animals.ContainsKey(b.Id) && before.Animals[b.Id] != b.CellId);
            List<string> details = new List<string>();
            bool gather = command == "guided-gather", move = command.StartsWith("guided-move:", StringComparison.Ordinal);
            bool populationChanged = game.Player.Population != before.Population;
            if (gather)
            {
                guidedBriefTitle = "The gathering returns";
                details.Add("Brought back " + GuidedAmount(game.Player.Food - before.Food) + " food, " +
                    GuidedAmount(game.Player.Wood - before.Wood) + " wood and " + GuidedAmount(game.Player.Salt - before.Salt) + " salt.");
            }
            else if (move)
            {
                guidedBriefTitle = "A new region";
                GuidedResourceYield yield = game.GuidedGatherForecast();
                details.Add(game.GuidedRegionName() + ". A gathering here can bring about " + GuidedAmount(yield.Food) + " food, " + GuidedAmount(yield.Wood) + " wood and " + GuidedAmount(yield.Salt) + " salt.");
            }
            else
            {
                guidedBriefTitle = "Report for turn " + game.Turn;
                EconomyForecast balance = game.Encounters.LastPlayerEconomy;
                if (balance != null && game.Turn > 2)
                    details.Add("Food " + GuidedAmount(before.Food) + " to " + GuidedAmount(game.Player.Food) + ": " + GuidedAmount(balance.Upkeep) +
                        " eaten, " + GuidedAmount(balance.Spoilage) + " spoiled. Salt used: " + GuidedAmount(Math.Max(0, before.Salt - game.Player.Salt)) +
                        ". Firewood used: " + GuidedAmount(Math.Max(0, before.Wood - game.Player.Wood)) + ".");
            }
            if (populationChanged) details.Add("Your people: " + before.Population + " to " + game.Player.Population + ".");
            if (arrivals.Length > 0)
                details.Add("New sightings: " + String.Join(", ", arrivals.GroupBy(b => b.Kind).Take(2).Select(g => g.Sum(b => b.Count) + " " + g.Key.ToString().ToLowerInvariant()).ToArray()) + ".");
            else if (departures > 0) details.Add(departures + " animal " + (departures == 1 ? "group has" : "groups have") + " moved out of sight.");
            else if (movedAnimals > 0) details.Add(movedAnimals + " observed animal " + (movedAnimals == 1 ? "group changed" : "groups changed") + " ground this turn.");
            string concernKey; string concern = GuidedConcern(out concernKey);
            bool freshConcern = concernKey.Length > 0 && (concernKey != guidedLastConcern || game.Turn - guidedLastConcernTurn >= 4);
            bool recovered = concernKey.Length == 0 && guidedLastConcern.Length > 0;
            if (concern.Length > 0) details.Insert(0, concern);
            else if (!move) details.Add(GuidedReserveSummary());
            if (freshConcern) guidedLastConcernTurn = game.Turn;
            guidedLastConcern = concernKey;
            bool lesson = game.Turn >= 3 && !guidedAnimalLesson;
            if (lesson)
            {
                guidedAnimalLesson = true; guidedBriefTitle = "The land is alive";
                details.Add("The animal counters are roaming groups. Click one for its count and kind. Wildlife moves between turns; these early lessons keep it peaceful.");
            }
            if (details.Count == 0) details.Add(GuidedReserveSummary());
            guidedBriefText = String.Join("\n\n", details.ToArray());
            guidedReport = game.Turn == 2 && command == "end" || game.IsOver || lesson || freshConcern || recovered ||
                move || gather && game.Turn >= 3 || arrivals.Length > 0 || populationChanged;
            guidedAnimalCell = -1;
            if (command == "end" && game.Turn > 2)
                guidedResult = "Food " + GuidedAmount(before.Food) + " \u2192 " + GuidedAmount(game.Player.Food) +
                    "  /  " + animals.Length + " animal groups observed  /  " + game.Influence + " influence";
        }

        private string GuidedSituationAdvice(out string title)
        {
            title = guidedBriefTitle;
            if (commands.Count >= MaximumCommands) { title = "A full chronicle"; return guidedResult; }
            return String.IsNullOrEmpty(guidedBriefText) ? GuidedReserveSummary() : guidedBriefText;
        }

        private void ToggleGuidedReport()
        {
            guidedReport = !guidedReport; guidedSupplies = guidedMenu = false; guidedAnimalCell = -1;
            if (guidedReport) SpeakGuidedAdviser(); else StopFirstAdviserVoice();
        }

        private bool TryGuidedAnimalClick(PointF point)
        {
            int cell = map.PickUnitStack(point.X, point.Y);
            int id = map.PeekAnimal(point.X, point.Y);
            Beast selectedAnimal = game.Beasts.FirstOrDefault(b => b.Id == id && b.Count > 0 && game.Explored.Contains(b.CellId));
            if (selectedAnimal != null) cell = selectedAnimal.CellId;
            if (cell < 0 || !game.Explored.Contains(cell)) return false;
            Beast[] animals = game.Beasts.Where(b => b.Count > 0 && b.CellId == cell).OrderBy(b => b.Id).ToArray();
            if (animals.Length == 0) return false;
            guidedAnimalCell = cell; guidedAnimalIndex = Math.Max(0, Array.FindIndex(animals, b => b.Id == id));
            guidedReport = guidedSupplies = guidedMenu = false; StopFirstAdviserVoice(); Invalidate(); return true;
        }

        private void DrawGuidedAnimalCard(Graphics g)
        {
            if (guidedAnimalCell < 0 || !game.Explored.Contains(guidedAnimalCell)) return;
            Beast[] animals = game.Beasts.Where(b => b.Count > 0 && b.CellId == guidedAnimalCell).OrderBy(b => b.Id).ToArray();
            if (animals.Length == 0) { guidedAnimalCell = -1; return; }
            guidedAnimalIndex = Math.Min(guidedAnimalIndex, animals.Length - 1);
            Beast animal = animals[guidedAnimalIndex];
            GuidedSheet(g, new RectangleF(24, 630, 380, 218));
            Typography.Line(g, animal.Count + " " + animal.Kind.ToString().ToLowerInvariant(), new RectangleF(44, 648, 290, 43), 28, MapPaper.Ink, TypeRole.Heading, true);
            GuidedButton(g, "\u00d7", new RectangleF(354, 643, 31, 29), delegate { guidedAnimalCell = -1; }, "Close wildlife details.");
            Typography.Draw(g, "Wild group in " + game.Place(animal.CellId) + ".\nMoves independently when turns end. Wildlife is peaceful during these early lessons.", new RectangleF(46, 704, 337, 89), 18, MapPaper.Ink, TypeRole.Body);
            if (animals.Length > 1) GuidedButton(g, "Next group  " + (guidedAnimalIndex + 1) + "/" + animals.Length, new RectangleF(46, 803, 336, 30),
                delegate { guidedAnimalIndex = (guidedAnimalIndex + 1) % animals.Length; }, "Read each animal group sharing this place.");
        }
    }
}
