using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text;
using Clio.Simulation;

namespace Clio.Tests
{
    public static class AutoplayChecks
    {
        private static int assertions;
        public static string PacingReport { get; private set; }

        public static int Run()
        {
            assertions = 0;
            CheckFoodAndPartialTurns();
            CheckShelterAndRelationships();
            CheckMovementAndFission();
            CheckPurityAndHiddenInformation();
            CheckLongPlay();
            return assertions;
        }

        private static Game NewGame(int seed)
        { return new Game(seed, LanguageStyle.Flowing, Ancestry.Human, false, "Autoplay hearth"); }

        private static void CheckFoodAndPartialTurns()
        {
            Game game = NewGame(73421);
            game.Beasts.Clear();
            game.Actions = 1;
            game.Player.Food = 0;
            Check(Choose(game) == "forage", "A hungry band uses its last action to gather.");
            Advance(game);
            Check(game.Actions == 0 && game.Player.Food > 0, "Taking over partway through a chapter spends exactly the remaining action.");
            Check(Choose(game) == "end", "Exhausted actions advance the chapter.");
            int turn = game.Turn;
            Advance(game);
            Check(game.Turn == turn + 1 && game.Actions == 2, "Autoplay resumes with the normal next chapter.");

            game.Player.Population = 0;
            string before = Snapshot(game, true);
            Check(AutoplayPolicy.Choose(game) == null, "A finished story has no further autoplay command.");
            Check(before == Snapshot(game, true), "Inspecting a finished story changes no state.");

            Game ending = NewGame(61);
            ending.Beasts.Clear();
            ending.Player.Population = 1;
            ending.Player.Food = 0;
            ending.Actions = 0;
            Advance(ending);
            Check(ending.IsOver && AutoplayPolicy.Choose(ending) == null, "Autoplay stops when normal turn resolution ends the band's story.");
        }

        private static void CheckShelterAndRelationships()
        {
            Game game = NewGame(73421);
            game.Beasts.Clear();
            Check(Choose(game) == "camp", "The initial food reserve supports an early hearth.");
            Advance(game);
            Check(game.Player.Settled && Choose(game) != "camp", "An established camp is never purchased again.");

            game.Player.Food = 500;
            Beast wolf = new Beast { Id = 0, Kind = BeastKind.Wolves, Count = 8, CellId = game.Player.CellId };
            game.Beasts.Add(wolf);
            Check(Choose(game) == "tame", "A secure camp can invest in animal contact.");
            Advance(game);
            Check(wolf.LastContactTurn == game.Turn, "A normal encounter records the turn.");
            game.Actions = 1;
            Check(Choose(game) != "tame", "The steward does not repeatedly contact the same herd in a chapter.");

            game.Beasts.Clear();
            game.Depletion[game.Player.CellId] = 0.9;
            game.Player.Food = 100;
            Beast mammoth = new Beast { Id = 1, Kind = BeastKind.Mammoths, Count = 20, CellId = game.Player.CellId };
            game.Beasts.Add(mammoth);
            Check(Choose(game) == "hunt", "A promising hunt supplements poor gathering.");
            mammoth.Kind = BeastKind.Dragon;
            Check(Choose(game) != "hunt", "The steward avoids speculative dragon hunts.");
            mammoth.Kind = BeastKind.Aurochs;
            mammoth.PositiveContacts = 5;
            mammoth.LastContactTurn = game.Turn;
            Check(Choose(game) != "hunt" && Choose(game) != "tame", "Existing animal trust is preserved and today's completed contact is respected.");

            game.Beasts.Clear();
            game.Player.Food = 500;
            foreach (Milestone knowledge in game.Knowledge) knowledge.Known = true;
            game.Depletion[game.Player.CellId] = 0;
            Check(Choose(game) == "end", "An abundant settled band can give its ground a rest.");

            game.Beasts.Add(new Beast { Id = 2, CellId = game.Player.CellId, Kind = BeastKind.Wolves, Count = 100, Domestic = true, OwnerId = 0 });
            game.Beasts.Add(new Beast { Id = 3, CellId = game.Player.CellId, Kind = BeastKind.Wolves, Count = 8 });
            Check(Choose(game) == "forage", "Animal upkeep raises the needed reserve, and the steward avoids adding another costly pack.");
        }

        private static void CheckMovementAndFission()
        {
            Game game = NewGame(73421);
            game.Beasts.Clear();
            game.Depletion[game.Player.CellId] = 1;
            game.Player.Food = 0;
            int oldCell = game.Player.CellId;
            string move = Choose(game);
            Check(move.StartsWith("move:", StringComparison.Ordinal), "A hungry band with two actions seeks richer known ground when its ground is depleted.");
            int target = Int32.Parse(move.Substring(5), CultureInfo.InvariantCulture);
            Check(game.Explored.Contains(target) && game.World.Cells[oldCell].Neighbors.Contains(target), "Autoplay travel is adjacent and explored.");
            Advance(game);
            Check(game.Player.CellId == target && game.Actions == 1, "Travel uses the same one-action command as manual play.");
            Check(Choose(game) == "forage", "The action left after an urgent move is spent gathering.");

            Game splitting = NewGame(902);
            splitting.Beasts.Clear();
            splitting.Player.Population = 96;
            splitting.Player.Food = 700;
            splitting.Player.Settled = true;
            splitting.Player.HomeCell = splitting.Player.CellId;
            Check(Choose(splitting) == "split", "A populous secure band can found a daughter polity.");
            int population = splitting.Player.Population;
            double food = splitting.Player.Food;
            Advance(splitting);
            Check(splitting.Bands.Count == 2 && splitting.Bands.Sum(b => b.Population) == population,
                "Autoplay division uses the ordinary population-conserving command.");
            Check(Math.Abs(splitting.Bands.Sum(b => b.Food) - food) < 1e-8, "Founding a daughter band conserves its provisions.");
            Check(Choose(splitting) != "split", "The smaller parent cannot immediately divide again.");
        }

        private static void CheckPurityAndHiddenInformation()
        {
            Game original = NewGame(609);
            string before = Snapshot(original, true);
            string command = Choose(original);
            for (int i = 0; i < 12; i++) Check(command == Choose(original), "Repeated inspection returns the same command.");
            Check(before == Snapshot(original, true), "Choosing touches no mutable state, world geometry, private counters or PRNG stream.");

            original.Beasts.Clear();
            original.Player.Food = 0;
            original.Depletion[original.Player.CellId] = 1;
            // Retain one legitimate visible destination while concealing another
            // adjacent tile to exercise the filter before any terrain reads.
            int concealed = original.World.Cells[original.Player.CellId].Neighbors.First(n => original.World.Cells[n].IsLand);
            original.Explored.Remove(concealed);
            command = Choose(original);
            string reason = AutoplayPolicy.Choose(original).Reason;
            foreach (Cell cell in original.World.Cells)
            {
                if (original.Explored.Contains(cell.Id)) continue;
                cell.Terrain = Terrain.Grassland;
                cell.Temperature = 1;
                cell.Forage = 1000;
                cell.Elevation = 1;
                cell.Moisture = 1;
                original.Depletion[cell.Id] = 0;
            }
            original.Beasts.Insert(0, new Beast { Id = 1000, CellId = concealed, Kind = BeastKind.Mammoths, Count = 1000 });
            original.Bands.Add(new Band { Id = 1, CellId = concealed, Population = 99999, Food = 999999, Name = "Unseen neighbors" });
            Check(command == Choose(original) && reason == AutoplayPolicy.Choose(original).Reason,
                "Hidden terrain, abundance, wildlife and polities cannot affect a decision or its explanation.");
            Check(Choose(original) != "move:" + concealed.ToString(CultureInfo.InvariantCulture), "Even impossibly rich unseen adjacent land cannot attract the steward.");

            original.Player.Population = 100;
            original.Player.Food = 2000;
            original.Player.Settled = true;
            Check(Choose(original) != "split", "The policy avoids delegating a daughter-home choice that could inspect concealed neighbors.");
        }

        private static void CheckLongPlay()
        {
            int[] seeds = { Int32.MinValue, -37, 0, 1, 17, 73421, 2026, Int32.MaxValue };
            StringBuilder report = new StringBuilder();
            foreach (int seed in seeds)
            {
                Game game = NewGame(seed);
                Game replay = NewGame(seed);
                Dictionary<string, int> counts = new Dictionary<string, int>();
                int steps = 0;
                while (!game.IsOver && game.Turn <= 180)
                {
                    string command = Choose(game);
                    Check(command == Choose(replay), "Identical stories independently choose identical actions.");
                    string key = command.StartsWith("move:", StringComparison.Ordinal) ? "move" : command;
                    counts[key] = counts.ContainsKey(key) ? counts[key] + 1 : 1;
                    Advance(game);
                    Execute(replay, command);
                    if (game.Turn % 12 == 0)
                        Check(Snapshot(game, false) == Snapshot(replay, false), "Replaying ordinary autoplay commands preserves complete simulation state.");
                    Check(game.Player.Population >= 0 && game.Player.Food >= 0 && !Double.IsNaN(game.Player.Food) && !Double.IsInfinity(game.Player.Food),
                        "Long autoplay keeps population and food finite and nonnegative.");
                    Check(++steps <= 180 * 3, "Each chapter finishes after at most two actions and an end command.");
                }
                Check(game.IsOver || game.Turn == 181, "Autoplay reaches the observation horizon or the band's natural ending.");
                if (game.IsOver) Check(AutoplayPolicy.Choose(game) == null, "A natural ending releases the steward.");
                report.Append("seed ").Append(seed).Append(": turn ").Append(game.Turn).Append(", people ").Append(game.Player.Population)
                    .Append(", food ").Append(game.Player.Food.ToString("F0", CultureInfo.InvariantCulture))
                    .Append(", bands ").Append(game.Bands.Count)
                    .Append(", hunger ").Append(game.Chronicle.Count(c => c.Text.StartsWith("Hunger takes", StringComparison.Ordinal)))
                    .Append(", practices ").Append(game.Knowledge.Count(k => k.Known))
                    .Append(", domestic ").Append(game.Beasts.Count(b => b.Domestic));
                foreach (KeyValuePair<string, int> pair in counts.OrderBy(p => p.Key, StringComparer.Ordinal))
                    report.Append(", ").Append(pair.Key).Append(' ').Append(pair.Value);
                report.AppendLine();
            }
            PacingReport = report.ToString();
        }

        private static string Choose(Game game)
        {
            AutoplayDecision decision = AutoplayPolicy.Choose(game);
            Check(decision != null && !String.IsNullOrWhiteSpace(decision.Command) && !String.IsNullOrWhiteSpace(decision.Reason),
                "Every living choice has a command and an observer-facing explanation.");
            return decision.Command;
        }

        private static void Advance(Game game)
        {
            string command = Choose(game);
            int turn = game.Turn, actions = game.Actions;
            Execute(game, command);
            Check(command == "end" ? game.Turn == turn + 1 : game.Turn == turn && game.Actions == actions - 1,
                "Every chosen command makes legal progress; rejected commands cannot form an autoplay loop.");
        }

        private static void Execute(Game game, string command)
        {
            if (command.StartsWith("move:", StringComparison.Ordinal)) { game.Move(Int32.Parse(command.Substring(5), CultureInfo.InvariantCulture)); return; }
            switch (command)
            {
                case "forage": game.Forage(); break;
                case "camp": game.Camp(); break;
                case "hunt": game.Hunt(); break;
                case "tame": game.Tame(); break;
                case "split": game.Split(); break;
                case "end": game.EndTurn(); break;
                default: throw new InvalidOperationException("Unknown autoplay command: " + command);
            }
        }

        private static string Snapshot(Game game, bool includeWorld)
        {
            StringBuilder output = new StringBuilder();
            foreach (FieldInfo field in typeof(Game).GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).OrderBy(f => f.Name, StringComparer.Ordinal))
            {
                if (!includeWorld && field.Name == "World") continue;
                output.Append(field.Name).Append('=');
                AppendValue(output, field.GetValue(game));
            }
            return output.ToString();
        }

        private static void AppendValue(StringBuilder output, object value)
        {
            if (value == null) { output.Append("null;"); return; }
            Type type = value.GetType();
            if (type.IsPrimitive || type.IsEnum || value is string)
            { output.Append(Convert.ToString(value, CultureInfo.InvariantCulture)).Append(';'); return; }
            IEnumerable values = value as IEnumerable;
            if (values != null)
            {
                output.Append('[');
                foreach (object item in values) AppendValue(output, item);
                output.Append(']'); return;
            }
            output.Append(type.Name).Append('{');
            foreach (FieldInfo field in type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).OrderBy(f => f.Name, StringComparer.Ordinal))
            { output.Append(field.Name).Append('='); AppendValue(output, field.GetValue(value)); }
            output.Append('}');
        }

        private static void Check(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException("Autoplay check failed: " + message);
            assertions++;
        }
    }
}
