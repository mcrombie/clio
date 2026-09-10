using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using Clio.Simulation;

namespace Clio.Desktop
{
    public sealed partial class GameForm
    {
        private StoryNotice activeNotice;
        private bool noticeModal, pauseOnEvents, resumeAfterNotice, suppressNotices;
        private readonly Queue<StoryNotice> pendingNotices = new Queue<StoryNotice>();
        private readonly HashSet<int> readNotices = new HashSet<int>();
        private readonly Timer noticeTimer = new Timer { Interval = 250 };
        private DateTime noticeUntil;

        private int UnreadNoticeCount() { return journal.Notices.Count(n => !readNotices.Contains(n.Id)); }

        private void ShowIntroduction()
        {
            if (journal.Notices.Count > 0) OpenNotice(journal.Notices[0], false);
        }

        private void ClearNotices(bool markRead)
        {
            activeNotice = null; noticeModal = false; resumeAfterNotice = false;
            noticeTimer.Stop(); pendingNotices.Clear(); readNotices.Clear(); buttons.Clear(); noticeOffset = 0;
            if (markRead) foreach (StoryNotice notice in journal.Notices) readNotices.Add(notice.Id);
        }

        private void PresentNotices(List<StoryNotice> notices, bool fromAutoplay)
        {
            if (noticeOffset > 0) noticeOffset += notices.Count;
            if (suppressNotices || notices.Count == 0) return;
            // Adviser side cards explain these developments. Their full history
            // entries remain available without a second automatic modal.
            notices = notices.Where(n => !IsAdviserGuidanceNotice(n)).ToList();
            if (notices.Count == 0) return;
            // Observers can keep the story flowing. Every event remains in the archive.
            StoryNotice[] major = notices.Where(n => n.Important).ToArray();
            // Large readings are opt-in. Manual play gets the same compact cue;
            // an explicit pause-on-events setting still opens major accounts.
            if (major.Length > 0 && fromAutoplay && pauseOnEvents)
            {
                bool resume = fromAutoplay && autoplay;
                foreach (StoryNotice notice in major) pendingNotices.Enqueue(notice);
                if (!noticeModal) OpenNotice(pendingNotices.Dequeue(), resume);
            }
            else if (!noticeModal)
            {
                // Leave each toast readable even at high autoplay speed.
                if (activeNotice != null && DateTime.UtcNow < noticeUntil && major.Length == 0) return;
                activeNotice = major.Length > 0 ? major[0] : notices[0];
                noticeUntil = DateTime.UtcNow.AddSeconds(10); noticeTimer.Start();
            }
        }

        private void OpenNotice(StoryNotice notice, bool resume)
        {
            ClearMapTransient();
            StopAutoplay(null); noticeTimer.Stop(); activeNotice = notice; noticeModal = true;
            buttons.Clear();
            resumeAfterNotice = resume; readNotices.Add(notice.Id);
            dragging = false; Capture = false; map.IsNavigating = false; Invalidate();
        }

        private void DismissNotice(bool resume)
        { CloseNotice(resume, resume); }

        private void CloseNotice(bool resume, bool advance)
        {
            bool shouldResume = resume && resumeAfterNotice;
            if (activeNotice != null) readNotices.Add(activeNotice.Id);
            activeNotice = null; noticeModal = false; noticeTimer.Stop(); resumeAfterNotice = false;
            buttons.Clear();
            if (advance && pendingNotices.Count > 0) { OpenNotice(pendingNotices.Dequeue(), shouldResume); return; }
            pendingNotices.Clear();
            if (shouldResume) ToggleAutoplay();
            Invalidate();
        }

        private void InspectNotice()
        {
            StoryNotice notice = activeNotice;
            DismissNotice(false);
            if (notice == null) return;
            if (notice.Kind == StoryNoticeKind.Gathering)
            {
                GatheringEvent entry = game.GatheringEvents.FirstOrDefault(e => e.VisibleToPlayer && e.Turn == notice.Turn &&
                    e.HostBandId == notice.ActorBandId && e.GuestBandId == notice.TargetBandId && e.Title == notice.Title);
                if (entry != null && entry.GatheringId >= 0) OpenGatheringRecord(entry.GatheringId);
                else { ClearMapTransient(); page = 5; ResetDiplomacy(); }
                Invalidate(); return;
            }
            if (notice.Kind == StoryNoticeKind.Travel) { OpenLandscapeGuide(); Invalidate(); return; }
            if (notice.Kind == StoryNoticeKind.Salt) { if (game.CanControlBand(notice.ActorBandId)) ArmMapCommandBand(notice.ActorBandId); OpenSaltEconomy(); Invalidate(); return; }
            if (notice.Kind == StoryNoticeKind.Wood) { if (game.CanControlBand(notice.ActorBandId)) ArmMapCommandBand(notice.ActorBandId); OpenWoodEconomy(); Invalidate(); return; }
            if (notice.Kind == StoryNoticeKind.Livestock) { if (game.CanControlBand(notice.ActorBandId)) ArmMapCommandBand(notice.ActorBandId); OpenEconomy(2); Invalidate(); return; }
            if (notice.AnimalId >= 0)
            {
                Beast animal = game.Beasts.FirstOrDefault(b => b.Id == notice.AnimalId && b.Count > 0 && UnitVisible(b.CellId));
                if (animal != null) { page = 0; SelectAnimal(animal); map.Focus(game.World.Cells[selected]); ShowMapSelection(); }
                else OpenEconomy(2);
                Invalidate(); return;
            }
            if (notice.ActorBandId >= 0 || notice.TargetBandId >= 0)
            {
                int id = notice.TargetBandId >= 0 && notice.TargetBandId != game.Player.Id ? notice.TargetBandId : notice.ActorBandId;
                Band band = game.Bands.FirstOrDefault(b => b.Id == id && b.Population > 0 && UnitVisible(b.CellId));
                if (band != null) { page = 0; selectedAnimalId = -1; selected = band.CellId; inspectedBandId = band.Id; inspectorPage = 1; map.Focus(game.World.Cells[selected]); ShowMapSelection(); }
                else OpenEconomy(1);
                Invalidate(); return;
            }
            if (notice.Kind == StoryNoticeKind.Knowledge) { page = 2; culturePage = 0; }
            else if (notice.Kind == StoryNoticeKind.Hunger) OpenEconomy(4);
            else if (notice.Kind == StoryNoticeKind.Growth || notice.Kind == StoryNoticeKind.Loss) OpenEconomy(1);
            else
            {
                page = 0;
                if (notice.CellId >= 0 && notice.CellId < game.World.Cells.Length && game.Explored.Contains(notice.CellId))
                { selected = notice.CellId; map.Focus(game.World.Cells[selected]); }
                selectedAnimalId = -1;
                inspectorPage = notice.Kind == StoryNoticeKind.PlaceNames ? 3 : 0;
                ShowMapSelection();
            }
            Invalidate();
        }

        private void DrawNotice(Graphics g)
        {
            if (activeNotice == null || !noticeModal && !RoutineNoticeVisible) return;
            StoryNotice notice = activeNotice;
            if (!noticeModal)
            {
                DrawRoutineNoticeCue(g, notice);
                return;
            }

            using (Brush shade = new SolidBrush(Color.FromArgb(185, 6, 13, 17))) g.FillRectangle(shade, 0, 0, 1600, 960);
            buttons.Clear();
            RectangleF sheet = new RectangleF(388, 181, 824, 639);
            Art.Panel(g, sheet, Color.FromArgb(27, 38, 40), true);
            Art.Rule(g, 430, 242, 740);
            if (notice.AnimalKind.HasValue) IdentityArt.DrawAnimal(g, notice.AnimalKind.Value, new RectangleF(765, 256, 70, 47), Art.Gold, notice.Kind == StoryNoticeKind.Domestication);
            else Art.Icon(g, "history", 783, 257, 36, Art.Gold);
            Typography.Label(g, "From the living history", new RectangleF(447, 205, 706, 25), 13, Art.Gold, 1.2f, StringAlignment.Center);
            Typography.Line(g, Timeline.DisplayText(game, notice.Title), new RectangleF(432, 310, 736, 56), 42, Art.Ink, TypeRole.Display, true, StringAlignment.Center);
            Typography.Label(g, Timeline.Label(game, notice.Turn), new RectangleF(451, 368, 698, 24), 12, Art.Gold, .8f, StringAlignment.Center);
            Typography.Draw(g, Timeline.DisplayText(game, notice.Body), new RectangleF(451, 408, 698, 75), 21, Art.Ink, TypeRole.Body);
            Typography.Label(g, "What changed", new RectangleF(451, 490, 695, 24), 12, Art.Gold);
            Typography.Draw(g, Timeline.DisplayText(game, notice.Impact), new RectangleF(451, 518, 695, 97), 18, Art.Ink, TypeRole.Body);
            Typography.Label(g, "The next page", new RectangleF(451, 626, 695, 23), 12, Art.Gold);
            Typography.Draw(g, Timeline.DisplayText(game, notice.Advice), new RectangleF(451, 654, 695, 65), 17, Art.Muted, TypeRole.Annotation);
            Art.Rule(g, 430, 735, 740);
            Button(g, "Inspect", 451, 754, 137, 39, InspectNotice, false, false);
            if (resumeAfterNotice)
            {
                Button(g, "Take control", 755, 754, 154, 39, delegate { DismissNotice(false); }, false, false);
                Button(g, "Continue autoplay", 922, 754, 226, 39, delegate { DismissNotice(true); }, true, false);
            }
            else Button(g, pendingNotices.Count > 0 ? "Next event" : "Continue  ·  Enter", 920, 754, 228, 39, delegate { CloseNotice(false, true); }, true, false);
        }

        private void DrawNoticeArchive(Graphics g)
        {
            noticeOffset = Math.Max(0, Math.Min(Math.Max(0, journal.Notices.Count - 1), noticeOffset));
            StoryNotice[] notices = journal.Notices.Take(journal.Notices.Count - noticeOffset).Reverse().Take(5).ToArray();
            float y = 333;
            foreach (StoryNotice entry in notices)
            {
                StoryNotice notice = entry;
                Art.Panel(g, new RectangleF(44, y, 1491, 73), Panel, false);
                Typography.Label(g, Timeline.Label(game, notice.Turn), new RectangleF(62, y + 12, 153, 23), 12, Art.Gold);
                Typography.Line(g, notice.Kind.ToString(), new RectangleF(62, y + 37, 150, 23), 15, Art.Muted, TypeRole.Annotation);
                Typography.Line(g, Timeline.DisplayText(game, notice.Title), new RectangleF(237, y + 5, 1090, 32), 24, readNotices.Contains(notice.Id) ? Art.Ink : Art.Gold, TypeRole.Heading, true);
                Typography.Line(g, Timeline.DisplayText(game, notice.Impact), new RectangleF(238, y + 37, 1090, 29), 16, Art.Muted, TypeRole.Body, true);
                Button(g, "Read", 1399, y + 20, 110, 33, delegate { OpenNotice(notice, autoplay); }, false, false);
                y += 81;
            }
            Typography.Line(g, journal.Notices.Count + " events remembered  /  " + UnreadNoticeCount() + " unread", new RectangleF(46, 750, 760, 29), 16, Art.Muted, TypeRole.Annotation);
            Button(g, "Earlier", 1333, 750, 93, 30, delegate { noticeOffset = Math.Min(Math.Max(0, journal.Notices.Count - 1), noticeOffset + 5); Invalidate(); }, false, false);
            Button(g, "Recent", 1437, 750, 93, 30, delegate { noticeOffset = Math.Max(0, noticeOffset - 5); Invalidate(); }, false, false);
        }
    }
}
