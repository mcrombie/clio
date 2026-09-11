using System;
using System.Globalization;
using System.Linq;
using Clio.Simulation;

namespace Clio.Tests
{
    public static class DomesticEffectsChecks
    {
        private static int assertions;
        public static string PacingReport { get; private set; }

        public static int Run()
        {
            assertions = 0;
            CheckAssistance();
            CheckAccounting();
            CheckLineageContinuity();
            CheckLongHistory();
            return assertions;
        }

        private static Game NewGame(HistoryPace pace, int seed)
        { return new Game(new GameSettings(seed, LanguageStyle.Flowing, Ancestry.Human, false, "Living lineage") { FoundingCulture = CultureTemplateId.Generated, Pace = pace }); }

        private static Beast AddDogs(Game game, int count)
        {
            Beast herd = new Beast { Id = game.Beasts.Count, CellId = game.Player.CellId, Count = count,
                Kind = BeastKind.Wolves, Domestic = true, OwnerId = 0, BreedName = "Hearth dogs", Yield = 0.7 };
            game.Beasts.Add(herd); return herd;
        }

        private static Beast AddCattle(Game game, int count)
        {
            Beast herd = new Beast { Id = game.Beasts.Count, CellId = game.Player.CellId, Count = count,
                Kind = BeastKind.Aurochs, Domestic = true, OwnerId = 0, BreedName = "River cattle", Yield = 0.8 };
            game.Beasts.Add(herd); return herd;
        }

        private static void CheckAssistance()
        {
            Game game = NewGame(HistoryPace.Generations, 73421);
            game.Beasts.Clear();
            double forage = game.ForageYield(game.Player.CellId, game.Player);
            Beast prey = new Beast { Id = 100, CellId = game.Player.CellId, Kind = BeastKind.Deer, Count = 20 };
            double chance = BandEconomy.HuntChance(game, prey);
            Beast dogs = AddDogs(game, 8);
            Check(game.ForageYield(game.Player.CellId, game.Player) > forage, "Living dogs visibly improve an actual gathering yield.");
            double before = game.Player.Food;
            double forecast = game.ForageYield(game.Player.CellId, game.Player);
            game.Forage();
            Check(game.Player.Food - before == forecast && forecast > forage, "Gathering credits the displayed assisted yield.");
            Check(BandEconomy.HuntChance(game, prey) > chance, "Dogs improve the actual hunt probability.");
            DomesticEconomy effects = BandEconomy.DomesticEffects(game, game.Player);
            Check(effects.Dogs == 8 && effects.AnimalCare > 0 && effects.HuntingBonus == 0.12 && effects.GatheringMultiplier == 1.08,
                "The ledger reports the working group, its aid and its recurring care.");
            dogs.Count = 1000;
            foreach (Milestone milestone in game.Knowledge) milestone.Known = true;
            Check(BandEconomy.DomesticEffects(game, game.Player).GatheringMultiplier == 1.1 && BandEconomy.HuntChance(game, prey) <= 0.97,
                "Large groups cannot turn assistance into unlimited production or guaranteed hunts.");
            dogs.Count = 0;
            Check(BandEconomy.DomesticEffects(game, game.Player).HuntingBonus == 0 && BandEconomy.DomesticEffects(game, game.Player).GatheringMultiplier == 1,
                "An extinct historical lineage supplies neither aid nor upkeep.");
            dogs.Count = 8; dogs.OwnerId = 1;
            Check(BandEconomy.DomesticEffects(game, game.Player).Dogs == 0, "A neighboring polity's dogs cannot provide the player's benefits.");

            Game legacy = NewGame(HistoryPace.LegacySeasons, 73421);
            legacy.Beasts.Clear();
            double oldForage = legacy.ForageYield(legacy.Player.CellId, legacy.Player);
            AddDogs(legacy, 8);
            Check(legacy.ForageYield(legacy.Player.CellId, legacy.Player) == oldForage && BandEconomy.DomesticEffects(legacy, legacy.Player).HuntingBonus == 0.05,
                "Released stories retain their exact old gathering and hunting effects.");
            Check(BandEconomy.DomesticEffects(legacy, legacy.Player).AnimalCare == 2.4, "Legacy dog care remains 0.3 per animal.");

            // Give both hunts the same random draw between the unassisted and
            // assisted probabilities. The actual commands must then diverge.
            Game hunter = NewGame(HistoryPace.Generations, 19);
            Game assisted = NewGame(HistoryPace.Generations, 19);
            hunter.Beasts.Clear(); assisted.Beasts.Clear();
            Beast ordinaryPrey = new Beast { Id = 0, CellId = hunter.Player.CellId, Kind = BeastKind.Deer, Count = 20 };
            Beast assistedPrey = new Beast { Id = 0, CellId = assisted.Player.CellId, Kind = BeastKind.Deer, Count = 20 };
            hunter.Beasts.Add(ordinaryPrey); assisted.Beasts.Add(assistedPrey); AddDogs(assisted, 8);
            uint randomState = 1;
            while (NextDraw(randomState) < 0.82 || NextDraw(randomState) > 0.90) randomState++;
            System.Reflection.FieldInfo stream = typeof(Game).GetField("actionRandom", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            stream.SetValue(hunter, randomState); stream.SetValue(assisted, randomState);
            double startingFood = assisted.Player.Food;
            hunter.Hunt(); assisted.Hunt();
            Check(ordinaryPrey.Count == 20 && assistedPrey.Count == 17 && assisted.Player.Food == startingFood + 114,
                "At the same uncertain hunt draw, dog assistance changes a failed hunt into real provisions.");
            Check(hunter.Player.Population < assisted.Player.Population, "The improved hunt also prevents the failed hunt's actual casualties.");
        }

        private static double NextDraw(uint state)
        { state ^= state << 13; state ^= state >> 17; state ^= state << 5; if (state == 0) state = 0x9e3779b9; return state / 4294967296.0; }

        private static void CheckAccounting()
        {
            foreach (HistoryPace pace in Enum.GetValues(typeof(HistoryPace)))
            {
                foreach (double food in new[] { 0.0, 1.25, 49.5, 105.75, 600.125 })
                {
                    foreach (bool settled in new[] { false, true })
                    {
                        Game game = NewGame(pace, 2026);
                        game.Beasts.Clear();
                        AddDogs(game, 8);
                        Beast cattle = AddCattle(game, 20);
                        game.Player.Food = food;
                        game.Player.Settled = settled;
                        game.Player.HomeCell = settled ? game.Player.CellId : -1;
                        game.Player.SafeTurns = 3;
                        game.Turn = 10;
                        game.World.Cells[game.Player.CellId].Temperature = 0.2;
                        if (settled) foreach (Milestone milestone in game.Knowledge) milestone.Known = true;
                        EconomyForecast expected = BandEconomy.Forecast(game, game.Player);
                        Check(expected.CattleFood == BandEconomy.CattleFood(cattle) && expected.CattleFood > 0, "Forecast cattle output comes from the same live lineage as the inspector.");
                        Check(expected.ActualAnimalCare <= food + 1e-9 && expected.ActualAnimalCare <= expected.AnimalCare + 1e-9, "Realized care never charges capacity that did not exist.");
                        int population = game.Player.Population;
                        game.EndTurn();
                        Check(expected.EndingFood == game.Player.Food, "The complete food forecast matches ordinary turn resolution exactly, including clamping and retention.");
                        Check(expected.EndingPopulation == game.Player.Population && population + expected.Births - expected.HungerLosses - expected.ExposureLosses == game.Player.Population,
                            "Forecast births and losses reconcile to the actual population without inventing movements.");
                        Check(expected.NetFood == game.Player.Food - food, "Chapter net food reconciles to the actual before/after balance.");
                    }
                }
            }

            Game withCattle = NewGame(HistoryPace.Generations, 41);
            Game withoutCattle = NewGame(HistoryPace.Generations, 41);
            withCattle.Beasts.Clear(); withoutCattle.Beasts.Clear();
            Beast productive = AddCattle(withCattle, 20);
            double contribution = BandEconomy.CattleFood(productive) - BandEconomy.AnimalCare(withCattle, productive);
            withCattle.EndTurn(); withoutCattle.EndTurn();
            Check(Math.Abs(withCattle.Player.Food - withoutCattle.Player.Food - contribution * 0.95) < 1e-9,
                "Cattle create a real positive food difference after care and retention, rather than only a descriptive badge.");
        }

        private static void CheckLineageContinuity()
        {
            Game game = NewGame(HistoryPace.Generations, 73421);
            game.Beasts.Clear();
            Beast dogs = AddDogs(game, 8);
            Beast cattle = AddCattle(game, 20);
            int destination = game.World.Cells[game.Player.CellId].Neighbors.First(n => game.CanMove(n) && game.World.Cells[n].Terrain != Terrain.Ice);
            game.Move(destination);
            Check(dogs.CellId == destination && cattle.CellId == destination, "The community's living lineages accompany migration.");
            for (int chapter = 0; chapter < 240; chapter++)
            {
                game.Player.Food = 10000;
                game.EndTurn();
                if ((game.Turn - 1) % 6 == 0)
                {
                    Check(dogs.Count <= Math.Max(8, game.Player.Population / 3) && dogs.Count <= 24 &&
                        cattle.Count <= Math.Max(12, game.Player.Population) && cattle.Count <= 60,
                        "Historical animal generations stabilize within the community's supported group sizes.");
                }
            }
            Check(dogs.Domestic && cattle.Domestic && dogs.BreedName == "Hearth dogs" && cattle.BreedName == "River cattle",
                "Thousands of years preserve lineage identity while animal populations change.");
            Check(HistoryTime.ElapsedYears(game) >= 6000 && dogs.Count > 8 && cattle.Count > 20,
                "The calendar spans millennia while succeeding generations grow the established lineages.");
        }

        private static void CheckLongHistory()
        {
            System.Text.StringBuilder report = new System.Text.StringBuilder();
            foreach (int seed in new[] { Int32.MinValue, 0, 73421, Int32.MaxValue })
            {
                Game game = NewGame(HistoryPace.Generations, seed);
                int steps = 0;
                while (!game.IsOver && game.Turn <= 240)
                {
                    string command = AutoplayPolicy.Choose(game).Command;
                    int turn = game.Turn, actions = game.Actions;
                    EconomyForecast balance = command == "end" ? BandEconomy.Forecast(game, game.Player) : null;
                    Execute(game, command);
                    Check(command == "end" ? game.Turn == turn + 1 : game.Actions == actions - 1, "Historical autoplay always makes ordinary legal progress.");
                    if (balance != null) Check(balance.EndingFood == game.Player.Food && balance.EndingPopulation == game.Player.Population,
                        "The economy ledger remains exact through a developing multi-polity history.");
                    Check(++steps <= 720, "A 240-chapter observation completes within its bounded action count.");
                }
                Check(game.IsOver || HistoryTime.ElapsedYears(game) == 6000, "Autoplay observes six thousand relative years or the band's natural ending.");
                report.Append("seed ").Append(seed).Append(": ").Append(HistoryTime.Label(game, game.Turn))
                    .Append(", people ").Append(game.Player.Population).Append(", bands ").Append(game.Bands.Count)
                    .Append(", domestic ").Append(game.Beasts.Count(b => b.Domestic && b.OwnerId == 0))
                    .Append(", hunger ").Append(game.Chronicle.Count(c => c.Text.StartsWith("Hunger takes", StringComparison.Ordinal))).AppendLine();
            }
            PacingReport = report.ToString();
        }

        private static void Execute(Game game, string command)
        {
            if (command.StartsWith("move:", StringComparison.Ordinal)) { game.Move(Int32.Parse(command.Substring(5), CultureInfo.InvariantCulture)); return; }
            switch (command)
            {
                case "end": game.EndTurn(); break;
                case "forage": game.Forage(); break;
                case "camp": game.Camp(); break;
                case "tame": game.Tame(); break;
                case "hunt": game.Hunt(); break;
                case "split": game.Split(); break;
                default: throw new InvalidOperationException(command);
            }
        }

        private static void Check(bool condition, string message)
        { if (!condition) throw new InvalidOperationException("Domestic effects check failed: " + message); assertions++; }
    }
}
