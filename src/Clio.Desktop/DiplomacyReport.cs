using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Clio.Simulation;

namespace Clio.Desktop
{
    public enum DiplomaticRelation { Neutral, Enemy }

    public sealed class DiplomacyPolity
    {
        public readonly int TribeId, RepresentativeBandId, CellId, KnownBandCount, KnownPopulation;
        public readonly int SecessionTurn, OriginBandId;
        public readonly string Name;
        public readonly DiplomaticRelation Relation;
        public readonly bool FromPlayerTribe;
        public readonly ReadOnlyCollection<int> KnownBandIds;

        internal DiplomacyPolity(int tribe, Band[] known, DiplomaticRelation relation, TribeEvent origin)
        {
            TribeId = tribe; RepresentativeBandId = known[0].Id; CellId = known[0].CellId; Name = known[0].Name;
            KnownBandCount = known.Length; KnownPopulation = known.Sum(b => b.Population); Relation = relation;
            KnownBandIds = known.Select(b => b.Id).ToList().AsReadOnly();
            FromPlayerTribe = origin != null; SecessionTurn = origin == null ? -1 : origin.Turn;
            OriginBandId = origin == null ? -1 : origin.BandId;
        }
    }

    public sealed class DiplomacyState
    {
        public readonly bool Unlocked;
        public readonly int FirstSecessionTurn;
        public readonly ReadOnlyCollection<DiplomacyPolity> Polities;

        internal DiplomacyState(int firstSecession, List<DiplomacyPolity> polities)
        { Unlocked = firstSecession >= 0; FirstSecessionTurn = firstSecession; Polities = polities.AsReadOnly(); }
    }

    /// <summary>Observed foreign polities after a recorded secession from the player's tribe.</summary>
    public static class DiplomacyReport
    {
        public static DiplomacyState Evaluate(Game game)
        {
            List<DiplomacyPolity> polities = new List<DiplomacyPolity>();
            if (game == null) return new DiplomacyState(-1, polities);
            TribeEvent[] secessions = game.TribeEvents.Where(e => e.Kind == TribeEventKind.Secession && e.VisibleToPlayer &&
                e.PreviousTribeId == game.PlayerTribeId).OrderBy(e => e.Turn).ThenBy(e => e.Id).ToArray();
            if (secessions.Length == 0) return new DiplomacyState(-1, polities);

            // Only currently observed members contribute names, positions and
            // totals. A hidden founder must not supply a visible row's identity.
            var groups = game.Bands.Where(b => b.Population > 0 && game.TribeOf(b.Id) != game.PlayerTribeId && game.Explored.Contains(b.CellId))
                .GroupBy(b => game.TribeOf(b.Id)).OrderBy(group => group.Key);
            foreach (var group in groups)
            {
                Band[] known = group.OrderBy(b => b.Id).ToArray();
                bool enemy = known.Any(b => EncounterRules.BandsHostile(game, game.Player.Id, b.Id));
                TribeEvent origin = secessions.FirstOrDefault(e => e.TribeId == group.Key);
                polities.Add(new DiplomacyPolity(group.Key, known, enemy ? DiplomaticRelation.Enemy : DiplomaticRelation.Neutral, origin));
            }
            return new DiplomacyState(secessions[0].Turn, polities);
        }
    }
}
