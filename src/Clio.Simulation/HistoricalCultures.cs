using System;
using System.Globalization;

namespace Clio.Simulation
{
    public enum CultureTemplateId { Generated, EarlyChinese, EarlyEgyptian, Sumerian, Zhol }

    /// <summary>
    /// Founding vocabularies for a fictional world: historical inspirations and personal inventions.
    /// Source boundaries, simplifications and invented forms are recorded in docs/HISTORICAL_TEMPLATES.md.
    /// Released roots, palettes and names are replay data for CLIO-STORY-2: changes require a version/migration.
    /// </summary>
    public static class HistoricalCultures
    {
        private static readonly string[] Concepts = { "water", "river", "mountain", "forest", "people", "home",
            "wolf", "cattle", "fire", "sun", "stone", "grass" };

        public static string DisplayName(CultureTemplateId template)
        {
            switch (template)
            {
                case CultureTemplateId.Generated: return "An imagined culture";
                case CultureTemplateId.EarlyChinese: return "Early Chinese inspiration";
                case CultureTemplateId.EarlyEgyptian: return "Early Egyptian inspiration";
                case CultureTemplateId.Sumerian: return "Sumerian inspiration";
                case CultureTemplateId.Zhol: return "Zhol - your invented language";
                default: throw new ArgumentOutOfRangeException("template");
            }
        }

        public static string LanguageLabel(CultureTemplateId template)
        {
            switch (template)
            {
                case CultureTemplateId.Generated: return "Generated language";
                case CultureTemplateId.EarlyChinese: return "Old Chinese-inspired";
                case CultureTemplateId.EarlyEgyptian: return "Early Egyptian-inspired";
                case CultureTemplateId.Sumerian: return "Sumerian-inspired";
                case CultureTemplateId.Zhol: return "Zhol - Mandarin-Spanish fusion";
                default: throw new ArgumentOutOfRangeException("template");
            }
        }

        public static string Description(CultureTemplateId template)
        {
            switch (template)
            {
                case CultureTemplateId.Generated: return "An original sound palette and inherited names, shaped by your seed. Your people begin a history of their own.";
                case CultureTemplateId.EarlyChinese: return "Experimental roots inspired by Old Chinese scholarship. Simplified sounds and fictional names; your history can take its own course.";
                case CultureTemplateId.EarlyEgyptian: return "Experimental roots inspired by Egyptian words. Vowels and compound names are invented for play; your history can take its own course.";
                case CultureTemplateId.Sumerian: return "Experimental Sumerian-inspired roots from southern Mesopotamia. Simplified spellings and fictional names; no prescribed historical path.";
                case CultureTemplateId.Zhol: return "An invented Mandarin-Spanish fusion named from zhong + espa\u00f1ol. Its founding words and later history are your own fiction.";
                default: throw new ArgumentOutOfRangeException("template");
            }
        }

        public static string DefaultBandName(CultureTemplateId template)
        {
            switch (template)
            {
                case CultureTemplateId.Generated: return "";
                case CultureTemplateId.EarlyChinese: return "Krong-ming";
                case CultureTemplateId.EarlyEgyptian: return "Iteru-remet";
                case CultureTemplateId.Sumerian: return "Id-lu";
                case CultureTemplateId.Zhol: return "Herio-rente";
                default: throw new ArgumentOutOfRangeException("template");
            }
        }

        public static LanguageProfile Create(CultureTemplateId template, int seed, int id, LanguageStyle generatedStyle)
        {
            if (template == CultureTemplateId.Generated) return LanguageGenerator.Create(seed, generatedStyle, id);
            if (id < 0) throw new ArgumentOutOfRangeException("id");
            if (!Enum.IsDefined(typeof(LanguageStyle), generatedStyle)) throw new ArgumentOutOfRangeException("generatedStyle");
            if (template == CultureTemplateId.Zhol) return ZholLanguage.Create(id, generatedStyle);
            string[] roots; LanguageSettings settings;
            switch (template)
            {
                case CultureTemplateId.EarlyChinese:
                    roots = new[] { "stur", "krong", "sngrar", "mok", "ming", "kra", "rang", "ngwe", "khwej", "nik", "dak", "tsen" };
                    settings = new LanguageSettings { Onsets = "ptkbdgmnsrlhwyj", Vowels = "aeiou", Codas = "ptkmnrgj",
                        MinSyllables = 1, MaxSyllables = 2, CodaChance = 0.85 };
                    break;
                case CultureTemplateId.EarlyEgyptian:
                    roots = new[] { "mu", "iteru", "dju", "khetu", "remet", "per", "wenesh", "ka", "rekah", "ra", "iner", "semu" };
                    settings = new LanguageSettings { Onsets = "ptkbdgmnrshwjf", Vowels = "aeiu", Codas = "mnrtskh",
                        MinSyllables = 1, MaxSyllables = 3, CodaChance = 0.35 };
                    break;
                case CultureTemplateId.Sumerian:
                    roots = new[] { "a", "id", "kur", "tir", "lu", "e", "urbar", "gud", "izi", "ud", "na", "u" };
                    settings = new LanguageSettings { Onsets = "bdgklmnprstzh", Vowels = "aeiu", Codas = "bdgklmnrsz",
                        MinSyllables = 1, MaxSyllables = 2, CodaChance = 0.60 };
                    break;
                default: throw new ArgumentOutOfRangeException("template");
            }
            LanguageProfile result = new LanguageProfile { Id = id, RootId = id, Template = template, Style = generatedStyle,
                Settings = settings, SoundChange = "Experimental founding template; later changes are fictional" };
            for (int i = 0; i < Concepts.Length; i++)
            {
                result.Words.Add(Concepts[i], roots[i]);
                result.Etymons.Add(Concepts[i], id.ToString(CultureInfo.InvariantCulture) + ":" + Concepts[i]);
            }
            string people = result.Words["people"];
            result.Name = Char.ToUpperInvariant(people[0]) + people.Substring(1);
            return result;
        }
    }
}
