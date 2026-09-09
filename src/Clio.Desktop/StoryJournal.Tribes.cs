using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using Clio.Simulation;

namespace Clio.Desktop
{
    internal sealed class JournalHousehold
    {
        internal readonly int Id, Population, Actions, CellId, SaltShortageTurns;
        internal readonly double Food, Salt, SaltReserve, UnassistedGathering, Wood, WoodReserve;
        internal readonly bool Settled;
        internal JournalHousehold(Game game, Band band)
        {
            Id = band.Id; Population = band.Population; Actions = game.ActionsFor(band.Id); CellId = band.CellId;
            Food = band.Food; Salt = band.Salt; Settled = band.Settled; SaltShortageTurns = band.SaltShortageTurns;
            SaltReserve = SaltEconomy.ReserveTurns(band); UnassistedGathering = BandEconomy.UnassistedForageYield(game, band.CellId, band);
            Wood = game.WoodEnabled ? band.Wood : 0; WoodReserve = game.WoodEnabled ? WoodEconomy.ReserveTurns(band) : 0;
        }
    }

    internal sealed partial class StoryJournal
    {
        private List<StoryNotice> RecordTribal(Game game, JournalSnapshot before, string command, string result)
        {
            int first = Notices.Count;
            bool ended = command == "end" && game.Turn > before.Turn;
            bool acted = before.Households.Any(b => game.ActionsFor(b.Id) < b.Actions);
            TribeEvent[] events = game.TribeEvents.Skip(before.TribeEventCount).Where(e => e.VisibleToPlayer).ToArray();
            if (!ended && !acted && events.Length == 0 && game.GatheringEvents.Count == before.GatheringEventCount) return new List<StoryNotice>();
            int actorId = game.Player.Id; string order = command;
            if (command.StartsWith("band:", StringComparison.Ordinal))
            {
                int colon = command.IndexOf(':', 5);
                if (colon > 5) { actorId = Int32.Parse(command.Substring(5, colon - 5), CultureInfo.InvariantCulture); order = command.Substring(colon + 1); }
            }
            Band actor = game.Bands.Find(b => b.Id == actorId);
            JournalHousehold priorActor = before.Households.FirstOrDefault(b => b.Id == actorId);
            double foodBefore = before.Households.Sum(b => b.Food), saltBefore = before.Households.Sum(b => b.Salt);
            int populationBefore = before.Households.Sum(b => b.Population);
            double foodAfter = game.ControlledBands.Sum(b => b.Food), saltAfter = game.ControlledBands.Sum(b => b.Salt);
            double priorIn = Totals.FoodGained, priorOut = Totals.FoodSpent;
            int priorBirths = Totals.Births, priorDeaths = Totals.Deaths, priorDeparted = Totals.PopulationDeparted;
            Totals.Commands++; Totals.NetPopulationChange += game.TribePopulation - populationBefore;
            RecordMobileEncounters(game, before, ended);

            if (ended)
            {
                foreach (TribeEconomyReceipt entry in game.LastTribeEconomy.Where(e => e.Turn == before.Turn && before.Households.Any(b => b.Id == e.BandId)))
                {
                    Band band = game.Bands.Find(b => b.Id == entry.BandId); EconomyForecast receipt = entry.Forecast;
                    if (band == null) continue;
                    Totals.Births += receipt.Births; Totals.Deaths += receipt.HungerLosses + receipt.ExposureLosses + receipt.SaltLosses;
                    Totals.HungerDeaths += receipt.HungerLosses; Totals.ExposureDeaths += receipt.ExposureLosses; Totals.SaltDeaths += receipt.SaltLosses;
                    Totals.CampFoodProduced += receipt.CampFood; Totals.CattleFoodProduced += receipt.CattleFood;
                    Totals.AnimalCarePaid += receipt.ActualAnimalCare; Totals.FoodSpoiled += receipt.Spoilage;
                    double supplied = Math.Min(receipt.Upkeep, Math.Max(0, receipt.StartingFood + receipt.CampFood + receipt.CattleFood - receipt.ActualAnimalCare));
                    Totals.FoodConsumed += supplied; Totals.FoodGained += receipt.CampFood + receipt.CattleFood;
                    Totals.FoodSpent += supplied + receipt.ActualAnimalCare + receipt.Spoilage;
                    Totals.SaltConsumed += receipt.SaltConsumed;
                    if (receipt.HungerLosses > 0) AddTribalNotice(game, band, StoryNoticeKind.Hunger, "The stores run short",
                        band.Name + " loses " + receipt.HungerLosses + (receipt.HungerLosses == 1 ? " person" : " people") + " to hunger.", "This household could not meet its food needs. " + receipt.EndingPopulation + " people remain.",
                        "Each band keeps its own provisions. Select this band, then gather or seek richer ground before ending the turn.", true);
                    if (receipt.ExposureLosses > 0) AddTribalNotice(game, band, StoryNoticeKind.Exposure, "The cold reaches the hearth",
                        band.Name + " loses " + receipt.ExposureLosses + (receipt.ExposureLosses == 1 ? " person" : " people") + " to exposure.", "Cold ground and a lack of shelter take their toll.",
                        "Raise a camp for this band or move it to warmer ground.", true);
                    if (receipt.SaltLosses > 0) AddTribalNotice(game, band, StoryNoticeKind.Salt, "The long salt shortage takes lives",
                        band.Name + " loses " + receipt.SaltLosses + (receipt.SaltLosses == 1 ? " person" : " people") + " after repeated salt deficits.", "Deficient turn " + receipt.EndingSaltShortageTurns + ". " + receipt.EndingPopulation + " people remain in this band.",
                        "Every band carries its own salt. Reach a source and collect enough for a full turn's demand.", true);
                    JournalHousehold old = before.Households.First(b => b.Id == band.Id);
                    RecordTribalSalt(game, band, old, "end");
                    if (receipt.Births > 0 && (priorBirths == 0 || priorBirths / 25 != Totals.Births / 25))
                        AddTribalNotice(game, band, StoryNoticeKind.Growth, "New voices within the tribe", band.Name + " welcomes " + receipt.Births + " new members.",
                            game.TribePopulation + " people remain within your tribe.", "Growth increases that band's food and salt needs.", false);
                }
                foreach (JournalHousehold previous in before.Households)
                {
                    Band band = game.Bands.Find(b => b.Id == previous.Id);
                    if (band != null && band.Population <= 0) { Totals.FoodSpent += band.Food; Totals.SaltLost += band.Salt; }
                }
                Timeline.Add(new HistoryPoint(game));
            }
            else
            {
                Totals.FoodGained += Math.Max(0, foodAfter - foodBefore); Totals.FoodSpent += Math.Max(0, foodBefore - foodAfter);
                if (actor != null && priorActor != null)
                {
                    if (order == "forage")
                    {
                        double gained = Math.Max(0, actor.Food - priorActor.Food);
                        Totals.GatherActions++; Totals.FoodGathered += gained;
                        Totals.CompanionGatheringFood += Math.Max(0, gained - priorActor.UnassistedGathering);
                    }
                    if (order == "camp" && actor.Settled && !priorActor.Settled)
                    {
                        Totals.CampsFounded++;
                        AddTribalNotice(game, actor, StoryNoticeKind.Camp, "A hearth within the tribe", actor.Name + " raises a camp at " + game.Place(actor.CellId) + ".",
                            "The camp contributes food and offers shelter to this band." + (game.WoodEnabled ? " Construction used " + Number(WoodEconomy.CampCost) + " wood and 30 food." : ""), "Other bands keep their own households. Your leader's current hex remains the reunion place.", Totals.CampsFounded == 1);
                    }
                    RecordTribalSalt(game, actor, priorActor, order);
                    if (actor.Population <= 0) Totals.SaltLost += actor.Salt;
                }
            }

            foreach (TribeEvent entry in events)
            {
                if (entry.Kind == TribeEventKind.Split)
                {
                    Totals.DaughterBandsFounded++;
                    encounteredBands.Add(entry.BandId);
                }
                if (entry.Kind == TribeEventKind.Reunion) Totals.TribalReunions++;
                if (entry.Kind == TribeEventKind.Secession)
                {
                    Totals.TribalSecessions++; Totals.PopulationDeparted += entry.Population;
                    Totals.FoodShared += entry.Food; Totals.SaltShared += entry.Salt;
                    if (ended) Totals.FoodSpent += entry.Food;
                }
                string impact = entry.Kind == TribeEventKind.Split ? entry.Population + " people, " + Number(entry.Food) + " provisions and " + Number(entry.Salt) + " salt now form another controlled household. Your tribe loses no people or supplies." :
                    entry.Kind == TribeEventKind.Secession ? entry.Population + " people leave your control with their household supplies. They remain alive as a related, independent people." :
                    entry.Kind == TribeEventKind.Drift ? game.BandPersonalitiesEnabled ? "This band's disposition and distance from the leader determine its remaining time within the tribe. The current deadline is shown in Units." : "Eight turns apart. This band remains controllable for eight more missed turns." :
                    entry.Kind == TribeEventKind.Disposition ? "Every daughter now has a lasting disposition, from loyal to separatist. More independent bands seek space and form new peoples sooner." :
                    entry.Kind == TribeEventKind.Wandering ? "The band used one ordinary journey, paying its movement and provision costs. Its household and companions remain together." :
                    entry.Kind == TribeEventKind.Succession ? "Command continues through the surviving bands. The new leader marks their reunion hex." :
                    entry.Kind == TribeEventKind.Extinction ? "No living band remains in your tribe." :
                    entry.Kind == TribeEventKind.Reunion ? "The band's separation counter returns to zero." : "Each controlled band has its own two actions, food and salt.";
                string advice = entry.Kind == TribeEventKind.Secession ? "Its speech branches from the parent language. Separation does not begin a war; the Units roster now contains only the bands still in your tribe." :
                    entry.Kind == TribeEventKind.Split ? game.BandPersonalitiesEnabled ? "Open Units to inspect the daughter's personality and separation deadline. It can act next turn. Meet the leader on the same hex to renew contact; an order or Hold this turn prevents voluntary wandering." : "Open Units to select the new band. It can act next turn. Meet your leader on the same hex to renew contact before sixteen turns apart." :
                    entry.Kind == TribeEventKind.Disposition || entry.Kind == TribeEventKind.Wandering ? "Give a daughter an order, or choose Hold this turn in Units, to prevent an unassigned journey this turn. To keep it in your tribe, reunite on the leader's hex before its deadline." :
                    entry.Kind == TribeEventKind.Extinction ? "Your final account and history retain the tribe's achievements and losses." :
                    "Open Units to inspect contact and find the leader. Each band must share the leader's hex to renew contact; proximity alone is not a reunion.";
                Notices.Add(new StoryNotice(nextId++, entry.Turn, entry.CellId, StoryNoticeKind.Tribe, entry.Title, entry.Detail, impact, advice,
                    entry.Kind != TribeEventKind.Reunion && entry.Kind != TribeEventKind.Wandering, null, -1, entry.BandId, entry.OtherBandId, -1));
            }
            RecordGatherings(game, before);
            RecordWood(game, before, command, ended);
            int explained = Totals.Births - priorBirths - (Totals.Deaths - priorDeaths) - (Totals.PopulationDeparted - priorDeparted);
            if (game.TribePopulation - populationBefore != explained) Totals.UnresolvedPopulationChanges += game.TribePopulation - populationBefore - explained;
            double recordedFood = Totals.FoodGained - priorIn - (Totals.FoodSpent - priorOut);
            if (!Near(recordedFood, foodAfter - foodBefore))
            {
                Totals.UnresolvedEconomyChapters++;
                double residual = foodAfter - foodBefore - recordedFood;
                Totals.FoodGained += Math.Max(0, residual); Totals.FoodSpent += Math.Max(0, -residual);
            }
            RecordDiscoveries(game, before); RecordPlaceKnowledge(game, before);
            RecordTribalCompanions(game, before);
            foreach (Band known in game.Bands.Where(b => b.Population > 0 && !game.CanControlBand(b.Id) && game.Explored.Contains(b.CellId)).OrderBy(b => b.Id))
                if (encounteredBands.Add(known.Id)) AddTribalNotice(game, known, StoryNoticeKind.Contact, "Other hearths enter the story", "Your remembered world includes " + known.Name + ".",
                    known.Population + " people live in this separate polity.", "Inspect their banner before approaching. Shared ancestry does not make them part of your tribe.", false);
            List<StoryNotice> added = Notices.Skip(first).ToList();
            LastCommandNotices = new ReadOnlyCollection<StoryNotice>(added.ToArray());
            LastCommandPopulationBefore = populationBefore; LastCommandPopulationAfter = game.TribePopulation;
            return added;
        }

        private void RecordTribalSalt(Game game, Band band, JournalHousehold before, string order)
        {
            if (!game.SaltEnabled) return;
            string advice = "Select this band and move to a known coastal salt deposit or spring. Gather salt there; supply a full turn's demand to end the shortage.";
            if (order == "salt" && band.Salt > before.Salt)
            {
                double gained = band.Salt - before.Salt; Totals.SaltGatherActions++; Totals.SaltGathered += gained;
                if (Totals.SaltGatherActions == 1 || before.SaltShortageTurns > 0 && before.SaltReserve < 1)
                    AddTribalNotice(game, band, StoryNoticeKind.Salt, "Salt returns to the hearth", band.Name + " gathers " + Number(gained) + " salt.",
                        "Reserve: " + Number(SaltEconomy.ReserveTurns(band)) + " turns at the current population. Recovery follows a fully supplied close.", advice, false);
            }
            if (order != "end" || band.Population <= 0) return;
            if (band.SaltShortageTurns == 0 && before.SaltShortageTurns > 0)
                AddTribalNotice(game, band, StoryNoticeKind.Salt, "The salt shortage ends", band.Name + " meets a full turn's salt demand.",
                    "Normal gathering resumes and salt no longer prevents births. Cohesion recovers through supplied turns.", advice, false);
            else if (band.SaltShortageTurns > 0 && band.SaltShortageTurns <= 3)
                AddTribalNotice(game, band, StoryNoticeKind.Salt, band.SaltShortageTurns == 1 ? "The salt reserve runs dry" : "The salt shortage deepens",
                    band.Name + " could not meet its salt demand. This is deficient turn " + band.SaltShortageTurns + ".",
                    "Gathering: " + Number(SaltEconomy.GatheringMultiplier(band) * 100) + "%. Cohesion falls, births pause, and losses begin on the fourth deficient turn.", advice, true);
            else if (band.SaltShortageTurns == 0 && before.SaltReserve > 2 && SaltEconomy.ReserveTurns(band) <= 2)
                AddTribalNotice(game, band, StoryNoticeKind.Salt, "The salt reserve is running low", band.Name + " needs to replenish its salt soon.",
                    "Reserve: " + Number(band.Salt) + ", enough for " + Number(SaltEconomy.ReserveTurns(band)) + " turns at the current population.", advice, true);
        }

        private void RecordTribalCompanions(Game game, JournalSnapshot before)
        {
            foreach (Beast animal in game.Beasts.Where(b => b.Domestic && b.Count > 0 && game.CanControlBand(b.OwnerId) &&
                !before.Animals.Any(a => a.Id == b.Id && a.Domestic && a.OwnerId == b.OwnerId)).OrderBy(b => b.Id))
            {
                if (!foundedDomesticGroups.Add(animal.Id)) continue;
                Totals.DomesticGroupsFounded++;
                Band owner = game.Bands.Find(b => b.Id == animal.OwnerId); DomesticEconomy support = BandEconomy.DomesticEffects(game, owner);
                Notices.Add(new StoryNotice(nextId++, game.Turn, animal.CellId, StoryNoticeKind.Domestication, "New companions share the paths",
                    animal.Count + " " + AnimalName(animal.Kind).ToLowerInvariant() + " form a domestic lineage with " + owner.Name + ".",
                    "All of this band's companions: +" + Number((support.GatheringMultiplier - 1) * 100) + "% gathering; " + Number(support.CattleFood) + " cattle provisions; " + Number(support.AnimalCare) + " care each turn.",
                    "Companions follow and support their owning band. Inspect the lineage in Units.", true, animal.Kind, -1, owner.Id, -1, animal.Id));
            }
        }

        private void AddTribalNotice(Game game, Band band, StoryNoticeKind kind, string title, string body, string impact, string advice, bool important)
        {
            Notices.Add(new StoryNotice(nextId++, game.Turn, game.Explored.Contains(band.CellId) ? band.CellId : -1, kind,
                title, body, impact, advice, important, null, -1, band.Id, -1, -1));
        }
    }
}
