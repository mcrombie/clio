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
    public static class UnitEncounterChecks
    {
        private static int assertions;
        public static string PacingReport { get; private set; }
        public static int Run()
        {
            assertions = 0;
            CheckUpgrade(); CheckRejectionsAndPurity(); CheckTargetedActions(); CheckTemperamentsAndCompanions();
            CheckBandCombat(); CheckImmediateRelease(); CheckEcologyAndReceipts(); CheckLongPlay(); return assertions;
        }
        private static Game NewGame(int seed)
        { return new Game(new GameSettings(seed, LanguageStyle.Flowing, Ancestry.Human, false, "Encounter hearth") { FoundingCulture = CultureTemplateId.Generated, Pace = HistoryPace.Generations, Rules = SimulationRules.MobileUnits }); }
        private static Beast Animal(Game game, int id, BeastKind kind, int count, int cell)
        { Beast result = new Beast { Id = id, CellId = cell, Kind = kind, Count = count }; game.Beasts.Add(result); return result; }
        private static Band People(Game game, int id, int count, int cell)
        {
            Band result = new Band { Id = id, CellId = cell, Population = count, Food = 210, Name = "Neighbor " + id, LanguageId = 0, Ancestry = Ancestry.Human };
            game.Bands.Add(result); return result;
        }
        private static int Adjacent(Game game)
        { return game.World.Cells[game.Player.CellId].Neighbors.First(n => game.World.Cells[n].IsLand && game.World.Cells[n].Terrain != Terrain.Ice); }
        private static void SetRandom(Game game, string name, uint state)
        { typeof(Game).GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).SetValue(game, state); }
        private static object Random(Game game, string name)
        { return typeof(Game).GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).GetValue(game); }

        private static void CheckUpgrade()
        {
            Game game = new Game(new GameSettings(73421, LanguageStyle.Flowing, Ancestry.Human, false, "Old hearth") { FoundingCulture = CultureTemplateId.EarlyEgyptian, Pace = HistoryPace.Generations });
            Check(game.Rules == SimulationRules.Classic, "The released seven-argument constructor retains Classic rules.");
            game.Forage(); game.Camp(); game.EndTurn();
            int turn = game.Turn, actions = game.Actions, population = game.Player.Population, cell = game.Player.CellId;
            double food = game.Player.Food;
            string[] chronicle = game.Chronicle.Select(e => e.Text).ToArray();
            object ecology = Random(game, "ecologyRandom"), action = Random(game, "actionRandom");
            game.EnableUnitEncounters();
            Check(game.Rules == SimulationRules.MobileUnits && game.Turn == turn && game.Actions == actions && game.Player.Population == population &&
                game.Player.Food == food && game.Player.CellId == cell, "Upgrading preserves the current community, date, location and remaining actions.");
            Check(ecology.Equals(Random(game, "ecologyRandom")) && action.Equals(Random(game, "actionRandom")), "Upgrading consumes no simulation randomness.");
            Check(game.Chronicle.Take(chronicle.Length).Select(e => e.Text).SequenceEqual(chronicle), "All earlier chronicle entries survive the upgrade.");
            Check(game.Encounters.Records.Count == 1 && game.Encounters.Records[0].Kind == EncounterKind.Enabled, "The upgrade has one explicit replayable record.");
            Reject(game, delegate { game.EnableUnitEncounters(); }, "Repeating the upgrade is idempotent.");
        }

        private static void CheckRejectionsAndPurity()
        {
            Game game = NewGame(73421); game.Beasts.Clear();
            Beast deer = Animal(game, 41, BeastKind.Deer, 10, game.Player.CellId);
            Reject(game, delegate { game.AttackAnimal(-1); game.BefriendAnimal(9999); game.AttackBand(-1); }, "Invalid IDs reject without changing state or random streams.");
            Reject(game, delegate { game.AttackBand(game.Player.Id); }, "The player's own band is protected.");
            deer.Domestic = true; deer.OwnerId = 0;
            Reject(game, delegate { game.AttackAnimal(deer.Id); game.BefriendAnimal(deer.Id); }, "Own companions cannot be attacked or re-tamed.");
            deer.OwnerId = 3;
            Reject(game, delegate { game.BefriendAnimal(deer.Id); }, "Other peoples' existing companions cannot be befriended away.");
            deer.Domestic = false; deer.OwnerId = -1; deer.Count = 0;
            Reject(game, delegate { game.AttackAnimal(deer.Id); game.BefriendAnimal(deer.Id); }, "Destroyed groups are invalid targets.");
            deer.Count = 10;
            int hidden = game.World.Cells.First(c => c.IsLand && !game.Explored.Contains(c.Id)).Id;
            deer.CellId = hidden;
            Reject(game, delegate { game.AttackAnimal(deer.Id); game.BefriendAnimal(deer.Id); }, "Unobserved groups cannot be targeted through known IDs.");
            EncounterOutlook unknown = EncounterRules.Outlook(game, UnitKind.Animal, deer.Id);
            Check(!unknown.CanAttack && !unknown.CanBefriend && unknown.TargetStrength == 0 && !unknown.RequiresMove, "An unknown group has no revealing strength or movement preview.");
            game.Explored.Add(hidden);
            Reject(game, delegate { game.AttackAnimal(deer.Id); game.BefriendAnimal(deer.Id); }, "Distant known groups remain out of one-action reach.");
            int ocean = game.World.Cells.First(c => !c.IsLand).Id; game.Explored.Add(ocean); deer.CellId = ocean;
            Reject(game, delegate { game.AttackAnimal(deer.Id); game.BefriendAnimal(deer.Id); }, "Land actions cannot reach groups on water.");
            deer.CellId = game.Player.CellId; game.Actions = 0;
            Reject(game, delegate { game.AttackAnimal(deer.Id); game.BefriendAnimal(deer.Id); }, "Exhausted priorities reject every targeted action.");
            game.Actions = 2; game.Player.Food = 0;
            Reject(game, delegate { game.BefriendAnimal(deer.Id); }, "An unaffordable encounter cannot take an offering or consume a random draw.");
            game.Player.Food = 500;
            string before = Snapshot(game);
            for (int i = 0; i < 8; i++)
            { EncounterRules.Animal(game, deer); EncounterRules.Band(game, game.Player); EncounterRules.Outlook(game, UnitKind.Animal, deer.Id); AutoplayPolicy.Choose(game); }
            Check(before == Snapshot(game), "Profiles, outlooks and autoplay planning create no hidden state, wounds or randomness.");
            game.Player.Population = 0;
            Reject(game, delegate { game.AttackAnimal(deer.Id); game.BefriendAnimal(deer.Id); game.AttackBand(3); game.EndTurn(); }, "A finished story cannot run another encounter.");
        }

        private static void CheckTargetedActions()
        {
            Game game = NewGame(73); game.Beasts.Clear();
            Beast unselected = Animal(game, 4, BeastKind.Wolves, 8, game.Player.CellId);
            Beast target = Animal(game, 8, BeastKind.Deer, 10, game.Player.CellId);
            int actions = game.Actions; double food = game.Player.Food;
            game.AttackAnimal(target.Id);
            EncounterRecord hit = game.Encounters.Records.Last(r => r.Kind == EncounterKind.Attack);
            Check(hit.TargetId == target.Id && target.Count < 10 && unselected.Count == 8 && unselected.PositiveContacts == 0, "The selected ID is attacked even when another animal is first in the cell list.");
            Check(hit.TargetName == "Deer group 8", "Encounter group names use the same actual unit ID as the map and inspector.");
            Check(game.Actions == actions - 1 && hit.DamageToTarget > 0 && hit.TargetCasualties == 10 - target.Count, "A targeted strike spends one action and records actual health damage and casualties.");
            Check(hit.FoodRecovered == (10 - target.Count) * 28 && game.Player.Food == food + hit.FoodRecovered, "Hunt production is backed by actual animal losses.");
            Reject(game, delegate { game.AttackAnimal(target.Id); game.BefriendAnimal(target.Id); }, "The same unit cannot be struck or approached repeatedly in one chapter.");

            Game approaching = NewGame(902); approaching.Beasts.Clear();
            int destination = Adjacent(approaching);
            Beast adjacent = Animal(approaching, 51, BeastKind.Deer, 12, destination);
            EncounterOutlook outlook = EncounterRules.Outlook(approaching, UnitKind.Animal, adjacent.Id);
            Check(outlook.RequiresMove && outlook.CanAttack && outlook.CanBefriend, "A reachable adjacent unit previews the combined approach action.");
            food = approaching.Player.Food; actions = approaching.Actions;
            approaching.AttackAnimal(adjacent.Id);
            Check(approaching.Player.CellId == destination && approaching.Actions == actions - 1, "Approach plus attack is exactly one priority.");
            Check(approaching.Encounters.Records.First().Kind == EncounterKind.Move &&
                Math.Abs(approaching.Encounters.Records.Sum(r => r.PlayerFoodDelta) - (approaching.Player.Food - food)) < 1e-9,
                "Approach and combat have separate, nonduplicated actual capacity receipts.");

            Game wounded = NewGame(113); wounded.Beasts.Clear();
            Beast mammoth = Animal(wounded, 9, BeastKind.Mammoths, 1, wounded.Player.CellId);
            wounded.AttackAnimal(mammoth.Id);
            UnitProfile first = EncounterRules.Animal(wounded, mammoth);
            Check(mammoth.Count == 1 && first.Wounds > 0 && first.CurrentHealth < first.MaxHealth, "A surviving large animal retains damage rather than resetting after a hit.");
            wounded.Turn++; wounded.Actions = 2;
            wounded.AttackAnimal(mammoth.Id);
            UnitProfile second = EncounterRules.Animal(wounded, mammoth);
            Check(second.CurrentHealth < first.CurrentHealth || mammoth.Count == 0, "A later targeted strike accumulates damage against that same moving ID.");

            Game guarded = NewGame(81); guarded.Beasts.Clear();
            int guardedCell = Adjacent(guarded); Animal(guarded, 10, BeastKind.Dragon, 1, guardedCell);
            Check(!guarded.CanMove(guardedCell), "Hostile occupancy is reflected by movement availability.");
            Reject(guarded, delegate { guarded.Move(guardedCell); }, "Ordinary movement does not silently enter a hostile group.");
            Check(EncounterRules.Outlook(guarded, UnitKind.Animal, 10).CanAttack, "An explicit risky attack can still approach guarded adjacent land.");
        }

        private static void CheckTemperamentsAndCompanions()
        {
            Game example = NewGame(63); example.Beasts.Clear();
            Beast deer = Animal(example, 1, BeastKind.Deer, 8, example.Player.CellId);
            Beast dragon = Animal(example, 2, BeastKind.Dragon, 1, example.Player.CellId);
            UnitProfile easy = EncounterRules.Animal(example, deer), difficult = EncounterRules.Animal(example, dragon);
            Check(easy.FriendChance > 0.7 && difficult.FriendChance < 0.02 && difficult.Hostile && difficult.TrustThreshold > easy.TrustThreshold * 5,
                "Skittish grazers and almost untamable hostile dragons present substantially different choices.");
            Check(difficult.Strength > EncounterRules.Band(example, example.Player).Strength * 2 && difficult.OfferingCost > easy.OfferingCost * 5,
                "A dragon's danger and offering cost are visible before committing.");
            foreach (BeastKind kind in Enum.GetValues(typeof(BeastKind)))
            {
                Game game = NewGame(17); game.Beasts.Clear(); game.Player.Food = 10000;
                Beast animal = Animal(game, 99, kind, kind == BeastKind.Dragon ? 1 : 4, Adjacent(game));
                int threshold = EncounterRules.Animal(game, animal).TrustThreshold;
                for (int contact = 0; contact < threshold; contact++)
                {
                    SetRandom(game, "actionRandom", 1); game.Player.Food = 10000;
                    game.BefriendAnimal(animal.Id);
                    Check(animal.PositiveContacts == contact + 1, "A successful selected contact advances the same group's trust exactly once.");
                    if (contact == 0) Reject(game, delegate { game.BefriendAnimal(animal.Id); }, "Contact cooldown is per selected group and chapter.");
                    if (contact + 1 < threshold) { game.Turn++; game.Actions = 2; }
                }
                Check(animal.Domestic && animal.OwnerId == 0 && !String.IsNullOrWhiteSpace(animal.BreedName), "Each species can establish its own named companion lineage under its own threshold.");
                Check(kind == BeastKind.Aurochs || !animal.BreedName.EndsWith(" cattle", StringComparison.Ordinal), "Non-cattle companionship does not acquire a cattle label.");
                DomesticEconomy economics = BandEconomy.DomesticEffects(game, game.Player);
                Check(kind == BeastKind.Wolves || economics.Dogs == 0, "Other companion species do not become dogs in the economy.");
                Check(BandEconomy.AnimalCare(game, animal) > 0, "Every living companion species has an explicit recurring care requirement.");
                if (kind == BeastKind.Deer || kind == BeastKind.Mammoths)
                    Check(economics.OtherCompanions == animal.Count && economics.GatheringMultiplier > 1, "Deer and mammoths supply real non-dog provisioning assistance.");
            }
            Game risk = NewGame(47); risk.Beasts.Clear(); risk.Player.Food = 1000;
            Beast hostile = Animal(risk, 7, BeastKind.Dragon, 1, risk.Player.CellId);
            uint state = 1;
            while (Draw(state) < 0.2 || Draw(NextState(state)) > 0.85) state++;
            SetRandom(risk, "actionRandom", state); risk.BefriendAnimal(hostile.Id);
            EncounterRecord rejected = risk.Encounters.Records.First(r => r.Kind == EncounterKind.Befriend);
            Check(rejected.Outcome == "attacked" && rejected.DamageToActor > 0 && rejected.ActorCasualties > 0 && rejected.TrustAfter == 0,
                "An unsuccessful hostile approach can cause real wounds and deaths instead of a harmless failed roll.");
        }

        private static void CheckBandCombat()
        {
            Game game = NewGame(59); game.Beasts.Clear();
            Band other = People(game, 1, 50, game.Player.CellId);
            int people = game.Bands.Sum(b => b.Population); double food = game.Bands.Sum(b => b.Food);
            Check(!EncounterRules.BandsHostile(game, 0, 1), "Neutral peoples can initially share a place.");
            game.AttackBand(other.Id);
            Check(EncounterRules.BandsHostile(game, 0, 1) && EncounterRules.BandsHostile(game, 1, 0), "Attacking a people creates persistent mutual hostility.");
            Check(game.Player.Population < 50 && other.Population < 50 && game.Bands.Sum(b => b.Population) < people,
                "Both sides can suffer real casualties in band combat.");
            Check(game.Player.Cohesion < 0.8 && other.Cohesion < 0.8 && game.Bands.Sum(b => b.Food) <= food, "Combat damages morale and does not invent resources.");
            Reject(game, delegate { game.AttackBand(other.Id); }, "A band target also enforces its encounter cooldown.");
            int record = game.Encounters.Records.Count;
            SetRandom(game, "ecologyRandom", 1); game.EndTurn();
            Check(game.Encounters.Records.Skip(record).Any(r => r.Kind == EncounterKind.Attack && r.ActorKind == UnitKind.Band && r.ActorId == other.Id),
                "An attacked independent band can retaliate through ordinary end-chapter AI.");

            Game looting = NewGame(83); looting.Beasts.Clear();
            Band doomed = People(looting, 1, 1, looting.Player.CellId); doomed.Food = 87.5;
            food = looting.Bands.Sum(b => b.Food); looting.AttackBand(doomed.Id);
            Check(doomed.Population == 0 && doomed.Food == 0 && looting.Bands.Sum(b => b.Food) == food,
                "A destroyed band can surrender only its actual remaining provisions; transfer conserves food.");
            Reject(looting, delegate { looting.AttackBand(doomed.Id); }, "A destroyed people cannot be attacked for repeated loot.");

            Game raiding = NewGame(21); raiding.Beasts.Clear();
            Band owner = People(raiding, 1, 50, raiding.Player.CellId);
            Beast herd = Animal(raiding, 12, BeastKind.Deer, 3, owner.CellId); herd.Domestic = true; herd.OwnerId = owner.Id; herd.BreedName = "Neighbor deer";
            Check(EncounterRules.Outlook(raiding, UnitKind.Animal, herd.Id).CanAttack, "A foreign household's animal unit can be explicitly attacked.");
            raiding.AttackAnimal(herd.Id);
            Check(herd.Count < 3 && EncounterRules.BandsHostile(raiding, 0, owner.Id), "A foreign herd raid causes actual animal losses and a feud with its owner.");
            raiding.Player.Food = owner.Food = 10000; raiding.EndTurn();
            Check(herd.Count == 0 || owner.Population == 0 || herd.CellId == owner.CellId,
                "A frightened foreign companion group rejoins its surviving household by the next chapter.");

            Game pets = NewGame(59); pets.Beasts.Clear();
            Band enemy = People(pets, 1, 50, pets.Player.CellId);
            Beast companion = Animal(pets, 25, BeastKind.Deer, 2, pets.Player.CellId); companion.Domestic = true; companion.OwnerId = 0; companion.BreedName = "Hearth deer";
            pets.AttackBand(enemy.Id); record = pets.Encounters.Records.Count;
            SetRandom(pets, "ecologyRandom", 1); pets.EndTurn();
            Check(pets.Encounters.Records.Skip(record).Any(r => r.Kind == EncounterKind.Attack && r.TargetKind == UnitKind.Animal && r.TargetId == companion.Id && r.TargetCasualties > 0 && r.PlayerInvolved),
                "A hostile band can raid an exposed owned animal group; typed records identify the real companion losses.");
        }

        private static void CheckImmediateRelease()
        {
            for (int method = 0; method < 3; method++)
            {
                Game game = NewGame(47); game.Beasts.Clear(); game.Player.Population = 1; game.Player.Food = 1000;
                Beast companion = Animal(game, 20, BeastKind.Deer, 3, game.Player.CellId);
                companion.Domestic = true; companion.OwnerId = 0; companion.BreedName = "Surviving hearth deer"; companion.PositiveContacts = 6;
                if (method == 2)
                {
                    Band enemy = People(game, 1, 50, game.Player.CellId); game.AttackBand(enemy.Id);
                }
                else
                {
                    Beast dragon = Animal(game, 7, BeastKind.Dragon, 1, game.Player.CellId);
                    if (method == 0) game.AttackAnimal(dragon.Id);
                    else
                    {
                        uint state = 1;
                        while (Draw(state) < 0.2 || Draw(NextState(state)) > 0.85) state++;
                        SetRandom(game, "actionRandom", state); game.BefriendAnimal(dragon.Id);
                    }
                }
                Check(game.IsOver && game.Turn == 1 && game.Actions == 1, "A lethal targeted action ends the band immediately without advancing the chapter.");
                Check(!companion.Domestic && companion.OwnerId == -1 && companion.Count == 3 && companion.PositiveContacts == 6,
                    "Every lethal player encounter immediately releases surviving companions while preserving their identity, population and remembered trust.");
                EncounterRecord release = game.Encounters.Records.Single(r => r.Kind == EncounterKind.Release && r.ActorId == companion.Id);
                EncounterRecord fatal = game.Encounters.Records.Last(r => r.Kind == EncounterKind.Attack || r.Kind == EncounterKind.Befriend);
                Check(release.Id > fatal.Id && release.ActorCountBefore == 3 && release.ActorCountAfter == 3 &&
                    release.ActorCasualties == 0 && release.TargetCasualties == 0 && release.PlayerFoodDelta == 0 && release.PlayerInvolved && release.VisibleToPlayer,
                    "A terminal release follows its causal encounter, identifies the player's household and duplicates no losses or capacity changes.");
                Reject(game, delegate { game.EndTurn(); game.AttackAnimal(7); game.BefriendAnimal(7); game.AttackBand(1); },
                    "Terminal input does not advance released companions or append duplicate release records.");
            }

            Game victorious = NewGame(83); victorious.Beasts.Clear();
            Band defender = People(victorious, 1, 1, victorious.Player.CellId);
            Beast foreign = Animal(victorious, 21, BeastKind.Deer, 3, defender.CellId);
            foreign.Domestic = true; foreign.OwnerId = defender.Id; foreign.BreedName = "Neighbor deer";
            double totalFood = victorious.Bands.Sum(b => b.Food); victorious.AttackBand(defender.Id);
            EncounterRecord released = victorious.Encounters.Records.Single(r => r.Kind == EncounterKind.Release);
            Check(defender.Population == 0 && !foreign.Domestic && foreign.OwnerId == -1 && foreign.Count == 3 && victorious.Bands.Sum(b => b.Food) == totalFood,
                "Killing a foreign household releases its surviving animals immediately without creating losses, ownership or extra loot.");
            Check(released.TargetId == defender.Id && released.VisibleToPlayer && !released.PlayerInvolved && released.TargetCasualties == 0,
                "A known foreign household's release is visible but does not falsely claim the player's companion losses.");
            victorious.EndTurn();
            Check(victorious.Encounters.Records.Count(r => r.Kind == EncounterKind.Release && r.ActorId == foreign.Id) == 1,
                "Later chapter resolution does not release an already independent group twice.");

            Game privateOwner = NewGame(17); privateOwner.Beasts.Clear();
            int hidden = privateOwner.World.Cells.First(c => c.IsLand && !privateOwner.Explored.Contains(c.Id)).Id;
            Band unseen = People(privateOwner, 2, 0, hidden); unseen.Name = "Unseen household";
            Beast visible = Animal(privateOwner, 22, BeastKind.Deer, 4, privateOwner.Player.CellId);
            visible.Domestic = true; visible.OwnerId = unseen.Id; visible.BreedName = "Stray deer";
            privateOwner.EndTurn();
            EncounterRecord redacted = privateOwner.Encounters.Records.Single(r => r.Kind == EncounterKind.Release);
            Check(redacted.VisibleToPlayer && !redacted.PlayerInvolved && redacted.TargetId == -1 &&
                !redacted.Detail.Contains(unseen.Name) && redacted.TargetName != unseen.Name,
                "A visible release does not reveal a dead household beyond the remembered land.");
        }

        private static void CheckEcologyAndReceipts()
        {
            Game roaming = NewGame(27); roaming.Beasts.Clear(); roaming.Player.Food = 10000;
            Beast herd = Animal(roaming, 11, BeastKind.Aurochs, 8, Adjacent(roaming)); herd.PositiveContacts = 2;
            for (int i = 0; i < 12; i++)
            { herd.LastContactTurn = roaming.Turn; roaming.Player.Food = 10000; roaming.EndTurn(); }
            Check(roaming.Encounters.Records.Count(r => r.ActorKind == UnitKind.Animal && r.ActorId == herd.Id && r.Kind == EncounterKind.Move) >= 2 && herd.PositiveContacts == 2,
                "Recently contacted wild groups keep moving between pastures without losing their group identity or trust.");
            foreach (EncounterRecord movement in roaming.Encounters.Records.Where(r => r.Kind == EncounterKind.Move || r.Kind == EncounterKind.Retreat))
                Check(roaming.World.Cells[movement.FromCell].Neighbors.Contains(movement.ToCell) && roaming.World.Cells[movement.ToCell].IsLand,
                    "Every recorded unit move follows adjacent land, without teleportation through oceans.");

            Game attacked = NewGame(55); attacked.Beasts.Clear(); attacked.Player.Population = 100; attacked.Player.Food = 1000;
            Beast dragon = Animal(attacked, 10, BeastKind.Dragon, 1, attacked.Player.CellId);
            SetRandom(attacked, "ecologyRandom", 1); attacked.EndTurn();
            EncounterRecord strike = attacked.Encounters.Records.First(r => r.Kind == EncounterKind.Attack);
            Check(strike.ActorKind == UnitKind.Animal && strike.ActorId == dragon.Id && strike.TargetId == 0 && strike.TargetCasualties > 0,
                "A dangerous predator can actively strike a nearby people during chapter resolution.");
            Check(attacked.Encounters.LastPlayerEconomy != null && attacked.Encounters.EconomyPlayerPopulation == 100 - strike.TargetCasualties &&
                attacked.Encounters.LastPlayerEconomy.EndingPopulation == attacked.Player.Population && attacked.Encounters.LastPlayerEconomy.EndingFood == attacked.Player.Food,
                "The actual economy receipt begins after combat and reconciles to the final community exactly.");
            Check(attacked.Encounters.LastEconomyTurn == attacked.Turn - 1, "The economy receipt identifies the chapter actually resolved.");

            Game ending = NewGame(55); ending.Beasts.Clear(); ending.Player.Population = 1; ending.Player.Food = 1000;
            Animal(ending, 10, BeastKind.Dragon, 1, ending.Player.CellId);
            Beast survivors = Animal(ending, 11, BeastKind.Deer, 3, ending.Player.CellId); survivors.Domestic = true; survivors.OwnerId = 0; survivors.BreedName = "Orphaned deer";
            SetRandom(ending, "ecologyRandom", 1); ending.EndTurn();
            Check(ending.IsOver && ending.Encounters.LastPlayerEconomy == null && ending.Encounters.EconomyPlayerPopulation == 0,
                "Death before the economy does not invent an upkeep or demographic tick.");
            Check(!survivors.Domestic && survivors.OwnerId == -1 && survivors.Count == 3 && ending.Encounters.Records.Any(r => r.Kind == EncounterKind.Release && r.ActorId == survivors.Id),
                "Surviving companions become independent when their household dies, with a typed release rather than fabricated animal deaths.");

            Game missingOwner = NewGame(17); missingOwner.Beasts.Clear();
            Beast orphan = Animal(missingOwner, 8, BeastKind.Deer, 4, Adjacent(missingOwner)); orphan.Domestic = true; orphan.OwnerId = 999; orphan.BreedName = "Stray herd";
            missingOwner.EndTurn(); int first = orphan.CellId;
            for (int i = 0; i < 8; i++) { missingOwner.Player.Food = 10000; missingOwner.EndTurn(); }
            Check(!orphan.Domestic && missingOwner.Encounters.Records.Any(r => r.ActorId == orphan.Id && (r.Kind == EncounterKind.Move || r.Kind == EncounterKind.Retreat)),
                "A missing owner's lineage rejoins mobile ecology rather than staying fixed forever.");
        }

        private static void CheckLongPlay()
        {
            StringBuilder report = new StringBuilder();
            foreach (int seed in new[] { Int32.MinValue, -37, 0, 73421, Int32.MaxValue })
            {
                Game game = NewGame(seed), replay = NewGame(seed); int attempts = 0;
                while (!game.IsOver && game.Turn <= 120)
                {
                    AutoplayDecision decision = AutoplayPolicy.Choose(game);
                    Check(decision.Command == AutoplayPolicy.Choose(replay).Command, "Identical mobile histories independently choose the same action.");
                    int turn = game.Turn, actions = game.Actions;
                    Execute(game, decision.Command); Execute(replay, decision.Command);
                    Check(decision.Command == "end" ? game.Turn == turn + 1 : game.Actions == actions - 1,
                        "Autoplay's selected commands make legal progress instead of looping on stale or unaffordable units.");
                    Check(game.Player.Population >= 0 && game.Player.Food >= 0 && game.Beasts.All(b => b.Count >= 0 && b.PositiveContacts >= 0),
                        "Combat, roaming and animal contact preserve nonnegative populations, food and trust.");
                    Check(++attempts <= 360, "A bounded observation needs no more than two actions and one end command per chapter.");
                    if (game.Turn % 30 == 0) Check(Snapshot(game) == Snapshot(replay), "Replay retains exact mobile positions, wounds, feuds, receipts, records and RNG streams.");
                }
                Check(game.IsOver || game.Turn == 121, "Autoplay reaches its observation horizon or an honest natural ending.");
                Check(!game.Encounters.Records.Any(r => r.Kind == EncounterKind.Attack && r.ActorKind == UnitKind.Band && r.ActorId == 0 && r.TargetKind == UnitKind.Band),
                    "An unprovoked observation does not start indiscriminate wars between neutral peoples.");
                report.Append("seed ").Append(seed).Append(": ").Append(HistoryTime.Label(game, game.Turn)).Append(", people ").Append(game.Player.Population)
                    .Append(", groups ").Append(game.Beasts.Count(b => b.Count > 0)).Append(", companions ").Append(game.Beasts.Count(b => b.Domestic && b.OwnerId == 0))
                    .Append(", attacks ").Append(game.Encounters.Records.Count(r => r.Kind == EncounterKind.Attack && r.PlayerInvolved))
                    .Append(", moves ").Append(game.Encounters.Records.Count(r => r.Kind == EncounterKind.Move && r.ActorKind == UnitKind.Animal))
                    .Append(", hunger ").Append(game.Chronicle.Count(c => c.Text.StartsWith("Hunger takes", StringComparison.Ordinal))).AppendLine();
            }
            PacingReport = report.ToString();
        }

        private static void Execute(Game game, string command)
        {
            int colon = command.IndexOf(':'); int id = colon < 0 ? -1 : Int32.Parse(command.Substring(colon + 1), CultureInfo.InvariantCulture);
            if (command.StartsWith("attack-animal:", StringComparison.Ordinal)) { game.AttackAnimal(id); return; }
            if (command.StartsWith("befriend-animal:", StringComparison.Ordinal)) { game.BefriendAnimal(id); return; }
            if (command.StartsWith("attack-band:", StringComparison.Ordinal)) { game.AttackBand(id); return; }
            if (command.StartsWith("move:", StringComparison.Ordinal)) { game.Move(id); return; }
            switch (command) { case "end": game.EndTurn(); break; case "forage": game.Forage(); break; case "camp": game.Camp(); break; case "split": game.Split(); break; case "hunt": game.Hunt(); break; case "tame": game.Tame(); break; default: throw new Exception(command); }
        }
        private static uint NextState(uint state)
        { state ^= state << 13; state ^= state >> 17; state ^= state << 5; return state == 0 ? 0x9e3779b9 : state; }
        private static double Draw(uint state) { return NextState(state) / 4294967296.0; }
        private static void Reject(Game game, Action command, string message)
        { string before = Snapshot(game); command(); Check(before == Snapshot(game), message); }
        private static string Snapshot(object value)
        {
            StringBuilder text = new StringBuilder(); Append(text, value);
            using (SHA256 hash = SHA256.Create()) return Convert.ToBase64String(hash.ComputeHash(Encoding.UTF8.GetBytes(text.ToString())));
        }
        private static void Append(StringBuilder text, object value)
        {
            if (value == null) { text.Append("null;"); return; }
            Type type = value.GetType(); text.Append(type.FullName).Append(':');
            if (type.IsPrimitive || type.IsEnum || value is string)
            { text.Append(Convert.ToString(value, CultureInfo.InvariantCulture)).Append(';'); return; }
            IDictionary dictionary = value as IDictionary;
            if (dictionary != null)
            {
                foreach (object key in dictionary.Keys.Cast<object>().OrderBy(k => Convert.ToString(k, CultureInfo.InvariantCulture), StringComparer.Ordinal))
                { Append(text, key); Append(text, dictionary[key]); } return;
            }
            IEnumerable sequence = value as IEnumerable;
            if (sequence != null)
            {
                List<string> items = new List<string>();
                foreach (object item in sequence) { StringBuilder entry = new StringBuilder(); Append(entry, item); items.Add(entry.ToString()); }
                if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(HashSet<>)) items.Sort(StringComparer.Ordinal);
                foreach (string item in items) text.Append(item); return;
            }
            foreach (FieldInfo field in type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).OrderBy(f => f.Name, StringComparer.Ordinal))
            { text.Append(field.Name).Append('='); Append(text, field.GetValue(value)); }
        }
        private static void Check(bool condition, string message)
        { if (!condition) throw new InvalidOperationException("Unit encounter check failed: " + message); assertions++; }
    }
}
