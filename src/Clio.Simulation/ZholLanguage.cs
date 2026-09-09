using System.Globalization;

namespace Clio.Simulation
{
    /// <summary>
    /// Zhol founding vocabulary v1. A personal sound-fusion invention, named
    /// from zhong + espanol by its creator; the proper name is not the people root.
    /// These spellings, inventory and naming choices are replay data. Future
    /// revisions need a new template or an explicit save-version migration.
    /// </summary>
    internal static class ZholLanguage
    {
        // Creative source cues explain the intended blend. They are design
        // notes for invented words, not historical derivations or translations.
        private static readonly string[,] Roots = {
            { "water", "shua" },       // shui + agua
            { "river", "herio" },      // he + rio
            { "mountain", "shanta" },  // shan + montana
            { "forest", "linque" },    // lin + bosque
            { "people", "rente" },     // ren + gente
            { "home", "jiaca" },       // jia + casa
            { "wolf", "lanbo" },       // lang + lobo
            { "cattle", "niuca" },     // niu + vaca
            { "fire", "huego" },       // huo + fuego
            { "sun", "riol" },         // ri + sol
            { "stone", "shiedra" },    // shi + piedra
            { "grass", "caoba" }       // cao + hierba
        };

        internal static LanguageProfile Create(int id, LanguageStyle style)
        {
            LanguageProfile result = new LanguageProfile {
                Id = id, RootId = id, Template = CultureTemplateId.Zhol, Style = style,
                Name = "Zhol", SoundChange = "Personal invented Mandarin-Spanish fusion; founding vocabulary",
                Settings = new LanguageSettings { Onsets = "bcdfghjklmnpqrstwyz", Vowels = "aeiou", Codas = "nrls",
                    MinSyllables = 2, MaxSyllables = 3, CodaChance = 0.20 }
            };
            for (int i = 0; i < Roots.GetLength(0); i++)
            {
                string concept = Roots[i, 0];
                result.Words.Add(concept, Roots[i, 1]);
                result.Etymons.Add(concept, id.ToString(CultureInfo.InvariantCulture) + ":" + concept);
            }
            return result;
        }
    }
}
