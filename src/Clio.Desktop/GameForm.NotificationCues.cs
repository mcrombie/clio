using System;
using System.Drawing;
using System.Linq;
using Clio.Simulation;

namespace Clio.Desktop
{
    public sealed partial class GameForm
    {
        // Explicit inspection has priority over automatic notifications. Reports
        // stay unread and can always be opened from the HUD or History.
        private bool NotificationInspectorOpen
        { get { return page == 0 && (mapSelectionOpen || unitStackOpen || bandDetailsOpen); } }

        private RectangleF NotificationCueBounds(float height)
        { return new RectangleF(1220, 222, 342, height); }

        private bool RoutineNoticeVisible
        {
            get { return activeNotice != null && !noticeModal && !BlockingSheet && !NotificationInspectorOpen &&
                (page != 0 || mapMenu == 0) && !dragging && !map.IsNavigating && !AdviserToastVisible; }
        }

        private string AdviserCueSentence(Advisory report)
        {
            string text;
            if (report.Key == "lesson:actions") text = "Use each band's two actions, then end the turn.";
            else if (report.Key == "lesson:movement") text = "Preview travel costs before right-clicking a destination.";
            else if (report.Key == "lesson:camp") text = "Compare camp costs with the food and shelter it provides.";
            else if (report.Key == "lesson:opportunities") text = "Hover over map markers to learn what they offer.";
            else if (report.Key == "guidance:orientation") text = "Select a band, secure its supplies, then explore.";
            else
            {
                switch (report.RecommendedAction)
                {
                    case AdviserAction.GatherSalt: text = "Salt is low: gather at this band's current source."; break;
                    case AdviserAction.FindSalt: text = report.IsGuidance ? "Reach a salt source, then spend an action to gather." : "Salt is low: reach a source and gather before it runs out."; break;
                    case AdviserAction.GatherFood: text = "Food is low: gather before ending the turn."; break;
                    case AdviserAction.ReviewEconomy: text = "Check this band's food forecast before ending the turn."; break;
                    case AdviserAction.ReviewWood: text = "Once food and salt are secure, collect wood for fires and camps."; break;
                    case AdviserAction.InspectThreat: text = "A hostile band is nearby; inspect it before moving."; break;
                    case AdviserAction.ReviewAnimals: text = "Compare animal benefits with care costs before giving orders."; break;
                    case AdviserAction.ReviewCulture: text = "Open Culture to see your practices and their effects."; break;
                    case AdviserAction.ReviewDiplomacy: text = "An independent people is known; review the relationship."; break;
                    case AdviserAction.ReviewGathering: text = "Review the gathering's meeting place, costs and deadline."; break;
                    case AdviserAction.ReviewUnits: text = "Check this band's actions and reunion deadline in Units."; break;
                    default: text = "Inspect the known map before choosing the band's next move."; break;
                }
            }
            return (report.Severity >= AdvisorySeverity.Urgent ? report.Severity + ": " : "") + text;
        }

        private void DrawRoutineNoticeCue(Graphics g, StoryNotice notice)
        {
            RectangleF box = MapToastBounds;
            buttons.RemoveAll(b => b.Bounds.IntersectsWith(box));
            Art.Panel(g, box, Color.FromArgb(24, 36, 40), false);
            string title, detail, icon; Color ink;
            NoticeCueContent(notice, out title, out detail, out icon, out ink);
            Art.Icon(g, icon, box.X + 12, box.Y + 17, 23, ink);
            if (detail.Length > 0)
            {
                Typography.Line(g, title, new RectangleF(box.X + 46, box.Y + 7, box.Width - 152, 24), 17, Art.Ink, TypeRole.Heading, true);
                Typography.Line(g, detail, new RectangleF(box.X + 46, box.Y + 31, box.Width - 152, 20), 14, ink, TypeRole.Body, true);
            }
            else
            {
                RectangleF titleBox = new RectangleF(box.X + 46, box.Y + 9, box.Width - 152, 42);
                Typography.Draw(g, title, titleBox, AdviserReadingSize(g, title, titleBox, 18, 15, TypeRole.Heading), Art.Ink, TypeRole.Heading);
            }
            buttons.Add(new UiButton(box, delegate { OpenNotice(notice, autoplay); }));
            MapTip(Timeline.Label(game, notice.Turn) + ": " + Timeline.DisplayText(game, notice.Title) + "\n" + Timeline.DisplayText(game, notice.Impact) + "\nRead opens the full account.");
            Button(g, "Read", box.Right - 94, box.Y + 16, 55, 27, delegate { OpenNotice(notice, autoplay); }, false, false);
            MapTip("Read what happened and its consequences. This pauses autoplay.");
            Button(g, "\u00d7", box.Right - 30, box.Y + 17, 22, 25, delegate { DismissNotice(false); }, false, false);
            MapTip("Dismiss this notification. The account remains in History.");
        }

        private void NoticeCueContent(StoryNotice notice, out string title, out string detail, out string icon, out Color ink)
        {
            title = Timeline.DisplayText(game, notice.Title); detail = ""; icon = "history"; ink = Art.Gold;
            if (notice.Kind == StoryNoticeKind.Growth)
            { icon = "people"; ink = Color.FromArgb(149, 189, 157); }
            else if (notice.Kind == StoryNoticeKind.Loss || notice.Kind == StoryNoticeKind.Hunger || notice.Kind == StoryNoticeKind.Exposure ||
                notice.Kind == StoryNoticeKind.Salt && notice.Title == "The long salt shortage takes lives")
            { icon = "people"; ink = Color.FromArgb(224, 146, 124); }
            else if (notice.Kind == StoryNoticeKind.Contact || notice.Kind == StoryNoticeKind.Gathering)
                icon = "cooperate";
            else if (notice.Kind == StoryNoticeKind.Combat)
            {
                icon = "conflict"; ink = Color.FromArgb(224, 146, 124);
                EncounterRecord encounter = game.Encounters.Records.FirstOrDefault(e => e.Id == notice.EncounterId);
                if (encounter != null)
                {
                    title = encounter.TargetKind == UnitKind.Animal || encounter.ActorKind == UnitKind.Animal ? "Animal encounter" : "Band combat";
                    detail = encounter.ActorCasualties > 0 || encounter.TargetCasualties > 0 ?
                        "Lost: " + encounter.ActorCasualties + " attackers, " + encounter.TargetCasualties + " defenders" :
                        encounter.DamageToActor > 0 || encounter.DamageToTarget > 0 ? "No losses; damage recorded" : "No losses recorded";
                }
            }
            else if (notice.Kind == StoryNoticeKind.Diplomacy) icon = "conflict";
        }
    }
}
