using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using Clio.Simulation;

namespace Clio.Desktop
{
    public sealed partial class GameForm
    {
        private bool adviserOpen;
        private List<Advisory> adviserReports = new List<Advisory>();
        private readonly Dictionary<string, AdvisorySeverity> adviserAcknowledged = new Dictionary<string, AdvisorySeverity>();
        private readonly Dictionary<string, int> adviserOccurrences = new Dictionary<string, int>();
        private int adviserOccurrenceSequence;
        private string adviserKey;
        private int adviserFilter, adviserPage;

        // Current counsel is presentation state, separate from recorded outcomes.
        // Refresh only after live commands or story replacement, never during replay.
        private void RefreshAdvisers()
        {
            adviserReports = AdviserReport.Evaluate(game).ToList();
            RefreshAdviserGuidance();
            adviserReports.AddRange(adviserGuidance.OrderByDescending(a => a.Turn));
            adviserReports.AddRange(BeginnerLessons());
            HashSet<string> active = new HashSet<string>(adviserReports.Select(a => a.Key));
            foreach (string key in adviserAcknowledged.Keys.Where(k => !active.Contains(k)).ToArray()) adviserAcknowledged.Remove(key);
            foreach (string key in adviserOccurrences.Keys.Where(k => !active.Contains(k)).ToArray()) adviserOccurrences.Remove(key);
            foreach (string key in active) if (!adviserOccurrences.ContainsKey(key)) adviserOccurrences[key] = ++adviserOccurrenceSequence;
            RefreshAdviserReminders();
            if (game.IsOver) { adviserOpen = false; adviserKey = null; }
        }

        private void ResetAdvisers()
        {
            adviserOpen = false; adviserKey = null; adviserFilter = adviserPage = 0; councilMeet = false;
            adviserAcknowledged.Clear(); adviserOccurrences.Clear(); adviserReviewedTurn.Clear(); beginnerLessonsRead.Clear(); adviserLastRoutineTurn = -100;
            ResetAdviserGuidance(); RefreshAdvisers();
        }

        private bool AdviserUnread(Advisory report)
        {
            if (IsBeginnerLesson(report) && beginnerLessonsRead.Contains(report.Key)) return false;
            AdvisorySeverity reviewed;
            return !adviserAcknowledged.TryGetValue(report.Key, out reviewed) || report.Severity > reviewed;
        }

        private Advisory AdviserToastReport
        {
            get
            {
                if (game.IsOver) return null;
                return adviserReports.FirstOrDefault(a => !a.IsGuidance && AutomaticAdviserAllowed(a)) ?? adviserReports.Where(a => a.IsGuidance && AutomaticAdviserAllowed(a))
                    .OrderBy(a => IsBeginnerLesson(a) ? 1 : 0).ThenByDescending(a => a.GuidancePriority).ThenByDescending(a => a.Turn).FirstOrDefault();
            }
        }
        private bool AdviserToastVisible
        { get { return !BlockingSheet && !NotificationInspectorOpen && (page != 0 || mapMenu == 0) && !dragging && !map.IsNavigating && AdviserToastReport != null; } }
        private RectangleF AdviserToastBounds
        { get { return NotificationCueBounds(132); } }

        private void AcknowledgeAdviser(string key)
        {
            Advisory shown = adviserReports.FirstOrDefault(a => a.Key == key);
            if (shown == null) return;
            AcknowledgeDisplayedAdviser(shown, adviserOccurrences[shown.Key]);
        }

        private void AcknowledgeDisplayedAdviser(Advisory shown, int occurrence)
        {
            // A stale close button cannot dismiss an unseen escalation or a new
            // occurrence after the original concern resolved.
            int current; AdvisorySeverity previous;
            if (!adviserOccurrences.TryGetValue(shown.Key, out current) || occurrence != current) return;
            if (!adviserAcknowledged.TryGetValue(shown.Key, out previous) || previous < shown.Severity)
                adviserAcknowledged[shown.Key] = shown.Severity;
            adviserReviewedTurn[shown.Key] = game.Turn;
            if (shown.IsGuidance) adviserLastRoutineTurn = game.Turn;
            if (IsBeginnerLesson(shown)) beginnerLessonsRead.Add(shown.Key);
            buttons.Clear(); HideMapHover(); Invalidate();
        }

        private void OpenAdvisers(string key)
        {
            if (BlockingSheet) return;
            StopAutoplay("Your advisers are waiting. You are in control.");
            ClearMapTransient(); dragging = false; Capture = false; map.IsNavigating = false; settleCamera.Stop();
            RefreshAdvisers(); adviserOpen = true; adviserFilter = adviserPage = 0;
            Advisory report = adviserReports.FirstOrDefault(a => a.Key == key) ?? adviserReports.FirstOrDefault(AdviserUnread) ?? adviserReports.FirstOrDefault();
            adviserKey = report == null ? null : report.Key; councilMeet = report == null; councilVoice = CouncilDefaultVoice(report);
            if (report != null) adviserPage = adviserReports.IndexOf(report) / 4;
            if (report != null) AcknowledgeAdviser(report.Key);
            buttons.Clear(); Invalidate();
        }

        private void CloseAdvisers()
        { adviserOpen = false; buttons.Clear(); HideMapHover(); Invalidate(); }

        private void SelectAdviser(string key)
        {
            if (!adviserOpen || noticeModal || encounterChoice || endingOpen) return;
            RefreshAdvisers();
            Advisory report = adviserReports.FirstOrDefault(a => a.Key == key);
            adviserKey = report == null ? null : report.Key; councilMeet = report == null; councilVoice = CouncilDefaultVoice(report);
            if (report != null) AcknowledgeAdviser(report.Key);
            buttons.Clear(); Invalidate();
        }

        private Advisory[] FilteredAdvisers()
        { return adviserReports.Where(a => adviserFilter == 0 || (int)a.Role == adviserFilter - 1).ToArray(); }

        private void SetAdviserFilter(int filter)
        {
            if (!CouncilCanChoose || filter < 0 || filter > 3) return;
            adviserFilter = filter; adviserPage = 0;
            Advisory first = FilteredAdvisers().FirstOrDefault();
            SelectAdviser(first == null ? null : first.Key);
        }

        private void InspectAdviser(string key)
        { InspectCouncilOpinion(key, councilVoice); }

        private static string AdviserName(Advisory report)
        { return CouncilPerspectives.Profile(CouncilPerspectives.Lead(report)).Name; }
        private static string AdviserIcon(Advisory report)
        { return report.IsGuidance ? "history" : report.Role == AdviserRole.Economic ? "leaf" : "hunt"; }
        private static string AdviserContext(Advisory report)
        { return IsBeginnerLesson(report) ? "Learning to play / " + report.SubjectName : report.IsGuidance ? "Turn " + report.Turn + " / Guidance" : report.Severity.ToString(); }
        private static Color AdviserInk(Advisory report)
        { return report.Severity >= AdvisorySeverity.Urgent ? Color.FromArgb(230, 152, 124) : Art.Gold; }

        private void DrawAdviserEntry(Graphics g, RectangleF box)
        {
            int count = game.IsOver ? 0 : adviserReports.Count(AdviserUnread);
            string text = "Advisers" + (count > 0 ? " " + count : "");
            bool unread = !game.IsOver && adviserReports.Any(AdviserUnread);
            Button(g, text, box.X, box.Y, box.Width, box.Height, delegate { OpenAdvisers(null); }, unread, false);
            MapTip(count == 0 ? "Read economic, military and remembered guidance in the council. Shortcut: C." :
                count + " items awaiting review. Open the council for the full explanation. Shortcut: C.");
        }

        private void DrawMapAdviserEntries(Graphics g)
        {
            DrawAdviserEntry(g, new RectangleF(1381, 109, 106, 32));
            Button(g, "Events " + UnreadNoticeCount(), 1493, 109, 83, 32,
                delegate { ClearMapTransient(); page = 4; storyChoicesVisible = false; chronicleEvents = true; noticeOffset = 0; Invalidate(); }, false, false);
            MapTip("Read the history archive. The count shows unread events.");
        }

        private void DrawAdviserToast(Graphics g)
        {
            if (!AdviserToastVisible) return;
            Advisory report = AdviserToastReport;
            RectangleF box = AdviserToastBounds;
            Color ink = AdviserInk(report);
            CounsellorId voice = CouncilDefaultVoice(report);
            CounsellorProfile profile = CouncilPerspectives.Profile(voice);
            string key = report.Key;
            int occurrence = adviserOccurrences[key];
            buttons.RemoveAll(b => b.Bounds.IntersectsWith(box));
            Art.Panel(g, box, Color.FromArgb(26, 37, 39), false);
            Art.Line(g, ink, 2, box.X, box.Y + 10, box.X, box.Bottom - 10);
            AdviserPortraits.Draw(g, new RectangleF(box.X + 12, box.Y + 13, 44, 44), voice, ink);
            Typography.Line(g, profile.Name, new RectangleF(box.X + 69, box.Y + 11, box.Width - 116, 27), 19, ink, TypeRole.Heading, true);
            string sentence = AdviserCueSentence(report);
            RectangleF summary = new RectangleF(box.X + 69, box.Y + 42, box.Width - 83, 51);
            Typography.Draw(g, sentence, summary, AdviserReadingSize(g, sentence, summary, 17, 15, TypeRole.Body), Art.Ink, TypeRole.Body);
            Typography.Line(g, report.SubjectName, new RectangleF(box.X + 14, box.Y + 102, box.Width - 135, 22), 15, Art.Muted, TypeRole.Annotation, true);
            buttons.Add(new UiButton(box, delegate { OpenAdvisers(key); }));
            MapTip(profile.Name + ": " + report.Title + "\n" + report.Summary + "\nOpen Details for the full explanation and other advisers' views.");
            Button(g, "Details >", box.Right - 110, box.Y + 100, 96, 25, delegate { OpenAdvisers(key); }, false, false);
            MapTip("Read the full advice, its tradeoffs and the other advisers' views. Opening Details pauses autoplay.");
            Button(g, "\u00d7", box.Right - 36, box.Y + 9, 28, 28, delegate { AcknowledgeDisplayedAdviser(report, occurrence); }, false, false);
            MapTip("Dismiss this advice. It remains available in Advisers.");
        }

        private void DrawAdvisers(Graphics g)
        { DrawCouncil(g); }

        private string AdviserRecommendation(Advisory report)
        {
            if (report.IsGuidance) return report.Counsel;
            Band actor = game.Bands.FirstOrDefault(b => b.Id == report.ActorBandId && game.CanControlBand(b.Id));
            if (actor != null && report.Role == AdviserRole.Economic && game.ActionsFor(actor.Id) <= 0)
                return "This band has spent its actions. Plan its first gathering order next turn; closing now still carries the forecast risk.";
            switch (report.RecommendedAction)
            {
                case AdviserAction.GatherSalt: return "Your band can gather here. Review the source, then spend an action on salt.";
                case AdviserAction.FindSalt: return "Find a known coast or salt spring in the ledger. Travel and gather before reserves fail.";
                case AdviserAction.GatherFood: return "Gather while actions remain. Check the forecast before ending the turn.";
                case AdviserAction.ReviewEconomy: return "Review food output and care. Plan your next gathering place before committing further.";
                case AdviserAction.ReviewWood: return "Keep food and salt supplied first, then collect wood for cooking fires and camps.";
                default: return "Inspect their strength and your band's condition. Choose whether to hold, withdraw or fight.";
            }
        }

        private static float AdviserReadingSize(Graphics g, string text, RectangleF bounds, float nominal, float minimum, TypeRole role)
        {
            using (StringFormat format = new StringFormat { Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.LineLimit })
            {
                for (float size = nominal; size >= minimum; size -= .5f)
                {
                    int fitted, lines;
                    g.MeasureString(text, Typography.Font(role, size), bounds.Size, format, out fitted, out lines);
                    if (fitted == text.Length) return size;
                }
            }
            return minimum;
        }
    }
}
