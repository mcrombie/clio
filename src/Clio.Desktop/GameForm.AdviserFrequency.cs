using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;

namespace Clio.Desktop
{
    public enum AdviserFrequency { High, Moderate, Low, None }

    internal static class AdviserPreferences
    {
        internal static string DefaultPath { get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Clio", "advisers.txt"); } }
        internal static AdviserFrequency Read(string path)
        {
            try
            {
                AdviserFrequency value;
                if (!String.IsNullOrEmpty(path) && File.Exists(path) && Enum.TryParse(File.ReadAllText(path).Trim(), true, out value) && Enum.IsDefined(typeof(AdviserFrequency), value)) return value;
            }
            catch (IOException) { } catch (UnauthorizedAccessException) { }
            return AdviserFrequency.High;
        }
        internal static bool Write(string path, AdviserFrequency value)
        {
            if (String.IsNullOrEmpty(path)) return true; // Isolated render/test forms have no user preferences.
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)));
                File.WriteAllText(path, value.ToString()); return true;
            }
            catch (IOException) { return false; } catch (UnauthorizedAccessException) { return false; }
        }
    }

    public sealed partial class GameForm
    {
        private AdviserFrequency adviserFrequency = AdviserFrequency.High;
        private string adviserPreferencePath;
        private readonly Dictionary<string, int> adviserReviewedTurn = new Dictionary<string, int>();
        private readonly HashSet<string> beginnerLessonsRead = new HashSet<string>();
        private int adviserLastRoutineTurn = -100;

        private static string FrequencyDescription(AdviserFrequency frequency)
        {
            switch (frequency)
            {
                case AdviserFrequency.High: return "Beginner lessons, developments and all warnings. Unresolved warnings return after 3 turns.";
                case AdviserFrequency.Moderate: return "Contextual lessons and important developments, at most one routine reading every 3 turns. All warnings; reminders after 8 turns.";
                case AdviserFrequency.Low: return "Urgent warnings and major developments only. Developments at most once every 8 turns; no beginner lessons or routine reminders.";
                default: return "No automatic adviser cards. Open Advisers or press C whenever you want to consult the council.";
            }
        }

        private void SetAdviserFrequency(AdviserFrequency frequency)
        {
            if (!Enum.IsDefined(typeof(AdviserFrequency), frequency)) return;
            adviserFrequency = frequency; adviserGuidanceEnabled = frequency != AdviserFrequency.None;
            adviserLastRoutineTurn = -100;
            bool saved = AdviserPreferences.Write(adviserPreferencePath, frequency);
            RefreshAdvisers(); buttons.Clear(); HideMapHover();
            status = "Adviser frequency: " + frequency + ". " + (saved ? FrequencyDescription(frequency) : "Set for this session; the preference could not be saved.");
            Invalidate();
        }

        private void DrawAdviserFrequencyChoices(Graphics g, float x, float y, float width)
        {
            float step = width / 4;
            for (int i = 0; i < 4; i++)
            {
                AdviserFrequency choice = (AdviserFrequency)i;
                Button(g, choice.ToString(), x + step * i, y, step - 7, 34, delegate { SetAdviserFrequency(choice); }, adviserFrequency == choice, false);
                MapTip(FrequencyDescription(choice) + " This preference is remembered between games.");
            }
        }

        private bool AutomaticAdviserAllowed(Advisory report)
        {
            if (semiautomatic && IsBeginnerLesson(report)) return false;
            if (adviserFrequency == AdviserFrequency.None || !adviserGuidanceEnabled || !AdviserUnread(report)) return false;
            if (!report.IsGuidance)
                return adviserFrequency != AdviserFrequency.Low || report.Severity >= AdvisorySeverity.Urgent;
            if (IsBeginnerLesson(report))
            {
                if (beginnerLessonsRead.Contains(report.Key) || adviserFrequency == AdviserFrequency.Low) return false;
                if (adviserFrequency == AdviserFrequency.Moderate && !IsContextualBeginnerLesson(report)) return false;
            }
            else if (adviserFrequency == AdviserFrequency.Low && report.GuidancePriority < 3) return false;
            int gap = adviserFrequency == AdviserFrequency.High ? 0 : adviserFrequency == AdviserFrequency.Moderate ? 3 : 8;
            return game.Turn - adviserLastRoutineTurn >= gap;
        }

        private void RefreshAdviserReminders()
        {
            int gap = adviserFrequency == AdviserFrequency.High ? 3 : adviserFrequency == AdviserFrequency.Moderate ? 8 : Int32.MaxValue;
            foreach (Advisory report in adviserReports.Where(a => !a.IsGuidance))
            {
                int reviewed;
                if (adviserAcknowledged.ContainsKey(report.Key) && adviserReviewedTurn.TryGetValue(report.Key, out reviewed) && game.Turn - reviewed >= gap)
                {
                    adviserAcknowledged.Remove(report.Key);
                    adviserOccurrences[report.Key] = ++adviserOccurrenceSequence;
                }
            }
        }

        private CouncilOpinion AdviserOpinion(Advisory report, CounsellorId voice)
        { return IsBeginnerLesson(report) ? BeginnerLessonOpinion(report, voice) : CouncilPerspectives.Evaluate(game, report, voice); }

        private void OpenAdviserFrequency()
        { OpenAdvisers(null); }
    }
}
