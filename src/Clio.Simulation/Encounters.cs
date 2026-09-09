using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;

namespace Clio.Simulation
{
    public enum SimulationRules { Classic, MobileUnits }
    public enum UnitKind { Band, Animal }
    public enum EncounterKind { Enabled, Move, Attack, Befriend, Retreat, Recovery, Release }

    public sealed class UnitProfile
    {
        public readonly UnitKind Kind;
        public readonly int Id, CellId, Count, Wounds, CurrentHealth, MaxHealth, TrustThreshold;
        public readonly double Strength, FriendChance, OfferingCost;
        public readonly bool Hostile;
        public readonly string Temperament, Description;
        internal UnitProfile(UnitKind kind, int id, int cell, int count, int wounds, int health, double strength,
            bool hostile, string temperament, double chance, int trust, double cost, string description)
        {
            Kind = kind; Id = id; CellId = cell; Count = count; Wounds = wounds; MaxHealth = count * health;
            CurrentHealth = Math.Max(0, MaxHealth - wounds); Strength = strength; Hostile = hostile;
            Temperament = temperament; FriendChance = chance; TrustThreshold = trust; OfferingCost = cost; Description = description;
        }
    }

    public sealed class EncounterOutlook
    {
        public readonly int ActionCost;
        public readonly bool CanAttack, CanBefriend, RequiresMove;
        public readonly string AttackReason, BefriendReason, Summary;
        public readonly double PlayerStrength, TargetStrength, ExpectedDamage, ExpectedRetaliation;
        internal EncounterOutlook(bool attack, bool befriend, bool move, string attackReason, string friendReason,
            double player, double target, double damage, double retaliation, int actionCost)
        {
            CanAttack = attack; CanBefriend = befriend; RequiresMove = move; AttackReason = attackReason; BefriendReason = friendReason;
            PlayerStrength = player; TargetStrength = target; ExpectedDamage = damage; ExpectedRetaliation = retaliation;
            ActionCost = actionCost;
            Summary = target > player * 1.65 ? "Outmatched: severe losses are possible" : target > player * 1.05 ?
                "Dangerous: the target can fight back strongly" : target > player * 0.55 ? "Contested: expect wounds and possible losses" : "Favorable, with some risk";
        }
    }

    internal sealed class UnitCondition
    {
        internal int Wounds, LastEncounterTurn = -1, LastAttackTurn = -1, HostileUntil = -1;
    }

    internal sealed class UnitFrame
    {
        internal UnitKind Kind;
        internal int Id, Cell, Count, Wounds, Health;
        internal double Food;
        internal string Name;
        internal BeastKind? AnimalKind;
    }

    public sealed class EncounterRecord
    {
        public readonly int Id, Turn, ActorId, TargetId, FromCell, ToCell, CellId;
        public readonly EncounterKind Kind;
        public readonly UnitKind ActorKind, TargetKind;
        public readonly string ActorName, TargetName, Outcome, Title, Detail;
        public readonly BeastKind? AnimalKind;
        public readonly bool VisibleToPlayer, PlayerInvolved;
        public readonly int ActorCountBefore, ActorCountAfter, TargetCountBefore, TargetCountAfter;
        public readonly int ActorWoundsBefore, ActorWoundsAfter, TargetWoundsBefore, TargetWoundsAfter;
        public readonly int DamageToActor, DamageToTarget, ActorCasualties, TargetCasualties, TrustBefore, TrustAfter, TrustThreshold;
        public readonly double PlayerFoodDelta, FoodRecovered, OfferingPaid, TravelPaid, FriendChance;
        public readonly double ActorFoodDelta, TargetFoodDelta;
        internal EncounterRecord(int id, int turn, EncounterKind kind, UnitFrame actor, UnitFrame actorAfter,
            UnitFrame target, UnitFrame targetAfter, bool visible, bool involved, double foodDelta, double recovered, double offered,
            double travel, int trustBefore, int trustAfter, int threshold, double chance, string outcome, string title, string detail)
        {
            Id = id; Turn = turn; Kind = kind; ActorKind = actor.Kind; ActorId = actor.Id; ActorName = actor.Name;
            TargetKind = target == null ? UnitKind.Animal : target.Kind; TargetId = target == null ? -1 : target.Id; TargetName = target == null ? "" : target.Name;
            FromCell = actor.Cell; ToCell = actorAfter.Cell; CellId = target == null ? actorAfter.Cell : target.Cell;
            AnimalKind = actor.AnimalKind ?? (target == null ? null : target.AnimalKind);
            VisibleToPlayer = visible; PlayerInvolved = involved;
            ActorCountBefore = actor.Count; ActorCountAfter = actorAfter.Count; ActorWoundsBefore = actor.Wounds; ActorWoundsAfter = actorAfter.Wounds;
            TargetCountBefore = target == null ? 0 : target.Count; TargetCountAfter = targetAfter == null ? 0 : targetAfter.Count;
            TargetWoundsBefore = target == null ? 0 : target.Wounds; TargetWoundsAfter = targetAfter == null ? 0 : targetAfter.Wounds;
            ActorCasualties = Math.Max(0, ActorCountBefore - ActorCountAfter); TargetCasualties = Math.Max(0, TargetCountBefore - TargetCountAfter);
            DamageToActor = Math.Max(0, (actor.Count - actorAfter.Count) * actor.Health + actorAfter.Wounds - actor.Wounds);
            DamageToTarget = target == null ? 0 : Math.Max(0, (target.Count - targetAfter.Count) * target.Health + targetAfter.Wounds - target.Wounds);
            ActorFoodDelta = actorAfter.Food - actor.Food; TargetFoodDelta = target == null ? 0 : targetAfter.Food - target.Food;
            PlayerFoodDelta = foodDelta; FoodRecovered = recovered; OfferingPaid = offered; TravelPaid = travel;
            TrustBefore = trustBefore; TrustAfter = trustAfter; TrustThreshold = threshold; FriendChance = chance;
            Outcome = outcome; Title = title; Detail = detail;
        }
    }

    public sealed class EncounterState
    {
        internal bool Enabled;
        internal readonly Dictionary<int, UnitCondition> BandConditions = new Dictionary<int, UnitCondition>();
        internal readonly Dictionary<int, UnitCondition> AnimalConditions = new Dictionary<int, UnitCondition>();
        internal readonly HashSet<long> Wars = new HashSet<long>();
        internal readonly List<EncounterRecord> History = new List<EncounterRecord>();
        public ReadOnlyCollection<EncounterRecord> Records { get { return History.AsReadOnly(); } }
        public EconomyForecast LastPlayerEconomy { get; internal set; }
        public int LastEconomyTurn { get; internal set; }
        public int EconomyPlayerPopulation { get; internal set; }
        public double EconomyPlayerFood { get; internal set; }
        internal UnitCondition Read(UnitKind kind, int id)
        { UnitCondition found; return (kind == UnitKind.Band ? BandConditions : AnimalConditions).TryGetValue(id, out found) ? found : null; }
        internal UnitCondition Write(UnitKind kind, int id)
        {
            Dictionary<int, UnitCondition> states = kind == UnitKind.Band ? BandConditions : AnimalConditions;
            UnitCondition found; if (!states.TryGetValue(id, out found)) { found = new UnitCondition(); states.Add(id, found); } return found;
        }
        internal static long Pair(int a, int b) { return ((long)Math.Min(a, b) << 32) | (uint)Math.Max(a, b); }
    }

    public static class EncounterRules
    {
        public static int HealthPerAnimal(BeastKind kind)
        { return kind == BeastKind.Dragon ? 320 : kind == BeastKind.Mammoths ? 75 : kind == BeastKind.Aurochs ? 28 : kind == BeastKind.Wolves ? 14 : 8; }
        public static bool BandsHostile(Game game, int first, int second)
        {
            if (first == second || game.Rules != SimulationRules.MobileUnits) return false;
            if (!game.TribesEnabled) return game.Encounters.Wars.Contains(EncounterState.Pair(first, second));
            int one = game.TribeOf(first), two = game.TribeOf(second); if (one == two) return false;
            return game.Bands.Where(b => game.TribeOf(b.Id) == one).Any(a => game.Bands.Where(b => game.TribeOf(b.Id) == two)
                .Any(b => game.Encounters.Wars.Contains(EncounterState.Pair(a.Id, b.Id))));
        }
        public static UnitProfile Band(Game game, Band band)
        {
            UnitCondition state = game.Encounters.Read(UnitKind.Band, band.Id);
            int wounds = state == null ? 0 : Math.Min(state.Wounds, Math.Max(0, band.Population * 10));
            double vigor = band.Population <= 0 ? 0 : Math.Sqrt(Math.Max(0, 1 - wounds / (band.Population * 10.0)));
            double companions = game.Rules == SimulationRules.MobileUnits ? game.Beasts.Where(b => b.Domestic && b.OwnerId == band.Id && b.Count > 0)
                .Sum(b => b.Kind == BeastKind.Wolves ? Math.Min(b.Count, 16) * 0.45 : b.Kind == BeastKind.Mammoths ? Math.Min(b.Count, 4) * 3 : b.Kind == BeastKind.Dragon ? Math.Min(b.Count, 1) * 35 : 0) : 0;
            double strength = (band.Population * 0.8 * (0.55 + band.Cohesion * 0.45) + companions) * vigor;
            bool hostile = BandsHostile(game, band.Id, game.Player.Id);
            return new UnitProfile(UnitKind.Band, band.Id, band.CellId, band.Population, wounds, 10, strength, hostile,
                hostile ? "Hostile" : "Neutral", 0, 0, 0, "People can share neutral ground. Attacking creates lasting hostility; wounds weaken a band until it recovers.");
        }
        public static UnitProfile Animal(Game game, Beast animal)
        {
            UnitCondition state = game.Encounters.Read(UnitKind.Animal, animal.Id);
            int health = HealthPerAnimal(animal.Kind), wounds = state == null ? 0 : Math.Min(state.Wounds, Math.Max(0, animal.Count * health));
            uint variation = unchecked((uint)game.Seed * 2654435761u + (uint)animal.Id * 2246822519u);
            bool naturalHostility = animal.Kind == BeastKind.Dragon || animal.Kind == BeastKind.Wolves && variation % 5 == 0 || animal.Kind == BeastKind.Aurochs && variation % 11 == 0;
            bool hostile = animal.Count > 0 && (animal.Domestic ? BandsHostile(game, animal.OwnerId, game.Player.Id) :
                state != null && state.HostileUntil >= game.Turn || naturalHostility && animal.PositiveContacts < 3);
            double power = animal.Kind == BeastKind.Dragon ? 95 : animal.Kind == BeastKind.Mammoths ? 5 : animal.Kind == BeastKind.Aurochs ? 2.2 : animal.Kind == BeastKind.Wolves ? 2.1 : 0.65;
            double vigor = animal.Count <= 0 ? 0 : Math.Sqrt(Math.Max(0, 1 - wounds / (animal.Count * (double)health)));
            double chance = animal.Kind == BeastKind.Dragon ? 0.006 : animal.Kind == BeastKind.Mammoths ? 0.28 : animal.Kind == BeastKind.Aurochs ? 0.56 : animal.Kind == BeastKind.Wolves ? 0.66 : 0.78;
            int threshold = animal.Kind == BeastKind.Dragon ? 40 : animal.Kind == BeastKind.Mammoths ? 18 : animal.Kind == BeastKind.Aurochs ? 12 : animal.Kind == BeastKind.Wolves ? 10 : 6;
            double cost = animal.Kind == BeastKind.Dragon ? 90 : animal.Kind == BeastKind.Mammoths ? 42 : animal.Kind == BeastKind.Aurochs ? 24 : animal.Kind == BeastKind.Wolves ? 18 : 12;
            chance = animal.Kind == BeastKind.Dragon ? Math.Min(0.012, chance + animal.PositiveContacts * 0.00015) : Math.Min(0.90, chance + animal.PositiveContacts * 0.008);
            if (hostile && animal.Kind != BeastKind.Dragon) chance *= 0.35;
            string temperament = animal.Domestic ? "Companion" : hostile ? "Hostile" : animal.Kind == BeastKind.Deer ? "Skittish" : animal.Kind == BeastKind.Wolves ? "Wary" : animal.Kind == BeastKind.Aurochs ? "Protective" : "Defensive";
            string description = animal.Kind == BeastKind.Dragon ? "A dangerous territorial creature. Peaceful bonds are extraordinarily rare; rejected approaches can be lethal." :
                animal.Kind == BeastKind.Mammoths ? "Strong family herds resist threats. Patient contact is costly; a charge can devastate a small band." :
                animal.Kind == BeastKind.Aurochs ? "Powerful grazing herds protect their own. They wander between pastures and may charge when threatened." :
                animal.Kind == BeastKind.Wolves ? "Packs range through the land. Wary packs can learn trust; hostile predators may pursue a vulnerable people." :
                "Quick and cautious grazers. They are easier to approach peacefully, but tend to flee people and attackers.";
            return new UnitProfile(UnitKind.Animal, animal.Id, animal.CellId, animal.Count, wounds, health, animal.Count * power * vigor,
                hostile, temperament, animal.Domestic ? 0 : chance, threshold, cost, description);
        }
        public static bool HostileAt(Game game, int cell, int bandId)
        {
            if (game.Rules != SimulationRules.MobileUnits) return false;
            return game.Beasts.Any(b => b.CellId == cell && b.Count > 0 && (b.Domestic ? BandsHostile(game, b.OwnerId, bandId) : Animal(game, b).Hostile)) ||
                game.Bands.Any(b => b.CellId == cell && b.Population > 0 && BandsHostile(game, b.Id, bandId));
        }
        public static EncounterOutlook Outlook(Game game, UnitKind kind, int id)
        { return Outlook(game, game.Player.Id, kind, id); }
        public static EncounterOutlook Outlook(Game game, int actorId, UnitKind kind, int id)
        {
            Band acting = game.Bands.Find(b => b.Id == actorId);
            string attackReason = Validate(game, actorId, kind, id, false), friendReason = Validate(game, actorId, kind, id, true);
            Beast animal = kind == UnitKind.Animal ? game.Beasts.Find(b => b.Id == id) : null;
            Band band = kind == UnitKind.Band ? game.Bands.Find(b => b.Id == id) : null;
            int cell = animal != null ? animal.CellId : band != null ? band.CellId : -1;
            bool visible = cell >= 0 && game.Explored.Contains(cell);
            UnitProfile target = !visible ? null : animal != null ? Animal(game, animal) : band != null ? Band(game, band) : null;
            double player = acting == null ? 0 : Band(game, acting).Strength, strength = target == null ? 0 : target.Strength;
            double retaliation = animal != null && animal.Kind == BeastKind.Deer ? 0.08 : 0.8;
            return new EncounterOutlook(attackReason.Length == 0, friendReason.Length == 0, visible && acting != null && cell != acting.CellId,
                attackReason, friendReason, player, strength, player * 0.42, strength * 0.42 * retaliation,
                visible && acting != null ? TravelRules.EncounterCost(game, acting, cell) : 0);
        }
        internal static string Validate(Game game, UnitKind kind, int id, bool friendly)
        { return Validate(game, game.ActionBand.Id, kind, id, friendly); }
        internal static string Validate(Game game, int actorId, UnitKind kind, int id, bool friendly)
        {
            Band acting = game.Bands.Find(b => b.Id == actorId);
            if (acting == null || !game.CanControlBand(actorId)) return "Choose a living band within your people.";
            if (game.Rules != SimulationRules.MobileUnits) return "Enable moving encounters to use targeted actions.";
            if (game.IsOver) return "This band's story has ended.";
            if (game.ActionsFor(actorId) <= 0) return "No priorities remain. End the chapter before another encounter.";
            Beast animal = kind == UnitKind.Animal ? game.Beasts.Find(b => b.Id == id) : null;
            Band band = kind == UnitKind.Band ? game.Bands.Find(b => b.Id == id) : null;
            int count = animal != null ? animal.Count : band != null ? band.Population : 0;
            int cell = animal != null ? animal.CellId : band != null ? band.CellId : -1;
            if (count <= 0 || cell < 0 || cell >= game.World.Cells.Length || !game.Explored.Contains(cell)) return "That group is no longer observed. Select a living known unit.";
            if (!game.World.Cells[cell].IsLand) return "Land encounters cannot reach a group on water.";
            if (cell != acting.CellId && !game.World.Cells[acting.CellId].Neighbors.Contains(cell)) return "Approach a group in this place or adjacent known land.";
            if (game.TerrainTravelEnabled && TravelRules.EncounterCost(game, acting, cell) > game.ActionsFor(actorId))
                return "The mountain route or river crossing and encounter need two actions.";
            if (band != null && game.IsPlayerTribe(band.Id) || animal != null && animal.Domestic && game.IsPlayerTribe(animal.OwnerId))
                return "Your own people and companions are not valid attack targets.";
            if (friendly && animal != null && animal.Domestic) return "This group already belongs to another people. It cannot be befriended away from them.";
            if (friendly && band != null) return "Peaceful contact with other bands does not use animal befriending.";
            UnitCondition state = game.Encounters.Read(kind, id);
            if (state != null && state.LastEncounterTurn == game.Turn || friendly && animal != null && animal.LastContactTurn == game.Turn)
                return "This group has already faced your people this chapter. Give it time to respond.";
            double travel = cell == acting.CellId ? 0 : acting.Population * (game.Known("routes") ? 0.08 : 0.15);
            if (game.World.Cells[cell].Terrain == Terrain.Ice && acting.Food < game.Upkeep(acting) * 3 + travel) return "The icy approach needs a stronger provisioning reserve.";
            if (acting.Food < travel) return "The band cannot provision that approach yet.";
            if (friendly && acting.Food < travel + Animal(game, animal).OfferingCost + game.Upkeep(acting)) return "Keep capacity for your people as well as the journey and offering.";
            return "";
        }
    }

    public sealed partial class Game
    {
        public readonly EncounterState Encounters = new EncounterState();
        public SimulationRules Rules { get { return Encounters.Enabled ? SimulationRules.MobileUnits : SimulationRules.Classic; } }

        public string EnableUnitEncounters()
        {
            if (Rules == SimulationRules.MobileUnits) return "Moving encounters are already enabled.";
            InitializeEncounters(true); return "Animals now range through the land. Select a group to approach, befriend or attack; bands can fight and retreat.";
        }
        private void InitializeEncounters(bool announce)
        {
            Encounters.Enabled = true;
            Milestone herd = Knowledge.Find(k => k.Id == "herds");
            if (herd != null) { herd.Target = 12; herd.Description = "Twelve positive contacts with one moving aurochs group establish a cattle lineage."; }
            if (announce)
            {
                UnitFrame before = Frame(UnitKind.Band, Player.Id);
                AddEncounter(EncounterKind.Enabled, before, null, Player.Food, 0, 0, 0, 0, 0, 0, 0,
                    "enabled", "The wild world begins to move", "Groups keep their identity as they travel. Encounters can wound, kill, drive off or befriend a chosen unit.");
                Log("A new chapter of moving encounters begins. The earlier history remains intact.");
            }
        }

        public string AttackAnimal(int id)
        {
            string error = EncounterRules.Validate(this, UnitKind.Animal, id, false); if (error.Length > 0) return error;
            Beast target = Beasts.Find(b => b.Id == id); ActionPoints -= TravelRules.EncounterCost(this, ActionBand, target.CellId);
            if (ActionBand.CellId != target.CellId) RelocateBand(ActionBand, target.CellId, true, EncounterKind.Move, false);
            Encounters.Write(UnitKind.Animal, id).LastEncounterTurn = Turn;
            string result = FightAnimal(ActionBand, target, false);
            if (GatheringsEnabled) ReconcileGatherings(false);
            return result;
        }

        public string BefriendAnimal(int id)
        {
            string error = EncounterRules.Validate(this, UnitKind.Animal, id, true); if (error.Length > 0) return error;
            Beast target = Beasts.Find(b => b.Id == id); UnitProfile profile = EncounterRules.Animal(this, target); ActionPoints -= TravelRules.EncounterCost(this, ActionBand, target.CellId);
            if (ActionBand.CellId != target.CellId) RelocateBand(ActionBand, target.CellId, true, EncounterKind.Move);
            UnitFrame actor = Frame(UnitKind.Band, ActionBand.Id), animal = Frame(UnitKind.Animal, id);
            double food = Player.Food; int trust = target.PositiveContacts;
            ActionBand.Food -= profile.OfferingCost; target.LastContactTurn = Turn;
            UnitCondition condition = Encounters.Write(UnitKind.Animal, id); condition.LastEncounterTurn = Turn;
            string outcome, title, detail;
            if (Next(ref actionRandom) < profile.FriendChance)
            {
                target.PositiveContacts++; ActionBand.Culture[1] = Math.Min(1, ActionBand.Culture[1] + 0.045);
                if (target.PositiveContacts >= profile.TrustThreshold)
                {
                    target.Domestic = true; target.OwnerId = ActionBand.Id; condition.HostileUntil = -1;
                    string suffix = target.Kind == BeastKind.Wolves ? " hearth dogs" : target.Kind == BeastKind.Aurochs ? " cattle" :
                        target.Kind == BeastKind.Deer ? " companion deer" : target.Kind == BeastKind.Mammoths ? " hearth mammoths" : " bonded dragon";
                    target.BreedName = Place(target.CellId) + suffix; target.Hardiness = 0.5 + (1 - World.Cells[target.CellId].Temperature) * 0.4;
                    target.Yield = 0.5 + World.Cells[target.CellId].Forage * 0.4;
                    outcome = "domesticated"; title = "A living companionship begins"; detail = target.BreedName + " now travels with " + ActionBand.Name + ".";
                }
                else { outcome = "trust"; title = "Trust grows with a moving group"; detail = "The same " + target.Kind + " group now remembers " + target.PositiveContacts + " / " + profile.TrustThreshold + " positive contacts."; }
            }
            else
            {
                double risk = target.Kind == BeastKind.Dragon ? 0.92 : target.Kind == BeastKind.Mammoths ? 0.38 : target.Kind == BeastKind.Wolves ? 0.22 : target.Kind == BeastKind.Aurochs ? 0.18 : 0.03;
                if (profile.Hostile) risk = Math.Min(0.97, risk + 0.3);
                if (Next(ref actionRandom) < risk)
                {
                    ApplyDamage(UnitKind.Band, ActionBand.Id, Math.Max(1, (int)Math.Round(profile.Strength * (0.18 + Next(ref actionRandom) * 0.14))));
                    ActionBand.Cohesion = Math.Max(0.1, ActionBand.Cohesion - 0.04); condition.HostileUntil = Turn + 3;
                    outcome = "attacked"; title = "The approach turns dangerous"; detail = target.Kind + " reject the offering and strike at the approaching people.";
                }
                else { outcome = "rejected"; title = "The animals keep their distance"; detail = "The offering is spent, but this group does not gain trust."; }
            }
            AddEncounter(EncounterKind.Befriend, actor, animal, food, 0, profile.OfferingCost, 0, trust, target.PositiveContacts,
                profile.TrustThreshold, profile.FriendChance, outcome, title, detail);
            if (!target.Domestic && outcome == "attacked" && ActionBand.Population > 0 && profile.Strength > EncounterRules.Band(this, ActionBand).Strength * 1.3)
                RetreatBand(ActionBand, target.CellId);
            else if (!target.Domestic && target.Kind == BeastKind.Deer && outcome == "rejected") FleeAnimal(target, ActionBand.CellId);
            ReleaseOwnerlessCompanions(); ReconcileTribe(false); UpdateKnowledge();
            if (GatheringsEnabled) ReconcileGatherings(false);
            return detail;
        }

        public string AttackBand(int id)
        {
            string error = EncounterRules.Validate(this, UnitKind.Band, id, false); if (error.Length > 0) return error;
            Band target = Bands.Find(b => b.Id == id); ActionPoints -= TravelRules.EncounterCost(this, ActionBand, target.CellId);
            if (ActionBand.CellId != target.CellId) RelocateBand(ActionBand, target.CellId, true, EncounterKind.Move, false);
            Encounters.Write(UnitKind.Band, id).LastEncounterTurn = Turn;
            string result = FightBands(ActionBand, target);
            if (GatheringsEnabled) ReconcileGatherings(false);
            return result;
        }

        private UnitFrame Frame(UnitKind kind, int id)
        {
            if (kind == UnitKind.Band)
            {
                Band band = Bands.Find(b => b.Id == id); UnitProfile profile = EncounterRules.Band(this, band);
                return new UnitFrame { Kind = kind, Id = id, Cell = band.CellId, Count = band.Population, Wounds = profile.Wounds, Health = 10, Name = band.Name, Food = band.Food };
            }
            Beast animal = Beasts.Find(b => b.Id == id); UnitProfile beast = EncounterRules.Animal(this, animal);
            return new UnitFrame { Kind = kind, Id = id, Cell = animal.CellId, Count = animal.Count, Wounds = beast.Wounds,
                Health = EncounterRules.HealthPerAnimal(animal.Kind), Name = animal.Domestic ? animal.BreedName : animal.Kind + " group " + animal.Id, AnimalKind = animal.Kind };
        }
        private void AddEncounter(EncounterKind kind, UnitFrame actor, UnitFrame target, double foodBefore, double recovered,
            double offered, double travel, int trustBefore, int trustAfter, int threshold, double chance, string outcome, string title, string detail)
        {
            UnitFrame after = Frame(actor.Kind, actor.Id), targetAfter = target == null ? null : Frame(target.Kind, target.Id);
            bool involved = actor.Kind == UnitKind.Band && IsPlayerTribe(actor.Id) || target != null && target.Kind == UnitKind.Band && IsPlayerTribe(target.Id);
            if (actor.Kind == UnitKind.Animal) involved |= Beasts.Any(b => b.Id == actor.Id && b.Domestic && IsPlayerTribe(b.OwnerId));
            if (target != null && target.Kind == UnitKind.Animal) involved |= Beasts.Any(b => b.Id == target.Id && b.Domestic && IsPlayerTribe(b.OwnerId));
            bool visible = involved || Explored.Contains(actor.Cell) || Explored.Contains(after.Cell) || target != null && Explored.Contains(target.Cell);
            Encounters.History.Add(new EncounterRecord(Encounters.History.Count + 1, Turn, kind, actor, after, target, targetAfter,
                visible, involved, Player.Food - foodBefore, recovered, offered, travel, trustBefore, trustAfter, threshold, chance, outcome, title, detail));
            if (visible && kind != EncounterKind.Move && kind != EncounterKind.Recovery) Log(title + ". " + detail);
        }
        private void ApplyDamage(UnitKind kind, int id, int damage)
        {
            UnitCondition state = Encounters.Write(kind, id);
            int count, health;
            Band band = kind == UnitKind.Band ? Bands.Find(b => b.Id == id) : null;
            Beast animal = kind == UnitKind.Animal ? Beasts.Find(b => b.Id == id) : null;
            count = band != null ? band.Population : animal.Count; health = band != null ? 10 : EncounterRules.HealthPerAnimal(animal.Kind);
            int total = Math.Min(count * health, state.Wounds + Math.Max(0, damage));
            int killed = Math.Min(count, total / health); state.Wounds = count == killed ? 0 : total % health;
            if (band != null) band.Population -= killed; else animal.Count -= killed;
        }
        private void RelocateBand(Band band, int destination, bool cost, EncounterKind kind, bool peaceful = true)
        {
            if (band.CellId == destination) return;
            UnitFrame before = Frame(UnitKind.Band, band.Id); double food = Player.Food;
            double spent = cost ? Math.Min(band.Food, band.Population * (IsPlayerTribe(band.Id) && Known("routes") ? 0.08 : 0.15)) : 0;
            band.Food -= spent; band.CellId = destination;
            if (band.Settled)
            {
                band.Settled = false; band.HomeCell = -1;
                if (!TribesEnabled ? band.Id == 0 : TribeLeaderBand != null && band.Id == TribeLeaderBand.Id) { campTurns = 0; provisionedCampTurns = 0; }
            }
            foreach (Beast companion in Beasts.Where(b => b.Domestic && b.OwnerId == band.Id && b.Count > 0)) companion.CellId = destination;
            if (TerrainTravelEnabled) ObserveTravelPlaces(band);
            if (IsPlayerTribe(band.Id))
            { visited.Add(destination); if (band.Id == Player.Id) Reveal(destination); band.Culture[0] = Math.Min(1, band.Culture[0] + 0.05); UpdateKnowledge(); }
            else if (CulturalPlaceNames) DiscoverPlaces(band.Id, destination);
            if (TribesEnabled && IsPlayerTribe(band.Id)) RevealTribeBand(band);
            // An attacking approach must not borrow the target's maps before
            // combat establishes hostility. Ordinary arrivals can exchange them.
            if (peaceful && kind != EncounterKind.Retreat) ShareNearbyPlaceKnowledge(band);
            AddEncounter(kind, before, null, food, 0, 0, spent, 0, 0, 0, 0,
                kind == EncounterKind.Retreat ? "retreated" : "moved", kind == EncounterKind.Retreat ? "A band falls back" : "A people takes to the paths",
                band.Name + (kind == EncounterKind.Retreat ? " retreats with its survivors and companions." : " moves to another place."));
            ReconcileTribe(false);
            if (GatheringsEnabled && peaceful && kind != EncounterKind.Retreat) ReconcileGatherings(false);
        }
        private void RelocateAnimal(Beast animal, int destination, EncounterKind kind)
        {
            if (animal.CellId == destination) return;
            UnitFrame before = Frame(UnitKind.Animal, animal.Id); double food = Player.Food; animal.CellId = destination;
            AddEncounter(kind, before, null, food, 0, 0, 0, 0, 0, 0, 0, kind == EncounterKind.Retreat ? "fled" : "moved",
                kind == EncounterKind.Retreat ? "A group flees" : "A wild group moves", before.Name + " carries its surviving members and remembered contacts to new ground.");
        }
        private string FightAnimal(Band band, Beast animal, bool animalInitiated)
        {
            UnitFrame actor = Frame(animalInitiated ? UnitKind.Animal : UnitKind.Band, animalInitiated ? animal.Id : band.Id);
            UnitFrame target = Frame(animalInitiated ? UnitKind.Band : UnitKind.Animal, animalInitiated ? band.Id : animal.Id);
            double food = Player.Food; int oldCount = animal.Count;
            if (animal.Domestic && animal.OwnerId != band.Id && Bands.Any(b => b.Id == animal.OwnerId))
                Encounters.Wars.Add(EncounterState.Pair(band.Id, animal.OwnerId));
            double human = EncounterRules.Band(this, band).Strength, wild = EncounterRules.Animal(this, animal).Strength;
            Encounters.Write(UnitKind.Animal, animal.Id).HostileUntil = Turn + 5;
            Encounters.Write(UnitKind.Animal, animal.Id).LastAttackTurn = Turn;
            animal.PositiveContacts = Math.Max(0, animal.PositiveContacts - (animalInitiated ? 0 : 2));
            if (animalInitiated)
            {
                ApplyDamage(UnitKind.Band, band.Id, Damage(wild, 0.42));
                if (band.Population > 0) ApplyDamage(UnitKind.Animal, animal.Id, Damage(EncounterRules.Band(this, band).Strength, 0.30));
            }
            else
            {
                ApplyDamage(UnitKind.Animal, animal.Id, Damage(human, 0.42));
                if (animal.Count > 0) ApplyDamage(UnitKind.Band, band.Id, Damage(EncounterRules.Animal(this, animal).Strength,
                    animal.Kind == BeastKind.Deer ? 0.035 : 0.34));
            }
            band.Cohesion = Math.Max(0.1, band.Cohesion - 0.025 - Math.Max(0, (animalInitiated ? target.Count : actor.Count) - band.Population) * 0.008);
            double recovered = 0;
            if (!animalInitiated && band.Population > 0)
            {
                recovered = (oldCount - animal.Count) * (animal.Kind == BeastKind.Dragon ? 160 : animal.Kind == BeastKind.Mammoths ? 85 : animal.Kind == BeastKind.Aurochs ? 38 : animal.Kind == BeastKind.Wolves ? 12 : 28);
                band.Food += recovered;
                if (IsPlayerTribe(band.Id) && recovered > 0) { hunts++; UpdateKnowledge(); }
            }
            string outcome = band.Population <= 0 ? "band destroyed" : animal.Count <= 0 ? "group destroyed" : "wounded";
            string detail = band.Name + " and " + animal.Kind + " clash. " + (oldCount - animal.Count) + " animals and " +
                ((animalInitiated ? target.Count : actor.Count) - band.Population) + " people are lost; surviving wounds remain.";
            AddEncounter(EncounterKind.Attack, actor, target, food, recovered, 0, 0, 0, animal.PositiveContacts, 0, 0, outcome,
                animalInitiated ? "Wild animals strike" : "A chosen group is attacked", detail);
            if (animal.Count > 0 && (animal.Kind == BeastKind.Deer || wild < human * 0.8 || animal.Count < oldCount * 0.86)) FleeAnimal(animal, band.CellId);
            if (band.Population > 0 && wild > human * 1.6) RetreatBand(band, animal.CellId);
            ReleaseOwnerlessCompanions(); ReconcileTribe(false);
            return detail;
        }
        private string FightBands(Band attacker, Band defender)
        {
            UnitFrame actor = Frame(UnitKind.Band, attacker.Id), target = Frame(UnitKind.Band, defender.Id); double food = Player.Food;
            Encounters.Wars.Add(EncounterState.Pair(attacker.Id, defender.Id));
            Encounters.Write(UnitKind.Band, attacker.Id).LastAttackTurn = Turn;
            double first = EncounterRules.Band(this, attacker).Strength, second = EncounterRules.Band(this, defender).Strength;
            ApplyDamage(UnitKind.Band, defender.Id, Damage(first, 0.45));
            if (defender.Population > 0) ApplyDamage(UnitKind.Band, attacker.Id, Damage(EncounterRules.Band(this, defender).Strength, 0.37));
            attacker.Cohesion = Math.Max(0.1, attacker.Cohesion - 0.035 - (actor.Count - attacker.Population) * 0.008);
            defender.Cohesion = Math.Max(0.1, defender.Cohesion - 0.055 - (target.Count - defender.Population) * 0.01);
            double recovered = 0;
            if (defender.Population == 0 && attacker.Population > 0)
            { recovered = defender.Food; defender.Food = 0; attacker.Food += recovered; }
            string outcome = attacker.Population <= 0 ? "attacker destroyed" : defender.Population <= 0 ? "defender destroyed" : "wounded";
            string detail = attacker.Name + " attacks " + defender.Name + ". " + (actor.Count - attacker.Population) + " attackers and " +
                (target.Count - defender.Population) + " defenders are lost. Surviving bands remember the hostility.";
            AddEncounter(EncounterKind.Attack, actor, target, food, recovered, 0, 0, 0, 0, 0, 0, outcome, "People turn against people", detail);
            if (defender.Population > 0 && (first > second * 1.3 || defender.Cohesion < 0.55)) RetreatBand(defender, attacker.CellId);
            if (attacker.Population > 0 && second > first * 1.5) RetreatBand(attacker, defender.CellId);
            ReleaseOwnerlessCompanions(); ReconcileTribe(false);
            return detail;
        }
        private int Damage(double strength, double factor)
        { return Math.Max(1, (int)Math.Round(strength * factor * (0.78 + Next(ref actionRandom) * 0.44))); }
        private void RetreatBand(Band band, int threatCell)
        {
            int target = World.Cells[band.CellId].Neighbors.Where(n => World.Cells[n].IsLand && World.Cells[n].Terrain != Terrain.Ice &&
                (!IsPlayerTribe(band.Id) || Explored.Contains(n)) && !EncounterRules.HostileAt(this, n, band.Id))
                .OrderByDescending(n => ForageYield(n, band)).ThenBy(n => n).DefaultIfEmpty(band.CellId).First();
            if (target != band.CellId) RelocateBand(band, target, true, EncounterKind.Retreat);
        }
        private void FleeAnimal(Beast animal, int threatCell)
        {
            int target = World.Cells[animal.CellId].Neighbors.Where(n => World.Cells[n].IsLand)
                .OrderBy(n => Bands.Count(b => b.Population > 0 && b.CellId == n)).ThenByDescending(n => World.Cells[n].Forage).ThenBy(n => n)
                .DefaultIfEmpty(animal.CellId).First();
            RelocateAnimal(animal, target, EncounterKind.Retreat);
        }

        private string EndTurnWithEncounters()
        {
            if (IsOver) return "This band's story has ended. Its chronicle remains.";
            DiscoverAndShareBandPlaces();
            BeginTribeEconomy();
            if (BandPersonalitiesEnabled) ResolveVoluntaryBandMoves();
            if (GatheringsEnabled) ReconcileGatherings(false);
            Encounters.LastPlayerEconomy = null; Encounters.LastEconomyTurn = Turn;
            RecoverWounds();
            foreach (Band band in Bands.Where(b => !IsPlayerTribe(b.Id) && b.Population > 0).OrderBy(b => b.Id).ToArray())
            { ActMobileBand(band); if (IsOver) break; }
            if (!IsOver) ActMobileAnimals();
            Encounters.EconomyPlayerPopulation = Player.Population; Encounters.EconomyPlayerFood = Player.Food;
            if (!IsOver)
            {
                Band[] living = Bands.Where(b => b.Population > 0).ToArray();
                EconomyForecast[] balances = living.Select(b => BandEconomy.Forecast(this, b)).ToArray();
                for (int i = 0; i < living.Length; i++)
                {
                    Band band = living[i]; EconomyForecast balance = balances[i];
                    if (band.Id == 0) Encounters.LastPlayerEconomy = balance;
                    RecordTribeEconomy(band, balance);
                    band.Food = balance.EndingFood; band.Population = balance.EndingPopulation;
                    if (balance.HungerLosses > 0) { band.SafeTurns = 0; band.Cohesion = Math.Max(0.1, band.Cohesion - 0.08); }
                    else { band.SafeTurns++; band.Cohesion = Math.Min(1, band.Cohesion + 0.015); }
                    if (SaltEnabled) ApplySaltBalance(band, balance);
                    ApplyWoodBalance(band, balance);
                    if (band.Id == 0)
                    {
                        if (balance.HungerLosses > 0) Log("Hunger takes " + balance.HungerLosses + " lives. Seek new ground or food before the next turn.");
                        if (balance.ExposureLosses > 0) Log("Exposure claims " + balance.ExposureLosses + " lives. A camp would offer shelter.");
                        if (balance.Births > 0) Log("A secure food surplus supports " + balance.Births + " new members of the band.");
                    }
                }
            }
            Band knowledgeHearth = TribesEnabled ? TribeLeaderBand : Player;
            if (knowledgeHearth != null && (!TribesEnabled || knowledgeHearth.Population > 0))
            {
                if (knowledgeHearth.Food >= Upkeep(knowledgeHearth) * 2) foodSecureTurns++;
                if (knowledgeHearth.Settled) campTurns++;
                provisionedCampTurns = knowledgeHearth.Settled && knowledgeHearth.SafeTurns > 0 && knowledgeHearth.Food >= Upkeep(knowledgeHearth) * 2 ? provisionedCampTurns + 1 : 0;
            }
            for (int i = 0; i < Depletion.Length; i++) Depletion[i] = Math.Max(0, Depletion[i] - 0.09);
            ReleaseOwnerlessCompanions();
            GrowMobileAnimals();
            ReconcileTribe(true);
            if (GatheringsEnabled) ReconcileGatherings(true);
            string previous = Season; Turn++; Actions = 2; BeginTribeTurn();
            if (Player.Population > 0 || !TribesEnabled) Reveal(Player.CellId); UpdateKnowledge();
            if (IsOver) Log("The last hearth goes cold. The chronicle of " + Player.Name + " ends here.");
            else if (Season != previous) Log(Season + " shape the land and its moving inhabitants.");
            return IsOver ? "The band's story has ended in the living chronicle." : HistoryTime.Label(this, Turn) + ". Groups have moved, and two priorities are available.";
        }
        private void RecoverWounds()
        {
            foreach (Band band in Bands.Where(b => b.Population > 0))
            {
                UnitCondition state = Encounters.Read(UnitKind.Band, band.Id);
                if (state != null && state.Wounds > 0)
                {
                    UnitFrame before = Frame(UnitKind.Band, band.Id); double food = Player.Food;
                    state.Wounds = Math.Max(0, state.Wounds - (band.Settled ? 2 : 1));
                    AddEncounter(EncounterKind.Recovery, before, null, food, 0, 0, 0, 0, 0, 0, 0, "recovering", "Wounds begin to heal", band.Name + " begins to recover from its surviving wounds.");
                }
            }
            foreach (Beast animal in Beasts.Where(b => b.Count > 0))
            {
                UnitCondition state = Encounters.Read(UnitKind.Animal, animal.Id);
                if (state != null && state.Wounds > 0)
                {
                    UnitFrame before = Frame(UnitKind.Animal, animal.Id); double food = Player.Food;
                    state.Wounds = Math.Max(0, state.Wounds - Math.Max(1, EncounterRules.HealthPerAnimal(animal.Kind) / 20));
                    AddEncounter(EncounterKind.Recovery, before, null, food, 0, 0, 0, 0, 0, 0, 0, "recovering", "A group recovers", before.Name + " survives with fewer wounds.");
                }
            }
        }
        private void ActMobileBand(Band band)
        {
            Band enemy = Bands.Where(b => b.Population > 0 && EncounterRules.BandsHostile(this, band.Id, b.Id) &&
                (b.CellId == band.CellId || World.Cells[band.CellId].Neighbors.Contains(b.CellId))).OrderBy(b => b.Id).FirstOrDefault();
            if (enemy != null && EncounterRules.Band(this, band).Strength >= EncounterRules.Band(this, enemy).Strength * 0.65 && Next(ref ecologyRandom) < 0.6)
            {
                Beast exposed = Beasts.Where(b => b.Domestic && b.OwnerId == enemy.Id && b.Count > 0 &&
                    (b.CellId == band.CellId || World.Cells[band.CellId].Neighbors.Contains(b.CellId)) &&
                    EncounterRules.Animal(this, b).Strength < EncounterRules.Band(this, band).Strength * 0.7).OrderBy(b => b.Id).FirstOrDefault();
                if (exposed != null && Next(ref ecologyRandom) < 0.22)
                {
                    if (band.CellId != exposed.CellId) RelocateBand(band, exposed.CellId, true, EncounterKind.Move, false);
                    FightAnimal(band, exposed, false); return;
                }
                if (band.CellId != enemy.CellId) RelocateBand(band, enemy.CellId, true, EncounterKind.Move, false);
                FightBands(band, enemy); return;
            }
            if (SaltEnabled || TerrainTravelEnabled || WoodEnabled) { ActSaltIndependent(band); return; }
            int best = World.Cells[band.CellId].Neighbors.Concat(new[] { band.CellId }).Where(n => World.Cells[n].IsLand &&
                !EncounterRules.HostileAt(this, n, band.Id)).OrderByDescending(n => ForageYield(n, band)).ThenBy(n => n).DefaultIfEmpty(band.CellId).First();
            RelocateBand(band, best, false, EncounterKind.Move);
            band.Food += ForageYield(best, band) * 1.5; Depletion[best] = Math.Min(1, Depletion[best] + 0.18);
            if (Turn % 18 == 0 && band.LanguageId == Player.LanguageId && Vec3.Dot(World.Cells[best].Center, World.Cells[Player.CellId].Center) < 0.95)
            {
                int id = Languages.Count; LanguageProfile child = LanguageGenerator.Branch(Languages[band.LanguageId], id, Seed + band.Id * 73 + Turn);
                Languages.Add(child); band.LanguageId = id;
                if (Explored.Contains(band.CellId)) Log("Away from the parent hearth, " + band.Name + " develops a distinct speech: " + child.Name + ".");
            }
        }
        private void ActMobileAnimals()
        {
            foreach (Beast animal in Beasts.Where(b => b.Count > 0 && !b.Domestic).OrderBy(b => b.Id).ToArray())
            {
                UnitProfile profile = EncounterRules.Animal(this, animal);
                Band near = Bands.Where(b => b.Population > 0 && (b.CellId == animal.CellId || World.Cells[animal.CellId].Neighbors.Contains(b.CellId)))
                    .OrderBy(b => b.CellId == animal.CellId ? 0 : 1).ThenBy(b => b.Population).ThenBy(b => b.Id).FirstOrDefault();
                UnitCondition state = Encounters.Read(UnitKind.Animal, animal.Id);
                if (near != null && (animal.Kind == BeastKind.Deer || profile.Strength < EncounterRules.Band(this, near).Strength * 0.45))
                { if (Next(ref ecologyRandom) < 0.8) FleeAnimal(animal, near.CellId); continue; }
                if (near != null && profile.Hostile && (state == null || state.LastAttackTurn != Turn) &&
                    Next(ref ecologyRandom) < (animal.Kind == BeastKind.Dragon ? 0.18 : 0.16))
                {
                    if (animal.CellId != near.CellId) RelocateAnimal(animal, near.CellId, EncounterKind.Move);
                    FightAnimal(near, animal, true); if (IsOver) break; continue;
                }
                double mobility = animal.Kind == BeastKind.Deer ? 0.85 : animal.Kind == BeastKind.Aurochs ? 0.65 : animal.Kind == BeastKind.Wolves ? 0.52 : animal.Kind == BeastKind.Mammoths ? 0.38 : 0.20;
                if (Next(ref ecologyRandom) < mobility)
                {
                    int[] options = World.Cells[animal.CellId].Neighbors.Where(n => World.Cells[n].IsLand).OrderBy(n => n).ToArray();
                    if (options.Length > 0)
                    {
                        // A small ecological preference, with wandering rather than
                        // repeatedly locking to the highest-forage tile.
                        int target = Next(ref ecologyRandom) < 0.4 ? options.OrderByDescending(n => World.Cells[n].Forage - Depletion[n] * 0.2).First() : options[(int)(Next(ref ecologyRandom) * options.Length)];
                        RelocateAnimal(animal, target, EncounterKind.Move);
                    }
                }
            }
        }
        private void ReleaseOwnerlessCompanions()
        {
            foreach (Beast animal in Beasts.Where(b => b.Count > 0 && b.Domestic))
            {
                Band owner = Bands.Find(b => b.Id == animal.OwnerId);
                if (owner != null && owner.Population > 0) continue;
                // Record the household before clearing ownership so a surviving
                // player's lineage remains identifiable even in a terminal action.
                // An unseen foreign owner contributes no name to a visible record.
                UnitFrame before = Frame(UnitKind.Animal, animal.Id), former = owner == null || !IsPlayerTribe(owner.Id) && !Explored.Contains(owner.CellId) ? null : Frame(UnitKind.Band, owner.Id);
                double food = Player.Food; animal.Domestic = false; animal.OwnerId = -1;
                AddEncounter(EncounterKind.Release, before, former, food, 0, 0, 0, animal.PositiveContacts, animal.PositiveContacts, 0, 0,
                    "released", "A companion group becomes independent", before.Name + " survives without its former household and will range through the land again.");
            }
        }
        private void GrowMobileAnimals()
        {
            foreach (Beast animal in Beasts.Where(b => b.Count > 0))
            {
                if (animal.Domestic)
                {
                    Band owner = Bands.Find(b => b.Id == animal.OwnerId);
                    if (owner != null && owner.Population > 0) animal.CellId = owner.CellId;
                    if (Turn % 6 == 0)
                    {
                        int capacity = animal.Kind == BeastKind.Dragon ? 1 : animal.Kind == BeastKind.Mammoths ? 8 : animal.Kind == BeastKind.Wolves ? 24 : 60;
                        if (owner != null) capacity = Math.Min(capacity, Math.Max(4, owner.Population));
                        animal.Count = Math.Min(capacity, animal.Count + Math.Max(1, animal.Count / 12));
                    }
                }
                else if (Turn % 8 == 0) animal.Count = Math.Min(animal.Kind == BeastKind.Dragon ? 1 : 120, animal.Count + Math.Max(1, animal.Count / 10));
            }
        }
    }
}
