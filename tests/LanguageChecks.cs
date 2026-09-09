using System;
using System.Collections.Generic;
using System.Text;
using Clio.Simulation;

namespace Clio.Tests
{
    public static class LanguageChecks
    {
        private static int assertions;

        public static int Run()
        {
            assertions = 0;
            int[] seeds = { Int32.MinValue, -9137, -1, 0, 1, 2, 3, 7, 17, 41, 91, 271, 2026, 123456789, Int32.MaxValue };
            foreach (LanguageStyle style in Enum.GetValues(typeof(LanguageStyle)))
            {
                foreach (int seed in seeds)
                {
                    LanguageProfile parent = LanguageGenerator.Create(seed, style, 0);
                    string before = Snapshot(parent);
                    Check(before == Snapshot(LanguageGenerator.Create(seed, style, 0)), "Founding language is reproducible.");
                    Check(parent.Id == 0 && parent.ParentId == -1 && parent.RootId == 0, "Root identity is explicit.");
                    Check(parent.Words.Count == 12 && new HashSet<string>(parent.Words.Values).Count == 12, "Twelve distinct founding roots exist.");
                    Check(parent.Name == Capitalize(parent.Words["people"]), "Autonym comes from the people root.");
                    foreach (string word in parent.Words.Values) CheckPhonotactics(word, parent.Settings);

                    LanguageProfile child = LanguageGenerator.Branch(parent, 1, seed);
                    Check(Snapshot(child) == Snapshot(LanguageGenerator.Branch(parent, 1, seed)), "Branching is reproducible.");
                    Check(Snapshot(parent) == before, "Branching does not mutate its ancestor.");
                    Check(child.ParentId == parent.Id && child.RootId == parent.RootId && child.Generation == 1, "Genealogy follows descent.");
                    Check(!Object.ReferenceEquals(parent.Words, child.Words) && !Object.ReferenceEquals(parent.Settings, child.Settings), "Snapshots own their mutable state.");
                    CheckConsistentInheritance(parent, child);
                    LanguageProfile grandchild = LanguageGenerator.Branch(child, 2, unchecked(seed + 79));
                    Check(grandchild.ParentId == child.Id && grandchild.RootId == parent.Id && grandchild.Generation == 2, "Multiple generations preserve the root.");
                    CheckConsistentInheritance(child, grandchild);

                    string oldName = LanguageGenerator.PlaceName(parent, "river", 551);
                    string newName = LanguageGenerator.PlaceName(child, "river", 551);
                    Check(newName.EndsWith("-" + child.Words["river"], StringComparison.Ordinal), "Place name retains the feature root.");
                    Check(ApplyLaw(oldName.ToLowerInvariant(), child.SoundChange) == newName.ToLowerInvariant(), "Fixed naming recipes inherit the same sound law.");
                    Check(oldName == LanguageGenerator.PlaceName(parent, "river", 551), "Place naming is deterministic.");
                    Check(LanguageGenerator.PlaceName(parent, "woods", 3) == LanguageGenerator.PlaceName(parent, "forest", 3), "Terrain aliases use preserved concepts.");
                    Check(LanguageGenerator.Intelligibility(parent, parent, 0.0) == 1.0, "Identical languages are fully intelligible in the abstraction.");
                    double baseline = LanguageGenerator.Intelligibility(parent, grandchild, 0.0);
                    double exposed = LanguageGenerator.Intelligibility(parent, grandchild, 0.6);
                    Check(baseline >= 0.0 && baseline <= exposed && exposed <= 1.0, "Exposure improves a bounded estimate.");
                    Check(LanguageGenerator.Intelligibility(parent, grandchild, 1.0) == 1.0, "Full learned exposure reaches fluent comprehension.");
                }
            }

            LanguageProfile root = LanguageGenerator.Create(734, LanguageStyle.Crisp, 100);
            HashSet<string> branches = new HashSet<string>();
            for (int i = 0; i < 32; i++) branches.Add(SnapshotWords(LanguageGenerator.Branch(root, i + 101, i)));
            Check(branches.Count > 3, "The same ancestor can produce several distinct systematic branches.");

            LanguageProfile donor = LanguageGenerator.Create(118, LanguageStyle.Resonant, 200);
            string recipientBefore = Snapshot(root);
            string donorBefore = Snapshot(donor);
            LanguageProfile borrowed = LanguageGenerator.Borrow(root, donor, "cattle");
            Check(Snapshot(root) == recipientBefore && Snapshot(donor) == donorBefore, "Contact mutates neither participant.");
            Check(borrowed.Id == root.Id && borrowed.ParentId == root.ParentId && borrowed.RootId == root.RootId, "A loan is not a genealogical branch.");
            Check(borrowed.Loans.Count == 1 && borrowed.Loans[0].DonorLanguageId == donor.Id, "The loan has explicit donor provenance.");
            Check(borrowed.Etymons["cattle"] == donor.Etymons["cattle"], "The borrowed root tracks its donor etymon.");
            Check(borrowed.Name == root.Name, "A contact event does not silently rename the community.");
            foreach (string concept in root.Words.Keys)
                if (concept != "cattle") Check(root.Words[concept] == borrowed.Words[concept], "Unborrowed vocabulary survives contact.");
            LanguageProfile loanChild = LanguageGenerator.Branch(borrowed, 301, 765);
            CheckConsistentInheritance(borrowed, loanChild);
            Check(loanChild.Etymons["cattle"] == donor.Etymons["cattle"], "Later sound change preserves a loan's etymology.");
            loanChild.Loans[0].AdaptedForm = "edited";
            loanChild.Settings.Onsets = "z";
            loanChild.Etymons["water"] = "edited";
            Check(borrowed.Loans[0].AdaptedForm != "edited" && borrowed.Settings.Onsets != "z" && borrowed.Etymons["water"] != "edited", "Nested provenance and settings are deeply copied.");

            LanguageSettings minimal = new LanguageSettings { Onsets = "mn", Vowels = "ai", Codas = "",
                CodaChance = 0.0, MinSyllables = 2, MaxSyllables = 2 };
            for (int i = 0; i < 10; i++)
            {
                LanguageProfile small = LanguageGenerator.Create(i, minimal, i);
                Check(new HashSet<string>(small.Words.Values).Count == 12, "Small valid inventories still produce twelve distinct roots.");
                foreach (string word in small.Words.Values) CheckPhonotactics(word, minimal);
            }
            LanguageProfile unusual = LanguageGenerator.Create(41, new LanguageSettings { Onsets = "qx", Vowels = "u",
                Codas = "", CodaChance = 0.0, MinSyllables = 2, MaxSyllables = 4 }, 500);
            CheckConsistentInheritance(unusual, LanguageGenerator.Branch(unusual, 501, 5));
            LanguageProfile noCodas = LanguageGenerator.Create(42, minimal, 502);
            LanguageProfile adaptedLoan = LanguageGenerator.Borrow(noCodas, donor, "cattle");
            CheckPhonotactics(adaptedLoan.Words["cattle"], minimal, false);

            int stableId = LanguageGenerator.Create(87, LanguageStyle.Flowing).Id;
            Check(stableId >= 0 && stableId == LanguageGenerator.Create(87, LanguageStyle.Flowing).Id, "Convenience root IDs are stable.");
            ExpectArgument(delegate { LanguageGenerator.Branch(root, root.Id, 2); }, "A branch cannot reuse its parent's ID.");
            ExpectArgument(delegate { LanguageGenerator.Create(1, new LanguageSettings { Onsets = "m", Vowels = "a", Codas = "", CodaChance = 0, MinSyllables = 1, MaxSyllables = 1 }, 0); }, "Impossible palettes fail clearly.");
            ExpectArgument(delegate { LanguageGenerator.Create(1, new LanguageSettings { Onsets = "mm" }, 0); }, "Duplicate inventory entries are rejected.");
            ExpectArgument(delegate { LanguageGenerator.Intelligibility(root, donor, Double.NaN); }, "Nonfinite exposure is rejected.");
            ExpectArgument(delegate { LanguageGenerator.Borrow(root, donor, "nonexistent"); }, "Undefined loan concepts are rejected.");
            return assertions;
        }

        private static void CheckConsistentInheritance(LanguageProfile parent, LanguageProfile child)
        {
            bool changed = false;
            foreach (string concept in parent.Words.Keys)
            {
                string oldForm = parent.Words[concept];
                string newForm = child.Words[concept];
                Check(ApplyLaw(oldForm, child.SoundChange) == newForm, "Every cognate follows the documented law: " + concept);
                Check(parent.Etymons[concept] == child.Etymons[concept], "Inherited roots keep their etymon identity.");
                if (oldForm != newForm) changed = true;
            }
            Check(changed, "A branch has at least one observable inherited sound change.");
        }

        // Independently interpret the public rule description, rather than calling the implementation's mapper.
        private static string ApplyLaw(string word, string law)
        {
            string mappings = law.Substring(0, law.IndexOf(", in every position", StringComparison.Ordinal));
            Dictionary<char, char> map = new Dictionary<char, char>();
            foreach (string raw in mappings.Split(';'))
            {
                string part = raw.Trim();
                map.Add(part[0], part[part.Length - 1]);
            }
            StringBuilder result = new StringBuilder();
            foreach (char sound in word) result.Append(map.ContainsKey(sound) ? map[sound] : sound);
            return result.ToString();
        }

        private static void CheckPhonotactics(string word, LanguageSettings settings) { CheckPhonotactics(word, settings, true); }
        private static void CheckPhonotactics(string word, LanguageSettings settings, bool checkLength)
        {
            int core = word.Length % 2 == 0 ? word.Length : word.Length - 1;
            if (checkLength) Check(core / 2 >= settings.MinSyllables && core / 2 <= settings.MaxSyllables, "Configured syllable range is respected.");
            for (int i = 0; i < core; i += 2)
            {
                Check(settings.Onsets.IndexOf(word[i]) >= 0, "Syllables use allowed onset consonants.");
                Check(settings.Vowels.IndexOf(word[i + 1]) >= 0, "Syllables use allowed vowels.");
            }
            if (core < word.Length) Check(settings.CodaChance > 0.0 && settings.Codas.IndexOf(word[word.Length - 1]) >= 0, "Only the final syllable has an allowed coda.");
        }

        private static string Snapshot(LanguageProfile profile)
        {
            return profile.Id + "/" + profile.ParentId + "/" + profile.RootId + "/" + profile.Generation + "/" + profile.Name
                + "/" + profile.SoundChange + "/" + profile.Settings.Onsets + "/" + profile.Settings.Vowels + "/" + profile.Settings.Codas
                + "/" + SnapshotWords(profile) + "/" + profile.Loans.Count;
        }

        private static string SnapshotWords(LanguageProfile profile)
        {
            List<string> keys = new List<string>(profile.Words.Keys);
            keys.Sort(StringComparer.Ordinal);
            StringBuilder result = new StringBuilder();
            foreach (string key in keys) result.Append(key).Append('=').Append(profile.Words[key]).Append('@').Append(profile.Etymons[key]).Append(';');
            return result.ToString();
        }

        private static string Capitalize(string value) { return Char.ToUpperInvariant(value[0]) + value.Substring(1); }
        private static void ExpectArgument(Action action, string message)
        {
            try { action(); }
            catch (ArgumentException) { assertions++; return; }
            throw new InvalidOperationException("Language check failed: " + message);
        }
        private static void Check(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException("Language check failed: " + message);
            assertions++;
        }
    }
}
