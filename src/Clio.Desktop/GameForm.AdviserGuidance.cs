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
        private readonly List<Advisory> adviserGuidance = new List<Advisory>();
        private readonly HashSet<string> adviserGuidanceSeen = new HashSet<string>();
        private int guidanceNoticeCursor, guidanceTribeCursor, guidanceGatheringCursor;
        private bool adviserGuidanceEnabled = true, initialGuidanceOffered;

        private void ResetAdviserGuidance()
        {
            adviserGuidance.Clear(); adviserGuidanceSeen.Clear(); initialGuidanceOffered = false;
            guidanceNoticeCursor = journal == null ? 0 : journal.Notices.Count;
            guidanceTribeCursor = game.TribeEvents.Count;
            guidanceGatheringCursor = game.GatheringEvents.Count;
        }

        // Called on native Shown and on a newly created story, never by replay.
        private void ShowInitialGuidance()
        {
            if (initialGuidanceOffered || game.IsOver) return;
            initialGuidanceOffered = true;
            AddAdviserGuidance(new Advisory("guidance:orientation", game.Turn, game.Player.Id, -1, game.Player.CellId,
                "Getting started", "Select a band, gather essential supplies, and explore. Each band has its own supplies and actions.",
                "Each household keeps its own supplies and two actions per turn. Select its banner or use Next band, then gather or move. Food and salt are consumed when you end the turn. The glowing places are opportunities, not free supplies.",
                "Begin with food, then locate a known salt source. Tell me more opens this council without spending an action.", AdviserAction.ReviewMap));
            RefreshAdvisers(); ShowOpeningAnnouncement(); buttons.Clear(); Invalidate();
        }

        private void AddAdviserGuidance(Advisory report)
        {
            if (!adviserGuidanceSeen.Add(report.Key)) return;
            adviserGuidance.Add(report);
            // Keep a short reading shelf; event cursors prevent retired entries
            // from returning as new advice on every refresh.
            if (adviserGuidance.Count > 32) adviserGuidance.RemoveAt(0);
        }

        private void RefreshAdviserGuidance()
        {
            TribeEvent[] events = game.TribeEvents.ToArray();
            if (guidanceTribeCursor > events.Length) guidanceTribeCursor = events.Length;
            foreach (TribeEvent entry in events.Skip(guidanceTribeCursor))
            {
                if (!entry.VisibleToPlayer || entry.TribeId != game.PlayerTribeId && entry.PreviousTribeId != game.PlayerTribeId) continue;
                Advisory report = TribeGuidance(entry);
                if (report != null) AddAdviserGuidance(report);
            }
            guidanceTribeCursor = events.Length;
            RefreshGatheringGuidance();
            if (journal == null) return;
            if (guidanceNoticeCursor > journal.Notices.Count) guidanceNoticeCursor = journal.Notices.Count;
            foreach (StoryNotice notice in journal.Notices.Skip(guidanceNoticeCursor))
            {
                if (notice.Kind != StoryNoticeKind.Domestication && notice.Kind != StoryNoticeKind.Knowledge && notice.Kind != StoryNoticeKind.Fission && notice.Kind != StoryNoticeKind.Wood && notice.Kind != StoryNoticeKind.Livestock) continue;
                if (notice.Kind == StoryNoticeKind.Wood && notice.Title == "Wood is running low") continue;
                if (notice.Kind == StoryNoticeKind.Livestock && notice.Title == "Livestock provides meat" && !notice.Important) continue;
                if (notice.Kind == StoryNoticeKind.Knowledge && !notice.Important) continue;
                string explanation = Timeline.DisplayText(game, notice.Body + " " + notice.Impact);
                if (notice.Kind == StoryNoticeKind.Domestication && !game.LivestockEnabled)
                    explanation = Timeline.DisplayText(game, notice.Body) + " The Resources ledger shows this household's actual companion benefits and ongoing care costs.";
                string summary = notice.Kind == StoryNoticeKind.Knowledge || notice.Kind == StoryNoticeKind.Domestication && game.LivestockEnabled ? Timeline.DisplayText(game, notice.Impact) : notice.Kind == StoryNoticeKind.Domestication ?
                    "Your people have established a companion lineage. Its benefits and ongoing care now belong in the household's resource accounts." : Timeline.DisplayText(game, notice.Body);
                AdviserAction action = notice.Kind == StoryNoticeKind.Wood ? AdviserAction.ReviewWood : notice.Kind == StoryNoticeKind.Knowledge ? AdviserAction.ReviewCulture : notice.Kind == StoryNoticeKind.Domestication || notice.Kind == StoryNoticeKind.Livestock ? AdviserAction.ReviewAnimals : AdviserAction.ReviewUnits;
                AddAdviserGuidance(new Advisory("guidance:notice:" + notice.Id, notice.Turn, notice.ActorBandId, notice.TargetBandId, notice.CellId,
                    notice.Title, summary, explanation, Timeline.DisplayText(game, notice.Advice), action, 2));
            }
            guidanceNoticeCursor = journal.Notices.Count;
            adviserGuidance.RemoveAll(a => a.RequiresControl && !game.CanControlBand(a.ActorBandId));
        }

        private Advisory TribeGuidance(TribeEvent entry)
        {
            string title, summary, explanation, counsel; AdviserAction action = AdviserAction.ReviewUnits;
            if (entry.Kind == TribeEventKind.Split)
            {
                title = "A new band has formed";
                summary = "A new household has formed within your tribe. Its people and supplies remain yours to command; it receives its first actions next turn.";
                explanation = entry.Detail;
                counsel = "Use Next band or Units to select it. Reunite on your leader's hex; the route preview helps you plan the journey.";
            }
            else if (entry.Kind == TribeEventKind.Drift)
            {
                title = "A band is losing contact";
                summary = "This household is beginning to drift from the tribe. It remains yours to command while reunion is still possible.";
                explanation = entry.Detail;
                counsel = "Check this band's reunion countdown in Units. Its outlook and distance can change the time remaining.";
            }
            else if (entry.Kind == TribeEventKind.Secession)
            {
                title = "A band has become independent";
                summary = "This household has become independent. Its people are alive, with their own supplies and a language descended from yours.";
                explanation = entry.Detail + " Diplomacy records the observed relationship.";
                counsel = game.GatheringsEnabled ? "A known peaceful splinter can be invited to a gathering. Read the cost and meeting place in Diplomacy before sending anything." :
                    "Open Diplomacy to read the relationship. Only currently known bands can be located on the map.";
                action = AdviserAction.ReviewDiplomacy;
            }
            else if (entry.Kind == TribeEventKind.Disposition)
            {
                title = "Bands have different personalities";
                summary = "Your households have their own dispositions. Distance and an idle turn can give those differences room to matter.";
                explanation = entry.Detail;
                counsel = "Read each household in Units. Give it a successful order this turn to keep its spare actions from becoming a voluntary departure.";
            }
            else if (entry.Kind == TribeEventKind.Wandering)
            {
                // One explanation per household, even when it later wanders again.
                string key = "guidance:wandering:" + entry.BandId;
                if (adviserGuidanceSeen.Contains(key)) return null;
                return new Advisory(key, entry.Turn, entry.BandId, entry.OtherBandId, entry.CellId,
                    "A household chooses its own path", "This household used an idle turn to move of its own accord. It still belongs to your tribe.",
                    entry.Detail + " A successful order, including Hold, prevents another voluntary journey this turn.",
                    "The leader never wanders voluntarily. Select this household to check its supplies and reunion countdown.", AdviserAction.ReviewUnits, 1, true, entry.BandName);
            }
            else return null;
            return new Advisory("guidance:tribe:" + entry.Id, entry.Turn, entry.BandId, entry.OtherBandId, entry.CellId,
                title, summary, Timeline.DisplayText(game, explanation), counsel, action,
                entry.Kind == TribeEventKind.Secession ? 4 : entry.Kind == TribeEventKind.Drift ? 3 : 2,
                entry.Kind == TribeEventKind.Split || entry.Kind == TribeEventKind.Drift, entry.BandName);
        }

        // Automatic presentation only. The underlying event and its manual Read
        // action remain in the historical archive.
        private bool IsAdviserGuidanceNotice(StoryNotice notice)
        {
            if (notice == null) return false;
            if (notice.Kind == StoryNoticeKind.Wood || notice.Kind == StoryNoticeKind.Livestock) return true;
            if (notice.Kind == StoryNoticeKind.Gathering)
                return game.GatheringEvents.Any(e => e.GatheringId >= 0 && e.VisibleToPlayer && e.Turn == notice.Turn && e.Title == notice.Title);
            // StoryNoticeKind.Salt also carries actual deaths and recoveries.
            // Only these three established concern notices may be replaced by
            // a current economic warning; unfamiliar/loss reports stay visible.
            if (notice.Kind == StoryNoticeKind.Salt && (notice.Title == "The salt reserve is running low" ||
                notice.Title == "The salt reserve runs dry" || notice.Title == "The salt shortage deepens"))
            {
                int actor = notice.ActorBandId >= 0 ? notice.ActorBandId : game.Player.Id;
                return AdviserReport.Evaluate(game).Any(a => a.ActorBandId == actor &&
                    (a.RecommendedAction == AdviserAction.GatherSalt || a.RecommendedAction == AdviserAction.FindSalt));
            }
            if (notice.Kind == StoryNoticeKind.Domestication || notice.Kind == StoryNoticeKind.Fission || notice.Kind == StoryNoticeKind.Knowledge && notice.Important) return true;
            if (notice.Kind != StoryNoticeKind.Tribe) return false;
            return game.TribeEvents.Any(e => e.VisibleToPlayer && e.Turn == notice.Turn && e.BandId == notice.ActorBandId &&
                (e.Kind == TribeEventKind.Split || e.Kind == TribeEventKind.Drift || e.Kind == TribeEventKind.Secession || e.Kind == TribeEventKind.Disposition || e.Kind == TribeEventKind.Wandering) && e.Title == notice.Title);
        }

        private void ToggleAdviserGuidance()
        {
            if (noticeModal || encounterChoice || endingOpen) return;
            SetAdviserFrequency(adviserFrequency == AdviserFrequency.None ? AdviserFrequency.High : AdviserFrequency.None);
        }

        private void InspectAdviserGuidance(Advisory report)
        {
            AcknowledgeAdviser(report.Key); CloseAdvisers(); StopAutoplay(null); ClearMapTransient();
            if (report.RecommendedAction != AdviserAction.ReviewDiplomacy && game.CanControlBand(report.ActorBandId)) ArmMapCommandBand(report.ActorBandId);
            if (report.RecommendedAction == AdviserAction.ReviewCulture) { page = 2; culturePage = 0; }
            else if (report.RecommendedAction == AdviserAction.ReviewUnits) page = 3;
            else if (report.RecommendedAction == AdviserAction.ReviewDiplomacy)
            {
                page = 5;
                Band subject = game.Bands.FirstOrDefault(b => b.Id == report.ActorBandId && b.Population > 0 && game.Explored.Contains(b.CellId));
                if (subject != null) SelectDiplomacyPolity(game.TribeOf(subject.Id));
            }
            else if (report.RecommendedAction == AdviserAction.ReviewAnimals) OpenEconomy(2);
            else if (report.RecommendedAction == AdviserAction.ReviewWood) OpenWoodEconomy();
            else page = 0;
            Invalidate();
        }

        private static string AdviserActionLabel(Advisory report)
        {
            switch (report.RecommendedAction)
            {
                case AdviserAction.ReviewMap: return "Return to the map";
                case AdviserAction.ReviewUnits: return "Review your households";
                case AdviserAction.ReviewDiplomacy: return "Read the relationship";
                case AdviserAction.ReviewGathering: return "Read the gathering";
                case AdviserAction.ReviewAnimals: return "Review companion lineages";
                case AdviserAction.ReviewCulture: return "Read the discovery";
                case AdviserAction.InspectThreat: return "Inspect hostile band";
                case AdviserAction.FindSalt: case AdviserAction.GatherSalt: return "Review salt & sources";
                case AdviserAction.ReviewWood: return "Review wood & fire";
                default: return "Review food forecast";
            }
        }

        // An original engraved scholar medallion: deliberately native vector
        // shapes, with no bitmap, franchise portrait or simulation randomness.
        private static void DrawAdviserPortrait(Graphics g, RectangleF box, Color ink)
        {
            GraphicsState state = g.Save(); g.TranslateTransform(box.X, box.Y); g.ScaleTransform(box.Width / 100, box.Height / 100);
            using (Brush dark = new SolidBrush(Color.FromArgb(17, 28, 32))) g.FillEllipse(dark, 1, 1, 98, 98);
            using (Pen rim = new Pen(Color.FromArgb(170, ink), 1)) { g.DrawEllipse(rim, 1, 1, 98, 98); g.DrawEllipse(rim, 6, 6, 88, 88); }
            using (GraphicsPath robe = new GraphicsPath())
            {
                robe.AddBezier(17, 82, 23, 66, 32, 60, 44, 62); robe.AddBezier(44, 62, 63, 57, 76, 65, 84, 84);
                robe.AddBezier(84, 84, 68, 97, 33, 99, 17, 82);
                using (Brush cloth = new SolidBrush(Color.FromArgb(91, 111, 105))) g.FillPath(cloth, robe);
                using (Pen seam = new Pen(Color.FromArgb(150, ink), 1)) { g.DrawPath(seam, robe); g.DrawBezier(seam, 40, 64, 39, 80, 52, 88, 64, 92); }
            }
            using (GraphicsPath face = new GraphicsPath())
            {
                face.AddBezier(39, 27, 44, 18, 65, 21, 68, 37); face.AddLine(68, 37, 75, 48); face.AddLine(75, 48, 68, 50);
                face.AddBezier(68, 50, 70, 64, 54, 67, 48, 59); face.AddLine(48, 59, 41, 64); face.AddLine(41, 64, 40, 49);
                face.CloseFigure(); using (Brush skin = new SolidBrush(Color.FromArgb(174, 163, 130))) g.FillPath(skin, face);
            }
            using (Brush hair = new SolidBrush(Color.FromArgb(195, 200, 176)))
            { g.FillEllipse(hair, 31, 21, 35, 27); g.FillEllipse(hair, 30, 37, 14, 20); }
            using (Pen etch = new Pen(Color.FromArgb(49, 64, 62), 1.2f))
            { g.DrawArc(etch, 39, 26, 22, 18, 190, 145); g.DrawLine(etch, 59, 43, 65, 43); g.DrawLine(etch, 65, 55, 70, 54); g.DrawLine(etch, 44, 51, 48, 57); }
            using (Pen scroll = new Pen(ink, 1.4f))
            { g.DrawLine(scroll, 25, 73, 62, 84); g.DrawLine(scroll, 25, 73, 21, 84); g.DrawLine(scroll, 21, 84, 58, 94); g.DrawLine(scroll, 58, 94, 62, 84); }
            g.Restore(state);
        }
    }
}
