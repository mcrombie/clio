using System;

namespace Clio.Simulation
{
    /// <summary>
    /// Everything a story is founded with. The defaults are the original Classic rules, so settings that
    /// name only the world and its founders replay exactly as the earliest stories did.
    /// </summary>
    public sealed class GameSettings
    {
        public int Seed;
        public LanguageStyle Style;
        public Ancestry Ancestry;
        public bool FourBands;
        public string BandName;
        public CultureTemplateId FoundingCulture = CultureTemplateId.Generated;
        public HistoryPace Pace = HistoryPace.LegacySeasons;
        public SimulationRules Rules = SimulationRules.Classic;
        public bool CulturalPlaceNames, SaltEnabled, TribesEnabled, TerrainTravelEnabled, BandPersonalitiesEnabled, GatheringsEnabled;
        public bool GuidedOpening;

        public GameSettings() { }

        public GameSettings(int seed, LanguageStyle style, Ancestry ancestry, bool fourBands, string bandName)
        { Seed = seed; Style = style; Ancestry = ancestry; FourBands = fourBands; BandName = bandName; }

        public GameSettings Clone() { return (GameSettings)MemberwiseClone(); }
    }
}
