using System;
using System.Linq;

namespace Clio.Simulation
{
    public sealed class DomesticEconomy
    {
        public int Dogs, Cattle, Goats, OtherCompanions;
        public double HuntingBonus, GatheringMultiplier = 1;
        public double AnimalCare, DogCare, CattleCare, OtherCare, CattleFood;
        public double MilkFood, CattleMilk, GoatMilk, GoatCare;
    }

    public sealed class EconomyForecast
    {
        public int BandId, Turn, HungerLosses, ExposureLosses, Births, EndingPopulation;
        public double StartingFood, CampFood, CattleFood, AnimalCare, ActualAnimalCare, Upkeep;
        public double MilkFood;
        public double FoodBeforeSpoilage, Spoilage, EndingFood, NetFood;
        public double StartingSalt, SaltNeed, SaltConsumed, EndingSalt;
        public int SaltLosses, EndingSaltShortageTurns;
        public double StartingWood, WoodNeed, WoodConsumed, EndingWood, BaseUpkeep, FireFoodSaved;
        public bool FireProtection;
    }

    /// <summary>Pure, shared accounting used by both historical simulation and its interface.</summary>
    public static class BandEconomy
    {
        public static double CattleFood(Beast herd)
        { return herd != null && herd.Domestic && herd.Count > 0 && herd.Kind == BeastKind.Aurochs ? Math.Min(herd.Count * 0.7, 25) * herd.Yield : 0; }

        public static double AnimalCare(Game game, Beast herd)
        {
            if (game == null) throw new ArgumentNullException("game");
            if (herd == null || !herd.Domestic || herd.Count <= 0) return 0;
            if (game.LivestockEnabled)
                return herd.Count * (herd.Kind == BeastKind.Aurochs ? .06 : herd.Kind == BeastKind.Goats ? .04 : herd.Kind == BeastKind.Wolves ? .3 :
                    herd.Kind == BeastKind.Deer ? .05 : herd.Kind == BeastKind.Mammoths ? .65 : herd.Kind == BeastKind.Dragon ? 8 : .3);
            if (game.Rules == SimulationRules.MobileUnits)
                return herd.Count * (herd.Kind == BeastKind.Deer ? 0.05 : herd.Kind == BeastKind.Mammoths ? 0.65 : herd.Kind == BeastKind.Dragon ? 8 : herd.Kind == BeastKind.Aurochs ? 0.06 : 0.3);
            return herd.Kind == BeastKind.Aurochs ? (game.Pace == HistoryPace.LegacySeasons ? 0 : herd.Count * 0.06) : herd.Count * 0.3;
        }

        public static DomesticEconomy DomesticEffects(Game game, Band band)
        {
            if (game == null) throw new ArgumentNullException("game");
            if (band == null) throw new ArgumentNullException("band");
            if (game.LivestockEnabled) return LivestockEconomy.Effects(game, band);
            DomesticEconomy result = new DomesticEconomy();
            foreach (Beast herd in game.Beasts.Where(b => b.Domestic && b.OwnerId == band.Id && b.Count > 0))
            {
                if (herd.Kind == BeastKind.Aurochs)
                {
                    result.Cattle += herd.Count;
                    result.CattleFood += CattleFood(herd);
                    result.CattleCare += AnimalCare(game, herd);
                }
                else
                {
                    if (herd.Kind == BeastKind.Wolves) result.Dogs += herd.Count;
                    if (game.Rules == SimulationRules.MobileUnits && herd.Kind != BeastKind.Wolves)
                    { result.OtherCompanions += herd.Count; result.OtherCare += AnimalCare(game, herd); }
                    else result.DogCare += AnimalCare(game, herd);
                }
            }
            result.AnimalCare = result.DogCare + result.CattleCare + result.OtherCare;
            if (game.Pace == HistoryPace.LegacySeasons && game.Rules == SimulationRules.Classic)
            {
                // This deliberately retains the old existence check, including
                // an empty owned group, because legacy command replays depend on it.
                result.HuntingBonus = game.Beasts.Any(b => b.Domestic && b.OwnerId == band.Id && b.Kind == BeastKind.Wolves) ? 0.05 : 0;
            }
            else
            {
                result.HuntingBonus = Math.Min(0.12, result.Dogs * 0.015);
                result.GatheringMultiplier = 1 + Math.Min(0.10, result.Dogs * 0.01);
                if (game.Rules == SimulationRules.MobileUnits)
                {
                    int deer = game.Beasts.Where(b => b.Domestic && b.OwnerId == band.Id && b.Kind == BeastKind.Deer && b.Count > 0).Sum(b => b.Count);
                    int mammoths = game.Beasts.Where(b => b.Domestic && b.OwnerId == band.Id && b.Kind == BeastKind.Mammoths && b.Count > 0).Sum(b => b.Count);
                    result.GatheringMultiplier += Math.Min(0.05, deer * 0.004) + Math.Min(0.06, mammoths * 0.015);
                }
            }
            return result;
        }

        public static double UnassistedForageYield(Game game, int cellId, Band band)
        {
            Cell cell = game.World.Cells[cellId];
            double gathering = game.IsPlayerTribe(band.Id) && game.Known("gathering") ? 1.15 : 1;
            double food = Math.Max(0, Math.Round((12 + cell.Forage * 100) * game.SeasonFactor * Game.Adaptation(band.Ancestry, cell) *
                (1 - game.Depletion[cellId] * 0.8) * gathering * Math.Min(2.5, band.Population / 50.0)));
            return game.SaltEnabled ? Math.Round(food * SaltEconomy.GatheringMultiplier(band)) : food;
        }

        public static double ForageYield(Game game, int cellId, Band band)
        {
            double unassisted = UnassistedForageYield(game, cellId, band);
            return game.Pace == HistoryPace.LegacySeasons && game.Rules == SimulationRules.Classic ? unassisted :
                Math.Round(unassisted * DomesticEffects(game, band).GatheringMultiplier);
        }

        public static double HuntChance(Game game, Beast prey)
        {
            if (game == null) throw new ArgumentNullException("game");
            if (prey == null) return 0;
            double chance = prey.Kind == BeastKind.Dragon ? 0.12 : prey.Kind == BeastKind.Wolves ? 0.6 : 0.8;
            if (game.Known("tracking")) chance += 0.1;
            chance += DomesticEffects(game, game.Player).HuntingBonus;
            return game.Pace == HistoryPace.LegacySeasons && !game.LivestockEnabled ? chance : Math.Min(0.97, chance);
        }

        public static double CampFood(Game game, Band band)
        { return band.Settled ? game.ForageYield(band.CellId, band) * (game.IsPlayerTribe(band.Id) && game.Known("gardens") ? 0.75 : 0.3) : 0; }

        /// <summary>
        /// Resolve the current band's economy without changing it. This is an
        /// exact player EndTurn forecast. Autonomous travel/actions occur before
        /// accounting and are deliberately outside this current-place forecast.
        /// </summary>
        public static EconomyForecast Forecast(Game game, Band band)
        {
            if (game == null) throw new ArgumentNullException("game");
            if (band == null) throw new ArgumentNullException("band");
            if (game.GuidedOpening && game.Turn == 1)
                return new EconomyForecast { BandId = band.Id, Turn = game.Turn, StartingFood = band.Food,
                    EndingFood = band.Food, FoodBeforeSpoilage = band.Food, EndingPopulation = band.Population,
                    StartingSalt = band.Salt, EndingSalt = band.Salt, EndingSaltShortageTurns = band.SaltShortageTurns,
                    StartingWood = band.Wood, EndingWood = band.Wood };
            DomesticEconomy domestic = DomesticEffects(game, band);
            EconomyForecast result = new EconomyForecast { BandId = band.Id, Turn = game.Turn,
                StartingFood = band.Food, CampFood = CampFood(game, band), CattleFood = domestic.CattleFood, MilkFood = domestic.MilkFood,
                AnimalCare = domestic.AnimalCare, Upkeep = game.Upkeep(band), EndingPopulation = band.Population };
            SaltEconomy.Forecast(game, band, result);
            WoodEconomy.Forecast(game, band, result);
            if (band.Population <= 0 || game.IsOver)
            { result.EndingFood = band.Food; result.FoodBeforeSpoilage = band.Food; return result; }

            double food = band.Food;
            double passive = result.CampFood;
            if (game.Pace == HistoryPace.LegacySeasons && game.Rules == SimulationRules.Classic && !game.LivestockEnabled)
            {
                // Preserve the legacy order of floating-point additions exactly.
                foreach (Beast herd in game.Beasts.Where(b => b.Domestic && b.OwnerId == band.Id && b.Count > 0))
                    if (herd.Kind == BeastKind.Aurochs) passive += Math.Min(herd.Count * 0.7, 25) * herd.Yield;
                    else food = Math.Max(0, food - herd.Count * 0.3);
            }
            else
            {
                food = Math.Max(0, food - domestic.AnimalCare);
                passive += domestic.CattleFood;
            }
            result.ActualAnimalCare = band.Food - food;
            food += passive;
            int safeTurns = band.SafeTurns;
            if (food < result.Upkeep)
            {
                result.HungerLosses = Math.Min(band.Population, Math.Max(1, (int)Math.Ceiling((result.Upkeep - food) * 0.15)));
                result.EndingPopulation -= result.HungerLosses; safeTurns = 0;
            }
            else safeTurns++;
            food = Math.Max(0, food - result.Upkeep);
            if (HistoryTime.HasExposureRisk(game, band) && !result.FireProtection)
            {
                result.ExposureLosses = Math.Min(result.EndingPopulation, 2);
                result.EndingPopulation -= result.ExposureLosses;
            }
            if (game.SaltEnabled && result.EndingSaltShortageTurns > 3 && result.EndingPopulation > 0)
            {
                result.SaltLosses = Math.Min(result.EndingPopulation, Math.Max(1, (int)Math.Ceiling(result.EndingPopulation * .02)));
                result.EndingPopulation -= result.SaltLosses;
            }
            double nextUpkeep = Math.Ceiling(result.EndingPopulation * (game.World.Cells[band.CellId].Temperature < 0.24 ? 1.35 : 1));
            if ((!game.SaltEnabled || result.EndingSaltShortageTurns == 0) && safeTurns >= 2 && food >= nextUpkeep * 2 && game.Turn % 2 == 0 && result.EndingPopulation > 0)
            {
                result.Births = Math.Max(1, (int)Math.Floor(result.EndingPopulation * 0.04));
                result.EndingPopulation += result.Births;
            }
            result.FoodBeforeSpoilage = food;
            result.EndingFood = food * (game.IsPlayerTribe(band.Id) && game.Known("stores") ? 0.98 : 0.95);
            result.Spoilage = food - result.EndingFood;
            result.NetFood = result.EndingFood - band.Food;
            return result;
        }
    }
}
