using System;
using System.Linq;
using Clio.Simulation;

namespace Clio.Desktop
{
    internal sealed partial class StoryJournal
    {
        private void RecordGatherings(Game game, JournalSnapshot before)
        {
            foreach (GatheringEvent entry in game.GatheringEvents.Skip(before.GatheringEventCount).Where(e => e.VisibleToPlayer))
            {
                if (entry.Kind == GatheringEventKind.InvitationAccepted) Totals.GatheringsAccepted++;
                if (entry.Kind == GatheringEventKind.InvitationRefused) Totals.GatheringsRefused++;
                if (entry.Kind == GatheringEventKind.FirstMeeting) Totals.GatheringsMet++;
                if (entry.Kind == GatheringEventKind.ReturnAgreed) Totals.ReturnsAgreed++;
                if (entry.Kind == GatheringEventKind.ReturnFulfilled) Totals.ReturnsFulfilled++;
                if (entry.Kind == GatheringEventKind.Missed) Totals.GatheringsMissed++;
                if (entry.Kind == GatheringEventKind.Interrupted) Totals.GatheringsInterrupted++;
                // FoodSpent already contains the controlled household's actual outflow.
                // These categories explain that outflow; they never count it twice.
                Totals.GatheringFoodSpent += entry.FoodSpent;
                Totals.GatheringFoodGiven += entry.FoodTransferred;
                Totals.GatheringSaltGiven += entry.SaltTransferred;
                string impact = entry.ActionCost > 0 ? entry.ActionCost + " action spent." : "No additional action spent.";
                if (entry.FoodSpent > 0) impact += " Invitation provisions: " + Number(entry.FoodSpent) + ".";
                if (entry.FoodTransferred > 0 || entry.SaltTransferred > 0)
                    impact += " Given to " + entry.GuestName + ": " + Number(entry.FoodTransferred) + " food and " + Number(entry.SaltTransferred) + " salt.";
                string advice = entry.Kind == GatheringEventKind.InvitationAccepted ? "Meet at the agreed place before the arrival deadline. Each band travels using its ordinary actions and supplies." :
                    entry.Kind == GatheringEventKind.FirstMeeting ? "While together, you can offer supplies or agree to return. Inspect the costs in Diplomacy before choosing." :
                    entry.Kind == GatheringEventKind.ReturnAgreed ? "Have your host at the meeting place within the agreed return window. The guest must depart and return; the record keeps each party's attendance." :
                    entry.Kind == GatheringEventKind.Missed || entry.Kind == GatheringEventKind.Interrupted ? "The gathering record explains what happened. A later invitation is a new commitment." :
                    "Review the gathering record in Diplomacy. Each people retains its own command and language.";
                Notices.Add(new StoryNotice(nextId++, entry.Turn, entry.CellId, StoryNoticeKind.Gathering, entry.Title,
                    entry.Detail, impact, advice, entry.Kind != GatheringEventKind.Enabled && entry.Kind != GatheringEventKind.Completed,
                    null, -1, entry.HostBandId, entry.GuestBandId, -1));
            }
        }
    }
}
