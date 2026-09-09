using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text;
using Clio.Simulation;

namespace Clio.Tests
{
    public static class SimulationChecks
    {
        private static int assertions;
        public static string PacingReport { get; private set; }

        public static int Run()
        {
            assertions = 0;
            CheckRejectedCommands();
            CheckFission();
            CheckContactAndDomestication();
            CheckProvisionedCamp();
            CheckContactedHerdEcology();
            CheckGameOver();
            CheckReplayAndLongPlay();
            return assertions;
        }

        private static Game NewGame(int seed, bool fourBands)
        { return new Game(seed, LanguageStyle.Flowing, Ancestry.Human, fourBands, "Test hearth"); }

        private static void CheckRejectedCommands()
        {
            Game game = NewGame(110, false);
            Check(game.Player.Population == 50 && game.Actions == 2 && game.Turn == 1, "A founding band starts with fifty people and two actions.");
            Reject(game, delegate { game.Move(-1); }, "A negative destination is rejected without mutation.");
            Reject(game, delegate { game.Move(Int32.MaxValue); }, "An out-of-range destination is rejected without mutation.");
            Reject(game, delegate { game.Move(game.Player.CellId); }, "Moving to the current cell is rejected.");
            int distant = game.World.Cells.First(c => c.IsLand && c.Id != game.Player.CellId && !game.World.Cells[game.Player.CellId].Neighbors.Contains(c.Id)).Id;
            Reject(game, delegate { game.Move(distant); }, "Nonadjacent land cannot be reached in one move.");
            int ocean = game.World.Cells.First(c => !c.IsLand).Id;
            Reject(game, delegate { game.Move(ocean); }, "A land band cannot enter ocean.");
            Reject(game, delegate { game.Split(); }, "An undersized band cannot divide.");

            game.Beasts.Clear();
            Reject(game, delegate { game.Hunt(); }, "A hunt with no prey spends nothing.");
            Reject(game, delegate { game.Tame(); }, "An encounter with no animals spends nothing.");
            game.Player.Food = 0;
            Reject(game, delegate { game.Camp(); }, "An unaffordable camp spends nothing.");
            game.Beasts.Add(new Beast { Id = 0, CellId = game.Player.CellId, Count = 10, Kind = BeastKind.Wolves });
            Reject(game, delegate { game.Tame(); }, "An unaffordable offering spends nothing.");
            game.Player.Food = 500;
            game.Camp();
            Reject(game, delegate { game.Camp(); }, "An existing camp cannot be bought twice.");
            game.Actions = 0;
            Check(!game.CanMove(game.World.Cells[game.Player.CellId].Neighbors.First(n => game.World.Cells[n].IsLand)), "Movement availability respects exhausted actions.");
            Reject(game, delegate { game.Forage(); game.Hunt(); game.Tame(); game.Camp(); game.Split(); game.Move(distant); }, "Exhausted actions block all commands, including hidden random-state mutation.");

            Game counters = NewGame(119, false);
            counters.Forage(); counters.Forage();
            Check(counters.Actions == 0, "Each valid forage uses one action.");
            Check(counters.Knowledge.First(k => k.Id == "gathering").Progress == 1, "Repeated gathering in one turn advances distinct-turn knowledge only once.");
            counters.EndTurn(); counters.Forage();
            Check(counters.Knowledge.First(k => k.Id == "gathering").Progress == 2, "A later gathering turn advances practice.");
        }

        private static void CheckFission()
        {
            Game game = NewGame(710, false);
            game.Player.Population = 91;
            game.Player.Food = 501.25;
            game.Player.Culture[0] = 0.7;
            int oldPeople = game.Bands.Sum(b => b.Population);
            double oldFood = game.Bands.Sum(b => b.Food);
            int languages = game.Languages.Count;
            int origin = game.Player.CellId;
            game.Split();
            Check(game.Bands.Count == 2 && game.Actions == 1, "A valid division creates one autonomous band for one action.");
            Check(game.Bands.Sum(b => b.Population) == oldPeople, "Division conserves all people.");
            Check(Math.Abs(game.Bands.Sum(b => b.Food) - oldFood) < 1e-9, "Division conserves provisions, including fractional values.");
            Band daughter = game.Bands[1];
            Check(daughter.Population > 0 && daughter.CellId != origin && game.World.Cells[origin].Neighbors.Contains(daughter.CellId), "The daughter band starts on adjacent land.");
            Check(daughter.LanguageId == game.Player.LanguageId && game.Languages.Count == languages, "Political division alone does not invent a language.");
            Check(daughter.Ancestry == game.Player.Ancestry && daughter.Culture.SequenceEqual(game.Player.Culture), "The new band inherits ancestry and cultural practices.");
            daughter.Culture[0] = 0.1;
            Check(game.Player.Culture[0] == 0.7, "The two bands can subsequently develop culture independently.");
            game.Player.Population = 80; game.Player.Food = 0;
            Reject(game, delegate { game.Split(); }, "Division requires sufficient food without partial changes.");
        }

        private static void CheckContactAndDomestication()
        {
            Game game = NewGame(912, false);
            game.Beasts.Clear();
            Beast wolf = new Beast { Id = 0, CellId = game.Player.CellId, Kind = BeastKind.Wolves, Count = 8 };
            game.Beasts.Add(wolf);
            game.Player.Food = 10000;
            game.Tame();
            Check(game.Actions == 1 && wolf.LastContactTurn == game.Turn, "An attempted contact costs one action and records the turn even if rejected by animals.");
            Reject(game, delegate { game.Tame(); }, "The same group cannot be contacted twice in one turn.");
            Check(wolf.PositiveContacts <= 1, "One attempt yields at most one positive contact.");
            for (int i = 0; i < 40 && !wolf.Domestic && !game.IsOver; i++)
            {
                game.EndTurn();
                // This fixture isolates relationship mechanics from the separate food economy.
                game.Player.Food = 10000;
                game.Player.CellId = wolf.CellId;
                game.Tame();
                Reject(game, delegate { game.Tame(); }, "A repeat attempt never rerolls a contact result.");
            }
            Check(wolf.Domestic && wolf.PositiveContacts == 10 && wolf.OwnerId == game.Player.Id, "Ten distinct positive encounters establish an owned domestic lineage.");
            Check(game.Known("dogs") && !String.IsNullOrEmpty(wolf.BreedName) && wolf.Hardiness > 0 && wolf.Yield > 0, "Domestication records practical knowledge and local breed traits.");
            int count = wolf.Count;
            Check(count > 0, "The actual encountered animal population survives domestication.");
            int target = game.World.Cells[game.Player.CellId].Neighbors.First(n => game.World.Cells[n].IsLand && game.World.Cells[n].Terrain != Terrain.Ice);
            if (game.Actions == 0) game.EndTurn();
            game.Move(target);
            Check(game.Player.CellId == target && wolf.CellId == target, "Owned domestic animals travel with their band.");
            Check(game.NearbyBeast(true) == null, "Owned domestic animals cannot be tamed repeatedly as wild groups.");
        }

        private static void CheckGameOver()
        {
            Game game = NewGame(401, true);
            game.Player.Population = 0;
            int target = game.World.Cells[game.Player.CellId].Neighbors.First(n => game.World.Cells[n].IsLand);
            Check(game.IsOver && !game.CanMove(target), "A dead band has no available movement.");
            Reject(game, delegate { game.Move(target); game.Forage(); game.Hunt(); game.Tame(); game.Camp(); game.Split(); game.EndTurn(); }, "Game over freezes simulation commands and random streams.");

            Game starvation = NewGame(430, false);
            starvation.Player.Population = 1; starvation.Player.Food = 0;
            starvation.EndTurn();
            Check(starvation.IsOver && starvation.Player.Population == 0 && starvation.Player.Food == 0, "Final starvation clamps people and provisions to zero.");
            Check(starvation.Chronicle.Last().Text.Contains("ends here"), "The final loss records an ending in the chronicle.");
            Reject(starvation, delegate { starvation.EndTurn(); starvation.Forage(); }, "A naturally ended game cannot resume production.");
        }

        private static void CheckProvisionedCamp()
        {
            Game game = NewGame(621, false);
            game.Beasts.Clear();
            // Earlier mobile abundance may establish food keeping, but must not count as provisioned camp time.
            for (int i = 0; i < 6; i++) { game.Player.Food = 10000; game.EndTurn(); }
            Check(game.Known("stores") && !game.Known("gardens"), "Food keeping can emerge while mobile without granting gardens.");
            game.Camp();
            for (int i = 0; i < 5; i++) { game.Player.Food = 10000; game.EndTurn(); }
            Milestone gardens = game.Knowledge.First(k => k.Id == "gardens");
            Check(game.Known("hearth") && !gardens.Known && gardens.Progress == 5, "Gardens need six provisioned camp turns, independent of earlier abundance.");
            game.Player.Food = 0;
            game.EndTurn();
            Check(!gardens.Known && gardens.Progress == 0, "A hungry camp interrupts the provisioned sequence.");
            for (int i = 0; i < 5; i++) { game.Player.Food = 10000; game.EndTurn(); }
            Check(!gardens.Known && gardens.Progress == 5, "A new provisioned sequence begins after hunger.");
            int destination = game.World.Cells[game.Player.CellId].Neighbors.First(n => game.World.Cells[n].IsLand && game.World.Cells[n].Terrain != Terrain.Ice);
            game.Move(destination);
            Check(!game.Player.Settled && game.Player.HomeCell == -1 && gardens.Progress == 0, "Leaving camp resets its pending gardening sequence.");
            game.Camp();
            for (int i = 0; i < 6; i++) { game.Player.Food = 10000; game.EndTurn(); }
            Check(gardens.Known && gardens.Progress >= 6, "A sustained provisioned camp establishes gardening.");
        }

        private static void CheckContactedHerdEcology()
        {
            Game game = NewGame(882, false);
            game.Beasts.Clear();
            int origin = game.Player.CellId;
            game.Turn = 8;
            Beast herd = new Beast { Id = 0, CellId = origin, Kind = BeastKind.Aurochs, Count = 20,
                PositiveContacts = 1, LastContactTurn = game.Turn };
            game.Beasts.Add(herd);
            game.Player.Food = 10000;
            game.EndTurn();
            Check(herd.CellId == origin, "A just-fed wild herd lingers at the contact site.");
            Check(herd.Count > 20, "Recently contacted wild herds still participate in population growth.");
            bool migrated = false;
            for (int i = 0; i < 40; i++)
            {
                game.Player.Food = 10000;
                game.EndTurn();
                if (herd.CellId != origin) migrated = true;
            }
            Check(migrated && !herd.Domestic, "An abandoned wild herd resumes movement despite its earlier positive contact.");
        }

        private static void CheckReplayAndLongPlay()
        {
            int[] seeds = { Int32.MinValue, -37, 0, 1, 17, 734, 2026, Int32.MaxValue };
            StringBuilder report = new StringBuilder();
            foreach (int seed in seeds)
            {
                Game first = NewGame(seed, true);
                Game second = NewGame(seed, true);
                Check(Snapshot(first) == Snapshot(second), "The same seed reconstructs the same initial state.");
                int attempts = 0, migrations = 0, hunger = 0;
                for (int step = 0; step < 120; step++)
                {
                    for (int action = 0; action < 2 && !first.IsOver; action++)
                    {
                        int target;
                        string command = ChooseAction(first, out target);
                        if (command == "tame") attempts++;
                        if (command == "move") migrations++;
                        string a = Execute(first, command, target);
                        string b = Execute(second, command, target);
                        Check(a == b, "A deterministic replay returns the same command result.");
                        Check(Snapshot(first) == Snapshot(second), "A deterministic replay preserves complete mutable state after actions.");
                        AssertBounds(first);
                    }
                    Check(first.EndTurn() == second.EndTurn(), "Turn resolution is reproducible.");
                    Check(Snapshot(first) == Snapshot(second), "Turn replay includes autonomous bands, herds, chronicle, knowledge and random streams.");
                    AssertBounds(first);
                }
                hunger = first.Chronicle.Count(c => c.Text.StartsWith("Hunger takes", StringComparison.Ordinal));
                report.Append("seed ").Append(seed).Append(": turn ").Append(first.Turn).Append(", people ").Append(first.Player.Population)
                    .Append(", food ").Append(first.Player.Food.ToString("F0", CultureInfo.InvariantCulture)).Append(", hunger turns ").Append(hunger)
                    .Append(", tame attempts ").Append(attempts).Append(", domestic groups ").Append(first.Beasts.Count(b => b.Domestic))
                    .Append(", moves ").Append(migrations).Append(", known practices ").Append(first.Knowledge.Count(k => k.Known)).AppendLine();
            }
            PacingReport = report.ToString();
        }

        // A practical greedy food policy, with affordable relationship-building and an early camp.
        private static string ChooseAction(Game game, out int target)
        {
            target = game.Player.CellId;
            if (game.Turn == 1 && game.Actions == 2) return "camp";
            Beast tamable = game.NearbyBeast(true);
            if (tamable != null && tamable.LastContactTurn != game.Turn && game.Player.Food >= game.Upkeep(game.Player) * 3 + 18) return "tame";
            Beast prey = game.NearbyBeast(false);
            double current = game.ForageYield(game.Player.CellId, game.Player);
            if (prey != null && prey.Kind != BeastKind.Dragon && prey.Kind != BeastKind.Wolves && prey.PositiveContacts == 0)
            {
                double expected = Math.Min(prey.Count, 3) * (prey.Kind == BeastKind.Mammoths ? 75 : 38) * 0.8;
                if (expected > current * 1.2) return "hunt";
            }
            if (game.Actions == 2)
            {
                int best = game.World.Cells[game.Player.CellId].Neighbors.Where(n => game.CanMove(n) && game.World.Cells[n].Terrain != Terrain.Ice)
                    .OrderByDescending(n => game.ForageYield(n, game.Player)).DefaultIfEmpty(game.Player.CellId).First();
                double available = game.ForageYield(best, game.Player) - game.Player.Population * (game.Known("routes") ? 0.08 : 0.15);
                if (best != game.Player.CellId && available > current * 1.7) { target = best; return "move"; }
            }
            return "forage";
        }

        private static string Execute(Game game, string command, int target)
        {
            switch (command)
            {
                case "camp": return game.Camp();
                case "tame": return game.Tame();
                case "hunt": return game.Hunt();
                case "move": return game.Move(target);
                default: return game.Forage();
            }
        }

        private static void AssertBounds(Game game)
        {
            Check(game.Actions >= 0 && game.Actions <= 2, "Actions stay within their per-turn range.");
            foreach (Band band in game.Bands)
                Check(band.Population >= 0 && band.Food >= 0 && !Double.IsNaN(band.Food) && !Double.IsInfinity(band.Food)
                    && band.Cohesion >= 0 && band.Cohesion <= 1, "Population, food and cohesion remain valid over long play.");
            foreach (Beast beast in game.Beasts)
                Check(beast.Count >= 0 && beast.PositiveContacts >= 0 && beast.CellId >= 0 && beast.CellId < game.World.Cells.Length, "Animal populations and locations stay valid.");
            Check(game.Depletion.All(value => value >= 0 && value <= 1 && !Double.IsNaN(value)), "Ecological depletion remains bounded.");
        }

        private static void Reject(Game game, Action action, string message)
        {
            string before = Snapshot(game);
            action();
            Check(before == Snapshot(game), message);
        }

        private static string Snapshot(Game game)
        {
            StringBuilder state = new StringBuilder();
            state.Append(game.Seed).Append('/').Append(game.Turn).Append('/').Append(game.Actions).Append('|');
            foreach (Band band in game.Bands)
            {
                state.Append(band.Id).Append('/').Append(band.CellId).Append('/').Append(band.Population).Append('/').Append(band.LanguageId)
                    .Append('/').Append(band.SafeTurns).Append('/').Append(band.Name).Append('/').Append(band.Ancestry).Append('/').Append(band.Settled).Append('/').Append(band.HomeCell);
                Number(state, band.Food); Number(state, band.Cohesion);
                foreach (double value in band.Culture) Number(state, value);
            }
            foreach (Beast beast in game.Beasts)
            {
                state.Append('|').Append(beast.Id).Append('/').Append(beast.CellId).Append('/').Append(beast.Count).Append('/').Append(beast.PositiveContacts)
                    .Append('/').Append(beast.LastContactTurn).Append('/').Append(beast.Kind).Append('/').Append(beast.Domestic).Append('/').Append(beast.OwnerId).Append('/').Append(beast.BreedName);
                Number(state, beast.Hardiness); Number(state, beast.Yield);
            }
            foreach (Milestone knowledge in game.Knowledge) state.Append('|').Append(knowledge.Id).Append('/').Append(knowledge.Progress).Append('/').Append(knowledge.Target).Append('/').Append(knowledge.Known);
            foreach (LanguageProfile language in game.Languages)
            {
                state.Append('|').Append(language.Id).Append('/').Append(language.ParentId).Append('/').Append(language.RootId).Append('/').Append(language.Name).Append('/').Append(language.SoundChange);
                foreach (string concept in language.Words.Keys.OrderBy(key => key, StringComparer.Ordinal))
                    state.Append('/').Append(concept).Append('=').Append(language.Words[concept]).Append('@').Append(language.Etymons[concept]);
            }
            foreach (ChronicleEntry entry in game.Chronicle) state.Append('|').Append(entry.Turn).Append(':').Append(entry.Text);
            foreach (int cell in game.Explored.OrderBy(value => value)) state.Append(',').Append(cell);
            foreach (double value in game.Depletion) Number(state, value);
            // Include private counters and PRNG states so a rejected command cannot silently alter future results.
            foreach (FieldInfo field in typeof(Game).GetFields(BindingFlags.Instance | BindingFlags.NonPublic).OrderBy(f => f.Name, StringComparer.Ordinal))
            {
                state.Append('|').Append(field.Name).Append('=');
                object value = field.GetValue(game);
                HashSet<int> set = value as HashSet<int>;
                if (set != null) foreach (int item in set.OrderBy(item => item)) state.Append(item).Append(',');
                else state.Append(Convert.ToString(value, CultureInfo.InvariantCulture));
            }
            return state.ToString();
        }

        private static void Number(StringBuilder output, double value) { output.Append('/').Append(value.ToString("R", CultureInfo.InvariantCulture)); }
        private static void Check(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException("Simulation check failed: " + message);
            assertions++;
        }
    }
}
