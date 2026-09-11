using System;
using System.Diagnostics;

namespace Clio.Tests
{
    internal static class Program
    {
        private static int assertions, failures;

        private static int Main()
        {
            Stopwatch timer = Stopwatch.StartNew();
            // Each suite runs in isolation so one failure cannot hide the results of the suites after it.
            Suite("world", WorldChecks.Run, null);
            Suite("language", LanguageChecks.Run, null);
            Suite("simulation", SimulationChecks.Run, null);
            Suite("autoplay", AutoplayChecks.Run, () => AutoplayChecks.PacingReport);
            Suite("historical cultures", HistoricalCultureChecks.Run, null);
            Suite("Zhol language", ZholLanguageChecks.Run, null);
            Suite("cultural place names", PlaceNameChecks.Run, null);
            Suite("historical time", HistoryTimeChecks.Run, null);
            Suite("domestic effects", DomesticEffectsChecks.Run, null);
            Suite("mobile encounters", UnitEncounterChecks.Run, () => UnitEncounterChecks.PacingReport);
            Suite("salt economy", SaltEconomyChecks.Run, () => SaltEconomyChecks.PacingReport);
            Suite("tribes", TribeChecks.Run, () => TribeChecks.PacingReport);
            Suite("terrain travel", TerrainTravelChecks.Run, () => TerrainTravelChecks.PacingReport);
            Suite("band personalities", BandPersonalityChecks.Run, () => BandPersonalityChecks.PacingReport);
            Suite("gatherings", GatheringChecks.Run, () => GatheringChecks.PacingReport);
            string elapsed = timer.Elapsed.TotalSeconds.ToString("0.00");
            if (failures == 0) { Console.WriteLine("PASS total: " + assertions + " assertions in " + elapsed + "s"); return 0; }
            Console.WriteLine("FAIL total: " + failures + (failures == 1 ? " suite" : " suites") + " failed; " + assertions + " assertions passed in " + elapsed + "s");
            return 1;
        }

        private static void Suite(string name, Func<int> run, Func<string> report)
        {
            try
            {
                int count = run();
                assertions += count;
                Console.WriteLine("PASS " + name + ": " + count + " assertions");
                if (report != null) Console.WriteLine(report());
            }
            catch (Exception ex)
            {
                failures++;
                Console.WriteLine("FAIL " + name + ": " + ex.Message);
                Console.Error.WriteLine(ex);
            }
        }
    }
}
