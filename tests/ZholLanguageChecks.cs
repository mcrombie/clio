using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using Clio.Simulation;

namespace Clio.Tests
{
    public static class ZholLanguageChecks
    {
        private static int assertions;
        public static int Run()
        {
            assertions = 0;
            CheckFoundingVocabulary(); CheckDescentAndContact(); CheckGameIntegration();
            return assertions;
        }

        private static LanguageProfile Create(int seed, int id, LanguageStyle style)
        { return HistoricalCultures.Create(CultureTemplateId.Zhol, seed, id, style); }
        private static string Snapshot(object value)
        { return HistoricalCultureChecks.Snapshot(value, false); }

        private static void CheckFoundingVocabulary()
        {
            Check((int)CultureTemplateId.Generated == 0 && (int)CultureTemplateId.EarlyChinese == 1 &&
                (int)CultureTemplateId.EarlyEgyptian == 2 && (int)CultureTemplateId.Sumerian == 3 && (int)CultureTemplateId.Zhol == 4,
                "Adding Zhol does not reinterpret any released culture ID.");
            string[] concepts = { "water", "river", "mountain", "forest", "people", "home", "wolf", "cattle", "fire", "sun", "stone", "grass" };
            string[] foundingWords = { "shua", "herio", "shanta", "linque", "rente", "jiaca", "lanbo", "niuca", "huego", "riol", "shiedra", "caoba" };
            LanguageProfile baseline = Create(73421, 7, LanguageStyle.Flowing);
            foreach (int seed in new[] { Int32.MinValue, -37, 0, 73421, Int32.MaxValue })
            foreach (LanguageStyle style in Enum.GetValues(typeof(LanguageStyle)))
            {
                LanguageProfile language = Create(seed, 7, style);
                Check(language.Name == "Zhol" && language.Words["people"] == "rente", "The chosen language name is independent of its people word, seed and sound-style selector.");
                Check(language.Id == 7 && language.RootId == 7 && language.ParentId == -1 && language.Generation == 0 && language.Template == CultureTemplateId.Zhol,
                    "A Zhol founding profile has an explicit unbranched identity.");
                Check(language.Words.Count == 12 && language.Words.Values.Distinct(StringComparer.Ordinal).Count() == 12,
                    "The founding lexicon has twelve distinct concept roots.");
                for (int i = 0; i < concepts.Length; i++)
                {
                    Check(language.Words[concepts[i]] == foundingWords[i] && language.Etymons[concepts[i]] == "7:" + concepts[i],
                        "The curated v1 root and its founding provenance remain stable: " + concepts[i]);
                    CheckInventory(language.Words[concepts[i]], language.Settings);
                }
                Check(Snapshot(language.Settings) == Snapshot(baseline.Settings), "The curated founding inventory does not change with another founder's generated palette.");
                Check(Snapshot(language) == Snapshot(Create(seed, 7, style)), "Repeated creation has exactly the same profile.");
                LanguageProfile generated = LanguageGenerator.Create(seed, language.Settings, 21);
                Check(generated.Words.Count == 12, "The ordinary generator accepts Zhol's ASCII palette for further word creation.");
            }
            Check(HistoricalCultures.DefaultBandName(CultureTemplateId.Zhol) == "Herio-rente", "The suggested people name is stable replay data.");
            Check(Snapshot(new object[] { Create(73421, 0, LanguageStyle.Flowing), HistoricalCultures.DefaultBandName(CultureTemplateId.Zhol) }) ==
                "88DE983DCA83EB7CBCD603EA79123B6C99933BB6674AD19E86F213EFEA4256EF",
                "The complete v1 founding profile, inventory and names match the released replay fixture.");
            Check(HistoricalCultures.Description(CultureTemplateId.Zhol).Contains("invented") &&
                HistoricalCultures.Description(CultureTemplateId.Zhol).Contains("zhong + espa\u00f1ol") &&
                HistoricalCultures.LanguageLabel(CultureTemplateId.Zhol).Contains("fusion"),
                "The personal fusion and creator's naming premise are distinguished from historical templates.");
            LanguageProfile edited = Create(0, 7, LanguageStyle.Flowing);
            edited.Words["water"] = "edited"; edited.Settings.Onsets = "z"; edited.Etymons["water"] = "edited";
            Check(Snapshot(Create(73421, 7, LanguageStyle.Flowing)) == Snapshot(baseline), "Editing one language cannot mutate curated roots or another profile's inventory.");
        }

        private static void CheckDescentAndContact()
        {
            LanguageProfile root = Create(0, 0, LanguageStyle.Flowing);
            LanguageProfile donor = LanguageGenerator.Create(17, LanguageStyle.Crisp, 9);
            string original = Snapshot(root), donorBefore = Snapshot(donor);
            foreach (int seed in new[] { Int32.MinValue, -9, 0, 1, 17, 91, Int32.MaxValue })
            {
                LanguageProfile child = LanguageGenerator.Branch(root, 1, seed);
                Check(child.ParentId == 0 && child.RootId == 0 && child.Generation == 1 && child.Template == CultureTemplateId.Zhol,
                    "Daughter languages retain their Zhol family provenance.");
                Check(child.Name == Char.ToUpperInvariant(child.Words["people"][0]) + child.Words["people"].Substring(1) && root.Name == "Zhol",
                    "Descendants receive the existing derived naming treatment while the founding proper name remains Zhol.");
                Check(Snapshot(child) == Snapshot(LanguageGenerator.Branch(root, 1, seed)) && child.Words.Any(w => w.Value != root.Words[w.Key]),
                    "A branch is deterministic and applies a productive change.");
                foreach (string concept in root.Words.Keys)
                {
                    Check(child.Words[concept] == ApplyLaw(root.Words[concept], child.SoundChange) && child.Etymons[concept] == root.Etymons[concept],
                        "Each inherited word follows the published simultaneous sound law without losing its etymon.");
                    CheckInventory(child.Words[concept], child.Settings);
                }
                string oldPlace = LanguageGenerator.PlaceName(root, "river", 551).ToLowerInvariant();
                string newPlace = LanguageGenerator.PlaceName(child, "river", 551).ToLowerInvariant();
                Check(newPlace == ApplyLaw(oldPlace, child.SoundChange), "Place names evolve by the same law as their vocabulary.");
                LanguageProfile grandchild = LanguageGenerator.Branch(child, 2, unchecked(seed + 31));
                Check(grandchild.Generation == 2 && grandchild.RootId == 0 && grandchild.Template == CultureTemplateId.Zhol,
                    "Several generations remain part of the Zhol language family.");
            }
            foreach (string concept in root.Words.Keys)
            {
                LanguageProfile borrowed = LanguageGenerator.Borrow(root, donor, concept);
                Check(borrowed.Name == "Zhol" && borrowed.Id == root.Id && borrowed.Template == CultureTemplateId.Zhol,
                    "A loan, including the people word, never silently renames Zhol.");
                Check(borrowed.Etymons[concept] == donor.Etymons[concept] && borrowed.Loans.Count == 1 && borrowed.Loans[0].DonorLanguageId == donor.Id,
                    "A borrowed root records its donor separately from founding ancestry.");
                Check(root.Words.All(w => w.Key == concept || borrowed.Words[w.Key] == w.Value), "One lexical loan leaves the rest of the founding words intact.");
                CheckInventory(borrowed.Words[concept], borrowed.Settings);
            }
            Check(Snapshot(root) == original && Snapshot(donor) == donorBefore, "Branching, naming and borrowing leave both source profiles unchanged.");
        }

        private static void CheckGameIntegration()
        {
            foreach (int seed in new[] { -37, 0, 73421 })
            {
                Game game = NewGame(seed, CultureTemplateId.Zhol), replay = NewGame(seed, CultureTemplateId.Zhol), generated = NewGame(seed, CultureTemplateId.Generated);
                Check(game.Player.Name == "Herio-rente" && game.Languages[0].Name == "Zhol" && game.FoundingCulture == CultureTemplateId.Zhol,
                    "An unnamed campaign starts the chosen people speaking exactly Zhol.");
                Check(Snapshot(game.World) == Snapshot(generated.World) && Snapshot(game.Beasts) == Snapshot(generated.Beasts),
                    "Choosing Zhol changes neither terrain and resources nor initial wildlife.");
                for (int id = 1; id < 4; id++)
                    Check(Snapshot(game.Languages[id]) == Snapshot(generated.Languages[id]) && Snapshot(game.Bands[id]) == Snapshot(generated.Bands[id]),
                        "Every other founder keeps its generated identity, language and starting resources.");
                for (int turn = 0; turn < 8; turn++)
                {
                    foreach (Game current in new[] { game, replay, generated })
                    { current.Forage(); if (turn == 0) current.Camp(); else current.Forage(); current.EndTurn(); }
                    Check(game.Player.Population == generated.Player.Population && game.Player.Food == generated.Player.Food &&
                        game.Player.CellId == generated.Player.CellId && game.Depletion.SequenceEqual(generated.Depletion),
                        "The personal language introduces no economy, starting-location or survival advantage.");
                    Check(Snapshot(game.Encounters.Records) == Snapshot(replay.Encounters.Records) && Snapshot(game.Languages) == Snapshot(replay.Languages) &&
                        Snapshot(game.Player) == Snapshot(replay.Player), "Replaying the same story preserves the chosen names, words and ordinary encounters.");
                    foreach (string field in new[] { "actionRandom", "ecologyRandom" })
                    {
                        FieldInfo stream = typeof(Game).GetField(field, BindingFlags.Instance | BindingFlags.NonPublic);
                        Check(stream.GetValue(game).Equals(stream.GetValue(generated)), "The personal vocabulary consumes no extra simulation randomness.");
                    }
                }
                Check(game.Languages[0].Name == "Zhol" && replay.Languages[0].Name == "Zhol", "Normal chapter advancement and replay retain the founding language's proper name.");
            }
            Game named = new Game(17, LanguageStyle.Resonant, Ancestry.Human, false, "  My hearth  ", CultureTemplateId.Zhol);
            Check(named.Player.Name == "My hearth" && named.Languages[0].Name == "Zhol", "A custom band name does not overwrite the selected language name.");
        }
        private static Game NewGame(int seed, CultureTemplateId template)
        { return new Game(seed, LanguageStyle.Flowing, Ancestry.Human, true, "", template, HistoryPace.Generations, SimulationRules.MobileUnits); }
        private static string ApplyLaw(string value, string law)
        {
            string mappings = law.Substring(0, law.IndexOf(", in every position", StringComparison.Ordinal));
            Dictionary<char, char> changes = new Dictionary<char, char>();
            foreach (string raw in mappings.Split(';')) { string part = raw.Trim(); changes.Add(part[0], part[part.Length - 1]); }
            return new string(value.Select(c => changes.ContainsKey(c) ? changes[c] : c).ToArray());
        }
        private static void CheckInventory(string word, LanguageSettings settings)
        {
            string allowed = settings.Onsets + settings.Vowels + settings.Codas;
            Check(word.Length > 0 && word.All(c => c >= 'a' && c <= 'z' && allowed.IndexOf(c) >= 0) && word.Any(c => settings.Vowels.IndexOf(c) >= 0),
                "Founding, inherited and borrowed forms fit the supported lowercase ASCII inventory and contain a vowel.");
        }
        private static void Check(bool condition, string message)
        { assertions++; if (!condition) throw new InvalidOperationException("Zhol language check failed: " + message); }
    }
}
