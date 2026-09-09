using System;
using System.Globalization;

namespace Clio.Simulation
{
    public enum HistoryPace { LegacySeasons, Generations, Centuries, Abstract }

    /// <summary>
    /// A chapter is an aggregate historical interval, not a loop of annual ticks.
    /// Fixed-year calendars are relative to this fictional founding, never BCE/CE.
    /// Legacy stories retain their original three-chapter seasons exactly.
    /// </summary>
    public static class HistoryTime
    {
        public static int YearsPerTurn(HistoryPace pace)
        {
            switch (pace)
            {
                case HistoryPace.Generations: return 25;
                case HistoryPace.Centuries: return 100;
                case HistoryPace.LegacySeasons:
                case HistoryPace.Abstract: return 0;
                default: throw new ArgumentOutOfRangeException("pace");
            }
        }

        public static long ElapsedYears(Game game)
        {
            if (game == null) throw new ArgumentNullException("game");
            return Math.Max(0L, (long)game.Turn - 1) * YearsPerTurn(game.Pace);
        }

        public static string Label(Game game, int turn)
        {
            if (game == null) throw new ArgumentNullException("game");
            int years = YearsPerTurn(game.Pace);
            return years == 0 ? "Chapter " + Math.Max(1, turn).ToString("00", CultureInfo.InvariantCulture) :
                "Year " + (Math.Max(0L, (long)turn - 1) * years).ToString("N0", CultureInfo.InvariantCulture);
        }

        public static string SpanLabel(Game game)
        {
            if (game == null) throw new ArgumentNullException("game");
            int years = YearsPerTurn(game.Pace);
            if (years > 0) return years.ToString(CultureInfo.InvariantCulture) + " years / chapter";
            return game.Pace == HistoryPace.LegacySeasons ? "Seasons change every three chapters" : "An unnumbered span of history / chapter";
        }

        public static string ConditionLabel(Game game)
        {
            if (game == null) throw new ArgumentNullException("game");
            return ConditionLabel(game, game.Turn);
        }

        public static string ConditionLabel(Game game, int turn)
        {
            if (game == null) throw new ArgumentNullException("game");
            if (game.Pace == HistoryPace.LegacySeasons)
                return new[] { "Spring", "Summer", "Autumn", "Winter" }[((Math.Max(1, turn) - 1) / 3) % 4];
            return new[] { "Mild years", "Lean years", "Harsh years", "Abundant years" }[ConditionIndex(game.Seed, turn)];
        }

        public static double ConditionFactor(Game game)
        {
            if (game == null) throw new ArgumentNullException("game");
            if (game.Pace == HistoryPace.LegacySeasons)
                return new[] { 1.15, 1.0, 0.85, 0.40 }[((game.Turn - 1) / 3) % 4];
            return new[] { 1.0, 0.90, 0.72, 1.14 }[ConditionIndex(game.Seed, game.Turn)];
        }

        public static bool HasExposureRisk(Game game, Band band)
        {
            if (game == null) throw new ArgumentNullException("game");
            if (band == null) throw new ArgumentNullException("band");
            bool harsh = game.Pace == HistoryPace.LegacySeasons ? ConditionLabel(game) == "Winter" : ConditionLabel(game) == "Harsh years";
            return harsh && !band.Settled && game.World.Cells[band.CellId].Temperature < 0.35;
        }

        private static int ConditionIndex(int seed, int turn)
        {
            // Two chapters share a prevailing condition. Hashing the interval
            // gives varied histories without consuming either simulation PRNG.
            uint value = unchecked((uint)seed ^ ((uint)(Math.Max(1, turn) - 1) / 2u + 1u) * 2654435761u);
            value ^= value >> 16; value *= 2246822519u;
            value ^= value >> 13; value *= 3266489917u;
            value ^= value >> 16;
            return (int)(value % 4);
        }
    }
}
