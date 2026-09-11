using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using Clio.Simulation;

namespace Clio.Desktop
{
    /// <summary>
    /// A saved story's header: the settings it was founded with and its decision record. CLIO-STORY-15
    /// writes named key=value lines ended by a blank line. Earlier versions stored the same values by
    /// position, each version appending to the last, so one frozen key order reads all of them.
    /// </summary>
    internal sealed class StoryHeader
    {
        internal const string CurrentVersion = "CLIO-STORY-15";
        private const int CurrentVersionNumber = 15;
        private const int MaximumNameLength = 40;

        // Frozen. CLIO-STORY-1 to 14 stored these values on lines 1-15 in exactly this order: version 1
        // wrote the first five, each later version appended more, and versions 11-14 wrote all fifteen.
        // Version 15 requires every key. A key added later must be optional and default to the old behavior.
        private static readonly string[] Keys = { "seed", "style", "ancestry", "bands", "name", "culture", "pace", "rules",
            "places", "salt", "tribes", "terrain", "personalities", "gatherings", "decisions" };

        private sealed class RuleFlag
        {
            internal string Key, Enabled, Legacy, Unknown;
            internal Func<GameSettings, bool> Get;
            internal Action<GameSettings, bool> Set;
        }

        private static readonly RuleFlag[] Flags =
        {
            new RuleFlag { Key = "places", Enabled = "CulturalPlaces", Legacy = "LegacyPlaces", Unknown = "Unknown place naming rules.",
                Get = s => s.CulturalPlaceNames, Set = (s, on) => s.CulturalPlaceNames = on },
            new RuleFlag { Key = "salt", Enabled = "SaltEconomy", Legacy = "LegacySalt", Unknown = "Unknown salt rules.",
                Get = s => s.SaltEnabled, Set = (s, on) => s.SaltEnabled = on },
            new RuleFlag { Key = "tribes", Enabled = "TribalBands", Legacy = "LegacyBands", Unknown = "Unknown band organization rules.",
                Get = s => s.TribesEnabled, Set = (s, on) => s.TribesEnabled = on },
            new RuleFlag { Key = "terrain", Enabled = "TerrainTravel", Legacy = "LegacyTravel", Unknown = "Unknown terrain travel rules.",
                Get = s => s.TerrainTravelEnabled, Set = (s, on) => s.TerrainTravelEnabled = on },
            new RuleFlag { Key = "personalities", Enabled = "BandPersonalities", Legacy = "LegacyPersonalities", Unknown = "Unknown band personality rules.",
                Get = s => s.BandPersonalitiesEnabled, Set = (s, on) => s.BandPersonalitiesEnabled = on },
            new RuleFlag { Key = "gatherings", Enabled = "GatheringRelations", Legacy = "LegacyGatherings", Unknown = "Unknown gathering rules.",
                Get = s => s.GatheringsEnabled, Set = (s, on) => s.GatheringsEnabled = on }
        };

        internal GameSettings Settings;
        /// <summary>The encoded decision record, or null for a story saved before decisions were recorded.</summary>
        internal string Decisions;
        /// <summary>The index of the first command line.</summary>
        internal int CommandStart;

        internal static List<string> Write(GameSettings settings, string decisions)
        {
            if (decisions == null) throw new ArgumentNullException("decisions");
            Dictionary<string, string> values = new Dictionary<string, string>(StringComparer.Ordinal);
            values.Add("seed", settings.Seed.ToString(CultureInfo.InvariantCulture));
            values.Add("style", settings.Style.ToString());
            values.Add("ancestry", settings.Ancestry.ToString());
            values.Add("bands", settings.FourBands ? "4" : "1");
            values.Add("name", Convert.ToBase64String(Encoding.UTF8.GetBytes(settings.BandName ?? "")));
            values.Add("culture", settings.FoundingCulture.ToString());
            values.Add("pace", settings.Pace.ToString());
            values.Add("rules", settings.Rules.ToString());
            foreach (RuleFlag flag in Flags) values.Add(flag.Key, flag.Get(settings) ? flag.Enabled : flag.Legacy);
            values.Add("decisions", decisions);
            List<string> lines = new List<string> { CurrentVersion };
            foreach (string key in Keys) lines.Add(key + "=" + values[key]);
            lines.Add("");
            return lines;
        }

        internal static StoryHeader Read(string[] lines)
        {
            int version = lines.Length == 0 ? 0 : Version(lines[0]);
            if (version == 0) throw new InvalidDataException("Unsupported story version.");
            Dictionary<string, string> values = new Dictionary<string, string>(StringComparer.Ordinal);
            int commandStart;
            if (version < CurrentVersionNumber)
            {
                int count = Math.Min(version, 11) + 4;
                if (lines.Length <= count) throw new InvalidDataException("The story header is incomplete.");
                for (int i = 0; i < count; i++) values.Add(Keys[i], lines[i + 1]);
                commandStart = count + 1;
            }
            else
            {
                int line = 1;
                for (; line < lines.Length && lines[line].Length > 0; line++)
                {
                    int separator = lines[line].IndexOf('=');
                    string key = separator > 0 ? lines[line].Substring(0, separator) : "";
                    if (Array.IndexOf(Keys, key) < 0) throw new InvalidDataException("Unknown story header entry.");
                    if (values.ContainsKey(key)) throw new InvalidDataException("Repeated story header entry: " + key + ".");
                    values.Add(key, lines[line].Substring(separator + 1));
                }
                if (line == lines.Length || values.Count != Keys.Length) throw new InvalidDataException("The story header is incomplete.");
                commandStart = line + 1;
            }

            int seed;
            if (!Int32.TryParse(values["seed"], NumberStyles.Integer, CultureInfo.InvariantCulture, out seed)) throw new InvalidDataException("Invalid story settings.");
            LanguageStyle style = ParseEnum<LanguageStyle>(values["style"], "Invalid story settings.");
            Ancestry ancestry = ParseEnum<Ancestry>(values["ancestry"], "Invalid story settings.");
            if (values["bands"] != "1" && values["bands"] != "4") throw new InvalidDataException("Invalid story settings.");
            string name;
            try { name = Encoding.UTF8.GetString(Convert.FromBase64String(values["name"])); }
            catch (FormatException error) { throw new InvalidDataException("Invalid story settings.", error); }
            if (name.Length > MaximumNameLength) throw new InvalidDataException("Story exceeds prototype limits.");

            GameSettings settings = new GameSettings(seed, style, ancestry, values["bands"] == "4", name);
            string text;
            if (values.TryGetValue("culture", out text)) settings.FoundingCulture = ParseEnum<CultureTemplateId>(text, "Unknown founding culture template.");
            if (values.TryGetValue("pace", out text)) settings.Pace = ParseEnum<HistoryPace>(text, "Unknown historical pace.");
            if (values.TryGetValue("rules", out text)) settings.Rules = ParseEnum<SimulationRules>(text, "Unknown encounter rules.");
            foreach (RuleFlag flag in Flags)
            {
                if (!values.TryGetValue(flag.Key, out text)) continue;
                if (text != flag.Enabled && text != flag.Legacy) throw new InvalidDataException(flag.Unknown);
                flag.Set(settings, text == flag.Enabled);
            }
            if (settings.TribesEnabled && (settings.Rules != SimulationRules.MobileUnits || !settings.SaltEnabled))
                throw new InvalidDataException("Tribes require moving unit and salt rules.");
            if (settings.BandPersonalitiesEnabled && !settings.TribesEnabled)
                throw new InvalidDataException("Band personalities require tribal households.");
            if (settings.GatheringsEnabled && (!settings.TribesEnabled || !settings.CulturalPlaceNames))
                throw new InvalidDataException("Gatherings require tribal households and cultural place knowledge.");

            string decisions;
            values.TryGetValue("decisions", out decisions);
            return new StoryHeader { Settings = settings, Decisions = decisions, CommandStart = commandStart };
        }

        private static int Version(string line)
        {
            for (int version = 1; version <= CurrentVersionNumber; version++)
                if (line == "CLIO-STORY-" + version.ToString(CultureInfo.InvariantCulture)) return version;
            return 0;
        }

        private static T ParseEnum<T>(string text, string error) where T : struct
        {
            T value;
            if (!Enum.TryParse<T>(text, out value) || !Enum.IsDefined(typeof(T), value)) throw new InvalidDataException(error);
            return value;
        }
    }
}
