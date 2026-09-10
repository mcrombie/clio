using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Clio.Simulation;

namespace Clio.Desktop
{
    internal enum StoryDirective { Provision, SeekSalt, Explore, Reunite, Settle, Divide, Befriend, Defend, GatherWood, HarvestMeat }

    internal sealed class StoryOption
    {
        internal readonly StoryDirective Directive;
        internal readonly string Title, Description, Consequences;
        internal readonly int DurationTurns;
        internal StoryOption(StoryDirective directive, string title, string description, string consequences, int durationTurns = 3)
        { Directive = directive; Title = title; Description = description; Consequences = consequences; DurationTurns = durationTurns; }
    }

    internal sealed class StoryEvent
    {
        internal readonly string Key, Title, Story, Context, Icon;
        internal readonly CounsellorId Voice;
        internal readonly StoryOption[] Options;
        internal readonly bool Urgent;
        internal StoryEvent(string key, string title, string story, string context, string icon, CounsellorId voice, bool urgent, params StoryOption[] options)
        { Key = key; Title = title; Story = story; Context = context; Icon = icon; Voice = voice; Urgent = urgent; Options = options; }
    }

    /// <summary>
    /// A council agenda and an ordinary-command steward. Reading either function
    /// changes no game state and draws no random numbers. Decisions are priorities,
    /// not free resources: the controller executes and journals every actual order.
    /// All geographic targets and foreign subjects come from the player's known map.
    /// </summary>
    internal static class StoryDecisionPolicy
    {
        internal static StoryEvent Create(Game game, string previousKey)
        {
            if (game == null) throw new ArgumentNullException("game");
            Band[] bands = game.ControlledBands.ToArray();
            if (game.IsOver || bands.Length == 0) return null;
            Band leader = game.TribeLeaderBand ?? bands[0];
            Band hungry = bands.OrderBy(b => FoodTurns(game, b)).ThenBy(b => b.Id).First();
            Band salt = game.SaltEnabled ? bands.OrderBy(SaltEconomy.ReserveTurns).ThenBy(b => b.Id).First() : null;
            Band threatened = bands.FirstOrDefault(b => NearbyEnemies(game, b).Any() || NearbyAnimals(game, b).Any(a => EncounterRules.Animal(game, a).Hostile));
            Band distant = game.TribesEnabled ? bands.Where(b => !game.TribeStatus(b.Id).IsLeader && b.CellId != leader.CellId)
                .OrderByDescending(b => game.TribeStatus(b.Id).TurnsAway).ThenBy(b => b.Id).FirstOrDefault() : null;

            if (FoodTurns(game, hungry) < 1.4)
            {
                List<StoryOption> foodOptions = new List<StoryOption> {
                    Option(StoryDirective.Provision, "Gather food", "Make food gathering the priority.", "Gather repeatedly; move to richer known ground when necessary. Gathering builds reserves but depletes the land."),
                    Option(StoryDirective.Explore, "Find better land", "Feed hungry bands first, then explore.", "Gather first if hunger is imminent, then move toward unexplored areas. Travel costs food and actions; richer ground is not guaranteed.")
                };
                Beast herd = OwnLivestock(game, hungry);
                if (herd != null) foodOptions.Add(MeatOption());
                return Event("hunger:" + hungry.Id, "Food is running low", hungry,
                    hungry.Name + " has " + Amount(hungry.Food) + " food and needs about " + Amount(Needs(game, hungry)) +
                    " each turn. The economic adviser recommends gathering now. The cultural adviser suggests finding fresh ground if local supplies are depleted." +
                    (herd == null ? "" : " The band also owns livestock: taking meat would provide food now, but leave fewer animals producing milk."),
                    "leaf", CounsellorId.Stores, true, foodOptions.ToArray());
            }

            if (salt != null && SaltEconomy.ReserveTurns(salt) < 2.5)
            {
                bool source = SaltEconomy.KnownSources(game).Any();
                return Event("salt:" + salt.Id, "Salt is running low", salt,
                    salt.Name + " has salt for " + SaltEconomy.ReserveTurns(salt).ToString("0.0", CultureInfo.InvariantCulture) +
                    " turns at its current population. " + (source ? "A known salt source appears on your map. Reaching it will take time away from other work. " : "You have not discovered a salt source yet. ") +
                    "The economic adviser recommends replenishing salt. The military adviser favors continued exploration.",
                    "salt", CounsellorId.Stores, SaltEconomy.ReserveTurns(salt) < 1.5,
                    Option(StoryDirective.SeekSalt, "Gather salt", "Make salt the priority.", "Travel to a safe known source and gather there, building up to six turns of salt. If none is reachable, explore. Travel and gathering use actions."),
                    Option(StoryDirective.Provision, "Food comes first", "Build food reserves before committing to another journey.", "Prioritize food; salt is replenished when nearly exhausted. Delaying salt can weaken gathering and cohesion before relief arrives."),
                    Option(StoryDirective.Explore, "Keep exploring", "Seek new land while monitoring salt reserves.", "Explore known frontiers rather than stockpiling salt early. Emergency shortages still redirect bands; travel exposes them to new conditions."));
            }

            if (threatened != null)
            {
                Band enemy = NearbyEnemies(game, threatened).FirstOrDefault();
                Beast animal = NearbyAnimals(game, threatened).FirstOrDefault(a => EncounterRules.Animal(game, a).Hostile);
                string subject = enemy != null ? enemy.Name : animal.Kind.ToString().ToLowerInvariant();
                string key = "danger:" + threatened.Id + ":" + (enemy != null ? "band" + enemy.Id : "animal" + animal.Id);
                if (Danger(game, threatened) || key != previousKey)
                    return Event(key, "A threat nearby", threatened,
                    threatened.Name + " can see " + subject + " nearby. The military adviser recommends confronting manageable threats. The social adviser warns that combat can cost lives and recommends avoiding a fight.",
                    "hunt", CounsellorId.Watch, Danger(game, threatened),
                    Option(StoryDirective.Defend, "Defend our bands", "Confront weaker hostile groups; retreat from stronger ones.", "Attack only observed, already-hostile targets when the odds are favorable. Combat may wound or kill. Outmatched households retreat instead; neutral peoples are left in peace."),
                    Option(StoryDirective.Explore, "Avoid fighting", "Seek safer land without starting fights.", "Move away from overwhelming threats, then explore. Leaving abandons a camp and spends travel food; a safe route may not be available."));
            }

            if (distant != null && game.TribeStatus(distant.Id).TurnsAway >= Math.Max(2, game.TribeStatus(distant.Id).DriftAfter - 2))
            {
                TribeMembership status = game.TribeStatus(distant.Id);
                return Event("kinship:" + distant.Id, "A band is losing contact", distant,
                    distant.Name + " has spent " + status.TurnsAway + " turns away from the leader. The social adviser recommends a reunion to keep the tribe together. The military adviser favors giving the band room to explore. Continued separation can create an independent people.",
                    "split", CounsellorId.Bonds, status.Drifting,
                    Option(StoryDirective.Reunite, "Reunite the bands", "Keep the leader in place and bring the other bands back.", "Daughter bands take safe known routes to the leader's hex. Meeting restores contact. Travel consumes food and gathering time; blocked routes can delay reunion."),
                    Option(StoryDirective.Explore, "Let them explore", "Allow bands to explore independently.", "Bands explore instead of returning to the leader. Separation can deepen cultural drift and eventually create an independent polity."));
            }

            Band shortOfWood = game.WoodEnabled ? bands.OrderBy(WoodEconomy.ReserveTurns).ThenBy(b => b.Id).First() : null;
            if (shortOfWood != null && WoodEconomy.ReserveTurns(shortOfWood) < 2 && previousKey != "wood:" + shortOfWood.Id)
                return Event("wood:" + shortOfWood.Id, "Firewood is running low", shortOfWood,
                    shortOfWood.Name + " carries " + Amount(shortOfWood.Wood) + " wood and uses " + Amount(WoodEconomy.FuelNeed(shortOfWood)) +
                    " each turn for a fire. Cooking reduces food needs by 10%, and a supplied fire protects against cold. The economic adviser recommends collecting wood. The cultural adviser would keep exploring. Without enough fuel, the band loses the fire's benefits.",
                    "camp", CounsellorId.Stores, false,
                    Option(StoryDirective.GatherWood, "Gather wood", "Collect fuel and materials for future camps.", "Use actions to collect wood, seeking richer known woodland when helpful. Build six turns of fuel plus 10 wood for an unbuilt camp. Food and urgent salt needs still come first."),
                    Option(StoryDirective.Explore, "Keep exploring", "Continue the journey without stockpiling wood.", "Spend actions on travel. Fires consume the remaining wood automatically; when it runs out, food needs rise and unsheltered cold can cause losses."));

            string campCost = game.WoodEnabled ? "30 food, 10 wood and one action" : "30 food and one action";
            List<StoryEvent> possibilities = new List<StoryEvent>();
            Band herder = bands.FirstOrDefault(b => FoodTurns(game, b) < 3 && OwnLivestock(game, b) != null);
            if (herder != null)
            {
                Beast herd = OwnLivestock(game, herder);
                possibilities.Add(Event("livestock:" + herd.Id, "Milk tomorrow or meat today?", herder,
                    herder.Name + " has " + herd.Count + " " + LivestockEconomy.DisplayName(game, herd).ToLowerInvariant() +
                    " producing " + Amount(LivestockEconomy.MilkFood(game, herd)) + " food from milk each turn. " +
                    "The economic adviser would keep the herd for its regular output. The military adviser favors meat supplies for the next journey. Slaughtering animals reduces future milk production.",
                    "camp", CounsellorId.Stores, false,
                    Option(StoryDirective.Provision, "Keep the herd for milk", "Gather food while the herd keeps producing.", "Milk enters food reserves at turn end. Larger herds produce more and need more care. No livestock is deliberately slaughtered under this priority."),
                    MeatOption()));
            }
            Band companionBand = bands.FirstOrDefault(b => BefriendTarget(game, b, false) != null);
            if (companionBand != null)
            {
                Beast companion = BefriendTarget(game, companionBand, false);
                possibilities.Add(Event("companions:" + companion.Id, "Animals worth befriending", companionBand,
                    "A group of " + companion.Kind.ToString().ToLowerInvariant() + " is observed near " + companionBand.Name +
                    ". The cultural adviser recommends trying to befriend them. The economic adviser points out that offerings and future animal care use food. Trust takes repeated contact, and an attempt can fail.",
                    "heart", CounsellorId.Memory, false,
                    Option(StoryDirective.Befriend, "Befriend animals", "Approach manageable animal groups when food is secure.", "Spend offerings and actions on befriending when food is secure. Rejection can cause injuries. Repeated successful contacts may form companions with benefits and ongoing care costs."),
                    Option(StoryDirective.Provision, "Keep food for our people", "Leave the animals alone and strengthen household supplies.", "Use those actions to gather food instead. No offerings are spent and no new trust is deliberately built.")));
            }
            Band founder = bands.FirstOrDefault(b => CanDivide(game, b));
            if (founder != null)
                possibilities.Add(Event("division:" + founder.Id, "Room for another band", founder,
                    founder.Name + " now holds " + founder.Population + " people and " + Amount(founder.Food) +
                    " food. A safe neighboring place is known. The social adviser supports forming a daughter band within the tribe. The economic adviser warns that each band will need its own supplies. Bands that remain apart can eventually become independent.",
                    "split", CounsellorId.Bonds, false,
                    Option(StoryDirective.Divide, "Send out a daughter band", "Let eligible households divide and claim room nearby.", "At 80 people and twice upkeep in food, a band can send one-third of its people and matching supplies to a known neighboring home. Daughters need food, salt and future reunions."),
                    Option(StoryDirective.Settle, "Improve existing camps", "Support the bands you already have.", "Establish camps for " + campCost + " when reserves permit, then gather and rest locally. Keeps people together and favors rootedness over expansion.")));
            Band unsettled = bands.FirstOrDefault(b => !b.Settled && FoodTurns(game, b) >= 2);
            if (unsettled != null)
                possibilities.Add(Event("hearth:" + unsettled.Id, "Make camp or keep moving?", unsettled,
                    unsettled.Name + " is at " + game.Place(unsettled.CellId) + ". The economic adviser recommends making camp for shelter and turn-end food. The cultural adviser favors exploring unknown land. Camps support staying in one place; leaving a camp abandons it.",
                    "camp", CounsellorId.Stores, false,
                    Option(StoryDirective.Settle, "Make camp", "Establish camps where supplies allow.", "Each new camp costs " + campCost + "; it adds food at turn end. Collect missing materials, then build. Leaving later abandons the camp."),
                    Option(StoryDirective.Explore, "Keep exploring", "Move bands toward new land.", "Move toward the known map's unexplored edges, paying food and terrain costs. Movement develops mobility; new land and encounters may be discovered.")));
            if (bands.Length > 1)
                possibilities.Add(Event("council", "Keep the tribe together or spread out?", leader,
                    "Your tribe has " + bands.Length + " bands. Each has separate supplies and actions. The social adviser recommends reuniting with the leader. The military adviser supports spreading out to explore. Long periods apart can lead bands to become independent.",
                    "split", CounsellorId.Bonds, false,
                    Option(StoryDirective.Reunite, "Reunite the bands", "Bring daughter bands back to the leader.", "Keep the leader still and guide daughters to its hex. Meeting renews contact but does not merge supplies or populations; journeys cost food and actions."),
                    Option(StoryDirective.Explore, "Explore separately", "Send each band toward unfamiliar land.", "Explore independently, increasing opportunities to find land and encounter others. Distant households may drift and become separate peoples.")));
            possibilities.Add(Event("frontier", "Explore or build food reserves?", leader,
                leader.Name + " can stay near familiar ground or explore beyond the known map. The cultural adviser recommends discovering new places. The economic adviser recommends building food reserves first. Your choice sets the bands' priorities for the next few turns.",
                "move", CounsellorId.Memory, false,
                Option(StoryDirective.Explore, "Explore new land", "Send bands toward the edge of the known map.", "Travel costs food and actions and can reveal new places. Repeated movement develops mobility, but may separate daughter bands from the leader."),
                Option(StoryDirective.Provision, "Build food reserves", "Gather food; move to richer known ground when needed.", "Build roughly five turns of food before resting. Gathering develops stewardship but depletes local ground; building a surplus leaves less time for exploration.")));

            StoryEvent[] fresh = possibilities.Where(e => e.Key != previousKey).ToArray();
            if (fresh.Length == 0) fresh = possibilities.ToArray();
            return fresh[(game.Turn / 3) % fresh.Length];
        }

        internal static AutoplayDecision Choose(Game game, StoryDirective directive)
        {
            if (game == null) throw new ArgumentNullException("game");
            if (game.IsOver) return null;
            Band band = game.ControlledBands.FirstOrDefault(b => game.ActionsFor(b.Id) > 0);
            if (band == null) return Decision("end", "The households have finished their work; the turn can close.");
            AutoplayDecision action = ChooseBand(game, band, directive);
            if (action.Command == "wait" && !game.TribesEnabled) action = Decision("end", action.Reason);
            if (game.TribesEnabled && action.Command != "end")
                action = Decision("band:" + band.Id.ToString(CultureInfo.InvariantCulture) + ":" + action.Command, band.Name + ": " + action.Reason);
            return action;
        }

        private static AutoplayDecision ChooseBand(Game game, Band band, StoryDirective directive)
        {
            double food = FoodTurns(game, band);
            // Surviving the imminent close takes precedence over an optional
            // expedition. The other priorities deliberately retain their tradeoffs.
            if (Danger(game, band))
            {
                int refuge = SafeNeighbors(game, band).OrderByDescending(n => ThreatDistance(game, band, n))
                    .ThenByDescending(n => game.ForageYield(n, band)).ThenBy(n => n).DefaultIfEmpty(-1).First();
                if (refuge >= 0 && band.Food >= TravelFood(game, band) + Needs(game, band))
                    return Move(refuge, "Keep the household alive by withdrawing from a stronger threat.");
            }
            if (directive == StoryDirective.HarvestMeat && food < 3)
            {
                Beast herd = OwnLivestock(game, band);
                if (herd != null && LivestockEconomy.CanSlaughter(game, band, herd))
                    return Decision("slaughter:" + herd.Id.ToString(CultureInfo.InvariantCulture), "Take meat from owned livestock to rebuild food reserves; fewer animals will remain for milk.");
            }
            if (food < 1.35) return Provision(game, band, 1.8);
            if (game.SaltEnabled && SaltEconomy.ReserveTurns(band) < 0.6)
            {
                AutoplayDecision relief = SaltOrder(game, band);
                if (relief != null) return relief;
            }

            if (directive == StoryDirective.SeekSalt && game.SaltEnabled)
            {
                if (SaltEconomy.ReserveTurns(band) < 6)
                {
                    AutoplayDecision salt = SaltOrder(game, band);
                    if (salt != null) return salt;
                    AutoplayDecision frontier = Explore(game, band);
                    if (frontier != null) return frontier;
                }
                return Provision(game, band, 4);
            }
            if (directive == StoryDirective.GatherWood && game.WoodEnabled)
            {
                if (game.SaltEnabled && SaltEconomy.ReserveTurns(band) < 1.5)
                {
                    AutoplayDecision relief = SaltOrder(game, band);
                    if (relief != null) return relief;
                }
                double target = WoodEconomy.FuelNeed(band) * 6 + (band.Settled ? 0 : WoodEconomy.CampCost);
                if (band.Wood < target)
                {
                    AutoplayDecision wood = WoodOrder(game, band);
                    if (wood != null) return wood;
                }
                return Provision(game, band, 4);
            }
            if (directive == StoryDirective.Explore)
            {
                AutoplayDecision explore = Explore(game, band);
                if (explore != null) return explore;
                return Provision(game, band, 3);
            }
            if (directive == StoryDirective.Reunite)
            {
                Band leader = game.TribeLeaderBand;
                if (leader != null && band.Id != leader.Id && band.CellId != leader.CellId && food >= 1.6)
                {
                    int next = KnownStep(game, band, n => n == leader.CellId);
                    if (CanTravel(game, band, next)) return Move(next, "Return to the leading household and renew contact.");
                }
                return Stay(game, band, 4, "Keep the reunion place and its people supplied.");
            }
            if (directive == StoryDirective.Settle)
            {
                if (!band.Settled && !EncounterRules.HostileAt(game, band.CellId, band.Id) && band.Food >= 30 + Needs(game, band) * 1.8)
                {
                    if (!game.WoodEnabled || band.Wood >= WoodEconomy.CampCost)
                        return Decision("camp", "Spend 30 food" + (game.WoodEnabled ? " and 10 wood" : "") + " to establish a camp here.");
                    AutoplayDecision wood = WoodOrder(game, band);
                    if (wood != null) return wood;
                }
                return Stay(game, band, band.Settled ? 4 : 5, "Provide for the hearth and give its ground time to recover.");
            }
            if (directive == StoryDirective.Divide)
            {
                if (CanDivide(game, band) && (!game.SaltEnabled || SaltEconomy.ReserveTurns(band) >= 2))
                    return Decision("split", "Give a daughter household one-third of the people and its matching share of supplies.");
                return Provision(game, band, 5);
            }
            if (directive == StoryDirective.Befriend)
            {
                Beast companion = BefriendTarget(game, band, true);
                if (companion != null && band.Food >= Needs(game, band) * 2 + EncounterRules.Animal(game, companion).OfferingCost +
                    (companion.CellId == band.CellId ? 0 : TravelFood(game, band)))
                    return Decision("befriend-animal:" + companion.Id.ToString(CultureInfo.InvariantCulture), "Offer food to a manageable animal group; trust may grow, but rejection can hurt.");
                return Provision(game, band, 5);
            }
            if (directive == StoryDirective.Defend)
            {
                double strength = EncounterRules.Band(game, band).Strength;
                Band enemy = NearbyEnemies(game, band).FirstOrDefault(b => EncounterRules.Outlook(game, band.Id, UnitKind.Band, b.Id).CanAttack &&
                    EncounterRules.Band(game, b).Strength < strength * .85);
                if (enemy != null && food >= 1.8)
                    return Decision("attack-band:" + enemy.Id.ToString(CultureInfo.InvariantCulture), "Confront a weaker hostile band; even favorable combat can cost lives.");
                Beast threat = NearbyAnimals(game, band).FirstOrDefault(a => EncounterRules.Animal(game, a).Hostile &&
                    EncounterRules.Outlook(game, band.Id, UnitKind.Animal, a.Id).CanAttack && EncounterRules.Animal(game, a).Strength < strength * .85);
                if (threat != null && food >= 1.8)
                    return Decision("attack-animal:" + threat.Id.ToString(CultureInfo.InvariantCulture), "Drive back a weaker hostile animal group.");
                if (!band.Settled && (!game.WoodEnabled || band.Wood >= WoodEconomy.CampCost) &&
                    !EncounterRules.HostileAt(game, band.CellId, band.Id) && band.Food >= 30 + Needs(game, band) * 2)
                    return Decision("camp", "Establish a sheltered base while keeping enough food in reserve.");
                return Stay(game, band, 4, "Supply the household while watching the nearby paths.");
            }
            return Provision(game, band, 5);
        }

        private static AutoplayDecision Provision(Game game, Band band, double target)
        {
            double here = game.ForageYield(band.CellId, band);
            int rich = SafeNeighbors(game, band).Where(n => game.ForageYield(n, band) - TravelFood(game, band) > here * 1.4 + 8)
                .OrderByDescending(n => game.ForageYield(n, band)).ThenBy(n => n).DefaultIfEmpty(-1).First();
            if (rich >= 0 && (game.ActionsFor(band.Id) > TravelRules.MoveCost(game, band, band.CellId, rich) || FoodTurns(game, band) > 2.2) &&
                band.Food >= TravelFood(game, band) + Needs(game, band))
                return Move(rich, "Gather on more productive known ground rather than exhaust the old place.");
            return Stay(game, band, target, "The food reserve is secure; leave the ground time to recover.");
        }

        private static AutoplayDecision Stay(Game game, Band band, double target, string rest)
        {
            if (FoodTurns(game, band) < target) return Decision("forage", "Gather food for this household before pursuing further ambitions.");
            if (game.WoodEnabled && WoodEconomy.ReserveTurns(band) < 2 &&
                (!game.SaltEnabled || SaltEconomy.ReserveTurns(band) >= 1.5) && WoodEconomy.CanGather(game, band))
                return Decision("wood", "Food is secure. Collect nearby wood to keep cooking fires supplied.");
            return Decision("wait", rest);
        }

        private static AutoplayDecision WoodOrder(Game game, Band band)
        {
            double here = WoodEconomy.GatherYield(game, band), fuel = WoodEconomy.FuelNeed(band);
            if (here < fuel * 2 && !band.Settled)
            {
                int next = KnownStep(game, band, n => WoodEconomy.GatherYield(game, band, n) >= Math.Max(here * 2, fuel * 3));
                if (CanTravel(game, band, next)) return Move(next, "Travel to a known place where wood is easier to collect.");
            }
            return WoodEconomy.CanGather(game, band) ? Decision("wood", "Use one action to gather wood for cooking, warmth and camp building.") : null;
        }

        private static AutoplayDecision SaltOrder(Game game, Band band)
        {
            if (SaltEconomy.CanGather(game, band)) return Decision("salt", "Gather salt at this source for the household's reserve.");
            int next = KnownStep(game, band, n => SaltEconomy.Source(game, n) != SaltSource.None);
            return CanTravel(game, band, next) ? Move(next, "Follow remembered safe ground toward a salt source.") : null;
        }

        private static AutoplayDecision Explore(Game game, Band band)
        {
            int next = KnownStep(game, band, n => n != band.CellId && game.World.Cells[n].Neighbors.Any(other => !game.Explored.Contains(other)));
            if (CanTravel(game, band, next)) return Move(next, "Reach the edge of remembered land and discover what lies beyond it.");
            return null;
        }

        private static bool CanTravel(Game game, Band band, int next)
        { return next >= 0 && next != band.CellId && game.CanMoveBand(band.Id, next) && band.Food >= TravelFood(game, band) + Needs(game, band) * 1.25; }

        private static IEnumerable<int> SafeNeighbors(Game game, Band band)
        { return game.World.Cells[band.CellId].Neighbors.Where(n => game.Explored.Contains(n) && game.World.Cells[n].Terrain != Terrain.Ice &&
            game.CanMoveBand(band.Id, n) && !EncounterRules.HostileAt(game, n, band.Id)); }

        private static IEnumerable<Band> NearbyEnemies(Game game, Band band)
        { return game.Bands.Where(b => b.Population > 0 && game.Explored.Contains(b.CellId) && Nearby(game, band, b.CellId) &&
            EncounterRules.BandsHostile(game, band.Id, b.Id)).OrderBy(b => b.Id); }

        private static IEnumerable<Beast> NearbyAnimals(Game game, Band band)
        { return game.Rules != SimulationRules.MobileUnits ? Enumerable.Empty<Beast>() : game.Beasts.Where(a => a.Count > 0 &&
            game.Explored.Contains(a.CellId) && Nearby(game, band, a.CellId) && (!a.Domestic || !game.CanControlBand(a.OwnerId))).OrderBy(a => a.Id); }

        private static bool Nearby(Game game, Band band, int cell)
        { return cell == band.CellId || game.World.Cells[band.CellId].Neighbors.Contains(cell); }

        private static Beast BefriendTarget(Game game, Band band, bool legalNow)
        {
            double strength = EncounterRules.Band(game, band).Strength;
            return NearbyAnimals(game, band).Where(a => !a.Domestic && a.Kind != BeastKind.Dragon && LivestockEconomy.CanDomesticate(game, a) &&
                EncounterRules.Animal(game, a).FriendChance >= .4 && EncounterRules.Animal(game, a).Strength < strength * 1.5 &&
                !game.Beasts.Any(pet => pet.Domestic && pet.OwnerId == band.Id && pet.Count > 0 && pet.Kind == a.Kind) &&
                (!legalNow || EncounterRules.Outlook(game, band.Id, UnitKind.Animal, a.Id).CanBefriend))
                .OrderByDescending(a => a.PositiveContacts).ThenBy(a => a.Id).FirstOrDefault();
        }

        private static Beast OwnLivestock(Game game, Band band)
        {
            if (!game.LivestockEnabled) return null;
            return game.Beasts.Where(a => a.Domestic && a.OwnerId == band.Id && a.CellId == band.CellId && a.Count > 0 &&
                LivestockEconomy.IsLivestock(a)).OrderByDescending(a => LivestockEconomy.MeatFood(game, a)).ThenBy(a => a.Id).FirstOrDefault();
        }

        private static StoryOption MeatOption()
        {
            return Option(StoryDirective.HarvestMeat, "Use the herd for meat", "Trade part of your herd for immediate food.",
                "Bands with low food slaughter 10% of a cattle or goat herd per action, at least one animal. Meat enters reserves immediately. Milk output and herd size fall; bands stop slaughtering once they have three turns of food.");
        }

        private static bool CanDivide(Game game, Band band)
        { return band.Population >= 80 && band.Food >= game.Upkeep(band) * 2 &&
            (game.Rules != SimulationRules.Classic || game.World.Cells[band.CellId].Neighbors.All(n => game.Explored.Contains(n))) && game.World.Cells[band.CellId].Neighbors.Any(n =>
            game.Explored.Contains(n) && game.World.Cells[n].IsLand && game.World.Cells[n].Terrain != Terrain.Ice && !EncounterRules.HostileAt(game, n, band.Id)); }

        private static bool Danger(Game game, Band band)
        {
            double strength = EncounterRules.Band(game, band).Strength;
            return NearbyEnemies(game, band).Any(b => EncounterRules.Band(game, b).Strength > strength * .95) ||
                NearbyAnimals(game, band).Any(a => EncounterRules.Animal(game, a).Hostile && EncounterRules.Animal(game, a).Strength > strength * .95);
        }

        private static int ThreatDistance(Game game, Band band, int cell)
        {
            int close = NearbyEnemies(game, band).Count(b => b.CellId == cell || game.World.Cells[cell].Neighbors.Contains(b.CellId)) +
                NearbyAnimals(game, band).Count(a => EncounterRules.Animal(game, a).Hostile && (a.CellId == cell || game.World.Cells[cell].Neighbors.Contains(a.CellId)));
            return -close;
        }

        // Weighted routes account for mountain and river effort, without reading
        // unexplored cells. Repeated calls recalculate as groups and knowledge move.
        private static int KnownStep(Game game, Band band, Func<int, bool> destination)
        {
            HashSet<int> open = new HashSet<int> { band.CellId }, closed = new HashSet<int>();
            Dictionary<int, int> distance = new Dictionary<int, int> { { band.CellId, 0 } };
            Dictionary<int, int> first = new Dictionary<int, int> { { band.CellId, band.CellId } };
            while (open.Count > 0)
            {
                int cell = open.OrderBy(id => distance[id]).ThenBy(id => id).First(); open.Remove(cell); closed.Add(cell);
                if (destination(cell) && (cell == band.CellId || !EncounterRules.HostileAt(game, cell, band.Id))) return first[cell];
                foreach (int next in game.World.Cells[cell].Neighbors.OrderBy(id => id))
                {
                    if (!game.Explored.Contains(next) || closed.Contains(next) || !game.World.Cells[next].IsLand ||
                        game.World.Cells[next].Terrain == Terrain.Ice || EncounterRules.HostileAt(game, next, band.Id)) continue;
                    int cost = TravelRules.MoveCost(game, band, cell, next), prior;
                    if (cost <= 0) continue;
                    int proposed = distance[cell] + cost;
                    if (!distance.TryGetValue(next, out prior) || proposed < prior)
                    { distance[next] = proposed; first[next] = cell == band.CellId ? next : first[cell]; open.Add(next); }
                }
            }
            return -1;
        }

        private static double Needs(Game game, Band band)
        { return game.Upkeep(band) + BandEconomy.DomesticEffects(game, band).AnimalCare; }
        private static double FoodTurns(Game game, Band band)
        { return band.Food / Math.Max(1, Needs(game, band)); }
        private static double TravelFood(Game game, Band band)
        { return band.Population * (game.Known("routes") ? .08 : .15); }
        private static string Amount(double number) { return Math.Floor(number).ToString("N0", CultureInfo.InvariantCulture); }
        private static StoryOption Option(StoryDirective directive, string title, string description, string consequences)
        { return new StoryOption(directive, title, description, consequences); }
        private static StoryEvent Event(string key, string title, Band band, string story, string icon, CounsellorId voice, bool urgent, params StoryOption[] options)
        { return new StoryEvent(key, title, story, band.Name + "  /  " + band.Population + " people  /  " + Amount(band.Food) + " food", icon, voice, urgent, options); }
        private static AutoplayDecision Move(int next, string reason)
        { return Decision("move:" + next.ToString(CultureInfo.InvariantCulture), reason); }
        private static AutoplayDecision Decision(string command, string reason)
        { return new AutoplayDecision { Command = command, Reason = reason }; }
    }
}
