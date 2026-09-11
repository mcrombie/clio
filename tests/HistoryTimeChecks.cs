using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using Clio.Simulation;

namespace Clio.Tests
{
    public static class HistoryTimeChecks
    {
        private static int assertions;

        public static int Run()
        {
            assertions = 0;
            Game legacy = new Game(new GameSettings(73421, LanguageStyle.Flowing, Ancestry.Human, false, ""));
            Game themed = new Game(new GameSettings(73421, LanguageStyle.Flowing, Ancestry.Human, false, "") { FoundingCulture = CultureTemplateId.EarlyEgyptian });
            Check(legacy.Pace == HistoryPace.LegacySeasons && themed.Pace == HistoryPace.LegacySeasons, "Both released constructor forms retain legacy seasons.");
            string[] seasons = { "Spring", "Summer", "Autumn", "Winter" };
            double[] factors = { 1.15, 1.0, 0.85, 0.40 };
            for (int i = 1; i <= 48; i++)
            {
                legacy.Turn = i;
                Check(legacy.Season == seasons[((i - 1) / 3) % 4] && legacy.SeasonFactor == factors[((i - 1) / 3) % 4], "Legacy labels and factors keep the exact released cadence.");
            }

            foreach (HistoryPace pace in Enum.GetValues(typeof(HistoryPace)))
            {
                Game game = NewGame(pace);
                Check(HistoryTime.ElapsedYears(game) == 0, "A founding begins at relative year zero.");
                int years = pace == HistoryPace.Generations ? 25 : pace == HistoryPace.Centuries ? 100 : 0;
                Check(HistoryTime.YearsPerTurn(pace) == years, "Each pace exposes its explicit calendar span.");
                game.EndTurn();
                Check(game.Turn == 2 && HistoryTime.ElapsedYears(game) == years, "One ordinary chapter advances the selected calendar exactly once.");
                Check(HistoryTime.Label(game, 1) == (years > 0 ? "Year 0" : "Chapter 01"), "Dated and abstract stories label their founding honestly.");
                Check(HistoryTime.Label(game, 2) == (years > 0 ? "Year " + years.ToString(CultureInfo.InvariantCulture) : "Chapter 02"), "Chronicle labels agree with the selected pace.");
                game.Turn = Int32.MaxValue;
                Check(HistoryTime.ElapsedYears(game) == ((long)Int32.MaxValue - 1) * years, "Long-running calendars use overflow-safe year arithmetic.");
                Check(!String.IsNullOrWhiteSpace(HistoryTime.SpanLabel(game)), "Every pace has a readable span description.");
                if (pace == HistoryPace.LegacySeasons) continue;
                HashSet<string> conditions = new HashSet<string>();
                object ecology = typeof(Game).GetField("ecologyRandom", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(game);
                object actions = typeof(Game).GetField("actionRandom", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(game);
                for (int turn = 1; turn <= 200; turn++)
                {
                    game.Turn = turn;
                    conditions.Add(game.Season);
                    Check(!seasons.Contains(game.Season), "Historical chapters describe prevailing years rather than season-long generations.");
                    Check(game.SeasonFactor >= 0.7 && game.SeasonFactor <= 1.15, "Aggregate conditions vary productivity without imposing a generation-long winter penalty.");
                    Check(game.Season == HistoryTime.ConditionLabel(game, turn), "The timeline and simulation use the same condition label.");
                    if (turn % 2 == 0) Check(game.Season == HistoryTime.ConditionLabel(game, turn - 1), "Prevailing conditions span coherent neighboring chapters.");
                }
                Check(conditions.Count == 4, "A long observation includes all four kinds of prevailing conditions.");
                Check(ecology.Equals(typeof(Game).GetField("ecologyRandom", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(game)) &&
                    actions.Equals(typeof(Game).GetField("actionRandom", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(game)), "Calendar inspection consumes no simulation randomness.");
            }

            Game generation = NewGame(HistoryPace.Generations);
            Game century = NewGame(HistoryPace.Centuries);
            Game unnumbered = NewGame(HistoryPace.Abstract);
            for (int step = 0; step < 100; step++)
            {
                string command = AutoplayPolicy.Choose(generation).Command;
                Execute(generation, command); Execute(century, command); Execute(unnumbered, command);
                Check(generation.Player.Food == century.Player.Food && generation.Player.Food == unnumbered.Player.Food &&
                    generation.Player.Population == century.Player.Population && generation.Player.Population == unnumbered.Player.Population,
                    "Changing the historical calendar scale does not silently multiply resource or demographic ticks.");
                Check(HistoryTime.ElapsedYears(century) == HistoryTime.ElapsedYears(generation) * 4 && HistoryTime.ElapsedYears(unnumbered) == 0,
                    "Identical histories can carry generation, century or abstract calendars.");
            }
            bool invalid = false;
            try { NewGame((HistoryPace)99); } catch (ArgumentOutOfRangeException) { invalid = true; }
            Check(invalid, "An unknown pace is rejected before starting a world.");
            return assertions;
        }

        private static Game NewGame(HistoryPace pace)
        { return new Game(new GameSettings(73421, LanguageStyle.Flowing, Ancestry.Human, false, "Calendar hearth") { FoundingCulture = CultureTemplateId.Generated, Pace = pace }); }

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
        { if (!condition) throw new InvalidOperationException("History time check failed: " + message); assertions++; }
    }
}
