using System;
using System.Diagnostics;

namespace Clio.Tests
{
    internal static class Program
    {
        private static int Main()
        {
            try
            {
                Stopwatch timer = Stopwatch.StartNew();
                int world = WorldChecks.Run(); Console.WriteLine("PASS world: " + world + " assertions");
                int language = LanguageChecks.Run(); Console.WriteLine("PASS language: " + language + " assertions");
                int simulation = SimulationChecks.Run(); Console.WriteLine("PASS simulation: " + simulation + " assertions");
                int autoplay = AutoplayChecks.Run(); Console.WriteLine("PASS autoplay: " + autoplay + " assertions");
                Console.WriteLine(AutoplayChecks.PacingReport);
                int historical = HistoricalCultureChecks.Run(); Console.WriteLine("PASS historical cultures: " + historical + " assertions");
                int zhol = ZholLanguageChecks.Run(); Console.WriteLine("PASS Zhol language: " + zhol + " assertions");
                int places = PlaceNameChecks.Run(); Console.WriteLine("PASS cultural place names: " + places + " assertions");
                int time = HistoryTimeChecks.Run(); Console.WriteLine("PASS historical time: " + time + " assertions");
                int domestic = DomesticEffectsChecks.Run(); Console.WriteLine("PASS domestic effects: " + domestic + " assertions");
                int encounters = UnitEncounterChecks.Run(); Console.WriteLine("PASS mobile encounters: " + encounters + " assertions");
                Console.WriteLine(UnitEncounterChecks.PacingReport);
                int salt = SaltEconomyChecks.Run(); Console.WriteLine("PASS salt economy: " + salt + " assertions");
                Console.WriteLine(SaltEconomyChecks.PacingReport);
                int tribes = TribeChecks.Run(); Console.WriteLine("PASS tribes: " + tribes + " assertions");
                Console.WriteLine(TribeChecks.PacingReport);
                int terrain = TerrainTravelChecks.Run(); Console.WriteLine("PASS terrain travel: " + terrain + " assertions");
                Console.WriteLine(TerrainTravelChecks.PacingReport);
                int personalities = BandPersonalityChecks.Run(); Console.WriteLine("PASS band personalities: " + personalities + " assertions");
                Console.WriteLine(BandPersonalityChecks.PacingReport);
                int gatherings = GatheringChecks.Run(); Console.WriteLine("PASS gatherings: " + gatherings + " assertions");
                Console.WriteLine(GatheringChecks.PacingReport);
                Console.WriteLine("PASS total: " + (world + language + simulation + autoplay + historical + zhol + places + time + domestic + encounters + salt + tribes + terrain + personalities + gatherings) + " assertions in " + timer.Elapsed.TotalSeconds.ToString("0.00") + "s");
                return 0;
            }
            catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
        }
    }
}
