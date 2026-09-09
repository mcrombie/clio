using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Clio.Simulation;

namespace Clio.Desktop
{
    public sealed partial class GameForm
    {
        // These are current readings, not historical events. The frequency
        // controller owns presentation and once-per-topic acknowledgement.
        // Rebuild them after live changes so that a lesson never retains an
        // earlier household's reserves, action count or diplomatic subject.
        private List<Advisory> BeginnerLessons()
        {
            List<Advisory> result = new List<Advisory>();
            if (game == null || game.IsOver) return result;
            Band band = CurrentOrderBand;
            if (band == null || !game.CanControlBand(band.Id) || !game.Explored.Contains(band.CellId)) return result;
            EconomyForecast forecast = BandEconomy.Forecast(game, band);
            DomesticEconomy domestic = BandEconomy.DomesticEffects(game, band);
            int actions = game.ActionsFor(band.Id);

            result.Add(BeginnerLesson("food", band, "Food reserves and turn-end needs",
                band.Name + " holds " + LessonNumber(band.Food) + " food. Its people need " + LessonNumber(forecast.Upkeep) + " at the next turn's close.",
                "Food reserves are food available to spend, not a storage limit. Gathering here adds " + LessonNumber(game.ForageYield(band.CellId, band)) + " food for one action. At the next close, household needs, companion care, output and spoilage leave a forecast of " + LessonNumber(forecast.EndingFood) + " food. Further orders can change it.",
                "Read Economy's food forecast before ending the turn. A full band does not feed its distant kin automatically.", AdviserAction.ReviewEconomy, 10));

            if (game.SaltEnabled)
            {
                bool atSource = SaltEconomy.Source(game, band.CellId) != SaltSource.None;
                result.Add(BeginnerLesson("salt", band, "How to replenish salt",
                    "This household has " + LessonNumber(SaltEconomy.ReserveTurns(band)) + " turns of salt. " + (atSource ? "There is a salt source on its present hex." : "Reach a known coast or spring to replenish it."),
                    "The reserve holds " + LessonNumber(band.Salt) + " salt; this population uses " + LessonNumber(forecast.SaltNeed) + " each close. Gathering at a source costs one action and adds five turns of salt at the band's current size. Walking onto the source does not collect it. A shortage stops growth and deepens over successive turns.",
                    "Inspect salt and known sources, then choose a route that leaves time to gather before the reserve runs out.", AdviserAction.FindSalt, 9));
            }

            if (game.WoodEnabled)
                result.Add(BeginnerLesson("wood", band, "Wood supports food and shelter",
                    "This band has " + LessonNumber(band.Wood) + " wood, enough for " + LessonNumber(WoodEconomy.ReserveTurns(band)) + " turns of fire.",
                    "A fire uses " + LessonNumber(forecast.WoodNeed) + " wood per turn, reduces food needs by 10% with the final need rounded up, and prevents exposure losses. " +
                    "Collecting here gives " + LessonNumber(WoodEconomy.GatherYield(game, band)) + " wood for one action. Forests provide the best yield. A camp costs 30 food and " + LessonNumber(WoodEconomy.CampCost) + " wood.",
                    "Food and salt are essential. When those are secure, collect wood for cooking and warmth. Open Resources to see each band's reserve and fuel use.", AdviserAction.ReviewWood, 6));

            result.Add(BeginnerLesson("actions", band, "Two actions per band",
                band.Name + " has " + actions + " of its two actions left. " + (actions == 0 ? "Select another ready band, or end the turn." : "Use them for work or travel before ending the turn."),
                    (game.SaltEnabled ? "Gathering food or salt costs one action. " : "Gathering food costs one action. ") + "Moving can use one or both, according to the ground. " +
                    (game.TribesEnabled ? "Each controlled band has its own allowance; a new daughter band's first actions arrive next turn. " : "This band receives a fresh action allowance next turn. ") +
                    "End turn resolves food needs and lets other units act.",
                "Use Next band or Units to find unfinished households. Give deliberate orders before the whole tribe's turn closes.", AdviserAction.ReviewUnits, 8));

            string terrain = game.TerrainTravelEnabled ? "Ordinary land entry costs one action; mountain entry and river crossings cost two. Mountain routes remain passable." :
                "Entering adjacent land costs one action under this story's travel rules.";
            result.Add(BeginnerLesson("movement", band, "Movement costs food and actions",
                "Select your banner, then right-click adjacent land. The destination preview explains the action cost and any encounter.",
                terrain + " Travel also spends food. If the journey uses both actions, the band cannot gather on arrival this turn. " +
                    (game.Rules == SimulationRules.MobileUnits ? "A hostile destination opens an encounter choice; inspecting it spends nothing." : "Inspect the chosen destination before spending an action."),
                "The military adviser recommends a safe route. The economic adviser recommends checking travel food before you move.", AdviserAction.ReviewMap, 7));

            result.Add(BeginnerLesson("camp", band, band.Settled ? "Your camp produces food" : "Making camp",
                band.Settled ? "This household's camp currently produces " + LessonNumber(forecast.CampFood) + " food at each close, before its needs and other costs." :
                    "Make camp costs one action, 30 food" + (game.WoodEnabled ? " and " + LessonNumber(WoodEconomy.CampCost) + " wood" : "") + ". A camp adds local food output while your people stay.",
                (band.Settled ? "The present camp output is " + LessonNumber(forecast.CampFood) + " food. " : "The construction supplies are spent immediately. ") +
                    "Camp output depends on local gathering conditions and learned practices. Moving leaves the hearth behind. Camping does not remove food or salt needs, and crowded, depleted ground can still become a poor home.",
                "Inspect the food forecast after making camp. Stay when the ground sustains you; leave when the benefit no longer justifies it.", AdviserAction.ReviewEconomy, 6));

            bool nearbyAnimals = game.Rules == SimulationRules.MobileUnits && game.Beasts.Any(b => b.Count > 0 && !b.Domestic &&
                game.Explored.Contains(b.CellId) && (b.CellId == band.CellId || game.World.Cells[band.CellId].Neighbors.Contains(b.CellId)));
            bool companions = domestic.Dogs + domestic.Cattle + domestic.OtherCompanions > 0;
            if (companions || nearbyAnimals)
            {
                string detail = companions ? "Its companions currently add " + LessonNumber((domestic.GatheringMultiplier - 1) * 100) + "% to gathering and produce " +
                    LessonNumber(domestic.CattleFood) + " food from cattle, with " + LessonNumber(domestic.AnimalCare) + " food of care per close. These effects belong to this household." :
                    "Wild groups move independently. Befriending has an offering cost and can provoke retaliation; inspect the chosen animal's temperament, chance and trust requirement first. Trust must reach that group's threshold before domestication.";
                result.Add(BeginnerLesson("companions", band, "Companions help, and must be fed",
                    companions ? "This household's animals cost " + LessonNumber(domestic.AnimalCare) + " food each close. Their actual output and benefits appear in Resources." :
                        "The animals nearby are living groups. Befriending them is a risky investment, not a free pickup.",
                    detail,
                    "Dogs can improve gathering and hunting; other species have different effects. Review the actual companion ledger before adopting more mouths.", AdviserAction.ReviewAnimals, 5));
            }

            if (game.TribesEnabled && game.ControlledBands.Count() > 1)
            {
                TribeMembership membership = game.TribeStatus(band.Id);
                string contact = membership.IsLeader ? "This is the leading household: other bands must visit its hex to renew contact." :
                    band.Name + " has missed " + membership.TurnsAway + " reunion turns; its current separation threshold is " + membership.SecedeAfter + ".";
                result.Add(BeginnerLesson("reunion", band, "A tribe needs reunions",
                    "Your bands share a tribe, but keep separate supplies and actions. Bring them onto the leader's hex to renew contact.",
                    contact + " Being nearby is not a reunion. Prolonged separation can turn a daughter household into an independent people; disposition and distance shape the countdown. Reunion does not merge stockpiles.",
                    "Read the selected band's countdown and Reunion route in Units. The social adviser favors contact; the military adviser favors exploration.", AdviserAction.ReviewUnits, 4));
            }

            if (game.TerrainTravelEnabled)
                result.Add(BeginnerLesson("opportunities", band, "Read the glowing opportunities",
                    "Gold grain marks promising food ground, crystals mark salt sources, and a blue compass marks the edge of known land.",
                    "These map markers explain opportunities, not an adviser's order. Hover over the badge for its meaning and the selected band's travel cost. Food and salt require gathering after arrival; the compass indicates neighboring hexes that remain unknown. Markers are reduced on occupied hexes to keep units readable.",
                    "Use the Food, Salt and Explore legend, then hover before moving. A promising marker is not a guarantee that the approach is safe.", AdviserAction.ReviewMap, 3));

            if (game.GatheringsEnabled)
            {
                HashSet<int> splinters = new HashSet<int>(game.TribeEvents.Where(e => e.Kind == TribeEventKind.Secession && e.VisibleToPlayer &&
                    e.PreviousTribeId == game.PlayerTribeId).Select(e => e.TribeId));
                Band guest = game.Bands.Where(b => b.Population > 0 && !game.CanControlBand(b.Id) && game.Explored.Contains(b.CellId) &&
                    splinters.Contains(game.TribeOf(b.Id)) && !EncounterRules.BandsHostile(game, band.Id, b.Id)).OrderBy(b => b.Id).FirstOrDefault();
                if (guest != null)
                    result.Add(new Advisory("lesson:diplomacy", game.Turn, band.Id, guest.Id, guest.CellId,
                        "Independence can begin a relationship", guest.Name + " is a known peaceful splinter people. Diplomacy can help you arrange a gathering.",
                        "An invitation costs one action and provisions, even if refused. Review the exact cost and shared meeting site before confirming. Accepted guests travel normally. Gifts require the two bands to meet, and a return agreement commits you to another visit. The guest stays independent.",
                        "The social adviser favors meeting. The economic adviser recommends checking the cost of the invitation, journey and gift.", AdviserAction.ReviewDiplomacy, 2, true, guest.Name));
            }

            result.Add(BeginnerLesson("culture", band, "Experience becomes culture",
                "Your people's practices develop through what they do. Culture shows the requirements and effects of each discovery.",
                "Read each practice's actual progress and prerequisites in Culture. Gathering, travel, successful provisioning and established hearths can contribute to different discoveries. The Language subtab records your people's speech and place names. Viewing a practice spends no action and does not unlock it.",
                "The cultural adviser recommends opening Culture to see which practices your actions have developed.", AdviserAction.ReviewCulture, 1));
            return result;
        }

        private Advisory BeginnerLesson(string topic, Band band, string title, string summary, string explanation, string counsel, AdviserAction action, int priority)
        { return new Advisory("lesson:" + topic, game.Turn, band.Id, -1, band.CellId, title, summary, explanation, counsel, action, priority, true, band.Name); }

        private static bool IsBeginnerLesson(Advisory report)
        { return report != null && report.Key != null && report.Key.StartsWith("lesson:", StringComparison.Ordinal); }

        private CounsellorId BeginnerLessonLead(Advisory report)
        {
            if (!IsBeginnerLesson(report)) return CounsellorId.Memory;
            switch (report.Key)
            {
                case "lesson:food": case "lesson:salt": case "lesson:wood": case "lesson:camp": case "lesson:companions": return CounsellorId.Stores;
                case "lesson:movement": return CounsellorId.Watch;
                case "lesson:reunion": case "lesson:diplomacy": return CounsellorId.Bonds;
                default: return CounsellorId.Memory;
            }
        }

        private CouncilOpinion BeginnerLessonOpinion(Advisory report, CounsellorId voice)
        {
            // Re-read even a displayed lesson: a band may have moved, departed
            // the tribe, or lost sight of its diplomatic subject since opening.
            Advisory fresh = !Enum.IsDefined(typeof(CounsellorId), voice) || !IsBeginnerLesson(report) ? null :
                BeginnerLessons().FirstOrDefault(a => a.Key == report.Key && a.ActorBandId == report.ActorBandId && a.TargetBandId == report.TargetBandId);
            if (fresh == null)
                return new CouncilOpinion(voice, "Let us return to the present circumstances.", "This teaching no longer fits the selected household or its observed surroundings.",
                    "Read a current lesson before choosing your next order.", "Earlier facts can change as the people travel.", AdviserAction.ReviewMap, -1, -1, -1, false);

            string tradeoff;
            switch (fresh.Key)
            {
                case "lesson:food": case "lesson:salt": tradeoff = "Gathering uses an action that could have served movement, contact or other work. Arrival alone supplies nothing."; break;
                case "lesson:actions": tradeoff = "End turn closes every household's economy. Unused effort does not carry forward as extra actions."; break;
                case "lesson:movement": tradeoff = "Travel uses food and actions. A two-action journey leaves no gathering action on arrival."; break;
                case "lesson:camp": tradeoff = "Making camp costs one action, 30 food" + (game.WoodEnabled ? " and " + LessonNumber(WoodEconomy.CampCost) + " wood" : "") + ". Leaving abandons the camp; staying still requires food and salt."; break;
                case "lesson:wood": tradeoff = "Collecting wood uses an action. Keep food and salt supplied first; a camp also spends wood that could have fueled fires."; break;
                case "lesson:companions": tradeoff = "Befriending can provoke damage and costs an offering. Domestic companions then require their owner's continuing care."; break;
                case "lesson:reunion": tradeoff = "A reunion journey costs travel and time away from productive ground. Sharing a hex does not pool supplies."; break;
                case "lesson:diplomacy": tradeoff = "A refused invitation still costs its stated action and provisions. Gifts and promised return journeys have separate costs."; break;
                case "lesson:culture": tradeoff = "Reading costs nothing, but it grants no discovery. Your bands must still carry out the relevant work."; break;
                default: tradeoff = "A glowing opportunity does not guarantee a safe approach or free supplies. Inspect it before spending an action."; break;
            }
            string speech, rebuttal;
            if (voice == CounsellorId.Stores)
            {
                speech = "I would count this household's food and salt before committing its next action.";
                rebuttal = "Military adviser: A full store cannot choose a safe road for us. Inspect the ground and any groups ahead as well.";
            }
            else if (voice == CounsellorId.Watch)
            {
                speech = "I would leave room to move. Read the approach before another choice uses up our effort.";
                rebuttal = "Economic adviser: Movement still needs food. A promising destination is no help if we arrive unable to gather.";
            }
            else if (voice == CounsellorId.Bonds)
            {
                speech = "I would ask what this choice means for every household, especially the ones farther away.";
                rebuttal = "Military adviser: Kinship needs room as well as meetings. Reuniting too often can leave good ground unused.";
            }
            else
            {
                speech = "I would understand the rule behind the choice. Read what this household's experience makes possible.";
                rebuttal = "Economic adviser: Understanding should guide the next useful order. Our needs are still resolved when the turn ends.";
            }
            if (voice == BeginnerLessonLead(fresh)) speech = fresh.Summary;
            return new CouncilOpinion(voice, speech, fresh.Counsel, tradeoff, rebuttal, fresh.RecommendedAction,
                fresh.ActorBandId, fresh.TargetBandId, fresh.CellId, true);
        }

        // Moderate shows lessons when the player encounters the corresponding
        // choice. General teaching is available at High or through the council.
        private bool IsContextualBeginnerLesson(Advisory report)
        {
            if (!IsBeginnerLesson(report) || game == null || game.IsOver) return false;
            Band band = game.ControlledBands.FirstOrDefault(b => b.Id == report.ActorBandId);
            if (band == null || !game.Explored.Contains(band.CellId)) return false;
            switch (report.Key)
            {
                case "lesson:food": return page == 1 || band.Food < game.Upkeep(band) * 3;
                case "lesson:salt": return game.SaltEnabled && (SaltEconomy.ReserveTurns(band) < 4 || SaltEconomy.Source(game, band.CellId) != SaltSource.None);
                case "lesson:wood": return game.WoodEnabled && (page == 1 || WoodEconomy.ReserveTurns(band) < 3 || band.Settled);
                case "lesson:actions": return game.ActionsFor(band.Id) == 0 || game.ControlledBands.Count() > 1;
                case "lesson:movement": return game.TerrainTravelEnabled && game.World.Cells[band.CellId].Neighbors.Any(n => game.Explored.Contains(n) && TravelRules.MoveCost(game, band, band.CellId, n) == 2);
                case "lesson:camp": return band.Settled || BandEconomy.Forecast(game, band).ExposureLosses > 0;
                case "lesson:companions": return game.Beasts.Any(b => b.Count > 0 && (b.Domestic && b.OwnerId == band.Id ||
                    !b.Domestic && game.Explored.Contains(b.CellId) && (b.CellId == band.CellId || game.World.Cells[band.CellId].Neighbors.Contains(b.CellId))));
                case "lesson:reunion": return game.TribesEnabled && game.ControlledBands.Count() > 1;
                case "lesson:opportunities": return page == 0 && hoverKind >= MapRenderer.FoodOpportunity && hoverKind <= MapRenderer.ExplorationOpportunity;
                case "lesson:diplomacy": return game.GatheringsEnabled && game.Bands.Any(b => b.Id == report.TargetBandId && b.Population > 0 &&
                    game.Explored.Contains(b.CellId) && !game.CanControlBand(b.Id) && !EncounterRules.BandsHostile(game, band.Id, b.Id));
                case "lesson:culture": return page == 2 || game.Knowledge.Any(k => k.Known);
                default: return false;
            }
        }

        private static string LessonNumber(double value)
        { return value.ToString("0.#", CultureInfo.InvariantCulture); }
    }
}
