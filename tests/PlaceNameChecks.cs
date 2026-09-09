using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text;
using Clio.Simulation;

namespace Clio.Tests
{
    public static class PlaceNameChecks
    {
        private static int assertions;
        public static int Run()
        {
            assertions = 0;
            CheckDiscovery(); CheckIndependentNames(); CheckSharing(); CheckContactHooks();
            CheckDaughters(); CheckUpgrade(); CheckDeterminism();
            return assertions;
        }

        private static Game NewGame(bool enabled, bool four)
        { return new Game(73421, LanguageStyle.Flowing, Ancestry.Human, four, "Naming people", CultureTemplateId.Zhol, HistoryPace.Abstract, SimulationRules.MobileUnits, enabled); }

        private static HashSet<int> Sight(Game game, int center)
        {
            HashSet<int> result = new HashSet<int> { center };
            foreach (int near in game.World.Cells[center].Neighbors)
            { result.Add(near); foreach (int next in game.World.Cells[near].Neighbors) result.Add(next); }
            return result;
        }

        private static void CheckDiscovery()
        {
            Game game = NewGame(true, true);
            foreach (Band band in game.Bands)
            {
                HashSet<int> expected = Sight(game, band.CellId);
                Check(expected.SetEquals(game.KnownPlaces(band.Id).Select(p => p.CellId)), "Every founding people independently observes its own two rings.");
                Check(game.KnownPlaces(band.Id).Select(p => p.CellId).SequenceEqual(expected.OrderBy(id => id)), "Known places have stable cell order.");
                foreach (PlaceKnowledge place in game.KnownPlaces(band.Id))
                {
                    Check(place.Acquisition == PlaceAcquisition.Discovered && place.OriginBandId == band.Id && place.OriginLanguageId == band.LanguageId &&
                        place.LearnedFromBandId == -1 && place.LearnedTurn == 1, "Firsthand names record their own language, origin and discovery turn.");
                    Check(place.Name == game.Place(place.CellId, band.Id), "Place display reads recorded names.");
                }
            }
            Check(game.Explored.SetEquals(game.KnownPlaces(0).Select(p => p.CellId)), "Player fog matches the player's known places only.");
            int hidden = game.Bands[1].CellId;
            Check(game.KnownPlace(0, hidden) == null && game.Place(hidden) == "Uncharted place", "An unseen foreign hex has no player name even when another people knows it.");
            string before = NamingSnapshot(game); string mechanics = Mechanics(game);
            for (int i = 0; i < 4; i++)
            { game.Place(hidden); game.KnownPlaces(0); game.KnownPlace(1, hidden); game.Place(-1); }
            Check(before == NamingSnapshot(game) && mechanics == Mechanics(game), "Read-only map queries do not coin names, reveal terrain or consume randomness.");
            string[] originals = game.KnownPlaces(0).Select(p => p.Name).ToArray();
            game.DiscoverPlaces(0, game.Player.CellId);
            Check(originals.SequenceEqual(game.KnownPlaces(0).Select(p => p.Name)), "Repeated observation never renames a place.");
        }

        private static void CheckIndependentNames()
        {
            Game game = NewGame(true, true); Band other = game.Bands[1];
            int destination = game.World.Cells.First(c => c.IsLand && game.KnownPlace(0, c.Id) == null && game.KnownPlace(other.Id, c.Id) == null).Id;
            other.LanguageId = game.Player.LanguageId;
            game.DiscoverPlaces(other.Id, destination);
            PlaceKnowledge earlier = game.KnownPlace(other.Id, destination);
            game.Turn = 7; game.DiscoverPlaces(0, destination);
            PlaceKnowledge later = game.KnownPlace(0, destination);
            Check(earlier.Name != later.Name && later.OriginBandId == 0 && later.Acquisition == PlaceAcquisition.Discovered,
                "Independent later discovery coins its own term even when another band uses the same language.");
            Check(later.LearnedTurn == 7 && game.KnownPlace(other.Id, destination) == earlier, "Other peoples' discoveries never rewrite existing coinages.");
            LanguageProfile speech = game.Languages[0];
            foreach (Cell cell in game.World.Cells) game.DiscoverPlaces(0, cell.Id);
            PlaceKnowledge[] all = game.KnownPlaces(0).ToArray();
            Check(all.Length == game.World.Cells.Length && all.Select(p => p.Name).Distinct(StringComparer.Ordinal).Count() == all.Length,
                "Every hex, including neighboring terrain in the same region, receives a distinct locally coined name.");
            foreach (PlaceKnowledge place in all)
            {
                string[] parts = place.Name.ToLowerInvariant().Split('-');
                Check(parts.Length == 2 && speech.Words.Values.Contains(parts[0]), "Each Zhol name retains a native semantic root.");
                Check(parts[1].Length >= 4 && parts[1].Select((c, i) => i % 2 == 0 ? speech.Settings.Onsets.Contains(c) : speech.Settings.Vowels.Contains(c)).All(valid => valid),
                    "Local epithets use the current language's sound inventory rather than tile numbers.");
            }
        }

        private static void CheckSharing()
        {
            Game game = NewGame(true, true); Band donor = game.Bands[1]; int remote = donor.CellId;
            PlaceKnowledge source = game.KnownPlace(donor.Id, remote), local = game.KnownPlace(0, game.Player.CellId);
            string before = NamingSnapshot(game);
            Check(game.SharePlaceKnowledge(0, donor.Id) == 0 && NamingSnapshot(game) == before, "Distant peoples cannot exchange maps.");
            donor.CellId = game.World.Cells[game.Player.CellId].Neighbors.First(n => game.World.Cells[n].IsLand);
            game.Turn = 4; int learned = game.SharePlaceKnowledge(0, donor.Id);
            PlaceKnowledge adopted = game.KnownPlace(0, remote);
            Check(learned > 0 && adopted.Name == source.Name && adopted.OriginBandId == donor.Id && adopted.OriginLanguageId == source.OriginLanguageId,
                "Peaceful contact adopts the exact foreign term and original provenance.");
            Check(adopted.LearnedFromBandId == donor.Id && adopted.LearnedTurn == 4 && adopted.Acquisition == PlaceAcquisition.Shared && game.Explored.Contains(remote),
                "Borrowed place knowledge records its immediate guide and reveals that hex.");
            Check(game.KnownPlace(0, local.CellId) == local, "Sharing cannot overwrite an independently known local term.");
            game.DiscoverPlaces(0, remote);
            Check(game.KnownPlace(0, remote) == adopted, "Later firsthand arrival preserves the already adopted foreign name and provenance.");
            before = NamingSnapshot(game); int history = game.Chronicle.Count;
            game.SharePlaceKnowledge(0, donor.Id);
            int afterFirst = game.Chronicle.Count; string after = NamingSnapshot(game);
            Check(game.SharePlaceKnowledge(0, donor.Id) == 0 && game.Chronicle.Count == afterFirst && NamingSnapshot(game) == after,
                "Repeated contact without new information does not duplicate names or notifications.");

            Game relay = NewGame(true, true); Band origin = relay.Bands[1], guide = relay.Bands[2]; int far = origin.CellId;
            PlaceKnowledge coined = relay.KnownPlace(origin.Id, far);
            origin.CellId = guide.CellId; relay.SharePlaceKnowledge(origin.Id, guide.Id);
            guide.CellId = relay.Player.CellId; relay.SharePlaceKnowledge(guide.Id, 0);
            PlaceKnowledge relayed = relay.KnownPlace(0, far);
            Check(relayed.Name == coined.Name && relayed.OriginBandId == origin.Id && relayed.OriginLanguageId == coined.OriginLanguageId && relayed.LearnedFromBandId == guide.Id,
                "A relay preserves the original foreign name while recording the actual intermediary.");
        }

        private static void CheckContactHooks()
        {
            foreach (bool mobile in new[] { false, true })
            {
                Game game = new Game(73421, LanguageStyle.Flowing, Ancestry.Human, true, "", CultureTemplateId.Zhol,
                    HistoryPace.Abstract, mobile ? SimulationRules.MobileUnits : SimulationRules.Classic, true);
                game.Beasts.Clear(); Band donor = game.Bands[1]; int remote = donor.CellId;
                int destination = game.World.Cells[game.Player.CellId].Neighbors.First(n => game.World.Cells[n].IsLand);
                donor.CellId = destination; game.Move(destination);
                Check(game.KnownPlace(0, remote) != null && game.KnownPlace(0, remote).Acquisition == PlaceAcquisition.Shared,
                    "Ordinary movement into peaceful contact exchanges maps in both supported movement rules.");
            }
            Game waiting = NewGame(true, true); waiting.Beasts.Clear(); int oldHome = waiting.Bands[1].CellId;
            waiting.Bands[1].CellId = waiting.World.Cells[waiting.Player.CellId].Neighbors.First(n => waiting.World.Cells[n].IsLand);
            waiting.EndTurn();
            Check(waiting.KnownPlace(0, oldHome) != null, "Ending a turn in peaceful adjacency exchanges knowledge even without a player move.");

            Game fighting = NewGame(true, true); fighting.Beasts.Clear(); Band enemy = fighting.Bands[1]; int enemyHome = enemy.CellId;
            enemy.CellId = fighting.World.Cells[fighting.Player.CellId].Neighbors.First(n => fighting.World.Cells[n].IsLand);
            fighting.AttackBand(enemy.Id);
            Check(EncounterRules.BandsHostile(fighting, 0, enemy.Id) && fighting.KnownPlace(0, enemyHome) == null,
                "An attacking approach cannot borrow the defender's map before hostility is established.");
            string snapshot = NamingSnapshot(fighting);
            Check(fighting.SharePlaceKnowledge(0, enemy.Id) == 0 && NamingSnapshot(fighting) == snapshot, "Hostile bands do not share knowledge.");
            enemy.Population = 0;
            Check(fighting.SharePlaceKnowledge(0, enemy.Id) == 0, "A people with no survivors cannot act as a guide.");

            Game raiding = NewGame(true, true); raiding.Beasts.Clear(); Band owner = raiding.Bands[1], bystander = raiding.Bands[2];
            int ownerHome = owner.CellId, bystanderHome = bystander.CellId;
            int approach = raiding.World.Cells[raiding.Player.CellId].Neighbors.First(n => raiding.World.Cells[n].IsLand);
            owner.CellId = bystander.CellId = approach;
            Beast companion = new Beast { Id = 9009, Kind = BeastKind.Aurochs, Count = 8, CellId = approach,
                Domestic = true, OwnerId = owner.Id, BreedName = "The owner's herd" };
            raiding.Beasts.Add(companion);
            Check(!EncounterRules.BandsHostile(raiding, 0, owner.Id) && raiding.KnownPlace(0, ownerHome) == null,
                "The foreign companion's owner begins neutral and has an unknown remembered home.");
            raiding.AttackAnimal(companion.Id);
            Check(raiding.Actions == 1 && EncounterRules.BandsHostile(raiding, 0, owner.Id) && raiding.KnownPlace(0, ownerHome) == null,
                "Attacking a neutral people's companion establishes hostility without first borrowing its owner's map.");
            Check(raiding.KnownPlace(0, bystanderHome) == null,
                "An animal attack approach does not exchange maps with a neutral bystander either.");
        }

        private static void CheckDaughters()
        {
            Game game = NewGame(true, true); game.Beasts.Clear(); Band donor = game.Bands[1]; int remote = donor.CellId;
            donor.CellId = game.Player.CellId; game.SharePlaceKnowledge(0, donor.Id);
            PlaceKnowledge[] parent = game.KnownPlaces(0).ToArray();
            game.Player.Population = 120; game.Player.Food = 1500; game.Split();
            Band daughter = game.Bands.Last();
            Check(daughter.Id == 4 && daughter.LanguageId == game.Player.LanguageId, "The daughter still begins in the parent's tongue.");
            foreach (PlaceKnowledge known in parent)
            {
                PlaceKnowledge inherited = game.KnownPlace(daughter.Id, known.CellId);
                Check(inherited != null && inherited.Name == known.Name && inherited.OriginBandId == known.OriginBandId && inherited.OriginLanguageId == known.OriginLanguageId &&
                    inherited.LearnedFromBandId == 0 && inherited.Acquisition == PlaceAcquisition.Inherited, "A daughter inherits exact remembered terms, including foreign origins.");
            }
            Check(game.KnownPlace(daughter.Id, remote).OriginBandId == donor.Id, "Inheritance preserves a borrowed term's original people.");
            Check(Sight(game, daughter.CellId).All(c => game.KnownPlace(daughter.Id, c) != null), "A daughter also observes the neighborhood of her own new position.");
            string saved = game.KnownPlace(daughter.Id, remote).Name;
            LanguageProfile branch = LanguageGenerator.Branch(game.Languages[0], game.Languages.Count, 912);
            game.Languages.Add(branch); daughter.LanguageId = branch.Id;
            game.DiscoverPlaces(daughter.Id, remote);
            Check(game.KnownPlace(daughter.Id, remote).Name == saved, "A later change of speech does not silently rename remembered places.");
        }

        private static void CheckUpgrade()
        {
            Game game = NewGame(false, true); game.Beasts.Clear(); game.Player.Population = 120; game.Player.Food = 1500; game.Split();
            Check(!game.CulturalPlaceNames && game.KnownPlaces(0).Count == 0, "Older constructor rules keep legacy names and carry no new naming state.");
            game.Bands[1].CellId = game.Player.CellId;
            HashSet<int> explored = new HashSet<int>(game.Explored); string before = Mechanics(game); int history = game.Chronicle.Count;
            game.EnableCulturalPlaceNames();
            Check(game.CulturalPlaceNames && game.Explored.SetEquals(explored) && game.KnownPlaces(0).Count == explored.Count,
                "Legacy upgrade names exactly the player's existing knowledge without extra revelation from nearby NPCs.");
            Check(Mechanics(game) == before && game.Chronicle.Count == history, "Upgrade preserves gameplay, RNG and all previous history.");
            Check(game.KnownPlaces(game.Bands.Last().Id).Count >= explored.Count, "Already existing daughters inherit the parent's remembered map during upgrade.");
            string names = NamingSnapshot(game); game.EnableCulturalPlaceNames();
            Check(names == NamingSnapshot(game) && before == Mechanics(game), "Repeated upgrade is a complete no-op.");
        }

        private static void CheckDeterminism()
        {
            Game game = NewGame(true, true), replay = NewGame(true, true), legacy = NewGame(false, true);
            Check(Mechanics(game) == Mechanics(legacy), "Opt-in naming alone does not consume gameplay RNG or change the founding mechanics.");
            for (int step = 0; step < 36 && !game.IsOver; step++)
            {
                game.Forage(); replay.Forage();
                if (step % 3 == 0)
                {
                    int target = game.World.Cells[game.Player.CellId].Neighbors.Where(n => game.CanMove(n)).OrderBy(n => n).DefaultIfEmpty(-1).First();
                    if (target >= 0) { game.Move(target); replay.Move(target); }
                }
                game.EndTurn(); replay.EndTurn();
                Check(Mechanics(game) == Mechanics(replay) && NamingSnapshot(game) == NamingSnapshot(replay), "Command replay reproduces discoveries, stable names, sharing, provenance and RNG state.");
            }
        }

        private static string Mechanics(Game game)
        {
            object ecology = typeof(Game).GetField("ecologyRandom", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(game);
            object action = typeof(Game).GetField("actionRandom", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(game);
            return HistoricalCultureChecks.Snapshot(new object[] { game.Turn, game.Actions, game.Bands, game.Beasts,
                game.Languages, game.Depletion, game.Knowledge, ecology, action }, false);
        }

        public static string NamingSnapshot(Game game)
        {
            StringBuilder result = new StringBuilder(); result.Append(game.CulturalPlaceNames);
            foreach (Band band in game.Bands.OrderBy(b => b.Id))
            foreach (PlaceKnowledge place in game.KnownPlaces(band.Id))
                result.Append('|').Append(band.Id).Append(':').Append(place.CellId).Append(':').Append(place.Name).Append(':')
                    .Append(place.OriginBandId).Append(':').Append(place.OriginLanguageId).Append(':').Append(place.LearnedFromBandId)
                    .Append(':').Append(place.LearnedTurn).Append(':').Append(place.Acquisition);
            foreach (int cell in game.Explored.OrderBy(c => c)) result.Append('/').Append(cell);
            return result.ToString();
        }

        private static void Check(bool condition, string message)
        { assertions++; if (!condition) throw new Exception("Place-name check failed: " + message); }
    }
}
