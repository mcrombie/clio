using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;

namespace Clio.Simulation
{
    public enum BandTemperament { Loyal, Restless, Adventurous, Separatist }

    public sealed class BandDisposition
    {
        public readonly int BandId, DriftTurns, SecessionTurns;
        public readonly BandTemperament Temperament;
        public readonly string Label;
        public readonly double Independence, WanderChance;
        internal BandDisposition(int band, BandTemperament temperament, double independence, bool enabled)
        {
            BandId = band; Temperament = temperament; Independence = independence; Label = enabled ? temperament.ToString() : "Undifferentiated";
            DriftTurns = enabled ? new[] { 8, 4, 2, 1 }[(int)temperament] : 8;
            SecessionTurns = enabled ? new[] { 18, 9, 6, 4 }[(int)temperament] : 16;
            WanderChance = enabled ? new[] { .10, .40, .75, .95 }[(int)temperament] : 0;
        }
    }

    public sealed class WanderReceipt
    {
        public readonly int Turn, BandId, FromCell, ToCell, ActionsSpent, EncounterRecordId;
        public readonly double FoodSpent;
        internal WanderReceipt(int turn, int band, int from, int to, int actions, int encounter, double food)
        { Turn = turn; BandId = band; FromCell = from; ToCell = to; ActionsSpent = actions; EncounterRecordId = encounter; FoodSpent = food; }
    }

    public sealed partial class Game
    {
        private bool tribePersonalities;
        private Dictionary<int, int> tribePersonalityOrders, tribePersonalityTravel;
        private HashSet<int> tribePersonalityDrifts;
        private List<WanderReceipt> tribeWanderReceipts;
        public bool BandPersonalitiesEnabled { get { return tribePersonalities; } }
        public ReadOnlyCollection<WanderReceipt> LastWanderReceipts { get { return (tribeWanderReceipts ?? new List<WanderReceipt>()).AsReadOnly(); } }
        public bool HasBandOrderThisTurn(int id)
        { int turn; return BandPersonalitiesEnabled && tribePersonalityOrders.TryGetValue(id, out turn) && turn == Turn; }
        public BandDisposition DispositionFor(int id)
        {
            if (!Bands.Any(b => b.Id == id)) return null;
            uint roll = PersonalityHash(Seed, id, 0) % 1000;
            BandTemperament temperament = roll < 250 ? BandTemperament.Loyal : roll < 500 ? BandTemperament.Restless : roll < 800 ? BandTemperament.Adventurous : BandTemperament.Separatist;
            return new BandDisposition(id, temperament, roll / 999.0, BandPersonalitiesEnabled);
        }
        public string EnableBandPersonalities()
        {
            if (BandPersonalitiesEnabled) return "Band dispositions already shape journeys and separation.";
            if (!TribesEnabled) return "Enable tribal bands before their independent dispositions.";
            if (IsOver) return "This tribe's history is complete.";
            InitializeBandPersonalities(true);
            return "Daughter bands now have distinct dispositions. Give a band an order or hold it this turn to prevent voluntary wandering.";
        }
        private void InitializeBandPersonalities(bool migration)
        {
            tribePersonalities = true; tribePersonalityOrders = new Dictionary<int, int>(); tribePersonalityTravel = new Dictionary<int, int>();
            tribePersonalityDrifts = new HashSet<int>(); tribeWanderReceipts = new List<WanderReceipt>();
            // Existing partially spent turns count as already directed when upgrading.
            foreach (Band band in ControlledBands) if (ActionsFor(band.Id) < 2) tribePersonalityOrders[band.Id] = Turn;
            if (migration) AddTribeEvent(TribeEventKind.Disposition, TribeLeaderBand, null, PlayerTribeId, TribeLeaderBand.LanguageId,
                "Distinct temperaments within the tribe", "Loyal bands seek reunion; adventurous and separatist bands seek space. An idle daughter may use its remaining effort for one safe journey. Any successful order holds its course for the rest of this turn.");
        }
        private static uint PersonalityHash(int seed, int id, int turn)
        {
            unchecked { uint value = (uint)seed ^ (uint)id * 0x9e3779b9u ^ (uint)turn * 0x85ebca6bu ^ 0x4b19d28fu;
                value ^= value >> 16; value *= 0x7feb352du; value ^= value >> 15; value *= 0x846ca68bu; return value ^ (value >> 16); }
        }
        private int PersonalitySeparation(Band band, Band leader)
        { return leader == null || band.CellId == leader.CellId ? 0 : World.Cells[band.CellId].Neighbors.Contains(leader.CellId) ? 1 : 2; }
        private static int PersonalitySecessionTurns(BandDisposition disposition, int separation)
        { return disposition.SecessionTurns - (separation >= 2 && disposition.Temperament != BandTemperament.Loyal ? 1 : 0); }

        // Called by autoplay and by idle-band closure. It considers remembered
        // terrain only and never spends effort, changes membership or draws RNG.
        internal int PersonalityTravelTarget(Band band)
        {
            if (!BandPersonalitiesEnabled || band == null || !CanControlBand(band.Id) || ActionsFor(band.Id) <= 0) return -1;
            Band leader = TribeLeaderBand; if (leader == null || leader.Id == band.Id) return -1;
            int traveled; if (tribePersonalityTravel.TryGetValue(band.Id, out traveled) && traveled == Turn) return -1;
            double needs = Upkeep(band) + BandEconomy.DomesticEffects(this, band).AnimalCare;
            double travelFood = band.Population * (Known("routes") ? .08 : .15);
            if (band.Food - travelFood < needs * 2 || SaltEconomy.ReserveTurns(band) < 2) return -1;
            if (EncounterRules.HostileAt(this, band.CellId, band.Id)) return -1;
            if (Bands.Any(b => b.Population > 0 && Explored.Contains(b.CellId) && EncounterRules.BandsHostile(this, band.Id, b.Id) &&
                World.Cells[band.CellId].Neighbors.Contains(b.CellId))) return -1;
            BandDisposition disposition = DispositionFor(band.Id); TribeMembership status = TribeStatus(band.Id);
            bool reunion = disposition.Temperament == BandTemperament.Loyal && status.TurnsAway >= 2 ||
                disposition.Temperament == BandTemperament.Restless && status.TurnsAway >= status.DriftAfter;
            if (reunion)
            {
                int step = TravelRules.KnownStep(this, band, Explored, cell => cell == leader.CellId);
                return step != band.CellId && CanMoveBand(band.Id, step) ? step : -1;
            }
            if (PersonalityHash(Seed, band.Id, Turn) / 4294967296.0 >= disposition.WanderChance) return -1;
            int[] safe = World.Cells[band.CellId].Neighbors.Where(cell => Explored.Contains(cell) && CanMoveBand(band.Id, cell) &&
                World.Cells[cell].Terrain != Terrain.Ice && ForageYield(cell, band) >= needs * .60 &&
                (disposition.Temperament == BandTemperament.Loyal || cell != leader.CellId)).ToArray();
            if (safe.Length == 0) return -1;
            // Actual unknown-neighbor counts use geometry only; their contents
            // are never read. Independent households favor room away from home.
            return safe.OrderByDescending(cell =>
                (disposition.Temperament >= BandTemperament.Adventurous ? (1 - Vec3.Dot(World.Cells[cell].Center, World.Cells[leader.CellId].Center)) * 1000 : 0) +
                World.Cells[cell].Neighbors.Count(n => !Explored.Contains(n)) * .35 + ForageYield(cell, band) / Math.Max(1, needs) +
                (SaltEconomy.Source(this, cell) != SaltSource.None ? .7 : 0) - (TerrainTravelEnabled ? TravelRules.MoveCost(this, band, band.CellId, cell) * .15 : 0))
                .ThenBy(cell => PersonalityHash(Seed, band.Id, cell)).ThenBy(cell => cell).First();
        }

        private void ResolveVoluntaryBandMoves()
        {
            tribeWanderReceipts.Clear();
            foreach (Band band in ControlledBands.OrderBy(b => b.Id).ToArray())
            {
                if (HasBandOrderThisTurn(band.Id)) continue;
                int destination = PersonalityTravelTarget(band); if (destination < 0) continue;
                int from = band.CellId, actions = ActionsFor(band.Id); double food = band.Food;
                IssueBandCommand(band.Id, "move:" + destination.ToString(CultureInfo.InvariantCulture));
                if (band.CellId == from) continue;
                EncounterRecord record = Encounters.Records.Last(e => e.Kind == EncounterKind.Move && e.ActorKind == UnitKind.Band && e.ActorId == band.Id && e.Turn == Turn);
                tribeWanderReceipts.Add(new WanderReceipt(Turn, band.Id, from, band.CellId, actions - ActionsFor(band.Id), record.Id, food - band.Food));
                AddTribeEvent(TribeEventKind.Wandering, band, TribeLeaderBand, PlayerTribeId, band.LanguageId, "A daughter band chooses its own path",
                    band.Name + " uses unassigned effort to travel. Its " + DispositionFor(band.Id).Label.ToLowerInvariant() +
                    " disposition shapes the journey; supplies and companions travel with this household.");
            }
        }
    }
}
