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
    public static class TerrainTravelChecks
    {
        private static int checks;
        public static string PacingReport;
        public static int Run()
        {
            checks = 0; Geography(); Movement(); Encounters(); IndependentEffort(); WeightedRoutes(); Privacy(); Journeys(); return checks;
        }
        private static void Check(bool value, string message) { checks++; if (!value) throw new InvalidOperationException("Terrain travel check: " + message); }
        private static Game Create(int seed = 73421, bool enabled = true, SimulationRules rules = SimulationRules.MobileUnits, bool tribes = true, bool four = false)
        { return new Game(seed, LanguageStyle.Flowing, Ancestry.Human, four, "Travel review", CultureTemplateId.Zhol, HistoryPace.Abstract, rules, true, true, tribes, enabled); }
        private static object Field(object target, string name) { return target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public).GetValue(target); }
        private static void Set(object target, string name, object value) { target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public).SetValue(target, value); }
        private static void Geography()
        {
            foreach (int seed in new[] { 73421, -9137, 0, 1, 17, 41, 9281, Int32.MinValue, Int32.MaxValue })
            {
                Game game = Create(seed), repeat = Create(seed), old = Create(seed, false);
                Check(Stamp(game) == Stamp(repeat), "Terrain initialization is deterministic.");
                Check(Stamp(game.World) == Stamp(old.World) && Stamp(game.Bands) == Stamp(old.Bands) && Stamp(game.Beasts) == Stamp(old.Beasts), "Travel changes no existing geography, supplies or wildlife.");
                Check(Field(game, "actionRandom").Equals(Field(old, "actionRandom")) && Field(game, "ecologyRandom").Equals(Field(old, "ecologyRandom")), "Fixed river generation consumes neither established random stream.");
                RiverEdge[] edges = TravelRules.RiverEdges(game).ToArray();
                Check(edges.Length > 5 && edges.All(e => e.Flow > 0), "Several real river boundaries are generated from uplands.");
                Check(edges.Select(e => Pair(e.FromCell, e.ToCell)).Distinct().Count() == edges.Length, "River boundaries are unique.");
                Dictionary<Vec3, List<Cell>> corners = new Dictionary<Vec3, List<Cell>>();
                foreach (Cell cell in game.World.Cells) foreach (Vec3 point in cell.Corners) { List<Cell> cells; if (!corners.TryGetValue(point, out cells)) { cells = new List<Cell>(); corners.Add(point, cells); } cells.Add(cell); }
                foreach (RiverEdge edge in edges)
                {
                    Cell first = game.World.Cells[edge.FromCell], second = game.World.Cells[edge.ToCell];
                    Check(first.Neighbors.Contains(second.Id) && first.Corners.Contains(edge.Start) && first.Corners.Contains(edge.End) && second.Corners.Contains(edge.Start) && second.Corners.Contains(edge.End), "Rivers lie on actual shared map boundaries, never center-to-center shortcuts.");
                    Check(Math.Abs(corners[edge.Start].Average(c => c.Elevation) - corners[edge.End].Average(c => c.Elevation)) > 1e-10, "Every generated boundary carries a strictly downhill segment.");
                    Check(TravelRules.HasRiver(game, first.Id, second.Id) && TravelRules.HasRiver(game, second.Id, first.Id), "Crossing detection is symmetric.");
                    if (first.IsLand && second.IsLand) Check(TravelRules.MoveCost(game, game.Player, first.Id, second.Id) == 2, "Every land river crossing really costs two actions.");
                }
                Check(edges.Any(a => edges.Any(b => a != b && (a.Start.Equals(b.Start) || a.Start.Equals(b.End) || a.End.Equals(b.Start) || a.End.Equals(b.End)))), "Rivers form connected courses rather than isolated blue edges.");
                Check(TravelRules.KnownRiverEdges(game).All(e => game.Explored.Contains(e.FromCell) && game.Explored.Contains(e.ToCell)), "Known river listings require both neighboring cells to be explored.");
                int turn = old.Turn, actions = old.Actions; string supplies = Stamp(old.Bands); old.EnableTerrainTravel();
                Check(old.Turn == turn && old.Actions == actions && Stamp(old.Bands) == supplies && Stamp(TravelRules.RiverEdges(old).ToArray()) == Stamp(edges), "Migration adds the same fixed river geography without spending turns, orders or supplies.");
                string state = Stamp(old); old.EnableTerrainTravel(); Check(Stamp(old) == state, "Repeated migration is a pure no-op.");
                foreach (Cell cell in old.World.Cells) { cell.Elevation = 0; cell.Moisture = 0; }
                Check(Stamp(TravelRules.RiverEdges(old).ToArray()) == Stamp(edges), "Later mutable terrain data cannot silently regenerate the stored river map.");
            }
            Game disabled = Create(1, false); Check(!disabled.TerrainTravelEnabled && !TravelRules.RiverEdges(disabled).Any(), "Old constructors and disabled flags have no rivers or terrain travel rules.");
        }
        private static long Pair(int one, int two) { return ((long)Math.Min(one, two) << 32) | (uint)Math.Max(one, two); }
        private static void Movement()
        {
            foreach (SimulationRules rules in Enum.GetValues(typeof(SimulationRules)))
            {
                Game game = Create(73421, true, rules, false); game.Beasts.Clear(); Band actor = game.Player;
                int to = game.World.Cells[actor.CellId].Neighbors.First(n => game.World.Cells[n].IsLand); game.World.Cells[to].Terrain = Terrain.Mountains;
                Check(TravelRules.MoveCost(game, actor, actor.CellId, to) == 2 && game.CanMove(to) && game.CanMoveBand(actor.Id, to), "Mountain routes are passable with two actions in both action systems.");
                game.Forage(); string state = Stamp(game); game.Move(to);
                Check(Stamp(game) == state && !game.CanMove(to) && !game.CanMoveBand(actor.Id, to), "One remaining action cannot enter mountains, and rejection changes nothing.");
                game.EndTurn(); actor.Food = 1000; double food = actor.Food; int from = actor.CellId; int population = actor.Population;
                game.Move(to);
                Check(actor.CellId == to && game.Actions == 0 && actor.Food == food - population * .15, "Mountain movement consumes two actions with the existing ordinary food travel cost.");
                Check(TravelRules.MoveCost(game, actor, from, to) == 2, "Mountain plus any existing river never costs more than two.");
                Check(TravelRules.MoveCost(game, actor, to, to) == 0 && TravelRules.MoveCost(game, actor, -1, to) == 0, "Invalid and same-cell movement has no valid price.");
            }
            Game river = Create(); river.Beasts.Clear(); RiverEdge crossing = TravelRules.RiverEdges(river).First(e => river.World.Cells[e.FromCell].IsLand && river.World.Cells[e.ToCell].IsLand);
            river.Player.CellId = crossing.FromCell; river.World.Cells[crossing.FromCell].Terrain = river.World.Cells[crossing.ToCell].Terrain = Terrain.Grassland;
            river.DiscoverPlaces(0, crossing.FromCell); river.IssueBandCommand(0, "move:" + crossing.ToCell);
            Check(river.Player.CellId == crossing.ToCell && river.ActionsFor(0) == 0, "Crossing an actual river on flat ground consumes both tribal orders.");
            Game ice = Create(); ice.Beasts.Clear(); int target = ice.World.Cells[ice.Player.CellId].Neighbors.First(n => ice.World.Cells[n].IsLand); ice.World.Cells[target].Terrain = Terrain.Ice; ice.Player.Food = 0;
            Check(!ice.CanMoveBand(0, target) && !ice.CanMove(target), "New travel previews retain the icy approach's real provisioning requirement.");
        }
        private static void Encounters()
        {
            foreach (string command in new[] { "attack-animal:999", "befriend-animal:999", "attack-band:1" })
            {
                Game game = Create(); game.Beasts.Clear(); int target = game.World.Cells[game.Player.CellId].Neighbors.First(n => game.World.Cells[n].IsLand); game.World.Cells[target].Terrain = Terrain.Mountains;
                game.Beasts.Add(new Beast { Id = 999, Kind = BeastKind.Deer, Count = 3, CellId = target });
                game.Bands.Add(new Band { Id = 1, Population = 10, Food = 10, Name = "Other people", CellId = target });
                UnitKind kind = command.StartsWith("attack-band", StringComparison.Ordinal) ? UnitKind.Band : UnitKind.Animal; int id = kind == UnitKind.Band ? 1 : 999;
                EncounterOutlook outlook = EncounterRules.Outlook(game, 0, kind, id);
                Check(outlook.ActionCost == 2 && outlook.CanAttack, "An adjacent mountain encounter quotes its full two-action approach.");
                game.IssueBandCommand(0, "forage"); string before = Stamp(game); game.IssueBandCommand(0, command);
                Check(Stamp(game) == before && !EncounterRules.Outlook(game, 0, kind, id).CanAttack, "Insufficient-effort encounters reject atomically: " + command);
                game.EndTurn(); game.Beasts[0].CellId = target; game.Bands[1].CellId = target; game.Player.Food = 1000;
                game.IssueBandCommand(0, command);
                Check(game.ActionsFor(0) == 0 && game.Player.CellId == target, "The combined mountain approach and encounter consumes two actions, not three: " + command);
            }
            Game same = Create(); same.Beasts.Clear(); same.World.Cells[same.Player.CellId].Terrain = Terrain.Mountains;
            same.Beasts.Add(new Beast { Id = 999, Kind = BeastKind.Deer, Count = 3, CellId = same.Player.CellId });
            Check(EncounterRules.Outlook(same, 0, UnitKind.Animal, 999).ActionCost == 1, "Already-present mountain encounters remain ordinary one-action interactions.");
            same.IssueBandCommand(0, "attack-animal:999"); Check(same.ActionsFor(0) == 1, "A same-place encounter actually spends only one order.");
        }
        private static void WeightedRoutes()
        {
            Game game = Create(); game.Beasts.Clear();
            int[][] neighbors = { new[] { 1, 2 }, new[] { 0, 4 }, new[] { 0, 3 }, new[] { 2, 4 }, new[] { 1, 3 } };
            game.World = new World { Cells = Enumerable.Range(0, 5).Select(id => new Cell { Id = id, Terrain = Terrain.Grassland, Temperature = .6, Forage = .5, Neighbors = neighbors[id], Corners = new Vec3[0] }).ToArray() };
            game.Player.CellId = 0; game.Explored.Clear(); foreach (Cell cell in game.World.Cells) game.Explored.Add(cell.Id);
            Set(game, "travelRiverPairs", new HashSet<long> { Pair(0, 1), Pair(1, 4) });
            Set(game, "saltSources", new[] { SaltSource.None, SaltSource.None, SaltSource.None, SaltSource.None, SaltSource.Spring });
            int next = (int)typeof(SaltEconomy).GetMethod("NextSourceStep", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new object[] { game, game.Player });
            Check(next == 2, "Salt seeking chooses three ordinary steps over two river crossings that cost four actions.");
            game.Explored.Remove(3);
            next = (int)typeof(SaltEconomy).GetMethod("NextSourceStep", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new object[] { game, game.Player });
            Check(next == 1, "Weighted routes do not use an unobserved cheaper shortcut.");
        }
        private static void IndependentEffort()
        {
            foreach (SimulationRules rules in Enum.GetValues(typeof(SimulationRules)))
            {
                Game game = Create(73421, true, rules, false, true); game.Beasts.Clear();
                foreach (Band other in game.Bands.Skip(2)) other.Population = 0;
                Band independent = game.Bands[1]; int destination = game.World.Cells[independent.CellId].Neighbors.First(n => game.World.Cells[n].IsLand && game.World.Cells[n].Terrain != Terrain.Ice);
                game.World.Cells[destination].Terrain = Terrain.Mountains; independent.Food = 300; independent.Salt = 0; independent.SafeTurns = 0;
                SaltSource[] sources = new SaltSource[game.World.Cells.Length]; sources[destination] = SaltSource.Spring; Set(game, "saltSources", sources);
                double travel = independent.Population * .15; game.EndTurn();
                Check(independent.CellId == destination && independent.Salt == 0 && independent.SaltShortageTurns == 1,
                    "An independent band spends its full turn entering a mountain spring and cannot also gather salt: " + rules);
                Check(Math.Abs(independent.Food - (300 - travel - game.Upkeep(independent)) * .95) < 1e-8,
                    "Independent two-action travel receives no extra forage or hidden food before household costs: " + rules);
            }
        }
        private static void Privacy()
        {
            Game game = Create(); string before = Stamp(game); AutoplayDecision choice = AutoplayPolicy.Choose(game);
            RiverEdge[] seen = TravelRules.KnownRiverEdges(game).ToArray();
            foreach (RiverEdge edge in seen) TravelRules.HasRiver(game, edge.FromCell, edge.ToCell);
            Check(Stamp(game) == before, "Choosing and reading fixed visible terrain is pure.");
            foreach (Cell cell in game.World.Cells.Where(c => !game.Explored.Contains(c.Id))) { cell.Terrain = Terrain.Mountains; cell.Forage = 0; cell.Elevation = 999; }
            Check(TravelRules.KnownRiverEdges(game).SequenceEqual(seen) && AutoplayPolicy.Choose(game).Command == choice.Command, "Hidden terrain mutations alter neither known river listings nor autoplay's next action.");
            int hidden = game.World.Cells.First(c => !game.Explored.Contains(c.Id)).Id; game.Beasts.Add(new Beast { Id = 99999, CellId = hidden, Kind = BeastKind.Dragon, Count = 50 });
            Check(EncounterRules.Outlook(game, 0, UnitKind.Animal, 99999).ActionCost == 0, "Hidden encounter previews disclose no terrain price.");
            before = Stamp(game); game.IssueBandCommand(0, "move:" + hidden); game.IssueBandCommand(0, "attack-animal:99999");
            Check(Stamp(game) == before, "Hidden move/encounter attempts are atomic.");
        }
        private static void Journeys()
        {
            int survivors = 0, moves = 0, difficult = 0, steps = 0;
            foreach (int seed in new[] { 73421, -9137, 0, 17 })
            {
                Game game = Create(seed, true, SimulationRules.MobileUnits, true, true), replay = Create(seed, true, SimulationRules.MobileUnits, true, true);
                while (!game.IsOver && game.Turn <= 100 && steps < 7000)
                {
                    AutoplayDecision choice = AutoplayPolicy.Choose(game); Check(choice != null, "Terrain autoplay always finds an ordinary action.");
                    int turn = game.Turn, effort = game.ControlledBands.Sum(b => game.ActionsFor(b.Id));
                    if (choice.Command != "end")
                    {
                        string[] bits = choice.Command.Split(':'); int actor = Int32.Parse(bits[1], CultureInfo.InvariantCulture);
                        if (bits[2] == "move") { moves++; if (TravelRules.MoveCost(game, game.Bands.First(b => b.Id == actor), game.Bands.First(b => b.Id == actor).CellId, Int32.Parse(bits[3], CultureInfo.InvariantCulture)) == 2) difficult++; }
                    }
                    Execute(game, choice.Command); Execute(replay, choice.Command); steps++;
                    Check(game.Turn > turn || game.ControlledBands.Sum(b => game.ActionsFor(b.Id)) < effort || game.IsOver, "Terrain autoplay never loops on an unaffordable two-action order.");
                    if (steps % 61 == 0) Check(Stamp(game) == Stamp(replay), "Terrain movement, rivers, salt routes and independent bands replay exactly.");
                }
                if (game.Turn > 100) survivors++;
                Check(Stamp(game) == Stamp(replay), "The completed terrain-aware journey has deterministic full state.");
            }
            Check(survivors >= 3 && moves > 10 && difficult > 0, "Autoplay sustains most trials while genuinely traversing costly terrain.");
            PacingReport = "Terrain travel: " + survivors + "/4 tribes reached turn 101; " + moves + " moves, including " + difficult + " two-action crossings; " + steps + " total steps.";
        }
        private static void Execute(Game game, string command)
        {
            if (command == "end") { game.EndTurn(); return; }
            int colon = command.IndexOf(':', 5); game.IssueBandCommand(Int32.Parse(command.Substring(5, colon - 5), CultureInfo.InvariantCulture), command.Substring(colon + 1));
        }
        private static string Stamp(object value)
        {
            StringBuilder buffer = new StringBuilder(); Append(buffer, value);
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
