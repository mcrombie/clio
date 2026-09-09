using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using Clio.Simulation;

namespace Clio.Tests
{
    public static class HistoricalCultureChecks
    {
        private static int assertions;
        public static int Run()
        {
            assertions = 0;
            HashSet<string> vocabularies = new HashSet<string>(StringComparer.Ordinal);
            HashSet<string> palettes = new HashSet<string>(StringComparer.Ordinal);
            HashSet<string> names = new HashSet<string>(StringComparer.Ordinal);
            foreach (CultureTemplateId template in Enum.GetValues(typeof(CultureTemplateId)))
            {
                LanguageProfile language = HistoricalCultures.Create(template, 73421, 0, LanguageStyle.Flowing);
                string before = Snapshot(language, false);
                Check(before == Snapshot(HistoricalCultures.Create(template, 73421, 0, LanguageStyle.Flowing), false), "Template is deterministic.");
                Check(language.Template == template && language.RootId == 0 && language.ParentId == -1, "Founding provenance is explicit.");
                Check(language.Words.Count == 12 && language.Etymons.Count == 12 && new HashSet<string>(language.Words.Values).Count == 12, "Twelve distinct roots and etymons are available.");
                Check(vocabularies.Add(String.Join("|", language.Words.Values)), "Each founding vocabulary is distinct.");
                Check(palettes.Add(language.Settings.Onsets + "/" + language.Settings.Vowels + "/" + language.Settings.Codas), "Each sound palette is distinct.");
                Check(names.Add(language.Name), "Founding language names are distinct.");
                foreach (string word in language.Words.Values)
                {
                    Check(word.Any(c => language.Settings.Vowels.IndexOf(c) >= 0), "Every root has a game vowel.");
                    Check(word.All(c => (language.Settings.Onsets + language.Settings.Codas + language.Settings.Vowels).IndexOf(c) >= 0), "Roots use the documented game inventory.");
                }
                LanguageProfile other = LanguageGenerator.Create(-27, LanguageStyle.Crisp, 5);
                foreach (int seed in new[] { Int32.MinValue, -1, 0, 1, 19, 57, Int32.MaxValue })
                {
                    LanguageProfile child = LanguageGenerator.Branch(language, 1, seed);
                    Check(child.Template == template && child.RootId == 0 && child.ParentId == 0 && child.Generation == 1, "Branching retains template and family provenance.");
                    Check(child.Words.Any(w => w.Value != language.Words[w.Key]), "Fictional sound change is productive.");
                    Check(Snapshot(child, false) == Snapshot(LanguageGenerator.Branch(language, 1, seed), false), "Branch sound change is deterministic.");
                    LanguageProfile grandchild = LanguageGenerator.Branch(child, 2, unchecked(seed + 31));
                    Check(grandchild.Template == template && grandchild.RootId == 0, "Template provenance survives multiple generations.");
                    LanguageProfile loan = LanguageGenerator.Borrow(child, other, "river");
                    Check(loan.Template == template && loan.Id == child.Id, "A lexical loan preserves recipient identity.");
                    Check(loan.Loans.Count == child.Loans.Count + 1 && loan.Etymons["river"] == other.Etymons["river"], "Borrowing records donor provenance separately.");
                    Check(LanguageGenerator.Intelligibility(child, language, 0) >= 0 && LanguageGenerator.Intelligibility(child, language, 0) <= 1, "Historical-inspired profiles support intelligibility.");
                    Check(LanguageGenerator.PlaceName(child, "river", 3).EndsWith("-" + child.Words["river"], StringComparison.Ordinal), "Place naming follows the inherited vocabulary.");
                }
                Check(Snapshot(language, false) == before, "Descendants and loans do not mutate the founding profile.");
                Game themed = new Game(73421, LanguageStyle.Flowing, Ancestry.Human, true, "", template);
                Game generated = new Game(73421, LanguageStyle.Flowing, Ancestry.Human, true, "");
                Check(themed.FoundingCulture == template, "Game retains the selected founding template.");
                Check(themed.Player.Name == (template == CultureTemplateId.Generated ? generated.Player.Name : HistoricalCultures.DefaultBandName(template)), "Blank names use the selected founding recipe.");
                Check(Snapshot(themed.World, false) == Snapshot(generated.World, false), "Culture does not alter geography, climate or resources.");
                Check(Snapshot(themed.Beasts, false) == Snapshot(generated.Beasts, false), "Culture does not alter initial wildlife.");
                Check(themed.Player.CellId == generated.Player.CellId && themed.Player.Population == generated.Player.Population && themed.Player.Food == generated.Player.Food && themed.Player.Cohesion == generated.Player.Cohesion, "Founding location and resources are unchanged.");
                Check(themed.Player.Culture.SequenceEqual(generated.Player.Culture), "No behavioral traits are prescribed by the template.");
                for (int i = 1; i < 4; i++)
                {
                    Check(Snapshot(themed.Languages[i], false) == Snapshot(generated.Languages[i], false), "Other founding languages retain original palettes.");
                    Check(Snapshot(themed.Bands[i], false) == Snapshot(generated.Bands[i], false), "Other founders retain original identities and starting states.");
                }
                Game named = new Game(17, LanguageStyle.Crisp, Ancestry.Dwarf, false, "  Cedar Keepers  ", template);
                Check(named.Player.Name == "Cedar Keepers", "A custom band name overrides historical naming.");
                for (int i = 0; i < 18; i++)
                {
                    Check(themed.Forage() == generated.Forage(), "Template gives no gathering bonus.");
                    if (i == 0) { themed.Camp(); generated.Camp(); }
                    else if (i % 3 == 0) { themed.Tame(); generated.Tame(); }
                    else { themed.Hunt(); generated.Hunt(); }
                    themed.EndTurn(); generated.EndTurn();
                    Check(themed.Player.Population == generated.Player.Population && themed.Player.Food == generated.Player.Food && themed.Player.Cohesion == generated.Player.Cohesion, "Template gives no survival or action bonus.");
                    Check(themed.Depletion.SequenceEqual(generated.Depletion), "Resource depletion evolves equally.");
                }
            }
            foreach (int seed in new[] { Int32.MinValue, -9137, 0, 73421, Int32.MaxValue })
            {
                foreach (LanguageStyle style in Enum.GetValues(typeof(LanguageStyle)))
                {
                    Check(Snapshot(HistoricalCultures.Create(CultureTemplateId.Generated, seed, 0, style), false) == Snapshot(LanguageGenerator.Create(seed, style, 0), false), "Generated template is the original generator.");
                    Game legacy = new Game(seed, style, Ancestry.Elf, true, "");
                    Game explicitDefault = new Game(seed, style, Ancestry.Elf, true, "", CultureTemplateId.Generated);
                    Exercise(legacy); Exercise(explicitDefault);
                    Check(Snapshot(legacy, true) == Snapshot(explicitDefault, true), "Legacy and explicit Generated constructors produce identical replay state.");
                }
            }
            ExpectInvalid(delegate { HistoricalCultures.Create((CultureTemplateId)99, 0, 0, LanguageStyle.Flowing); });
            ExpectInvalid(delegate { new Game(0, LanguageStyle.Flowing, Ancestry.Human, false, "", (CultureTemplateId)(-1)); });
            CheckLegacyFixtures();
            CheckHistoricalFixtures();
            return assertions;
        }

        private static void CheckHistoricalFixtures()
        {
            // CLIO-STORY-2 replays depend on these founding roots, inventories and names.
            // Intentional changes require a save-version migration and an explicit fixture update.
            CultureTemplateId[] templates = { CultureTemplateId.EarlyChinese, CultureTemplateId.EarlyEgyptian, CultureTemplateId.Sumerian };
            string[] fingerprints = {
                "E2EDD2ECDF0A03EC030B9419FDF9A1FBE343415A1F365DCAC1ADD454704804C9",
                "7951B70EA3647B5D8803115440179961A5D3A13D8809BFC144115064EEEE63C9",
                "4EA59605B196D81759872CA71831387296A16180E5CDD45BBCC852CC59A5A7D4" };
            for (int i = 0; i < templates.Length; i++)
            {
                object[] replayData = {
                    HistoricalCultures.Create(templates[i], 73421, 0, LanguageStyle.Flowing),
                    HistoricalCultures.DefaultBandName(templates[i]) };
                Check(Snapshot(replayData, false) == fingerprints[i], "Historical founding data matches the frozen CLIO-STORY-2 fingerprint: " + templates[i] + ".");
            }
        }

        private static void CheckLegacyFixtures()
        {
            // Captured from the unmodified atlas-02 core, before historical templates existed.
            int[] seeds = { 73421, -9137, Int32.MinValue, Int32.MaxValue };
            LanguageStyle[] styles = { LanguageStyle.Flowing, LanguageStyle.Crisp, LanguageStyle.Resonant, LanguageStyle.Flowing };
            Ancestry[] ancestries = { Ancestry.Human, Ancestry.Elf, Ancestry.Dwarf, Ancestry.Goblin };
            string[] initial = {
                "FD21539A3ABDF677EC268494D8BA6D7DA8BF60B83DD0DE21DDF1A472B357F562",
                "7C0BBB1D3A6C50E72209F2B8C9725DA1410D10F8F3E6E5CDF663DA25723A0586",
                "194442A85B37A07DED7893970D81D7C1585F139A4E556BF6B506CB8BB4245104",
                "66A1124D60F6D743E4B3965838A0E45DFE94B0D102CA01FF534FEADB341666F1" };
            string[] played = {
                "056B742A58DEC0B1DAB2A3DF80D925785EC5EB758BDC012BFC72015BFF2922BE",
                "BEDA0E465D4D8B5ED845A89D89B5A4F2A40DDA2DC5E621C3B3B5736F7730F3F5",
                "5AE61AAE7EE3A2782694732EEEA358B7D2CBE2AEBDDBD0580CB7299297D5277D",
                "2C5D394BA2282B6075877B335AD216D04DB0C17A1F5446074F6E3FA081035D85" };
            for (int i = 0; i < seeds.Length; i++)
            {
                Game game = new Game(seeds[i], styles[i], ancestries[i], i == 1 || i == 2, i == 1 ? "Old Hearth" : "");
                Check(Snapshot(game, true) == initial[i], "Generated start matches the original atlas-02 core fingerprint.");
                Exercise(game);
                Check(Snapshot(game, true) == played[i], "Generated replay matches the original atlas-02 core fingerprint, including private RNG state.");
            }
        }

        public static void Exercise(Game game)
        {
            for (int turn = 0; turn < 12; turn++)
            {
                game.Forage();
                if (turn == 0) game.Camp();
                else if (turn % 3 == 0) game.Tame();
                else game.Hunt();
                game.EndTurn();
            }
        }

        // Captures private RNG/progress fields as well as public state. New descriptive provenance
        // is omitted only when comparing a legacy replay; all lexical content is still included.
        public static string Snapshot(object value, bool legacy)
        {
            StringBuilder text = new StringBuilder(); Append(text, value, legacy);
            using (SHA256 hash = SHA256.Create())
                return BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(text.ToString()))).Replace("-", "");
        }
        private static void Append(StringBuilder text, object value, bool legacy)
        {
            if (value == null) { text.Append("null;"); return; }
            Type type = value.GetType(); text.Append(type.FullName).Append(':');
            if (value is string) { text.Append(((string)value).Length).Append(':').Append(value).Append(';'); return; }
            if (value is double) { text.Append(((double)value).ToString("R", CultureInfo.InvariantCulture)).Append(';'); return; }
            if (value is float) { text.Append(((float)value).ToString("R", CultureInfo.InvariantCulture)).Append(';'); return; }
            if (type.IsPrimitive || type.IsEnum) { text.Append(Convert.ToString(value, CultureInfo.InvariantCulture)).Append(';'); return; }
            IDictionary dictionary = value as IDictionary;
            if (dictionary != null)
            {
                List<string> keys = new List<string>(); foreach (object key in dictionary.Keys) keys.Add((string)key);
                keys.Sort(StringComparer.Ordinal); foreach (string key in keys) { Append(text, key, legacy); Append(text, dictionary[key], legacy); }
                text.Append("end;"); return;
            }
            IEnumerable sequence = value as IEnumerable;
            if (sequence != null)
            {
                List<string> items = new List<string>();
                foreach (object item in sequence) { StringBuilder element = new StringBuilder(); Append(element, item, legacy); items.Add(element.ToString()); }
                if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(HashSet<>)) items.Sort(StringComparer.Ordinal);
                foreach (string item in items) text.Append(item); text.Append("end;"); return;
            }
            FieldInfo[] fields = type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Array.Sort(fields, delegate(FieldInfo a, FieldInfo b) { return StringComparer.Ordinal.Compare(a.Name, b.Name); });
            foreach (FieldInfo field in fields)
            {
                if (legacy && (field.Name == "Template" || field.Name == "FoundingCulture" || field.Name == "Pace" || field.Name == "Encounters" ||
                    field.Name == "culturalPlaceNames" || field.Name == "placeNames" || field.Name == "saltEnabled" || field.Name == "saltSources" ||
                    field.Name == "saltExplored" || field.Name == "Salt" || field.Name == "SaltShortageTurns" ||
                    field.DeclaringType == typeof(Game) && (field.Name.StartsWith("tribe", StringComparison.Ordinal) || field.Name.StartsWith("travel", StringComparison.Ordinal) ||
                    field.Name.StartsWith("gathering", StringComparison.Ordinal)))) continue;
                text.Append(field.Name).Append('='); Append(text, field.GetValue(value), legacy);
            }
            text.Append("end;");
        }
        private static void ExpectInvalid(Action action)
        { bool rejected = false; try { action(); } catch (ArgumentOutOfRangeException) { rejected = true; } Check(rejected, "Unknown culture IDs are rejected."); }
        private static void Check(bool condition, string message)
        { assertions++; if (!condition) throw new Exception("Historical culture check failed: " + message); }
    }
}
