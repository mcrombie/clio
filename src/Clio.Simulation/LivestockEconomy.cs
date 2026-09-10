using System;
using System.Globalization;
using System.Linq;

namespace Clio.Simulation
{
    /// <summary>Milk is a recurring food source; meat requires reducing an owned herd.</summary>
    public static class LivestockEconomy
    {
        public static bool IsLivestock(Beast herd)
        { return herd != null && herd.Domestic && herd.Count > 0 && (herd.Kind == BeastKind.Aurochs || herd.Kind == BeastKind.Goats); }

        public static bool CanDomesticate(Game game, Beast animal)
        { return game != null && animal != null && (!game.LivestockEnabled || animal.Kind != BeastKind.Deer); }

        public static string DisplayName(Game game, Beast animal)
        {
            if (animal == null) return "Animals";
            if (game != null && game.LivestockEnabled && animal.Domestic)
            {
                if (animal.Kind == BeastKind.Wolves) return "Dogs";
                if (animal.Kind == BeastKind.Aurochs) return "Cattle";
            }
            return animal.Kind.ToString();
        }

        public static double MilkFood(Game game, Beast herd)
        {
            if (game == null || !game.LivestockEnabled || !IsLivestock(herd)) return 0;
            Band owner = game.Bands.Find(b => b.Id == herd.OwnerId);
            if (owner == null || owner.Population <= 0 || owner.CellId != herd.CellId) return 0;
            return herd.Count * Math.Max(0, herd.Yield) * (herd.Kind == BeastKind.Aurochs ? .5 : .25);
        }

        public static int SlaughterCount(Game game, Beast herd)
        { return game == null || !game.LivestockEnabled || !IsLivestock(herd) ? 0 : Math.Min(herd.Count, Math.Max(1, herd.Count / 10)); }

        public static double MeatFood(Game game, Beast herd)
        { return SlaughterCount(game, herd) * (herd == null ? 0 : Math.Max(0, herd.Yield) * (herd.Kind == BeastKind.Aurochs ? 18 : 8)); }

        public static bool CanSlaughter(Game game, Band band, Beast herd)
        {
            return game != null && game.LivestockEnabled && !game.IsOver && band != null && band.Population > 0 &&
                game.CanControlBand(band.Id) && game.ActionsFor(band.Id) > 0 && IsLivestock(herd) &&
                herd.OwnerId == band.Id && herd.CellId == band.CellId;
        }

        internal static DomesticEconomy Effects(Game game, Band band)
        {
            DomesticEconomy result = new DomesticEconomy();
            foreach (Beast herd in game.Beasts.Where(b => b.Domestic && b.OwnerId == band.Id && b.Count > 0))
            {
                double care = BandEconomy.AnimalCare(game, herd);
                if (herd.Kind == BeastKind.Aurochs)
                { result.Cattle += herd.Count; result.CattleCare += care; result.CattleMilk += MilkFood(game, herd); }
                else if (herd.Kind == BeastKind.Goats)
                { result.Goats += herd.Count; result.GoatCare += care; result.GoatMilk += MilkFood(game, herd); }
                else if (herd.Kind == BeastKind.Wolves)
                { result.Dogs += herd.Count; result.DogCare += care; }
                else
                {
                    result.OtherCompanions += herd.Count; result.OtherCare += care;
                }
            }
            result.AnimalCare = result.DogCare + result.CattleCare + result.GoatCare + result.OtherCare;
            result.MilkFood = result.CattleMilk + result.GoatMilk;
            // Existing receipt consumers read CattleFood. It remains the total
            // passive herd food so no second food credit is introduced.
            result.CattleFood = result.MilkFood;
            if (game.Rules == SimulationRules.MobileUnits)
            {
                int mammoths = game.Beasts.Where(b => b.Domestic && b.Kind == BeastKind.Mammoths && b.OwnerId == band.Id && b.Count > 0).Sum(b => b.Count);
                result.GatheringMultiplier += Math.Min(.06, mammoths * .015);
            }
            int huntingDogs = game.Beasts.Where(b => b.Domestic && b.Kind == BeastKind.Wolves && b.OwnerId == band.Id && b.CellId == band.CellId && b.Count > 0).Sum(b => b.Count);
            result.HuntingBonus = Math.Min(.12, huntingDogs * .015);
            return result;
        }

        internal static Beast EmergencyHerd(Game game, Band band)
        {
            if (game == null || !game.LivestockEnabled || band == null || band.Population <= 0) return null;
            EconomyForecast close = BandEconomy.Forecast(game, band);
            double gathering = game.ForageYield(band.CellId, band);
            // Keep breeding/milking animals when an ordinary gathering action
            // can meet this turn's need. Slaughter only meaningfully better food.
            if (close.HungerLosses <= 0 || gathering >= close.Upkeep - Math.Max(0, band.Food - close.AnimalCare) - close.CampFood - close.MilkFood) return null;
            return game.Beasts.Where(b => IsLivestock(b) && b.OwnerId == band.Id && b.CellId == band.CellId && MeatFood(game, b) > gathering)
                .OrderByDescending(b => MeatFood(game, b)).ThenBy(b => b.Id).FirstOrDefault();
        }
    }

    public sealed partial class Game
    {
        private bool livestockEnabled;
        public bool LivestockEnabled { get { return livestockEnabled; } }

        public string EnableLivestock()
        {
            if (LivestockEnabled) return "Livestock already supports this story.";
            livestockEnabled = true;
            int released = 0;
            foreach (Beast deer in Beasts.Where(b => b.Kind == BeastKind.Deer && b.Domestic))
            {
                deer.Domestic = false; deer.OwnerId = -1; deer.PositiveContacts = 0;
                deer.LastContactTurn = -1; deer.BreedName = null; released++;
                UnitCondition state = Encounters.Read(UnitKind.Animal, deer.Id);
                if (state != null) { state.LastEncounterTurn = -1; state.HostileUntil = -1; }
            }
            foreach (Cell cell in World.Cells.Where(c => (c.Terrain == Terrain.Hills || c.Terrain == Terrain.Mountains) && c.Forage > .05).OrderBy(c => c.Id))
                if (LivestockHash(Seed, cell.Id) % 73 == 0) AddWildGoats(cell.Id);
            Band hearth = TribeLeaderBand ?? Player;
            int near = World.Cells[hearth.CellId].Neighbors.Concat(new[] { hearth.CellId })
                .Where(id => World.Cells[id].IsLand && World.Cells[id].Terrain != Terrain.Ice)
                .OrderBy(id => World.Cells[id].Terrain == Terrain.Hills || World.Cells[id].Terrain == Terrain.Mountains ? 0 : 1)
                .ThenBy(id => id == hearth.CellId ? 1 : 0).ThenBy(id => id).DefaultIfEmpty(-1).First();
            if (near >= 0 && !Beasts.Any(b => b.Kind == BeastKind.Goats && b.CellId == near && b.Count > 0)) AddWildGoats(near);
            Milestone dogs = Knowledge.Find(k => k.Id == "dogs");
            if (dogs != null) dogs.Description = "Build trust with wolves to raise dogs. Dogs improve hunting, require food, and do not increase gathering or produce food.";
            Milestone cattle = Knowledge.Find(k => k.Id == "herds");
            if (cattle != null) cattle.Description = "Build trust with aurochs to raise cattle. Each animal contributes milk; slaughter gives meat but reduces the herd.";
            return "Cattle and goats provide milk each turn and meat when slaughtered. Dogs help hunting. Deer remain wild." +
                (released > 0 ? " " + released + " formerly domestic deer group" + (released == 1 ? " has" : "s have") + " been released where they stood." : "");
        }

        private static uint LivestockHash(int seed, int cell)
        {
            unchecked { uint value = (uint)seed ^ (uint)cell * 0x9e3779b9u ^ 0x10a57acdu;
                value ^= value >> 16; value *= 0x7feb352du; value ^= value >> 15; value *= 0x846ca68bu; return value ^ (value >> 16); }
        }

        private void AddWildGoats(int cell)
        {
            int id = Beasts.Count == 0 ? 0 : Beasts.Max(b => b.Id) + 1;
            Beasts.Add(new Beast { Id = id, CellId = cell, Kind = BeastKind.Goats, Count = 12 + (int)((LivestockHash(Seed, cell) >> 8) % 17) });
        }

        public string SlaughterHerd(int id)
        {
            if (!LivestockEnabled) return "Livestock is not part of this story's current rules.";
            string error; if (!CanAct(out error)) return error;
            Beast herd = Beasts.Find(b => b.Id == id);
            if (!LivestockEconomy.CanSlaughter(this, ActionBand, herd)) return "Choose cattle or goats owned by this band on the same hex.";
            ActionPoints--;
            return ResolveSlaughter(ActionBand, herd);
        }

        private string ResolveSlaughter(Band band, Beast herd)
        {
            int count = LivestockEconomy.SlaughterCount(this, herd); double meat = LivestockEconomy.MeatFood(this, herd);
            UnitFrame actor = Rules == SimulationRules.MobileUnits ? Frame(UnitKind.Band, band.Id) : null;
            UnitFrame target = Rules == SimulationRules.MobileUnits ? Frame(UnitKind.Animal, herd.Id) : null;
            double foodBefore = Player.Food;
            SetLivestockCount(herd, herd.Count - count);
            band.Food += meat;
            string detail = band.Name + " slaughters " + count + " " + LivestockEconomy.DisplayName(this, herd).ToLowerInvariant() +
                " for " + meat.ToString("0.#", CultureInfo.InvariantCulture) + " food. " + herd.Count + " remain, providing " +
                LivestockEconomy.MilkFood(this, herd).ToString("0.#", CultureInfo.InvariantCulture) + " milk food per turn.";
            if (Rules == SimulationRules.MobileUnits)
                AddEncounter(EncounterKind.Slaughter, actor, target, foodBefore, meat, 0, 0, herd.PositiveContacts, herd.PositiveContacts, 0, 0,
                    "slaughtered", "Meat from the herd", detail);
            else Log("Meat from the herd. " + detail);
            return detail;
        }

        private void SetLivestockCount(Beast herd, int count)
        {
            int before = herd.Count; herd.Count = Math.Max(0, count);
            UnitCondition condition = Encounters.Read(UnitKind.Animal, herd.Id);
            if (condition == null) return;
            int wounds = condition.Wounds;
            if (before > 0 && herd.Count < before) wounds = (int)Math.Floor(Math.Max(0, wounds) * (double)herd.Count / before);
            condition.Wounds = Math.Max(0, Math.Min(wounds, herd.Count * EncounterRules.HealthPerAnimal(herd.Kind) - 1));
        }

        private void GrowLivestockHerd(Beast herd)
        {
            Band owner = Bands.Find(b => b.Id == herd.OwnerId);
            int capacity = herd.Kind == BeastKind.Dragon ? 1 : herd.Kind == BeastKind.Mammoths ? 8 : herd.Kind == BeastKind.Wolves ? 24 : 60;
            if (owner != null) capacity = Math.Min(capacity, Math.Max(4, owner.Population));
            // A growth ceiling limits new births; it never silently removes an
            // existing large herd and the milk its surviving members produce.
            int next = Math.Max(herd.Count, Math.Min(capacity, herd.Count + Math.Max(1, herd.Count / 12)));
            SetLivestockCount(herd, next);
        }
    }
}
