using System;
using System.Drawing;
using System.Linq;
using Clio.Simulation;

namespace Clio.Desktop
{
    public sealed partial class GameForm
    {
        // A preference for a voice, never a policy or an acknowledgement. It
        // deliberately survives new/load within this application session.
        private CounsellorId? favoredCounsellor;
        private CounsellorId councilVoice = CounsellorId.Memory;
        private bool councilMeet;
        private bool CouncilCanChoose { get { return adviserOpen && !noticeModal && !encounterChoice && !endingOpen; } }

        private CounsellorId CouncilDefaultVoice(Advisory report)
        {
            if (IsBeginnerLesson(report)) return favoredCounsellor ?? BeginnerLessonLead(report);
            if (report != null && !report.IsGuidance && report.Severity >= AdvisorySeverity.Urgent)
                return CouncilPerspectives.Lead(report);
            return favoredCounsellor ?? CouncilPerspectives.Lead(report);
        }

        private void SelectCouncilVoice(CounsellorId voice)
        {
            if (!CouncilCanChoose || !Enum.IsDefined(typeof(CounsellorId), voice)) return;
            councilVoice = voice; buttons.Clear(); Invalidate();
        }

        private void FavorCouncilVoice()
        {
            if (!CouncilCanChoose) return;
            favoredCounsellor = councilVoice; buttons.Clear(); Invalidate();
        }

        private void MeetCouncil()
        {
            if (!CouncilCanChoose) return;
            councilMeet = true; councilVoice = favoredCounsellor ?? councilVoice; buttons.Clear(); Invalidate();
        }

        private void InspectCouncilOpinion(string key, CounsellorId voice)
        {
            if (!CouncilCanChoose) return;
            Advisory shown = adviserReports.FirstOrDefault(a => a.Key == key);
            int occurrence;
            if (shown == null || !adviserOccurrences.TryGetValue(key, out occurrence)) return;
            InspectDisplayedCouncilOpinion(shown, occurrence, voice);
        }

        private void InspectDisplayedCouncilOpinion(Advisory shown, int occurrence, CounsellorId voice)
        {
            if (!CouncilCanChoose || councilMeet || adviserKey != shown.Key || councilVoice != voice) return;
            RefreshAdvisers();
            int current;
            if (!adviserOccurrences.TryGetValue(shown.Key, out current) || current != occurrence) return;
            Advisory report = adviserReports.FirstOrDefault(a => a.Key == shown.Key);
            CouncilOpinion opinion = AdviserOpinion(report, voice);
            if (!opinion.Available)
            {
                adviserKey = report == null ? null : report.Key;
                status = "That reading no longer fits the current facts. Choose current counsel.";
                buttons.Clear(); Invalidate(); return;
            }
            Band target = game.Bands.FirstOrDefault(b => b.Id == opinion.TargetBandId && b.Population > 0 && game.Explored.Contains(b.CellId));
            if (CouncilNeedsObservedSubject(report, opinion) && target == null)
            {
                status = "That independent household is no longer observed. Its earlier reading remains here.";
                buttons.Clear(); Invalidate(); return;
            }
            if (opinion.Action == AdviserAction.InspectThreat && target == null) return;
            if (!game.CanControlBand(opinion.ActorBandId)) return;
            AcknowledgeDisplayedAdviser(shown, occurrence); CloseAdvisers(); StopAutoplay(null); ClearMapTransient();
            // Foreign records preserve the player's selected household; supply
            // and unit readings deliberately select the living subject household.
            if (opinion.Action != AdviserAction.ReviewDiplomacy && opinion.Action != AdviserAction.ReviewGathering && !(opinion.Action == AdviserAction.ReviewMap && target != null))
                ArmMapCommandBand(opinion.ActorBandId);
            switch (opinion.Action)
            {
                case AdviserAction.GatherSalt: case AdviserAction.FindSalt: OpenSaltEconomy(); break;
                case AdviserAction.ReviewWood: OpenWoodEconomy(); break;
                case AdviserAction.GatherFood: case AdviserAction.ReviewEconomy: OpenEconomy(4); break;
                case AdviserAction.ReviewAnimals: economyResourcePage = 0; OpenEconomy(2); break;
                case AdviserAction.ReviewUnits:
                    page = 3; unitsFilter = 1; unitsSelectedKind = UnitKind.Band; unitsSelectedId = opinion.ActorBandId;
                    SynchronizeUnitsRoster(UnitsRoster()); break;
                case AdviserAction.ReviewCulture: page = 2; culturePage = 0; break;
                case AdviserAction.ReviewDiplomacy:
                    page = 5; ResetDiplomacy(); if (target != null) SelectDiplomacyPolity(game.TribeOf(target.Id)); break;
                case AdviserAction.ReviewGathering:
                    OpenGatheringReading(report); break;
                default:
                    page = 0;
                    if (opinion.CellId >= 0 && opinion.CellId < game.World.Cells.Length && game.Explored.Contains(opinion.CellId))
                    {
                        selected = opinion.CellId; selectedAnimalId = -1;
                        if (target != null) { inspectedBandId = target.Id; inspectorPage = 1; }
                        else { inspectedBandId = opinion.ActorBandId; inspectorPage = 0; }
                        map.Focus(game.World.Cells[selected]); ShowMapSelection();
                    }
                    break;
            }
            status = CouncilPerspectives.Profile(voice).Name + " has offered a reading. The next order is yours.";
            Invalidate();
        }

        private static bool CouncilNeedsObservedSubject(Advisory report, CouncilOpinion opinion)
        { return report.RecommendedAction == AdviserAction.ReviewDiplomacy && (opinion.Action == AdviserAction.ReviewMap || opinion.Action == AdviserAction.ReviewDiplomacy || opinion.Action == AdviserAction.ReviewGathering); }

        private static string CouncilActionLabel(AdviserAction action)
        {
            switch (action)
            {
                case AdviserAction.GatherSalt: case AdviserAction.FindSalt: return "Inspect salt & sources";
                case AdviserAction.ReviewWood: return "Inspect wood & fire";
                case AdviserAction.GatherFood: case AdviserAction.ReviewEconomy: return "Inspect food forecast";
                case AdviserAction.InspectThreat: return "Inspect the hostile band";
                case AdviserAction.ReviewAnimals: return "Read companion lineages";
                case AdviserAction.ReviewUnits: return "Inspect your households";
                case AdviserAction.ReviewCulture: return "Read culture & language";
                case AdviserAction.ReviewDiplomacy: return "Read the relationship";
                case AdviserAction.ReviewGathering: return "Read the gathering";
                default: return "Inspect the known map";
            }
        }

        private void ChangeCouncilPage(int direction)
        {
            if (!CouncilCanChoose) return;
            adviserPage = Math.Max(0, Math.Min(Math.Max(0, (FilteredAdvisers().Length - 1) / 4), adviserPage + direction));
            buttons.Clear(); Invalidate();
        }

        private void DrawCouncil(Graphics g)
        {
            if (!adviserOpen) return;
            using (Brush shade = new SolidBrush(Art.PaperMode ? Color.FromArgb(110, MapPaper.MutedInk) : Color.FromArgb(205, 6, 13, 17))) g.FillRectangle(shade, 0, 0, 1600, 960);
            buttons.Clear();
            Art.Panel(g, new RectangleF(132, 94, 1336, 780), Color.FromArgb(25, 36, 39), true);
            Art.Icon(g, "history", 164, 117, 33, Art.Gold);
            Typography.Line(g, "Your advisers", new RectangleF(216, 108, 840, 51), 40, Art.Ink, TypeRole.Display);
            Typography.Line(g, "Compare advice from four specialists. You choose which priorities to follow.", new RectangleF(217, 158, 1080, 32), 20, Art.Muted, TypeRole.Annotation);
            Button(g, "\u00d7", 1413, 110, 34, 34, CloseAdvisers, false, false);
            Art.Rule(g, 162, 205, 1276);
            DrawCouncilReadings(g);
            for (int i = 0; i < CouncilPerspectives.All.Count; i++) DrawCouncilVoice(g, CouncilPerspectives.All[i], i);
            Art.Line(g, Border, 1, 439, 224, 439, 791);
            Advisory report = councilMeet ? null : FilteredAdvisers().FirstOrDefault(a => a.Key == adviserKey);
            if (report == null) DrawCouncilProfiles(g);
            else DrawCouncilArgument(g, report);
            Art.Rule(g, 162, 807, 1276);
            Typography.Line(g, "Adviser frequency", new RectangleF(164, 822, 237, 28), 21, Art.Gold, TypeRole.Heading);
            DrawAdviserFrequencyChoices(g, 425, 821, 490);
            Typography.Line(g, "Remembered between games", new RectangleF(930, 823, 299, 28), 17, Art.Muted, TypeRole.Annotation);
            Button(g, "Close  /  Esc", 1242, 821, 194, 35, CloseAdvisers, false, false);
        }

        private void DrawCouncilReadings(Graphics g)
        {
            Typography.Label(g, "Readings", new RectangleF(163, 221, 258, 23), 13, Art.Gold, .8f);
            string[] filters = { "All counsel", "Economic", "Military", "Guidance" };
            for (int i = 0; i < filters.Length; i++)
            {
                int choice = i;
                Button(g, filters[i], 162 + i % 2 * 136, 251 + i / 2 * 36, 128, 29, delegate { SetAdviserFilter(choice); }, adviserFilter == i, false);
            }
            Advisory[] filtered = FilteredAdvisers();
            adviserPage = Math.Max(0, Math.Min(adviserPage, Math.Max(0, (filtered.Length - 1) / 4)));
            for (int i = 0; i < Math.Min(4, filtered.Length - adviserPage * 4); i++)
            {
                Advisory report = filtered[adviserPage * 4 + i];
                RectangleF row = new RectangleF(162, 331 + i * 89, 264, 80);
                if (Art.PaperMode) MapPaper.Surface(g, row, false, !councilMeet && report.Key == adviserKey);
                else Art.Panel(g, row, !councilMeet && report.Key == adviserKey ? Color.FromArgb(43, 51, 46) : Color.FromArgb(21, 31, 35), false);
                Typography.Label(g, AdviserContext(report) + (AdviserUnread(report) ? " *" : ""), new RectangleF(row.X + 12, row.Y + 7, row.Width - 24, 19), 11, AdviserInk(report), .3f);
                Typography.Draw(g, report.Title, new RectangleF(row.X + 12, row.Y + 28, row.Width - 24, 48), 18, Art.Ink, TypeRole.Heading);
                string key = report.Key; buttons.Add(new UiButton(row, delegate { SelectAdviser(key); }));
            }
            if (filtered.Length == 0)
                Typography.Draw(g, "No readings in this section. Meet the four voices, or choose another section.", new RectangleF(174, 351, 238, 124), 20, Art.Muted, TypeRole.Annotation);
            if (filtered.Length > 4)
            {
                Button(g, "Previous", 162, 701, 92, 31, delegate { ChangeCouncilPage(-1); }, false, false);
                Typography.Line(g, (adviserPage + 1) + " / " + ((filtered.Length + 3) / 4), new RectangleF(255, 701, 77, 31), 17, Art.Muted, TypeRole.Number, false, StringAlignment.Center);
                Button(g, "Next", 334, 701, 92, 31, delegate { ChangeCouncilPage(1); }, false, false);
            }
            Typography.Line(g, adviserReports.Count(a => !a.IsGuidance) + " current / " + adviserReports.Count(AdviserUnread) + " unread", new RectangleF(164, 733, 260, 24), 16, Art.Muted, TypeRole.Annotation);
            Button(g, "Meet the council", 162, 760, 264, 33, MeetCouncil, councilMeet, false);
        }

        private void DrawCouncilVoice(Graphics g, CounsellorProfile profile, int index)
        {
            RectangleF box = new RectangleF(455 + index * 248, 219, 237, 102);
            bool chosen = councilVoice == profile.Id;
            if (Art.PaperMode) MapPaper.Surface(g, box, false, chosen);
            else Art.Panel(g, box, chosen ? Color.FromArgb(44, 52, 46) : Color.FromArgb(21, 31, 34), false);
            AdviserPortraits.Draw(g, new RectangleF(box.X + 8, box.Y + 18, 64, 64), profile.Id, chosen ? Art.Gold : Art.Muted);
            Typography.Draw(g, profile.Name, new RectangleF(box.X + 83, box.Y + 8, 143, 48), 21, chosen ? Art.Gold : Art.Ink, TypeRole.Heading);
            Typography.Draw(g, profile.Title, new RectangleF(box.X + 84, box.Y + 60, 142, 34), 14, Art.Muted, TypeRole.Annotation);
            CounsellorId voice = profile.Id;
            buttons.Add(new UiButton(box, delegate { SelectCouncilVoice(voice); }));
            MapTip("Read the " + profile.Name.ToLowerInvariant() + "'s advice. This spends no action.");
        }

        private void DrawCouncilArgument(Graphics g, Advisory report)
        {
            CounsellorProfile profile = CouncilPerspectives.Profile(councilVoice);
            CouncilOpinion opinion = AdviserOpinion(report, councilVoice);
            Color ink = AdviserInk(report);
            Typography.Label(g, (IsBeginnerLesson(report) ? "How to play" : report.IsGuidance ? "Recorded development" : "Observed facts") + " / " + AdviserContext(report), new RectangleF(459, 339, 972, 23), 12, ink, .6f);
            CouncilReading(g, report.Title, new RectangleF(455, 365, 979, 56), 27, 20, Art.Ink, TypeRole.Heading);
            CouncilReading(g, report.Explanation, new RectangleF(458, 427, 973, 68), 18, 16, Art.Muted, TypeRole.Body);
            Art.Line(g, Border, 1, 458, 503, 1431, 503);
            Typography.Line(g, profile.Name + " / " + profile.Motive, new RectangleF(458, 509, 973, 29), 18, Art.Gold, TypeRole.Annotation, true);
            CouncilReading(g, "\u201c" + opinion.Speech + "\u201d", new RectangleF(458, 546, 970, 56), 24, 19, Art.Ink, TypeRole.Annotation);
            DrawCouncilColumn(g, "Recommendation", opinion.Argument, 458, 375);
            DrawCouncilColumn(g, "Tradeoff", opinion.Tradeoff, 854, 259);
            DrawCouncilColumn(g, "Another adviser", opinion.Rebuttal, 1134, 299);
            DrawCouncilFavor(g, 458, 757);
            if (opinion.Available && !(CouncilNeedsObservedSubject(report, opinion) && opinion.TargetBandId < 0))
            {
                int occurrence = adviserOccurrences[report.Key]; CounsellorId voice = councilVoice;
                Button(g, CouncilActionLabel(opinion.Action), 671, 757, 318, 35, delegate { InspectDisplayedCouncilOpinion(report, occurrence, voice); }, true, false);
                MapTip("Open the evidence for this reading. Inspection issues no order and spends no action.");
            }
            else Typography.Line(g, opinion.Available ? "That household is no longer observed." : "This reading is no longer current.", new RectangleF(680, 757, 324, 35), 17, Art.Muted, TypeRole.Annotation, true);
            Typography.Draw(g, "Hear a favored voice first.\nUrgent alerts keep their specialist.", new RectangleF(1010, 750, 422, 54), 16, Art.Muted, TypeRole.Annotation);
        }

        private void DrawCouncilColumn(Graphics g, string title, string body, float x, float width)
        {
            Typography.Label(g, title, new RectangleF(x, 612, width, 24), 12, Art.Gold, .5f);
            CouncilReading(g, body, new RectangleF(x, 642, width, 106), 18, 16, Art.Muted, TypeRole.Body);
        }

        private void DrawCouncilFavor(Graphics g, float x, float y)
        {
            string name = CouncilPerspectives.Profile(councilVoice).Name;
            Button(g, favoredCounsellor == councilVoice ? "Preferred adviser" : "Prefer this adviser", x, y, 192, 35, FavorCouncilVoice, favoredCounsellor == councilVoice, false);
            MapTip("Hear the " + name.ToLowerInvariant() + " first in ordinary advice during this session. Urgent warnings retain their specialist. This does not change orders or autoplay.");
        }

        private void DrawCouncilProfiles(Graphics g)
        {
            CounsellorProfile profile = CouncilPerspectives.Profile(councilVoice);
            Typography.Label(g, "Adviser role", new RectangleF(459, 346, 574, 23), 12, Art.Gold, .7f);
            Typography.Line(g, profile.Name, new RectangleF(455, 389, 571, 63), 42, Art.Ink, TypeRole.Display, true);
            Typography.Line(g, profile.Title, new RectangleF(458, 458, 570, 32), 25, Art.Gold, TypeRole.Annotation);
            CouncilReading(g, "\u201c" + profile.Motive + "\u201d", new RectangleF(458, 512, 573, 84), 28, 23, Art.Ink, TypeRole.Annotation);
            Typography.Draw(g, "Each voice argues from a different priority. Choose a reading to hear their arguments and their objections to one another. The recorded facts stay the same.", new RectangleF(459, 616, 574, 113), 21, Art.Muted, TypeRole.Body);
            AdviserPortraits.Draw(g, new RectangleF(1100, 387, 286, 286), councilVoice, Art.Gold);
            DrawCouncilFavor(g, 458, 757);
            Typography.Line(g, "A favored voice speaks first; it never gives orders.", new RectangleF(671, 757, 755, 35), 18, Art.Muted, TypeRole.Annotation, true);
        }

        private static void CouncilReading(Graphics g, string text, RectangleF bounds, float nominal, float minimum, Color color, TypeRole role)
        { Typography.Draw(g, text, bounds, AdviserReadingSize(g, text, bounds, nominal, minimum, role), color, role); }
    }
}
