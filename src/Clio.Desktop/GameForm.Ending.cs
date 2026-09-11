using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using Clio.Simulation;

namespace Clio.Desktop
{
    public sealed partial class GameForm
    {
        private bool endingOpen, endingReported;
        private EndingRecord endingRecord;
        private string endingStatus;

        private sealed class EndingRecord
        {
            internal string Name, Cause, Survivors;
            internal bool Tribe;
            internal int Turn, Peak, Births, Deaths, Places, Practices, Daughters;
        }

        private void ResetEnding()
        { endingOpen = endingReported = false; endingRecord = null; endingStatus = null; }

        private void ReportEndingIfNeeded()
        {
            if (game.Battle != null || !game.IsOver || endingReported) return;
            endingReported = true; endingRecord = CaptureEnding(); ShowEnding();
        }

        private void ShowEnding()
        {
            if (!game.IsOver) return;
            if (endingRecord == null) endingRecord = CaptureEnding();
            endingReported = true;
            adviserOpen = false;
            StopAutoplay("Your people's story has ended. Its history remains.");
            CloseNotice(false, false); encounterChoice = false; ClearMapTransient();
            dragging = false; Capture = false; map.IsNavigating = false;
            page = 0; endingOpen = true; buttons.Clear(); Invalidate();
        }

        private void DismissEnding()
        { endingOpen = false; buttons.Clear(); Invalidate(); }

        private void ReadEndingHistory()
        { DismissEnding(); page = 4; chronicleEvents = true; noticeOffset = 0; Invalidate(); }

        private void SurveyEndingMap()
        { DismissEnding(); page = 0; Invalidate(); }

        private void SaveEndingHistory()
        {
            SaveStory();
            if (status.StartsWith("Story saved.", StringComparison.Ordinal) || status.StartsWith("Could not save:", StringComparison.Ordinal)) endingStatus = status;
            Invalidate();
        }

        private void LoadEndingStory()
        {
            LoadStory();
            if (endingOpen && status.StartsWith("Could not load:", StringComparison.Ordinal)) endingStatus = status;
            Invalidate();
        }

        private EndingRecord CaptureEnding()
        {
            Band[] survivors = game.Bands.Where(b => b.Id != game.Player.Id && b.Population > 0 && game.Explored.Contains(b.CellId)).OrderBy(b => b.Id).ToArray();
            string beyond = survivors.Length == 0 ? "No other living household is recorded in the land your people explored." :
                survivors.Length.ToString("N0") + (survivors.Length == 1 ? " other household remains" : " other households remain") + " in the remembered land: " +
                String.Join(", ", survivors.Take(3).Select(b => b.Name)) + (survivors.Length > 3 ? ", and others." : ".");
            if (game.TribesEnabled && survivors.Length > 0)
            {
                int descendants = survivors.Count(EndingHasOwnLineage);
                beyond = survivors.Length.ToString("N0") + (survivors.Length == 1 ? " independent household remains" : " independent households remain") +
                    " in explored land. " + (descendants == 0 ? "No surviving daughter lineage is currently observed." :
                    descendants.ToString("N0") + (descendants == 1 ? " carries" : " carry") + " a daughter lineage of your people: " +
                    String.Join(", ", survivors.Where(EndingHasOwnLineage).Take(2).Select(b => b.Name)) + (descendants > 2 ? ", and others." : "."));
            }
            return new EndingRecord {
                Name = game.Player.Name, Turn = game.Turn, Cause = EndingCause(), Survivors = beyond,
                Tribe = game.TribesEnabled,
                Peak = journal.Timeline.Count == 0 ? 0 : journal.Timeline.Max(p => p.Population),
                Births = journal.Totals.Births, Deaths = journal.Totals.Deaths,
                Places = game.Explored.Count, Practices = game.Knowledge.Count(k => k.Known), Daughters = journal.Totals.DaughterBandsFounded
            };
        }

        private bool EndingHasOwnLineage(Band band)
        {
            if (!game.TribesEnabled) return false;
            // Parentage of our own offshoots is remembered; unrelated hidden
            // peoples' populations, names and locations never enter this view.
            HashSet<int> visited = new HashSet<int>();
            TribeMembership membership = game.TribeStatus(band.Id);
            int parent = membership == null ? -1 : membership.ParentBandId;
            while (parent >= 0 && visited.Add(parent))
            {
                if (parent == game.Player.Id || game.TribeOf(parent) == game.PlayerTribeId) return true;
                membership = game.TribeStatus(parent);
                parent = membership == null ? -1 : membership.ParentBandId;
            }
            return false;
        }

        private bool EndingOwnBand(int id)
        { return game.TribesEnabled ? game.TribeOf(id) == game.PlayerTribeId : id == game.Player.Id; }

        private string EndingCause()
        {
            // A depleted reserve is not proof of a death. Only the successful
            // final command's recorded casualties can explain this ending.
            if (!game.IsOver || journal.LastCommandPopulationBefore <= journal.LastCommandPopulationAfter || journal.LastCommandPopulationAfter != 0)
                return "The final lives are gone. This record does not identify the immediate cause; the earlier history is preserved.";
            List<string> causes = new List<string>();
            foreach (StoryNotice notice in journal.LastCommandNotices)
            {
                if (notice.Kind == StoryNoticeKind.Hunger || notice.Kind == StoryNoticeKind.Exposure || notice.Kind == StoryNoticeKind.Loss ||
                    notice.Kind == StoryNoticeKind.Salt && notice.Title == "The long salt shortage takes lives")
                    causes.Add(Timeline.DisplayText(game, notice.Body));
                else if (notice.EncounterId >= 0)
                {
                    EncounterRecord record = game.Encounters.Records.FirstOrDefault(r => r.Id == notice.EncounterId && r.VisibleToPlayer);
                    if (record == null || (record.Kind != EncounterKind.Attack && record.Kind != EncounterKind.Befriend)) continue;
                    int lost = (record.ActorKind == UnitKind.Band && EndingOwnBand(record.ActorId) ? record.ActorCasualties : 0) +
                        (record.TargetKind == UnitKind.Band && EndingOwnBand(record.TargetId) ? record.TargetCasualties : 0);
                    if (lost > 0) causes.Add(lost.ToString("N0") + (lost == 1 ? " person is" : " people are") + " lost in " +
                        (record.Kind == EncounterKind.Befriend ? "an attempt to befriend " + record.TargetName : "the encounter between " + record.ActorName + " and " + record.TargetName) + ".");
                }
            }
            return causes.Count == 0 ? "The last people have been lost. No exact cause is available in this part of the record." : String.Join(" ", causes.Distinct());
        }

        private void DrawEnding(Graphics g)
        {
            if (!endingOpen || !game.IsOver || endingRecord == null) return;
            EndingRecord record = endingRecord;
            using (Brush veil = new SolidBrush(Color.FromArgb(237, 5, 10, 13))) g.FillRectangle(veil, 0, 0, 1600, 960);
            buttons.Clear();
            RectangleF pageBounds = new RectangleF(166, 55, 1268, 849);
            if (Art.PaperMode) MapPaper.Surface(g, pageBounds, true);
            else
            {
                using (LinearGradientBrush paper = new LinearGradientBrush(pageBounds, Color.FromArgb(26, 32, 33), Color.FromArgb(13, 20, 23), 90)) g.FillRectangle(paper, pageBounds);
                Art.Grain(g, pageBounds);
            }
            Art.Line(g, Color.FromArgb(102, Art.Gold), 1, 183, 74, 1417, 74);
            Art.Line(g, Color.FromArgb(102, Art.Gold), 1, 183, 885, 1417, 885);
            Art.Line(g, Color.FromArgb(42, Art.Gold), 1, 183, 74, 183, 885);
            Art.Line(g, Color.FromArgb(42, Art.Gold), 1, 1417, 74, 1417, 885);
            Typography.Label(g, "The last page", new RectangleF(277, 103, 1046, 28), 15, Art.Gold, 2.8f, StringAlignment.Center);
            DrawSilentHearth(g, new RectangleF(733, 145, 134, 99));
            Typography.Line(g, "The last hearth falls silent", new RectangleF(243, 245, 1114, 85), 62, Art.Ink, TypeRole.Display, true, StringAlignment.Center);
            Typography.Line(g, record.Name, new RectangleF(277, 328, 1046, 48), 34, Art.Gold, TypeRole.Annotation, true, StringAlignment.Center);
            Typography.Label(g, Timeline.Label(game, record.Turn) + "  /  The " + (record.Tribe ? "tribe's" : "household's") + " record closes", new RectangleF(277, 385, 1046, 26), 12, Art.Muted, .8f, StringAlignment.Center);
            Art.Rule(g, 262, 432, 1076);
            Typography.Label(g, "The final account", new RectangleF(278, 450, 1044, 23), 12, Art.Gold, .7f);
            Typography.Draw(g, record.Cause, new RectangleF(278, 478, 1044, 77), 20, Art.Ink, TypeRole.Body);
            Art.Line(g, Color.FromArgb(54, Art.Gold), 1, 261, 577, 1339, 577);
            DrawEndingMetric(g, 234, "Recorded peak", record.Peak, record.Tribe ? "people across your tribe" : "people at the hearth");
            DrawEndingMetric(g, 425, "Births", record.Births, "recorded new lives");
            DrawEndingMetric(g, 616, "Deaths", record.Deaths, "remembered in the ledger");
            DrawEndingMetric(g, 807, "Explored places", record.Places, "a world given names");
            DrawEndingMetric(g, 998, "Practices learned", record.Practices, "ways of living");
            DrawEndingMetric(g, 1189, "Daughter bands", record.Daughters, "new hearths founded");
            Art.Line(g, Color.FromArgb(54, Art.Gold), 1, 261, 687, 1339, 687);
            Typography.Label(g, "Beyond this hearth", new RectangleF(278, 707, 1044, 23), 12, Art.Gold, .7f);
            Typography.Draw(g, record.Survivors, new RectangleF(278, 736, 1044, 52), 19, Art.Muted, TypeRole.Annotation);
            Button(g, "Save history", 230, 811, 215, 42, SaveEndingHistory, false, false);
            Button(g, "Read history", 457, 811, 215, 42, ReadEndingHistory, false, false);
            Button(g, "Survey map", 684, 811, 215, 42, SurveyEndingMap, false, false);
            Button(g, "Load", 911, 811, 149, 42, LoadEndingStory, false, false);
            Button(g, "Begin a new story", 1072, 811, 297, 42, NewStory, true, false);
            Typography.Line(g, endingStatus ?? "Enter or Escape returns to the map. The final account can be opened again.", new RectangleF(281, 859, 1038, 25), 15, endingStatus == null ? Art.Muted : Art.Gold, TypeRole.Annotation, true, StringAlignment.Center);
        }

        private void DrawEndingMetric(Graphics g, float x, string label, int value, string note)
        {
            Typography.Label(g, label, new RectangleF(x, 594, 178, 23), 11.5f, Art.Muted, .5f, StringAlignment.Center);
            Typography.Line(g, value.ToString("N0"), new RectangleF(x, 615, 178, 44), 36, Art.Ink, TypeRole.Number, true, StringAlignment.Center);
            Typography.Line(g, note, new RectangleF(x - 5, 661, 188, 21), 13.5f, Art.Muted, TypeRole.Annotation, true, StringAlignment.Center);
        }

        private static void DrawSilentHearth(Graphics g, RectangleF bounds)
        {
            GraphicsState state = g.Save(); g.TranslateTransform(bounds.X, bounds.Y); g.ScaleTransform(bounds.Width / 134, bounds.Height / 99);
            using (GraphicsPath halo = new GraphicsPath())
            {
                halo.AddEllipse(15, 46, 104, 51);
                using (PathGradientBrush glow = new PathGradientBrush(halo))
                { glow.CenterColor = Color.FromArgb(62, 182, 110, 56); glow.SurroundColors = new[] { Color.FromArgb(0, 182, 110, 56) }; g.FillPath(glow, halo); }
            }
            using (Pen ash = new Pen(Color.FromArgb(131, Art.Muted), 1.15f))
            using (Pen ember = new Pen(Color.FromArgb(160, 165, 101, 58), 1.9f))
            {
                for (int i = 0; i < 7; i++)
                {
                    double a = i * Math.PI * 2 / 7;
                    float x = 67 + (float)Math.Cos(a) * 43, y = 76 + (float)Math.Sin(a) * 12;
                    g.DrawArc(ash, x - 8, y - 5, 17, 10, 190, 280);
                }
                g.DrawLine(ash, 40, 73, 90, 81); g.DrawLine(ash, 42, 81, 86, 68);
                g.DrawLine(ember, 62, 77, 72, 74); g.DrawLine(ember, 70, 79, 76, 78);
                g.DrawBezier(ash, 66, 66, 82, 47, 47, 48, 64, 27);
                g.DrawBezier(ash, 75, 51, 86, 39, 69, 27, 81, 11);
            }
            g.Restore(state);
        }
    }
}
