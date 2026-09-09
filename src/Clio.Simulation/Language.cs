using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Clio.Simulation
{
    public enum LanguageStyle { Flowing, Crisp, Resonant }

    /// <summary>A small, editable sound palette. Syllables are CV; only the last may have a coda.</summary>
    public sealed class LanguageSettings
    {
        public string Onsets = "mnlrsvhwy";
        public string Vowels = "aeiou";
        public string Codas = "nlr";
        public int MinSyllables = 2;
        public int MaxSyllables = 3;
        public double CodaChance = 0.15;

        public static LanguageSettings ForStyle(LanguageStyle style)
        {
            LanguageSettings value = new LanguageSettings();
            if (style == LanguageStyle.Crisp)
            {
                value.Onsets = "ptksnrldg";
                value.Codas = "knrst";
                value.MaxSyllables = 2;
                value.CodaChance = 0.55;
            }
            else if (style == LanguageStyle.Resonant)
            {
                value.Onsets = "bdgmnlrv";
                value.Codas = "mnlr";
                value.CodaChance = 0.40;
            }
            else if (style != LanguageStyle.Flowing)
                throw new ArgumentOutOfRangeException("style");
            return value;
        }

        public LanguageSettings Copy()
        {
            return new LanguageSettings { Onsets = Onsets, Vowels = Vowels, Codas = Codas,
                MinSyllables = MinSyllables, MaxSyllables = MaxSyllables, CodaChance = CodaChance };
        }
    }

    public sealed class LoanRecord
    {
        public int DonorLanguageId;
        public string Concept;
        public string DonorForm;
        public string AdaptedForm;
        public string Etymon;
    }

    /// <summary>Serializable language snapshot. Generator operations copy their inputs.</summary>
    public sealed class LanguageProfile
    {
        public int Id;
        public int ParentId = -1;
        public int RootId;
        public int Generation;
        public string Name;
        public string SoundChange;
        public LanguageStyle Style;
        public CultureTemplateId Template;
        public LanguageSettings Settings;
        public Dictionary<string, string> Words = new Dictionary<string, string>(StringComparer.Ordinal);
        public Dictionary<string, string> Etymons = new Dictionary<string, string>(StringComparer.Ordinal);
        public List<LoanRecord> Loans = new List<LoanRecord>();
    }

    /// <summary>Deterministic, intentionally compact language flavor; not a natural-language simulator.</summary>
    public static class LanguageGenerator
    {
        private static readonly string[] Concepts = { "water", "river", "mountain", "forest", "people", "home",
            "wolf", "cattle", "fire", "sun", "stone", "grass" };
        private static readonly double[] ConceptWeights = { 1.4, 1.0, 0.8, 0.8, 1.5, 1.5, 0.6, 0.6, 1.3, 1.0, 0.8, 0.7 };
        private static readonly string[] SoundLaws = {
            "p > f; t > s; k > h, in every position (simultaneously)",
            "p > b; t > d; k > g, in every position (simultaneously)",
            "a > e; e > i; o > u, in every position (simultaneously)",
            "r > l; w > v, in every position (simultaneously)",
            "b > v; d > z; g > h, in every position (simultaneously)",
            "i > e; u > o, in every position (simultaneously)",
            "s > h; v > w, in every position (simultaneously)",
            "l > r; n > m, in every position (simultaneously)",
            "a > o; o > a; e > i; i > e; u > a, in every position (simultaneously)" };
        private static readonly string[] RuleFrom = { "ptk", "ptk", "aeo", "rw", "bdg", "iu", "sv", "ln", "aoeiu" };
        private static readonly string[] RuleTo = { "fsh", "bdg", "eiu", "lv", "vzh", "eo", "hw", "rm", "oaiea" };

        public static LanguageProfile Create(int seed, LanguageStyle style)
        {
            int id = unchecked((int)(Mix(unchecked((uint)seed) ^ (uint)style * 7919U) & 0x3fffffffU));
            return Create(seed, style, id);
        }

        public static LanguageProfile Create(int seed, LanguageStyle style, int id)
        {
            LanguageProfile result = Create(seed, LanguageSettings.ForStyle(style), id);
            result.Style = style;
            return result;
        }

        public static LanguageProfile Create(int seed, LanguageSettings settings, int id)
        {
            if (id < 0) throw new ArgumentOutOfRangeException("id");
            ValidateSettings(settings);
            StableRandom random = new StableRandom(seed);
            LanguageProfile result = new LanguageProfile { Id = id, RootId = id,
                Settings = settings.Copy(), SoundChange = "Founding vocabulary; no parent language" };
            HashSet<string> used = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < Concepts.Length; i++)
            {
                string word = null;
                for (int attempt = 0; attempt < 128; attempt++)
                {
                    string candidate = MakeWord(settings, random);
                    if (used.Add(candidate)) { word = candidate; break; }
                }
                if (word == null) word = FindUnusedWord(settings, used);
                if (word == null) throw new ArgumentException("The sound palette cannot form twelve distinct roots.", "settings");
                result.Words.Add(Concepts[i], word);
                result.Etymons.Add(Concepts[i], id.ToString(CultureInfo.InvariantCulture) + ":" + Concepts[i]);
            }
            result.Name = Capitalize(result.Words["people"]);
            return result;
        }

        public static LanguageProfile Branch(LanguageProfile parent, int id, int seed)
        {
            ValidateProfile(parent, "parent");
            if (id < 0 || id == parent.Id) throw new ArgumentOutOfRangeException("id", "A branch needs a distinct nonnegative ID.");
            LanguageProfile child = Copy(parent);
            child.Id = id;
            child.ParentId = parent.Id;
            child.Generation = parent.Generation + 1;
            StableRandom random = new StableRandom(seed);
            int firstRule = random.Next(SoundLaws.Length);
            int selectedRule = -1;
            // Skip rules absent from this inventory. The final vowel permutation is always productive.
            for (int offset = 0; offset < SoundLaws.Length; offset++)
            {
                int rule = (firstRule + offset) % SoundLaws.Length;
                foreach (string form in parent.Words.Values)
                    if (ApplyRule(form, rule) != form) { selectedRule = rule; break; }
                if (selectedRule >= 0) break;
            }
            if (selectedRule < 0)
                throw new ArgumentException("No supported sound law changes this vocabulary.", "parent");
            List<string> keys = new List<string>(parent.Words.Keys);
            keys.Sort(StringComparer.Ordinal);
            foreach (string concept in keys)
                child.Words[concept] = ApplyRule(parent.Words[concept], selectedRule);
            child.Settings.Onsets = Unique(ApplyRule(parent.Settings.Onsets, selectedRule));
            child.Settings.Vowels = Unique(ApplyRule(parent.Settings.Vowels, selectedRule));
            child.Settings.Codas = Unique(ApplyRule(parent.Settings.Codas, selectedRule));
            child.SoundChange = SoundLaws[selectedRule];
            child.Name = Capitalize(child.Words["people"]);
            return child;
        }

        /// <summary>Translate a saved naming recipe, keeping salt and feature fixed across language changes.</summary>
        public static string PlaceName(LanguageProfile language, string feature, int salt)
        {
            ValidateProfile(language, "language");
            string root = FeatureConcept(feature);
            string[] modifiers = { "sun", "stone", "wolf", "grass", "fire", "people", "water", "cattle" };
            int index = (int)(Mix(unchecked((uint)salt)) % (uint)modifiers.Length);
            string modifier = modifiers[index];
            if (modifier == root) modifier = "home";
            return Capitalize(language.Words[modifier]) + "-" + language.Words[root];
        }

        /// <summary>Borrowing is contact, not descent. Returns a replacement snapshot with the same identity.</summary>
        public static LanguageProfile Borrow(LanguageProfile recipient, LanguageProfile donor, string concept)
        {
            ValidateProfile(recipient, "recipient");
            ValidateProfile(donor, "donor");
            if (recipient.Id == donor.Id) throw new ArgumentException("Borrowing requires a different donor language.", "donor");
            if (concept == null || !recipient.Words.ContainsKey(concept) || !donor.Words.ContainsKey(concept))
                throw new ArgumentException("Both languages must contain the borrowed concept.", "concept");
            LanguageProfile result = Copy(recipient);
            string adapted = Adapt(donor.Words[concept], recipient.Settings);
            result.Words[concept] = adapted;
            result.Etymons[concept] = donor.Etymons[concept];
            result.Loans.Add(new LoanRecord { DonorLanguageId = donor.Id, Concept = concept,
                DonorForm = donor.Words[concept], AdaptedForm = adapted, Etymon = donor.Etymons[concept] });
            // The community's existing autonym is a proper name; a lexical loan does not silently rename it.
            return result;
        }

        /// <summary>A 0..1 gameplay estimate, with listener exposure separate from lexical resemblance.</summary>
        public static double Intelligibility(LanguageProfile listener, LanguageProfile speaker, double exposure)
        {
            ValidateProfile(listener, "listener");
            ValidateProfile(speaker, "speaker");
            if (Double.IsNaN(exposure) || Double.IsInfinity(exposure)) throw new ArgumentOutOfRangeException("exposure");
            exposure = Math.Max(0.0, Math.Min(1.0, exposure));
            double sum = 0.0;
            double weights = 0.0;
            for (int i = 0; i < Concepts.Length; i++)
            {
                string concept = Concepts[i];
                string a = listener.Words[concept];
                string b = speaker.Words[concept];
                double similarity = 1.0 - (double)EditDistance(a, b) / Math.Max(a.Length, b.Length);
                bool inherited = listener.Etymons[concept] == speaker.Etymons[concept];
                double lexical = a == b ? 1.0 : similarity * (inherited ? 1.0 : 0.45);
                sum += lexical * ConceptWeights[i];
                weights += ConceptWeights[i];
            }
            double baseline = sum / weights;
            return Math.Max(0.0, Math.Min(1.0, baseline + (1.0 - baseline) * exposure));
        }

        private static string FeatureConcept(string feature)
        {
            if (String.IsNullOrWhiteSpace(feature)) return "home";
            string value = feature.Trim().ToLowerInvariant();
            if (Array.IndexOf(Concepts, value) >= 0) return value;
            switch (value)
            {
                case "ocean": case "sea": case "lake": case "coast": case "wetland": case "marsh": return "water";
                case "hills": case "hill": case "mountains": case "highland": case "peak": return "mountain";
                case "woodland": case "woods": case "jungle": case "taiga": return "forest";
                case "plains": case "plain": case "steppe": case "grassland": case "tundra": return "grass";
                case "desert": case "barren": case "barrens": case "badlands": return "stone";
                case "volcano": return "fire";
                case "village": case "settlement": case "town": case "camp": return "home";
                default: return "home";
            }
        }

        private static LanguageProfile Copy(LanguageProfile source)
        {
            LanguageProfile result = new LanguageProfile { Id = source.Id, ParentId = source.ParentId,
                RootId = source.RootId, Generation = source.Generation, Name = source.Name, SoundChange = source.SoundChange,
                Style = source.Style, Template = source.Template, Settings = source.Settings.Copy(),
                Words = new Dictionary<string, string>(source.Words, StringComparer.Ordinal),
                Etymons = new Dictionary<string, string>(source.Etymons, StringComparer.Ordinal) };
            foreach (LoanRecord loan in source.Loans)
                result.Loans.Add(new LoanRecord { DonorLanguageId = loan.DonorLanguageId, Concept = loan.Concept,
                    DonorForm = loan.DonorForm, AdaptedForm = loan.AdaptedForm, Etymon = loan.Etymon });
            return result;
        }

        private static string MakeWord(LanguageSettings settings, StableRandom random)
        {
            int syllables = settings.MinSyllables + random.Next(settings.MaxSyllables - settings.MinSyllables + 1);
            StringBuilder word = new StringBuilder();
            for (int i = 0; i < syllables; i++)
            {
                word.Append(settings.Onsets[random.Next(settings.Onsets.Length)]);
                word.Append(settings.Vowels[random.Next(settings.Vowels.Length)]);
            }
            if (settings.Codas.Length > 0 && random.Fraction() < settings.CodaChance)
                word.Append(settings.Codas[random.Next(settings.Codas.Length)]);
            return word.ToString();
        }

        private static string FindUnusedWord(LanguageSettings settings, HashSet<string> used)
        {
            int codaOptions = settings.CodaChance == 0.0 ? 1 : settings.Codas.Length + (settings.CodaChance < 1.0 ? 1 : 0);
            for (int syllables = settings.MinSyllables; syllables <= settings.MaxSyllables; syllables++)
            {
                long capacity = codaOptions;
                for (int j = 0; j < syllables; j++) capacity = Math.Min(1000000L, capacity * settings.Onsets.Length * settings.Vowels.Length);
                for (int index = 0; index < Math.Min(capacity, used.Count + 1L); index++)
                {
                    int number = index;
                    int coda = number % codaOptions;
                    number /= codaOptions;
                    StringBuilder word = new StringBuilder();
                    for (int j = 0; j < syllables; j++)
                    {
                        word.Append(settings.Onsets[number % settings.Onsets.Length]); number /= settings.Onsets.Length;
                        word.Append(settings.Vowels[number % settings.Vowels.Length]); number /= settings.Vowels.Length;
                    }
                    if (settings.CodaChance >= 1.0) word.Append(settings.Codas[coda]);
                    else if (settings.CodaChance > 0.0 && coda > 0) word.Append(settings.Codas[coda - 1]);
                    string candidate = word.ToString();
                    if (used.Add(candidate)) return candidate;
                }
            }
            return null;
        }

        private static string ApplyRule(string form, int rule)
        {
            StringBuilder changed = new StringBuilder(form.Length);
            foreach (char letter in form)
            {
                int index = RuleFrom[rule].IndexOf(letter);
                changed.Append(index < 0 ? letter : RuleTo[rule][index]);
            }
            return changed.ToString();
        }

        private static string Adapt(string form, LanguageSettings settings)
        {
            StringBuilder result = new StringBuilder();
            for (int i = 0; i < form.Length; i++)
            {
                char sound = form[i];
                bool vowel = "aeiou".IndexOf(sound) >= 0;
                bool finalCoda = !vowel && i == form.Length - 1;
                string inventory = vowel ? settings.Vowels : (finalCoda ? (settings.CodaChance == 0.0 ? "" : settings.Codas) : settings.Onsets);
                if (finalCoda && inventory.Length == 0)
                {
                    result.Append(Closest(sound, settings.Onsets));
                    result.Append(settings.Vowels[0]);
                }
                else result.Append(Closest(sound, inventory));
            }
            return result.ToString();
        }

        private static char Closest(char sound, string inventory)
        {
            if (inventory.IndexOf(sound) >= 0) return sound;
            string[] groups = { "pbmfvw", "tdsznlr", "kghy", "aei", "ou" };
            foreach (string group in groups)
                if (group.IndexOf(sound) >= 0)
                    foreach (char candidate in group)
                        if (inventory.IndexOf(candidate) >= 0) return candidate;
            return inventory[0];
        }

        private static int EditDistance(string a, string b)
        {
            int[] previous = new int[b.Length + 1];
            int[] current = new int[b.Length + 1];
            for (int j = 0; j <= b.Length; j++) previous[j] = j;
            for (int i = 1; i <= a.Length; i++)
            {
                current[0] = i;
                for (int j = 1; j <= b.Length; j++)
                    current[j] = Math.Min(Math.Min(current[j - 1] + 1, previous[j] + 1),
                        previous[j - 1] + (a[i - 1] == b[j - 1] ? 0 : 1));
                int[] swap = previous; previous = current; current = swap;
            }
            return previous[b.Length];
        }

        private static string Capitalize(string word) { return Char.ToUpperInvariant(word[0]) + word.Substring(1); }
        private static string Unique(string value)
        {
            StringBuilder result = new StringBuilder();
            foreach (char letter in value) if (result.ToString().IndexOf(letter) < 0) result.Append(letter);
            return result.ToString();
        }

        private static void ValidateSettings(LanguageSettings settings)
        {
            if (settings == null) throw new ArgumentNullException("settings");
            if (String.IsNullOrEmpty(settings.Onsets) || String.IsNullOrEmpty(settings.Vowels) || settings.Codas == null)
                throw new ArgumentException("Onsets and vowels must be nonempty; codas may be empty.", "settings");
            if (settings.MinSyllables < 1 || settings.MaxSyllables < settings.MinSyllables || settings.MaxSyllables > 4)
                throw new ArgumentException("Syllable range must lie between one and four.", "settings");
            if (Double.IsNaN(settings.CodaChance) || settings.CodaChance < 0.0 || settings.CodaChance > 1.0
                || (settings.Codas.Length == 0 && settings.CodaChance > 0.0))
                throw new ArgumentException("Coda chance must be 0..1, and zero when there are no codas.", "settings");
            CheckInventory(settings.Onsets, "bcdfghjklmnpqrstvwxyz");
            CheckInventory(settings.Codas, "bcdfghjklmnpqrstvwxyz");
            CheckInventory(settings.Vowels, "aeiou");
        }

        private static void CheckInventory(string inventory, string allowed)
        {
            if (Unique(inventory).Length != inventory.Length) throw new ArgumentException("Sound inventories must not contain duplicate letters.");
            foreach (char letter in inventory)
                if (allowed.IndexOf(letter) < 0) throw new ArgumentException("Use lowercase ASCII consonants and the vowels a e i o u.");
        }

        private static void ValidateProfile(LanguageProfile profile, string argument)
        {
            if (profile == null) throw new ArgumentNullException(argument);
            if (profile.Settings == null || profile.Words == null || profile.Etymons == null || profile.Loans == null)
                throw new ArgumentException("Incomplete language profile.", argument);
            ValidateSettings(profile.Settings);
            foreach (string concept in Concepts)
                if (!profile.Words.ContainsKey(concept) || String.IsNullOrEmpty(profile.Words[concept])
                    || !profile.Etymons.ContainsKey(concept) || String.IsNullOrEmpty(profile.Etymons[concept]))
                    throw new ArgumentException("Missing founding concept or etymon: " + concept, argument);
        }

        private static uint Mix(uint value)
        {
            unchecked { value ^= value >> 16; value *= 0x7feb352dU; value ^= value >> 15;
                value *= 0x846ca68bU; value ^= value >> 16; return value; }
        }

        private sealed class StableRandom
        {
            private uint state;
            public StableRandom(int seed) { state = Mix(unchecked((uint)seed)) ^ 0x9e3779b9U; if (state == 0) state = 1; }
            private uint NextUInt() { unchecked { state ^= state << 13; state ^= state >> 17; state ^= state << 5; return state; } }
            public int Next(int exclusiveMaximum) { return (int)(NextUInt() % (uint)exclusiveMaximum); }
            public double Fraction() { return NextUInt() / 4294967296.0; }
        }
    }
}
