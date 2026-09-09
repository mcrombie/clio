using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;

namespace Clio.Simulation
{
    public enum PlaceAcquisition { Discovered, Shared, Inherited }

    /// <summary>A people's remembered name, with the original coinage and its most recent source.</summary>
    public sealed class PlaceKnowledge
    {
        public readonly int CellId, OriginBandId, OriginLanguageId, LearnedFromBandId, LearnedTurn;
        public readonly string Name;
        public readonly PlaceAcquisition Acquisition;
        internal PlaceKnowledge(int cell, string name, int origin, int language, int donor, int turn, PlaceAcquisition acquisition)
        {
            CellId = cell; Name = name; OriginBandId = origin; OriginLanguageId = language;
            LearnedFromBandId = donor; LearnedTurn = turn; Acquisition = acquisition;
        }
    }

    public sealed partial class Game
    {
        private bool culturalPlaceNames;
        private Dictionary<int, Dictionary<int, PlaceKnowledge>> placeNames;
        public bool CulturalPlaceNames { get { return culturalPlaceNames; } private set { culturalPlaceNames = value; } }

        public ReadOnlyCollection<PlaceKnowledge> KnownPlaces(int bandId)
        {
            Dictionary<int, PlaceKnowledge> atlas;
            return (placeNames != null && placeNames.TryGetValue(bandId, out atlas) ?
                atlas.Values.OrderBy(p => p.CellId).ToList() : new List<PlaceKnowledge>()).AsReadOnly();
        }

        public PlaceKnowledge KnownPlace(int bandId, int cellId)
        {
            Dictionary<int, PlaceKnowledge> atlas; PlaceKnowledge place;
            return placeNames != null && placeNames.TryGetValue(bandId, out atlas) && atlas.TryGetValue(cellId, out place) ? place : null;
        }

        public string Place(int cellId, int bandId)
        {
            if (cellId < 0 || cellId >= World.Cells.Length) return "Uncharted place";
            if (CulturalPlaceNames)
            {
                PlaceKnowledge place = KnownPlace(bandId, cellId);
                return place == null ? "Uncharted place" : place.Name;
            }
            Band band = Bands.Find(b => b.Id == bandId);
            return band == null ? "Uncharted place" : LanguageGenerator.PlaceName(Languages[band.LanguageId],
                World.Cells[cellId].Terrain.ToString().ToLowerInvariant(), World.Cells[cellId].RegionId);
        }

        /// <summary>Opt in after replaying an older story, without advancing it or revealing new ground.</summary>
        public string EnableCulturalPlaceNames()
        {
            if (CulturalPlaceNames) return "Your people already keep their own names for the map.";
            CulturalPlaceNames = true;
            placeNames = new Dictionary<int, Dictionary<int, PlaceKnowledge>>();
            foreach (int cell in Explored.OrderBy(id => id)) RememberDiscovery(Player, cell);
            foreach (Band band in Bands.Where(b => b.Id != Player.Id).OrderBy(b => b.Id))
            {
                // The founding scenario has only one band of each ancestry; all later
                // bands of the player's ancestry are daughters of the player in this ruleset.
                if (band.Ancestry == Player.Ancestry) InheritPlaceKnowledge(Player, band);
                if (band.Population > 0) DiscoverPlaces(band.Id, band.CellId);
            }
            return "Your map now remembers names coined by your people and names learned through contact.";
        }

        /// <summary>Observe a place and the same two rings used by the player's fog of knowledge.</summary>
        public int DiscoverPlaces(int bandId, int centerCellId)
        {
            if (!CulturalPlaceNames) return 0;
            Band band = Bands.Find(b => b.Id == bandId);
            if (band == null) throw new ArgumentOutOfRangeException("bandId");
            if (centerCellId < 0 || centerCellId >= World.Cells.Length) throw new ArgumentOutOfRangeException("centerCellId");
            HashSet<int> seen = new HashSet<int> { centerCellId };
            foreach (int neighbor in World.Cells[centerCellId].Neighbors)
            { seen.Add(neighbor); foreach (int next in World.Cells[neighbor].Neighbors) seen.Add(next); }
            int count = 0;
            foreach (int cell in seen.OrderBy(id => id))
            {
                if (RememberDiscovery(band, cell)) count++;
                if (IsPlayerTribe(band.Id)) { Explored.Add(cell); if (TribesEnabled) RememberTribePlace(KnownPlace(band.Id, cell)); }
            }
            return count;
        }

        /// <summary>Peaceful neighbors exchange their maps; existing names are never overwritten.</summary>
        public int SharePlaceKnowledge(int firstBandId, int secondBandId)
        {
            if (!CulturalPlaceNames || firstBandId == secondBandId) return 0;
            Band first = Bands.Find(b => b.Id == firstBandId), second = Bands.Find(b => b.Id == secondBandId);
            if (first == null || second == null) throw new ArgumentOutOfRangeException(first == null ? "firstBandId" : "secondBandId");
            if (first.Population <= 0 || second.Population <= 0 || EncounterRules.BandsHostile(this, first.Id, second.Id) ||
                first.CellId != second.CellId && !World.Cells[first.CellId].Neighbors.Contains(second.CellId)) return 0;
            PlaceKnowledge[] fromFirst = KnownPlaces(first.Id).ToArray(), fromSecond = KnownPlaces(second.Id).ToArray();
            int firstLearned = AdoptPlaces(first, second.Id, fromSecond, PlaceAcquisition.Shared);
            int secondLearned = AdoptPlaces(second, first.Id, fromFirst, PlaceAcquisition.Shared);
            int learnedByPlayer = first.Id == Player.Id ? firstLearned : second.Id == Player.Id ? secondLearned : 0;
            if (learnedByPlayer > 0)
            {
                Band donor = first.Id == Player.Id ? second : first;
                Log("In peaceful contact, " + donor.Name + " shares " + learnedByPlayer +
                    " unfamiliar places. Your people keep the names their guides use.");
            }
            return firstLearned + secondLearned;
        }

        private Dictionary<int, PlaceKnowledge> PlaceAtlas(int bandId)
        {
            if (placeNames == null) placeNames = new Dictionary<int, Dictionary<int, PlaceKnowledge>>();
            Dictionary<int, PlaceKnowledge> atlas;
            if (!placeNames.TryGetValue(bandId, out atlas)) { atlas = new Dictionary<int, PlaceKnowledge>(); placeNames.Add(bandId, atlas); }
            return atlas;
        }

        private bool RememberDiscovery(Band band, int cellId)
        {
            Dictionary<int, PlaceKnowledge> atlas = PlaceAtlas(band.Id);
            if (atlas.ContainsKey(cellId)) return false;
            LanguageProfile language = Languages[band.LanguageId];
            Cell cell = World.Cells[cellId];
            string concept = cell.Terrain == Terrain.Ocean || cell.Terrain == Terrain.Coast || cell.Terrain == Terrain.Wetland || cell.Terrain == Terrain.Ice ? "water" :
                cell.Terrain == Terrain.Forest ? "forest" : cell.Terrain == Terrain.Mountains || cell.Terrain == Terrain.Hills ? "mountain" :
                cell.Terrain == Terrain.Desert ? "stone" : "grass";
            string root = language.Words[concept];
            HashSet<string> used = new HashSet<string>(atlas.Values.Select(p => p.Name), StringComparer.Ordinal);
            string name; int attempt = 0;
            do
            {
                uint local = PlaceMix(unchecked((uint)Seed) ^ PlaceMix((uint)cellId + 1) ^
                    PlaceMix((uint)band.Id + 0x9e3779b9u) ^ PlaceMix((uint)band.LanguageId + 0x85ebca6bu) ^ PlaceMix((uint)attempt++));
                StringBuilder suffix = new StringBuilder();
                // A semantic root plus a local epithet gives neighboring places their
                // own identity without borrowing a global discoverer's vocabulary.
                int syllables = 2 + (int)(local % 2);
                for (int i = 0; i < syllables; i++)
                {
                    local = PlaceMix(local + 0x9e3779b9u);
                    suffix.Append(language.Settings.Onsets[(int)(local % (uint)language.Settings.Onsets.Length)]);
                    local = PlaceMix(local + 0x85ebca6bu);
                    suffix.Append(language.Settings.Vowels[(int)(local % (uint)language.Settings.Vowels.Length)]);
                }
                // A crowded or very small phoneme palette can exhaust short forms.
                // Extend with additional native syllables, never numerical tile IDs.
                for (int extra = 0; extra < attempt / 32; extra++)
                {
                    local = PlaceMix(local + (uint)extra + 0xc2b2ae35u);
                    suffix.Append(language.Settings.Onsets[(int)(local % (uint)language.Settings.Onsets.Length)]);
                    local = PlaceMix(local + 0x27d4eb2fu);
                    suffix.Append(language.Settings.Vowels[(int)(local % (uint)language.Settings.Vowels.Length)]);
                }
                name = Char.ToUpperInvariant(root[0]) + root.Substring(1) + "-" + suffix;
            } while (used.Contains(name));
            atlas.Add(cellId, new PlaceKnowledge(cellId, name, band.Id, band.LanguageId, -1, Turn, PlaceAcquisition.Discovered));
            return true;
        }

        private static uint PlaceMix(uint value)
        { unchecked { value ^= value >> 16; value *= 0x7feb352du; value ^= value >> 15; value *= 0x846ca68bu; return value ^ (value >> 16); } }

        private int AdoptPlaces(Band recipient, int donor, IEnumerable<PlaceKnowledge> places, PlaceAcquisition acquisition)
        {
            Dictionary<int, PlaceKnowledge> atlas = PlaceAtlas(recipient.Id); int count = 0;
            foreach (PlaceKnowledge place in places.OrderBy(p => p.CellId))
            {
                if (atlas.ContainsKey(place.CellId)) continue;
                atlas.Add(place.CellId, new PlaceKnowledge(place.CellId, place.Name, place.OriginBandId,
                    place.OriginLanguageId, donor, Turn, acquisition)); count++;
                if (IsPlayerTribe(recipient.Id)) { Explored.Add(place.CellId); if (TribesEnabled) RememberTribePlace(atlas[place.CellId]); }
            }
            return count;
        }

        private void InheritPlaceKnowledge(Band parent, Band daughter)
        { if (CulturalPlaceNames) AdoptPlaces(daughter, parent.Id, KnownPlaces(parent.Id), PlaceAcquisition.Inherited); }

        private void ShareNearbyPlaceKnowledge(Band band)
        {
            if (!CulturalPlaceNames || band.Population <= 0) return;
            foreach (Band other in Bands.Where(b => b.Id != band.Id && b.Population > 0 &&
                (b.CellId == band.CellId || World.Cells[band.CellId].Neighbors.Contains(b.CellId))).OrderBy(b => b.Id).ToArray())
                SharePlaceKnowledge(band.Id, other.Id);
        }

        private void DiscoverAndShareBandPlaces()
        {
            if (!CulturalPlaceNames) return;
            Band[] living = Bands.Where(b => b.Population > 0).OrderBy(b => b.Id).ToArray();
            foreach (Band band in living) DiscoverPlaces(band.Id, band.CellId);
            foreach (Band band in living) ShareNearbyPlaceKnowledge(band);
        }
    }
}
