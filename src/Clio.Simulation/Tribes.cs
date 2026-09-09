using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;

namespace Clio.Simulation
{
    public enum TribeEventKind { Enabled, Split, Reunion, Drift, Secession, Succession, Extinction, Disposition, Wandering }

    public sealed class TribeMembership
    {
        public readonly int BandId, TribeId, ParentBandId, LastReunionTurn, TurnsAway, ReunionCellId;
        public readonly bool Drifting, IsLeader;
        public readonly int DriftAfter, SecedeAfter, SeparationBand;
        internal TribeMembership(int band, int tribe, int parent, int reunion, int away, bool leader, int cell)
            : this(band, tribe, parent, reunion, away, leader, cell, 8, 16, 0) { }
        internal TribeMembership(int band, int tribe, int parent, int reunion, int away, bool leader, int cell, int drift, int secede, int separation)
        { BandId = band; TribeId = tribe; ParentBandId = parent; LastReunionTurn = reunion; TurnsAway = away; Drifting = away >= drift; IsLeader = leader; ReunionCellId = cell;
            DriftAfter = drift; SecedeAfter = secede; SeparationBand = separation; }
    }

    public sealed class TribeEvent
    {
        public readonly int Id, Turn, BandId, OtherBandId, TribeId, PreviousTribeId, CellId, Population, LanguageId, PreviousLanguageId;
        public readonly TribeEventKind Kind;
        public readonly bool VisibleToPlayer;
        public readonly string BandName, OtherBandName, Title, Detail;
        public readonly double Food, Salt;
        internal TribeEvent(int id, int turn, TribeEventKind kind, Band band, Band other, int tribe, int previousTribe,
            int previousLanguage, bool visible, string title, string detail)
        {
            Id = id; Turn = turn; Kind = kind; BandId = band == null ? -1 : band.Id; OtherBandId = other == null ? -1 : other.Id;
            TribeId = tribe; PreviousTribeId = previousTribe; CellId = band == null ? -1 : band.CellId;
            Population = band == null ? 0 : band.Population; Food = band == null ? 0 : band.Food; Salt = band == null ? 0 : band.Salt;
            LanguageId = band == null ? -1 : band.LanguageId; PreviousLanguageId = previousLanguage;
            VisibleToPlayer = visible; BandName = band == null ? "" : band.Name; OtherBandName = other == null ? "" : other.Name;
            Title = title; Detail = detail;
        }
    }

    public sealed class TribeEconomyReceipt
    {
        public readonly int Turn, BandId, StartingPopulation;
        public readonly double StartingFood, StartingSalt;
        public readonly EconomyForecast Forecast;
        internal TribeEconomyReceipt(Band band, EconomyForecast forecast)
        { Turn = forecast.Turn; BandId = band.Id; StartingPopulation = band.Population; StartingFood = band.Food; StartingSalt = band.Salt; Forecast = forecast; }
    }

    internal sealed class TribeMemberState
    {
        internal int TribeId, ParentBandId = -1, LastReunionTurn, MissedReunions, Actions;
    }

    public sealed partial class Game
    {
        // All tribe state is additive. Disabled games retain their original
        // command dispatch, two-action counter and deterministic random streams.
        private bool tribeEnabled, tribeExtinctionRecorded;
        private int tribePlayerId, tribeLeaderId;
        private Band tribeActionBand;
        private Dictionary<int, TribeMemberState> tribeMembers;
        private List<TribeEvent> tribeEvents;
        private List<TribeEconomyReceipt> tribeEconomy;
        private Dictionary<int, PlaceKnowledge> tribeObservedPlaces;
        public bool TribesEnabled { get { return tribeEnabled; } }
        public int PlayerTribeId { get { return tribeEnabled ? tribePlayerId : Player.Id; } }
        public Band TribeLeaderBand { get { return !tribeEnabled ? (Player.Population > 0 ? Player : null) :
            Bands.FirstOrDefault(b => b.Id == tribeLeaderId && b.Population > 0 && IsPlayerTribe(b.Id)) ?? ControlledBands.OrderBy(b => b.Id).FirstOrDefault(); } }
        public IEnumerable<Band> ControlledBands { get { return Bands.Where(b => b.Population > 0 && IsPlayerTribe(b.Id)).OrderBy(b => b.Id).ToArray(); } }
        public int TribePopulation { get { return ControlledBands.Sum(b => b.Population); } }
        public int TribeReunionCell { get { Band leader = TribeLeaderBand; return leader == null ? -1 : leader.CellId; } }
        public ReadOnlyCollection<TribeEvent> TribeEvents { get { return (tribeEvents ?? new List<TribeEvent>()).AsReadOnly(); } }
        public ReadOnlyCollection<TribeEconomyReceipt> LastTribeEconomy { get { return (tribeEconomy ?? new List<TribeEconomyReceipt>()).AsReadOnly(); } }
        public bool CanControlBand(int id) { return Bands.Any(b => b.Id == id && b.Population > 0) && IsPlayerTribe(id); }
        internal bool IsPlayerTribe(int id) { return !tribeEnabled ? id == Player.Id : TribeOf(id) == tribePlayerId; }
        public int TribeOf(int id)
        { TribeMemberState state; return tribeEnabled && tribeMembers.TryGetValue(id, out state) ? state.TribeId : id; }
        public int ActionsFor(int id)
        {
            if (!CanControlBand(id)) return 0;
            if (!tribeEnabled) return Actions;
            TribeMemberState state; return tribeMembers.TryGetValue(id, out state) ? Math.Max(0, state.Actions) : 0;
        }
        public TribeMembership TribeStatus(int id)
        {
            Band band = Bands.Find(b => b.Id == id); if (band == null) return null;
            TribeMemberState state;
            if (!tribeEnabled || !tribeMembers.TryGetValue(id, out state))
                return new TribeMembership(id, id, -1, Turn, 0, true, band.CellId);
            bool own = state.TribeId == tribePlayerId;
            if (BandPersonalitiesEnabled && own)
            {
                Band leader = TribeLeaderBand; int separation = PersonalitySeparation(band, leader);
                BandDisposition disposition = DispositionFor(id);
                return new TribeMembership(id, state.TribeId, state.ParentBandId, state.LastReunionTurn, state.MissedReunions,
                    leader != null && leader.Id == id, TribeReunionCell, disposition.DriftTurns, PersonalitySecessionTurns(disposition, separation), separation);
            }
            return new TribeMembership(id, state.TribeId, state.ParentBandId, state.LastReunionTurn, state.MissedReunions,
                own ? TribeLeaderBand != null && TribeLeaderBand.Id == id : true, own ? TribeReunionCell : band.CellId);
        }
        internal Band ActionBand { get { return tribeEnabled && tribeActionBand != null ? tribeActionBand : Player; } }
        private int ActionPoints
        {
            get { return tribeEnabled ? ActionsFor(ActionBand.Id) : Actions; }
            set { if (tribeEnabled) { tribeMembers[ActionBand.Id].Actions = value; if (ActionBand.Id == Player.Id) Actions = value; } else Actions = value; }
        }
        public bool CanMoveBand(int actorId, int target)
        {
            Band actor = Bands.Find(b => b.Id == actorId);
            return actor != null && CanControlBand(actorId) && ActionsFor(actorId) > 0 && !IsOver && target >= 0 && target < World.Cells.Length &&
                World.Cells[target].IsLand && World.Cells[actor.CellId].Neighbors.Contains(target) &&
                (Rules == SimulationRules.Classic || Explored.Contains(target) && !EncounterRules.HostileAt(this, target, actorId)) &&
                (!TerrainTravelEnabled || Explored.Contains(target) && TravelRules.MoveCost(this, actor, actor.CellId, target) <= ActionsFor(actorId) &&
                    (World.Cells[target].Terrain != Terrain.Ice || actor.Food >= Upkeep(actor) * 3));
        }
        public string IssueBandCommand(int actorId, string command)
        {
            if (!TribesEnabled) return "Enable tribes before issuing orders to several bands.";
            if (!CanControlBand(actorId)) return "That living band does not belong to your tribe.";
            if (command != null && command.StartsWith("gather-", StringComparison.Ordinal)) return IssueGatheringBandCommand(actorId, command);
            string verb = command ?? ""; int target = -1, colon = verb.IndexOf(':');
            if (colon >= 0)
            {
                if (!Int32.TryParse(verb.Substring(colon + 1), NumberStyles.Integer, CultureInfo.InvariantCulture, out target)) return "Choose a valid order target.";
                verb = verb.Substring(0, colon);
                if (verb != "move" && verb != "attack-animal" && verb != "befriend-animal" && verb != "attack-band") return "That is not a band order.";
            }
            else if (verb != "forage" && verb != "salt" && verb != "wood" && verb != "camp" && verb != "split" && verb != "hunt" && verb != "tame" && verb != "wait") return "That is not a band order.";
            if (ActionsFor(actorId) <= 0) return "This band has spent its effort. Other bands may still act.";
            tribeActionBand = Bands.Find(b => b.Id == actorId); int before = ActionPoints;
            try
            {
                string result = verb == "move" ? Move(target) : verb == "forage" ? Forage() : verb == "salt" ? GatherSalt() :
                    verb == "wood" ? GatherWood() :
                    verb == "camp" ? Camp() : verb == "split" ? Split() : verb == "hunt" ? Hunt() : verb == "tame" ? Tame() :
                    verb == "attack-animal" ? AttackAnimal(target) : verb == "befriend-animal" ? BefriendAnimal(target) :
                    verb == "attack-band" ? AttackBand(target) : WaitForTribe();
                if (BandPersonalitiesEnabled && ActionPoints != before)
                {
                    tribePersonalityOrders[actorId] = Turn;
                    if (verb == "move") tribePersonalityTravel[actorId] = Turn;
                }
                if (ActionPoints != before || tribeActionBand.Population <= 0) ReconcileTribe(false);
                if (GatheringsEnabled && (ActionPoints != before || tribeActionBand.Population <= 0)) ReconcileGatherings(false);
                return result;
            }
            finally { tribeActionBand = null; }
        }
        private string WaitForTribe() { ActionPoints = 0; return "This band rests while the other bands finish their work."; }
        public string EnableTribes()
        {
            if (TribesEnabled) return "Your bands already belong to a shared tribe.";
            if (Rules != SimulationRules.MobileUnits || !SaltEnabled) return "Enable moving encounters and salt before forming a controllable tribe.";
            InitializeTribes(true); return "New daughter bands will remain within your tribe. Existing independent peoples keep their autonomy.";
        }
        private void InitializeTribes(bool migration)
        {
            tribeEnabled = true; tribePlayerId = Player.Id; tribeLeaderId = Player.Id;
            tribeMembers = new Dictionary<int, TribeMemberState>(); tribeEvents = new List<TribeEvent>(); tribeEconomy = new List<TribeEconomyReceipt>();
            tribeObservedPlaces = new Dictionary<int, PlaceKnowledge>();
            foreach (Band band in Bands.OrderBy(b => b.Id))
            {
                bool daughter = false; // Old saves have no reliable tribal parentage; existing polities remain independent.
                tribeMembers.Add(band.Id, new TribeMemberState { TribeId = daughter ? tribePlayerId : band.Id, ParentBandId = daughter ? Player.Id : -1,
                    LastReunionTurn = Turn, Actions = band.Id == Player.Id ? Actions : daughter && band.Population > 0 ? 2 : 0 });
            }
            if (migration) AddTribeEvent(TribeEventKind.Enabled, Player, null, tribePlayerId, Player.LanguageId, "A tribe holds its bands together",
                "Future daughter bands will share a people while keeping their own supplies and orders. Existing independent polities remain separate.");
            ReconcileTribe(false);
        }
        private void JoinDaughterToTribe(Band parent, Band daughter)
        {
            if (!TribesEnabled) return;
            tribeMembers.Add(daughter.Id, new TribeMemberState { TribeId = TribeOf(parent.Id), ParentBandId = parent.Id, LastReunionTurn = Turn, Actions = 0 });
            RevealTribeBand(daughter);
            AddTribeEvent(TribeEventKind.Split, daughter, parent, TribeOf(parent.Id), parent.LanguageId, "A new band within the tribe",
                daughter.Name + " carries its share of people, food and salt. It remains part of the tribe and can receive orders next turn." +
                (BandPersonalitiesEnabled ? " Its " + DispositionFor(daughter.Id).Label.ToLowerInvariant() + " disposition shapes its journeys and willingness to remain together." : ""));
        }
        private void AddTribeEvent(TribeEventKind kind, Band band, Band other, int previousTribe, int previousLanguage, string title, string detail)
        {
            bool visible = band != null && (IsPlayerTribe(band.Id) || previousTribe == tribePlayerId || Explored.Contains(band.CellId));
            tribeEvents.Add(new TribeEvent(tribeEvents.Count + 1, Turn, kind, band, other, band == null ? tribePlayerId : TribeOf(band.Id),
                previousTribe, previousLanguage, visible, title, detail));
            if (visible) Log(title + ". " + detail);
        }
        private void ReconcileTribe(bool close)
        {
            if (!TribesEnabled) return;
            Band[] living = ControlledBands.ToArray();
            Band leader = living.FirstOrDefault(b => b.Id == tribeLeaderId);
            if (leader == null && living.Length > 0)
            {
                Band previous = Bands.Find(b => b.Id == tribeLeaderId); leader = living[0]; tribeLeaderId = leader.Id;
                AddTribeEvent(TribeEventKind.Succession, leader, previous, tribePlayerId, leader.LanguageId, "The tribe finds a new leader",
                    leader.Name + " keeps the surviving bands together. Its present camp becomes their reunion place.");
            }
            if (leader == null)
            {
                if (!tribeExtinctionRecorded)
                {
                    tribeExtinctionRecorded = true;
                    AddTribeEvent(TribeEventKind.Extinction, Player, null, tribePlayerId, Player.LanguageId, "The tribe's final hearth goes cold",
                        "No living band remains within the tribe. Its remembered history endures.");
                }
                return;
            }
            foreach (Band band in living)
            {
                TribeMemberState state = tribeMembers[band.Id];
                if (band.CellId == leader.CellId)
                {
                    if (band.Id != leader.Id && state.MissedReunions > 0)
                        AddTribeEvent(TribeEventKind.Reunion, band, leader, tribePlayerId, band.LanguageId, "Bands return to the shared hearth",
                            band.Name + " meets " + leader.Name + ". Their contact is renewed and the drift toward separation ends.");
                    state.LastReunionTurn = Turn; state.MissedReunions = 0;
                    if (BandPersonalitiesEnabled) tribePersonalityDrifts.Remove(band.Id);
                }
                else if (close)
                {
                    state.MissedReunions++;
                    BandDisposition disposition = BandPersonalitiesEnabled ? DispositionFor(band.Id) : null;
                    int drift = disposition == null ? 8 : disposition.DriftTurns;
                    int secede = disposition == null ? 16 : PersonalitySecessionTurns(disposition, PersonalitySeparation(band, leader));
                    if (BandPersonalitiesEnabled ? state.MissedReunions >= drift && tribePersonalityDrifts.Add(band.Id) : state.MissedReunions == 8)
                        AddTribeEvent(TribeEventKind.Drift, band, leader, tribePlayerId, band.LanguageId, "A distant band begins to drift",
                            BandPersonalitiesEnabled ? band.Name + " has been apart for " + state.MissedReunions + " turns. Its " + disposition.Label.ToLowerInvariant() +
                            " disposition currently puts separation at " + secede + " turns apart; meeting the leader renews contact." :
                            band.Name + " has missed eight reunions. Return to the leader before sixteen missed turns to keep the tribe together.");
                    if (state.MissedReunions >= secede)
                    {
                        int oldLanguage = band.LanguageId; state.TribeId = band.Id; state.Actions = 0;
                        LanguageProfile child = LanguageGenerator.Branch(Languages[oldLanguage], Languages.Count, unchecked(Seed + band.Id * 73 + Turn));
                        Languages.Add(child); band.LanguageId = child.Id;
                        band.Name = LanguageGenerator.PlaceName(child, "people", band.Id + Turn + 17);
                        band.Culture = (double[])band.Culture.Clone(); band.Culture[3] = Math.Min(1, band.Culture[3] + .08);
                        AddTribeEvent(TribeEventKind.Secession, band, leader, tribePlayerId, oldLanguage, "A distinct people takes shape",
                            BandPersonalitiesEnabled ? band.Name + " becomes a separate polity after " + state.MissedReunions + " turns apart. Its " + disposition.Label.ToLowerInvariant() +
                            " disposition and distance from the shared hearth have shaped this choice. Speech and customs begin a new branch; separation does not begin a war." :
                            band.Name + " becomes a separate polity after sixteen missed reunions. Its speech and customs begin a new branch; peace remains possible.");
                    }
                }
                if (IsPlayerTribe(band.Id)) RevealTribeBand(band);
            }
        }
        private void RevealTribeBand(Band band)
        {
            if (!TribesEnabled || !IsPlayerTribe(band.Id) || band.Population <= 0) return;
            ObserveSaltPlaces(band);
            if (CulturalPlaceNames) { DiscoverPlaces(band.Id, band.CellId); foreach (PlaceKnowledge place in KnownPlaces(band.Id)) { Explored.Add(place.CellId); RememberTribePlace(place); } }
            else { Explored.Add(band.CellId); foreach (int n in World.Cells[band.CellId].Neighbors) { Explored.Add(n); foreach (int next in World.Cells[n].Neighbors) Explored.Add(next); } }
        }
        public PlaceKnowledge ObserverKnownPlace(int cell)
        {
            PlaceKnowledge place = KnownPlace(Player.Id, cell); if (place != null || !TribesEnabled) return place;
            return tribeObservedPlaces.TryGetValue(cell, out place) ? place : null;
        }
        private void RememberTribePlace(PlaceKnowledge place)
        { if (TribesEnabled && place != null && !tribeObservedPlaces.ContainsKey(place.CellId)) tribeObservedPlaces.Add(place.CellId, place); }
        private void BeginTribeEconomy() { if (TribesEnabled) tribeEconomy.Clear(); }
        private void RecordTribeEconomy(Band band, EconomyForecast forecast)
        { if (TribesEnabled && IsPlayerTribe(band.Id)) tribeEconomy.Add(new TribeEconomyReceipt(band, forecast)); }
        private void BeginTribeTurn()
        {
            if (!TribesEnabled) return;
            foreach (Band band in Bands) { TribeMemberState state; if (tribeMembers.TryGetValue(band.Id, out state)) state.Actions = CanControlBand(band.Id) ? 2 : 0; }
            Actions = ActionsFor(Player.Id);
        }
    }
}
