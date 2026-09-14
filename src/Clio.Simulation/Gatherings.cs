using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;

namespace Clio.Simulation
{
    public enum GatheringStatus { Traveling, Meeting, ReturnPlanned, Completed, Fulfilled, Missed, Interrupted, Refused }
    public enum GatheringEventKind { Enabled, InvitationAccepted, InvitationRefused, FirstMeeting, AidGiven, ReturnAgreed, ReturnFulfilled, Completed, Missed, Interrupted }

    public sealed class GatheringInvitation
    {
        public readonly bool SiteAllowed, CanInvite, WillAccept;
        public readonly string Reason;
        public readonly int ActionCost, TravelActions, HostTravelActions, ArrivalDeadline;
        public readonly double FoodCost;
        internal GatheringInvitation(bool site, bool can, bool accepts, string reason, double food, int guestTravel, int hostTravel, int deadline)
        { SiteAllowed = site; CanInvite = can; WillAccept = accepts; Reason = reason; FoodCost = food; ActionCost = 1;
            TravelActions = guestTravel; HostTravelActions = hostTravel; ArrivalDeadline = deadline; }
    }
    public sealed class GatheringAidOffer
    {
        public readonly bool CanGive;
        public readonly string Reason;
        public readonly int ActionCost;
        public readonly double Food, Salt, MaxFood, MaxSalt;
        internal GatheringAidOffer(bool can, string reason, double food, double salt, double maxFood, double maxSalt)
        { CanGive = can; Reason = reason; ActionCost = 1; Food = food; Salt = salt; MaxFood = maxFood; MaxSalt = maxSalt; }
    }
    public sealed class GatheringReturnOffer
    {
        public readonly bool CanAgree;
        public readonly string Reason;
        public readonly int ActionCost, ReturnDueTurn, WindowEndTurn;
        internal GatheringReturnOffer(bool can, string reason, int due, int end)
        { CanAgree = can; Reason = reason; ActionCost = 1; ReturnDueTurn = due; WindowEndTurn = end; }
    }
    internal sealed class GatheringState
    {
        internal int Id, HostBandId, GuestBandId, GuestTribeId, CellId, InvitedTurn, ArrivalDeadline, MeetingTurn = -1,
            ReturnDueTurn = -1, WindowEndTurn = -1, ClosedTurn = -1, GuestOriginCell;
        internal GatheringStatus Status;
        internal bool HostAttended, GuestAttended, GuestDeparted, AidGiven;
        internal string HostName, GuestName, Reason;
        internal bool Active { get { return Status == GatheringStatus.Traveling || Status == GatheringStatus.Meeting || Status == GatheringStatus.ReturnPlanned; } }
    }
    public sealed class Gathering
    {
        public readonly int Id, HostBandId, GuestBandId, GuestTribeId, CellId, InvitedTurn, ArrivalDeadline, MeetingTurn,
            ReturnDueTurn, WindowEndTurn, ClosedTurn, GuestTravelActionsRemaining;
        public readonly GatheringStatus Status;
        public readonly bool Active, HostAtSite, GuestAtSite, HostAttended, GuestAttended, GuestDeparted, AidGiven;
        public readonly string HostName, GuestName, Reason;
        internal Gathering(GatheringState state, bool hostHere, bool guestHere, int remaining)
        {
            Id = state.Id; HostBandId = state.HostBandId; GuestBandId = state.GuestBandId; GuestTribeId = state.GuestTribeId;
            CellId = state.CellId; InvitedTurn = state.InvitedTurn; ArrivalDeadline = state.ArrivalDeadline; MeetingTurn = state.MeetingTurn;
            ReturnDueTurn = state.ReturnDueTurn; WindowEndTurn = state.WindowEndTurn; ClosedTurn = state.ClosedTurn;
            Status = state.Status; Active = state.Active; HostAtSite = hostHere; GuestAtSite = guestHere;
            HostAttended = state.HostAttended; GuestAttended = state.GuestAttended; GuestDeparted = state.GuestDeparted;
            AidGiven = state.AidGiven; HostName = state.HostName; GuestName = state.GuestName; Reason = state.Reason;
            GuestTravelActionsRemaining = remaining;
        }
    }
    public sealed class GatheringEvent
    {
        public readonly int Id, Turn, GatheringId, HostBandId, GuestBandId, GuestTribeId, CellId, ActionCost;
        public readonly GatheringEventKind Kind;
        public readonly GatheringStatus Status;
        public readonly bool VisibleToPlayer, HostAtSite, GuestAtSite, HostAttended, GuestAttended;
        public readonly string HostName, GuestName, Title, Detail;
        public readonly double FoodSpent, FoodTransferred, SaltTransferred, HostFoodDelta, GuestFoodDelta, HostSaltDelta, GuestSaltDelta;
        internal GatheringEvent(int id, int turn, GatheringState state, GatheringEventKind kind, string title, string detail,
            bool hostHere, bool guestHere, int actions, double cost, double food, double salt)
        {
            Id = id; Turn = turn; GatheringId = state == null ? -1 : state.Id; Kind = kind;
            HostBandId = state == null ? -1 : state.HostBandId; GuestBandId = state == null ? -1 : state.GuestBandId;
            GuestTribeId = state == null ? -1 : state.GuestTribeId; CellId = state == null ? -1 : state.CellId;
            Status = state == null ? GatheringStatus.Completed : state.Status; VisibleToPlayer = true;
            HostName = state == null ? "" : state.HostName; GuestName = state == null ? "" : state.GuestName;
            HostAtSite = hostHere; GuestAtSite = guestHere; HostAttended = state != null && state.HostAttended; GuestAttended = state != null && state.GuestAttended;
            Title = title; Detail = detail; ActionCost = actions; FoodSpent = cost; FoodTransferred = food; SaltTransferred = salt;
            HostFoodDelta = -cost - food; GuestFoodDelta = food; HostSaltDelta = -salt; GuestSaltDelta = salt;
        }
    }

    public sealed partial class Game
    {
        private bool gatheringsEnabled;
        private List<GatheringState> gatheringStates;
        private List<GatheringEvent> gatheringEvents;
        public bool GatheringsEnabled { get { return gatheringsEnabled; } }
        public ReadOnlyCollection<GatheringEvent> GatheringEvents { get { return (gatheringEvents ?? new List<GatheringEvent>()).AsReadOnly(); } }
        public ReadOnlyCollection<Gathering> Gatherings
        {
            get
            {
                return (gatheringStates ?? new List<GatheringState>()).Select(s =>
                {
                    Band host = Bands.Find(b => b.Id == s.HostBandId), guest = Bands.Find(b => b.Id == s.GuestBandId);
                    int remaining = -1;
                    if (s.Active && guest != null && guest.Population > 0 && Explored.Contains(guest.CellId))
                        remaining = GatheringRoute(guest, s.CellId, GatheringKnown(guest), null);
                    return new Gathering(s, GatheringAt(host, s.CellId), GatheringAt(guest, s.CellId), remaining);
                }).ToList().AsReadOnly();
            }
        }
        public string EnableGatherings()
        {
            if (GatheringsEnabled) return "Gatherings are already part of this story.";
            if (IsOver || !TribesEnabled || !SaltEnabled || !CulturalPlaceNames || Rules != SimulationRules.MobileUnits)
                return "A living tribe with moving units, salt and remembered places is needed before arranging gatherings.";
            InitializeGatherings();
            AddGatheringEvent(null, GatheringEventKind.Enabled, "A place to meet again", "Known peaceful peoples descended from your tribe can now be invited to a gathering.", 0, 0, 0, 0);
            return "Gatherings can now turn a remembered separation into a meeting between independent peoples.";
        }
        private void InitializeGatherings()
        { gatheringsEnabled = true; gatheringStates = new List<GatheringState>(); gatheringEvents = new List<GatheringEvent>(); }

        private static bool GatheringAt(Band band, int cell) { return band != null && band.Population > 0 && band.CellId == cell; }
        private HashSet<int> GatheringKnown(Band band, bool observedOnly = true)
        {
            // The invited journey is planned through common, observed geography.
            // Later private NPC exploration must not alter the visible route preview.
            return new HashSet<int>(KnownPlaces(band.Id).Select(p => p.CellId).Where(id => !observedOnly || Explored.Contains(id)));
        }
        private bool GatheringPartner(Band host, Band guest)
        {
            return GatheringsEnabled && !IsOver && host != null && CanControlBand(host.Id) && guest != null && guest.Population > 0 &&
                !CanControlBand(guest.Id) && Explored.Contains(host.CellId) && Explored.Contains(guest.CellId) &&
                !EncounterRules.BandsHostile(this, host.Id, guest.Id) && TribeEvents.Any(e => e.VisibleToPlayer && e.Kind == TribeEventKind.Secession &&
                    e.PreviousTribeId == PlayerTribeId && e.TribeId == TribeOf(guest.Id));
        }
        private int GatheringRoute(Band band, int destination, HashSet<int> known, List<int> path)
        {
            if (band == null || !known.Contains(band.CellId) || !known.Contains(destination) || destination < 0 || destination >= World.Cells.Length ||
                !World.Cells[destination].IsLand || World.Cells[destination].Terrain == Terrain.Ice) return -1;
            var open = new HashSet<int> { band.CellId }; var closed = new HashSet<int>();
            var cost = new Dictionary<int, int> { { band.CellId, 0 } }; var parent = new Dictionary<int, int>();
            while (open.Count > 0)
            {
                int cell = open.OrderBy(n => cost[n]).ThenBy(n => n).First(); open.Remove(cell); closed.Add(cell);
                if (cell == destination)
                {
                    if (EncounterRules.HostileAt(this, cell, band.Id)) return -1;
                    if (path != null) { int at = cell; while (at != band.CellId) { path.Add(at); at = parent[at]; } path.Reverse(); }
                    return cost[cell];
                }
                foreach (int next in World.Cells[cell].Neighbors.OrderBy(n => n))
                {
                    if (!known.Contains(next) || closed.Contains(next) || !World.Cells[next].IsLand || World.Cells[next].Terrain == Terrain.Ice ||
                        EncounterRules.HostileAt(this, next, band.Id)) continue;
                    int step = TravelRules.MoveCost(this, band, cell, next), old;
                    if (step <= 0) continue;
                    int candidate = cost[cell] + step;
                    if (!cost.TryGetValue(next, out old) || candidate < old) { cost[next] = candidate; parent[next] = cell; open.Add(next); }
                }
            }
            return -1;
        }
        public ReadOnlyCollection<int> GatheringSites(int hostBandId, int guestBandId)
        {
            Band host = Bands.Find(b => b.Id == hostBandId), guest = Bands.Find(b => b.Id == guestBandId);
            if (!GatheringPartner(host, guest)) return new List<int>().AsReadOnly();
            HashSet<int> common = GatheringKnown(host); common.IntersectWith(GatheringKnown(guest));
            Dictionary<int, int> hostCosts = GatheringDistances(host, common), guestCosts = GatheringDistances(guest, common);
            return common.Where(id => hostCosts.ContainsKey(id) && guestCosts.ContainsKey(id)).OrderBy(id => id).ToList().AsReadOnly();
        }
        private Dictionary<int, int> GatheringDistances(Band band, HashSet<int> known)
        {
            var costs = new Dictionary<int, int>();
            if (!known.Contains(band.CellId)) return costs;
            var open = new HashSet<int> { band.CellId }; costs.Add(band.CellId, 0);
            var closed = new HashSet<int>();
            while (open.Count > 0)
            {
                int cell = open.OrderBy(n => costs[n]).ThenBy(n => n).First(); open.Remove(cell); closed.Add(cell);
                foreach (int next in World.Cells[cell].Neighbors.OrderBy(n => n))
                {
                    if (!known.Contains(next) || closed.Contains(next) || !World.Cells[next].IsLand || World.Cells[next].Terrain == Terrain.Ice || EncounterRules.HostileAt(this, next, band.Id)) continue;
                    int move = TravelRules.MoveCost(this, band, cell, next), old, distance = costs[cell] + move;
                    if (move <= 0 || distance > 8) continue;
                    if (!costs.TryGetValue(next, out old) || distance < old) { costs[next] = distance; open.Add(next); }
                }
            }
            foreach (int cell in costs.Keys.Where(c => !World.Cells[c].IsLand || World.Cells[c].Terrain == Terrain.Ice || EncounterRules.HostileAt(this, c, band.Id)).ToArray()) costs.Remove(cell);
            return costs;
        }
        public GatheringInvitation PreviewGathering(int hostBandId, int guestBandId, int siteCellId)
        {
            Band host = Bands.Find(b => b.Id == hostBandId), guest = Bands.Find(b => b.Id == guestBandId);
            double food = host == null ? 0 : Math.Ceiling(host.Population * .1);
            if (!GatheringPartner(host, guest)) return new GatheringInvitation(false, false, false, "Choose a living controlled host and a known peaceful people that separated from your tribe.", food, -1, -1, -1);
            HashSet<int> common = GatheringKnown(host); common.IntersectWith(GatheringKnown(guest));
            int hostTravel = GatheringRoute(host, siteCellId, common, null), guestTravel = GatheringRoute(guest, siteCellId, common, null);
            bool site = hostTravel >= 0 && hostTravel <= 8 && guestTravel >= 0 && guestTravel <= 8;
            int deadline = Turn + Math.Max(hostTravel, guestTravel) + 4;
            if (!site) return new GatheringInvitation(false, false, false, "Choose mutually remembered, safe land within eight travel actions of both bands.", food, guestTravel, hostTravel, -1);
            string invalid = gatheringStates.Any(s => s.Active && (s.HostBandId == hostBandId || s.GuestBandId == guestBandId || s.GuestTribeId == TribeOf(guestBandId))) ?
                "One of these peoples already has an active gathering commitment." :
                gatheringStates.Any(s => s.GuestTribeId == TribeOf(guestBandId) && !s.Active && Turn < Math.Max(s.ClosedTurn, s.InvitedTurn) + 3) ?
                "Allow three turns after the previous reply or gathering before inviting this people again." :
                ActionsFor(hostBandId) < 1 ? "The host needs one remaining action to send an invitation." :
                host.Food - food < GatheringFoodNeed(host) ? "The invitation must leave the host enough food for its next close, including companion care." : null;
            if (invalid != null) return new GatheringInvitation(true, false, false, invalid, food, guestTravel, hostTravel, deadline);
            string refusal = guest.Cohesion < .3 ? "The invited band cannot commit while its own cohesion is so fragile." :
                guest.Food < GatheringFoodNeed(guest) * 1.5 || SaltEconomy.ReserveTurns(guest) < 2 ? "The invited band cannot spare a safe journey while it attends to its own supplies." :
                guest.Settled && guestTravel > 4 ? "The invited band will not leave its established hearth for a journey this long." :
                BandPersonalitiesEnabled && DispositionFor(guest.Id).Temperament == BandTemperament.Separatist && guestTravel > 4 ?
                "The invited band prefers its own path to a gathering so far away." : null;
            return new GatheringInvitation(true, true, refusal == null, refusal ?? "They will accept. Both bands must reach the agreed place; the invitation does not move either band.", food, guestTravel, hostTravel, deadline);
        }
        public string InviteGathering(int hostBandId, int guestBandId, int siteCellId)
        {
            if (GuidedOpening) return GuidedOrdersOnly;
            if (BattleLocked) return "Finish the regional battle before arranging a gathering.";
            GatheringInvitation preview = PreviewGathering(hostBandId, guestBandId, siteCellId);
            if (!preview.CanInvite) return preview.Reason;
            Band host = Bands.Find(b => b.Id == hostBandId), guest = Bands.Find(b => b.Id == guestBandId);
            SpendGatheringAction(host); host.Food -= preview.FoodCost;
            GatheringState state = new GatheringState { Id = gatheringStates.Count + 1, HostBandId = hostBandId, GuestBandId = guestBandId,
                GuestTribeId = TribeOf(guestBandId), CellId = siteCellId, InvitedTurn = Turn, ArrivalDeadline = preview.ArrivalDeadline,
                GuestOriginCell = guest.CellId, HostName = host.Name, GuestName = guest.Name, Reason = preview.Reason,
                Status = preview.WillAccept ? GatheringStatus.Traveling : GatheringStatus.Refused, ClosedTurn = preview.WillAccept ? -1 : Turn };
            gatheringStates.Add(state);
            AddGatheringEvent(state, preview.WillAccept ? GatheringEventKind.InvitationAccepted : GatheringEventKind.InvitationRefused,
                preview.WillAccept ? "An invitation is accepted" : "An invitation is declined", preview.Reason, 1, preview.FoodCost, 0, 0);
            ReconcileGatherings(false);
            return preview.WillAccept ? "The gathering is agreed. Meet at the remembered place by the close of Turn " + state.ArrivalDeadline + "." : preview.Reason;
        }
        private double GatheringFoodNeed(Band band) { return Upkeep(band) + BandEconomy.DomesticEffects(this, band).AnimalCare; }
        private void SpendGatheringAction(Band host)
        {
            tribeMembers[host.Id].Actions--;
            if (host.Id == Player.Id) Actions = tribeMembers[host.Id].Actions;
            if (BandPersonalitiesEnabled) tribePersonalityOrders[host.Id] = Turn;
            ReconcileTribe(false);
        }
        private string GatheringAtMeeting(int hostId, GatheringState state)
        {
            if (BattleLocked) return "Finish the regional battle before giving gathering orders.";
            if (!GatheringsEnabled || IsOver || state == null || state.HostBandId != hostId || !CanControlBand(hostId)) return "Choose a current gathering hosted by this living controlled band.";
            Band host = Bands.Find(b => b.Id == hostId), guest = Bands.Find(b => b.Id == state.GuestBandId);
            if (guest == null || guest.Population <= 0 || CanControlBand(guest.Id) || TribeOf(guest.Id) != state.GuestTribeId || EncounterRules.BandsHostile(this, host.Id, guest.Id)) return "This gathering's independent peaceful guest is no longer available.";
            if (state.Status != GatheringStatus.Meeting || !GatheringAt(host, state.CellId) || !GatheringAt(guest, state.CellId)) return "Both bands must be together at the first gathering before making this choice.";
            if (ActionsFor(hostId) < 1) return "The host needs one remaining action.";
            return null;
        }
        public GatheringAidOffer PreviewGatheringAid(int hostBandId, int gatheringId, double food, double salt)
        {
            GatheringState state = gatheringStates == null ? null : gatheringStates.Find(s => s.Id == gatheringId);
            string invalid = GatheringAtMeeting(hostBandId, state);
            if (invalid != null) return new GatheringAidOffer(false, invalid, food, salt, 0, 0);
            Band host = Bands.Find(b => b.Id == hostBandId), guest = Bands.Find(b => b.Id == state.GuestBandId);
            double maxFood = Math.Max(0, Math.Min(Upkeep(guest) * 2, host.Food - GatheringFoodNeed(host)));
            double maxSalt = Math.Max(0, Math.Min(SaltEconomy.Need(guest) * 2, host.Salt - SaltEconomy.Need(host)));
            invalid = state.AidGiven ? "This gathering has already received its one donation." :
                Double.IsNaN(food) || Double.IsInfinity(food) || Double.IsNaN(salt) || Double.IsInfinity(salt) || food < 0 || salt < 0 || food + salt <= 0 ? "Offer a positive finite amount of food and/or salt." :
                food > maxFood || salt > maxSalt ? "Aid is limited to two turns of the guest's needs and must leave one turn of the host's own needs." : null;
            return new GatheringAidOffer(invalid == null, invalid ?? "One action transfers these actual supplies to the independent guest. Nothing is created or promised in return.", food, salt, maxFood, maxSalt);
        }
        public string GiveGatheringAid(int hostBandId, int gatheringId, double food, double salt)
        {
            if (GuidedOpening) return GuidedOrdersOnly;
            GatheringAidOffer preview = PreviewGatheringAid(hostBandId, gatheringId, food, salt); if (!preview.CanGive) return preview.Reason;
            GatheringState state = gatheringStates.Find(s => s.Id == gatheringId); Band host = Bands.Find(b => b.Id == hostBandId), guest = Bands.Find(b => b.Id == state.GuestBandId);
            SpendGatheringAction(host); host.Food -= food; host.Salt -= salt; guest.Food += food; guest.Salt += salt; state.AidGiven = true;
            AddGatheringEvent(state, GatheringEventKind.AidGiven, "Supplies pass between peoples", NumberGathering(food) + " food and " + NumberGathering(salt) + " salt pass from " + state.HostName + " to " + state.GuestName + ". Both peoples keep their independence.", 1, 0, food, salt);
            return "The donation is delivered to the guest at the gathering.";
        }
        public GatheringReturnOffer PreviewGatheringReturn(int hostBandId, int gatheringId)
        {
            GatheringState state = gatheringStates == null ? null : gatheringStates.Find(s => s.Id == gatheringId);
            string invalid = GatheringAtMeeting(hostBandId, state);
            return new GatheringReturnOffer(invalid == null, invalid ?? "Both peoples agree to meet here again in six turns, with two turns of grace. The guest must depart before a return can count.", Turn + 6, Turn + 8);
        }
        public string AgreeGatheringReturn(int hostBandId, int gatheringId)
        {
            if (GuidedOpening) return GuidedOrdersOnly;
            GatheringReturnOffer preview = PreviewGatheringReturn(hostBandId, gatheringId); if (!preview.CanAgree) return preview.Reason;
            GatheringState state = gatheringStates.Find(s => s.Id == gatheringId); SpendGatheringAction(Bands.Find(b => b.Id == hostBandId));
            state.Status = GatheringStatus.ReturnPlanned; state.ReturnDueTurn = preview.ReturnDueTurn; state.WindowEndTurn = preview.WindowEndTurn;
            state.HostAttended = state.GuestAttended = state.GuestDeparted = false;
            state.Reason = "Return to this place from Turn " + state.ReturnDueTurn + " through Turn " + state.WindowEndTurn + ".";
            AddGatheringEvent(state, GatheringEventKind.ReturnAgreed, "A return visit is promised", state.Reason + " The guest will travel independently; each household still supplies itself.", 1, 0, 0, 0);
            return state.Reason;
        }
        private void AddGatheringEvent(GatheringState state, GatheringEventKind kind, string title, string detail, int actions, double cost, double food, double salt)
        {
            bool host = state != null && GatheringAt(Bands.Find(b => b.Id == state.HostBandId), state.CellId);
            bool guest = state != null && GatheringAt(Bands.Find(b => b.Id == state.GuestBandId), state.CellId);
            gatheringEvents.Add(new GatheringEvent(gatheringEvents.Count + 1, Turn, state, kind, title, detail, host, guest, actions, cost, food, salt));
            Log(title + ". " + detail);
        }
        private void CloseGathering(GatheringState state, GatheringStatus status, GatheringEventKind kind, string title, string reason)
        { state.Status = status; state.ClosedTurn = Turn; state.Reason = reason; AddGatheringEvent(state, kind, title, reason, 0, 0, 0, 0); }
        private void ReconcileGatherings(bool close)
        {
            if (!GatheringsEnabled) return;
            foreach (GatheringState state in gatheringStates.Where(s => s.Active).ToArray())
            {
                Band host = Bands.Find(b => b.Id == state.HostBandId), guest = Bands.Find(b => b.Id == state.GuestBandId);
                if (host == null || !CanControlBand(state.HostBandId) || guest == null || guest.Population <= 0 || CanControlBand(guest.Id) || TribeOf(guest.Id) != state.GuestTribeId)
                { CloseGathering(state, GatheringStatus.Interrupted, GatheringEventKind.Interrupted, "A gathering is interrupted", "One of the participating households is no longer available under the agreed relationship."); continue; }
                if (EncounterRules.BandsHostile(this, host.Id, guest.Id))
                { CloseGathering(state, GatheringStatus.Interrupted, GatheringEventKind.Interrupted, "Hostility interrupts a gathering", "The two peoples are now hostile. The peaceful meeting commitment has ended."); continue; }
                bool hostHere = GatheringAt(host, state.CellId), guestHere = GatheringAt(guest, state.CellId);
                if (state.Status == GatheringStatus.ReturnPlanned && !guestHere) state.GuestDeparted = true;
                bool attendanceWindow = state.Status != GatheringStatus.ReturnPlanned || Turn >= state.ReturnDueTurn;
                if (attendanceWindow) { state.HostAttended |= hostHere; state.GuestAttended |= guestHere; }
                if (state.Status == GatheringStatus.Traveling && hostHere && guestHere)
                {
                    state.Status = GatheringStatus.Meeting; state.MeetingTurn = Turn; state.WindowEndTurn = Turn + 3;
                    state.Reason = "Both bands are together. The host may offer one donation or agree a return visit before the close of Turn " + state.WindowEndTurn + ".";
                    AddGatheringEvent(state, GatheringEventKind.FirstMeeting, "Separated peoples meet again", state.HostName + " and " + state.GuestName + " reach the same remembered place. " + state.Reason, 0, 0, 0, 0);
                }
                if (state.Status == GatheringStatus.ReturnPlanned && Turn >= state.ReturnDueTurn && Turn <= state.WindowEndTurn && state.GuestDeparted && hostHere && guestHere)
                { CloseGathering(state, GatheringStatus.Fulfilled, GatheringEventKind.ReturnFulfilled, "The return promise is kept", "Both independent peoples meet again after a real departure. Their agreed return visit is fulfilled."); continue; }
                int deadline = state.Status == GatheringStatus.Traveling ? state.ArrivalDeadline : state.WindowEndTurn;
                if (Turn > deadline || close && Turn >= deadline)
                {
                    if (state.Status == GatheringStatus.Meeting)
                        CloseGathering(state, GatheringStatus.Completed, GatheringEventKind.Completed, "A gathering concludes", "The first meeting took place. No later return commitment was made.");
                    else
                    {
                        bool blocked = GatheringRoute(host, state.CellId, GatheringKnown(host), null) < 0 ||
                            Explored.Contains(guest.CellId) && GatheringRoute(guest, state.CellId, GatheringKnown(guest), null) < 0;
                        string reason = blocked ? "A safe remembered approach is no longer available; the visit was interrupted." :
                            !state.HostAttended && !state.GuestAttended ? "Neither people attended within the agreed window." :
                            !state.HostAttended ? "The invited people attended, but the host missed the agreed window." :
                            !state.GuestAttended ? "The host attended, but the invited people did not complete the promised visit." :
                            !state.GuestDeparted && state.Status == GatheringStatus.ReturnPlanned ? "There was no separate return journey; remaining together does not fulfill a return visit." :
                            "Both peoples visited, but they did not meet during the agreed window.";
                        CloseGathering(state, blocked ? GatheringStatus.Interrupted : GatheringStatus.Missed,
                            blocked ? GatheringEventKind.Interrupted : GatheringEventKind.Missed, blocked ? "A journey cannot be completed" : "A gathering promise is missed", reason);
                    }
                }
            }
        }
        private string IssueGatheringBandCommand(int actorId, string command)
        {
            string[] parts = command.Split(':'); int id, site; double food, salt;
            if (parts.Length == 3 && parts[0] == "gather-invite" && Int32.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out id) && Int32.TryParse(parts[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out site)) return InviteGathering(actorId, id, site);
            if (parts.Length == 4 && parts[0] == "gather-aid" && Int32.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out id) && Double.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out food) && Double.TryParse(parts[3], NumberStyles.Float, CultureInfo.InvariantCulture, out salt)) return GiveGatheringAid(actorId, id, food, salt);
            if (parts.Length == 2 && parts[0] == "gather-return" && Int32.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out id)) return AgreeGatheringReturn(actorId, id);
            return "Choose a valid gathering order and amounts.";
        }
        private bool TryGatheringGuestAction(Band band, int remaining, out int spent)
        {
            spent = 0;
            GatheringState state = gatheringStates.FirstOrDefault(s => s.Active && s.GuestBandId == band.Id);
            if (state == null || band.Food < GatheringFoodNeed(band) * 2 || SaltEconomy.ReserveTurns(band) < 1.5) return false;
            int destination = state.CellId;
            HashSet<int> known = GatheringKnown(band, false);
            if (state.Status == GatheringStatus.ReturnPlanned && Turn < state.ReturnDueTurn)
            {
                int cost = GatheringRoute(band, state.CellId, known, null);
                if (state.GuestDeparted && Turn < state.ReturnDueTurn - Math.Max(1, (cost + 1) / 2) - 1) return false;
                if (!state.GuestDeparted)
                {
                    destination = state.GuestOriginCell;
                    if (destination == band.CellId || GatheringRoute(band, destination, known, null) < 0)
                        destination = World.Cells[band.CellId].Neighbors.Where(n => known.Contains(n) && World.Cells[n].IsLand && World.Cells[n].Terrain != Terrain.Ice &&
                            !EncounterRules.HostileAt(this, n, band.Id)).OrderByDescending(n => ForageYield(n, band)).ThenBy(n => n).DefaultIfEmpty(-1).First();
                }
            }
            if (destination < 0) return false;
            if (band.CellId == destination)
            {
                // A guest awaiting its host stays productively at the meeting
                // place. This consumes one of its ordinary two work actions.
                band.Food += ForageYield(band.CellId, band); Depletion[band.CellId] = Math.Min(1, Depletion[band.CellId] + .18);
                spent = 1; return true;
            }
            List<int> path = new List<int>();
            if (GatheringRoute(band, destination, known, path) < 0 || path.Count == 0) return false;
            int next = path[0], move = TravelRules.MoveCost(this, band, band.CellId, next);
            if (move > remaining || band.Food - band.Population * .15 < GatheringFoodNeed(band) * 1.5) return false;
            MoveSaltBand(band, next); spent = move; return true;
        }
        internal AutoplayDecision GatheringAutoplay(Band band)
        {
            if (!GatheringsEnabled || band == null) return null;
            GatheringState state = gatheringStates.FirstOrDefault(s => s.Active && s.HostBandId == band.Id);
            if (state == null || EncounterRules.HostileAt(this, band.CellId, band.Id)) return null;
            if (Bands.Any(b => b.Population > 0 && Explored.Contains(b.CellId) && EncounterRules.BandsHostile(this, band.Id, b.Id) &&
                World.Cells[band.CellId].Neighbors.Contains(b.CellId))) return null;
            // A promise is not a survival override: the ordinary policy may
            // need to leave exhausted ground, seek salt, or avoid a threat.
            double needs = GatheringFoodNeed(band);
            if (band.Food < needs * 2 || band.Food < needs * 3 && ForageYield(band.CellId, band) < needs) return null;
            List<int> path = new List<int>(); int cost = GatheringRoute(band, state.CellId, GatheringKnown(band), path);
            if (state.Status == GatheringStatus.ReturnPlanned && Turn < state.ReturnDueTurn - Math.Max(1, cost) - 2) return null;
            string command, reason;
            if (path.Count > 0 && cost >= 0 && TravelRules.MoveCost(this, band, band.CellId, path[0]) <= ActionsFor(band.Id))
            { command = "move:" + path[0]; reason = "Honor the accepted gathering at its remembered place."; }
            else if (cost == 0)
            { command = band.Food < GatheringFoodNeed(band) * 5 ? "forage" : "wait"; reason = "Keep the host at the agreed meeting place while its guest travels."; }
            else if (cost > 0) { command = "wait"; reason = "Keep enough actions next turn for the gathering's difficult crossing."; }
            else return null;
            return new AutoplayDecision { Command = command, Reason = reason };
        }
        private static string NumberGathering(double amount) { return amount.ToString("0.##", CultureInfo.InvariantCulture); }
    }
}
