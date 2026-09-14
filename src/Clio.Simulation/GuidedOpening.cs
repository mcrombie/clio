using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Clio.Simulation
{
    /// <summary>A single regional gathering expedition, without changing any game state.</summary>
    public sealed class GuidedResourceYield
    {
        public readonly int RegionId, CellCount;
        public readonly double Food, Salt, Wood;
        internal GuidedResourceYield(int region, int cells, double food, double salt, double wood)
        { RegionId = region; CellCount = cells; Food = food; Salt = salt; Wood = wood; }
    }

    /// <summary>A reachable neighboring region and its nearest land entrance.</summary>
    public sealed class GuidedRegionDestination
    {
        public readonly int RegionId, CellId;
        public readonly string Name;
        public readonly bool Known;
        internal GuidedRegionDestination(int region, int cell, string name, bool known)
        { RegionId = region; CellId = cell; Name = name; Known = known; }
    }

    public sealed partial class Game
    {
        public bool GuidedOpening { get; private set; }
        public bool GuidedWildlifeEnabled { get; private set; }
        public int Influence { get; private set; }
        private const string GuidedOrdersOnly = "For now, spend influence on Gather or Move. End the turn to receive one more influence.";

        private void InitializeGuidedOpening()
        {
            // This opt-in ruleset owns its opening and its turn loop. The older
            // two-actions-per-band simulation is deliberately left untouched.
            if (!SaltEnabled) InitializeSaltEconomy();
            if (!WoodEnabled) EnableWood();
            SetGuidedInfluence(0);
        }

        // Recorded as an explicit story command. Older guided stories replay
        // their original, still wildlife until this upgrade is encountered.
        public string EnableGuidedWildlife()
        {
            if (!GuidedOpening) return "This story already follows its original wildlife rules.";
            if (GuidedWildlifeEnabled) return "Roaming wildlife is already part of this story.";
            GuidedWildlifeEnabled = true;
            return "Wild animal groups now roam between turns. Their numbers and movements can be observed on known land.";
        }

        private void SetGuidedInfluence(int amount)
        {
            Influence = Math.Max(0, amount); Actions = Influence;
            if (TribesEnabled)
                foreach (KeyValuePair<int, TribeMemberState> member in tribeMembers)
                    member.Value.Actions = member.Key == Player.Id ? Influence : 0;
        }

        public string GuidedRegionName()
        { return "Region of " + Place(Player.CellId); }

        private Cell[] GuidedRegionCells()
        {
            int region = World.Cells[Player.CellId].RegionId;
            return World.Cells.Where(c => c.RegionId == region && c.IsLand && c.Terrain != Terrain.Ice).OrderBy(c => c.Id).ToArray();
        }

        public GuidedResourceYield GuidedGatherForecast()
        {
            int region = World.Cells[Player.CellId].RegionId;
            Cell[] cells = GuidedRegionCells();
            if (!GuidedOpening || Player.Population <= 0 || cells.Length == 0)
                return new GuidedResourceYield(region, cells.Length, 0, 0, 0);
            // One influence directs the households over a whole region. Food is
            // sized for a regional expedition, usually allowing a following turn
            // of travel; poor or exhausted country still yields less.
            double productivity = cells.Average(c => c.Forage);
            double recovery = 1 - cells.Average(c => Depletion[c.Id]) * .45;
            double adaptation = cells.Average(c => Adaptation(Player.Ancestry, c));
            double food = Math.Round(Player.Population * (1 + productivity * 3) * adaptation * recovery *
                SeasonFactor * (Known("gathering") ? 1.15 : 1) * SaltEconomy.GatheringMultiplier(this, Player));
            double wood = WoodEnabled ? Math.Round(cells.Average(c => WoodEconomy.GatherYield(this, Player, c.Id)) * recovery, 1) : 0;
            int sources = cells.Count(c => SaltEconomy.Source(this, c.Id) != SaltSource.None);
            // A real source is required. Its regional prevalence affects how
            // much can be brought back; a saltless region never creates salt.
            double salt = SaltEnabled && sources > 0 ? Math.Round(SaltEconomy.Need(Player) *
                Math.Min(6, 2.5 + sources * 12.0 / cells.Length), 1) : 0;
            return new GuidedResourceYield(region, cells.Length, Math.Max(0, food), salt, Math.Max(0, wood));
        }

        private bool CanSpendGuidedInfluence(out string message)
        {
            message = !GuidedOpening ? "This story uses the original band orders." :
                IsOver ? "Your people's story has ended. Their history remains." :
                Turn == 1 ? "Rest tonight. End the turn; the first matters of the tribe begin in the morning." :
                Influence < 1 ? "Your influence is spent. End the turn to receive one more influence." : "";
            return message.Length == 0;
        }

        public string GuidedGather()
        {
            string message; if (!CanSpendGuidedInfluence(out message)) return message;
            GuidedResourceYield yield = GuidedGatherForecast();
            Player.Food += yield.Food; Player.Salt += yield.Salt; Player.Wood += yield.Wood;
            foreach (Cell cell in GuidedRegionCells()) Depletion[cell.Id] = Math.Min(1, Depletion[cell.Id] + .14);
            SetGuidedInfluence(Influence - 1);
            if (lastForageTurn != Turn) { forageTurns++; lastForageTurn = Turn; }
            Player.Culture[2] = Math.Min(1, Player.Culture[2] + .025);
            UpdateKnowledge();
            string result = "Gathered " + GuidedNumber(yield.Food) + " food, " + GuidedNumber(yield.Wood) + " wood and " +
                GuidedNumber(yield.Salt) + " salt from " + GuidedRegionName() + ".";
            Log(result + " One influence spent.");
            return result;
        }

        public GuidedRegionDestination[] GuidedDestinations()
        {
            if (!GuidedOpening || IsOver) return new GuidedRegionDestination[0];
            int currentRegion = World.Cells[Player.CellId].RegionId;
            Queue<int> queue = new Queue<int>(); HashSet<int> seen = new HashSet<int>();
            Dictionary<int, int> entrances = new Dictionary<int, int>();
            queue.Enqueue(Player.CellId); seen.Add(Player.CellId);
            // Breadth first, with stable cell order: nearby borders are chosen
            // without inspecting hidden resources or hidden occupants.
            while (queue.Count > 0)
            {
                int here = queue.Dequeue();
                foreach (int id in World.Cells[here].Neighbors.OrderBy(n => n))
                {
                    Cell next = World.Cells[id];
                    if (!next.IsLand || next.Terrain == Terrain.Ice) continue;
                    if (next.RegionId != currentRegion)
                    { if (!entrances.ContainsKey(next.RegionId)) entrances.Add(next.RegionId, id); }
                    else if (seen.Add(id)) queue.Enqueue(id);
                }
            }
            List<GuidedRegionDestination> result = new List<GuidedRegionDestination>();
            foreach (KeyValuePair<int, int> entrance in entrances.OrderBy(e => e.Key))
            {
                int remembered = Explored.Where(id => World.Cells[id].RegionId == entrance.Key && World.Cells[id].IsLand)
                    .OrderBy(id => id == entrance.Value ? 0 : 1).ThenBy(id => id).DefaultIfEmpty(-1).First();
                bool known = remembered >= 0;
                result.Add(new GuidedRegionDestination(entrance.Key, entrance.Value,
                    known ? "Region of " + Place(remembered) : "Uncharted region", known));
            }
            return result.ToArray();
        }

        public bool CanGuidedMoveToCell(int cellId)
        {
            string message;
            if (!CanSpendGuidedInfluence(out message) || cellId < 0 || cellId >= World.Cells.Length ||
                !World.Cells[cellId].IsLand || World.Cells[cellId].Terrain == Terrain.Ice) return false;
            int region = World.Cells[cellId].RegionId;
            return GuidedDestinations().Any(d => d.RegionId == region);
        }

        public string GuidedMove(int cellId)
        {
            string message; if (!CanSpendGuidedInfluence(out message)) return message;
            if (!CanGuidedMoveToCell(cellId)) return "Choose a neighboring land region. Move costs one influence.";
            int region = World.Cells[cellId].RegionId;
            GuidedRegionDestination destination = GuidedDestinations().First(d => d.RegionId == region);
            // The icon marks the tribe's entrance into its new region, not an
            // arbitrary jump to the clicked hex deep inside that region.
            Player.CellId = destination.CellId; Player.Settled = false; Player.HomeCell = -1;
            campTurns = 0; provisionedCampTurns = 0; visited.Add(Player.CellId);
            foreach (Beast herd in Beasts.Where(b => b.Domestic && b.OwnerId == Player.Id && b.Count > 0)) herd.CellId = Player.CellId;
            SetGuidedInfluence(Influence - 1);
            Reveal(Player.CellId);
            if (TribesEnabled) RevealTribeBand(Player);
            if (TerrainTravelEnabled) ObserveTravelPlaces(Player);
            Player.Culture[0] = Math.Min(1, Player.Culture[0] + .05);
            UpdateKnowledge();
            string result = "Your people enter " + GuidedRegionName() + ". One influence spent.";
            Log(result); return result;
        }

        private string IssueGuidedBandCommand(int actorId, string command)
        {
            if (actorId != Player.Id) return "The chief directs the tribe together in this mode.";
            if (command == "forage" || command == "guided-gather") return GuidedGather();
            int cell;
            if (command != null && command.StartsWith("move:", StringComparison.Ordinal) &&
                Int32.TryParse(command.Substring(5), NumberStyles.Integer, CultureInfo.InvariantCulture, out cell)) return GuidedMove(cell);
            return GuidedOrdersOnly;
        }

        private string EndGuidedTurn()
        {
            if (IsOver) return "Your people's story has ended. Their history remains.";
            BeginTribeEconomy();
            Encounters.LastEconomyTurn = Turn; Encounters.EconomyPlayerPopulation = Player.Population; Encounters.EconomyPlayerFood = Player.Food;
            EconomyForecast forecast = BandEconomy.Forecast(this, Player);
            Encounters.LastPlayerEconomy = forecast; RecordTribeEconomy(Player, forecast);
            if (Turn == 1)
            {
                Log("The tribe rests for its first night. Its reserves are untouched; the morning will bring the chief's first influence.");
                Turn++; SetGuidedInfluence(1);
                return "Morning comes. You have one influence: gather supplies or move to a neighboring region.";
            }
            // Early lessons deliberately omit autonomous combat and splitting.
            // Food, salt, fuel, births and land recovery remain real accounting.
            ResolveSaltHousehold(Player);
            if (Player.Food >= Upkeep(Player) * 2) foodSecureTurns++;
            for (int i = 0; i < Depletion.Length; i++) Depletion[i] = Math.Max(0, Depletion[i] - .09);
            if (GuidedWildlifeEnabled) { MoveGuidedWildlife(); GrowMobileAnimals(); }
            ReconcileTribe(false);
            Turn++; SetGuidedInfluence(IsOver ? Influence : Influence == Int32.MaxValue ? Influence : Influence + 1);
            if (!IsOver) Reveal(Player.CellId);
            UpdateKnowledge();
            if (IsOver) { Log("The last hearth goes cold. The history of " + Player.Name + " remains."); return "Your people's story has ended. Their history remains."; }
            return "Turn " + Turn + ". One influence gained; " + Influence + " available.";
        }

        private void MoveGuidedWildlife()
        {
            // Use the mobile world's species rhythms and forage preference,
            // without its combat branches. Wildlife is visible and alive during
            // the early lessons, but cannot initiate an unintroduced battle.
            foreach (Beast animal in Beasts.Where(b => b.Count > 0 && !b.Domestic).OrderBy(b => b.Id).ToArray())
            {
                bool lingers = animal.LastContactTurn >= 0 && Turn - animal.LastContactTurn <= 2;
                if (lingers) continue;
                double mobility = animal.Kind == BeastKind.Deer ? .85 : animal.Kind == BeastKind.Goats ? .72 :
                    animal.Kind == BeastKind.Aurochs ? .65 : animal.Kind == BeastKind.Wolves ? .52 :
                    animal.Kind == BeastKind.Mammoths ? .38 : .20;
                if (Next(ref ecologyRandom) >= mobility) continue;
                int[] options = World.Cells[animal.CellId].Neighbors.Where(n => World.Cells[n].IsLand &&
                    World.Cells[n].Terrain != Terrain.Ice).OrderBy(n => n).ToArray();
                if (options.Length == 0) continue;
                // Timid grazers favor unoccupied ground when people are near.
                // Other wildlife still wanders; it is not pulled toward the
                // player or kept artificially inside the explored area.
                if (animal.Kind == BeastKind.Deer || animal.Kind == BeastKind.Goats)
                {
                    int[] quiet = options.Where(n => !Bands.Any(b => b.Population > 0 && b.CellId == n)).ToArray();
                    if (quiet.Length > 0) options = quiet;
                }
                int target = Next(ref ecologyRandom) < .4 ?
                    options.OrderByDescending(n => World.Cells[n].Forage - Depletion[n] * .2).ThenBy(n => n).First() :
                    options[(int)(Next(ref ecologyRandom) * options.Length)];
                RelocateAnimal(animal, target, EncounterKind.Move);
            }
        }

        private static string GuidedNumber(double number)
        { return number.ToString("0.#", CultureInfo.InvariantCulture); }
    }
}
