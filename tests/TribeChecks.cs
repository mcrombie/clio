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
    public static class TribeChecks
    {
        private static int checks;
        public static string PacingReport;
        public static int Run()
        {
            checks = 0;
            DefaultsAndMigration(); SplittingAndEconomy(); ActorOrders(); ContactAndSecession(); SuccessionAndExtinction(); Privacy(); Autoplay();
            return checks;
        }
        private static void Check(bool value, string reason) { checks++; if (!value) throw new InvalidOperationException("Tribe check: " + reason); }
        private static Game Create(int seed = 73421, bool tribes = true, bool four = false)
        { return new Game(seed, LanguageStyle.Flowing, Ancestry.Human, four, "Zholhen", CultureTemplateId.Zhol, HistoryPace.Abstract, SimulationRules.MobileUnits, true, true, tribes); }
        private static object Field(object value, string name)
        { return value.GetType().GetField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance).GetValue(value); }
        private static Band Daughter(Game game)
        {
            game.Beasts.Clear(); game.Player.Population = 120; game.Player.Food = 1200; game.Player.Salt = 120;
            int id = game.Bands.Count; game.IssueBandCommand(game.Player.Id, "split"); return game.Bands.Single(b => b.Id == id);
        }
        private static void DefaultsAndMigration()
        {
            Game old = new Game(7, LanguageStyle.Flowing, Ancestry.Human, true, "");
            Check(!old.TribesEnabled && old.ControlledBands.Single() == old.Player && old.TribeLeaderBand == old.Player, "Old constructors retain a single controlled founder.");
            string before = Stamp(old); old.EnableTribes(); old.IssueBandCommand(0, "forage");
            Check(Stamp(old) == before, "Disabled commands and migration without prerequisites are atomic.");
            Game enabled = Create(), disabled = Create(73421, false);
            Check(Stamp(enabled.World) == Stamp(disabled.World) && Stamp(enabled.Beasts) == Stamp(disabled.Beasts), "Tribal initialization changes neither geography nor wildlife.");
            Check(Field(enabled, "actionRandom").Equals(Field(disabled, "actionRandom")) && Field(enabled, "ecologyRandom").Equals(Field(disabled, "ecologyRandom")), "Tribal initialization consumes no old random stream.");
            disabled.Beasts.Clear(); disabled.Player.Population = 120; disabled.Player.Food = 1000; disabled.Split(); Band previousDaughter = disabled.Bands.Last();
            int turn = disabled.Turn, actions = disabled.Actions; string people = Stamp(disabled.Bands); object rng = Field(disabled, "actionRandom");
            disabled.EnableTribes();
            Check(disabled.TribesEnabled && disabled.ControlledBands.Single() == disabled.Player && !disabled.CanControlBand(previousDaughter.Id), "Migration preserves every preexisting other polity's independence.");
            Check(disabled.Turn == turn && disabled.ActionsFor(0) == actions && Stamp(disabled.Bands) == people && rng.Equals(Field(disabled, "actionRandom")), "Migration preserves people, supply, time, spent actions and RNG.");
            before = Stamp(disabled); disabled.EnableTribes(); Check(before == Stamp(disabled), "Repeated enabling neither refills orders nor duplicates events.");
            before = Stamp(enabled); enabled.TribeStatus(0); enabled.ControlledBands.ToArray(); enabled.TribeLeaderBand.ToString(); enabled.CanMoveBand(0, -1);
            Check(before == Stamp(enabled), "Membership and movement read models are pure.");
            bool rejected = false;
            try { new Game(1, LanguageStyle.Flowing, Ancestry.Human, false, "", CultureTemplateId.Zhol, HistoryPace.Abstract, SimulationRules.Classic, true, true, true); }
            catch (ArgumentException) { rejected = true; }
            Check(rejected, "Tribes cannot initialize without mobile encounters.");
            rejected = false;
            try { new Game(1, LanguageStyle.Flowing, Ancestry.Human, false, "", CultureTemplateId.Zhol, HistoryPace.Abstract, SimulationRules.MobileUnits, true, false, true); }
            catch (ArgumentException) { rejected = true; }
            Check(rejected, "Tribes cannot initialize without salt.");
        }
        private static void SplittingAndEconomy()
        {
            Game game = Create(); Band player = game.Player; player.SaltShortageTurns = 2; Band daughter = Daughter(game);
            Check(ReferenceEquals(player, game.Player) && game.TribePopulation == 120 && game.CanControlBand(daughter.Id), "Splitting preserves the founder identity and tribe population.");
            Check(Math.Abs(game.ControlledBands.Sum(b => b.Food) - 1200) < 1e-8 && Math.Abs(game.ControlledBands.Sum(b => b.Salt) - 120) < 1e-8, "Split food and salt are conserved across households.");
            Check(game.ActionsFor(0) == 1 && game.ActionsFor(daughter.Id) == 0 && daughter.SaltShortageTurns == 2, "A daughter inherits shortage state but no extra current-turn orders.");
            Check(game.TribeStatus(daughter.Id).ParentBandId == 0 && game.TribeOf(daughter.Id) == game.PlayerTribeId, "Parentage and tribal membership are explicit.");
            Check(game.KnownPlaces(daughter.Id).Any(p => p.Acquisition == PlaceAcquisition.Inherited && p.LearnedFromBandId == 0), "A daughter inherits actual named-place provenance.");
            string before = Stamp(game); game.IssueBandCommand(daughter.Id, "forage"); Check(Stamp(game) == before, "A newborn cannot obtain free gathering actions.");
            int childCell = daughter.CellId; double childFood = daughter.Food; EconomyForecast expected = BandEconomy.Forecast(game, daughter);
            game.EndTurn();
            Check(daughter.CellId == childCell && game.LastTribeEconomy.Count == 2, "Controlled bands do not receive independent AI travel or gathering at close.");
            TribeEconomyReceipt receipt = game.LastTribeEconomy.Single(r => r.BandId == daughter.Id);
            Check(receipt.StartingFood == childFood && daughter.Food == expected.EndingFood && daughter.Population == expected.EndingPopulation && daughter.Salt == expected.EndingSalt, "A daughter resolves exactly one forecast from its true starting supplies.");
            Check(receipt.Forecast.EndingFood == daughter.Food && receipt.StartingPopulation == 40 && receipt.Turn == 1, "The per-household receipt preserves the actual pre-economy population and turn.");
            Check(game.ActionsFor(0) == 2 && game.ActionsFor(daughter.Id) == 2, "Each surviving controlled band receives two fresh orders.");
            daughter.CellId = player.CellId; daughter.Population = player.Population; daughter.SaltShortageTurns = player.SaltShortageTurns;
            foreach (Milestone k in game.Knowledge) k.Known = true;
            daughter.Settled = player.Settled = true;
            Check(game.ForageYield(player.CellId, player) == game.ForageYield(daughter.CellId, daughter) && BandEconomy.CampFood(game, player) == BandEconomy.CampFood(game, daughter), "Gathering and gardening knowledge support every controlled band.");
            daughter.Food = player.Food = 1000;
            Check(BandEconomy.Forecast(game, daughter).EndingFood == BandEconomy.Forecast(game, player).EndingFood, "Shared storage knowledge has the same household effect.");
        }
        private static void ActorOrders()
        {
            Game game = Create(); Band daughter = Daughter(game); game.EndTurn(); Band founder = game.Player;
            double oldFounderFood = founder.Food, childFood = daughter.Food; int founderCell = founder.CellId;
            game.IssueBandCommand(daughter.Id, "forage");
            Check(founder.Food == oldFounderFood && daughter.Food > childFood && game.ActionsFor(0) == 2 && game.ActionsFor(daughter.Id) == 1, "Actor gathering spends only that band's action and increases only its food.");
            int target = game.World.Cells[daughter.CellId].Neighbors.First(n => game.CanMoveBand(daughter.Id, n) && game.World.Cells[n].Terrain != Terrain.Ice);
            Beast pet = new Beast { Id = 999, CellId = daughter.CellId, Count = 3, Domestic = true, OwnerId = daughter.Id, Kind = BeastKind.Wolves, BreedName = "Test companions" }; game.Beasts.Add(pet);
            childFood = daughter.Food; game.IssueBandCommand(daughter.Id, "move:" + target);
            Check(daughter.CellId == target && pet.CellId == target && founder.CellId == founderCell && founder.Food == oldFounderFood, "Moving a daughter takes its own companions and never teleports the founder.");
            Check(daughter.Food < childFood && game.ActionsFor(daughter.Id) == 0, "Actor movement pays ordinary travel food and AP.");
            string before = Stamp(game); game.IssueBandCommand(daughter.Id, "move:" + founderCell); game.IssueBandCommand(88, "forage"); game.IssueBandCommand(0, "end"); game.IssueBandCommand(0, "move:no");
            Check(Stamp(game) == before, "Invalid, exhausted, foreign and malformed actor commands are atomic.");
            game.EndTurn(); game.IssueBandCommand(daughter.Id, "camp"); Check(daughter.Settled && !founder.Settled, "A daughter's camp belongs to that household.");
            int source = SaltEconomy.KnownSources(game).First(); daughter.CellId = source; daughter.Settled = false; daughter.Salt = 0;
            oldFounderFood = founder.Food; double founderSalt = founder.Salt; double gathered = SaltEconomy.GatherYield(game, daughter);
            Check(SaltEconomy.CanGather(game, daughter), "A controlled daughter can gather at a known source with remaining AP.");
            game.IssueBandCommand(daughter.Id, "salt");
            Check(daughter.Salt == gathered && founder.Salt == founderSalt && founder.Food == oldFounderFood && !SaltEconomy.CanGather(game, daughter), "Salt collection uses only the selected household's reserve and AP.");
            game.EndTurn(); daughter.CellId = founder.CellId; game.Beasts.Clear();
            Beast targetBeast = new Beast { Id = 888, CellId = daughter.CellId, Count = 2, Kind = BeastKind.Deer }; game.Beasts.Add(targetBeast);
            before = Stamp(game); EncounterOutlook friendly = EncounterRules.Outlook(game, daughter.Id, UnitKind.Band, founder.Id);
            Check(!friendly.CanAttack && Stamp(game) == before, "Actor preview cannot attack another band in the same tribe and is pure.");
            oldFounderFood = founder.Food; childFood = daughter.Food;
            Check(EncounterRules.Outlook(game, daughter.Id, UnitKind.Animal, targetBeast.Id).CanAttack, "A valid animal target is reachable from the acting daughter.");
            game.IssueBandCommand(daughter.Id, "attack-animal:" + targetBeast.Id);
            EncounterRecord record = game.Encounters.Records.Last(r => r.Kind == EncounterKind.Attack);
            Check(record.ActorId == daughter.Id && record.PlayerInvolved && record.PlayerFoodDelta == 0 && founder.Food == oldFounderFood, "A daughter's encounter identifies its actual actor while retaining founder-only food delta.");
            Check(record.ActorFoodDelta == daughter.Food - childFood && record.ActorFoodDelta > 0, "The typed encounter records the actual acting household's food gain.");
            game.Beasts.Clear(); Supply(game); game.EndTurn();
            int remote = game.World.Cells.First(c => c.IsLand && c.Terrain != Terrain.Ice && c.Id != founder.CellId && !game.World.Cells[founder.CellId].Neighbors.Contains(c.Id)).Id;
            daughter.CellId = remote; game.DiscoverPlaces(daughter.Id, remote);
            Beast willing = new Beast { Id = 889, CellId = remote, Kind = BeastKind.Deer, Count = 3, PositiveContacts = 5 }; game.Beasts.Add(willing);
            Check(EncounterRules.Outlook(game, daughter.Id, UnitKind.Animal, willing.Id).CanBefriend && !EncounterRules.Outlook(game, founder.Id, UnitKind.Animal, willing.Id).CanBefriend,
                "Actor reach is independent of the founder when a daughter operates far away.");
            typeof(Game).GetField("actionRandom", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(game, (uint)1);
            oldFounderFood = founder.Food; game.IssueBandCommand(daughter.Id, "befriend-animal:" + willing.Id);
            Check(willing.Domestic && willing.OwnerId == daughter.Id && founder.Food == oldFounderFood, "A successful remote animal bond belongs to the actual approaching daughter.");
            Check(!EncounterRules.Outlook(game, daughter.Id, UnitKind.Animal, willing.Id).CanAttack, "Actor commands cannot attack their own tribe's companion lineages.");
        }
        private static void ContactAndSecession()
        {
            Game game = Create(); Band daughter = Daughter(game);
            for (int i = 0; i < 8; i++) { Supply(game); game.EndTurn(); }
            Check(game.TribeStatus(daughter.Id).TurnsAway == 8 && game.TribeStatus(daughter.Id).Drifting && game.TribeEvents.Count(e => e.Kind == TribeEventKind.Drift) == 1, "Eight missed closes marks drift once without immediate separation.");
            game.IssueBandCommand(daughter.Id, "move:" + game.TribeLeaderBand.CellId);
            Check(game.TribeStatus(daughter.Id).TurnsAway == 0 && !game.TribeStatus(daughter.Id).Drifting && game.TribeEvents.Count(e => e.Kind == TribeEventKind.Reunion) == 1, "Physical reunion clears drift immediately before the next close.");
            int away = game.World.Cells[daughter.CellId].Neighbors.First(n => game.CanMoveBand(daughter.Id, n) && game.World.Cells[n].Terrain != Terrain.Ice);
            game.IssueBandCommand(daughter.Id, "move:" + away); int previousLanguage = daughter.LanguageId; string root = Stamp(game.Languages[previousLanguage]);
            for (int i = 0; i < 15; i++) { Supply(game); game.EndTurn(); }
            Check(game.CanControlBand(daughter.Id) && game.TribeStatus(daughter.Id).TurnsAway == 15, "A band remains controllable through fifteen missed reunions.");
            Supply(game); double food = daughter.Food; int population = daughter.Population; EconomyForecast expected = BandEconomy.Forecast(game, daughter); game.EndTurn();
            Check(!game.CanControlBand(daughter.Id) && game.TribeOf(daughter.Id) != game.PlayerTribeId && daughter.Population == expected.EndingPopulation && daughter.Food == expected.EndingFood, "The sixteenth missed close makes a separate polity without inventing or deleting its people and resources.");
            Check(daughter.LanguageId != previousLanguage && game.Languages[daughter.LanguageId].ParentId == previousLanguage && Stamp(game.Languages[previousLanguage]) == root, "Secession branches language without mutating the founding tongue.");
            Check(!EncounterRules.BandsHostile(game, daughter.Id, game.Player.Id) && game.TribeEvents.Count(e => e.Kind == TribeEventKind.Secession) == 1, "Secession is recorded once and creates no automatic war.");
            Check(game.LastTribeEconomy.Any(r => r.BandId == daughter.Id), "The last controlled close has an exact receipt before separation.");
            string before = Stamp(game); game.IssueBandCommand(daughter.Id, "forage"); Check(Stamp(game) == before, "A seceded polity rejects further player orders atomically.");
            Supply(game); game.EndTurn(); Check(!game.LastTribeEconomy.Any(r => r.BandId == daughter.Id), "Later independent economies are excluded from player-tribe receipts.");
        }
        private static void SuccessionAndExtinction()
        {
            foreach (string command in new[] { "attack-animal:999", "befriend-animal:999", "attack-band:2" })
            {
                Game game = Create(); Band daughter = Daughter(game); game.EndTurn(); game.Player.Population = 1; game.Player.Food = 1000;
                game.Beasts.Add(new Beast { Id = 999, Kind = BeastKind.Dragon, Count = 20, CellId = game.Player.CellId });
                game.Bands.Add(new Band { Id = 2, Name = "Opponents", CellId = game.Player.CellId, Population = 1000, Food = 1000 });
                game.IssueBandCommand(0, command);
                Check(game.Player.Population == 0 && !game.IsOver && game.TribeLeaderBand == daughter, "A fatal founder encounter yields deterministic living succession: " + command);
                Check(game.TribeEvents.Count(e => e.Kind == TribeEventKind.Succession) == 1 && game.CanControlBand(daughter.Id) && !game.CanControlBand(0), "The old founder remains stable while a living daughter takes leadership.");
                game.Beasts.Clear(); game.Bands.Last().Population = 0; int turn = game.Turn; Supply(game); game.EndTurn();
                Check(game.Turn == turn + 1 && game.ActionsFor(daughter.Id) == 2 && game.LastTribeEconomy.Single().BandId == daughter.Id, "A tribe keeps resolving the survivor's economy after founder death.");
                int secure = (int)Field(game, "foodSecureTurns"); daughter.Food = 0; daughter.Settled = false; daughter.Salt = 1000;
                game.EndTurn(); Check((int)Field(game, "foodSecureTurns") == secure, "A dead founder cannot award free storage progress while the living leader is hungry.");
                daughter.Settled = true; daughter.HomeCell = daughter.CellId; Supply(game);
                int camps = (int)Field(game, "campTurns"); game.EndTurn();
                Check((int)Field(game, "campTurns") == camps + 1, "The living successor's hearth advances shared camp knowledge.");
                daughter.Population = 1; game.Beasts.Add(new Beast { Id = 998, Kind = BeastKind.Dragon, Count = 20, CellId = daughter.CellId });
                game.IssueBandCommand(daughter.Id, "attack-animal:998");
                Check(game.IsOver && game.TribeLeaderBand == null && game.TribePopulation == 0 && game.TribeEvents.Count(e => e.Kind == TribeEventKind.Extinction) == 1, "Only the final controlled band's destruction ends the tribe.");
                string before = Stamp(game); game.EndTurn(); game.IssueBandCommand(daughter.Id, "forage"); Check(before == Stamp(game), "A terminal tribe cannot spend or resolve another turn.");
            }
        }
        private static void Privacy()
        {
            Game game = Create(); Band daughter = Daughter(game); game.EndTurn();
            int destination = game.World.Cells.First(c => c.IsLand && c.Terrain != Terrain.Ice && !game.Explored.Contains(c.Id)).Id;
            daughter.CellId = destination; game.DiscoverPlaces(daughter.Id, destination);
            Check(game.Explored.Contains(destination) && game.KnownPlace(0, destination) == null && game.ObserverKnownPlace(destination) == game.KnownPlace(daughter.Id, destination), "Observer knowledge unions a daughter's discoveries without fabricating founder provenance.");
            string before = Stamp(game); string place = game.Place(destination); AutoplayDecision choice = AutoplayPolicy.Choose(game);
            game.ObserverKnownPlace(destination); EncounterRules.Outlook(game, daughter.Id, UnitKind.Animal, 456789);
            Check(Stamp(game) == before && place != "Uncharted place", "Observer name and actor preview reads do not mutate the world or random state.");
            foreach (Cell cell in game.World.Cells.Where(c => !game.Explored.Contains(c.Id))) { cell.Forage = 0; cell.Terrain = Terrain.Ice; }
            int hidden = game.World.Cells.First(c => !game.Explored.Contains(c.Id)).Id;
            game.Beasts.Add(new Beast { Id = 875, CellId = hidden, Kind = BeastKind.Dragon, Count = 1000 });
            Check(AutoplayPolicy.Choose(game).Command == choice.Command, "Unseen terrain and animals do not change actor autoplay choices.");
            before = Stamp(game); game.IssueBandCommand(daughter.Id, "attack-animal:875"); game.IssueBandCommand(daughter.Id, "move:" + hidden);
            Check(before == Stamp(game) && !EncounterRules.Outlook(game, daughter.Id, UnitKind.Animal, 875).CanAttack, "Hidden actor attack/movement commands reject without revealing or consuming state.");
            PlaceKnowledge remembered = game.ObserverKnownPlace(destination); daughter.Population = 0;
            Check(game.ObserverKnownPlace(destination) == remembered && game.Place(destination) == place, "The tribe retains names actually learned before the remembering household dies.");
        }
        private static void Autoplay()
        {
            int completed = 0, orders = 0, childOrders = 0;
            foreach (int seed in new[] { 73421, 0, -9137, 17 })
            {
                Game game = Create(seed), replay = Create(seed); Daughter(game); Daughter(replay);
                while (!game.IsOver && game.Turn <= 80 && orders < 10000)
                {
                    AutoplayDecision choice = AutoplayPolicy.Choose(game), repeated = AutoplayPolicy.Choose(game);
                    Check(choice != null && choice.Command == repeated.Command && choice.Reason == repeated.Reason, "Tribal autoplay choices are deterministic.");
                    int turn = game.Turn, effort = game.ControlledBands.Sum(b => game.ActionsFor(b.Id));
                    string before = orders % 53 == 0 ? Stamp(game) : null;
                    if (before != null) { AutoplayPolicy.Choose(game); Check(before == Stamp(game), "Choosing an actor order is read-only, including private state."); }
                    Execute(game, choice.Command); Execute(replay, choice.Command); orders++;
                    if (choice.Command.StartsWith("band:1:", StringComparison.Ordinal)) childOrders++;
                    Check(game.Turn > turn || game.ControlledBands.Sum(b => game.ActionsFor(b.Id)) < effort || game.IsOver, "Every autoplay step advances time or consumes a real household order.");
                    if (orders % 80 == 0) Check(Stamp(game) == Stamp(replay), "Actor orders replay tribal contacts, supplies and random streams exactly.");
                }
                Check(Stamp(game) == Stamp(replay), "A complete multi-band autoplay journey replays identically.");
                if (game.Turn > 80) completed++;
            }
            Check(completed >= 3 && childOrders > 20, "Autoplay sustains most trial tribes and genuinely commands their daughters.");
            PacingReport = "Tribe balance: " + completed + "/4 trials reached turn 81; " + orders + " total steps; " + childOrders + " orders to the first daughter.";
        }
        private static void Execute(Game game, string command)
        {
            if (command == "end") { game.EndTurn(); return; }
            Check(command.StartsWith("band:", StringComparison.Ordinal), "Every tribal action carries an explicit actor ID.");
            int end = command.IndexOf(':', 5); int id = Int32.Parse(command.Substring(5, end - 5), CultureInfo.InvariantCulture);
            game.IssueBandCommand(id, command.Substring(end + 1));
        }
        private static void Supply(Game game)
        { foreach (Band band in game.ControlledBands) { band.Food = Math.Max(band.Food, 10000); band.Salt = Math.Max(band.Salt, 10000); } }
        private static string Stamp(object value)
        {
            var buffer = new StringBuilder(); Append(buffer, value);
            using (SHA256 hash = SHA256.Create()) return BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(buffer.ToString())));
        }
        private static void Append(StringBuilder text, object value)
        {
            if (value == null) { text.Append("null;"); return; } Type type = value.GetType(); text.Append(type.FullName).Append(':');
            if (type.IsPrimitive || type.IsEnum || value is string) { text.Append(value is double ? ((double)value).ToString("R", CultureInfo.InvariantCulture) : Convert.ToString(value, CultureInfo.InvariantCulture)).Append(';'); return; }
            IDictionary dictionary = value as IDictionary;
            if (dictionary != null) { foreach (object key in dictionary.Keys.Cast<object>().OrderBy(k => Convert.ToString(k, CultureInfo.InvariantCulture), StringComparer.Ordinal)) { Append(text, key); Append(text, dictionary[key]); } return; }
            IEnumerable sequence = value as IEnumerable;
            if (sequence != null) { foreach (object item in sequence) Append(text, item); text.Append("end;"); return; }
            foreach (FieldInfo field in type.GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public).OrderBy(f => f.Name, StringComparer.Ordinal)) { text.Append(field.Name); Append(text, field.GetValue(value)); }
        }
    }
}
