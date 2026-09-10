using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using Clio.Simulation;

namespace Clio.Desktop
{
    internal enum StoryNoticeKind
    { Introduction, Migration, Camp, Hunt, AnimalEncounter, Domestication, Hunger, Exposure, Loss, Growth, Knowledge, Fission, Conditions, Contact, Combat, AnimalMovement, Diplomacy, PlaceNames, Salt, Tribe, Travel, Gathering, Wood, Livestock }

    internal sealed class StoryNotice
    {
        public readonly int Id, Turn, CellId;
        public readonly int EncounterId, ActorBandId, TargetBandId, AnimalId;
        public readonly string Title, Body, Impact, Advice;
        public readonly StoryNoticeKind Kind;
        public readonly bool Important;
        public readonly BeastKind? AnimalKind;
        internal StoryNotice(int id, int turn, int cell, StoryNoticeKind kind, string title, string body, string impact, string advice, bool important, BeastKind? animal)
            : this(id, turn, cell, kind, title, body, impact, advice, important, animal, -1, -1, -1, -1) { }
        internal StoryNotice(int id, int turn, int cell, StoryNoticeKind kind, string title, string body, string impact, string advice, bool important, BeastKind? animal,
            int encounterId, int actorBandId, int targetBandId, int animalId)
        { Id = id; Turn = turn; CellId = cell; Kind = kind; Title = title; Body = body; Impact = impact; Advice = advice; Important = important; AnimalKind = animal;
            EncounterId = encounterId; ActorBandId = actorBandId; TargetBandId = targetBandId; AnimalId = animalId; }
    }

    internal sealed class HistoryPoint
    {
        public readonly int Turn, Population, ExploredPlaces, KnownPractices, DomesticGroups, DomesticAnimals;
        public readonly double Food, Salt, Wood;
        public readonly string TimeLabel, Conditions;
        public int KnownPlaces { get { return ExploredPlaces; } }
        public string Season { get { return Conditions; } }
        internal HistoryPoint(Game game)
        {
            Turn = game.Turn; Population = game.TribesEnabled ? game.TribePopulation : game.Player.Population;
            Food = game.TribesEnabled ? game.ControlledBands.Sum(b => b.Food) : game.Player.Food;
            Salt = game.TribesEnabled ? game.ControlledBands.Sum(b => b.Salt) : game.Player.Salt;
            Wood = game.WoodEnabled ? (game.TribesEnabled ? game.ControlledBands.Sum(b => b.Wood) : game.Player.Wood) : 0;
            ExploredPlaces = game.Explored.Count; KnownPractices = game.Knowledge.Count(k => k.Known);
            Beast[] owned = game.Beasts.Where(b => b.Domestic && (game.TribesEnabled ? game.CanControlBand(b.OwnerId) : b.OwnerId == game.Player.Id) && b.Count > 0).ToArray();
            DomesticGroups = owned.Length; DomesticAnimals = owned.Sum(b => b.Count);
            TimeLabel = Clio.Desktop.Timeline.Label(game, game.Turn); Conditions = Clio.Desktop.Timeline.ConditionLabel(game);
        }
    }

    internal sealed class JournalTotals
    {
        public int Commands, GatherActions, HuntAttempts, SuccessfulHunts, AnimalEncounters, FailedAnimalEncounters;
        public int Births, Deaths, HungerDeaths, ExposureDeaths, NetPopulationChange, PopulationDeparted;
        public int Moves, CampsFounded, DomesticGroupsFounded, DaughterBandsFounded, Discoveries;
        public int UnresolvedPopulationChanges, UnresolvedEconomyChapters;
        public int Battles, PlayerCombatDeaths, AnimalsTaken, DomesticAnimalsLost, DomesticGroupsLost;
        public int DomesticAnimalsReleased, DomesticGroupsReleased;
        public double FoodGathered, FoodHunted, FoodGained, FoodSpent, FoodConsumed, FoodSpoiled, FoodShared;
        public double CampFoodProduced, CattleFoodProduced, AnimalCarePaid, DogGatheringFood;
        public double CombatFoodGained, CombatFoodLost;
        public double CompanionGatheringFood;
        public int SaltGatherActions, SaltDeaths;
        public double SaltGathered, SaltConsumed, SaltShared, SaltAllocated;
        public double SaltLost;
        public int TribalSecessions, TribalReunions;
        public int GatheringsAccepted, GatheringsRefused, GatheringsMet, ReturnsAgreed, ReturnsFulfilled, GatheringsMissed, GatheringsInterrupted;
        public double GatheringFoodSpent, GatheringFoodGiven, GatheringSaltGiven;
        public int WoodGatherActions;
        public double WoodGathered, WoodConsumed, CampWoodSpent, FireFoodSaved, WoodAllocated, WoodShared, WoodLost;
        public double MilkProduced, LivestockMeatProduced;
        public int LivestockSlaughtered, SlaughterActions;
    }

    internal sealed class JournalAnimal
    {
        internal readonly int Id, Count, Contacts, OwnerId;
        internal readonly BeastKind Kind;
        internal readonly bool Domestic;
        internal JournalAnimal(Beast animal)
        { Id = animal.Id; Count = animal.Count; Contacts = animal.PositiveContacts; OwnerId = animal.OwnerId; Kind = animal.Kind; Domestic = animal.Domestic; }
    }
    internal sealed class JournalBand
    {
        internal readonly int Id, Population;
        internal JournalBand(Band band) { Id = band.Id; Population = band.Population; }
    }
    internal sealed class JournalReceipt
    {
        internal readonly int HungerLosses, ExposureLosses, SaltLosses, Births, EndingPopulation;
        internal readonly double StartingFood, CampFood, CattleFood, ActualAnimalCare, Upkeep, Spoilage, EndingFood;
        internal readonly double StartingSalt, SaltConsumed, EndingSalt;
        internal readonly double WoodConsumed, FireFoodSaved;
        internal readonly double MilkFood;
        internal readonly int EndingSaltShortageTurns;
        internal JournalReceipt(EconomyForecast forecast)
        {
            HungerLosses = forecast.HungerLosses; ExposureLosses = forecast.ExposureLosses; Births = forecast.Births; EndingPopulation = forecast.EndingPopulation;
            StartingFood = forecast.StartingFood; CampFood = forecast.CampFood; CattleFood = forecast.CattleFood; ActualAnimalCare = forecast.ActualAnimalCare;
            Upkeep = forecast.Upkeep; Spoilage = forecast.Spoilage; EndingFood = forecast.EndingFood;
            SaltLosses = forecast.SaltLosses; StartingSalt = forecast.StartingSalt;
            SaltConsumed = forecast.SaltConsumed; EndingSalt = forecast.EndingSalt;
            EndingSaltShortageTurns = forecast.EndingSaltShortageTurns;
            WoodConsumed = forecast.WoodConsumed; FireFoodSaved = forecast.FireFoodSaved;
            MilkFood = forecast.MilkFood;
        }
    }

    // An opaque, detached snapshot: collections and animal records cannot change
    // when the simulation advances. Unknown people and herds are not captured.
    internal sealed class JournalSnapshot
    {
        internal readonly int Seed, Turn, Actions, Population, CellId, KnownPlaces, HuntTarget, TameTarget;
        internal readonly int EncounterCount;
        internal readonly double Food, ConditionFactor, UnassistedGathering;
        internal readonly bool Settled;
        internal readonly HistoryPace Pace;
        internal readonly SimulationRules Rules;
        internal readonly string Condition;
        internal readonly ReadOnlyCollection<string> Knowledge;
        internal readonly ReadOnlyCollection<int> NamedPlaces;
        internal readonly bool CulturalPlaces;
        internal readonly int SharedPlaceCount;
        internal readonly ReadOnlyCollection<JournalAnimal> Animals;
        internal readonly ReadOnlyCollection<JournalBand> Bands;
        internal readonly ReadOnlyCollection<int> HostileBands;
        internal readonly JournalReceipt Receipt;
        internal readonly bool SaltEnabled;
        internal readonly bool WoodEnabled;
        internal readonly bool LivestockEnabled;
        internal readonly double Salt, SaltReserve;
        internal readonly int SaltShortageTurns;
        internal readonly ReadOnlyCollection<int> SaltSources;
        internal readonly bool TribesEnabled;
        internal readonly bool TerrainTravelEnabled;
        internal readonly int TribeEventCount;
        internal readonly int GatheringEventCount;
        internal readonly ReadOnlyCollection<JournalHousehold> Households;
        internal JournalSnapshot(Game game)
        {
            Seed = game.Seed; Turn = game.Turn; Actions = game.Actions; Population = game.Player.Population;
            CellId = game.Player.CellId; Food = game.Player.Food; Settled = game.Player.Settled; Pace = game.Pace; Rules = game.Rules;
            EncounterCount = game.Encounters.Records.Count;
            KnownPlaces = game.Explored.Count; Condition = HistoryTime.ConditionLabel(game); ConditionFactor = game.SeasonFactor;
            UnassistedGathering = BandEconomy.UnassistedForageYield(game, CellId, game.Player);
            Knowledge = new ReadOnlyCollection<string>(game.Knowledge.Where(k => k.Known).Select(k => k.Id).ToArray());
            NamedPlaces = new ReadOnlyCollection<int>(game.KnownPlaces(game.Player.Id).Select(p => p.CellId).ToArray());
            CulturalPlaces = game.CulturalPlaceNames;
            SharedPlaceCount = game.KnownPlaces(game.Player.Id).Count(p => p.Acquisition == PlaceAcquisition.Shared);
            Animals = new ReadOnlyCollection<JournalAnimal>(game.Beasts.Where(b => b.Count > 0 && (b.CellId == CellId || b.Domestic && b.OwnerId == game.Player.Id ||
                Rules == SimulationRules.MobileUnits && game.Explored.Contains(b.CellId))).Select(b => new JournalAnimal(b)).ToArray());
            Bands = new ReadOnlyCollection<JournalBand>(game.Bands.Where(b => b.Population > 0 && (b.Id == game.Player.Id || game.Explored.Contains(b.CellId))).Select(b => new JournalBand(b)).ToArray());
            HostileBands = new ReadOnlyCollection<int>(game.Bands.Where(b => b.Id != game.Player.Id && EncounterRules.BandsHostile(game, game.Player.Id, b.Id)).Select(b => b.Id).ToArray());
            Beast hunt = game.NearbyBeast(false), tame = game.NearbyBeast(true);
            HuntTarget = hunt == null ? -1 : hunt.Id; TameTarget = tame == null ? -1 : tame.Id;
            Receipt = new JournalReceipt(BandEconomy.Forecast(game, game.Player));
            SaltEnabled = game.SaltEnabled; Salt = game.Player.Salt; SaltReserve = SaltEconomy.ReserveTurns(game.Player);
            WoodEnabled = game.WoodEnabled;
            LivestockEnabled = game.LivestockEnabled;
            SaltShortageTurns = game.Player.SaltShortageTurns;
            SaltSources = new ReadOnlyCollection<int>(SaltEconomy.KnownSources(game).ToArray());
            TribesEnabled = game.TribesEnabled; TribeEventCount = game.TribeEvents.Count;
            GatheringEventCount = game.GatheringEvents.Count;
            TerrainTravelEnabled = game.TerrainTravelEnabled;
            Households = new ReadOnlyCollection<JournalHousehold>((game.TribesEnabled ? game.ControlledBands : new[] { game.Player }).Select(b => new JournalHousehold(game, b)).ToArray());
        }
    }

    // Presentation only. Capture and Record never execute commands, consume
    // randomness, modify Game, or write files. Replaying normal commands rebuilds
    // the same notices, IDs, metrics, and timeline without any UI side effects.
    internal sealed partial class StoryJournal
    {
        public readonly List<StoryNotice> Notices = new List<StoryNotice>();
        public readonly List<HistoryPoint> Timeline = new List<HistoryPoint>();
        public JournalTotals Totals { get; private set; }
        public ReadOnlyCollection<StoryNotice> LastCommandNotices { get; private set; }
        public int LastCommandPopulationBefore { get; private set; }
        public int LastCommandPopulationAfter { get; private set; }
        private int nextId;
        private readonly HashSet<int> encounteredBands = new HashSet<int>();
        private readonly HashSet<int> foundedDomesticGroups = new HashSet<int>();

        public StoryJournal(Game game) { Reset(game); }
        public static JournalSnapshot Capture(Game game)
        { if (game == null) throw new ArgumentNullException("game"); return new JournalSnapshot(game); }
        public void Reset(Game game)
        {
            if (game == null) throw new ArgumentNullException("game");
            Notices.Clear(); Timeline.Clear(); encounteredBands.Clear(); foundedDomesticGroups.Clear(); Totals = new JournalTotals(); nextId = 1;
            LastCommandNotices = new ReadOnlyCollection<StoryNotice>(new StoryNotice[0]);
            LastCommandPopulationBefore = LastCommandPopulationAfter = game.TribesEnabled ? game.TribePopulation : game.Player.Population;
            foreach (Band band in game.Bands.Where(b => b.Population > 0 && (b.Id == game.Player.Id || game.Explored.Contains(b.CellId)))) encounteredBands.Add(band.Id);
            foreach (Beast beast in game.Beasts.Where(b => b.Domestic && b.OwnerId == game.Player.Id)) foundedDomesticGroups.Add(beast.Id);
            Timeline.Add(new HistoryPoint(game));
            Totals.SaltAllocated = game.SaltEnabled ? (game.TribesEnabled ? game.ControlledBands.Sum(b => b.Salt) : game.Player.Salt) : 0;
            Totals.WoodAllocated = game.WoodEnabled ? (game.TribesEnabled ? game.ControlledBands.Sum(b => b.Wood) : game.Player.Wood) : 0;
            if (game.Rules == SimulationRules.MobileUnits)
                Add(game, StoryNoticeKind.Introduction, "The living world", game.Player.Name + " begins with " + game.Player.Population + " people. Animal groups and independent bands move as each chapter unfolds.",
                    "Provisioning capacity: " + Number(game.Player.Food) + "; current community needs: " + Number(game.Upkeep(game.Player)) + ". Two priorities shape the chapter.",
                    "Select an animal banner to choose Attack or Befriend. Select another people's banner to inspect an attack. Read the chances first: encounters can cause wounds, deaths or retreat. Trust follows the animal group when it moves.", true, null);
            else if (game.Pace == HistoryPace.LegacySeasons)
                Add(game, StoryNoticeKind.Introduction, "The first hearths", game.Player.Name + " begins with " + game.Player.Population + " people and a small remembered world.",
                    Number(game.Player.Food) + " provisions; " + Number(game.Upkeep(game.Player)) + " needed for your people each chapter. Two actions are available.",
                    "Gather a reserve, explore nearby land, and raise a camp when you can spare the food. Animal bonds grow through repeated encounters. End the chapter when your work is done.", true, null);
            else
                Add(game, StoryNoticeKind.Introduction, "The first hearths", game.Player.Name + " begins with " + game.Player.Population + " people. Time advances one turn at a time.",
                    "Provisioning capacity: " + Number(game.Player.Food) + "; current community needs: " + Number(game.Upkeep(game.Player)) + ". Two priorities shape the next chapter.",
                    "Gathering and hunting strengthen support; migration seeks new ground. Establish a hearth or invest in animal relationships when capacity allows. End the chapter to see how these priorities shape your people.", true, null);
            if (game.SaltEnabled && Notices.Count > 0)
            {
                StoryNotice intro = Notices[0];
                Notices[0] = new StoryNotice(intro.Id, intro.Turn, intro.CellId, intro.Kind, intro.Title,
                    intro.Body, intro.Impact + " Salt reserve: " + Number(game.Player.Salt) + ", enough for four turns at your starting population.",
                    "Food and salt both sustain your people. Crystal markers identify known coastal deposits and salt springs. Move onto a source, then choose Gather salt in Orders. Economy / Resources explains supply and shortages.", intro.Important, intro.AnimalKind);
            }
        }

        public List<StoryNotice> Record(Game game, JournalSnapshot before, string command, string result)
        {
            if (game == null) throw new ArgumentNullException("game");
            if (before == null) throw new ArgumentNullException("before");
            if (command == null) throw new ArgumentNullException("command");
            if (game.Seed != before.Seed || game.Pace != before.Pace || game.Turn < before.Turn)
                throw new ArgumentException("The journal snapshot belongs to a different story.", "before");
            if (!before.LivestockEnabled && game.LivestockEnabled) return RecordLivestockEnabled(game, before);
            if (!before.WoodEnabled && game.WoodEnabled)
            {
                int previous = Notices.Count; Totals.Commands++;
                Totals.WoodAllocated += game.TribesEnabled ? game.ControlledBands.Sum(b => b.Wood) : game.Player.Wood;
                AddTribalNotice(game, game.Player, StoryNoticeKind.Wood, "Wood is now available",
                    "Each band begins with enough wood for six turns of cooking fires.",
                    "A fueled fire reduces food needs and protects against exposure. A new camp also costs " + Number(WoodEconomy.CampCost) + " wood.",
                    "Food and salt remain the essentials. Collect wood when reserves allow; forests give the best yield. Economy / Resources explains wood use.", false);
                List<StoryNotice> notices = Notices.Skip(previous).ToList(); LastCommandNotices = new ReadOnlyCollection<StoryNotice>(notices.ToArray());
                LastCommandPopulationBefore = LastCommandPopulationAfter = game.TribesEnabled ? game.TribePopulation : game.Player.Population;
                return notices;
            }
            if (!before.TerrainTravelEnabled && game.TerrainTravelEnabled)
            {
                int previous = Notices.Count; Totals.Commands++;
                Add(game, StoryNoticeKind.Travel, "The land shapes the journey", "The earlier journeys retain their outcomes. From now on, terrain affects the effort needed to travel.",
                    "Ordinary land costs one action. Mountains and river crossings cost two, including an adjacent encounter's approach. Mountains remain passable.",
                    "Inspect a destination before committing. The map's opportunity key explains food, salt and exploration highlights; gathering still uses an action.", true, null);
                List<StoryNotice> notices = Notices.Skip(previous).ToList(); LastCommandNotices = new ReadOnlyCollection<StoryNotice>(notices.ToArray());
                LastCommandPopulationBefore = LastCommandPopulationAfter = game.TribesEnabled ? game.TribePopulation : game.Player.Population;
                return notices;
            }
            if (game.TribesEnabled) return RecordTribal(game, before, command, result);
            int first = Notices.Count;
            bool ended = command == "end" && game.Turn > before.Turn;
            bool spentAction = game.Turn == before.Turn && game.Actions < before.Actions;
            bool enabled = command == "enable-encounters" && before.Rules != game.Rules;
            bool named = !before.CulturalPlaces && game.CulturalPlaceNames;
            bool salted = !before.SaltEnabled && game.SaltEnabled;
            if (!ended && !spentAction && !enabled && !named && !salted) return new List<StoryNotice>();
            Totals.Commands++;
            int populationDelta = game.Player.Population - before.Population;
            double foodDelta = game.Player.Food - before.Food;
            Totals.NetPopulationChange += populationDelta;
            bool mobile = game.Rules == SimulationRules.MobileUnits;
            JournalCombatReceipt combat = mobile ? RecordMobileEncounters(game, before, ended) : new JournalCombatReceipt();
            if (ended) RecordChapter(game, before, combat);
            else
            {
                Totals.FoodGained += Math.Max(0, foodDelta); Totals.FoodSpent += Math.Max(0, -foodDelta);
                if (command == "forage")
                {
                    Totals.GatherActions++; Totals.FoodGathered += Math.Max(0, foodDelta);
                    if (mobile) Totals.CompanionGatheringFood += Math.Max(0, foodDelta - before.UnassistedGathering);
                    else Totals.DogGatheringFood += Math.Max(0, foodDelta - before.UnassistedGathering);
                }
                else if (command == "hunt" && !mobile) RecordHunt(game, before, foodDelta);
                else if (command == "tame" && !mobile) RecordEncounter(game, before);
                else if (command == "camp" && !before.Settled && game.Player.Settled)
                {
                    Totals.CampsFounded++;
                    Add(game, StoryNoticeKind.Camp, "A hearth takes root", "Shelters rise at " + game.Place(game.Player.CellId) + ". Your people can remain here or take to the paths again.",
                        Number(Math.Max(0, -foodDelta)) + " provisions used." + (game.WoodEnabled ? " " + Number(WoodEconomy.CampCost) + " wood used for construction." : "") + " The camp currently brings " + Number(BandEconomy.CampFood(game, game.Player)) + " provisions each chapter.",
                        "A camp offers protection from exposure. Its gathering yield depends on the land, prevailing conditions, and remembered practices.", Totals.CampsFounded == 1, null);
                }
                else if (command == "split" && populationDelta < 0) RecordFission(game, before, foodDelta);
                else if (command.StartsWith("move:", StringComparison.Ordinal) && game.Player.CellId != before.CellId)
                {
                    if (!mobile) Totals.Moves++;
                    int learned = Math.Max(0, game.Explored.Count - before.KnownPlaces);
                    Add(game, StoryNoticeKind.Migration, "The paths grow longer", game.Player.Name + " reaches " + game.Place(game.Player.CellId) + "." + (before.Settled ? " The former hearth is left behind." : ""),
                        Number(Math.Max(0, -foodDelta)) + " travel provisions; " + learned + " newly remembered places.",
                        "The place ledger shows this land's resources. Move on when the ground is tired and give it time to recover before returning.", false, null);
                }
                if (!mobile && (command == "hunt" || command == "tame") && populationDelta < 0)
                {
                    int lost = -populationDelta; Totals.Deaths += lost;
                    JournalAnimal animal = before.Animals.FirstOrDefault(b => b.Id == (command == "hunt" ? before.HuntTarget : before.TameTarget));
                    Add(game, StoryNoticeKind.Loss, lost == 1 ? "A life is lost" : "Lives are lost", lost + (lost == 1 ? " person does" : " people do") + " not return from the " + (command == "hunt" ? "hunt." : "animal encounter."),
                        game.Player.Population + " people remain in your band.", "Animal encounters carry real risks. Gather food and rebuild the band's reserve before another attempt.", true, animal == null ? (BeastKind?)null : animal.Kind);
                }
            }
            RecordDiscoveries(game, before);
            RecordDomesticGroups(game, before);
            RecordPlaceKnowledge(game, before);
            RecordSalt(game, before, command, ended, salted);
            RecordWood(game, before, command, ended);
            RecordLivestockAction(game, before, command, ended);
            RecordContacts(game);
            List<StoryNotice> added = Notices.Skip(first).ToList();
            LastCommandNotices = new ReadOnlyCollection<StoryNotice>(added.ToArray());
            LastCommandPopulationBefore = before.Population; LastCommandPopulationAfter = game.Player.Population;
            return added;
        }

        private void RecordChapter(Game game, JournalSnapshot before, JournalCombatReceipt combat)
        {
            bool mobile = game.Rules == SimulationRules.MobileUnits;
            JournalReceipt receipt = mobile ? game.Encounters.LastEconomyTurn == before.Turn && game.Encounters.LastPlayerEconomy != null ?
                new JournalReceipt(game.Encounters.LastPlayerEconomy) : null : before.Receipt;
            bool matches = receipt != null && game.Turn == before.Turn + 1 && receipt.EndingPopulation == game.Player.Population &&
                Math.Abs(receipt.EndingFood - game.Player.Food) <= Math.Max(0.0000001, Math.Abs(game.Player.Food) * 0.000000001) &&
                (!game.SaltEnabled || Near(receipt.EndingSalt, game.Player.Salt) && receipt.EndingSaltShortageTurns == game.Player.SaltShortageTurns);
            if (matches && mobile)
                matches = game.Encounters.EconomyPlayerPopulation == before.Population - combat.PlayerDeaths &&
                    Near(game.Encounters.EconomyPlayerFood, before.Food + combat.FoodDelta) && Near(receipt.StartingFood, game.Encounters.EconomyPlayerFood);
            if (matches)
            {
                int priorBirths = Totals.Births;
                Totals.Births += receipt.Births; Totals.Deaths += receipt.HungerLosses + receipt.ExposureLosses + receipt.SaltLosses;
                Totals.HungerDeaths += receipt.HungerLosses; Totals.ExposureDeaths += receipt.ExposureLosses;
                Totals.SaltDeaths += receipt.SaltLosses; Totals.SaltConsumed += receipt.SaltConsumed;
                Totals.CampFoodProduced += receipt.CampFood; Totals.CattleFoodProduced += receipt.CattleFood;
                if (game.LivestockEnabled) Totals.MilkProduced += receipt.MilkFood;
                Totals.AnimalCarePaid += receipt.ActualAnimalCare; Totals.FoodSpoiled += receipt.Spoilage;
                double consumed = Math.Min(receipt.Upkeep, Math.Max(0, receipt.StartingFood + receipt.CampFood + receipt.CattleFood - receipt.ActualAnimalCare));
                Totals.FoodConsumed += consumed;
                Totals.FoodGained += receipt.CampFood + receipt.CattleFood;
                Totals.FoodSpent += receipt.ActualAnimalCare + consumed + receipt.Spoilage;
                if (receipt.HungerLosses > 0)
                    Add(game, StoryNoticeKind.Hunger, "The stores run short", receipt.HungerLosses + (receipt.HungerLosses == 1 ? " person is" : " people are") + " lost to hunger as the chapter closes.",
                        game.Player.Population + " people remain; " + Number(game.Player.Food) + " provisions are left.", "Gather before ending another chapter, or move to richer known ground while you still have an action to gather. Animal care also draws on the reserve.", true, null);
                if (receipt.ExposureLosses > 0)
                    Add(game, StoryNoticeKind.Exposure, "The cold reaches the hearth", receipt.ExposureLosses + (receipt.ExposureLosses == 1 ? " person is" : " people are") + " lost to exposure on unprotected ground.",
                        "Shelter was absent during " + before.Condition.ToLowerInvariant() + ".", "Raise a camp when provisions allow, or seek warmer land before the next harsh interval.", true, null);
                if (receipt.SaltLosses > 0)
                    Add(game, StoryNoticeKind.Salt, "The long salt shortage takes lives", receipt.SaltLosses + (receipt.SaltLosses == 1 ? " person is" : " people are") + " lost after repeated turns without enough salt.",
                        game.Player.Population + " people remain. This is deficient turn " + game.Player.SaltShortageTurns + ". Salt losses are recorded separately in Demographics.",
                        "Reach a known salt spring or coastal deposit and choose Gather salt. Supply the full demand at the next turn's close to end the shortage. Inspect opens the salt ledger and known sources.", true, null);
                if (receipt.Births > 0 && (priorBirths == 0 || priorBirths / 25 != Totals.Births / 25))
                    Add(game, StoryNoticeKind.Growth, "The hearth welcomes new voices", "A sustained food reserve supports " + receipt.Births + " new members of your people.",
                        game.Player.Population + " people now share the hearth; " + Totals.Births + " births have been recorded since the founding.", "A larger community needs a larger reserve. Once it is secure, a daughter band can begin a story nearby.", false, null);
            }
            else if (!(mobile && game.Player.Population == 0 && before.Population == combat.PlayerDeaths && Near(game.Player.Food, before.Food + combat.FoodDelta)))
            {
                // A future rule or externally altered state must never produce
                // invented demographic components. Preserve observed net values.
                Totals.UnresolvedEconomyChapters++;
                Totals.UnresolvedPopulationChanges += game.Player.Population - before.Population + combat.PlayerDeaths;
                Totals.FoodGained += Math.Max(0, game.Player.Food - before.Food - combat.FoodDelta);
                Totals.FoodSpent += Math.Max(0, before.Food + combat.FoodDelta - game.Player.Food);
            }
            Timeline.Add(new HistoryPoint(game));
            string nextCondition = HistoryTime.ConditionLabel(game);
            double nextFactor = game.SeasonFactor;
            bool materialShift = Math.Abs(nextFactor - before.ConditionFactor) >= 0.15 || before.ConditionFactor >= 1 && nextFactor < 1 || before.ConditionFactor < 1 && nextFactor >= 1;
            if (!game.IsOver && nextCondition != before.Condition && materialShift)
                Add(game, StoryNoticeKind.Conditions, nextCondition, "The prevailing conditions change from " + before.Condition.ToLowerInvariant() + " to " + nextCondition.ToLowerInvariant() + ".",
                    "Gathering here can currently bring " + Number(game.ForageYield(game.Player.CellId, game.Player)) + " provisions per action.",
                    "Check the food forecast before ending the next chapter. Shelter matters most on cold ground during harsh conditions.", false, null);
        }

        private void RecordHunt(Game game, JournalSnapshot before, double gained)
        {
            Totals.HuntAttempts++;
            JournalAnimal prey = before.Animals.FirstOrDefault(b => b.Id == before.HuntTarget);
            Beast after = prey == null ? null : game.Beasts.FirstOrDefault(b => b.Id == prey.Id);
            if (prey != null && after != null && after.Count < prey.Count && gained > 0)
            {
                Totals.SuccessfulHunts++; Totals.FoodHunted += gained;
                // Ordinary successful hunts belong in the totals and simulation
                // chronicle. The first one introduces that part of the ledger.
                if (Totals.SuccessfulHunts == 1 || prey.Kind == BeastKind.Dragon)
                    Add(game, StoryNoticeKind.Hunt, "The hunters return", "The hunt provides food from " + AnimalName(prey.Kind).ToLowerInvariant() + ".",
                        "+" + Number(gained) + " provisions; " + (prey.Count - after.Count) + " animals taken.", "The animal ledger records the herd that remains. Food reserves give you room to choose between hunting and patient contact.", prey.Kind == BeastKind.Dragon, prey.Kind);
            }
        }

        private sealed class JournalCombatReceipt
        {
            internal int PlayerDeaths;
            internal double FoodDelta;
        }

        private JournalCombatReceipt RecordMobileEncounters(Game game, JournalSnapshot before, bool ended)
        {
            JournalCombatReceipt receipt = new JournalCombatReceipt();
            HashSet<int> playerBands = new HashSet<int>(game.TribesEnabled ? before.Households.Select(b => b.Id) : new[] { game.Player.Id });
            HashSet<int> owned = new HashSet<int>(before.Animals.Where(a => a.Domestic && a.OwnerId == game.Player.Id).Select(a => a.Id));
            HashSet<int> newFeuds = new HashSet<int>();
            foreach (EncounterRecord record in game.Encounters.Records.Skip(before.EncounterCount))
            {
                // Visibility belongs to the occurrence, never to a later location.
                if (!record.VisibleToPlayer) continue;
                double foodDelta = game.TribesEnabled ? (record.ActorKind == UnitKind.Band && playerBands.Contains(record.ActorId) ? record.ActorFoodDelta : 0) +
                    (record.TargetKind == UnitKind.Band && playerBands.Contains(record.TargetId) ? record.TargetFoodDelta : 0) : record.PlayerFoodDelta;
                int lost = (record.ActorKind == UnitKind.Band && playerBands.Contains(record.ActorId) ? record.ActorCasualties : 0) +
                    (record.TargetKind == UnitKind.Band && playerBands.Contains(record.TargetId) ? record.TargetCasualties : 0);
                receipt.PlayerDeaths += lost; receipt.FoodDelta += foodDelta;
                Totals.Deaths += lost;
                Totals.PlayerCombatDeaths += lost;
                if ((record.Kind == EncounterKind.Move || record.Kind == EncounterKind.Retreat) && record.ActorKind == UnitKind.Band && playerBands.Contains(record.ActorId) && record.FromCell != record.ToCell) Totals.Moves++;
                if (ended)
                {
                    Totals.FoodGained += Math.Max(0, foodDelta);
                    Totals.FoodSpent += Math.Max(0, -foodDelta);
                }
                if (record.Kind == EncounterKind.Attack)
                {
                    if (record.PlayerInvolved) Totals.Battles++;
                    Totals.CombatFoodGained += Math.Max(0, foodDelta);
                    Totals.CombatFoodLost += Math.Max(0, -foodDelta);
                    if (record.ActorKind == UnitKind.Band && playerBands.Contains(record.ActorId) && record.TargetKind == UnitKind.Animal)
                    {
                        Totals.HuntAttempts++; Totals.AnimalsTaken += record.TargetCasualties;
                        if (record.FoodRecovered > 0) { Totals.SuccessfulHunts++; Totals.FoodHunted += record.FoodRecovered; }
                    }
                    int domesticLost = 0;
                    if (record.ActorKind == UnitKind.Animal && owned.Contains(record.ActorId))
                    {
                        domesticLost += record.ActorCasualties;
                        if (record.ActorCountBefore > 0 && record.ActorCountAfter == 0) Totals.DomesticGroupsLost++;
                    }
                    if (record.TargetKind == UnitKind.Animal && owned.Contains(record.TargetId))
                    {
                        domesticLost += record.TargetCasualties;
                        if (record.TargetCountBefore > 0 && record.TargetCountAfter == 0) Totals.DomesticGroupsLost++;
                    }
                    Totals.DomesticAnimalsLost += domesticLost;
                    if (domesticLost > 0 && !record.PlayerInvolved) Totals.Battles++;
                    List<string> impact = new List<string>();
                    impact.Add("Attacker: " + record.ActorCasualties + " lost, " + Number(record.DamageToActor) + " damage");
                    impact.Add("Defender: " + record.TargetCasualties + " lost, " + Number(record.DamageToTarget) + " damage");
                    if (foodDelta != 0) impact.Add((foodDelta > 0 ? "+" : "") + Number(foodDelta) + " provision capacity");
                    if (domesticLost > 0) impact.Add(domesticLost + " of your domestic animals lost");
                    AddMobileNotice(game, record, StoryNoticeKind.Combat,
                        record.Detail,
                        String.Join(". ", impact) + ".", "Inspect the survivors and their wounds before another encounter. Retreat changes where a group can be found; strength and outcomes are never guaranteed.", record.PlayerInvolved || domesticLost > 0);
                    if (record.ActorKind == UnitKind.Band && playerBands.Contains(record.ActorId))
                    {
                        Beast herd = record.TargetKind == UnitKind.Animal ? game.Beasts.FirstOrDefault(b => b.Id == record.TargetId) : null;
                        int otherId = record.TargetKind == UnitKind.Band ? record.TargetId : herd != null && herd.Domestic ? herd.OwnerId : -1;
                        if (otherId >= 0 && !playerBands.Contains(otherId) && !before.HostileBands.Contains(otherId) && newFeuds.Add(otherId) && EncounterRules.BandsHostile(game, record.ActorId, otherId))
                        {
                            Band other = game.Bands.FirstOrDefault(b => b.Id == otherId);
                            bool known = record.TargetKind == UnitKind.Band || other != null && game.Explored.Contains(other.CellId);
                            AddMobileNotice(game, record, StoryNoticeKind.Diplomacy,
                                known && other != null ? "The attack begins a feud with " + other.Name + "." : "The attack begins a feud with this herd's household.",
                                "The peoples are now hostile. Their units can attack one another when they meet.",
                                "Inspect known bands and consider their strength before approaching hostile ground.", false, "An attack becomes a feud", known ? otherId : -1);
                        }
                    }
                }
                else if (record.Kind == EncounterKind.Befriend)
                {
                    if (record.ActorKind == UnitKind.Band && playerBands.Contains(record.ActorId))
                    {
                        Totals.AnimalEncounters++;
                        if (record.TrustAfter <= record.TrustBefore) Totals.FailedAnimalEncounters++;
                    }
                    Beast animal = game.Beasts.FirstOrDefault(b => b.Id == record.TargetId);
                    bool formedLineage = animal != null && animal.Domestic && playerBands.Contains(animal.OwnerId) && !owned.Contains(animal.Id);
                    // The founding notice below carries a successful final encounter.
                    // A casualty still needs its own visible account.
                    if (!formedLineage || lost > 0)
                        AddMobileNotice(game, record, StoryNoticeKind.AnimalEncounter,
                            record.ActorName + " approaches " + record.TargetName + ". " + record.Detail,
                            "Trust " + record.TrustBefore + " \u2192 " + record.TrustAfter + " / " + record.TrustThreshold + "; chance at approach " + Number(record.FriendChance * 100) + "%. " +
                                Number(record.OfferingPaid) + " capacity offered" + (lost > 0 ? "; " + lost + " people lost." : "."),
                            "Trust belongs to this particular group and follows it when it moves. Read its current difficulty and wait until another chapter before approaching again.", lost > 0);
                }
                else if (record.Kind == EncounterKind.Enabled)
                    AddMobileNotice(game, record, StoryNoticeKind.Introduction,
                        "Animal groups and independent peoples now act as moving units in this continuing story.",
                        "Existing people, relationships and chapter history are retained. Future encounters use the new rules.",
                        "Select an animal banner to choose Attack or Befriend, or another band's banner to inspect an attack. Read its chances and strength. Groups can move or retreat, and trust stays with the same group.", true);
                else if (record.Kind == EncounterKind.Release)
                {
                    bool formerCompanion = owned.Contains(record.ActorId);
                    if (formerCompanion) { Totals.DomesticAnimalsReleased += record.ActorCountAfter; Totals.DomesticGroupsReleased++; }
                    AddMobileNotice(game, record, StoryNoticeKind.AnimalMovement,
                        record.ActorName + " survives without its former household and becomes an independent group.",
                        record.ActorCountAfter + " living animals; " + record.TrustAfter + " remembered positive contacts. The same group can range through the land again.",
                        "Independence preserves the surviving animals and their identity. It is distinct from animals lost in combat.", formerCompanion);
                }
                else if (record.Kind == EncounterKind.Move || record.Kind == EncounterKind.Retreat)
                {
                    bool animal = record.ActorKind == UnitKind.Animal;
                    bool relevantAnimal = animal && (owned.Contains(record.ActorId) || before.Animals.Any(a => a.Id == record.ActorId && a.Contacts > 0) ||
                        record.FromCell == before.CellId || record.ToCell == game.Player.CellId);
                    if (!relevantAnimal && !(record.PlayerInvolved && record.Kind == EncounterKind.Retreat)) continue;
                    bool knownDestination = record.ToCell >= 0 && game.Explored.Contains(record.ToCell);
                    string destination = knownDestination ? " reaches " + game.Place(record.ToCell) + "." : " passes beyond the remembered paths.";
                    string impact = record.FromCell == record.ToCell ? "The group remains in place." : "Its position changes; its identity and surviving relationships remain.";
                    if (foodDelta < 0) impact += " " + Number(-foodDelta) + " travel capacity used.";
                    AddMobileNotice(game, record, animal ? StoryNoticeKind.AnimalMovement : StoryNoticeKind.Migration,
                        record.ActorName + destination, impact, "Find the same banner in known land to inspect it again. Movement does not create a new group or reset its trust.", false);
                }
            }
            return receipt;
        }

        private void AddMobileNotice(Game game, EncounterRecord record, StoryNoticeKind kind, string body, string impact, string advice, bool important, string title = null, int targetBandOverride = -1)
        {
            int cell = record.CellId >= 0 && game.Explored.Contains(record.CellId) ? record.CellId :
                record.FromCell >= 0 && game.Explored.Contains(record.FromCell) ? record.FromCell : -1;
            if ((record.Kind == EncounterKind.Move || record.Kind == EncounterKind.Retreat) && record.ToCell >= 0 && game.Explored.Contains(record.ToCell)) cell = record.ToCell;
            int actorBand = record.ActorKind == UnitKind.Band ? record.ActorId : -1;
            int targetBand = record.TargetKind == UnitKind.Band ? record.TargetId : -1;
            if (targetBandOverride >= 0) targetBand = targetBandOverride;
            if (record.Kind == EncounterKind.Release && targetBand != game.Player.Id && !game.Bands.Any(b => b.Id == targetBand && game.Explored.Contains(b.CellId))) targetBand = -1;
            int animal = record.TargetKind == UnitKind.Animal && record.TargetId >= 0 ? record.TargetId : record.ActorKind == UnitKind.Animal ? record.ActorId : -1;
            Notices.Add(new StoryNotice(nextId++, record.Turn, cell, kind, title ?? record.Title, body, impact, advice, important,
                record.AnimalKind, record.Id, actorBand, targetBand, animal));
        }

        private static bool Near(double a, double b)
        { return Math.Abs(a - b) <= Math.Max(0.0000001, Math.Max(Math.Abs(a), Math.Abs(b)) * 0.000000001); }

        private void RecordEncounter(Game game, JournalSnapshot before)
        {
            Totals.AnimalEncounters++;
            JournalAnimal target = before.Animals.FirstOrDefault(b => b.Id == before.TameTarget);
            Beast animal = target == null ? null : game.Beasts.FirstOrDefault(b => b.Id == target.Id);
            if (animal == null) return;
            if (animal.PositiveContacts > target.Contacts)
            {
                if (!animal.Domestic)
                    Add(game, StoryNoticeKind.AnimalEncounter, "Trust grows by the fire", "A patient offering brings the " + AnimalName(animal.Kind).ToLowerInvariant() + " closer to your people.",
                        animal.PositiveContacts + " / 10 positive encounters; " + Number(Math.Max(0, before.Food - game.Player.Food)) + " provisions offered.", "Give this group time. It can respond to another encounter in a later chapter.", false, animal.Kind);
            }
            else
            {
                Totals.FailedAnimalEncounters++;
                Add(game, StoryNoticeKind.AnimalEncounter, "The animals keep their distance", "The " + AnimalName(animal.Kind).ToLowerInvariant() + " reject the offering. The relationship does not advance.",
                    Number(Math.Max(0, before.Food - game.Player.Food)) + " provisions used; trust remains " + animal.PositiveContacts + " / 10.", "Wait until another chapter before approaching this group. Keep enough food for the band as well as the offering.", false, animal.Kind);
            }
        }

        private void RecordFission(Game game, JournalSnapshot before, double foodDelta)
        {
            int departed = before.Population - game.Player.Population;
            Band daughter = game.Bands.Where(b => b.Id != game.Player.Id && b.Population > 0 && game.Explored.Contains(b.CellId) && !before.Bands.Any(p => p.Id == b.Id)).OrderByDescending(b => b.Id).FirstOrDefault();
            Totals.DaughterBandsFounded++; Totals.PopulationDeparted += departed; Totals.FoodShared += Math.Max(0, -foodDelta);
            if (daughter != null) encounteredBands.Add(daughter.Id);
            Add(game, StoryNoticeKind.Fission, "A daughter hearth sets out", (daughter == null ? "A daughter band" : daughter.Name) + " departs with " + departed + " people, carrying the parent language into another place.",
                Number(Math.Max(0, -foodDelta)) + " provisions shared. " + game.Player.Population + " people remain in your band.", "This is a new polity with shared ancestry. It makes its own choices; separation may eventually leave a trace in its speech.", true, null);
        }

        private void RecordDiscoveries(Game game, JournalSnapshot before)
        {
            foreach (Milestone milestone in game.Knowledge.Where(k => k.Known && !before.Knowledge.Contains(k.Id)))
            {
                Totals.Discoveries++;
                string effect = KnowledgeEffect(milestone.Id);
                if (game.LivestockEnabled && milestone.Id == "dogs")
                    effect = "Your people have learned to establish dogs from wolves. Dogs improve " +
                        (game.Rules == SimulationRules.MobileUnits ? "strength when attacking animals" : "hunting chances") +
                        " and need food for care; they do not produce food or improve gathering.";
                else if (game.LivestockEnabled && milestone.Id == "herds")
                    effect = "Your people have learned to keep livestock. Cattle and goats provide milk in proportion to herd size; slaughter provides meat while reducing future milk and care.";
                Add(game, StoryNoticeKind.Knowledge, milestone.Name, "Remembered practice becomes knowledge shared by your people.",
                    effect, milestone.Description, milestone.Id != "dogs" && milestone.Id != "herds", null);
            }
        }

        private void RecordDomesticGroups(Game game, JournalSnapshot before)
        {
            foreach (Beast animal in game.Beasts.Where(b => b.Domestic && b.OwnerId == game.Player.Id && b.Count > 0 &&
                !before.Animals.Any(a => a.Id == b.Id && a.Domestic && a.OwnerId == game.Player.Id)).OrderBy(b => b.Id))
            {
                if (!foundedDomesticGroups.Add(animal.Id)) continue;
                Totals.DomesticGroupsFounded++;
                if (game.LivestockEnabled) { RecordLivestockDomestication(game, game.Player, animal); continue; }
                DomesticEconomy effects = BandEconomy.DomesticEffects(game, game.Player);
                string name = String.IsNullOrEmpty(animal.BreedName) ? AnimalName(animal.Kind) : animal.BreedName;
                List<string> impact = new List<string>();
                if (game.Rules == SimulationRules.MobileUnits)
                {
                    impact.Add(effects.Dogs + " dogs, " + effects.Cattle + " cattle, " + effects.OtherCompanions + " other companions");
                    impact.Add("Current band strength: " + Number(EncounterRules.Band(game, game.Player).Strength));
                    if (effects.GatheringMultiplier > 1) impact.Add("All companions: +" + Number((effects.GatheringMultiplier - 1) * 100) + "% gathering before rounding");
                }
                else if (effects.Dogs > 0)
                {
                    impact.Add(effects.Dogs + " hearth dogs: +" + Number(effects.HuntingBonus * 100) + " percentage points to hunting before any chance limit");
                    if (effects.GatheringMultiplier > 1) impact.Add("+" + Number((effects.GatheringMultiplier - 1) * 100) + "% gathering before rounding");
                }
                if (effects.Cattle > 0) impact.Add(effects.Cattle + " cattle: +" + Number(effects.CattleFood) + " provisions each chapter");
                impact.Add("All animal care: " + Number(effects.AnimalCare) + " provisions each chapter");
                string title = animal.Kind == BeastKind.Wolves ? "Companions by the fire" : animal.Kind == BeastKind.Aurochs ? "A herd walks with your people" : "New companions share the paths";
                string body = name + " becomes a domestic lineage of " + animal.Count + " animals after " + animal.PositiveContacts + " positive encounters.";
                string advice = "The animal ledger shows the living lineage and its care. These are the current totals for all animals belonging to your band; numbers change as the herds grow or decline.";
                EncounterRecord encounter = game.Rules == SimulationRules.MobileUnits ? game.Encounters.Records.Skip(before.EncounterCount).LastOrDefault(r => r.VisibleToPlayer && r.Kind == EncounterKind.Befriend && r.TargetId == animal.Id) : null;
                if (encounter != null) AddMobileNotice(game, encounter, StoryNoticeKind.Domestication, body + " The final approach had a " + Number(encounter.FriendChance * 100) + "% chance.", String.Join(". ", impact) + ".", advice, true, title);
                else Add(game, StoryNoticeKind.Domestication, title, body, String.Join(". ", impact) + ".", advice, true, animal.Kind);
            }
        }

        private void RecordContacts(Game game)
        {
            foreach (Band band in game.Bands.Where(b => b.Id != game.Player.Id && b.Population > 0 && game.Explored.Contains(b.CellId)).OrderBy(b => b.Id))
            {
                if (!encounteredBands.Add(band.Id)) continue;
                Add(game, StoryNoticeKind.Contact, "Other hearths enter the story", "Your remembered world now includes " + band.Name + ".", band.Population + " people; " + (band.Settled ? "a settled hearth." : "a wandering band."),
                    "Their emblem marks their presence. People, language and ancestry have separate histories.", true, null, band.CellId);
            }
        }

        private void RecordPlaceKnowledge(Game game, JournalSnapshot before)
        {
            if (!game.CulturalPlaceNames) return;
            if (!before.CulturalPlaces)
                Add(game, StoryNoticeKind.PlaceNames, "The remembered land takes names", "Your people now record a name for every hex in their remembered map.",
                    game.KnownPlaces(game.Player.Id).Count + " places named from the land already known.",
                    "Past discoveries have no recorded source in this older story, so these first names are coined in your tongue now. New discoveries and peaceful exchanges will keep their origins. Select a hex and open Names to read its account.", true, null);
            var earlier = new HashSet<int>(before.NamedPlaces);
            var learned = game.KnownPlaces(game.Player.Id).Where(p => !earlier.Contains(p.CellId) && p.Acquisition == PlaceAcquisition.Shared)
                .GroupBy(p => p.LearnedFromBandId).OrderBy(p => p.Key);
            bool first = before.SharedPlaceCount == 0;
            foreach (var group in learned)
            {
                Band donor = game.Bands.FirstOrDefault(b => b.Id == group.Key);
                PlaceKnowledge example = group.First();
                Add(game, StoryNoticeKind.PlaceNames, "Names arrive from another hearth", (donor == null ? "Your guides" : donor.Name) + " shared names for " + group.Count() + " places your people had not known, including " + example.Name + ".",
                    "These places are now on your map. Their foreign names are kept exactly as heard.",
                    "Names already known to your people stay unchanged. A later visit keeps the borrowed name; independent discovery creates a name in your own tongue. Read the selected hex's Names tab for its source.", first, null, example.CellId);
                first = false;
            }
        }

        private void RecordSalt(Game game, JournalSnapshot before, string command, bool ended, bool enabled)
        {
            if (!game.SaltEnabled) return;
            Band band = game.Player;
            string guidance = "Move onto a known salt spring or coastal deposit, then choose Gather salt in Orders. Inspect opens Economy / Resources with known source locations.";
            if (enabled)
            {
                Totals.SaltAllocated += band.Salt;
                Add(game, StoryNoticeKind.Salt, "Salt enters the household ledger", "Your earlier history is retained. From this turn onward, each living band needs salt as well as food.",
                    "Your people receive " + Number(band.Salt) + " salt, enough for four turns at the current population. Nearby sources are available in the remembered land.",
                    guidance, true, null);
                return;
            }
            if (command == "salt" && band.Salt > before.Salt)
            {
                double gained = band.Salt - before.Salt;
                Totals.SaltGatherActions++; Totals.SaltGathered += gained;
                if (Totals.SaltGatherActions == 1 || before.SaltShortageTurns > 0 && before.SaltReserve < 1)
                    Add(game, StoryNoticeKind.Salt, "Salt returns to the hearth", "Your people collect salt at " + game.Place(band.CellId) + ".",
                        "+" + Number(gained) + " salt. The reserve now covers " + Number(SaltEconomy.ReserveTurns(band)) + " turns at the current population." +
                        (band.SaltShortageTurns > 0 ? " The shortage ends after a fully supplied turn closes." : ""),
                        "Salt travels with your band and is shared when a daughter band departs. Watch the reserve as your population grows; collection uses one action.", false, null);
            }
            if (command == "split") Totals.SaltShared += Math.Max(0, before.Salt - band.Salt);
            if (!ended || game.IsOver) return;
            if (band.SaltShortageTurns == 0 && before.SaltShortageTurns > 0)
                Add(game, StoryNoticeKind.Salt, "The salt shortage ends", "A full turn's salt demand has been met. Your people can gather at their usual strength again.",
                    "Salt no longer prevents births or reduces gathering. The reserve holds " + Number(band.Salt) + ", enough for " + Number(SaltEconomy.ReserveTurns(band)) + " turns at the current population.",
                    "Cohesion rebuilds through supplied turns. Keep a reserve before leaving this source for distant land.", false, null);
            else if (band.SaltShortageTurns > 0 && band.SaltShortageTurns <= 3)
                Add(game, StoryNoticeKind.Salt, band.SaltShortageTurns == 1 ? "The salt reserve runs dry" : "The salt shortage deepens",
                    "The band could not meet its salt demand as the turn closed. This is deficient turn " + band.SaltShortageTurns + ".",
                    "Gathering is now " + Number(SaltEconomy.GatheringMultiplier(band) * 100) + "% of its otherwise available yield. Cohesion falls and births pause. Life losses begin on the fourth consecutive deficient turn.",
                    guidance, true, null);
            else if (band.SaltShortageTurns == 0 && before.SaltReserve > 2 && SaltEconomy.ReserveTurns(band) <= 2)
                Add(game, StoryNoticeKind.Salt, "The salt reserve is running low", "There is still time to replenish your people's salt before a shortage begins.",
                    Number(band.Salt) + " salt remains, covering " + Number(SaltEconomy.ReserveTurns(band)) + " turns at the current population. Demand is " + Number(SaltEconomy.Need(band)) + " per turn.",
                    guidance, true, null);
        }

        private void Add(Game game, StoryNoticeKind kind, string title, string body, string impact, string advice, bool important, BeastKind? animal, int cell = -1)
        { Notices.Add(new StoryNotice(nextId++, game.Turn, cell < 0 ? game.Player.CellId : cell, kind, Clio.Desktop.Timeline.Text(game, title), Clio.Desktop.Timeline.Text(game, body), Clio.Desktop.Timeline.Text(game, impact), Clio.Desktop.Timeline.Text(game, advice), important, animal)); }
        private static string AnimalName(BeastKind kind)
        { return kind == BeastKind.Aurochs ? "Aurochs" : kind == BeastKind.Wolves ? "Wolves" : kind == BeastKind.Mammoths ? "Mammoths" : kind == BeastKind.Deer ? "Deer" : kind == BeastKind.Goats ? "Goats" : "Dragon"; }
        private static string Number(double number) { return number.ToString("0.#", CultureInfo.InvariantCulture); }
        private static string KnowledgeEffect(string id)
        {
            switch (id)
            {
                case "routes": return "Travel provisions fall from 15% to 8% of the band's population for each move.";
                case "gathering": return "Gathering yields increase by 15%, before the final rounding and other factors.";
                case "tracking": return "Hunting success gains 10 percentage points before the applicable chance limit.";
                case "stores": return "Food spoilage falls from 5% to 2% at the close of a chapter.";
                case "hearth": return "The band has learned to sustain a hearth. Together with food keeping, it opens the way toward tended ground.";
                case "dogs": return "Repeated positive encounters have established a practice of building trust with wolves. See the animal ledger for any domestic lineage and its current effects.";
                case "herds": return "Repeated positive encounters have established a practice of building trust with aurochs. See the animal ledger for any domestic lineage and its current effects.";
                case "gardens": return "A camp's passive food rises from 30% to 75% of its current gathering yield.";
                default: return "A new shared practice is recorded in the knowledge ledger.";
            }
        }
    }
}
