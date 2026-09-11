using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using Clio.Simulation;

namespace Clio.Tests
{
    public static class SaltEconomyChecks
    {
        private static int checks;
        public static string PacingReport;
        public static int Run()
        {
            checks = 0; PacingReport = "";
            Initialization(); Migration(); Collection(); ShortageAndRecovery(); Splitting(); HiddenInformation(); AutonomousPlay();
            return checks;
        }
        private static void Check(bool value, string message) { checks++; if (!value) throw new InvalidOperationException("Salt check: " + message); }
        private static Game Create(int seed, SimulationRules rules, bool enabled, bool four = false, HistoryPace pace = HistoryPace.Abstract)
        { return new Game(new GameSettings(seed, LanguageStyle.Flowing, Ancestry.Human, four, "Salt review") { FoundingCulture = CultureTemplateId.Zhol, Pace = pace, Rules = rules, CulturalPlaceNames = true, SaltEnabled = enabled }); }
        private static object Field(object value, string name)
        { return value.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public).GetValue(value); }
        private static void Initialization()
        {
            foreach (SimulationRules rules in Enum.GetValues(typeof(SimulationRules)))
            foreach (int seed in Enumerable.Range(-8, 17).Concat(new[] { Int32.MinValue, 73421, Int32.MaxValue }))
            {
                Game salted = Create(seed, rules, true, true), old = Create(seed, rules, false, true), repeated = Create(seed, rules, true, true);
                Check(Snapshot(salted) == Snapshot(repeated), "Salt initialization and all source/knowledge fields are deterministic.");
                Check(Snapshot(salted.World) == Snapshot(old.World) && Snapshot(salted.Beasts) == Snapshot(old.Beasts), "Salt does not change terrain generation or initial wildlife.");
                Check(Field(salted, "actionRandom").Equals(Field(old, "actionRandom")) && Field(salted, "ecologyRandom").Equals(Field(old, "ecologyRandom")), "Salt consumes neither established random stream.");
                foreach (Band band in salted.Bands)
                {
                    Check(band.Salt == SaltEconomy.Need(band) * 4 && band.SaltShortageTurns == 0, "Every founder starts with four turns of salt.");
                    int[] reachable = salted.World.Cells[band.CellId].Neighbors.Concat(new[] { band.CellId }).Where(n => SaltEconomy.Source(salted, n) != SaltSource.None &&
                        salted.World.Cells[n].IsLand && !EncounterRules.HostileAt(salted, n, band.Id)).ToArray();
                    Check(reachable.Length > 0, "Every founder has salt at a safe current or adjacent place.");
                    Check(reachable.Any(n => salted.KnownPlace(band.Id, n) != null), "The guaranteed source lies in the founder's observed neighborhood.");
                }
                int[] sources = SaltEconomy.KnownSources(salted).ToArray();
                Check(sources.Length > 0 && sources.SequenceEqual(sources.OrderBy(n => n)) && sources.All(salted.Explored.Contains), "Player source listings are sorted and contain only explored locations.");
                int hidden = salted.World.Cells.First(c => !salted.Explored.Contains(c.Id) && SaltEconomy.Source(salted, c.Id) != SaltSource.None).Id;
                Check(!sources.Contains(hidden), "An existing distant source does not leak through the known-source list.");
                SaltSource fixedSource = SaltEconomy.Source(salted, hidden); salted.World.Cells[hidden].Terrain = Terrain.Ocean;
                Check(SaltEconomy.Source(salted, hidden) == fixedSource, "Source geology remains fixed instead of being recomputed from mutable terrain during reads.");
            }
            Game disabled = Create(7, SimulationRules.Classic, false);
            string state = Snapshot(disabled); disabled.GatherSalt();
            Check(Snapshot(disabled) == state && !SaltEconomy.CanGather(disabled, disabled.Player) && !SaltEconomy.KnownSources(disabled).Any(), "Disabled rules reject collection without mutation.");
            EconomyForecast forecast = BandEconomy.Forecast(disabled, disabled.Player);
            Check(forecast.StartingSalt == 0 && forecast.SaltNeed == 0 && forecast.SaltConsumed == 0 && forecast.EndingSalt == 0 && forecast.SaltLosses == 0 && forecast.EndingSaltShortageTurns == 0, "Disabled forecasts retain zero salt fields.");
        }
        private static void Migration()
        {
            foreach (SimulationRules rules in Enum.GetValues(typeof(SimulationRules)))
            {
                Game game = Create(73421, rules, false, true); game.Forage(); game.EndTurn();
                string world = Snapshot(game.World), beasts = Snapshot(game.Beasts), history = Snapshot(game.Chronicle); object random = Field(game, "actionRandom"), ecology = Field(game, "ecologyRandom");
                int turn = game.Turn, actions = game.Actions; double food = game.Player.Food; int population = game.Player.Population;
                game.EnableSaltEconomy();
                Check(game.SaltEnabled && game.Turn == turn && game.Actions == actions && game.Player.Food == food && game.Player.Population == population, "Migration changes no time, action, food or population.");
                Check(Snapshot(game.World) == world && Snapshot(game.Beasts) == beasts && Snapshot(game.Chronicle) == history && random.Equals(Field(game, "actionRandom")) && ecology.Equals(Field(game, "ecologyRandom")), "Migration preserves established history, geography, wildlife and RNG streams.");
                Check(game.Bands.Where(b => b.Population > 0).All(b => b.Salt == SaltEconomy.Need(b) * 4), "Every living migrated band receives the documented starting reserve.");
                string enabled = Snapshot(game); game.EnableSaltEconomy(); Check(Snapshot(game) == enabled, "Repeated migration cannot refill reserves or alter the source map.");
            }
        }
        private static void Collection()
        {
            Game game = Create(73421, SimulationRules.MobileUnits, true); Band band = game.Player;
            int source = SaltEconomy.KnownSources(game).First(); band.CellId = source; band.Salt = 0; band.SaltShortageTurns = 2;
            double amount = SaltEconomy.GatherYield(game, band); double food = band.Food; int actions = game.Actions; object rng = Field(game, "actionRandom");
            game.GatherSalt(); Check(band.Salt == amount && amount == SaltEconomy.Need(band) * 5 && game.Actions == actions - 1, "One action gathers precisely five current turns of salt.");
            Check(band.Food == food && band.SaltShortageTurns == 2 && rng.Equals(Field(game, "actionRandom")), "Collection spends no food or RNG and does not instantly cure an ongoing shortage.");
            game.Actions = 0; string state = Snapshot(game); game.GatherSalt(); Check(Snapshot(game) == state, "Collection with exhausted actions is atomic.");
            game.Actions = 2; band.CellId = game.World.Cells.First(c => game.Explored.Contains(c.Id) && SaltEconomy.Source(game, c.Id) == SaltSource.None).Id;
            state = Snapshot(game); game.GatherSalt(); Check(Snapshot(game) == state, "Collection away from a source is atomic.");
            band.Population = 0; state = Snapshot(game); game.GatherSalt(); Check(Snapshot(game) == state, "A dead household cannot gather salt.");
        }
        private static void ShortageAndRecovery()
        {
            foreach (SimulationRules rules in Enum.GetValues(typeof(SimulationRules)))
            foreach (HistoryPace pace in new[] { HistoryPace.LegacySeasons, HistoryPace.Abstract })
            {
                Game game = Create(73421, rules, true, false, pace); game.Beasts.Clear(); Band band = game.Player;
                band.Settled = true; band.SafeTurns = 4; band.Salt = 0; game.World.Cells[band.CellId].Temperature = .5;
                for (int close = 1; close <= 5; close++)
                {
                    band.Food = 10000; int before = band.Population; double salt = band.Salt, cohesion = band.Cohesion;
                    string state = Snapshot(game); EconomyForecast expected = BandEconomy.Forecast(game, band);
                    Check(Snapshot(game) == state, "Salt forecasts are pure, including all private simulation state.");
                    Check(expected.SaltNeed == before / 10.0 && expected.StartingSalt == salt && expected.SaltConsumed == 0 && expected.EndingSaltShortageTurns == close,
                        "Deficient forecasts consume only available salt and advance the exact shortage counter.");
                    Check(expected.Births == 0 && expected.SaltLosses == (close <= 3 ? 0 : Math.Max(1, (int)Math.Ceiling(before * .02))), "Three complete deficient turns are a grace period; later losses and blocked births are explicit.");
                    game.EndTurn();
                    Check(band.Population == expected.EndingPopulation && band.Population == before - expected.SaltLosses && band.Food == expected.EndingFood && band.Salt == expected.EndingSalt && band.SaltShortageTurns == expected.EndingSaltShortageTurns,
                        "The actual household exactly matches its salted forecast in both encounter/time rules.");
                    Check(Math.Abs(band.Cohesion - Math.Max(.1, Math.Min(1, cohesion + .015) - SaltEconomy.CohesionPenalty(close))) < 1e-12, "Cohesion applies the documented successive shortage penalty.");
                    Check(SaltEconomy.GatheringMultiplier(band) == 1 - Math.Min(3, close) * .1, "Gathering impairment tracks shortage severity and is capped.");
                }
                band.Food = 10000; band.Salt = SaltEconomy.Need(band) / 2; EconomyForecast partial = BandEconomy.Forecast(game, band); game.EndTurn();
                Check(partial.SaltConsumed == partial.StartingSalt && partial.EndingSalt == 0 && band.SaltShortageTurns == 6, "Partial provisioning consumes available reserves but does not end a deficit.");
                band.Food = 10000; band.Salt = SaltEconomy.Need(band); EconomyForecast recovered = BandEconomy.Forecast(game, band); game.EndTurn();
                Check(recovered.SaltLosses == 0 && band.SaltShortageTurns == 0 && band.Salt == 0 && SaltEconomy.GatheringMultiplier(band) == 1, "One fully supplied close resets impairment and prevents further salt losses.");
                band.Salt = SaltEconomy.Need(band) * 10; band.Food = 10000; if (game.Turn % 2 != 0) game.EndTurn();
                EconomyForecast growth = BandEconomy.Forecast(game, band); game.EndTurn(); Check(growth.Births > 0 && band.Population == growth.EndingPopulation, "Growth resumes when ordinary food and recovery requirements are satisfied.");
            }
            Game limited = Create(73421, SimulationRules.MobileUnits, true); limited.Beasts.Clear(); limited.Player.Population = 1; limited.Player.Salt = 0; limited.Player.SaltShortageTurns = 3; limited.Player.Food = 100;
            EconomyForecast terminal = BandEconomy.Forecast(limited, limited.Player); limited.EndTurn();
            Check(terminal.SaltLosses == 1 && limited.IsOver && limited.Player.Population == 0, "Salt losses cannot exceed the living population and can end a one-person household.");
        }
        private static void Splitting()
        {
            foreach (SimulationRules rules in Enum.GetValues(typeof(SimulationRules)))
            {
                Game game = Create(73421, rules, true); game.Player.Population = 101; game.Player.Food = 1000; game.Player.Salt = 47.3; game.Player.SaltShortageTurns = 2;
                double before = game.Player.Salt, ratio = SaltEconomy.ReserveTurns(game.Player); int bands = game.Bands.Count; game.Split();
                Check(game.Bands.Count == bands + 1, "The split fixture creates a daughter band."); Band child = game.Bands.Last();
                Check(Math.Abs(game.Player.Salt + child.Salt - before) < 1e-12 && Math.Abs(SaltEconomy.ReserveTurns(child) - ratio) < 1e-12 && Math.Abs(SaltEconomy.ReserveTurns(game.Player) - ratio) < 1e-12,
                    "Splitting conserves salt and divides it proportionally to people.");
                Check(child.SaltShortageTurns == 2 && game.Player.SaltShortageTurns == 2, "Splitting cannot erase an existing shortage.");
            }
        }
        private static void Execute(Game game, string command)
        {
            if (command.StartsWith("move:", StringComparison.Ordinal)) { game.Move(Int32.Parse(command.Substring(5), CultureInfo.InvariantCulture)); return; }
            if (command.StartsWith("attack-animal:", StringComparison.Ordinal)) { game.AttackAnimal(Int32.Parse(command.Substring(14), CultureInfo.InvariantCulture)); return; }
            if (command.StartsWith("befriend-animal:", StringComparison.Ordinal)) { game.BefriendAnimal(Int32.Parse(command.Substring(16), CultureInfo.InvariantCulture)); return; }
            if (command.StartsWith("attack-band:", StringComparison.Ordinal)) { game.AttackBand(Int32.Parse(command.Substring(12), CultureInfo.InvariantCulture)); return; }
            switch (command) { case "salt": game.GatherSalt(); break; case "forage": game.Forage(); break; case "hunt": game.Hunt(); break; case "tame": game.Tame(); break; case "camp": game.Camp(); break; case "split": game.Split(); break; case "end": game.EndTurn(); break; default: throw new Exception("Unknown salt autoplay command: " + command); }
        }
        private static void HiddenInformation()
        {
            foreach (SimulationRules rules in Enum.GetValues(typeof(SimulationRules)))
            {
                Game original = Create(73421, rules, true, true), changed = Create(73421, rules, true, true);
                original.Player.Salt = changed.Player.Salt = SaltEconomy.Need(original.Player) * 2;
                original.Player.Food = changed.Player.Food = 400;
                foreach (Cell cell in changed.World.Cells.Where(c => !changed.Explored.Contains(c.Id)))
                { cell.Terrain = Terrain.Ice; cell.Forage = .999; cell.Temperature = .001; }
                foreach (Beast animal in changed.Beasts.Where(b => !changed.Explored.Contains(b.CellId))) { animal.Kind = BeastKind.Dragon; animal.Count = 999; }
                foreach (Band band in changed.Bands.Where(b => !changed.Explored.Contains(b.CellId))) { band.Population = 99999; band.Salt = 0; band.SaltShortageTurns = 50; }
                AutoplayDecision before = AutoplayPolicy.Choose(original), after = AutoplayPolicy.Choose(changed);
                Check(before.Command == after.Command && before.Reason == after.Reason, "Hidden terrain, bands and animals cannot change the player's salt-seeking decision.");
                Check(SaltEconomy.KnownSources(original).SequenceEqual(SaltEconomy.KnownSources(changed)), "Known source read models ignore hidden-world mutations.");
            }
        }
        private static void AutonomousPlay()
        {
            int survived = 0, total = 0, gathers = 0, deficits = 0, independentSupplied = 0;
            foreach (SimulationRules rules in Enum.GetValues(typeof(SimulationRules)))
            foreach (int seed in new[] { 73421, -9137, 0, 17, 41, 9281 })
            {
                Game game = Create(seed, rules, true, true), replay = Create(seed, rules, true, true); int commands = 0, sourceGather = 0;
                while (!game.IsOver && game.Turn <= 140 && commands++ < 500)
                {
                    int turn = game.Turn, actions = game.Actions; string before = commands % 17 == 1 ? Snapshot(game) : null;
                    AutoplayDecision choice = AutoplayPolicy.Choose(game), repeated = AutoplayPolicy.Choose(game);
                    Check(choice != null && choice.Command == repeated.Command && choice.Reason == repeated.Reason && (before == null || Snapshot(game) == before), "Salt-aware autoplay is deterministic and read-only.");
                    if (choice.Command == "salt") { gathers++; sourceGather++; Check(SaltEconomy.CanGather(game, game.Player), "Autoplay only collects at a known usable source."); }
                    Execute(game, choice.Command); Execute(replay, choice.Command);
                    Check(game.Turn > turn || game.Actions < actions || game.IsOver, "Every salt-aware choice makes legal progress instead of a failed/no-op loop.");
                    if (game.Turn > turn) { if (game.Player.SaltShortageTurns > 0) deficits++; if (game.Bands.Skip(1).Any(b => b.Population > 0 && b.SaltShortageTurns == 0 && b.Salt > 0)) independentSupplied++; }
                    if (commands % 30 == 0) Check(Snapshot(game) == Snapshot(replay), "Salt gathering, sources and independent-band decisions replay exactly.");
                }
                Check(sourceGather > 0, "Autoplay actually uses the salt economy in each seeded journey.");
                Check(Snapshot(game) == Snapshot(replay), "The full salt-enabled journey replays all state and random streams.");
                if (!game.IsOver && game.Turn > 140) survived++; total++;
            }
            Check(survived >= total * 2 / 3, "Most unassisted seeded salt-enabled bands sustain a long journey (" + survived + "/" + total + ", gathers " + gathers + ", deficits " + deficits + ").");
            Check(independentSupplied > 100 && gathers > 50, "Independent peoples and player autoplay actively replenish reserves over time.");
            PacingReport = "Salt balance: " + survived + "/" + total + " bands reached turn141; " + gathers + " collection actions; " + deficits + " player deficient closes; " + independentSupplied + " closes with supplied independent bands.";
        }
        private static string Snapshot(object value)
        {
            StringBuilder buffer = new StringBuilder(); Append(buffer, value);
            using (SHA256 hash = SHA256.Create()) return BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(buffer.ToString())));
        }
        private static void Append(StringBuilder text, object value)
        {
            if (value == null) { text.Append("null;"); return; } Type type = value.GetType(); text.Append(type.FullName).Append(':');
            if (type.IsPrimitive || type.IsEnum || value is string) { text.Append(Convert.ToString(value, CultureInfo.InvariantCulture)).Append(';'); return; }
            IDictionary dictionary = value as IDictionary;
            if (dictionary != null) { foreach (object key in dictionary.Keys.Cast<object>().OrderBy(k => Convert.ToString(k, CultureInfo.InvariantCulture), StringComparer.Ordinal)) { Append(text, key); Append(text, dictionary[key]); } return; }
            IEnumerable sequence = value as IEnumerable;
            if (sequence != null) { foreach (object item in sequence) Append(text, item); text.Append("end;"); return; }
            foreach (FieldInfo field in type.GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public).OrderBy(f => f.Name, StringComparer.Ordinal)) { text.Append(field.Name); Append(text, field.GetValue(value)); }
        }
    }
}
