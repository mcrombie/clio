using System;
using System.Collections.Generic;
using System.Linq;

namespace Clio.Simulation
{
    public enum SaltSource { None, Coastal, Spring }

    /// <summary>Salt is an abstract community reserve, not a medical dosage.</summary>
    public static class SaltEconomy
    {
        public static double Need(Band band) { return band == null ? 0 : Math.Max(0, band.Population) / 10.0; }
        public static double ReserveTurns(Band band) { double need = Need(band); return need <= 0 ? 0 : Math.Max(0, band.Salt) / need; }
        public static SaltSource Source(Game game, int cell) { return game == null ? SaltSource.None : game.ReadSaltSource(cell); }
        public static double GatherYield(Game game, Band band) { return game != null && game.SaltEnabled && band != null && Source(game, band.CellId) != SaltSource.None ? Need(band) * 5 : 0; }
        public static bool CanGather(Game game, Band band)
        {
            return game != null && game.SaltEnabled && !game.IsOver && band != null && band.Population > 0 &&
                (!game.CanControlBand(band.Id) || game.ActionsFor(band.Id) > 0) && game.SaltPlaceKnown(band, band.CellId) && Source(game, band.CellId) != SaltSource.None;
        }
        public static IEnumerable<int> KnownSources(Game game)
        { return game == null || !game.SaltEnabled ? new int[0] : game.Explored.Where(id => Source(game, id) != SaltSource.None).OrderBy(id => id).ToArray(); }
        public static double GatheringMultiplier(Band band) { return band == null ? 1 : 1 - Math.Min(3, Math.Max(0, band.SaltShortageTurns)) * .1; }
        public static double GatheringMultiplier(Game game, Band band) { return game != null && game.SaltEnabled ? GatheringMultiplier(band) : 1; }
        public static double CohesionPenalty(int shortageTurns) { return Math.Min(3, Math.Max(0, shortageTurns)) * .03; }

        internal static void Forecast(Game game, Band band, EconomyForecast result)
        {
            if (!game.SaltEnabled) return;
            result.StartingSalt = Math.Max(0, band.Salt);
            result.EndingSalt = result.StartingSalt;
            result.EndingSaltShortageTurns = Math.Max(0, band.SaltShortageTurns);
            if (band.Population <= 0 || game.IsOver) return;
            result.SaltNeed = Need(band); result.SaltConsumed = Math.Min(result.StartingSalt, result.SaltNeed);
            result.EndingSalt = Math.Max(0, result.StartingSalt - result.SaltConsumed);
            result.EndingSaltShortageTurns = result.SaltConsumed + 1e-9 >= result.SaltNeed ? 0 : Math.Min(1000000, Math.Max(0, band.SaltShortageTurns) + 1);
        }

        // Search only remembered cells; no hidden terrain, sources or inhabitants
        // influence route choices. Return the first step, not a teleport target.
        internal static int NextSourceStep(Game game, Band band)
        {
            if (!game.SaltEnabled || band.Population <= 0) return -1;
            if (game.TerrainTravelEnabled) return TravelRules.KnownStep(game, band, game.SaltKnownCells(band), cell => Source(game, cell) != SaltSource.None);
            HashSet<int> known = new HashSet<int>(game.SaltKnownCells(band));
            Queue<int> queue = new Queue<int>(); Dictionary<int, int> first = new Dictionary<int, int>();
            queue.Enqueue(band.CellId); first[band.CellId] = band.CellId;
            while (queue.Count > 0)
            {
                int cell = queue.Dequeue();
                if (Source(game, cell) != SaltSource.None && (cell == band.CellId || !EncounterRules.HostileAt(game, cell, band.Id))) return first[cell];
                foreach (int next in game.World.Cells[cell].Neighbors.OrderBy(n => n))
                {
                    if (!known.Contains(next) || first.ContainsKey(next) || !game.World.Cells[next].IsLand || game.World.Cells[next].Terrain == Terrain.Ice || EncounterRules.HostileAt(game, next, band.Id)) continue;
                    first[next] = cell == band.CellId ? next : first[cell]; queue.Enqueue(next);
                }
            }
            return -1;
        }
    }

    public sealed partial class Game
    {
        private bool saltEnabled;
        private SaltSource[] saltSources;
        private Dictionary<int, HashSet<int>> saltExplored;
        public bool SaltEnabled { get { return saltEnabled; } }

        internal SaltSource ReadSaltSource(int cell)
        { return saltEnabled && saltSources != null && cell >= 0 && cell < saltSources.Length ? saltSources[cell] : SaltSource.None; }
        internal IEnumerable<int> SaltKnownCells(Band band)
        {
            if (IsPlayerTribe(band.Id)) return Explored;
            HashSet<int> known;
            return saltExplored != null && saltExplored.TryGetValue(band.Id, out known) ? (IEnumerable<int>)known : new int[0];
        }
        internal bool SaltPlaceKnown(Band band, int cell) { return SaltKnownCells(band).Contains(cell); }
        private static uint SaltHash(int seed, int cell)
        {
            uint value = unchecked((uint)seed ^ ((uint)cell * 0x9e3779b9u) ^ 0x51a7c39du);
            value ^= value >> 16; value *= 0x7feb352du; value ^= value >> 15; value *= 0x846ca68bu; return value ^ (value >> 16);
        }
        private void ObserveSaltPlaces(Band band)
        {
            if (!SaltEnabled || band == null || band.Population <= 0) return;
            HashSet<int> known;
            if (!saltExplored.TryGetValue(band.Id, out known)) { known = new HashSet<int>(); saltExplored[band.Id] = known; }
            known.Add(band.CellId);
            foreach (int neighbor in World.Cells[band.CellId].Neighbors)
            { known.Add(neighbor); foreach (int next in World.Cells[neighbor].Neighbors) known.Add(next); }
            if (CulturalPlaceNames) foreach (PlaceKnowledge place in KnownPlaces(band.Id)) known.Add(place.CellId);
        }
        private void InitializeSaltEconomy()
        {
            saltEnabled = true; saltSources = new SaltSource[World.Cells.Length]; saltExplored = new Dictionary<int, HashSet<int>>();
            foreach (Cell cell in World.Cells)
            {
                if (!cell.IsLand || cell.Terrain == Terrain.Ice) continue;
                if (cell.Neighbors.Any(n => !World.Cells[n].IsLand)) saltSources[cell.Id] = SaltSource.Coastal;
                else if (SaltHash(Seed, cell.Id) % 61 == 0) saltSources[cell.Id] = SaltSource.Spring;
            }
            foreach (Band band in Bands.Where(b => b.Population > 0).OrderBy(b => b.Id))
            {
                band.Salt = SaltEconomy.Need(band) * 4; band.SaltShortageTurns = 0; ObserveSaltPlaces(band);
                int source = World.Cells[band.CellId].Neighbors.Concat(new[] { band.CellId }).Where(n => World.Cells[n].IsLand && World.Cells[n].Terrain != Terrain.Ice &&
                    !EncounterRules.HostileAt(this, n, band.Id)).OrderBy(n => saltSources[n] == SaltSource.None ? 1 : 0).ThenBy(n => n == band.CellId ? 1 : 0).ThenBy(n => n).DefaultIfEmpty(band.CellId).First();
                if (saltSources[source] == SaltSource.None) saltSources[source] = SaltSource.Spring;
            }
        }
        public string EnableSaltEconomy()
        {
            if (SaltEnabled) return "Salt gathering already supports this story.";
            InitializeSaltEconomy(); return "Salt now sustains each living band, with four turns of reserve and nearby sources to discover.";
        }
        public string GatherSalt()
        {
            if (!SaltEnabled) return "Salt is not part of this story's current rules.";
            string message; if (!CanAct(out message)) return message;
            if (!SaltEconomy.CanGather(this, ActionBand)) return "Move to a known coastal salt source or inland spring to gather salt.";
            double gained = SaltEconomy.GatherYield(this, ActionBand); ActionPoints--; ActionBand.Salt += gained;
            Log("Your people gather " + gained.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture) + " salt at " + Place(ActionBand.CellId) + ".");
            return "Salt gathered: enough for five turns at the band's current size.";
        }
        private void ApplySaltBalance(Band band, EconomyForecast balance)
        {
            if (!SaltEnabled) return;
            int previous = band.SaltShortageTurns;
            band.Salt = balance.EndingSalt; band.SaltShortageTurns = balance.EndingSaltShortageTurns;
            if (band.SaltShortageTurns > 0) band.Cohesion = Math.Max(.1, band.Cohesion - SaltEconomy.CohesionPenalty(band.SaltShortageTurns));
            if (band.Id == Player.Id)
            {
                if (balance.SaltLosses > 0) Log("Prolonged salt shortage takes " + balance.SaltLosses + " lives.");
                else if (band.SaltShortageTurns > 0) Log("Salt runs short. Gathering weakens and growth pauses until a fully supplied turn.");
                else if (previous > 0) Log("A full turn of salt restores the community's strength.");
            }
        }
        private void ResolveSaltHousehold(Band band)
        {
            EconomyForecast balance = BandEconomy.Forecast(this, band);
            band.Food = balance.EndingFood; band.Population = balance.EndingPopulation;
            if (balance.HungerLosses > 0) { band.SafeTurns = 0; band.Cohesion = Math.Max(.1, band.Cohesion - .08); }
            else { band.SafeTurns++; band.Cohesion = Math.Min(1, band.Cohesion + .015); }
            ApplySaltBalance(band, balance);
            ApplyWoodBalance(band, balance);
            if (band.Id == Player.Id)
            {
                if (balance.HungerLosses > 0) Log("Hunger takes " + balance.HungerLosses + " lives. Seek new ground or food before the next turn.");
                if (balance.ExposureLosses > 0) Log("Exposure claims " + balance.ExposureLosses + " lives. A camp would offer shelter.");
                if (balance.Births > 0) Log("A secure food surplus supports " + balance.Births + " new members of the band.");
            }
        }
        private void MoveSaltBand(Band band, int destination)
        {
            if (Rules == SimulationRules.MobileUnits) RelocateBand(band, destination, true, EncounterKind.Move);
            else
            {
                band.Food = Math.Max(0, band.Food - band.Population * .15); band.CellId = destination;
                band.Settled = false; band.HomeCell = -1;
                foreach (Beast companion in Beasts.Where(b => b.Domestic && b.OwnerId == band.Id && b.Count > 0)) companion.CellId = destination;
                if (CulturalPlaceNames) { DiscoverPlaces(band.Id, destination); ShareNearbyPlaceKnowledge(band); }
            }
            ObserveSaltPlaces(band);
        }
        private void ActSaltIndependent(Band band)
        {
            ObserveSaltPlaces(band);
            if (TerrainTravelEnabled) ObserveTravelPlaces(band);
            for (int action = 0; action < 2 && band.Population > 0; action++)
            {
                double needs = Upkeep(band) + BandEconomy.DomesticEffects(this, band).AnimalCare;
                int[] safe = World.Cells[band.CellId].Neighbors.Where(n => (TerrainTravelEnabled ? TravelPlaceKnown(band, n) : SaltEnabled ? SaltPlaceKnown(band, n) : WoodEnabled) && World.Cells[n].IsLand && World.Cells[n].Terrain != Terrain.Ice &&
                    (!TerrainTravelEnabled || TravelRules.MoveCost(this, band, band.CellId, n) <= 2 - action) &&
                    !EncounterRules.HostileAt(this, n, band.Id)).OrderByDescending(n => ForageYield(n, band)).ThenBy(n => n).ToArray();
                if (EncounterRules.HostileAt(this, band.CellId, band.Id) && safe.Length > 0)
                { if (TerrainTravelEnabled) action += TravelRules.MoveCost(this, band, band.CellId, safe[0]) - 1; MoveSaltBand(band, safe[0]); continue; }
                if (SaltEnabled && SaltEconomy.ReserveTurns(band) <= 3.25 && band.Food >= needs * 1.5)
                {
                    if (SaltEconomy.CanGather(this, band)) { band.Salt += SaltEconomy.GatherYield(this, band); continue; }
                    int step = SaltEconomy.NextSourceStep(this, band);
                    if (step >= 0 && step != band.CellId && (!TerrainTravelEnabled || TravelRules.MoveCost(this, band, band.CellId, step) <= 2 - action))
                    { if (TerrainTravelEnabled) action += TravelRules.MoveCost(this, band, band.CellId, step) - 1; MoveSaltBand(band, step); continue; }
                }
                int gatheringSpent;
                if (WoodEnabled && WoodEconomy.ReserveTurns(band) < 3 && band.Food >= needs * 1.5 &&
                    (!SaltEnabled || SaltEconomy.ReserveTurns(band) >= 2) && WoodEconomy.CanGather(this, band))
                { band.Wood += WoodEconomy.GatherYield(this, band); continue; }
                if (GatheringsEnabled && TryGatheringGuestAction(band, 2 - action, out gatheringSpent)) { action += gatheringSpent - 1; continue; }
                if (band.Food >= needs * 5) break;
                double here = ForageYield(band.CellId, band);
                if (action == 0 && safe.Length > 0 && (!TerrainTravelEnabled || TravelRules.MoveCost(this, band, band.CellId, safe[0]) < 2 || band.Food >= needs * 2) && ForageYield(safe[0], band) - band.Population * .15 > here * 1.6 + 8)
                { if (TerrainTravelEnabled) action += TravelRules.MoveCost(this, band, band.CellId, safe[0]) - 1; MoveSaltBand(band, safe[0]); continue; }
                band.Food += here; Depletion[band.CellId] = Math.Min(1, Depletion[band.CellId] + .18);
            }
            if (Turn % 18 == 0 && band.LanguageId == Player.LanguageId && Vec3.Dot(World.Cells[band.CellId].Center, World.Cells[Player.CellId].Center) < .95)
            {
                int id = Languages.Count; LanguageProfile child = LanguageGenerator.Branch(Languages[band.LanguageId], id, Seed + band.Id * 73 + Turn);
                Languages.Add(child); band.LanguageId = id;
                if (Rules == SimulationRules.Classic || Explored.Contains(band.CellId)) Log("Away from the parent hearth, " + band.Name + " develops a distinct speech: " + child.Name + ".");
            }
        }
    }
}
