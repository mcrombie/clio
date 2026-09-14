using System;
using System.Collections.Generic;
using System.Linq;

namespace Clio.Simulation
{
    public enum Ancestry { Human, Elf, Dwarf, Goblin }
    public enum BeastKind { Aurochs, Wolves, Mammoths, Deer, Dragon, Goats }
    public sealed class Band
    {
        public int Id, CellId, Population, LanguageId, SafeTurns;
        public double Food, Cohesion = 0.8;
        public double Salt, Wood;
        public int SaltShortageTurns;
        public string Name;
        public Ancestry Ancestry;
        public bool Settled;
        public int HomeCell = -1;
        public double[] Culture = new double[4]; // mobility, reciprocity, stewardship, rootedness
    }
    public sealed class Beast
    {
        public int Id, CellId, Count, PositiveContacts, LastContactTurn = -1;
        public BeastKind Kind;
        public bool Domestic;
        public int OwnerId = -1;
        public string BreedName;
        public double Hardiness, Yield;
    }
    public sealed class ChronicleEntry
    {
        public int Turn;
        public string Text;
        public ChronicleEntry(int turn, string text) { Turn = turn; Text = text; }
    }
    public sealed class Milestone
    {
        public string Id, Name, Description;
        public int Progress, Target;
        public bool Known;
        public Milestone(string id, string name, string description, int target)
        { Id = id; Name = name; Description = description; Target = target; }
    }
    // Explicit state, explicit commands, no rendering dependency and no wall-clock randomness.
    public sealed partial class Game
    {
        public World World;
        public readonly List<Band> Bands = new List<Band>();
        public readonly List<Beast> Beasts = new List<Beast>();
        public readonly List<LanguageProfile> Languages = new List<LanguageProfile>();
        public readonly List<ChronicleEntry> Chronicle = new List<ChronicleEntry>();
        public readonly List<Milestone> Knowledge = new List<Milestone>();
        public readonly HashSet<int> Explored = new HashSet<int>();
        public int Turn = 1, Actions = 2, Seed;
        public double[] Depletion;
        public Band Player { get { return Bands[0]; } }
        public bool IsOver { get { return TribesEnabled ? !ControlledBands.Any() : Player.Population <= 0; } }
        public string Season { get { return HistoryTime.ConditionLabel(this); } }
        public double SeasonFactor { get { return HistoryTime.ConditionFactor(this); } }
        private uint ecologyRandom, actionRandom;
        private readonly HashSet<int> visited = new HashSet<int>();
        private int forageTurns, hunts, campTurns, foodSecureTurns, provisionedCampTurns;
        private int lastForageTurn = -1;
        public readonly CultureTemplateId FoundingCulture;
        public readonly HistoryPace Pace;
        public Game(GameSettings settings)
        {
            if (settings == null) throw new ArgumentNullException("settings");
            if (!Enum.IsDefined(typeof(CultureTemplateId), settings.FoundingCulture)) throw new ArgumentOutOfRangeException("settings", "Unknown founding culture template.");
            if (!Enum.IsDefined(typeof(HistoryPace), settings.Pace)) throw new ArgumentOutOfRangeException("settings", "Unknown historical pace.");
            if (!Enum.IsDefined(typeof(SimulationRules), settings.Rules)) throw new ArgumentOutOfRangeException("settings", "Unknown encounter rules.");
            if (settings.TribesEnabled && (settings.Rules != SimulationRules.MobileUnits || !settings.SaltEnabled)) throw new ArgumentException("Tribes require moving encounters and salt.");
            if (settings.BandPersonalitiesEnabled && !settings.TribesEnabled) throw new ArgumentException("Band personalities require tribal bands.");
            if (settings.GatheringsEnabled && (!settings.TribesEnabled || !settings.CulturalPlaceNames)) throw new ArgumentException("Gatherings require tribal bands and remembered place names.");
            FoundingCulture = settings.FoundingCulture;
            GuidedOpening = settings.GuidedOpening;
            Pace = settings.Pace;
            CulturalPlaceNames = settings.CulturalPlaceNames;
            Seed = settings.Seed; World = World.Generate(settings.Seed, 4);
            actionRandom = unchecked((uint)settings.Seed * 747796405u + 2891336453u);
            ecologyRandom = unchecked((uint)settings.Seed * 277803737u + 14321u);
            Depletion = new double[World.Cells.Length];
            LanguageProfile proto = HistoricalCultures.Create(settings.FoundingCulture, settings.Seed, 0, settings.Style);
            Languages.Add(proto);
            Cell start = World.Cells.Where(c => c.IsLand && c.Terrain != Terrain.Ice && c.Terrain != Terrain.Mountains)
                .OrderByDescending(c => c.Forage + 0.10 * c.Neighbors.Count(n => World.Cells[n].IsLand) - Math.Abs(c.Center.Y - 0.28) * 0.2).First();
            Bands.Add(new Band { Id = 0, CellId = start.Id, Population = 50, Food = 210, LanguageId = 0,
                Name = String.IsNullOrWhiteSpace(settings.BandName) ? (settings.FoundingCulture == CultureTemplateId.Generated ? LanguageGenerator.PlaceName(proto, "people", 0) : HistoricalCultures.DefaultBandName(settings.FoundingCulture)) : settings.BandName.Trim(), Ancestry = settings.Ancestry });
            if (settings.FourBands && !GuidedOpening)
            {
                foreach (Ancestry other in Enum.GetValues(typeof(Ancestry)))
                {
                    if (other == settings.Ancestry) continue;
                    Cell home = World.Cells.Where(c => c.IsLand && c.Forage > 0.25 && c.Terrain != Terrain.Ice)
                        .OrderByDescending(c => Bands.Min(b => 1 - Vec3.Dot(c.Center, World.Cells[b.CellId].Center)) + Adaptation(other, c) * 0.12).First();
                    int id = Bands.Count;
                    LanguageProfile language = LanguageGenerator.Create(settings.Seed + id * 37, settings.Style, id);
                    Languages.Add(language);
                    Bands.Add(new Band { Id = id, CellId = home.Id, Population = 50, Food = 210, LanguageId = id,
                        Name = LanguageGenerator.PlaceName(language, "people", id), Ancestry = other });
                }
            }
            foreach (Cell cell in World.Cells)
            {
                if (!cell.IsLand || cell.Forage < 0.12 || Next(ref ecologyRandom) > 0.07) continue;
                BeastKind kind = cell.Terrain == Terrain.Forest ? BeastKind.Wolves : cell.Terrain == Terrain.Tundra ? BeastKind.Mammoths :
                    cell.Terrain == Terrain.Mountains ? BeastKind.Dragon : Next(ref ecologyRandom) < 0.5 ? BeastKind.Deer : BeastKind.Aurochs;
                Beasts.Add(new Beast { Id = Beasts.Count, CellId = cell.Id, Kind = kind, Count = kind == BeastKind.Dragon ? 1 : 12 + (int)(Next(ref ecologyRandom) * 70) });
            }
            // Tutorial scenario guarantees nearby encounters, with the same interaction settings.Rules as all other herds.
            Beasts.Add(new Beast { Id = Beasts.Count, CellId = start.Id, Kind = BeastKind.Wolves, Count = 8 });
            int neighbor = start.Neighbors.First(n => World.Cells[n].IsLand);
            Beasts.Add(new Beast { Id = Beasts.Count, CellId = neighbor, Kind = BeastKind.Aurochs, Count = 42 });
            Knowledge.Add(new Milestone("routes", "Remembered paths", "Visit six distinct places. Cheaper travel provisions.", 6));
            Knowledge.Add(new Milestone("gathering", "Seasonal gathering", "Forage in four different turns. Gather 15% more food.", 4));
            Knowledge.Add(new Milestone("tracking", "Reading the tracks", "Complete three successful hunts. Safer future hunts.", 3));
            Knowledge.Add(new Milestone("stores", "Food keeping", "End four turns with two turns of provisions. Less spoilage.", 4));
            Knowledge.Add(new Milestone("hearth", "A lasting hearth", "Keep a seasonal camp through three turns. Winter shelter.", 3));
            Knowledge.Add(new Milestone("dogs", "Companions by the fire", "Ten positive wolf encounters on distinct turns. Found a dog lineage.", 10));
            Knowledge.Add(new Milestone("herds", "Walking with herds", "Ten positive aurochs encounters on distinct turns. Found a cattle lineage.", 10));
            Knowledge.Add(new Milestone("gardens", "Tended ground", "Food keeping and a lasting hearth; stay provisioned at camp for six turns.", 6));
            if (Pace != HistoryPace.LegacySeasons)
            {
                Knowledge.Find(k => k.Id == "gathering").Name = "Gathering traditions";
                Knowledge.Find(k => k.Id == "gathering").Description = "Gather through four chapters. Improve provisioning yield by 15%.";
                Knowledge.Find(k => k.Id == "stores").Description = "End four chapters with capacity twice the community's needs. Retain more capacity.";
                Knowledge.Find(k => k.Id == "hearth").Description = "Keep a hearth through three chapters. Shelter the community in harsh years.";
                Knowledge.Find(k => k.Id == "dogs").Description = "Ten positive encounters across chapters. Establish a living dog lineage: gathering and hunting aid, with care needs.";
                Knowledge.Find(k => k.Id == "herds").Description = "Ten positive encounters across chapters. Establish a cattle lineage with continuing provisioning yield.";
                Knowledge.Find(k => k.Id == "gardens").Description = "Food keeping and a lasting hearth; maintain twice the community's needs for six settled chapters.";
            }
            Reveal(start.Id); visited.Add(start.Id); UpdateKnowledge();
            Log("Fifty people gather beneath an unfamiliar sky. The story of " + Player.Name + " begins.");
            if (settings.Rules == SimulationRules.MobileUnits) InitializeEncounters(false);
            DiscoverAndShareBandPlaces();
            if (settings.SaltEnabled) InitializeSaltEconomy();
            if (settings.TribesEnabled) InitializeTribes(false);
            if (settings.TerrainTravelEnabled) InitializeTerrainTravel();
            if (settings.BandPersonalitiesEnabled) InitializeBandPersonalities(false);
            if (settings.GatheringsEnabled) InitializeGatherings();
            if (GuidedOpening) InitializeGuidedOpening();
        }
        private static double Next(ref uint state)
        { state ^= state << 13; state ^= state >> 17; state ^= state << 5; if (state == 0) state = 0x9e3779b9; return state / 4294967296.0; }
        public static double Adaptation(Ancestry ancestry, Cell cell)
        {
            if (ancestry == Ancestry.Elf) return cell.Terrain == Terrain.Forest ? 1.4 : 0.95;
            if (ancestry == Ancestry.Dwarf) return cell.Terrain == Terrain.Mountains ? 1.45 : cell.Terrain == Terrain.Hills ? 1.2 : 0.95;
            if (ancestry == Ancestry.Goblin) return cell.Terrain == Terrain.Mountains || cell.Terrain == Terrain.Hills || cell.Terrain == Terrain.Desert ? 1.25 : 0.95;
            return 1.05;
        }
        public string Place(int cellId)
        { PlaceKnowledge place = TribesEnabled && CulturalPlaceNames ? ObserverKnownPlace(cellId) : null; return place == null ? Place(cellId, Player.Id) : place.Name; }
        public double ForageYield(int cellId, Band band)
        {
            if (SaltEnabled || LivestockEnabled || Pace != HistoryPace.LegacySeasons || Rules == SimulationRules.MobileUnits) return BandEconomy.ForageYield(this, cellId, band);
            Cell cell = World.Cells[cellId];
            double gathering = band.Id == 0 && Known("gathering") ? 1.15 : 1;
            return Math.Max(0, Math.Round((12 + cell.Forage * 100) * SeasonFactor * Adaptation(band.Ancestry, cell) *
                (1 - Depletion[cellId] * 0.8) * gathering * Math.Min(2.5, band.Population / 50.0)));
        }
        public double Upkeep(Band band)
        {
            double cold = World.Cells[band.CellId].Temperature < 0.24 ? 1.35 : 1;
            double need = Math.Ceiling(band.Population * cold);
            return WoodEconomy.HasFuel(this, band) ? Math.Ceiling(need * .9) : need;
        }
        public bool Known(string id) { Milestone m = Knowledge.Find(k => k.Id == id); return m != null && m.Known; }
        public bool CanMove(int id)
        { if (GuidedOpening) return CanGuidedMoveToCell(id);
          return !BattleLocked && !IsOver && ActionBand.Population > 0 && ActionPoints > 0 && id >= 0 && id < World.Cells.Length && World.Cells[id].IsLand && World.Cells[ActionBand.CellId].Neighbors.Contains(id) &&
            (Rules == SimulationRules.Classic || Explored.Contains(id) && !EncounterRules.HostileAt(this, id, ActionBand.Id)) &&
            (!TerrainTravelEnabled || Explored.Contains(id) && TravelRules.MoveCost(this, ActionBand, ActionBand.CellId, id) <= ActionPoints &&
                (World.Cells[id].Terrain != Terrain.Ice || ActionBand.Food >= Upkeep(ActionBand) * 3)); }
        private bool CanAct(out string message)
        { message = BattleLocked ? "Finish the regional battle before issuing world orders." : IsOver || ActionBand.Population <= 0 ? "This band's story has ended. Start a new world to play again." : ActionPoints <= 0 ? "The band has spent its effort. End the turn to continue." : ""; return message.Length == 0; }
        public string Move(int id)
        {
            if (GuidedOpening) return GuidedMove(id);
            string message; if (!CanAct(out message)) return message;
            if (TerrainTravelEnabled && id >= 0 && id < World.Cells.Length && Explored.Contains(id) &&
                TravelRules.MoveCost(this, ActionBand, ActionBand.CellId, id) > ActionPoints)
                return "This mountain route or river crossing needs two actions. Wait until the band has enough effort.";
            if (Rules == SimulationRules.MobileUnits && id >= 0 && id < World.Cells.Length && Explored.Contains(id) &&
                World.Cells[ActionBand.CellId].Neighbors.Contains(id) && EncounterRules.HostileAt(this, id, ActionBand.Id))
                return "Hostile units guard that place. Select a group and choose attack or befriend before approaching.";
            if (!CanMove(id)) return "Choose an adjacent land cell. The pale outline marks your next steps.";
            Cell destination = World.Cells[id];
            if (destination.Terrain == Terrain.Ice && ActionBand.Food < Upkeep(ActionBand) * 3) return Pace == HistoryPace.LegacySeasons ?
                "Crossing polar ice needs at least three turns of provisions." : "Crossing polar ice needs provisioning capacity at least three times the community's needs.";
            if (Rules == SimulationRules.MobileUnits) { ActionPoints -= TravelRules.MoveCost(this, ActionBand, ActionBand.CellId, id); RelocateBand(ActionBand, id, true, EncounterKind.Move); return "Your people reach the selected place."; }
            if (ActionBand.Settled) { ActionBand.Settled = false; ActionBand.HomeCell = -1; campTurns = 0; provisionedCampTurns = 0; Log("The hearth is left behind; the people take to the paths again."); }
            ActionBand.Food = Math.Max(0, ActionBand.Food - ActionBand.Population * (Known("routes") ? 0.08 : 0.15));
            ActionPoints -= TravelRules.MoveCost(this, ActionBand, ActionBand.CellId, id); ActionBand.CellId = id; visited.Add(id); Reveal(id);
            ShareNearbyPlaceKnowledge(ActionBand);
            foreach (Beast herd in Beasts.Where(b => b.Domestic && b.OwnerId == ActionBand.Id)) herd.CellId = id;
            ActionBand.Culture[0] = Math.Min(1, ActionBand.Culture[0] + 0.05);
            UpdateKnowledge(); Log("The band reaches " + Place(id) + ", a place of " + destination.Terrain.ToString().ToLowerInvariant() + ".");
            return Pace == HistoryPace.LegacySeasons ? "The band has moved. Food and shelter will be resolved when you end the turn." :
                "The community has migrated. Provisioning and shelter will be resolved when you end the chapter.";
        }
        public string Forage()
        {
            if (GuidedOpening) return GuidedGather();
            string message; if (!CanAct(out message)) return message;
            double gained = ForageYield(ActionBand.CellId, ActionBand);
            ActionBand.Food += gained; Depletion[ActionBand.CellId] = Math.Min(1, Depletion[ActionBand.CellId] + 0.22); ActionPoints--;
            if (lastForageTurn != Turn) { forageTurns++; lastForageTurn = Turn; }
            ActionBand.Culture[2] = Math.Min(1, ActionBand.Culture[2] + 0.025); UpdateKnowledge();
            if (Pace == HistoryPace.LegacySeasons)
            { Log("Gatherers bring home " + gained + " provisions. The ground needs time to recover."); return "+" + gained + " provisions gathered."; }
            Log("Gathering traditions add " + gained + " provisioning capacity. Intensive use leaves less opportunity for the ground to recover.");
            return "+" + gained + " provisioning capacity from gathering.";
        }
        public Beast NearbyBeast(bool tamable)
        { return Beasts.Where(b => b.CellId == ActionBand.CellId && b.Count > 0 && !b.Domestic && (!tamable || b.Kind == BeastKind.Wolves || b.Kind == BeastKind.Aurochs || LivestockEnabled && b.Kind == BeastKind.Goats)).OrderBy(b => b.LastContactTurn == Turn).ThenByDescending(b => b.PositiveContacts).FirstOrDefault(); }
        public string Hunt()
        {
            if (GuidedOpening) return GuidedOrdersOnly;
            if (Rules == SimulationRules.MobileUnits)
            { Beast target = NearbyBeast(false); return target == null ? "Select a known animal group to approach and hunt." : AttackAnimal(target.Id); }
            string message; if (!CanAct(out message)) return message;
            Beast prey = NearbyBeast(false); if (prey == null) return "There is no wild herd in this cell. Look for animal markers on the map.";
            ActionPoints--; double chance;
            if (Pace == HistoryPace.LegacySeasons && !LivestockEnabled)
            {
                chance = prey.Kind == BeastKind.Dragon ? 0.12 : prey.Kind == BeastKind.Wolves ? 0.6 : 0.8;
                if (Known("tracking")) chance += 0.1;
                if (Beasts.Any(b => b.Domestic && b.OwnerId == ActionBand.Id && b.Kind == BeastKind.Wolves)) chance += 0.05;
            }
            else chance = BandEconomy.HuntChance(this, prey);
            if (Next(ref actionRandom) < chance)
            {
                int killed = Math.Min(prey.Count, prey.Kind == BeastKind.Dragon ? 1 : 3);
                prey.Count -= killed; double food = killed * (prey.Kind == BeastKind.Mammoths ? 75 : prey.Kind == BeastKind.Wolves ? 15 : prey.Kind == BeastKind.Goats ? 8 : 38);
                ActionBand.Food += food; hunts++; UpdateKnowledge(); Log("Hunters return with " + food + " provisions from " + prey.Kind.ToString().ToLowerInvariant() + ".");
                return "The hunt succeeds: +" + food + " provisions.";
            }
            int losses = Math.Min(ActionBand.Population, prey.Kind == BeastKind.Dragon ? 12 : 1 + (int)(Next(ref actionRandom) * 3));
            ActionBand.Population -= losses; Log("The hunt turns dangerous. " + losses + " lives are lost."); return "The hunt fails. " + losses + " people are lost.";
        }
        public string Tame()
        {
            if (GuidedOpening) return GuidedOrdersOnly;
            if (Rules == SimulationRules.MobileUnits)
            {
                Beast target = Beasts.Where(b => b.CellId == ActionBand.CellId && b.Count > 0 && !b.Domestic && b.LastContactTurn != Turn && LivestockEconomy.CanDomesticate(this, b))
                    .OrderByDescending(b => b.PositiveContacts).ThenBy(b => b.Id).FirstOrDefault();
                return target == null ? "Select a known animal group to approach and befriend." : BefriendAnimal(target.Id);
            }
            string message; if (!CanAct(out message)) return message;
            Beast beast = NearbyBeast(true); if (beast == null) return LivestockEnabled ? "Seek wild wolves, aurochs or goats on this band's hex. Deer cannot be domesticated." : "Seek a wild wolf pack or aurochs herd in the band's cell.";
            if (beast.LastContactTurn == Turn) return "This group needs time to respond. Try again next turn.";
            if (ActionBand.Food < 18 + Upkeep(ActionBand)) return Pace == HistoryPace.LegacySeasons ?
                "Keep one turn of food for the band, plus 18 provisions for this encounter." :
                "Keep capacity for the community's needs, plus 18 to support these animal encounters.";
            ActionPoints--; ActionBand.Food -= 18; beast.LastContactTurn = Turn;
            if (Next(ref actionRandom) < 0.73)
            {
                beast.PositiveContacts++; ActionBand.Culture[1] = Math.Min(1, ActionBand.Culture[1] + 0.045);
                if (beast.PositiveContacts >= 10)
                {
                    beast.Domestic = true; beast.OwnerId = 0;
                    beast.BreedName = Place(ActionBand.CellId) + (beast.Kind == BeastKind.Wolves ? " hearth dogs" : beast.Kind == BeastKind.Goats ? " goats" : " cattle");
                    beast.Hardiness = 0.5 + (1 - World.Cells[ActionBand.CellId].Temperature) * 0.4;
                    beast.Yield = 0.5 + World.Cells[ActionBand.CellId].Forage * 0.4;
                    Log("A domestic lineage emerges: " + beast.BreedName + ". Its ancestry belongs to this place.");
                }
                else Log("Trust grows with the " + beast.Kind.ToString().ToLowerInvariant() + ": " + beast.PositiveContacts + "/10 positive encounters.");
                UpdateKnowledge(); return beast.Domestic ? "A domestic lineage has formed." : "A peaceful encounter. Trust " + beast.PositiveContacts + "/10.";
            }
            int loss = beast.Kind == BeastKind.Wolves && Next(ref actionRandom) < 0.35 ? Math.Min(1, ActionBand.Population) : 0;
            ActionBand.Population -= loss; Log("The animals reject the offering." + (loss > 0 ? " A handler is killed." : " The food is lost."));
            return "The encounter backfires; trust does not grow.";
        }
        public string Camp()
        {
            if (GuidedOpening) return GuidedOrdersOnly;
            string message; if (!CanAct(out message)) return message;
            if (ActionBand.Settled) return "Your seasonal camp is already here. Move to resume wandering.";
            if (ActionBand.Food < 30) return "Making camp needs 30 provisions.";
            if (WoodEnabled && ActionBand.Wood < WoodEconomy.CampCost) return "Making camp needs 10 wood as well as 30 food. Collect wood first.";
            ActionPoints--; ActionBand.Food -= 30; ActionBand.Settled = true; ActionBand.HomeCell = ActionBand.CellId;
            if (WoodEnabled) ActionBand.Wood -= WoodEconomy.CampCost;
            if (!TribesEnabled || TribeLeaderBand != null && ActionBand.Id == TribeLeaderBand.Id) { campTurns = 0; provisionedCampTurns = 0; }
            ActionBand.Culture[3] = Math.Min(1, ActionBand.Culture[3] + 0.12);
            Log((Pace == HistoryPace.LegacySeasons ? "A seasonal camp takes root at " : "A community hearth takes root at ") + Place(ActionBand.CellId) + ". Departure remains possible."); return "Shelters are raised. Stay to develop a lasting hearth.";
        }
        public string Split()
        {
            if (GuidedOpening) return GuidedOrdersOnly;
            string message; if (!CanAct(out message)) return message;
            if (ActionBand.Population < 80) return Pace == HistoryPace.LegacySeasons ?
                "A daughter band needs a population of at least 80 and two turns of food." :
                "A daughter band needs at least 80 people and provisioning capacity twice the community's needs.";
            if (ActionBand.Food < Upkeep(ActionBand) * 2) return Pace == HistoryPace.LegacySeasons ?
                "Store two turns of provisions before dividing the band." : "Build capacity twice the community's needs before dividing.";
            int target = World.Cells[ActionBand.CellId].Neighbors.Where(n => World.Cells[n].IsLand).OrderByDescending(n => World.Cells[n].Forage - Depletion[n]).First();
            if (Rules == SimulationRules.MobileUnits)
            {
                target = World.Cells[ActionBand.CellId].Neighbors.Where(n => Explored.Contains(n) && World.Cells[n].IsLand &&
                    World.Cells[n].Terrain != Terrain.Ice && !EncounterRules.HostileAt(this, n, ActionBand.Id))
                    .OrderByDescending(n => World.Cells[n].Forage - Depletion[n]).DefaultIfEmpty(-1).First();
                if (target < 0) return "No safe known neighboring home is available for a daughter band.";
            }
            int people = ActionBand.Population / 3; double share = ActionBand.Food * people / ActionBand.Population;
            double saltShare = SaltEnabled ? ActionBand.Salt * people / ActionBand.Population : 0;
            double woodShare = WoodEnabled ? ActionBand.Wood * people / ActionBand.Population : 0;
            ActionBand.Population -= people; ActionBand.Food -= share; ActionPoints--;
            Band daughter = new Band { Id = Bands.Count, CellId = target, Population = people, Food = share, Ancestry = ActionBand.Ancestry,
                Name = LanguageGenerator.PlaceName(Languages[ActionBand.LanguageId], "people", Bands.Count + 17), LanguageId = ActionBand.LanguageId,
                Culture = (double[])ActionBand.Culture.Clone() };
            if (WoodEnabled) { ActionBand.Wood -= woodShare; daughter.Wood = woodShare; }
            if (SaltEnabled)
            {
                ActionBand.Salt -= saltShare; daughter.Salt = saltShare; daughter.SaltShortageTurns = ActionBand.SaltShortageTurns;
                saltExplored[daughter.Id] = new HashSet<int>(SaltKnownCells(ActionBand));
            }
            Bands.Add(daughter); InheritPlaceKnowledge(ActionBand, daughter);
            if (TribesEnabled) JoinDaughterToTribe(ActionBand, daughter);
            if (CulturalPlaceNames) DiscoverPlaces(daughter.Id, daughter.CellId);
            if (SaltEnabled) ObserveSaltPlaces(daughter);
            if (TribesEnabled) return "A daughter band remains within your tribe and can receive orders next turn.";
            Log(daughter.Name + " departs with " + people + " people. A new polity still speaks the parent tongue.");
            return "A daughter band now makes its own decisions.";
        }
        public string EndTurn()
        {
            if (GuidedOpening) return EndGuidedTurn();
            if (Rules == SimulationRules.MobileUnits) return EndTurnWithEncounters();
            if (IsOver) return "This band's story has ended. Its chronicle remains.";
            DiscoverAndShareBandPlaces();
            foreach (Band band in Bands.Where(b => b.Population > 0).ToArray())
            {
                if (band.Id != 0) ActIndependent(band);
                if (SaltEnabled || WoodEnabled || LivestockEnabled) { ResolveSaltHousehold(band); continue; }
                double passive = band.Settled ? ForageYield(band.CellId, band) * (band.Id == 0 && Known("gardens") ? 0.75 : 0.3) : 0;
                if (Pace == HistoryPace.LegacySeasons)
                {
                    foreach (Beast herd in Beasts.Where(b => b.Domestic && b.OwnerId == band.Id && b.Count > 0))
                    {
                        if (herd.Kind == BeastKind.Aurochs) passive += Math.Min(herd.Count * 0.7, 25) * herd.Yield;
                        else band.Food = Math.Max(0, band.Food - herd.Count * 0.3);
                    }
                }
                else
                {
                    DomesticEconomy domestic = BandEconomy.DomesticEffects(this, band);
                    band.Food = Math.Max(0, band.Food - domestic.AnimalCare);
                    passive += domestic.CattleFood;
                }
                band.Food += passive;
                double upkeep = Upkeep(band);
                if (band.Food < upkeep)
                {
                    int losses = Math.Min(band.Population, Math.Max(1, (int)Math.Ceiling((upkeep - band.Food) * 0.15)));
                    band.Population -= losses; band.Cohesion = Math.Max(0.1, band.Cohesion - 0.08); band.SafeTurns = 0;
                    if (band.Id == 0) Log("Hunger takes " + losses + " lives. Seek new ground or food before the next turn.");
                }
                else { band.Cohesion = Math.Min(1, band.Cohesion + 0.015); band.SafeTurns++; }
                band.Food = Math.Max(0, band.Food - upkeep);
                if (HistoryTime.HasExposureRisk(this, band))
                { int lost = Math.Min(band.Population, 2); band.Population -= lost; if (band.Id == 0) Log("Exposure claims " + lost + " lives. A camp would offer shelter."); }
                if (band.SafeTurns >= 2 && band.Food >= Upkeep(band) * 2 && Turn % 2 == 0 && band.Population > 0)
                {
                    int births = Math.Max(1, (int)Math.Floor(band.Population * 0.04)); band.Population += births;
                    if (band.Id == 0) Log("A secure food surplus supports " + births + " new members of the band.");
                }
                band.Food *= band.Id == 0 && Known("stores") ? 0.98 : 0.95;
            }
            if (Player.Food >= Upkeep(Player) * 2) foodSecureTurns++;
            if (Player.Settled) campTurns++;
            provisionedCampTurns = Player.Settled && Player.SafeTurns > 0 && Player.Food >= Upkeep(Player) * 2 ? provisionedCampTurns + 1 : 0;
            for (int i = 0; i < Depletion.Length; i++) Depletion[i] = Math.Max(0, Depletion[i] - 0.09);
            foreach (Beast beast in Beasts.Where(b => b.Count > 0))
            {
                if (beast.Domestic)
                {
                    if (Turn % 6 == 0)
                    {
                        if (LivestockEnabled) { GrowLivestockHerd(beast); continue; }
                        beast.Count += Math.Max(1, beast.Count / 12);
                        if (Pace != HistoryPace.LegacySeasons)
                        {
                            Band owner = Bands.Find(b => b.Id == beast.OwnerId);
                            int capacity = owner == null ? 8 : beast.Kind == BeastKind.Wolves ?
                                Math.Max(8, Math.Min(24, owner.Population / 3)) : Math.Max(12, Math.Min(60, owner.Population));
                            // These are successive generations of a living lineage.
                            // Supported groups stabilize instead of compounding without limit.
                            beast.Count = Math.Min(beast.Count, capacity);
                        }
                    }
                    continue;
                }
                // Recently fed groups linger. Abandoned groups resume migration; growth is never skipped.
                bool lingers = beast.LastContactTurn >= 0 && Turn - beast.LastContactTurn <= 2;
                if (!lingers && Next(ref ecologyRandom) < 0.22)
                {
                    int[] options = World.Cells[beast.CellId].Neighbors.Where(n => World.Cells[n].IsLand).ToArray();
                    if (options.Length > 0) beast.CellId = options[(int)(Next(ref ecologyRandom) * options.Length)];
                }
                if (Turn % 8 == 0) beast.Count = Math.Min(beast.Kind == BeastKind.Dragon ? 1 : 120, beast.Count + Math.Max(1, beast.Count / 10));
            }
            Turn++; Actions = 2; Reveal(Player.CellId); UpdateKnowledge();
            if (IsOver) Log("The last hearth goes cold. The chronicle of " + Player.Name + " ends here.");
            else if (Pace == HistoryPace.LegacySeasons && (Turn - 1) % 3 == 0) Log(Season + " changes the land. Food yields now reflect the new conditions.");
            else if (Pace != HistoryPace.LegacySeasons && Season != HistoryTime.ConditionLabel(this, Turn - 1))
                Log(Season + " shape this chapter. Provisioning reflects the prevailing conditions.");
            return IsOver ? "The band has perished. Open New story to begin again." : Pace == HistoryPace.LegacySeasons ?
                Season + ", turn " + Turn + ". Two actions are available." :
                HistoryTime.Label(this, Turn) + ". " + Season + ". Two actions are available.";
        }
        private void ActIndependent(Band band)
        {
            if (SaltEnabled || TerrainTravelEnabled || WoodEnabled || LivestockEnabled) { ActSaltIndependent(band); return; }
            int best = World.Cells[band.CellId].Neighbors.Concat(new[] { band.CellId }).Where(n => World.Cells[n].IsLand)
                .OrderByDescending(n => ForageYield(n, band)).First();
            band.CellId = best; band.Food += ForageYield(best, band) * 1.5; Depletion[best] = Math.Min(1, Depletion[best] + 0.18);
            if (CulturalPlaceNames) { DiscoverPlaces(band.Id, best); ShareNearbyPlaceKnowledge(band); }
            // Prototype distance + duration gate. Full contact-based divergence is specified in LANGUAGES.md.
            if (Turn % 18 == 0 && band.LanguageId == Player.LanguageId && Vec3.Dot(World.Cells[best].Center, World.Cells[Player.CellId].Center) < 0.95)
            {
                int id = Languages.Count; LanguageProfile child = LanguageGenerator.Branch(Languages[band.LanguageId], id, Seed + band.Id * 73 + Turn);
                Languages.Add(child); band.LanguageId = id;
                Log("Away from the parent hearth, " + band.Name + " develops a distinct speech: " + child.Name + ".");
            }
        }
        private void Reveal(int id)
        {
            if (SaltEnabled) ObserveSaltPlaces(Player);
            if (CulturalPlaceNames) { DiscoverPlaces(Player.Id, id); return; }
            Explored.Add(id); foreach (int n in World.Cells[id].Neighbors) { Explored.Add(n); foreach (int nn in World.Cells[n].Neighbors) Explored.Add(nn); }
        }
        private void UpdateKnowledge()
        {
            foreach (Milestone k in Knowledge)
            {
                if (k.Id == "routes") k.Progress = visited.Count;
                if (k.Id == "gathering") k.Progress = forageTurns;
                if (k.Id == "tracking") k.Progress = hunts;
                if (k.Id == "stores") k.Progress = foodSecureTurns;
                if (k.Id == "hearth") k.Progress = campTurns;
                if (k.Id == "dogs") k.Progress = Beasts.Where(b => b.Kind == BeastKind.Wolves).Select(b => b.PositiveContacts).DefaultIfEmpty(0).Max();
                if (k.Id == "herds") k.Progress = Beasts.Where(b => b.Kind == BeastKind.Aurochs).Select(b => b.PositiveContacts).DefaultIfEmpty(0).Max();
                if (k.Id == "gardens") k.Progress = Known("stores") && Known("hearth") ? provisionedCampTurns : 0;
                if (!k.Known && k.Progress >= k.Target) { k.Known = true; Log("A practice becomes shared knowledge: " + k.Name + "."); }
            }
        }
        private void Log(string text) { Chronicle.Add(new ChronicleEntry(Turn, text)); }
    }
}
