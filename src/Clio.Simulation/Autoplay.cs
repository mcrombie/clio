using System;
using System.Globalization;
using System.Linq;

namespace Clio.Simulation
{
    public sealed class AutoplayDecision
    {
        public string Command;
        public string Reason;
    }

    /// <summary>
    /// An observer-friendly steward for the player's band. Choosing never spends
    /// an action, changes simulation state, or consumes either random stream.
    /// The caller executes and records the returned ordinary game command.
    /// </summary>
    public static class AutoplayPolicy
    {
        public static AutoplayDecision Choose(Game game)
        {
            if (game == null) throw new ArgumentNullException("game");
            if (game.IsOver) return null;
            if (game.TribesEnabled) return ChooseTribe(game);
            if (game.Actions <= 0) return Decide("end", game.Pace == HistoryPace.LegacySeasons ?
                "The chapter's work is done; let the season unfold." : "The chapter's work is done; let the history unfold.");
            if (game.SaltEnabled)
            {
                AutoplayDecision salt = ChooseSalt(game, game.Player);
                if (salt != null) return salt;
            }
            if (game.WoodEnabled)
            {
                AutoplayDecision wood = ChooseWood(game, game.Player);
                if (wood != null) return wood;
            }
            if (game.Rules == SimulationRules.MobileUnits) return ChooseMobile(game, game.Player);

            Band band = game.Player;
            Cell here = game.World.Cells[band.CellId];
            double animalCare = game.Beasts.Where(b => b.CellId == band.CellId && b.Domestic && b.OwnerId == band.Id &&
                b.Count > 0 && b.Kind == BeastKind.Wolves).Sum(b => b.Count * 0.3);
            if (game.Pace != HistoryPace.LegacySeasons) animalCare = BandEconomy.DomesticEffects(game, band).AnimalCare;
            double upkeep = game.Upkeep(band) + animalCare;
            double food = band.Food;
            double gathering = game.ForageYield(here.Id, band);
            int destination = BestDestination(game, gathering);

            // Reserve the last action for gathering when provisions are scarce.
            // Moving with two actions leaves one available to feed the band.
            if (food < upkeep * 1.4)
            {
                if (destination != here.Id && game.Actions >= 2 && (!game.TerrainTravelEnabled || TravelRules.MoveCost(game, band, here.Id, destination) == 1))
                    return Move(destination, "Seek richer known ground, with time left to gather food.");
                Beast emergencyPrey = game.NearbyBeast(false);
                if (WorthHunting(game, emergencyPrey, gathering) && food + gathering < upkeep)
                    return Decide("hunt", "Gathering cannot feed everyone; a nearby herd offers a chance.");
                return Decide("forage", "Put food aside before the next chapter.");
            }

            // A camp gives shelter and a modest continuing yield. Give a healthy
            // camp time to establish gardens before abandoning it for a small gain.
            bool developingCamp = band.Settled && !game.Known("gardens") && food >= upkeep * 3.5 && gathering >= upkeep * 0.4;
            if (destination != here.Id && !developingCamp &&
                (game.Actions >= 2 || food >= upkeep * 3 && game.Depletion[here.Id] >= 0.85) &&
                (!game.TerrainTravelEnabled || TravelRules.MoveCost(game, band, here.Id, destination) == 1 || food >= upkeep * 2))
                return Move(destination, "Leave tired ground for a more productive known place.");

            bool winterShelter = HistoryTime.HasExposureRisk(game, band);
            if (!band.Settled && food >= 30 + upkeep * (winterShelter ? 1.5 : 2.8) &&
                (winterShelter || game.Depletion[here.Id] <= 0.55 && gathering >= upkeep * 0.65))
            {
                AutoplayDecision camp = PrepareCamp(game, band, winterShelter ? (game.Pace == HistoryPace.LegacySeasons ?
                    "Raise shelter against the winter cold." : "Raise shelter for a community facing harsh years.") : "A secure surplus can support a lasting hearth.");
                if (camp != null) return camp;
            }

            // The simulation itself picks the daughter's neighboring home. Only
            // ask it to do so when every possible destination is already known.
            // This keeps even the choice to divide independent of hidden geography.
            if ((!game.SaltEnabled || SaltEconomy.ReserveTurns(band) >= 4) && band.Population >= 90 && food >= upkeep * 3.5 && here.Neighbors.All(n => game.Explored.Contains(n)) &&
                here.Neighbors.Any(n => game.World.Cells[n].IsLand && game.World.Cells[n].Terrain != Terrain.Ice))
                return Decide("split", "Share the surplus and give a daughter band room to grow.");

            Beast companion = game.NearbyBeast(true);
            bool alreadyCompanions = companion != null && game.Beasts.Any(b => b.CellId == band.CellId && b.Domestic &&
                b.OwnerId == band.Id && b.Count > 0 && b.Kind == companion.Kind);
            if (companion != null && !alreadyCompanions && companion.LastContactTurn != game.Turn && food >= upkeep * 3 + 18)
                return Decide("tame", "Offer food and patiently build trust with nearby animals.");

            Beast prey = game.NearbyBeast(false);
            if (WorthHunting(game, prey, gathering) && (food < upkeep * 5 || !game.Known("tracking")))
                return Decide("hunt", "An unbefriended herd offers more food than gathering here.");

            if (food >= upkeep * 7 && band.Settled)
                return Decide("end", "The stores are full; let the ground recover around the hearth.");

            if (game.Pace != HistoryPace.LegacySeasons)
                return Decide("forage", game.Season == "Harsh years" ? "Strengthen provisioning through these harsh years." : "Build the community's capacity to meet future needs.");
            return Decide("forage", game.Season == "Winter" ? "Gather what winter allows and protect the food reserve." : "Build provisions for leaner seasons.");
        }

        private static int BestDestination(Game game, double gathering)
        {
            Band band = game.Player;
            int best = band.CellId;
            double travel = band.Population * (game.Known("routes") ? 0.08 : 0.15);
            double threshold = gathering * (band.Settled && game.Known("gardens") ? 2.1 : 1.7) + 8;
            // Assess only adjacent explored cells. Stable ID order resolves ties.
            foreach (int id in game.World.Cells[band.CellId].Neighbors.OrderBy(n => n))
            {
                if (!game.Explored.Contains(id) || !game.CanMove(id)) continue;
                Cell candidate = game.World.Cells[id];
                if (candidate.Terrain == Terrain.Ice) continue;
                double coldCost = Math.Max(0, Math.Ceiling(band.Population * (candidate.Temperature < 0.24 ? 1.35 : 1)) - game.Upkeep(band));
                double available = game.ForageYield(id, band) - travel - coldCost;
                if (available > threshold) { threshold = available; best = id; }
            }
            return best;
        }

        private static AutoplayDecision ChooseMobile(Game game, Band player)
        {
            UnitProfile people = EncounterRules.Band(game, player);
            double needs = game.Upkeep(player) + BandEconomy.DomesticEffects(game, player).AnimalCare;
            double gathering = game.ForageYield(player.CellId, player);
            int[] safe = game.World.Cells[player.CellId].Neighbors.Where(n => game.Explored.Contains(n) && game.CanMoveBand(player.Id, n) &&
                game.World.Cells[n].Terrain != Terrain.Ice).OrderByDescending(n => game.ForageYield(n, player)).ThenBy(n => n).ToArray();
            Beast[] seen = game.Beasts.Where(b => b.Count > 0 && game.Explored.Contains(b.CellId) &&
                (b.CellId == player.CellId || game.World.Cells[player.CellId].Neighbors.Contains(b.CellId))).ToArray();
            Band[] enemies = game.Bands.Where(b => b.Population > 0 && b.Id != player.Id && game.Explored.Contains(b.CellId) &&
                EncounterRules.BandsHostile(game, player.Id, b.Id) && (b.CellId == player.CellId || game.World.Cells[player.CellId].Neighbors.Contains(b.CellId))).ToArray();
            bool danger = seen.Any(b => b.CellId == player.CellId && EncounterRules.Animal(game, b).Hostile && EncounterRules.Animal(game, b).Strength > people.Strength * 0.85) ||
                enemies.Any(b => EncounterRules.Band(game, b).Strength > people.Strength * 0.9);
            if (danger && safe.Length > 0) return Move(safe[0], "Keep the community away from a stronger hostile unit.");
            Band enemy = enemies.FirstOrDefault(b => EncounterRules.Outlook(game, player.Id, UnitKind.Band, b.Id).CanAttack && EncounterRules.Band(game, b).Strength < people.Strength * 0.8);
            if (enemy != null && player.Food > needs * 1.5)
                return Decide("attack-band:" + enemy.Id.ToString(CultureInfo.InvariantCulture), "Drive back a weaker hostile band; leave neutral peoples in peace.");

            double travel = player.Population * (game.Known("routes") ? 0.08 : 0.15);
            if (game.ActionsFor(player.Id) == 2 && safe.Length > 0 && game.ForageYield(safe[0], player) - travel > gathering * (player.Settled && game.Known("gardens") ? 2.1 : 1.7) + 8 &&
                (!game.TerrainTravelEnabled || TravelRules.MoveCost(game, player, player.CellId, safe[0]) == 1 || player.Food >= needs * 2) &&
                (!player.Settled || game.Known("gardens") || player.Food < needs * 3.5))
                return Move(safe[0], "Seek productive known ground without walking into a hostile group.");
            if (player.Food < needs * 1.4) return Decide("forage", "Meet the people's needs before risking another encounter.");
            if (!player.Settled && !EncounterRules.HostileAt(game, player.CellId, player.Id) && player.Food >= 30 + needs * 2.8 &&
                game.Depletion[player.CellId] <= 0.55 && gathering >= needs * 0.65)
            {
                AutoplayDecision camp = PrepareCamp(game, player, "Establish shelter on secure ground.");
                if (camp != null) return camp;
            }
            if ((!game.SaltEnabled || SaltEconomy.ReserveTurns(player) >= 4) && player.Population >= 90 && player.Food >= needs * 3.5 && safe.Length > 0 && game.World.Cells[player.CellId].Neighbors.All(n => game.Explored.Contains(n)))
                return Decide("split", "Give a daughter band a safe known home and a share of the surplus.");

            Beast companion = seen.Where(b => !b.Domestic && b.Kind != BeastKind.Dragon &&
                !game.Beasts.Any(pet => pet.Domestic && pet.OwnerId == player.Id && pet.Count > 0 && pet.Kind == b.Kind))
                .Where(b => EncounterRules.Outlook(game, player.Id, UnitKind.Animal, b.Id).CanBefriend && EncounterRules.Animal(game, b).FriendChance >= 0.4 &&
                    EncounterRules.Animal(game, b).Strength < people.Strength * 1.9 && player.Food >= needs * 3 + EncounterRules.Animal(game, b).OfferingCost + (b.CellId == player.CellId ? 0 : travel))
                .OrderByDescending(b => b.PositiveContacts).ThenBy(b => b.CellId == player.CellId ? 0 : 1).ThenBy(b => b.Id).FirstOrDefault();
            if (companion != null)
                return Decide("befriend-animal:" + companion.Id.ToString(CultureInfo.InvariantCulture), "Follow a manageable moving group and build trust with that same lineage.");
            Beast prey = seen.Where(b => !b.Domestic && b.PositiveContacts == 0 && b.Kind != BeastKind.Dragon &&
                    EncounterRules.Outlook(game, player.Id, UnitKind.Animal, b.Id).CanAttack && EncounterRules.Animal(game, b).Strength < people.Strength * 0.85)
                .OrderBy(b => b.Id).FirstOrDefault(b =>
                {
                    UnitProfile target = EncounterRules.Animal(game, b);
                    double kills = Math.Min(b.Count, Math.Floor((people.Strength * 0.42 + target.Wounds) / EncounterRules.HealthPerAnimal(b.Kind)));
                    double expected = kills * (b.Kind == BeastKind.Mammoths ? 85 : b.Kind == BeastKind.Aurochs ? 38 : b.Kind == BeastKind.Wolves ? 12 : 28);
                    return expected - (b.CellId == player.CellId ? 0 : travel) > gathering * 1.25;
                });
            if (prey != null && player.Food < needs * 5)
                return Decide("attack-animal:" + prey.Id.ToString(CultureInfo.InvariantCulture), "Hunt a weaker unbefriended group while preserving the community's safety.");
            if (player.Settled && player.Food >= needs * 7) return Decide("end", "Capacity is secure; let the land recover and watch the groups move.");
            return Decide("forage", "Strengthen provisioning while observing the living world.");
        }

        private static bool WorthHunting(Game game, Beast prey, double gathering)
        {
            if (prey == null || prey.Kind == BeastKind.Wolves || prey.Kind == BeastKind.Dragon || prey.PositiveContacts > 0) return false;
            double chance = game.Known("tracking") ? 0.9 : 0.8;
            if (game.Pace != HistoryPace.LegacySeasons) chance = BandEconomy.HuntChance(game, prey);
            double expected = Math.Min(prey.Count, 3) * (prey.Kind == BeastKind.Mammoths ? 75 : 38) * chance;
            return expected > gathering * 1.35;
        }

        private static AutoplayDecision ChooseSalt(Game game, Band band)
        {
            if (SaltEconomy.ReserveTurns(band) > 3.25) return null;
            double needs = game.Upkeep(band) + BandEconomy.DomesticEffects(game, band).AnimalCare;
            if (band.Food < needs * 1.5) return null; // Keep food for the imminent close.
            if (game.Rules == SimulationRules.MobileUnits)
            {
                double strength = EncounterRules.Band(game, band).Strength;
                bool danger = game.Beasts.Any(b => b.Count > 0 && b.CellId == band.CellId && game.Explored.Contains(b.CellId) &&
                    EncounterRules.Animal(game, b).Hostile && EncounterRules.Animal(game, b).Strength > strength * .85) ||
                    game.Bands.Any(b => b.Id != band.Id && b.Population > 0 && game.Explored.Contains(b.CellId) &&
                        (b.CellId == band.CellId || game.World.Cells[band.CellId].Neighbors.Contains(b.CellId)) &&
                        EncounterRules.BandsHostile(game, band.Id, b.Id) && EncounterRules.Band(game, b).Strength > strength * .9);
                if (danger) return null;
            }
            if (SaltEconomy.CanGather(game, band)) return Decide("salt", "Replenish the community's salt before its reserve runs out.");
            int step = SaltEconomy.NextSourceStep(game, band);
            if (step >= 0 && step != band.CellId && game.CanMoveBand(band.Id, step))
                return Move(step, "Follow a safe remembered route toward a salt source.");
            return null;
        }

        private static AutoplayDecision Move(int destination, string reason)
        { return Decide("move:" + destination.ToString(CultureInfo.InvariantCulture), reason); }

        private static AutoplayDecision ChooseWood(Game game, Band band)
        {
            if (!game.WoodEnabled || WoodEconomy.ReserveTurns(band) >= 3 || !WoodEconomy.CanGather(game, band)) return null;
            double needs = WoodEconomy.BaseUpkeep(game, band) + BandEconomy.DomesticEffects(game, band).AnimalCare;
            if (band.Food < needs * 1.5 || game.SaltEnabled && SaltEconomy.ReserveTurns(band) < 2) return null;
            if (game.Rules == SimulationRules.MobileUnits)
            {
                double strength = EncounterRules.Band(game, band).Strength;
                if (game.Beasts.Any(b => b.Count > 0 && b.CellId == band.CellId && game.Explored.Contains(b.CellId) &&
                    EncounterRules.Animal(game, b).Hostile && EncounterRules.Animal(game, b).Strength > strength * .85) ||
                    game.Bands.Any(b => b.Id != band.Id && b.Population > 0 && game.Explored.Contains(b.CellId) &&
                        (b.CellId == band.CellId || game.World.Cells[band.CellId].Neighbors.Contains(b.CellId)) &&
                        EncounterRules.BandsHostile(game, band.Id, b.Id) && EncounterRules.Band(game, b).Strength > strength * .9)) return null;
            }
            return Decide("wood", "Collect wood while food and salt are safe; cooking fires save food and protect against cold.");
        }

        private static AutoplayDecision PrepareCamp(Game game, Band band, string reason)
        {
            if (!game.WoodEnabled) return Decide("camp", reason);
            // Keep fuel after paying for shelter rather than immediately losing
            // the food-saving fire that made this surplus affordable.
            if (band.Wood >= WoodEconomy.CampCost + WoodEconomy.FuelNeed(band)) return Decide("camp", reason);
            if ((!game.SaltEnabled || SaltEconomy.ReserveTurns(band) >= 2) && WoodEconomy.CanGather(game, band))
                return Decide("wood", "Collect the 10 wood needed for a camp, with some left for cooking fires.");
            return null;
        }

        private static AutoplayDecision ChooseTribe(Game game)
        {
            Band band = game.ControlledBands.FirstOrDefault(b => game.ActionsFor(b.Id) > 0);
            if (band == null) return Decide("end", "Every band has finished its orders; let the whole tribe's turn close.");
            AutoplayDecision choice = ChooseSalt(game, band);
            if (choice == null && game.WoodEnabled) choice = ChooseWood(game, band);
            if (choice == null && game.GatheringsEnabled) choice = game.GatheringAutoplay(band);
            double needs = game.Upkeep(band) + BandEconomy.DomesticEffects(game, band).AnimalCare;
            TribeMembership status = game.TribeStatus(band.Id);
            if (choice == null && game.BandPersonalitiesEnabled)
            {
                int personal = game.PersonalityTravelTarget(band);
                if (personal >= 0) choice = Move(personal, game.DispositionFor(band.Id).Label + " instincts shape this household's journey.");
            }
            if (choice == null && !game.BandPersonalitiesEnabled && !status.IsLeader && status.TurnsAway >= 5 && band.Food >= needs * 1.5)
            {
                int next = ReunionStep(game, band, game.TribeReunionCell);
                if (next >= 0 && game.CanMoveBand(band.Id, next)) choice = Move(next, "Return this band to the leader before separation weakens the tribe.");
            }
            if (choice == null) choice = ChooseMobile(game, band);
            if (choice.Command == "end") choice = Decide("wait", "This band can rest while the other bands finish their work.");
            return Decide("band:" + band.Id.ToString(CultureInfo.InvariantCulture) + ":" + choice.Command, band.Name + ": " + choice.Reason);
        }

        private static int ReunionStep(Game game, Band band, int target)
        {
            if (target < 0 || target == band.CellId) return -1;
            if (game.TerrainTravelEnabled) return TravelRules.KnownStep(game, band, game.Explored, cell => cell == target);
            var queue = new System.Collections.Generic.Queue<int>();
            var first = new System.Collections.Generic.Dictionary<int, int>();
            queue.Enqueue(band.CellId); first[band.CellId] = -1;
            while (queue.Count > 0)
            {
                int cell = queue.Dequeue();
                foreach (int next in game.World.Cells[cell].Neighbors.OrderBy(n => n))
                {
                    if (first.ContainsKey(next) || !game.Explored.Contains(next) || !game.World.Cells[next].IsLand ||
                        game.World.Cells[next].Terrain == Terrain.Ice || EncounterRules.HostileAt(game, next, band.Id)) continue;
                    first[next] = cell == band.CellId ? next : first[cell];
                    if (next == target) return first[next]; queue.Enqueue(next);
                }
            }
            return -1;
        }

        private static AutoplayDecision Decide(string command, string reason)
        { return new AutoplayDecision { Command = command, Reason = reason }; }
    }
}
