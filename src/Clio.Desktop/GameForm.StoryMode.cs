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
        private bool semiautomatic, playModeOpen, storyEventOpen, storyChoicesVisible;
        private bool playModeWasAutomatic;
        private StoryDirective storyDirective;
        private int storyDirectiveUntilTurn, storyOfferTurn, storyChoiceOffset;
        private string storyPreviousEventKey = "";
        private readonly List<StoryChoiceRecord> storyChoices = new List<StoryChoiceRecord>();
        private StoryEvent pendingStoryEvent;
        private int storyStartPopulation, storyStartKnown, storyStartTurn;
        private double storyStartFood;
        private bool storyHasSnapshot;
        private bool SemiautomaticMode { get { return semiautomatic; } }
        private bool AutomaticMode { get { return !semiautomatic && autoplay; } }
        private bool StoryModeBlocking { get { return playModeOpen || storyEventOpen; } }

        private void ResetStoryMode()
        {
            StopAutoplay(null); semiautomatic = playModeOpen = storyEventOpen = storyChoicesVisible = false;
            playModeWasAutomatic = false;
            storyDirective = default(StoryDirective); storyDirectiveUntilTurn = 0; storyPreviousEventKey = "";
            storyChoices.Clear(); pendingStoryEvent = null; storyChoiceOffset = 0; storyHasSnapshot = false;
        }

        private void OpenPlayMode()
        {
            if (BlockingSheet) return;
            playModeWasAutomatic = AutomaticMode;
            StopAutoplay(null); ClearMapTransient(); playModeOpen = true; buttons.Clear(); Invalidate();
        }

        private void SelectAutomaticMode()
        {
            SelectPlayMode(false);
            ToggleAutoplay();
        }

        private void ClosePlayMode()
        {
            bool resume = playModeWasAutomatic;
            playModeOpen = false; playModeWasAutomatic = false; buttons.Clear();
            if (resume) ToggleAutoplay();
            Invalidate();
        }

        private void SelectPlayMode(bool assisted)
        {
            StopAutoplay(null); playModeOpen = false; storyEventOpen = false; pendingStoryEvent = null;
            playModeWasAutomatic = false;
            bool changed = semiautomatic != assisted; semiautomatic = assisted;
            ClearMapTransient(); buttons.Clear(); page = 0;
            if (changed) { storyDirectiveUntilTurn = 0; storyHasSnapshot = false; }
            status = assisted ? "Semiautomatic: choose the people's direction; their bands carry out the work." : "Manual: select a band and give its actions yourself.";
            if (assisted && !game.IsOver) ContinueSemiautomatic();
            Invalidate();
        }

        private bool RequireManualOrders()
        {
            if (!semiautomatic) return true;
            StopAutoplay("Semiautomatic is waiting. Use Continue story, or switch to Manual to give individual orders.");
            return false;
        }

        private void ContinueSemiautomatic()
        {
            if (!semiautomatic || BlockingSheet) return;
            if (autoplay) { StopAutoplay("The story is paused. Continue when you are ready."); return; }
            if (game.IsOver) { ShowEnding(); return; }
            if (commands.Count >= MaximumCommands) { status = "The story record is full. Save its history before beginning another."; Invalidate(); return; }
            ClearMapTransient();
            if (pendingStoryEvent != null || storyDirectiveUntilTurn <= game.Turn) { OfferStoryDecision(); return; }
            StartSemiautomaticRun();
        }

        private void StartSemiautomaticRun()
        {
            if (!semiautomatic || BlockingSheet || game.IsOver) return;
            autoplay = true; autoplayTimer.Interval = AutoplayIntervals[autoplaySpeed]; autoplayTimer.Start();
            status = "Semiautomatic: your bands are following " + CurrentStoryChoiceTitle() + ". P pauses the story.";
            buttons.Clear(); Invalidate();
        }

        private string CurrentStoryChoiceTitle()
        { return storyChoices.Count == 0 ? "your next decision" : storyChoices[storyChoices.Count - 1].Choice; }

        private void OfferStoryDecision()
        {
            if (!semiautomatic || game.IsOver || BlockingSheet) return;
            StopAutoplay(null); ClearMapTransient();
            // Offers are reconstructed from present, observed circumstances.
            pendingStoryEvent = StoryDecisionPolicy.Create(game, storyPreviousEventKey);
            if (pendingStoryEvent == null || pendingStoryEvent.Options == null || pendingStoryEvent.Options.Length < 2)
            { status = "No story choice is available. You can return to Manual."; Invalidate(); return; }
            storyOfferTurn = game.Turn; storyEventOpen = true;
            dragging = false; Capture = false; map.IsNavigating = false; settleCamera.Stop(); buttons.Clear(); Invalidate();
        }

        private bool StoryDecisionDue()
        {
            if (!semiautomatic || game.IsOver) return false;
            if (storyDirectiveUntilTurn <= game.Turn) return true;
            int lastChoiceTurn = storyChoices.Count == 0 ? 0 : storyChoices[storyChoices.Count - 1].Turn;
            if (game.Turn <= lastChoiceTurn) return false;
            StoryEvent situation = StoryDecisionPolicy.Create(game, storyPreviousEventKey);
            return situation != null && situation.Urgent && situation.Key != storyPreviousEventKey;
        }

        private void ChooseStoryOption(StoryEvent offered, int option)
        {
            if (!storyEventOpen || offered != pendingStoryEvent || option < 0 || option >= offered.Options.Length) return;
            if (game.Turn != storyOfferTurn) { storyEventOpen = false; pendingStoryEvent = null; OfferStoryDecision(); return; }
            StoryOption choice = offered.Options[option];
            storyDirective = choice.Directive; storyDirectiveUntilTurn = game.Turn + Math.Max(1, Math.Min(6, choice.DurationTurns));
            storyPreviousEventKey = offered.Key;
            storyChoices.Add(new StoryChoiceRecord(game.Turn, offered.Title, choice.Title, choice.Consequences));
            if (storyChoices.Count > 256) storyChoices.RemoveAt(0);
            storyStartTurn = game.Turn; storyStartPopulation = game.ControlledBands.Sum(b => b.Population);
            storyStartFood = game.ControlledBands.Sum(b => b.Food); storyStartKnown = game.Explored.Count; storyHasSnapshot = true;
            storyEventOpen = false; pendingStoryEvent = null; buttons.Clear(); StartSemiautomaticRun();
        }

        private void PauseStoryDecision()
        {
            storyEventOpen = false; playModeOpen = false;
            StopAutoplay(pendingStoryEvent != null ? "The decision is waiting. Continue story to return to it." : semiautomatic ? "The story is paused. Continue when ready." : "Manual control. Select a band to give orders.");
            buttons.Clear(); Invalidate();
        }

        private string StoryProgress()
        {
            if (!storyHasSnapshot) return "Your bands act on the choice. Their actual journeys, supplies and encounters remain in History.";
            int people = game.ControlledBands.Sum(b => b.Population) - storyStartPopulation;
            double food = game.ControlledBands.Sum(b => b.Food) - storyStartFood;
            int known = game.Explored.Count - storyStartKnown;
            return "Since turn " + storyStartTurn + ": people " + people.ToString("+0;-0;0") + ", food " + food.ToString("+0;-0;0") + ", known places " + known.ToString("+0;-0;0") + ". Read History for individual outcomes.";
        }

        private void DrawSemiautomaticDock(Graphics g)
        {
            DrawFloatingMapPanel(g, new RectangleF(16, 860, 953, 59));
            Art.Icon(g, "quill", 31, 874, 29, Art.Gold);
            Typography.Line(g, storyDirectiveUntilTurn > game.Turn ? CurrentStoryChoiceTitle() : "A decision awaits your people", new RectangleF(79, 864, 590, 27), 23, Art.Ink, TypeRole.Heading, true);
            string state = autoplay ? "Bands are acting" : "Paused";
            string detail = storyDirectiveUntilTurn > game.Turn ? state + " / " + (storyDirectiveUntilTurn - game.Turn) + " turns until the next decision" : "Continue story to choose a direction. You do not need to order each band.";
            Typography.Line(g, detail, new RectangleF(81, 893, 724, 21), 14, Art.Muted, TypeRole.Annotation, true);
            Button(g, "Decisions", 830, 871, 124, 37, OpenStoryChoiceHistory, false, false);
            MapTip("Read your decisions and their intended consequences. " + StoryProgress());
        }

        private void DrawSemiautomaticPageDock(Graphics g)
        {
            DrawSemiautomaticDock(g); DrawFloatingMapPanel(g, new RectangleF(977, 860, 607, 59));
            Button(g, "Semiautomatic / Mode", 989, 868, 181, 43, OpenPlayMode, true, false);
            Button(g, "Return to map", 1180, 868, 183, 43, delegate { page = 0; buttons.Clear(); Invalidate(); }, false, false);
            Button(g, autoplay ? "Pause story [P]" : "Continue story", 1373, 868, 195, 43, ContinueSemiautomatic, true, false);
        }

        private void OpenStoryChoiceHistory()
        { StopAutoplay(null); ClearMapTransient(); page = 4; storyChoicesVisible = true; storyChoiceOffset = 0; buttons.Clear(); Invalidate(); }

        private void DrawStoryChoiceHistory(Graphics g)
        {
            StoryChoiceRecord[] choices = storyChoices.AsEnumerable().Reverse().Skip(storyChoiceOffset).Take(4).ToArray();
            if (choices.Length == 0) Typography.Draw(g, "Your choices will be remembered here. Select Semiautomatic from the Mode button to guide your people through story events.", new RectangleF(50, 356, 1200, 96), 24, Art.Muted, TypeRole.Annotation);
            for (int i = 0; i < choices.Length; i++)
            {
                StoryChoiceRecord choice = choices[i]; float y = 340 + i * 98;
                Typography.Label(g, "Turn " + choice.Turn, new RectangleF(49, y + 3, 137, 27), 12, Art.Gold, .5f);
                Typography.Line(g, choice.Title + " / " + choice.Choice, new RectangleF(206, y, 1325, 32), 23, Art.Ink, TypeRole.Heading, true);
                Typography.Draw(g, choice.Consequences, new RectangleF(209, y + 35, 1318, 55), 17, Art.Muted, TypeRole.Body);
            }
            Typography.Line(g, "Chosen priorities are recorded here; actual band actions and results appear in Full history.", new RectangleF(49, 749, 1190, 27), 17, Art.Muted, TypeRole.Annotation, true);
            Button(g, "Earlier", 1333, 750, 93, 30, delegate { storyChoiceOffset = Math.Min(Math.Max(0, storyChoices.Count - 1), storyChoiceOffset + 4); Invalidate(); }, false, false);
            Button(g, "Recent", 1437, 750, 93, 30, delegate { storyChoiceOffset = Math.Max(0, storyChoiceOffset - 4); Invalidate(); }, false, false);
        }

        private void DrawStoryModeOverlay(Graphics g)
        {
            if (!StoryModeBlocking) return;
            using (Brush shade = new SolidBrush(Color.FromArgb(205, 5, 13, 16))) g.FillRectangle(shade, 0, 0, 1600, 960);
            buttons.Clear();
            if (playModeOpen) { DrawPlayModeChoice(g); return; }
            StoryEvent story = pendingStoryEvent;
            if (story == null) return;
            Art.Panel(g, new RectangleF(150, 106, 1300, 748), Color.FromArgb(25, 36, 39), true);
            Typography.Label(g, "Semiautomatic / Turn " + game.Turn, new RectangleF(186, 126, 1188, 24), 12, Art.Gold, 1);
            Typography.Line(g, story.Title, new RectangleF(183, 164, 1192, 59), 40, Art.Ink, TypeRole.Display, true);
            Button(g, "\u00d7", 1388, 126, 34, 32, PauseStoryDecision, false, false);
            AdviserPortraits.Draw(g, new RectangleF(189, 242, 186, 186), story.Voice, Art.Gold);
            CounsellorProfile profile = CouncilPerspectives.Profile(story.Voice);
            Typography.Line(g, profile.Name, new RectangleF(191, 430, 184, 32), 21, Art.Gold, TypeRole.Heading, true, StringAlignment.Center);
            Typography.Line(g, profile.Title, new RectangleF(178, 466, 210, 25), 16, Art.Muted, TypeRole.Annotation, true, StringAlignment.Center);
            Typography.Label(g, story.Context, new RectangleF(413, 238, 991, 31), 11.5f, Art.Gold, .4f);
            CouncilReading(g, story.Story, new RectangleF(412, 281, 992, 156), 25, 21, Art.Ink, TypeRole.Body);
            Typography.Draw(g, StoryProgress(), new RectangleF(414, 450, 986, 48), 17, Art.Muted, TypeRole.Annotation);
            int count = Math.Min(3, story.Options.Length); float width = (1228 - (count - 1) * 18) / count;
            for (int i = 0; i < count; i++)
            {
                int option = i; StoryOption choice = story.Options[i]; RectangleF box = new RectangleF(186 + i * (width + 18), 504, width, 267);
                bool hoverChoice = box.Contains(hoverPoint);
                Art.Panel(g, box, hoverChoice ? Color.FromArgb(56, 60, 47) : Color.FromArgb(29, 41, 44), false);
                if (hoverChoice) using (Pen pen = new Pen(Art.Gold, 1.5f)) g.DrawRectangle(pen, box.X, box.Y, box.Width, box.Height);
                Typography.Label(g, "Choice " + (i + 1) + " / " + choice.DurationTurns + " turns", new RectangleF(box.X + 18, box.Y + 12, width - 36, 22), 11, Art.Gold, .5f);
                Typography.Line(g, choice.Title, new RectangleF(box.X + 15, box.Y + 41, width - 30, 39), 26, Art.Ink, TypeRole.Heading, true);
                CouncilReading(g, choice.Description, new RectangleF(box.X + 18, box.Y + 84, width - 36, 65), 19, 17, Art.Ink, TypeRole.Annotation);
                Art.Line(g, Border, 1, box.X + 18, box.Y + 156, box.Right - 18, box.Y + 156);
                CouncilReading(g, choice.Consequences, new RectangleF(box.X + 18, box.Y + 168, width - 36, 87), 17, 15, Art.Muted, TypeRole.Body);
                buttons.Add(new UiButton(box, delegate { ChooseStoryOption(story, option); }));
            }
            Button(g, "Return to Manual", 186, 783, 188, 40, delegate { SelectPlayMode(false); }, false, false);
            Button(g, "Save story", 389, 783, 130, 40, SaveStory, false, false);
            Typography.Line(g, "Click a choice or press its number to continue. Escape pauses without choosing.", new RectangleF(541, 790, 873, 28), 16, Art.Muted, TypeRole.Annotation, true);
        }

        private void DrawPlayModeChoice(Graphics g)
        {
            Art.Panel(g, new RectangleF(125, 224, 1350, 510), Color.FromArgb(25, 36, 39), true);
            Typography.Label(g, "How will you guide your people?", new RectangleF(160, 253, 1220, 25), 13, Art.Gold, .8f);
            Typography.Line(g, "Choose your mode", new RectangleF(157, 293, 1230, 57), 40, Art.Ink, TypeRole.Display, true);
            Button(g, "\u00d7", 1423, 241, 30, 30, ClosePlayMode, false, false);
            DrawPlayModeCard(g, new RectangleF(160, 377, 414, 246), "Manual", "Choose every action for each band. Gather, move, fight, befriend and build.", "leaf", !semiautomatic && !playModeWasAutomatic, delegate { SelectPlayMode(false); });
            DrawPlayModeCard(g, new RectangleF(593, 377, 414, 246), "Semiautomatic", "Choose story decisions. Your bands follow your direction, then return with another choice.", "quill", semiautomatic, delegate { SelectPlayMode(true); });
            DrawPlayModeCard(g, new RectangleF(1026, 377, 414, 246), "Automatic", "Watch the computer manage every band and advance turns. No story choices required.", "history", playModeWasAutomatic, SelectAutomaticMode);
            Typography.Draw(g, "Switch at any time. In Automatic, press P or Take control to return to Manual.\nWatch settings controls speed, event pauses and camera following.", new RectangleF(164, 653, 1258, 58), 20, Art.Muted, TypeRole.Annotation);
        }

        private void DrawPlayModeCard(Graphics g, RectangleF box, string title, string description, string icon, bool selectedMode, Action choose)
        {
            Art.Panel(g, box, selectedMode || box.Contains(hoverPoint) ? Color.FromArgb(47, 56, 49) : Color.FromArgb(29, 41, 44), false);
            Art.Icon(g, icon, box.X + 20, box.Y + 19, 31, Art.Gold);
            Typography.Line(g, title, new RectangleF(box.X + 68, box.Y + 13, box.Width - 89, 43), 31, Art.Ink, TypeRole.Heading, true);
            Typography.Draw(g, description, new RectangleF(box.X + 23, box.Y + 76, box.Width - 46, box.Height - 124), 22, Art.Ink, TypeRole.Annotation);
            Typography.Label(g, selectedMode ? "Current mode" : "Choose this mode", new RectangleF(box.X + 23, box.Bottom - 31, box.Width - 46, 23), 11, Art.Gold, .5f);
            buttons.Add(new UiButton(box, choose));
        }
    }
}
