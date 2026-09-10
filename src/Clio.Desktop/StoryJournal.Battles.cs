using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Clio.Simulation;

namespace Clio.Desktop
{
    internal sealed partial class StoryJournal
    {
        private List<StoryNotice> RecordTacticalBattlesEnabled(Game game)
        {
            int first = Notices.Count; Totals.Commands++;
            Add(game, StoryNoticeKind.Combat, "Battles on the map",
                "Attacks involving your people now open a battlefield on the same land. Position your groups, then direct them through combat rounds.",
                "Terrain and nearby participants shape the fight. Losses and recovered food carry back into the campaign when the battle finishes.",
                "Inspect the battlefield before starting. An enemy attack pauses the turn; closing its result continues the remaining turn. History records the result and each household's actual losses.", false, null);
            List<StoryNotice> notices = Notices.Skip(first).ToList();
            LastCommandNotices = new ReadOnlyCollection<StoryNotice>(notices.ToArray());
            LastCommandPopulationBefore = LastCommandPopulationAfter = game.TribesEnabled ? game.TribePopulation : game.Player.Population;
            return notices;
        }

        private void RecordBattleSupport(EncounterRecord record, HashSet<int> owned)
        {
            if (record.ActorKind == UnitKind.Animal && owned.Contains(record.ActorId))
            {
                Totals.DomesticAnimalsLost += record.ActorCasualties;
                if (record.ActorCountBefore > 0 && record.ActorCountAfter == 0) Totals.DomesticGroupsLost++;
            }
            if (record.TargetKind == UnitKind.Animal && owned.Contains(record.TargetId))
            {
                Totals.DomesticAnimalsLost += record.TargetCasualties;
                if (record.TargetCountBefore > 0 && record.TargetCountAfter == 0) Totals.DomesticGroupsLost++;
            }
        }
    }
}
