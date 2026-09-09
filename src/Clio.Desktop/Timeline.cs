using System;
using System.Globalization;
using System.Text.RegularExpressions;
using Clio.Simulation;

namespace Clio.Desktop
{
    // Presentation only. Saved calendars and their simulation rules remain intact.
    internal static class Timeline
    {
        public static string PaceDescription { get { return "Two priorities per turn"; } }

        public static string Label(Game game, int turn)
        {
            if (game == null) throw new ArgumentNullException("game");
            return "Turn " + Math.Max(1, turn).ToString("N0", CultureInfo.InvariantCulture);
        }

        public static string ConditionLabel(Game game)
        { return Text(game, HistoryTime.ConditionLabel(game)); }

        // Display vocabulary can evolve without rewriting replay-visible journal
        // text. Keep Text unchanged: released journal construction also uses it.
        public static string DisplayText(Game game, string text)
        {
            text = Text(game, text);
            if (String.IsNullOrEmpty(text)) return text;
            text = Regex.Replace(text, @"\b(?:provisioning|provision|travel) capacity\b", delegate(Match match)
            { return Char.IsUpper(match.Value[0]) ? "Food reserves" : "food reserves"; }, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            text = text.Replace("community's capacity to meet future needs", "community's food reserves for future needs");
            text = text.Replace("Retain more capacity.", "Retain more food.");
            text = Regex.Replace(text, @"\bCapacity is secure\b", "Food reserves are secure", RegexOptions.CultureInvariant);
            text = Regex.Replace(text, @"\bwhen capacity allows\b", "when food reserves allow", RegexOptions.CultureInvariant);
            return Regex.Replace(text, @"\bcapacity(?= (?:twice|for (?:your people|the community)|offered)\b)", "food reserves", RegexOptions.CultureInvariant);
        }

        public static string Text(Game game, string text)
        {
            if (String.IsNullOrEmpty(text)) return text;
            // Released simulation messages contain calendar labels. Translate the
            // label for this view without rewriting their replay-visible records.
            int span = game == null ? 0 : HistoryTime.YearsPerTurn(game.Pace);
            if (span > 0)
                text = Regex.Replace(text, @"\bYear ([0-9][0-9,]*)\b", delegate(Match match)
                {
                    long year;
                    if (!Int64.TryParse(match.Groups[1].Value, NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out year)) return match.Value;
                    return "Turn " + (year / span + 1).ToString("N0", CultureInfo.InvariantCulture);
                }, RegexOptions.CultureInvariant);
            text = Regex.Replace(text, @"\b(?:Chapter|Turn) ([0-9][0-9,]*)\b", delegate(Match match)
            {
                long turn;
                if (!Int64.TryParse(match.Groups[1].Value, NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out turn)) return match.Value;
                return "Turn " + Math.Max(1, turn).ToString("N0", CultureInfo.InvariantCulture);
            }, RegexOptions.CultureInvariant);
            text = Regex.Replace(text, @"\b(mild|lean|harsh|abundant) years\b", "$1 conditions", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            return Regex.Replace(text, @"\bchapters?\b", delegate(Match match)
            {
                string replacement = match.Value.EndsWith("s", StringComparison.OrdinalIgnoreCase) ? "turns" : "turn";
                return Char.IsUpper(match.Value[0]) ? Char.ToUpperInvariant(replacement[0]) + replacement.Substring(1) : replacement;
            }, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        }
    }
}
