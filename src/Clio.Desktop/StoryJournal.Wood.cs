using System;
using System.Globalization;
using System.Linq;
using Clio.Simulation;

namespace Clio.Desktop
{
    internal sealed partial class StoryJournal
    {
        // Wood is opt-in. Old stories retain their existing notice order and totals.
        // Attribute collection and construction to real stock changes; fuel comes
        // from the end-turn receipt, after any autonomous travel or encounters.
        private void RecordWood(Game game, JournalSnapshot before, string command, bool ended)
        {
            if (!game.WoodEnabled || !before.WoodEnabled) return;
            int actorId = game.Player.Id;
            string order = command;
            if (command.StartsWith("band:", StringComparison.Ordinal))
            {
                int colon = command.IndexOf(':', 5);
                if (colon > 5)
                {
                    actorId = Int32.Parse(command.Substring(5, colon - 5), CultureInfo.InvariantCulture);
                    order = command.Substring(colon + 1);
                }
            }
            Band actor = game.Bands.FirstOrDefault(b => b.Id == actorId);
            JournalHousehold prior = before.Households.FirstOrDefault(b => b.Id == actorId);
            if (!ended && actor != null && prior != null)
            {
                if (order == "wood" && actor.Wood > prior.Wood)
                {
                    double collected = actor.Wood - prior.Wood;
                    Totals.WoodGatherActions++; Totals.WoodGathered += collected;
                    if (Totals.WoodGatherActions == 1)
                        AddTribalNotice(game, actor, StoryNoticeKind.Wood, "Your first wood collection",
                            actor.Name + " collects " + Number(collected) + " wood.",
                            "Wood reserve: " + Number(actor.Wood) + ", enough for " + Number(WoodEconomy.ReserveTurns(actor)) + " turns of fire at the current population.",
                            "Cooking fires reduce food needs and prevent exposure losses. Building a camp spends " + Number(WoodEconomy.CampCost) + " wood. Each band carries its own supply.", false);
                }
                if (order == "camp" && !prior.Settled && actor.Settled)
                    Totals.CampWoodSpent += Math.Max(0, prior.Wood - actor.Wood);
                if (!game.TribesEnabled && order == "split" && actor.Population < prior.Population)
                    Totals.WoodShared += Math.Max(0, prior.Wood - actor.Wood);
            }

            if (ended)
            {
                if (game.TribesEnabled)
                {
                    foreach (TribeEconomyReceipt entry in game.LastTribeEconomy.Where(e => e.Turn == before.Turn && before.Households.Any(b => b.Id == e.BandId)))
                        RecordWoodFuel(entry.Forecast);
                }
                else if (game.Encounters.LastEconomyTurn == before.Turn && game.Encounters.LastPlayerEconomy != null)
                    RecordWoodFuel(game.Encounters.LastPlayerEconomy);
                else if (game.Rules != SimulationRules.MobileUnits)
                {
                    Totals.WoodConsumed += before.Receipt.WoodConsumed;
                    Totals.FireFoodSaved += before.Receipt.FireFoodSaved;
                }
            }

            foreach (JournalHousehold previous in before.Households)
            {
                Band band = game.Bands.FirstOrDefault(b => b.Id == previous.Id);
                if (band == null) continue;
                if (previous.Population > 0 && band.Population <= 0) Totals.WoodLost += Math.Max(0, band.Wood);
                else if (game.TribesEnabled && !game.CanControlBand(band.Id)) Totals.WoodShared += Math.Max(0, band.Wood);
                if (band.Population <= 0 || game.TribesEnabled && !game.CanControlBand(band.Id)) continue;
                double reserve = WoodEconomy.ReserveTurns(band);
                if (previous.WoodReserve >= 2 && reserve < 2)
                    AddTribalNotice(game, band, StoryNoticeKind.Wood, "Wood is running low",
                        band.Name + " has " + Number(band.Wood) + " wood left: " + Number(reserve) + " turns of fire at its current population.",
                        "Without enough fuel, food needs return to their normal level and fires no longer protect this band from exposure.",
                        "Keep food and salt supplied first. Then collect wood; forests and wetlands provide more per action than open or barren ground.", false);
            }
        }

        private void RecordWoodFuel(EconomyForecast receipt)
        {
            if (receipt == null) return;
            Totals.WoodConsumed += receipt.WoodConsumed;
            Totals.FireFoodSaved += receipt.FireFoodSaved;
        }
    }
}
