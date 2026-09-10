using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using Clio.Simulation;

namespace Clio.Desktop
{
    public enum CounsellorId { Stores, Watch, Memory, Bonds }

    public sealed class CounsellorProfile
    {
        public readonly CounsellorId Id;
        public readonly string Name, Title, Motive, PortraitKey;
        internal CounsellorProfile(CounsellorId id, string name, string title, string motive, string portrait)
        { Id = id; Name = name; Title = title; Motive = motive; PortraitKey = portrait; }
    }

    public sealed class CouncilOpinion
    {
        public readonly CounsellorId Voice;
        public readonly string Speech, Argument, Tradeoff, Rebuttal;
        public readonly AdviserAction Action;
        public readonly int ActorBandId, TargetBandId, CellId;
        // A reading can remain valid after its recorded foreign subject leaves
        // sight. Map/diplomacy navigation must also require TargetBandId >= 0.
        public readonly bool Available;
        internal CouncilOpinion(CounsellorId voice, string speech, string argument, string tradeoff, string rebuttal,
            AdviserAction action, int actor, int target, int cell, bool available)
        { Voice = voice; Speech = speech; Argument = argument; Tradeoff = tradeoff; Rebuttal = rebuttal;
            Action = action; ActorBandId = actor; TargetBandId = target; CellId = cell; Available = available; }
    }

    /// <summary>
    /// Four personal readings of the same evidence. All returned actions inspect;
    /// none issue orders, retain state, share supplies, or discover information.
    /// Evaluate again at inspection time: source advisories can become stale.
    /// </summary>
    public static class CouncilPerspectives
    {
        private enum Subject { Salt, Food, Threat, Beginning, Split, Wandering, Drift, Disposition, Secession, Knowledge, Companions, Gathering }
        private static readonly ReadOnlyCollection<CounsellorProfile> profiles = new List<CounsellorProfile> {
            new CounsellorProfile(CounsellorId.Stores, "Economic adviser", "Supplies and survival", "Keep essential supplies secure.", "sula"),
            new CounsellorProfile(CounsellorId.Watch, "Military adviser", "Movement and threats", "Keep routes open and respond to threats.", "tavo"),
            new CounsellorProfile(CounsellorId.Memory, "Cultural adviser", "Knowledge and language", "Explore, learn and develop useful practices.", "yara"),
            new CounsellorProfile(CounsellorId.Bonds, "Social adviser", "Bands and relationships", "Keep bands connected and maintain relationships.", "lian")
        }.AsReadOnly();

        public static ReadOnlyCollection<CounsellorProfile> All { get { return profiles; } }
        public static CounsellorProfile Profile(CounsellorId id)
        {
            if (!Enum.IsDefined(typeof(CounsellorId), id)) throw new ArgumentOutOfRangeException("id");
            return profiles[(int)id];
        }
        public static CounsellorId Lead(Advisory report)
        {
            if (report == null) return CounsellorId.Memory;
            if (report.RecommendedAction == AdviserAction.ReviewWood) return CounsellorId.Stores;
            if (report.Role == AdviserRole.Economic) return CounsellorId.Stores;
            if (report.Role == AdviserRole.Military) return CounsellorId.Watch;
            return report.RecommendedAction == AdviserAction.ReviewUnits || report.RecommendedAction == AdviserAction.ReviewDiplomacy || report.RecommendedAction == AdviserAction.ReviewGathering ? CounsellorId.Bonds : CounsellorId.Memory;
        }

        public static CouncilOpinion Evaluate(Game game, Advisory report, CounsellorId voice)
        {
            if (game == null || report == null || game.IsOver || !Enum.IsDefined(typeof(CounsellorId), voice)) return Unavailable(voice);
            // Reuse the factual evaluator's thresholds, hostility and observer
            // gates. A resolved warning cannot retain an actionable old opinion.
            if (!report.IsGuidance)
            {
                report = AdviserReport.Evaluate(game).FirstOrDefault(a => a.Key == report.Key && a.Role == report.Role && a.ActorBandId == report.ActorBandId);
                if (report == null) return Unavailable(voice);
            }
            Band actor = game.ControlledBands.FirstOrDefault(b => b.Id == report.ActorBandId);
            if (report.RequiresControl && actor == null) return Unavailable(voice);
            Subject subject = Topic(game, report);
            if (subject == Subject.Secession && !game.TribeEvents.Any(e => e.VisibleToPlayer && e.Kind == TribeEventKind.Secession &&
                e.PreviousTribeId == game.PlayerTribeId && e.BandId == report.ActorBandId)) return Unavailable(voice);
            if (actor == null && report.ActorBandId >= 0 && subject != Subject.Secession && subject != Subject.Gathering) return Unavailable(voice);
            // A seceded household is a subject for observation, never an actor
            // whose supplies or orders this council may silently appropriate.
            if (actor == null) actor = game.TribeLeaderBand ?? game.Player;
            if (actor == null || !game.CanControlBand(actor.Id) || !game.Explored.Contains(actor.CellId)) return Unavailable(voice);
            if (report.RecommendedAction == AdviserAction.ReviewWood) return WoodOpinion(game, actor, voice);
            if (subject == Subject.Gathering) return GatheringOpinion(game, report, actor, voice);

            int target = -1;
            if (subject == Subject.Threat) target = report.TargetBandId;
            if (subject == Subject.Secession)
            {
                Band former = game.Bands.FirstOrDefault(b => b.Id == report.ActorBandId && b.Population > 0 && game.Explored.Contains(b.CellId));
                if (former != null) target = former.Id;
            }
            EconomyForecast forecast = BandEconomy.Forecast(game, actor);
            string speech, argument, tradeoff, rebuttal;
            AdviserAction action;
            switch (subject)
            {
                case Subject.Salt:
                    if (voice == CounsellorId.Stores)
                    {
                        speech = "I would make salt our next task, even if that means postponing a longer journey.";
                        argument = "This household holds " + Number(actor.Salt) + " salt and needs " + Number(forecast.SaltNeed) + " at the next close. Salt has no passive income; inspect its own source and reserve.";
                        tradeoff = "Gathering spends an action that could have served travel or reunion.";
                        rebuttal = "Military adviser: A reserve is useless if we lose the household on an unchecked approach. Look at the road before ordering the journey.";
                        action = SaltEconomy.CanGather(game, actor) ? AdviserAction.GatherSalt : AdviserAction.FindSalt;
                    }
                    else if (voice == CounsellorId.Watch)
                    {
                        speech = "I want a way to the salt, and a way back. A promising coast is not a safe road.";
                        argument = "Inspect known approaches before moving. Mountain entry and river crossings can use both actions under terrain rules; a hostile destination needs an encounter decision.";
                        tradeoff = "A safer detour still costs travel food and may postpone gathering.";
                        rebuttal = "Economic adviser: Caution must end in salt gathered. We cannot keep inspecting the road while another close consumes the reserve.";
                        action = AdviserAction.ReviewMap;
                    }
                    else if (voice == CounsellorId.Memory)
                    {
                        speech = "I would return to a spring we remember and build our next journey around a dependable source.";
                        argument = "Read the known salt sources. A source must be reached and worked; a remembered name supplies no salt by itself.";
                        tradeoff = "Known sources may be farther away. Remembering them does not make travel free.";
                        rebuttal = "Military adviser: A remembered spring is not a promise of a safe approach. Read the present ground as carefully as its name.";
                        action = AdviserAction.FindSalt;
                    }
                    else
                    {
                        speech = "I would provision every household for its own road and bring our kin back within reach.";
                        argument = "Review the controlled households separately. Each consumes its own salt; reunion restores contact, not a shared stockpile.";
                        tradeoff = "Keeping families within reach may limit the ground they can explore.";
                        rebuttal = "Economic adviser: Count every hearth, yes. But each still needs its own salt; proximity is no substitute for gathering.";
                        action = AdviserAction.ReviewUnits;
                    }
                    break;
                case Subject.Food:
                    if (voice == CounsellorId.Stores)
                    {
                        speech = "I would feed this household before I fund anyone's grand plan.";
                        argument = "The next close requires " + Number(forecast.Upkeep) + " food. " + (forecast.HungerLosses > 0 ? "Its current forecast includes " + forecast.HungerLosses + " hunger deaths. " : "Its remaining reserve is narrow. ") + "Review output, care and needs together.";
                        tradeoff = "Gathering now leaves fewer actions for movement or other work.";
                        rebuttal = "Cultural adviser: Another hurried gathering may save today. Read the practices that could change the pattern behind tomorrow's need.";
                        action = game.ActionsFor(actor.Id) > 0 ? AdviserAction.GatherFood : AdviserAction.ReviewEconomy;
                    }
                    else if (voice == CounsellorId.Watch)
                    {
                        speech = "I would move toward better known gathering ground, if we can still afford to gather after the journey.";
                        argument = "Compare known gathering ground and movement costs. A journey that uses both actions leaves no gathering action this turn.";
                        tradeoff = "Moving costs food immediately, even when the destination offers better gathering.";
                        rebuttal = "Economic adviser: Better ground is no meal until we reach it and gather. Show me the travel cost before moving a hungry household.";
                        action = AdviserAction.ReviewMap;
                    }
                    else if (voice == CounsellorId.Memory)
                    {
                        speech = "I would build on what our people have learned, instead of demanding one more load from tired ground.";
                        argument = "Review learned practices and their stated effects. A known technique may shape provisioning; reading about an unknown one does not grant its benefit.";
                        tradeoff = "Knowledge is no substitute for gathering before a hungry close.";
                        rebuttal = "Economic adviser: A practice worth remembering is still no food in hand. Meet this close's needs while you study the next.";
                        action = AdviserAction.ReviewCulture;
                    }
                    else
                    {
                        speech = "I would look at every hearth. One full household cannot eat for its distant kin.";
                        argument = "Food belongs to each band. Inspect the household roster before sending more people away or assuming one surplus covers the whole tribe.";
                        tradeoff = "Keeping bands together does not pool food or remove their separate upkeep.";
                        rebuttal = "Military adviser: Keeping everyone near tired ground may spread the hunger. Some households need room, not another return journey.";
                        action = AdviserAction.ReviewUnits;
                    }
                    break;
                case Subject.Threat:
                    if (voice == CounsellorId.Stores)
                    {
                        speech = "I will not pay for a quarrel without counting the people and provisions it could cost.";
                        argument = "The nearby band is actually hostile. Inspect this household's food position before deciding whether it can afford travel or a confrontation.";
                        tradeoff = "Delaying an encounter decision does not make the hostile band harmless.";
                        rebuttal = "Military adviser: An enemy does not promise to wait for balanced accounts. Inspect the confrontation before the choice narrows.";
                        action = AdviserAction.ReviewEconomy;
                    }
                    else if (voice == CounsellorId.Watch)
                    {
                        speech = "I would choose ground that leaves a way out. Do not let that hostile band choose the confrontation for us.";
                        argument = "Inspect the observed group's strength, condition and encounter outlook. Hostility is real; an attack this turn is not certain.";
                        tradeoff = "Fighting risks losses; withdrawal spends actions and provisions.";
                        rebuttal = "Economic adviser: A strong stance still has a price. Look at our food before choosing a battle or a long withdrawal.";
                        action = AdviserAction.InspectThreat;
                    }
                    else if (voice == CounsellorId.Memory)
                    {
                        speech = "I would study the known approaches. Fear makes a familiar path easy to overlook.";
                        argument = "Read the surrounding explored ground before deciding. The map can show known positions and travel costs, not the intentions of unseen bands.";
                        tradeoff = "A remembered route still requires enough actions and food to follow.";
                        rebuttal = "Military adviser: The terrain matters, but so does the hostile group standing on it. I want their condition inspected directly.";
                        action = AdviserAction.ReviewMap;
                    }
                    else
                    {
                        speech = "I would draw our scattered households toward the leader before danger cuts between them.";
                        argument = "Review which bands remain under your control and where they stand. Reuniting preserves contact; it grants no automatic combat reinforcement.";
                        tradeoff = "Concentrating households costs travel and may leave productive ground behind.";
                        rebuttal = "Military adviser: Reunion takes time and does not combine our bands' combat strength. First judge the danger already in reach.";
                        action = AdviserAction.ReviewUnits;
                    }
                    break;
                case Subject.Secession:
                    if (voice == CounsellorId.Stores)
                    {
                        speech = "I accept their independence. Now I want an honest account of what our remaining households can sustain.";
                        argument = "Their people and supplies are outside your control. Inspect the current leader's forecast; the departed household is not available to provision your tribe.";
                        tradeoff = "An inward focus may leave changes among the new people unexamined.";
                        rebuttal = "Social adviser: They are still a people with whom we share a past. Do not let an inward account become indifference to the relationship.";
                        action = AdviserAction.ReviewEconomy;
                    }
                    else if (voice == CounsellorId.Watch)
                    {
                        speech = "I will watch their approaches. Independence is not enmity, but it changes who answers our call.";
                        argument = "Inspect only their currently observed position. A hidden or dead former household has no current map target; separation alone does not make a neutral band hostile.";
                        tradeoff = "Watching another people does not reveal their intentions or give you control of them.";
                        rebuttal = "Social adviser: Independence alone is not hostility. Read the actual relationship before treating a former household as a rival.";
                        action = AdviserAction.ReviewMap;
                    }
                    else if (voice == CounsellorId.Memory)
                    {
                        speech = "I would listen for what becomes different. A branch of our language now has a history of its own.";
                        argument = "Read the language and practices your people carry. This secession branched the departing household's language; shared descent does not keep it under your command.";
                        tradeoff = "Valuing a new tradition does not restore the lost household's population or supplies.";
                        rebuttal = "Economic adviser: Honor their new story. Then count the supplies and people that remain ours to sustain.";
                        action = AdviserAction.ReviewCulture;
                    }
                    else
                    {
                        speech = "I would learn to meet them as another people. A broken command is not necessarily a broken bond.";
                        argument = game.GatheringsEnabled ? "A known peaceful splinter can be invited to a gathering. Read the host's food cost, the shared meeting place and the arrival deadline before sending it." :
                            "Read their observed relationship in Diplomacy, then decide how to share the neighboring ground. Actual neutrality or hostility matters more than a shared origin.";
                        tradeoff = game.GatheringsEnabled ? "An invitation and the host's travel use actions and supplies. A meeting gives us no command over the guest." : "A neutral relationship is not a promise of help or a shared reserve.";
                        rebuttal = game.GatheringsEnabled ? "Economic adviser: Invite them, but keep enough for the host. Every gift at that fire must come out of somebody's real stores." :
                            "Military adviser: Kin now chooses its own road. Neutrality is useful evidence, but it is no promise that our interests stay aligned.";
                        action = game.GatheringsEnabled ? AdviserAction.ReviewGathering : AdviserAction.ReviewDiplomacy;
                    }
                    break;
                case Subject.Split: case Subject.Wandering: case Subject.Drift: case Subject.Disposition:
                    KinOpinion(game, actor, subject, voice, out speech, out argument, out tradeoff, out rebuttal, out action);
                    break;
                case Subject.Knowledge:
                    if (voice == CounsellorId.Stores)
                    {
                        speech = "I will praise this discovery when we understand what it changes in the household accounts.";
                        argument = "Inspect the current food forecast. Learned practices apply their actual effects automatically; a promising discovery is not a gift of supplies.";
                        tradeoff = "Judging everything by today's reserve can undervalue a useful long-term practice.";
                        rebuttal = "Cultural adviser: If we value only what feeds us today, we may miss a practice that changes the shape of many turns.";
                        action = AdviserAction.ReviewEconomy;
                    }
                    else if (voice == CounsellorId.Watch)
                    {
                        speech = "I want to know whether this changes the paths we can safely take.";
                        argument = "Inspect known ground and the displayed travel costs. A discovery changes movement only where its actual rules say so.";
                        tradeoff = "A new practice does not make unknown terrain visible or hostile routes safe.";
                        rebuttal = "Cultural adviser: A discovery may matter beyond the next journey. Read its own meaning before judging it only as a tool for travel.";
                        action = AdviserAction.ReviewMap;
                    }
                    else if (voice == CounsellorId.Memory)
                    {
                        speech = "I would linger over this. A people becomes more than its next meal by remembering what it learns.";
                        argument = "Read the learned practice and its stated effect in Culture. The record distinguishes what is known from what remains a possibility.";
                        tradeoff = "Time spent considering possibilities does not satisfy a household's immediate needs.";
                        rebuttal = "Economic adviser: Memory can carry a people far, provided somebody still supplies the people doing the carrying.";
                        action = AdviserAction.ReviewCulture;
                    }
                    else
                    {
                        speech = "I would keep the households in touch as their experience grows. Shared origins do not keep people close.";
                        argument = "Review the controlled bands and their contact state. Tribal knowledge already applies to controlled households; reunion is about contact, not unlocking a duplicate benefit.";
                        tradeoff = "Seeking reunion can delay exploration even while shared knowledge continues to work.";
                        rebuttal = "Cultural adviser: A practice carried away can begin another tradition. Keeping every bearer close is not the only way to value it.";
                        action = AdviserAction.ReviewUnits;
                    }
                    break;
                case Subject.Companions:
                    if (voice == CounsellorId.Stores)
                    {
                        speech = "I want every companion's care counted beside its contribution. Affection does not feed an animal.";
                        argument = game.LivestockEnabled ? "Milk and care scale with the number of cattle and goats. Compare the milk forecast with the food care cost. Dogs help hunting but produce no food and do not improve gathering." :
                            "Review the owned lineages' actual benefits and care costs. Dogs, cattle and other companions have different effects; their number alone is not a measure of their value.";
                        tradeoff = game.LivestockEnabled ? "Slaughter provides meat immediately, but fewer animals remain to supply milk. Care also falls as the herd shrinks." :
                            "Care consumes food even when a companion's particular benefit is not being used.";
                        rebuttal = "Social adviser: Count the care, but remember which household formed the bond. A lineage is more than a favorable balance.";
                        action = AdviserAction.ReviewAnimals;
                    }
                    else if (voice == CounsellorId.Watch)
                    {
                        speech = "I would inspect the companions before trusting them with a dangerous journey.";
                        argument = game.LivestockEnabled ? "Dogs improve " + (game.Rules == SimulationRules.MobileUnits ? "strength when attacking animals" : "hunting chances") +
                            " and follow their owning band. They do not produce food, improve gathering or become independently commanded scouts. Inspect the prey before hunting." :
                            "Review the living owned units and their conditions. Companions follow their owner; they do not become independently commanded scouts or automatic reinforcements.";
                        tradeoff = "A useful companion cannot make every encounter safe.";
                        rebuttal = "Economic adviser: Their usefulness still draws on a household's food. Read the care before making the next journey harder to provision.";
                        action = AdviserAction.ReviewUnits;
                    }
                    else if (voice == CounsellorId.Memory)
                    {
                        speech = "I would remember this as more than a useful capture. Our people have begun a living lineage.";
                        argument = game.LivestockEnabled ? "Cattle and goats provide milk without losing animals. Meat requires slaughter. Deer cannot be domesticated; inspect a species before investing in befriending it." :
                            "Read the companion lineages and their stated effects. The founding relationship belongs to the history; later generations still need care.";
                        tradeoff = "A long memory does not protect a lineage from loss or release when its owner dies.";
                        rebuttal = "Economic adviser: Remember the beginning, and account for the living animals now. A lineage needs more than an honored name.";
                        action = AdviserAction.ReviewAnimals;
                    }
                    else
                    {
                        speech = "I would keep this bond near the household that made it, and notice when that household is drifting away.";
                        argument = "Review the owner in Units. Companions belong to that household and follow it; another hearth cannot silently claim their care or benefits.";
                        tradeoff = "A household's departure can also take its companion lineages outside your control.";
                        rebuttal = "Military adviser: A valued bond does not make every journey safe. Inspect the units and the ground their owner intends to cross.";
                        action = AdviserAction.ReviewUnits;
                    }
                    break;
                default:
                    if (voice == CounsellorId.Stores)
                    {
                        speech = "I want a reserve before a legend. Begin by learning what this household must provide.";
                        argument = "Read the food forecast and salt needs, then give ordinary orders. The close consumes supplies whether or not every band used its actions.";
                        tradeoff = "Building reserves can slow early exploration.";
                        rebuttal = "Cultural adviser: Survival gives a story time to happen. It does not tell us what we want that story to become.";
                        action = AdviserAction.ReviewEconomy;
                    }
                    else if (voice == CounsellorId.Watch)
                    {
                        speech = "I would learn the ground around us. A hearth needs more than one way out.";
                        argument = "Inspect the known map, costs and nearby groups. Opportunity markers invite inspection; they do not gather resources or reveal unknown ground.";
                        tradeoff = "Every journey uses food and actions that could have built the reserve.";
                        rebuttal = "Economic adviser: A second way out still needs provisions. Learn the road, but do not spend the first reserve merely proving it exists.";
                        action = AdviserAction.ReviewMap;
                    }
                    else if (voice == CounsellorId.Memory)
                    {
                        speech = "I want to know who we are before we decide how far to go. Let this beginning have a voice.";
                        argument = "Read the founding language and practices in Culture. The words belong to this beginning; later history can change what the people know and speak.";
                        tradeoff = "A thoughtful beginning still needs practical orders before the first close.";
                        rebuttal = "Economic adviser: A thoughtful beginning still reaches its first close. Let us give it enough food and salt to survive the occasion.";
                        action = AdviserAction.ReviewCulture;
                    }
                    else
                    {
                        speech = "I would learn every household by name. Growth should not make our own kin disappear from view.";
                        argument = "Inspect Units to see who belongs to your tribe and who can still act. Each household keeps its own supplies and actions.";
                        tradeoff = "Keeping close ties may compete with the wish to spread across new ground.";
                        rebuttal = "Military adviser: A band that never ranges beyond familiar ground cannot find what lies past it. Leave room for a useful departure.";
                        action = AdviserAction.ReviewUnits;
                    }
                    break;
            }
            int cell = actor.CellId;
            if (target >= 0 && (action == AdviserAction.InspectThreat || subject == Subject.Secession && action == AdviserAction.ReviewMap))
                cell = game.Bands.First(b => b.Id == target).CellId;
            if (subject == Subject.Secession && action == AdviserAction.ReviewMap && target < 0) cell = -1;
            // Only the relevant inspection carries a foreign target. Economy and
            // unit/culture pages must not accidentally switch to a foreign band.
            if (action != AdviserAction.InspectThreat && action != AdviserAction.ReviewDiplomacy && action != AdviserAction.ReviewGathering && !(subject == Subject.Secession && action == AdviserAction.ReviewMap)) target = -1;
            return new CouncilOpinion(voice, speech, argument, tradeoff, rebuttal, action, actor.Id, target, cell, true);
        }

        private static void KinOpinion(Game game, Band actor, Subject subject, CounsellorId voice,
            out string speech, out string argument, out string tradeoff, out string rebuttal, out AdviserAction action)
        {
            if (!game.TribesEnabled)
            {
                LegacyKinOpinion(voice, out speech, out argument, out tradeoff, out rebuttal, out action);
                return;
            }
            TribeMembership member = game.TribeStatus(actor.Id);
            string state = member.IsLeader ? "This household now leads the tribe." :
                "It has been apart for " + member.TurnsAway + " turns; at its current separation, independence begins at " + member.SecedeAfter + ".";
            if (voice == CounsellorId.Stores)
            {
                speech = subject == Subject.Split ? "I would check what each hearth can carry before celebrating another departure." :
                    "I would read this household's reserve before asking it to come home or range farther.";
                argument = "Inspect this band's own forecast. " + state;
                tradeoff = "Travel consumes food; neither a shared name nor reunion pools the household reserves.";
                rebuttal = "Social adviser: A healthy reserve does not preserve contact. Read the reunion countdown before prosperity becomes permanent separation.";
                action = AdviserAction.ReviewEconomy;
            }
            else if (voice == CounsellorId.Watch)
            {
                speech = subject == Subject.Wandering ? "I would use their restlessness to learn the approaches, provided we can still provision the journey." :
                    "I would give them useful ground to explore instead of treating every step apart as a failure.";
                argument = "Inspect this household's known surroundings and travel costs. " + state;
                tradeoff = "Distance can hasten independence. An exploring daughter may cease to be yours to command.";
                rebuttal = "Social adviser: Useful ground can become a road out of our tribe. I want the reunion cost and countdown understood before we encourage it.";
                action = AdviserAction.ReviewMap;
            }
            else if (voice == CounsellorId.Memory)
            {
                speech = subject == Subject.Drift ? "I would make room for a different voice. Keeping every household obedient is not the only kind of history." :
                    "I would let this household find a story of its own, while remembering what it carries from us.";
                argument = (game.BandPersonalitiesEnabled ? "Read its disposition and contact state in Units. " : "Read its contact state in Units. ") +
                    "If it becomes independent, its language branches and its people remain alive outside your control.";
                tradeoff = "Letting a new people emerge gives up its orders, population and supplies as controlled resources.";
                rebuttal = "Social adviser: A new voice need not begin with lost contact. Bring them within reach while their future is still a choice we can discuss.";
                action = AdviserAction.ReviewUnits;
            }
            else
            {
                speech = subject == Subject.Drift ? "I want their faces beside our fire again before distance becomes a decision we cannot take back." :
                    "I would give them a purpose and a way home. Kinship needs meetings, not merely a shared name.";
                argument = state + (game.BandPersonalitiesEnabled ? " Review reunion in Units. A successful order, including Hold, prevents voluntary wandering this turn." :
                    " Review reunion in Units and plan ordinary movement back to the leader's hex.");
                tradeoff = "Hold does not reset separation. Reunion requires reaching the leader's hex.";
                rebuttal = "Cultural adviser: A return journey is not always the finest ending. Some households may become more by finding a voice beyond our fire.";
                action = AdviserAction.ReviewUnits;
            }
        }

        private static void LegacyKinOpinion(CounsellorId voice, out string speech, out string argument,
            out string tradeoff, out string rebuttal, out AdviserAction action)
        {
            if (voice == CounsellorId.Stores)
            {
                speech = "I would rebuild the reserve we shared with them. Our remaining people still need to eat.";
                argument = "This daughter began as an independent band. Inspect the food forecast of the band you still control; the departed supplies are no longer yours to spend.";
                tradeoff = "Rebuilding reserves can delay your own next journey.";
                rebuttal = "Cultural adviser: Count what remains, but remember that those departing people began another story rather than vanished.";
                action = AdviserAction.ReviewEconomy;
            }
            else if (voice == CounsellorId.Watch)
            {
                speech = "I would give our own band room to move. The daughter now chooses its paths without waiting for our orders.";
                argument = "Inspect the known ground around your remaining band. The independent daughter makes its own choices; it has no controlled actions for you to allocate.";
                tradeoff = "Your own travel still costs actions and provisions.";
                rebuttal = "Social adviser: Leave them room, but do not mistake separate orders for a reason to forget our shared beginning.";
                action = AdviserAction.ReviewMap;
            }
            else if (voice == CounsellorId.Memory)
            {
                speech = "I would remember the words they carried away. A shared beginning can grow into different traditions.";
                argument = "Read your people's language and practices. The daughter carried the parent language into an independent band; its speech may change as its own history unfolds.";
                tradeoff = "Shared language does not preserve control of the daughter or its supplies.";
                rebuttal = "Economic adviser: Honor their future, then feed the people who remain. A new story does not reduce our present needs.";
                action = AdviserAction.ReviewCulture;
            }
            else
            {
                speech = "I would remember them as kin without pretending their choices are still ours to make.";
                argument = "Inspect the household you still control. In this story, a daughter is independent from its founding; there is no reunion countdown that brings it back.";
                tradeoff = "Remembering the bond does not return the departing population or provisions.";
                rebuttal = "Military adviser: Kinship may endure in memory. Our next journey must still be planned for the band that answers our orders.";
                action = AdviserAction.ReviewUnits;
            }
        }

        private static Subject Topic(Game game, Advisory report)
        {
            if (report.RecommendedAction == AdviserAction.ReviewGathering) return Subject.Gathering;
            if (report.Role == AdviserRole.Military) return Subject.Threat;
            if (report.Role == AdviserRole.Economic) return report.RecommendedAction == AdviserAction.FindSalt || report.RecommendedAction == AdviserAction.GatherSalt ? Subject.Salt : Subject.Food;
            if (report.RecommendedAction == AdviserAction.ReviewDiplomacy) return Subject.Secession;
            if (report.RecommendedAction == AdviserAction.ReviewCulture) return Subject.Knowledge;
            if (report.RecommendedAction == AdviserAction.ReviewAnimals) return Subject.Companions;
            if (report.Key != null && report.Key.StartsWith("guidance:wandering:", StringComparison.Ordinal)) return Subject.Wandering;
            int id;
            if (report.Key != null && report.Key.StartsWith("guidance:tribe:", StringComparison.Ordinal) && Int32.TryParse(report.Key.Substring(15), out id))
            {
                TribeEvent entry = game.TribeEvents.FirstOrDefault(e => e.Id == id && e.VisibleToPlayer && e.BandId == report.ActorBandId);
                if (entry != null && entry.Kind == TribeEventKind.Drift) return Subject.Drift;
                if (entry != null && entry.Kind == TribeEventKind.Wandering) return Subject.Wandering;
                if (entry != null && entry.Kind == TribeEventKind.Disposition) return Subject.Disposition;
            }
            return report.RecommendedAction == AdviserAction.ReviewUnits ? Subject.Split : Subject.Beginning;
        }
        private static CouncilOpinion Unavailable(CounsellorId voice)
        { return new CouncilOpinion(voice, "I would hear the current facts before urging a course.", "This concern or household is no longer available for this inspection.",
            "Earlier counsel may no longer fit the present situation.", "Let us return to the living households and what is currently known.", AdviserAction.ReviewMap, -1, -1, -1, false); }
        private static string Number(double value) { return value.ToString("0.#", CultureInfo.InvariantCulture); }

        private static CouncilOpinion WoodOpinion(Game game, Band actor, CounsellorId voice)
        {
            if (!game.WoodEnabled) return Unavailable(voice);
            EconomyForecast forecast = BandEconomy.Forecast(game, actor);
            string facts = actor.Name + " carries " + Number(actor.Wood) + " wood. A fire uses " + Number(forecast.WoodNeed) +
                " each turn. A fueled fire reduces food needs by 10%, rounded up to whole food, and prevents exposure losses. A camp costs " + Number(WoodEconomy.CampCost) + " wood plus 30 food.";
            string speech, rebuttal;
            if (voice == CounsellorId.Watch)
            {
                speech = "Keep enough wood for warmth on cold journeys. Inspect the route before moving to collect more.";
                rebuttal = "Economic adviser: Travel and wood collection use actions. Do not delay essential food or salt gathering.";
            }
            else if (voice == CounsellorId.Memory)
            {
                speech = "Forests offer more wood per action. Learn where supplies are plentiful before committing to a long stay.";
                rebuttal = "Military adviser: A productive forest still needs a safe approach. Check nearby threats.";
            }
            else if (voice == CounsellorId.Bonds)
            {
                speech = "Check every band's fuel. Reuniting on one hex does not automatically share their wood.";
                rebuttal = "Economic adviser: Collect what each band needs while keeping food and salt secure.";
            }
            else
            {
                speech = "Keep food and salt secure first, then gather wood for cooking fires and camps.";
                rebuttal = "Military adviser: On cold ground, a fire also prevents exposure losses. Include warmth in your plans.";
            }
            return new CouncilOpinion(voice, speech, facts,
                "Collecting wood takes one action that could be used for food, salt or movement. Camp building spends wood immediately.",
                rebuttal, AdviserAction.ReviewWood, actor.Id, -1, actor.CellId, true);
        }

        private static CouncilOpinion GatheringOpinion(Game game, Advisory report, Band actor, CounsellorId voice)
        {
            const string prefix = "guidance:gathering:"; int id;
            if (report.Key == null || !report.Key.StartsWith(prefix, StringComparison.Ordinal) || !Int32.TryParse(report.Key.Substring(prefix.Length), out id)) return Unavailable(voice);
            GatheringEvent entry = game.GatheringEvents.FirstOrDefault(e => e.Id == id && e.VisibleToPlayer && e.HostBandId == report.ActorBandId && e.GuestBandId == report.TargetBandId);
            Gathering record = entry == null ? null : game.Gatherings.FirstOrDefault(r => r.Id == entry.GatheringId);
            if (record == null) return Unavailable(voice);
            bool missed = record.Status == GatheringStatus.Missed || record.Status == GatheringStatus.Interrupted || record.Status == GatheringStatus.Refused;
            bool returning = record.Status == GatheringStatus.ReturnPlanned;
            bool fulfilled = record.Status == GatheringStatus.Fulfilled;
            string speech, argument, tradeoff, rebuttal;
            if (voice == CounsellorId.Stores)
            {
                speech = entry.Kind == GatheringEventKind.AidGiven ? "We have given from our own stores. I would count what remains before promising anything more." :
                    "I would give a modest welcome and keep a proper reserve. Generosity must leave its host alive.";
                argument = entry.Kind == GatheringEventKind.AidGiven ? "The recorded gift transferred " + Number(entry.FoodTransferred) + " food and " + Number(entry.SaltTransferred) + " salt. Those supplies left the host; they are not a loan or a shared reserve." :
                    "Read the host's invitation and gift costs. Aid is paid from its real food and salt, only after both households reach the meeting place.";
                tradeoff = "A larger gift leaves less for the host's own people, care and journey.";
                rebuttal = "Social adviser: A gift can be small and still deliberate. I want us to offer what we can afford, not make scarcity an excuse never to meet.";
            }
            else if (voice == CounsellorId.Watch)
            {
                speech = missed ? "I would understand what broke this meeting before sending another household along the same road." :
                    "I want our host at the right place with a way home. A peaceful invitation does not make every road safe.";
                argument = returning ? "The promised return is due on Turn " + record.ReturnDueTurn + ", with a window through Turn " + record.WindowEndTurn + ". Inspect the known place and the host's position before committing its actions." :
                    "Inspect the agreed place and the host's route. The guest remains independent; invitations do not give us its orders or reveal ground beyond our knowledge.";
                tradeoff = "A cautious journey spends time and provisions, and can still miss a deadline.";
                rebuttal = "Social adviser: Caution must leave room to arrive. I would rather make one achievable promise than keep postponing every meeting.";
            }
            else if (voice == CounsellorId.Memory)
            {
                speech = fulfilled ? "This return is an event, not merely a wish. I would remember the promise that was kept." :
                    missed ? "I would keep the failed meeting in the record. A flattering history teaches us very little." :
                    "I would mark what happens at this fire. One visit can begin a history without binding another people.";
                argument = "Read the recorded invitation, attendance, gift and return. An accepted plan differs from a completed visit; no treaty or cultural bonus is implied.";
                tradeoff = "A remembered meeting does not replenish either household's supplies.";
                rebuttal = "Economic adviser: Remember the names and the visit. I will still ask which household paid, and what its remaining stores can sustain.";
            }
            else
            {
                speech = fulfilled ? "They turned a promise into a journey. I would build the next invitation with the same care." :
                    missed ? "I would face the missed promise plainly, then offer a smaller meeting we can truly keep." :
                    returning ? "I want us there when they come back. A promise needs a place in our orders, not only in our memory." :
                    "I would welcome them as another people, and leave room for a second visit. Kinship need not mean command.";
                argument = returning ? "Return requires the guest to depart and visit again within Turn " + record.ReturnDueTurn + "\u2013" + record.WindowEndTurn + ". Your host must attend; an unattended promise is not fulfilled." :
                    "At the first meeting, aid and a return agreement are separate choices. Inspect their cost before confirming; the guest keeps its people, supplies and allegiance.";
                tradeoff = "A visit can draw the host away from exploration, gathering or reunion.";
                rebuttal = "Military adviser: A promise is not an escort. Give our host a journey it can finish; do not mistake a peaceful guest for a safe road.";
            }
            Band guest = game.Bands.FirstOrDefault(b => b.Id == record.GuestBandId && b.Population > 0 && game.Explored.Contains(b.CellId));
            return new CouncilOpinion(voice, speech, argument, tradeoff, rebuttal, AdviserAction.ReviewGathering,
                actor.Id, guest == null ? -1 : guest.Id, game.Explored.Contains(record.CellId) ? record.CellId : -1, true);
        }
    }
}
