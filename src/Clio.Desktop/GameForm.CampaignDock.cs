using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using Clio.Simulation;

namespace Clio.Desktop
{
    public sealed partial class GameForm
    {
        private static readonly RectangleF CampaignToolbarBounds = new RectangleF(14, 64, 282, 40);
        private static readonly RectangleF CampaignBandBadgeBounds = new RectangleF(14, 840, 376, 54);
        private static readonly RectangleF CampaignActionsBounds = new RectangleF(14, 898, 376, 48);
        private static readonly RectangleF CampaignModeRowBounds = new RectangleF(1370, 796, 216, 36);
        private static readonly RectangleF CampaignWatchBounds = new RectangleF(1548, 796, 36, 36);
        private static readonly RectangleF CampaignTurnBounds = new RectangleF(1478, 838, 108, 108);
        private static readonly RectangleF CampaignDecisionsBounds = new RectangleF(1370, 854, 96, 65);

        private RectangleF CampaignToolbarIconBounds(int index)
        { return new RectangleF(21 + index * 45, 67, 34, 34); }

        private RectangleF CampaignActionBounds(int index)
        { return page == 0 ? new RectangleF(20 + index * 46, 904, 40, 40) : new RectangleF(24 + index * 56, 868, 46, 43); }

        private bool CampaignDockContains(PointF point)
        {
            if (page != 0 || BlockingSheet) return false;
            return CampaignToolbarBounds.Contains(point) || CampaignBandBadgeBounds.Contains(point) || CampaignActionsBounds.Contains(point) ||
                CampaignTurnBounds.Contains(point) || !game.IsOver && CampaignModeRowBounds.Contains(point) ||
                !game.IsOver && SemiautomaticMode && CampaignDecisionsBounds.Contains(point);
        }

        private static GraphicsPath CampaignCapsule(RectangleF bounds, float radius)
        {
            GraphicsPath path = new GraphicsPath(); float diameter = Math.Min(Math.Min(bounds.Width, bounds.Height), radius * 2);
            path.AddArc(bounds.X, bounds.Y, diameter, diameter, 180, 90);
            path.AddArc(bounds.Right - diameter, bounds.Y, diameter, diameter, 270, 90);
            path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
            path.AddArc(bounds.X, bounds.Bottom - diameter, diameter, diameter, 90, 90);
            path.CloseFigure(); return path;
        }

        private void DrawCampaignBacking(Graphics g, RectangleF bounds)
        {
            using (GraphicsPath path = CampaignCapsule(bounds, 12))
            using (Brush fill = new SolidBrush(Color.FromArgb(226, 17, 29, 33)))
            using (Pen edge = new Pen(Color.FromArgb(117, Art.Border), .8f))
            { g.FillPath(fill, path); g.DrawPath(edge, path); }
        }

        private void DrawCampaignDock(Graphics g)
        {
            DrawCampaignBandBadge(g);
            DrawCampaignBacking(g, CampaignActionsBounds);
            if (game.IsOver)
            {
                DrawCampaignTextChip(g, "Read history", new RectangleF(22, 905, 170, 34), ReadEndingHistory, false,
                    "Read your people's surviving history.");
                DrawCampaignTextChip(g, "Final account", new RectangleF(200, 905, 182, 34), ShowEnding, false,
                    "Reopen the ending and the final account of your people.");
            }
            else
            {
                DrawMapActionIcons(g);
                DrawCampaignModeControls(g);
            }
            DrawCampaignTurnControl(g);
        }

        private void DrawCampaignBandBadge(Graphics g)
        {
            RectangleF box = CampaignBandBadgeBounds;
            DrawCampaignBacking(g, box);
            if (game.IsOver)
            {
                Art.Icon(g, "history", box.X + 13, box.Y + 13, 28, Art.Gold);
                Typography.Line(g, "Your history remains", new RectangleF(box.X + 53, box.Y + 8, 306, 38), 26, Art.Ink, TypeRole.Heading, true);
                buttons.Add(new UiButton(box, ReadEndingHistory) { Tip = "The living bands are gone. Their recorded history remains." });
                return;
            }
            Band actor = CurrentOrderBand;
            if (HasCommandBand)
                IdentityArt.DrawEmblem(g, game.TribeOf(actor.Id), new RectangleF(box.X + 10, box.Y + 10, 34, 34), false);
            else Art.Icon(g, "people", box.X + 15, box.Y + 15, 25, Art.Muted);
            Typography.Line(g, HasCommandBand ? actor.Name : "Select a band", new RectangleF(box.X + 53, box.Y + (SemiautomaticMode ? 3 : 10), 226, 34), SemiautomaticMode ? 22 : 25, HasCommandBand ? Art.Ink : Art.Muted, TypeRole.Heading, true);
            if (SemiautomaticMode)
                Typography.Line(g, storyDirectiveUntilTurn > game.Turn ? CurrentStoryChoiceTitle() : "A decision awaits", new RectangleF(box.X + 54, box.Y + 32, 263, 18), 14, Art.Muted, TypeRole.Body, true);
            for (int i = 0; i < 2; i++)
            {
                float x = box.X + 288 + i * 18, y = box.Y + (SemiautomaticMode ? 16 : 27);
                using (Brush fill = new SolidBrush(HasCommandBand && i < game.ActionsFor(actor.Id) ? Art.Gold : Art.Border))
                    g.FillPolygon(fill, new[] { new PointF(x, y - 5), new PointF(x + 3.5f, y), new PointF(x, y + 5), new PointF(x - 3.5f, y) });
            }
            buttons.Add(new UiButton(box, ToggleBandDetails) { Tip = HasCommandBand ?
                actor.Name + " / " + game.ActionsFor(actor.Id) + " actions remain (gold diamonds). Click for supplies, personality, Hold and reunion details. Right-click adjacent land to move." :
                "Choose one of your banners to give orders. Click for the band panel, or use Find band above." });
            MapIconButton(g, "branch", new RectangleF(box.Right - 43, box.Y + 9, 35, 35), NextReadyBand, false, false,
                "Next ready band [N]\nSelect a household with actions remaining. No action is spent.");
        }

        private void DrawCampaignModeControls(Graphics g)
        {
            string mode = SemiautomaticMode ? "Semiautomatic" : AutomaticMode ? "Automatic" : "Manual";
            DrawCampaignTextChip(g, mode, new RectangleF(1370, 796, 169, 36), delegate { CloseMapMenus(); OpenPlayMode(); }, SemiautomaticMode || AutomaticMode,
                "Gameplay mode\nManual: choose each band's orders. Semiautomatic: choose story directions. Automatic: watch your people act. Click to change mode.");
            MapIconButton(g, "settings", CampaignWatchBounds, delegate { ToggleMapMenu(3); }, mapMenu == 3, false,
                "Watch settings\nAdviser frequency, autoplay speed, event pauses and camera following.");
            if (SemiautomaticMode)
            {
                RectangleF box = CampaignDecisionsBounds;
                DrawCampaignBacking(g, box);
                Art.Icon(g, "quill", box.X + 37, box.Y + 9, 22, Art.Gold);
                Typography.Line(g, "Decisions", new RectangleF(box.X + 6, box.Y + 36, box.Width - 12, 23), 16, Art.Ink, TypeRole.Action, true, StringAlignment.Center);
                buttons.Add(new UiButton(box, OpenStoryChoiceHistory) { Tip = "Your decisions\n" + CurrentStoryChoiceTitle() + ". " +
                    (storyDirectiveUntilTurn > game.Turn ? (storyDirectiveUntilTurn - game.Turn) + " turns until the next decision. " : "Continue the story to choose a direction. ") +
                    "Open the history of chosen priorities and consequences." });
            }
        }

        private void DrawCampaignTextChip(Graphics g, string text, RectangleF bounds, Action click, bool active, string tip)
        {
            bool hover = bounds.Contains(hoverPoint);
            using (GraphicsPath path = CampaignCapsule(bounds, 12))
            using (Brush fill = new SolidBrush(Color.FromArgb(238, active || hover ? Color.FromArgb(54, 60, 48) : Color.FromArgb(21, 34, 38))))
            using (Pen edge = new Pen(active || hover ? Color.FromArgb(180, Art.Gold) : Color.FromArgb(125, Art.Border), .9f))
            { g.FillPath(fill, path); g.DrawPath(edge, path); }
            Typography.Line(g, text, new RectangleF(bounds.X + 8, bounds.Y, bounds.Width - 16, bounds.Height), 18, active ? Art.Gold : Art.Ink, TypeRole.Action, true, StringAlignment.Center);
            buttons.Add(new UiButton(bounds, click) { Tip = tip });
        }

        private void DrawCampaignTurnControl(Graphics g)
        {
            RectangleF box = CampaignTurnBounds;
            bool hover = box.Contains(hoverPoint);
            string first = "End", second = "turn", key = "SPACE", tip = "End turn [Space]\nResolve supplies, growth and encounters, then restore each band's actions.";
            Action click = delegate { CloseMapMenus(); Command("end"); };
            if (game.IsOver) { first = "New"; second = "story"; key = ""; tip = "Begin a new people's story."; click = NewStory; }
            else if (SemiautomaticMode)
            {
                first = autoplay ? "Pause" : "Continue"; second = "story"; key = "P / SPACE";
                tip = autoplay ? "Pause your bands following the chosen direction [P / Space]." : "Continue the chosen direction, or open the next decision when one is waiting.";
                click = ContinueSemiautomatic;
            }
            else if (AutomaticMode)
            {
                first = "Take"; second = "control"; key = "P"; tip = "Stop Automatic and return to Manual [P].";
                click = ToggleAutoplay;
            }
            using (GraphicsPath ring = new GraphicsPath())
            {
                ring.AddEllipse(box);
                using (PathGradientBrush wash = new PathGradientBrush(ring))
                {
                    wash.CenterColor = hover ? Color.FromArgb(90, 85, 57) : Color.FromArgb(60, 63, 48);
                    wash.SurroundColors = new[] { Color.FromArgb(16, 30, 35) };
                    g.FillPath(wash, ring);
                }
            }
            using (Pen edge = new Pen(Art.Gold, hover ? 2 : 1.2f)) g.DrawEllipse(edge, box);
            using (Pen line = new Pen(Color.FromArgb(95, Art.Gold), .8f)) g.DrawEllipse(line, box.X + 6, box.Y + 6, box.Width - 12, box.Height - 12);
            Typography.Line(g, first, new RectangleF(box.X + 11, box.Y + 22, box.Width - 22, 29), first.Length > 6 ? 20 : 25, Art.Ink, TypeRole.Heading, true, StringAlignment.Center);
            Typography.Line(g, second, new RectangleF(box.X + 11, box.Y + 48, box.Width - 22, 29), 25, Art.Gold, TypeRole.Heading, true, StringAlignment.Center);
            if (key.Length > 0) Typography.Label(g, key, new RectangleF(box.X + 10, box.Y + 80, box.Width - 20, 16), 9, Art.Muted, .45f, StringAlignment.Center);
            buttons.Add(new UiButton(box, click) { Tip = tip });
        }
    }
}
