using System;
using System.Linq;

namespace Clio.Simulation
{
    public sealed partial class Game
    {
        private bool battleTurnPending;
        private int tacticalTurnStage, tacticalTurnBandCursor, tacticalTurnAnimalCursor;
        private int[] tacticalTurnBandIds = new int[0], tacticalTurnAnimalIds = new int[0];

        // The recorded end command opens this continuation once. Battle orders
        // leave its cursors intact; closing each battle resumes the next actor.
        // Replaying those same commands reconstructs the suspended turn exactly.
        private string BeginTacticalTurn()
        {
            if (battleTurnPending) return ResumeTacticalTurn();
            battleTurnPending = true; tacticalTurnStage = 0;
            DiscoverAndShareBandPlaces();
            BeginTribeEconomy();
            if (BandPersonalitiesEnabled) ResolveVoluntaryBandMoves();
            if (GatheringsEnabled) ReconcileGatherings(false);
            Encounters.LastPlayerEconomy = null; Encounters.LastEconomyTurn = Turn;
            RecoverWounds();
            tacticalTurnBandIds = Bands.Where(b => !IsPlayerTribe(b.Id) && b.Population > 0).OrderBy(b => b.Id).Select(b => b.Id).ToArray();
            tacticalTurnBandCursor = 0; tacticalTurnAnimalCursor = -1;
            return ResumeTacticalTurn();
        }

        private string ResumeTacticalTurn()
        {
            if (BattleLocked) return "A regional battle interrupts the turn. Resolve it before the remaining world activity continues.";
            if (!battleTurnPending) return "The world is ready for orders.";
            if (tacticalTurnStage == 0)
            {
                while (tacticalTurnBandCursor < tacticalTurnBandIds.Length && !IsOver)
                {
                    int id = tacticalTurnBandIds[tacticalTurnBandCursor++];
                    Band band = Bands.Find(b => b.Id == id);
                    if (band == null || band.Population <= 0 || IsPlayerTribe(id)) continue;
                    ActMobileBand(band);
                    if (BattleLocked) return "An incoming attack opens a regional battle. The world turn will continue after its result is closed.";
                }
                tacticalTurnStage = 1;
                tacticalTurnAnimalIds = Beasts.Where(b => b.Count > 0 && !b.Domestic).OrderBy(b => b.Id).Select(b => b.Id).ToArray();
            }
            if (tacticalTurnStage == 1)
            {
                if (!IsOver) ActMobileAnimals();
                if (BattleLocked) return "An animal attack opens a regional battle. The world turn will continue after its result is closed.";
                tacticalTurnStage = 2;
            }
            // No combat occurs in settlement. Economy, growth and the turn
            // increment run only here, after every interrupted actor has finished.
            battleTurnPending = false;
            return CompleteMobileTurn();
        }
    }
}
