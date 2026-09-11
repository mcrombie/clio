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
    public static class BandPersonalityChecks
    {
        private static int checks;
        public static string PacingReport;
        private static readonly BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        public static int Run()
        { checks = 0; DefaultsAndIdentity(); Contact(); VoluntaryMoves(); AutoplayPrivacy(); Comparison(); return checks; }
        private static void Check(bool value, string reason) { checks++; if (!value) throw new InvalidOperationException("Band personalities: " + reason); }
        private static object Field(object value, string name) { return value.GetType().GetField(name, Flags).GetValue(value); }
        private static Game New(int seed = 73421, bool enabled = true, bool terrain = true)
        { return new Game(new GameSettings(seed, LanguageStyle.Flowing, Ancestry.Human, false, "Zholhen") { FoundingCulture = CultureTemplateId.Zhol, Pace = HistoryPace.Abstract, Rules = SimulationRules.MobileUnits, CulturalPlaceNames = true, SaltEnabled = true, TribesEnabled = true, TerrainTravelEnabled = terrain, BandPersonalitiesEnabled = enabled }); }
        private static Band Daughter(Game game)
        {
            game.Beasts.Clear(); game.Player.Population = 120; game.Player.Food = 10000; game.Player.Salt = 1000;
            game.IssueBandCommand(0, "split"); Band band = game.Bands.Last();
            Check(band.Id != 0 && game.CanControlBand(band.Id) && game.ActionsFor(band.Id) == 0, "A new daughter has a stable identity and waits until the next turn."); return band;
        }
        private static Game OfTemperament(BandTemperament temperament)
        {
            Game probe = New(); probe.Bands.Add(new Band { Id = 1 }); int seed = 0;
            while (seed < 1000) { probe.Seed = seed; if (probe.DispositionFor(1).Temperament == temperament) break; seed++; }
            Check(seed < 1000, "The deterministic population includes " + temperament + " households.");
            return New(seed);
        }
        private static void SafeFixture(Game game)
        {
            game.Beasts.Clear();
            foreach (int id in game.Explored) { game.World.Cells[id].Terrain = Terrain.Grassland; game.World.Cells[id].Temperature = .6; game.World.Cells[id].Forage = 1; game.Depletion[id] = 0; }
            foreach (Band band in game.ControlledBands) { band.Food = 10000; band.Salt = 1000; }
        }
        private static void HoldAndClose(Game game)
        { foreach (Band band in game.ControlledBands) { band.Food = 10000; band.Salt = 1000; if (game.ActionsFor(band.Id) > 0) game.IssueBandCommand(band.Id, "wait"); } game.EndTurn(); }
        private static void DefaultsAndIdentity()
        {
            Game old = new Game(new GameSettings(73421, LanguageStyle.Flowing, Ancestry.Human, false, "Zholhen") { FoundingCulture = CultureTemplateId.Zhol, Pace = HistoryPace.Abstract, Rules = SimulationRules.MobileUnits, CulturalPlaceNames = true, SaltEnabled = true, TribesEnabled = true, TerrainTravelEnabled = true });
            Game disabled = New(73421, false), enabled = New();
            Check(!old.BandPersonalitiesEnabled && Stamp(old) == Stamp(disabled), "Every existing twelve-argument story matches explicit disabled rules exactly.");
            Check(Stamp(old.World) == Stamp(enabled.World) && Stamp(old.Beasts) == Stamp(enabled.Beasts) && Field(old, "actionRandom").Equals(Field(enabled, "actionRandom")) && Field(old, "ecologyRandom").Equals(Field(enabled, "ecologyRandom")),
                "Personality initialization changes no terrain, wildlife or existing random stream.");
            for (int i = 1; i < 80; i++) enabled.Bands.Add(new Band { Id = i, Name = "Fixture " + i });
            string before = Stamp(enabled); BandDisposition[] profiles = enabled.Bands.Select(b => enabled.DispositionFor(b.Id)).ToArray();
            Check(profiles.Select(p => p.Temperament).Distinct().Count() == 4 && profiles.Select(p => p.Independence).Distinct().Count() > 65, "Stable identities span all four temperaments and a fine-grained independence spectrum.");
            for (int i = 0; i < 10; i++) Check(Stamp(enabled.Bands.Select(b => enabled.DispositionFor(b.Id)).ToArray()) == Stamp(profiles) && Stamp(enabled) == before, "Repeated disposition reads are deterministic and completely pure.");
            enabled.Bands[1].Name = "Renamed"; enabled.Bands[1].Culture[3] = .95;
            Check(Stamp(enabled.DispositionFor(1)) == Stamp(profiles[1]) && enabled.DispositionFor(9999) == null, "Name/culture changes do not rewrite personality and missing units have no profile.");
            disabled.Beasts.Clear(); disabled.IssueBandCommand(0, "forage"); before = Stamp(disabled.Bands); int turn = disabled.Turn, ap = disabled.ActionsFor(0); object random = Field(disabled, "actionRandom");
            disabled.EnableBandPersonalities();
            Check(disabled.BandPersonalitiesEnabled && disabled.HasBandOrderThisTurn(0) && Stamp(disabled.Bands) == before && disabled.Turn == turn && disabled.ActionsFor(0) == ap && random.Equals(Field(disabled, "actionRandom")),
                "Migration preserves supplies, actions, existing direction and RNG.");
            before = Stamp(disabled); disabled.EnableBandPersonalities(); Check(Stamp(disabled) == before, "Repeated migration is an exact no-op.");
            Game classic = new Game(new GameSettings(7, LanguageStyle.Flowing, Ancestry.Human, false, "")); before = Stamp(classic); classic.EnableBandPersonalities(); Check(Stamp(classic) == before, "Migration without tribes rejects without changing the old story.");
            bool rejected = false; try { new Game(new GameSettings(7, LanguageStyle.Flowing, Ancestry.Human, false, "") { FoundingCulture = CultureTemplateId.Zhol, Pace = HistoryPace.Abstract, Rules = SimulationRules.MobileUnits, CulturalPlaceNames = true, SaltEnabled = true, TribesEnabled = false, TerrainTravelEnabled = false, BandPersonalitiesEnabled = true }); } catch (ArgumentException) { rejected = true; }
            Check(rejected, "Personality rules cannot initialize without tribes.");
        }
        private static void Contact()
        {
            foreach (BandTemperament temperament in Enum.GetValues(typeof(BandTemperament)))
            {
                Game game = OfTemperament(temperament); Band daughter = Daughter(game); SafeFixture(game);
                BandDisposition profile = game.DispositionFor(daughter.Id); string identity = Stamp(profile); int adjacent = daughter.CellId;
                Check(game.TribeStatus(daughter.Id).DriftAfter == profile.DriftTurns && game.TribeStatus(daughter.Id).SecedeAfter == profile.SecessionTurns && game.TribeStatus(daughter.Id).SeparationBand == 1, "Adjacent contact thresholds match " + temperament + " disposition.");
                int far = game.World.Cells[adjacent].Neighbors.First(id => game.Explored.Contains(id) && game.World.Cells[id].IsLand && id != game.Player.CellId && !game.World.Cells[game.Player.CellId].Neighbors.Contains(id));
                daughter.CellId = far; int threshold = profile.SecessionTurns - (temperament == BandTemperament.Loyal ? 0 : 1);
                Check(game.TribeStatus(daughter.Id).SecedeAfter == threshold && game.TribeStatus(daughter.Id).SeparationBand == 2, "Space beyond neighboring hexes hastens nonloyal separation.");
                while (game.CanControlBand(daughter.Id)) HoldAndClose(game);
                TribeEvent separated = game.TribeEvents.Single(e => e.Kind == TribeEventKind.Secession);
                Check(separated.Turn == threshold && separated.PreviousTribeId == game.PlayerTribeId && separated.VisibleToPlayer && !EncounterRules.BandsHostile(game, 0, daughter.Id), "Actual secession occurs at the precise effective threshold and is peaceful.");
                Check(Stamp(game.DispositionFor(daughter.Id)) == identity && daughter.LanguageId != game.Player.LanguageId, "Secession branches speech without rewriting the household's base personality.");
                Check(game.TribeEvents.Count(e => e.Kind == TribeEventKind.Drift && e.BandId == daughter.Id) == 1, "A separation interval emits one drift warning.");
            }
            Game reunited = OfTemperament(BandTemperament.Adventurous); Band child = Daughter(reunited); SafeFixture(reunited); HoldAndClose(reunited); HoldAndClose(reunited);
            child.CellId = reunited.Player.CellId; HoldAndClose(reunited);
            Check(reunited.TribeStatus(child.Id).TurnsAway == 0 && !reunited.TribeStatus(child.Id).Drifting, "Co-location resets even an independent household's actual separation counter.");
        }
        private static void VoluntaryMoves()
        {
            Game game = OfTemperament(BandTemperament.Separatist); Band daughter = Daughter(game); SafeFixture(game); game.EndTurn();
            Check(game.LastWanderReceipts.Count == 0, "A newborn with no current-turn AP cannot wander for free.");
            daughter.CellId = game.Player.CellId;
            game.IssueBandCommand(daughter.Id, "forage"); game.EndTurn();
            Beast companion = new Beast { Id = 8000, Count = 2, Kind = BeastKind.Wolves, Domestic = true, OwnerId = daughter.Id, CellId = daughter.CellId, BreedName = "Companions" }; game.Beasts.Add(companion);
            int attempts = 0;
            while (game.LastWanderReceipts.Count == 0 && attempts++ < 8) { daughter.CellId = game.Player.CellId; daughter.Food = 10000; daughter.Salt = 1000; game.EndTurn(); }
            Check(game.LastWanderReceipts.Count == 1, "An untouched separatist actually volunteers one journey using spare effort.");
            WanderReceipt receipt = game.LastWanderReceipts.Single(); EncounterRecord motion = game.Encounters.Records.Single(e => e.Id == receipt.EncounterRecordId);
            TribeEconomyReceipt economy = game.LastTribeEconomy.Single(e => e.BandId == daughter.Id);
            Check(receipt.BandId == daughter.Id && receipt.FromCell != receipt.ToCell && receipt.ActionsSpent >= 1 && receipt.ActionsSpent <= 2 && companion.CellId == daughter.CellId,
                "Voluntary movement uses ordinary AP and carries the correct companions.");
            Check(Math.Abs(receipt.FoodSpent - motion.TravelPaid) < 1e-9 && Math.Abs(motion.ActorFoodDelta + receipt.FoodSpent) < 1e-9 && Math.Abs(economy.StartingFood - (10000 - receipt.FoodSpent)) < 1e-9,
                "One linked movement record and pre-economy receipt explain the exact travel food, without invented provisions.");
            int held = daughter.CellId; game.IssueBandCommand(daughter.Id, "forage");
            Check(game.HasBandOrderThisTurn(daughter.Id) && game.ActionsFor(daughter.Id) == 1, "A successful partial order marks the household as directed.");
            game.EndTurn(); Check(daughter.CellId == held && game.LastWanderReceipts.Count == 0, "Even with spare AP, an explicitly directed band does not wander afterward.");
            held = daughter.CellId; game.IssueBandCommand(daughter.Id, "wait"); EconomyForecast forecast = BandEconomy.Forecast(game, daughter); game.EndTurn();
            Check(game.CanControlBand(daughter.Id) && daughter.CellId == held && game.LastWanderReceipts.Count == 0 && Math.Abs(daughter.Food - forecast.EndingFood) < 1e-8, "Holding consumes remaining actions, prevents wandering and preserves ordinary household upkeep.");
            Check(!game.HasBandOrderThisTurn(daughter.Id), "The following turn starts without a stale explicit-order marker.");
            string before = Stamp(game); game.IssueBandCommand(daughter.Id, "move:-1"); Check(Stamp(game) == before && !game.HasBandOrderThisTurn(daughter.Id), "A rejected order does not suppress autonomy or mutate any state.");
            daughter.CellId = game.Player.CellId; daughter.Food = 0; daughter.Salt = 0; held = daughter.CellId; game.EndTurn();
            Check(daughter.CellId == held && game.LastWanderReceipts.Count == 0, "Voluntary wander does not spend scarce food or salt during a survival deficit (cell " + held + " -> " + daughter.CellId + ", controlled " + game.CanControlBand(daughter.Id) + ", receipts " + game.LastWanderReceipts.Count + ").");
            Check(game.TribeEvents.All(e => e.Kind != TribeEventKind.Wandering || e.BandId != game.Player.Id), "The leading band never volunteers an unordered journey.");
        }
        private static void AutoplayPrivacy()
        {
            Game game = OfTemperament(BandTemperament.Adventurous); Band child = Daughter(game); SafeFixture(game); game.EndTurn(); game.IssueBandCommand(0, "wait");
            string before = Stamp(game); AutoplayDecision expected = AutoplayPolicy.Choose(game);
            for (int i = 0; i < 10; i++) Check(AutoplayPolicy.Choose(game).Command == expected.Command && Stamp(game) == before, "Personality-aware autoplay remains deterministic and read-only.");
            foreach (Cell cell in game.World.Cells.Where(c => !game.Explored.Contains(c.Id))) { cell.Forage = 999; cell.Terrain = Terrain.Mountains; }
            int unseen = game.World.Cells.First(c => !game.Explored.Contains(c.Id)).Id;
            game.Beasts.Add(new Beast { Id = 9999, CellId = unseen, Count = 900, Kind = BeastKind.Dragon });
            Check(AutoplayPolicy.Choose(game).Command == expected.Command, "Unknown riches, mountains and hostile wildlife cannot attract or repel personality routes.");
            child.Food = 0; child.Salt = 0; Check(AutoplayPolicy.Choose(game).Command == "band:" + child.Id + ":forage", "A hungry adventurous band gathers before following personality travel.");
        }
        private static void Execute(Game game, string command)
        { if (command == "end") { game.EndTurn(); return; } int colon = command.IndexOf(':', 5); game.IssueBandCommand(Int32.Parse(command.Substring(5, colon - 5), CultureInfo.InvariantCulture), command.Substring(colon + 1)); }
        private static void Comparison()
        {
            int manualOld = 0, manualNew = 0, wander = 0;
            foreach (int seed in Enumerable.Range(0, 12))
            foreach (bool personalities in new[] { false, true })
            {
                Game game = New(seed, personalities); Daughter(game); SafeFixture(game);
                for (int turn = 0; turn < 10; turn++) { foreach (Band band in game.ControlledBands) { band.Food = 10000; band.Salt = 1000; } game.EndTurn(); wander += personalities ? game.LastWanderReceipts.Count : 0; }
                int splits = game.TribeEvents.Count(e => e.Kind == TribeEventKind.Secession); if (personalities) manualNew += splits; else manualOld += splits;
            }
            Check(manualOld == 0 && manualNew >= 6 && wander >= 6, "Within ten manually closed turns, personality households wander and form materially more independent polities than legacy households.");
            int oldSecessions = 0, newSecessions = 0, oldSurvivors = 0, newSurvivors = 0, steps = 0, mostBands = 0;
            foreach (int seed in new[] { 73421, -9137, 0, 17 })
            foreach (bool personalities in new[] { false, true })
            {
                Game game = New(seed, personalities), replay = New(seed, personalities); int local = 0;
                while (!game.IsOver && game.Turn <= 120 && local++ < 2500)
                {
                    AutoplayDecision choice = AutoplayPolicy.Choose(game); int turn = game.Turn, actions = game.ControlledBands.Sum(b => game.ActionsFor(b.Id));
                    Execute(game, choice.Command); Execute(replay, choice.Command); steps++;
                    Check(game.IsOver || game.Turn > turn || game.ControlledBands.Sum(b => game.ActionsFor(b.Id)) < actions, "Personality autoplay makes legal progress without rejected-order loops.");
                    if (local % 71 == 0) Check(Stamp(game) == Stamp(replay), "Autoplay, personalities, movements and secessions replay exactly.");
                }
                Check(local < 2500 && Stamp(game) == Stamp(replay), "The complete personality journey is bounded and deterministic.");
                mostBands = Math.Max(mostBands, game.Bands.Count);
                int secessions = game.TribeEvents.Count(e => e.Kind == TribeEventKind.Secession);
                if (personalities) { newSecessions += secessions; if (!game.IsOver) newSurvivors++; }
                else { oldSecessions += secessions; if (!game.IsOver) oldSurvivors++; }
            }
            PacingReport = "Personalities: ten-turn manual separation " + manualOld + " -> " + manualNew + "/12, " + wander + " voluntary moves; 120-turn autoplay secessions " + oldSecessions + " -> " + newSecessions +
                ", surviving tribes " + oldSurvivors + " -> " + newSurvivors + "/4; peak " + mostBands + " bands, " + steps + " decisions.";
            Console.WriteLine(PacingReport);
            Check(newSecessions > oldSecessions && newSurvivors >= 3 && mostBands < 30, "Autoplay produces more independent polities while sustaining most tribes and bounded household growth.");
        }
        private static string Stamp(object value)
        { StringBuilder text = new StringBuilder(); Append(text, value); using (SHA256 hash = SHA256.Create()) return BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(text.ToString()))); }
        private static void Append(StringBuilder text, object value)
        {
            if (value == null) { text.Append("null;"); return; } Type type = value.GetType(); text.Append(type.FullName).Append(':');
            if (type.IsPrimitive || type.IsEnum || value is string) { text.Append(value is double ? ((double)value).ToString("R", CultureInfo.InvariantCulture) : Convert.ToString(value, CultureInfo.InvariantCulture)).Append(';'); return; }
            IDictionary dictionary = value as IDictionary;
            if (dictionary != null) { foreach (object key in dictionary.Keys.Cast<object>().OrderBy(k => Convert.ToString(k, CultureInfo.InvariantCulture), StringComparer.Ordinal)) { Append(text, key); Append(text, dictionary[key]); } return; }
            IEnumerable sequence = value as IEnumerable; if (sequence != null) { foreach (object item in sequence) Append(text, item); text.Append("end;"); return; }
            foreach (FieldInfo field in type.GetFields(Flags).OrderBy(f => f.Name, StringComparer.Ordinal)) { text.Append(field.Name); Append(text, field.GetValue(value)); }
        }
    }
}
