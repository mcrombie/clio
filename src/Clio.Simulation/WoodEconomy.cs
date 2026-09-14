using System;
using System.Globalization;
using System.Linq;

namespace Clio.Simulation
{
    /// <summary>Collected wood supports cooking fires and shelters; it is not a direct survival meter.</summary>
    public static class WoodEconomy
    {
        public const double CampCost = 10;

        public static double FuelNeed(Band band)
        { return band == null || band.Population <= 0 ? 0 : Math.Max(1, band.Population * .04); }

        public static double ReserveTurns(Band band)
        { double need = FuelNeed(band); return need <= 0 ? 0 : Math.Max(0, band.Wood) / need; }

        public static bool HasFuel(Game game, Band band)
        { return game != null && game.WoodEnabled && band != null && band.Population > 0 && band.Wood + 1e-9 >= FuelNeed(band); }

        public static double BaseUpkeep(Game game, Band band)
        {
            if (game == null) throw new ArgumentNullException("game");
            if (band == null) throw new ArgumentNullException("band");
            return Math.Ceiling(band.Population * (game.World.Cells[band.CellId].Temperature < .24 ? 1.35 : 1));
        }

        public static double GatherYield(Game game, Band band)
        { return band == null ? 0 : GatherYield(game, band, band.CellId); }

        public static double GatherYield(Game game, Band band, int cellId)
        {
            if (game == null || !game.WoodEnabled || band == null || band.Population <= 0 || cellId < 0 || cellId >= game.World.Cells.Length) return 0;
            Cell cell = game.World.Cells[cellId];
            if (!cell.IsLand || cell.Terrain == Terrain.Ice) return 0;
            double amount;
            switch (cell.Terrain)
            {
                case Terrain.Forest: amount = 18; break;
                case Terrain.Wetland: amount = 12; break;
                case Terrain.Hills:
                case Terrain.Grassland: amount = 6; break;
                case Terrain.Tundra: amount = 4; break;
                case Terrain.Mountains: amount = 2; break;
                case Terrain.Desert: amount = 1; break;
                default: return 0;
            }
            return Math.Max(1, Math.Floor(amount * Math.Max(.3, Math.Min(2.5, band.Population / 50.0))));
        }

        public static bool CanGather(Game game, Band band)
        {
            return game != null && game.WoodEnabled && !game.GuidedOpening && !game.IsOver && band != null && band.Population > 0 &&
                (!game.CanControlBand(band.Id) || game.ActionsFor(band.Id) > 0) && GatherYield(game, band) > 0;
        }

        internal static void Forecast(Game game, Band band, EconomyForecast result)
        {
            result.BaseUpkeep = BaseUpkeep(game, band);
            if (!game.WoodEnabled) return;
            result.StartingWood = result.EndingWood = Math.Max(0, band.Wood);
            if (band.Population <= 0 || game.IsOver) return;
            result.WoodNeed = FuelNeed(band);
            if (!HasFuel(game, band)) return;
            result.WoodConsumed = result.WoodNeed;
            result.EndingWood = Math.Max(0, result.StartingWood - result.WoodConsumed);
            result.FireFoodSaved = result.BaseUpkeep - result.Upkeep;
            result.FireProtection = true;
        }
    }

    public sealed partial class Game
    {
        private bool woodEnabled;
        public bool WoodEnabled { get { return woodEnabled; } }

        public string EnableWood()
        {
            if (WoodEnabled) return "Wood collection already supports this story.";
            woodEnabled = true;
            foreach (Band band in Bands.Where(b => b.Population > 0)) band.Wood = WoodEconomy.FuelNeed(band) * 6;
            return "Each band carries six turns of firewood. Collect wood to cook food, keep warm and build camps.";
        }

        public string GatherWood()
        {
            if (GuidedOpening) return "Gather brings back food, wood and any salt available in this region together, for one influence.";
            if (!WoodEnabled) return "Wood is not part of this story's current rules.";
            string message; if (!CanAct(out message)) return message;
            if (!WoodEconomy.CanGather(this, ActionBand)) return "No usable wood can be collected here. Move to land with trees or scrub.";
            double gained = WoodEconomy.GatherYield(this, ActionBand);
            ActionPoints--; ActionBand.Wood += gained;
            Log(ActionBand.Name + " collects " + gained.ToString("0.#", CultureInfo.InvariantCulture) + " wood at " + Place(ActionBand.CellId) + ".");
            return "+" + gained.ToString("0.#", CultureInfo.InvariantCulture) + " wood for cooking fires and camp building.";
        }

        private void ApplyWoodBalance(Band band, EconomyForecast balance)
        { if (WoodEnabled) band.Wood = balance.EndingWood; }
    }
}
