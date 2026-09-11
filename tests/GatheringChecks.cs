using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using Clio.Simulation;

namespace Clio.Tests
{
    internal static class GatheringChecks
    {
        private static int assertions;
        public static string PacingReport;
        private const BindingFlags Fields = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        private static void Check(bool value, string reason) { assertions++; if (!value) throw new Exception("Gathering check: " + reason); }
        private static Game New(int seed, bool enabled)
        { return new Game(new GameSettings(seed, LanguageStyle.Flowing, Ancestry.Human, false, "First Hearth") { FoundingCulture = CultureTemplateId.Zhol, Pace = HistoryPace.Abstract, Rules = SimulationRules.MobileUnits, CulturalPlaceNames = true, SaltEnabled = true, TribesEnabled = true, TerrainTravelEnabled = true, BandPersonalitiesEnabled = false, GatheringsEnabled = enabled }); }
        private static Game Splinter(int seed)
        {
            Game game = New(seed, true); game.Beasts.Clear(); game.Player.Population = 120; game.Player.Food = game.Player.Salt = 10000;
            game.IssueBandCommand(0, "split"); Band guest = game.Bands.Last();
            int near = game.World.Cells[game.Player.CellId].Neighbors.First(n => game.World.Cells[n].IsLand);
            guest.CellId = near; game.DiscoverPlaces(guest.Id, near);
            foreach (int cell in game.Explored) { game.World.Cells[cell].Terrain = Terrain.Grassland; game.World.Cells[cell].Temperature = .5; }
            for (int i = 0; i < 16; i++)
            {
                foreach (Band band in game.ControlledBands) { band.Food = band.Salt = 10000; game.IssueBandCommand(band.Id, "wait"); }
                game.EndTurn();
            }
            Check(game.TribeEvents.Any(e => e.Kind == TribeEventKind.Secession && e.BandId == guest.Id) && !game.CanControlBand(guest.Id), "Fixture creates an actual independent splinter through ordinary tribal closure.");
            guest.CellId = near; guest.Food = guest.Salt = 10000; guest.Cohesion = .9;
            game.Player.Food = game.Player.Salt = 10000; game.DiscoverPlaces(guest.Id, game.Player.CellId);
            return game;
        }
        private static Gathering Only(Game game) { return game.Gatherings.Last(); }
        private static void InvalidPure(Game game, Func<string> action, string reason)
        { string before = Stamp(game, false); action(); Check(Stamp(game, false) == before, reason); }
        private static void CompleteAndReturn()
        {
            Game game = Splinter(73421); Band host = game.Player, guest = game.Bands[1]; int origin = guest.CellId, site = host.CellId;
            game.World.Cells[site].Terrain = Terrain.Mountains;
            GatheringInvitation preview = game.PreviewGathering(0, 1, site);
            Check(preview.CanInvite && preview.WillAccept && preview.SiteAllowed && preview.TravelActions == 2 && preview.HostTravelActions == 0, "Invitation preview uses the real two-action mountain approach and mutual knowledge.");
            int action = game.ActionsFor(0), language = guest.LanguageId, population = guest.Population; double stock = host.Food;
            game.IssueBandCommand(0, "gather-invite:1:" + site);
            Check(game.ActionsFor(0) == action - 1 && host.Food == stock - preview.FoodCost && guest.CellId == origin, "Invitation pays exactly one host action and preparation food without teleporting the guest.");
            Check(Only(game).Status == GatheringStatus.Traveling && game.GatheringEvents.Last().FoodSpent == preview.FoodCost, "Accepted invitation has its own typed expenditure receipt.");
            InvalidPure(game, () => game.InviteGathering(0, 1, site), "A duplicate active invitation is rejected without any state or RNG change.");
            InvalidPure(game, () => game.GiveGatheringAid(0, Only(game).Id, 10, 1), "Supplies cannot be gifted remotely while the guest travels.");
            double guestBefore = guest.Food, travel = guest.Population * .15, upkeep = game.Upkeep(guest);
            game.EndTurn();
            Check(guest.CellId == site && Only(game).Status == GatheringStatus.Meeting && Only(game).MeetingTurn == game.Turn - 1, "A meeting occurs only after actual guest movement reaches the host on the agreed site.");
            EncounterRecord[] moves = game.Encounters.Records.Where(r => r.Turn == game.Turn - 1 && r.Kind == EncounterKind.Move && r.ActorId == guest.Id).ToArray();
            Check(moves.Length == 1 && moves[0].FromCell == origin && moves[0].ToCell == site && Math.Abs(moves[0].TravelPaid - travel) < 1e-9,
                "The guest's journey is one ordinary adjacent move with normal provision cost.");
            Check(Math.Abs(guest.Food - (guestBefore - travel - upkeep) * .95) < 1e-7, "A two-action mountain journey leaves no extra forage action before the guest's normal economy close.");
            Check(guest.LanguageId == language && !game.CanControlBand(guest.Id) && guest.Population == population, "The meeting changes neither guest control nor language and creates no people.");
            int id = Only(game).Id; double totalFood = host.Food + guest.Food, totalSalt = host.Salt + guest.Salt;
            GatheringAidOffer aid = game.PreviewGatheringAid(0, id, 10, 2);
            Check(aid.CanGive && aid.MaxFood <= game.Upkeep(guest) * 2 && aid.MaxSalt <= SaltEconomy.Need(guest) * 2, "Aid preview bounds each donation by recipient needs and the donor reserve.");
            game.IssueBandCommand(0, "gather-aid:" + id + ":10:2");
            Check(Math.Abs(host.Food + guest.Food - totalFood) < 1e-9 && Math.Abs(host.Salt + guest.Salt - totalSalt) < 1e-9 && Only(game).AidGiven,
                "A co-located donation conserves food and salt exactly.");
            GatheringEvent transfer = game.GatheringEvents.Last();
            Check(transfer.Kind == GatheringEventKind.AidGiven && transfer.HostFoodDelta == -10 && transfer.GuestFoodDelta == 10 && transfer.HostSaltDelta == -2 && transfer.GuestSaltDelta == 2 && transfer.FoodSpent == 0,
                "The donation receipt separates true transfers from invitation consumption for exact journal accounting.");
            InvalidPure(game, () => game.GiveGatheringAid(0, id, 1, 0), "A meeting cannot receive a second donation.");
            GatheringReturnOffer returning = game.PreviewGatheringReturn(0, id);
            Check(returning.CanAgree && returning.ReturnDueTurn == game.Turn + 6 && returning.WindowEndTurn == game.Turn + 8, "The return promise quotes one bounded six-turn appointment and two-turn grace window.");
            game.IssueBandCommand(0, "gather-return:" + id);
            Check(Only(game).Status == GatheringStatus.ReturnPlanned && !Only(game).GuestDeparted && !Only(game).HostAttended, "An agreed return resets attendance and cannot count continued co-location as a return.");
            InvalidPure(game, () => game.AgreeGatheringReturn(0, id), "A second return agreement cannot extend or duplicate the commitment.");
            game.EndTurn();
            Check(Only(game).GuestDeparted && guest.CellId != site, "The guest leaves physically under its ordinary movement budget before a return can count.");
            while (Only(game).Active && game.Turn <= returning.WindowEndTurn + 1)
            { game.IssueBandCommand(0, "wait"); game.EndTurn(); }
            Check(Only(game).Status == GatheringStatus.Fulfilled && Only(game).ClosedTurn >= returning.ReturnDueTurn && Only(game).ClosedTurn <= returning.WindowEndTurn,
                "An actual separated guest returns and fulfills the meeting in its agreed window.");
            Check(game.GatheringEvents.Count(e => e.Kind == GatheringEventKind.ReturnFulfilled) == 1 && guest.LanguageId == language && !game.CanControlBand(guest.Id),
                "Return fulfillment is recorded once and never annexes or linguistically merges the guest.");
            int closedEvents = game.GatheringEvents.Count; game.EndTurn(); Check(game.GatheringEvents.Count == closedEvents, "A closed promise does not generate repeated completion events.");
        }
        private static void ValidationPrivacyAndRefusal()
        {
            Game game = Splinter(0); Band host = game.Player, guest = game.Bands[1]; int site = host.CellId;
            AdvisoryFreePurity(game, site);
            int hidden = game.World.Cells.First(c => !game.Explored.Contains(c.Id) && c.IsLand).Id;
            InvalidPure(game, () => game.InviteGathering(0, 1, hidden), "An uncharted invitation site cannot reveal terrain, consume effort or mutate RNG.");
            InvalidPure(game, () => game.IssueBandCommand(0, "gather-aid:1:NaN:4"), "Malformed/nonfinite aid cannot change the state.");
            InvalidPure(game, () => game.IssueBandCommand(0, "gather-invite:bogus:2"), "Malformed gathering targets are atomic rejections.");
            Band unrelated = new Band { Id = 90, Population = 50, CellId = site, Name = "Unrelated", LanguageId = 0, Food = 1000, Salt = 1000 };
            game.Bands.Add(unrelated); InvalidPure(game, () => game.InviteGathering(0, 90, site), "An unrelated known founder cannot pose as a player-origin splinter.");
            game.Bands.Remove(unrelated);
            guest.Food = 0; GatheringInvitation refused = game.PreviewGathering(0, 1, site);
            Check(refused.CanInvite && !refused.WillAccept && refused.Reason.Contains("own supplies"), "A guest facing supply pressure gives a deterministic factual refusal preview.");
            double food = host.Food; int actions = game.ActionsFor(0); game.InviteGathering(0, 1, site);
            Check(Only(game).Status == GatheringStatus.Refused && host.Food == food - refused.FoodCost && game.ActionsFor(0) == actions - 1 && guest.CellId != site,
                "Sending a valid invitation pays its stated cost even when the guest declines; the guest does not travel.");
            InvalidPure(game, () => game.InviteGathering(0, 1, site), "Refusal cooldown prevents rerolled or repeated free responses.");
            game.IssueBandCommand(0, "wait"); Check(game.GatheringSites(0, 1).Contains(site), "No remaining actions does not erase the mutually known site chooser.");
            Check(game.PreviewGathering(0, 1, site).SiteAllowed && !game.PreviewGathering(0, 1, site).CanInvite, "Site validity remains distinct from present invitation affordability.");
            game = Splinter(17); host = game.Player; guest = game.Bands[1]; site = host.CellId;
            game.InviteGathering(0, 1, site); game.EndTurn(); int id = Only(game).Id;
            foreach (double bad in new[] { -1, Double.NaN, Double.PositiveInfinity }) InvalidPure(game, () => game.GiveGatheringAid(0, id, bad, 1), "Invalid aid quantity preserves all fields.");
            InvalidPure(game, () => game.GiveGatheringAid(0, id, 0, 0), "A zero gift creates neither labor nor an event.");
            InvalidPure(game, () => game.GiveGatheringAid(0, id, host.Food, host.Salt), "A donation cannot empty the host's own survival reserve.");
            hidden = game.World.Cells.First(c => !game.Explored.Contains(c.Id) && c.IsLand).Id;
            string recordedName = Only(game).GuestName; guest.CellId = hidden; int originCount = game.GatheringEvents.Count;
            Gathering snapshot = Only(game); Check(snapshot.GuestTravelActionsRemaining == -1 && !snapshot.GuestAtSite && snapshot.GuestName == recordedName,
                "A hidden guest retains its recorded identity but exposes no current position or route length.");
            string publicBefore = Stamp(game.Gatherings, false); guest.Name = "HIDDEN CHANGED NAME"; guest.Food = 987654; guest.Population += 500;
            foreach (Cell cell in game.World.Cells.Where(c => !game.Explored.Contains(c.Id))) { cell.Terrain = Terrain.Mountains; cell.Forage = 999; }
            Check(Stamp(game.Gatherings, false) == publicBefore && game.GatheringEvents.Count == originCount, "Hidden identity, stores, population and terrain do not change remembered public commitment records.");
        }
        private static void AdvisoryFreePurity(Game game, int site)
        {
            string original = Stamp(game, false), expected = Stamp(game.PreviewGathering(0, 1, site), false);
            for (int i = 0; i < 6; i++)
            {
                game.GatheringSites(0, 1); game.Gatherings.ToArray(); game.GatheringEvents.ToArray();
                Check(Stamp(game.PreviewGathering(0, 1, site), false) == expected && Stamp(game, false) == original, "Repeated gathering previews and views preserve all state and both simulation random streams.");
            }
        }
        private static void MissedInterruptedAndAutoplay()
        {
            Game game = Splinter(2); int site = game.Player.CellId, away = game.Bands[1].CellId;
            game.InviteGathering(0, 1, site); game.IssueBandCommand(0, "move:" + away);
            while (Only(game).Active) { game.Player.Food = game.Player.Salt = 10000; game.IssueBandCommand(0, "wait"); game.EndTurn(); }
            Check(Only(game).Status == GatheringStatus.Missed && Only(game).GuestAttended, "A guest that arrives without meeting its absent host produces a missed gathering, not a fulfilled promise.");
            game = Splinter(3); site = game.Player.CellId; game.InviteGathering(0, 1, site); game.Bands[1].Population = 0; game.EndTurn();
            Check(Only(game).Status == GatheringStatus.Interrupted && game.GatheringEvents.Last().Kind == GatheringEventKind.Interrupted, "Guest death interrupts rather than morally blaming a missed promise.");
            game = Splinter(4); site = game.Player.CellId;
            game.Bands[1].CellId = game.World.Cells[site].Neighbors.First(n => game.Explored.Contains(n) && game.World.Cells[n].IsLand && TravelRules.MoveCost(game, game.Player, site, n) == 1);
            game.DiscoverPlaces(1, site); game.InviteGathering(0, 1, site);
            game.Player.Population = 300; string attack = game.IssueBandCommand(0, "attack-band:1");
            Check(Only(game).Status == GatheringStatus.Interrupted && game.GatheringEvents.Last().Detail.Contains("hostile"), "An actual attack interrupts the peaceful agreement immediately without recording a false first meeting. " + attack + " / " + Only(game).Reason);
            Check(!game.GatheringEvents.Any(e => e.Kind == GatheringEventKind.FirstMeeting), "An attacking approach cannot briefly claim a peaceful gathering before the feud is established.");
            int honored = 0;
            foreach (int seed in new[] { 0, 17, 73421 })
            {
                game = Splinter(seed); Band guest = game.Bands[1]; site = guest.CellId;
                game.InviteGathering(0, 1, site); int firstTurn = game.Turn, steps = 0;
                while (Only(game).Status == GatheringStatus.Traveling && !game.IsOver && steps++ < 100)
                {
                    string before = Stamp(game, false); AutoplayDecision decision = AutoplayPolicy.Choose(game);
                    Check(Stamp(game, false) == before, "Gathering-aware autoplay choice is pure.");
                    int turn = game.Turn, actions = game.ControlledBands.Sum(b => game.ActionsFor(b.Id));
                    Apply(game, decision.Command);
                    Check(game.Turn != turn || game.ControlledBands.Sum(b => game.ActionsFor(b.Id)) < actions, "Honoring a gathering progresses through ordinary legal autoplay commands.");
                }
                if (Only(game).Status == GatheringStatus.Meeting) honored++;
                Check(Only(game).Status == GatheringStatus.Meeting && steps < 100 && game.Turn <= firstTurn + 4, "Autoplay carries an accepted host to its meeting instead of blindly wandering away.");
            }
            PacingReport = "Gathering hosts honored " + honored + "/3 accepted appointments through ordinary autoplay; one explicit return journey fulfilled under normal NPC action and food costs.";
        }
        private static void CompatibilityAndMigration()
        {
            Game old = new Game(new GameSettings(73421, LanguageStyle.Flowing, Ancestry.Human, false, "First Hearth") { FoundingCulture = CultureTemplateId.Zhol, Pace = HistoryPace.Abstract, Rules = SimulationRules.MobileUnits, CulturalPlaceNames = true, SaltEnabled = true, TribesEnabled = true, TerrainTravelEnabled = true, BandPersonalitiesEnabled = false });
            Game disabled = New(73421, false), enabled = New(73421, true);
            Check(!old.GatheringsEnabled && !disabled.GatheringsEnabled && Stamp(old, false) == Stamp(disabled, false), "All previous constructor semantics remain identical to the explicit disabled gathering overload.");
            for (int i = 0; i < 50 && !old.IsOver; i++)
            {
                string command = AutoplayPolicy.Choose(old).Command;
                Apply(old, command); Apply(disabled, command); Apply(enabled, command);
                Check(Stamp(old, false) == Stamp(disabled, false), "Disabled gathering hooks leave every legacy game and RNG field unchanged through play.");
                Check(Stamp(old, true) == Stamp(enabled, true), "Enabled rules with no invitations do not alter existing gameplay or random flow.");
            }
            int turn = old.Turn, actions = old.Actions; double food = old.Player.Food, salt = old.Player.Salt;
            uint actionRandom = (uint)typeof(Game).GetField("actionRandom", Fields).GetValue(old), ecologyRandom = (uint)typeof(Game).GetField("ecologyRandom", Fields).GetValue(old);
            old.EnableGatherings();
            Check(old.GatheringsEnabled && old.Turn == turn && old.Actions == actions && old.Player.Food == food && old.Player.Salt == salt &&
                actionRandom == (uint)typeof(Game).GetField("actionRandom", Fields).GetValue(old) && ecologyRandom == (uint)typeof(Game).GetField("ecologyRandom", Fields).GetValue(old),
                "Migration spends no supplies, actions, time or random draws.");
            InvalidPure(old, () => old.EnableGatherings(), "Repeated enabling cannot refill, reset or duplicate gathering history.");
            Game classic = new Game(new GameSettings(4, LanguageStyle.Flowing, Ancestry.Human, false, "Old"));
            InvalidPure(classic, () => classic.EnableGatherings(), "Unsupported legacy rules cannot be partially upgraded by enabling gatherings.");
            InvalidPure(classic, () => classic.InviteGathering(0, 1, classic.Player.CellId), "Gathering commands remain harmless when disabled.");
            Check(typeof(Gathering).GetFields(BindingFlags.Public | BindingFlags.Instance).All(f => f.IsInitOnly) &&
                typeof(GatheringEvent).GetFields(BindingFlags.Public | BindingFlags.Instance).All(f => f.IsInitOnly), "All gathering public snapshots and receipts are immutable.");
        }
        private static void BoundedWindowsAndReserves()
        {
            Game game = Splinter(29); Band host = game.Player, guest = game.Bands[1]; int site = host.CellId;
            game.InviteGathering(0, 1, site); game.EndTurn(); int id = Only(game).Id;
            game.Beasts.Add(new Beast { Id = 700, Kind = BeastKind.Wolves, Count = 8, Domestic = true, OwnerId = host.Id, CellId = site });
            double care = BandEconomy.DomesticEffects(game, host).AnimalCare;
            host.Food = game.Upkeep(host) + care + 3; host.Salt = SaltEconomy.Need(host) + .5;
            GatheringAidOffer exact = game.PreviewGatheringAid(0, id, 3, .5);
            Check(care > 0 && exact.CanGive && Math.Abs(exact.MaxFood - 3) < 1e-9 && Math.Abs(exact.MaxSalt - .5) < 1e-9,
                "Donation bounds reserve the donor's real companion care as well as its own food and salt needs.");
            InvalidPure(game, () => game.GiveGatheringAid(0, id, 3.01, .5), "Even a small donation beyond the next-close reserve is refused atomically.");
            game.GiveGatheringAid(0, id, 3, .5);
            EconomyForecast forecast = BandEconomy.Forecast(game, host);
            Check(forecast.HungerLosses == 0 && forecast.SaltLosses == 0 && forecast.EndingSaltShortageTurns == 0,
                "The maximum safe donation leaves enough actual resources for the donor's next household close.");
            while (Only(game).Active) { host.Food = host.Salt = 10000; game.IssueBandCommand(0, "wait"); game.EndTurn(); }
            Check(Only(game).Status == GatheringStatus.Completed && game.GatheringEvents.Count(e => e.Kind == GatheringEventKind.Completed) == 1,
                "A first meeting with no return agreement closes normally once, without inventing a missed promise.");

            game = Splinter(30); host = game.Player; guest = game.Bands[1]; site = host.CellId;
            game.InviteGathering(0, 1, site); game.EndTurn(); id = Only(game).Id; game.AgreeGatheringReturn(0, id);
            foreach (int cell in game.World.Cells[site].Neighbors) game.World.Cells[cell].Terrain = Terrain.Ocean;
            while (Only(game).Active) { host.Food = host.Salt = guest.Food = guest.Salt = 10000; game.IssueBandCommand(0, "wait"); game.EndTurn(); }
            Check(Only(game).Status == GatheringStatus.Missed && !Only(game).GuestDeparted && !game.GatheringEvents.Any(e => e.Kind == GatheringEventKind.ReturnFulfilled),
                "Continued co-location through a return window cannot fake a departure and second visit.");

            game = Splinter(31); host = game.Player; guest = game.Bands[1]; site = host.CellId;
            game.InviteGathering(0, 1, site); game.World.Cells[site].Terrain = Terrain.Ice;
            while (Only(game).Active) { host.Food = host.Salt = guest.Food = guest.Salt = 10000; game.IssueBandCommand(0, "wait"); game.EndTurn(); }
            Check(Only(game).Status == GatheringStatus.Interrupted && Only(game).Reason.Contains("approach"),
                "A genuinely blocked safe approach is distinguished from a host simply missing the agreed window.");

            game = Splinter(32); host = game.Player; guest = game.Bands[1]; game.InviteGathering(0, 1, guest.CellId);
            host.Food = game.Upkeep(host) * .25; host.Salt = 10000; game.Depletion[host.CellId] = 1;
            string promised = AutoplayPolicy.Choose(game).Command;
            typeof(Game).GetField("gatheringsEnabled", Fields).SetValue(game, false);
            string ordinary = AutoplayPolicy.Choose(game).Command;
            typeof(Game).GetField("gatheringsEnabled", Fields).SetValue(game, true);
            Check(promised == ordinary, "An accepted gathering does not replace the ordinary survival decision when the host is short of food.");

            game = Splinter(33); host = game.Player; game.InviteGathering(0, 1, host.CellId); host.Population = 0; game.IssueBandCommand(0, "wait");
            Check(!game.CanControlBand(0) && !game.PreviewGatheringAid(0, Only(game).Id, 1, 0).CanGive,
                "A dead host cannot spend actions or send gifts even before the next reconciliation.");
        }
        public static int Run()
        { assertions = 0; CompleteAndReturn(); ValidationPrivacyAndRefusal(); MissedInterruptedAndAutoplay(); BoundedWindowsAndReserves(); CompatibilityAndMigration(); return assertions; }
        private static void Apply(Game game, string command)
        {
            if (command == "end") game.EndTurn();
            else { int colon = command.IndexOf(':', 5); game.IssueBandCommand(Int32.Parse(command.Substring(5, colon - 5)), command.Substring(colon + 1)); }
        }
        private static string Stamp(object value, bool excludeGathering)
        {
            StringBuilder text = new StringBuilder(); Append(text, value, excludeGathering);
            using (SHA256 hash = SHA256.Create()) return BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(text.ToString())));
        }
        private static void Append(StringBuilder text, object value, bool excludeGathering)
        {
            if (value == null) { text.Append("null;"); return; } Type type = value.GetType(); text.Append(type.FullName).Append(':');
            if (type.IsPrimitive || type.IsEnum || value is string) { text.Append(value is double ? ((double)value).ToString("R", CultureInfo.InvariantCulture) : Convert.ToString(value, CultureInfo.InvariantCulture)).Append(';'); return; }
            IDictionary dictionary = value as IDictionary;
            if (dictionary != null) { foreach (object key in dictionary.Keys.Cast<object>().OrderBy(k => Convert.ToString(k, CultureInfo.InvariantCulture), StringComparer.Ordinal)) { Append(text, key, excludeGathering); Append(text, dictionary[key], excludeGathering); } return; }
            IEnumerable sequence = value as IEnumerable;
            if (sequence != null) { foreach (object item in sequence) Append(text, item, excludeGathering); text.Append("end;"); return; }
            foreach (FieldInfo field in type.GetFields(Fields).OrderBy(f => f.Name, StringComparer.Ordinal))
            { if (excludeGathering && type == typeof(Game) && field.Name.StartsWith("gathering", StringComparison.Ordinal)) continue; text.Append(field.Name); Append(text, field.GetValue(value), excludeGathering); }
        }
    }
}
