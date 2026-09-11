using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using Clio.Simulation;

namespace Clio.Desktop
{
    internal sealed class UiButton
    {
        public RectangleF Bounds;
        public Action Click;
        public string Tip;
        public UiButton(RectangleF bounds, Action click) { Bounds = bounds; Click = click; }
    }
    public sealed partial class GameForm : Form
    {
        private Game game;
        private readonly MapRenderer map = new MapRenderer();
        private readonly List<UiButton> buttons = new List<UiButton>();
        private readonly List<string> commands = new List<string>();
        private readonly Timer settleCamera = new Timer { Interval = 140 };
        private readonly Timer autoplayTimer = new Timer { Interval = 900 };
        private bool autoplay, followAutoplay = true;
        private int autoplaySpeed;
        private static readonly int[] AutoplayIntervals = { 900, 300, 90 };
        private static readonly string[] AutoplaySpeeds = { "1×", "3×", "10×" };
        private const int MaximumCommands = 20000;
        private int selected, page;
        // The settings the current story was founded with. They are saved as the story's header.
        private GameSettings starting = NewStorySettings(73421, LanguageStyle.Flowing, Ancestry.Human, false, NewStoryForm.ZholPeople, CultureTemplateId.Zhol, HistoryPace.Abstract);
        private StoryJournal journal;
        private int inspectorPage, inspectedBandId;
        private bool chronicleEvents = true;
        private int noticeOffset;
        private string status = "A world without borders. Choose where your people go next.";
        private Point dragStart, lastMouse;
        private bool dragging, moved;
        private int chronicleOffset, languagePage;
        private int culturePage;
        private bool languagePreview = true;
        private PointF hoverPoint = new PointF(-1, -1);
        private string presentationCaption = "";
        private static readonly Color Background = Color.FromArgb(13, 22, 27), Panel = Color.FromArgb(24, 34, 38), Border = Color.FromArgb(57, 67, 66);
        public GameForm() : this(null) { }
        internal GameForm(string preferencesPath)
        {
            Text = BuildWindowTitle; Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); ClientSize = new Size(1440, 864); MinimumSize = new Size(1050, 700);
            adviserPreferencePath = preferencesPath; adviserFrequency = AdviserPreferences.Read(preferencesPath);
            adviserGuidanceEnabled = adviserFrequency != AdviserFrequency.None;
            BackColor = Background; DoubleBuffered = true; KeyPreview = true; StartPosition = FormStartPosition.CenterScreen;
            game = new Game(starting);
            journal = new StoryJournal(game);
            EnableWoodForStory(); EnableLivestockForStory(); EnableBattlesForStory();
            selected = game.Player.CellId; map.Focus(game.World.Cells[selected]); ArmMapCommandBand(game.Player.Id);
            ResetAdvisers();
            MouseLeave += delegate { hoverPoint = new PointF(-1, -1); HideMapHover(); map.OrderPreviewCell = -1; Invalidate(); };
            Deactivate += delegate { HideMapHover(); map.OrderPreviewCell = -1; Invalidate(); };
            MouseDown += OnDown; MouseMove += OnMove; MouseUp += OnUp; MouseWheel += OnWheel; KeyDown += OnKey;
            settleCamera.Tick += delegate { settleCamera.Stop(); if (!dragging) { map.IsNavigating = false; Invalidate(); } };
            autoplayTimer.Tick += delegate { AutoplayStep(); };
            mapHoverTimer.Tick += delegate { RevealMapHover(); };
            noticeTimer.Tick += delegate { if (activeNotice != null && !noticeModal && DateTime.UtcNow >= noticeUntil) { activeNotice = null; noticeTimer.Stop(); Invalidate(); } };
            unitAnimationTimer.Tick += delegate { if (page == 0 && map.Animating && !BlockingSheet) Invalidate(); };
            unitAnimationTimer.Start();
            Shown += delegate { ApplyStartupWindowMode(); ShowInitialGuidance(); };
            Resize += delegate { CancelWindowGesture(); Invalidate(); };
        }
        protected override void Dispose(bool disposing)
        { if (disposing) { autoplay = false; autoplayTimer.Dispose(); settleCamera.Dispose(); noticeTimer.Dispose(); unitAnimationTimer.Dispose(); mapHoverTimer.Dispose(); DisposeBattleView(); map.Dispose(); } base.Dispose(disposing); }
        private PointF Virtual(Point p)
        {
            RectangleF viewport = GameViewport;
            if (viewport.Width <= 0) return new PointF(-1, -1);
            float scale = viewport.Width / 1600f;
            return new PointF((p.X - viewport.X) / scale, (p.Y - viewport.Y) / scale);
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e); e.Graphics.Clear(Background);
            RectangleF viewport = GameViewport;
            if (viewport.Width <= 0 || viewport.Height <= 0) return;
            GraphicsState frame = e.Graphics.Save();
            try
            {
                e.Graphics.SetClip(viewport, CombineMode.Intersect);
                e.Graphics.TranslateTransform(viewport.X, viewport.Y);
                float scale = viewport.Width / 1600f; e.Graphics.ScaleTransform(scale, scale);
                Draw(e.Graphics);
            }
            finally { e.Graphics.Restore(frame); }
        }
        public void Render(string path)
        {
            using (Bitmap image = new Bitmap(1600, 960)) using (Graphics g = Graphics.FromImage(image))
            { Draw(g); image.Save(path, System.Drawing.Imaging.ImageFormat.Png); }
        }
        private void Draw(Graphics g)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias; g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
            g.Clear(Background); buttons.Clear();
            if (BattleOverlayActive) { DrawBattle(g); return; }
            using (LinearGradientBrush sky = new LinearGradientBrush(new RectangleF(0, 0, 1600, 95), Color.FromArgb(29, 39, 42), Background, 90))
                g.FillRectangle(sky, 0, 0, 1600, 94);
            Art.Line(g, Border, 1, 18, 92, 1582, 92);
            DrawLaurel(g, 36, 43, 21, Art.Gold);
            Typography.Line(g, "C L I O", new RectangleF(69, 6, 204, 58), 43, Art.Ink, TypeRole.Display);
            Typography.Line(g, "The first hearths", new RectangleF(71, 58, 201, 24), 18, Art.Gold, TypeRole.Annotation);
            string[] tabs = { "MAP", "ECONOMY", "CULTURE", "UNITS", "DIPLOMACY", "HISTORY" };
            int[] tabPages = { 0, 1, 2, 3, 5, 4 };
            for (int i = 0; i < tabs.Length; i++)
            {
                int index = tabPages[i]; Navigation(g, tabs[i], 290 + i * 106, 18, 102, 60, delegate { ClearMapTransient(); page = index; buttons.Clear(); Invalidate(); }, page == index);
            }
            Art.Line(g, Border, 1, 934, 25, 934, 69);
            Art.Icon(g, "history", 951, 31, 28, Art.Gold);
            Typography.Label(g, semiautomatic ? "Your people, your decisions" : game.TribesEnabled ? "Two priorities per band" : Timeline.PaceDescription, new RectangleF(995, 18, 224, 20), 11, Art.Gold);
            Typography.Line(g, Timeline.Label(game, game.Turn), new RectangleF(994, 35, 225, 37), 29, Art.Ink, TypeRole.Display, true);
            Button(g, "Save", 1233, 29, 87, 36, SaveStory, false, false);
            Button(g, "Load", 1330, 29, 87, 36, LoadStory, false, false);
            Button(g, "New story", 1427, 29, 145, 36, NewStory, false, false);
            DrawCompactStatus(g);
            if (page == 0) DrawWorld(g);
            else if (page == 1) DrawEconomy(g);
            else if (page == 2) DrawCulture(g);
            else if (page == 3) DrawUnits(g);
            else if (page == 5) DrawDiplomacy(g);
            else DrawChronicle(g);
            if (page == 0)
            {
                DrawMapDock(g);
                if (!BlockingSheet) DrawBandPanel(g);
                if (!BlockingSheet && mapSelectionOpen) DrawMapSelectionCard(g);
            }
            else DrawCommandDock(g);
            Typography.Line(g, Timeline.DisplayText(game, status), new RectangleF(29, 930, 1150, 25), 14, Art.Muted, TypeRole.Body);
            DrawBuildIdentity(g);
            Button(g, "F11", 1521, 930, 63, 26, ToggleFullscreen, fullscreen, false);
            MapTip(fullscreen ? "Return to a window [F11 / Alt+Enter]." : "Enter fullscreen [F11 / Alt+Enter].");
            DrawNotice(g);
            DrawAdviserToast(g);
            if (page == 0 && !BlockingSheet) DrawMapMenus(g);
            DrawEncounterChoice(g);
            DrawMapHoverOverlay(g);
            DrawAdvisers(g);
            DrawEnding(g);
            DrawGatheringChoice(g);
            DrawOpeningAnnouncement(g);
            DrawStoryModeOverlay(g);
        }
        private void DrawWorld(Graphics g)
        {
            SynchronizeUnitSelection();
            map.SelectedAnimalId = inspectorPage == 2 ? selectedAnimalId : -1;
            map.SelectedBandId = inspectorPage == 1 ? inspectedBandId : -1;
            map.CommandedBandId = HasCommandBand ? commandedBandId : -1;
            RefreshReunionRoute();
            map.Draw(g, game, selected);
            DrawMapToolbar(g);
            if (presentationCaption.Length > 0)
            {
                Art.Fill(g, Color.FromArgb(235, Background), 35, 247, 750, 28);
                Typography.Label(g, presentationCaption, new RectangleF(47, 251, 726, 22), 11, Art.Gold, 0.4f);
            }
        }
        private void DrawPolityKey(Graphics g)
        {
            Band[] visible = game.Bands.Where(b => b.Population > 0 && (!map.Fog || game.Explored.Contains(b.CellId))).Take(4).ToArray();
            Art.Panel(g, new RectangleF(35, 666, 429, 114), Color.FromArgb(23, 33, 38), false);
            Typography.Label(g, map.Fog ? "Peoples of the known land" : "Peoples of the atlas", new RectangleF(50, 672, 388, 23), 12, Art.Gold);
            for (int i = 0; i < visible.Length; i++)
            {
                float x = 49 + (i % 2) * 207, y = 702 + (i / 2) * 30;
                IdentityArt.DrawEmblem(g, visible[i].Id, new RectangleF(x, y, 21, 21), false);
                Typography.Line(g, visible[i].Name, new RectangleF(x + 28, y - 1, 168, 25), 16, IdentityArt.ColorFor(visible[i].Id), TypeRole.Heading, true);
            }
            Typography.Line(g, "Presence and camp ranges; land is not owned.", new RectangleF(482, 752, 420, 28), 15, Art.Muted, TypeRole.Annotation);
        }
        private void DrawCulture(Graphics g)
        {
            if (culturePage == 1) DrawLanguages(g); else DrawKnowledge(g);
        }
        private void DrawCultureTabs(Graphics g)
        {
            Button(g, "Practices", 44, 286, 148, 31, delegate { culturePage = 0; Invalidate(); }, culturePage == 0, false);
            Button(g, "Language", 202, 286, 148, 31, delegate { culturePage = 1; Invalidate(); }, culturePage == 1, false);
        }
        private void DrawKnowledge(Graphics g)
        {
            Surface(g, "What a people learns", "Knowledge grows from remembered practice. Each discovery opens another way to live.");
            DrawCultureTabs(g);
            Typography.Label(g, "I   /   Experience", new RectangleF(44, 326, 444, 27), 13, Art.Gold, 0.9f);
            Typography.Label(g, "II   /   Shared practice", new RectangleF(562, 326, 444, 27), 13, Art.Gold, 0.9f);
            Typography.Label(g, "III   /   New ways of living", new RectangleF(1080, 326, 444, 27), 13, Art.Gold, 0.8f);
            PointF[] positions = { new PointF(44, 360), new PointF(44, 499), new PointF(44, 638), new PointF(562, 360), new PointF(562, 499), new PointF(1080, 360), new PointF(1080, 499), new PointF(1080, 638) };
            using (Pen pen = new Pen(Color.FromArgb(104, 130, 109), 1.5f))
            {
                g.DrawBezier(pen, 1006, 420, 1050, 420, 1030, 702, 1080, 702);
                g.DrawBezier(pen, 1006, 559, 1050, 559, 1030, 702, 1080, 702);
            }
            for (int i = 0; i < game.Knowledge.Count; i++)
            {
                Milestone k = game.Knowledge[i]; PointF p = positions[i];
                Art.Panel(g, new RectangleF(p.X, p.Y, 444, 130), k.Known ? Color.FromArgb(43, 55, 47) : Panel, false);
                Art.Line(g, k.Known ? Art.Gold : Border, 2, p.X, p.Y, p.X + 444, p.Y);
                Typography.Line(g, k.Name, new RectangleF(p.X + 16, p.Y + 6, 412, 34), 26, k.Known ? Art.Gold : Art.Ink, TypeRole.Heading, true);
                Art.Icon(g, k.Known ? "branch" : "quill", p.X + 17, p.Y + 54, 23, k.Known ? Art.Gold : Art.Muted);
                Typography.Line(g, k.Known ? "Shared practice" : "Learning through experience", new RectangleF(p.X + 53, p.Y + 51, 368, 29), 17, Art.Muted, TypeRole.Annotation, true);
                string practiceHelp = Timeline.DisplayText(game, k.Description);
                buttons.Add(new UiButton(new RectangleF(p.X, p.Y, 444, 130), delegate { status = practiceHelp; Invalidate(); })
                    { Tip = k.Name + "\n" + practiceHelp + "\n" + (k.Known ? "Known to your people." : Math.Min(k.Progress, k.Target) + " / " + k.Target + " experience. Practice the related activity to learn.") });
                Meter(g, p.X + 16, p.Y + 109, 337, (double)k.Progress / k.Target, Art.Gold);
                Typography.Line(g, k.Known ? "Known" : Math.Min(k.Progress, k.Target) + " / " + k.Target, new RectangleF(p.X + 371, p.Y + 99, 54, 25), 15, Art.Gold, k.Known ? TypeRole.Annotation : TypeRole.Number, true, StringAlignment.Far);
            }
        }
        private void DrawLanguages(Graphics g)
        {
            string introduction = game.FoundingCulture == CultureTemplateId.Zhol ? "Zhol joins Chinese and Spanish sounds: zhong lends zh, and espa\u00f1ol lends ol. These are the first words of your own language." :
                game.FoundingCulture == CultureTemplateId.Generated ? "Every inherited word holds a trace of an older home. State, culture, and language have separate histories." : "An experimental " + HistoricalCultures.LanguageLabel(game.FoundingCulture) + " beginning. Later names and sound changes belong to this imagined world.";
            Surface(g, "The words we carry", introduction);
            DrawCultureTabs(g);
            LanguageProfile root = game.Languages[0];
            // A clearly labeled preview exercises the real sound-change engine before a language branch has emerged in play.
            LanguageProfile a = LanguageGenerator.Branch(root, 10001, game.Seed + 71);
            LanguageProfile b = LanguageGenerator.Branch(root, 10002, game.Seed + 119);
            Band[] knownBands = game.Bands.Where(band => band.Population > 0 && (!map.Fog || game.Explored.Contains(band.CellId))).ToArray();
            LanguageProfile[] living = game.Languages.Where(lang => knownBands.Any(band => band.LanguageId == lang.Id)).ToArray();
            languagePage = Math.Min(languagePage, Math.Max(0, (living.Length - 1) / 3));
            LanguageProfile[] columns = languagePreview ? new[] { root, a, b } : living.Skip(languagePage * 3).Take(3).ToArray();
            Typography.Label(g, languagePreview ? "Descent preview  ·  Possible daughter languages" : "Living speech of " + (map.Fog ? "the known peoples" : "the atlas"), new RectangleF(44, 329, 1110, 30), 12, Art.Gold, 0.9f);
            Button(g, languagePreview ? "Show living speech" : "Show descent preview", 1290, 329, 249, 30, delegate { languagePreview = !languagePreview; languagePage = 0; Invalidate(); }, false, false);
            for (int i = 0; i < columns.Length; i++)
            {
                float x = 282 + i * 430; LanguageProfile lang = columns[i];
                Art.Panel(g, new RectangleF(x, 367, 400, 386), Panel, false);
                Typography.Label(g, languagePreview ? (i == 0 ? "Founding language" : "Illustrative branch " + i) : "Profile " + lang.Id + (lang.ParentId < 0 ? " · Founder" : " · Parent " + lang.ParentId), new RectangleF(x + 17, 373, 368, 25), 11.5f, Art.Gold, 0.6f);
                FittedTitle(g, lang.Name, new RectangleF(x + 16, 398, 368, 40), 30, Art.Ink);
                string origin = lang.Template == CultureTemplateId.Zhol ? "Mandarin + Spanish · your founding vocabulary" : lang.Template == CultureTemplateId.Generated ? "Founding palette · " + starting.Style : HistoricalCultures.LanguageLabel(lang.Template) + " · experimental roots";
                Typography.Draw(g, lang.ParentId < 0 ? origin : lang.SoundChange, new RectangleF(x + 16, 438, 368, 42), 15, Art.Muted, TypeRole.Annotation);
                Art.Rule(g, x + 17, 481, 366);
            }
            string[] words = { "water", "river", "forest", "mountain", "wolf", "cattle", "people", "home", "fire" };
            for (int row = 0; row < words.Length; row++)
            {
                float y = 490 + row * 28;
                Typography.Line(g, words[row], new RectangleF(48, y - 1, 210, 28), 19, Art.Muted, TypeRole.Annotation);
                for (int col = 0; col < columns.Length; col++) Typography.Line(g, columns[col].Words[words[row]], new RectangleF(300 + col * 430, y - 1, 367, 28), 21, Art.Ink, TypeRole.Body, true);
            }
            Typography.Line(g, (map.Fog ? "Known to your people: " : "In the atlas: ") + living.Length + (living.Length == 1 ? " living language" : " living languages") + ", carried by " + knownBands.Length + (knownBands.Length == 1 ? " polity." : " polities.") + (languagePreview ? " Preview branches illustrate possible descent." : ""), new RectangleF(44, 757, 1225, 31), 16, Art.Muted, TypeRole.Annotation, true);
            if (!languagePreview && living.Length > 3) Button(g, "Next profiles", 1395, 757, 144, 28, delegate { languagePage = (languagePage + 1) % ((living.Length + 2) / 3); Invalidate(); }, false, false);
        }
        private void DrawChronicle(Graphics g)
        {
            Surface(g, "A history made by living", "Consequences, discoveries and counsel. Every choice leaves a memory.");
            Button(g, "Events & counsel", 44, 285, 171, 31, delegate { storyChoicesVisible = false; chronicleEvents = true; Invalidate(); }, !storyChoicesVisible && chronicleEvents, false);
            Button(g, "Full history", 225, 285, 163, 31, delegate { storyChoicesVisible = false; chronicleEvents = false; Invalidate(); }, !storyChoicesVisible && !chronicleEvents, false);
            Button(g, "Your decisions", 398, 285, 173, 31, delegate { storyChoicesVisible = true; Invalidate(); }, storyChoicesVisible, false);
            if (storyChoicesVisible) { DrawStoryChoiceHistory(g); return; }
            if (chronicleEvents) { DrawNoticeArchive(g); return; }
            ChronicleEntry[] record = VisibleChronicle();
            chronicleOffset = Math.Max(0, Math.Min(Math.Max(0, record.Length - 1), chronicleOffset));
            int end = Math.Max(0, record.Length - chronicleOffset);
            var entries = record.Take(end).Reverse().Take(6).ToArray();
            float y = 334;
            foreach (ChronicleEntry entry in entries)
            {
                string when = Timeline.Label(game, entry.Turn);
                Typography.Line(g, when, new RectangleF(48, y + 1, 165, 25), 15, Art.Gold, TypeRole.Number, true);
                Art.Line(g, Art.Mix(Border, Art.Gold, 0.18), 1, 229, y + 4, 229, y + 52);
                Typography.Draw(g, Timeline.DisplayText(game, entry.Text), new RectangleF(253, y - 2, 1260, 59), 19, Art.Ink, TypeRole.Body); y += 68;
            }
            Typography.Line(g, "Entries describe the choices and outcomes within each turn.", new RectangleF(48, 750, 1120, 28), 16, Art.Muted, TypeRole.Annotation);
            Button(g, "Earlier", 1333, 750, 93, 30, delegate { chronicleOffset = Math.Min(Math.Max(0, record.Length - 1), chronicleOffset + 6); Invalidate(); }, false, false);
            Button(g, "Recent", 1437, 750, 93, 30, delegate { chronicleOffset = Math.Max(0, chronicleOffset - 6); Invalidate(); }, false, false);
        }
        private void Surface(Graphics g, string title, string subtitle)
        {
            Art.Panel(g, new RectangleF(16, 174, 1568, 621), Color.FromArgb(19, 29, 34), false);
            Typography.Line(g, title, new RectangleF(42, 204, 1427, 49), 35, Art.Ink, TypeRole.Display, true);
            if (!String.IsNullOrWhiteSpace(subtitle))
            {
                RectangleF help = new RectangleF(1503, 212, 32, 32);
                Typography.Line(g, "?", help, 21, Art.Muted, TypeRole.Heading, false, StringAlignment.Center);
                buttons.Add(new UiButton(help, delegate { status = subtitle; Invalidate(); }) { Tip = title + "\n" + subtitle });
            }
        }
        private void Button(Graphics g, string text, float x, float y, float w, float h, Action click, bool active, bool action)
        {
            RectangleF bounds = new RectangleF(x, y, w, h);
            bool disabled = action && (CurrentOrderActions <= 0 || game.IsOver || game.TribesEnabled && !HasCommandBand), hover = bounds.Contains(hoverPoint) && !disabled;
            Color fill = active ? Color.FromArgb(65, 65, 49) : Color.FromArgb(27, 39, 43);
            if (hover) fill = Art.Mix(fill, Art.Gold, 0.11);
            using (LinearGradientBrush brush = new LinearGradientBrush(bounds, Art.Mix(fill, Art.Ink, 0.025), fill, 90)) g.FillRectangle(brush, bounds);
            using (Pen pen = new Pen(active ? Art.Mix(Art.Gold, Background, 0.16) : hover ? Art.Mix(Border, Art.Gold, 0.50) : Border, 1)) g.DrawRectangle(pen, x, y, w, h);
            Typography.Line(g, text, new RectangleF(x + 6, y, w - 12, h), 13.5f, disabled ? Color.FromArgb(103, 113, 111) : active ? Art.Gold : Art.Ink, TypeRole.Action, true, StringAlignment.Center);
            buttons.Add(new UiButton(bounds, click));
        }
        private void Navigation(Graphics g, string text, float x, float y, float w, float h, Action click, bool active)
        {
            RectangleF bounds = new RectangleF(x, y, w, h); bool hover = bounds.Contains(hoverPoint);
            if (active || hover)
            {
                using (LinearGradientBrush light = new LinearGradientBrush(bounds, Color.FromArgb(0, Art.Gold), Color.FromArgb(active ? 17 : 10, Art.Gold), 90)) g.FillRectangle(light, bounds);
            }
            Typography.Label(g, text, new RectangleF(x + 8, y + 1, w - 16, h - 7), 13, active ? Art.Gold : hover ? Art.Ink : Art.Muted, 0.9f, StringAlignment.Center);
            if (active)
            {
                Art.Line(g, Art.Gold, 1.5f, x + 15, y + h - 3, x + w - 15, y + h - 3);
                using (Brush point = new SolidBrush(Art.Gold)) g.FillPolygon(point, new[] { new PointF(x + w / 2 - 3, y + h - 3), new PointF(x + w / 2, y + h), new PointF(x + w / 2 + 3, y + h - 3) });
            }
            buttons.Add(new UiButton(bounds, click));
        }
        private void ActionButton(Graphics g, string text, string shortcut, string icon, float x, float y, float width, Action click, bool primary)
        {
            float height = primary ? 48 : 43; RectangleF bounds = new RectangleF(x, y, width, height);
            bool disabled = !primary && (CurrentOrderActions <= 0 || game.IsOver || game.TribesEnabled && !HasCommandBand), hover = bounds.Contains(hoverPoint) && !disabled;
            Color fill = primary ? Color.FromArgb(188, 157, 101) : Color.FromArgb(29, 41, 45);
            Color foreground = primary ? Color.FromArgb(26, 32, 31) : disabled ? Color.FromArgb(108, 117, 112) : Art.Ink;
            if (hover) fill = Art.Mix(fill, Art.Ink, primary ? 0.14 : 0.07);
            using (LinearGradientBrush brush = new LinearGradientBrush(bounds, Art.Mix(fill, Art.Ink, primary ? 0.08 : 0.025), fill, 90)) g.FillRectangle(brush, bounds);
            using (Pen border = new Pen(primary ? Art.Gold : hover ? Art.Mix(Border, Art.Gold, 0.5) : Border, 1)) g.DrawRectangle(border, x, y, width, height);
            Art.Line(g, primary ? Color.FromArgb(63, 239, 225, 187) : Color.FromArgb(15, Art.Ink), 1, x + 1, y + 1, x + width - 1, y + 1);
            Art.Icon(g, icon, x + 12, y + (height - 19) / 2, 19, primary ? foreground : disabled ? foreground : Art.Gold);
            Typography.Line(g, text, new RectangleF(x + 41, y, width - 46 - (shortcut.Length > 0 ? (primary ? 48 : 20) : 0), height), 14, foreground, TypeRole.Action, true);
            if (shortcut.Length > 0) Typography.Line(g, shortcut, new RectangleF(x + width - (primary ? 56 : 31), y + 8, primary ? 47 : 22, height - 16), primary ? 10 : 11, primary ? foreground : Art.Muted, TypeRole.Utility, false, StringAlignment.Center);
            buttons.Add(new UiButton(bounds, click));
        }
        private void LineText(Graphics g, string text, RectangleF bounds, float size, Color color, bool fit)
        {
            Typography.Line(g, text, bounds, size, color, TypeRole.Body, fit);
        }
        private ChronicleEntry[] VisibleChronicle()
        {
            // The released core's observer-only entry has no subject ID. Keep that
            // legacy format intact and admit it only for a currently known band.
            const string distant = "Away from the parent hearth, ";
            return game.Chronicle.Where(e => !map.Fog || !e.Text.StartsWith(distant, StringComparison.Ordinal) ||
                game.Bands.Any(b => game.Explored.Contains(b.CellId) && e.Text.StartsWith(distant + b.Name + " develops a distinct speech: ", StringComparison.Ordinal))).ToArray();
        }
        private void Number(Graphics g, string text, RectangleF bounds, Color color)
        {
            float size = 49;
            while (size > 18)
            {
                if (g.MeasureString(text, Typography.Font(TypeRole.Number, size)).Width <= bounds.Width) break;
                size -= 1;
            }
            using (Brush brush = new SolidBrush(color))
            using (StringFormat format = new StringFormat { FormatFlags = StringFormatFlags.NoWrap, LineAlignment = StringAlignment.Center, Trimming = StringTrimming.EllipsisCharacter })
                g.DrawString(text, Typography.Font(TypeRole.Number, size), brush, bounds, format);
        }
        private void FittedTitle(Graphics g, string text, RectangleF bounds, float desiredSize, Color color)
        {
            float size = desiredSize;
            using (StringFormat format = new StringFormat { Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.LineLimit })
            {
                while (size > 18)
                {
                    if (g.MeasureString(text, Typography.Font(TypeRole.Heading, size), (int)bounds.Width, format).Height <= bounds.Height) break;
                    size -= 1;
                }
                using (Brush brush = new SolidBrush(color)) g.DrawString(text, Typography.Font(TypeRole.Heading, size), brush, bounds, format);
            }
        }
        private void DrawLaurel(Graphics g, float x, float y, float radius, Color color)
        {
            using (Pen pen = new Pen(Art.Mix(color, Background, 0.18), 1))
            {
                g.DrawArc(pen, x - radius * 0.65f, y - radius, radius * 1.3f, radius * 2, 45, 104);
                g.DrawArc(pen, x - radius * 0.65f, y - radius, radius * 1.3f, radius * 2, 31, -104);
                for (int side = -1; side <= 1; side += 2)
                    for (int i = 0; i < 5; i++)
                    {
                        float yy = y + 12 - i * 6, xx = x + side * (9 + (float)Math.Sin(i * 0.65) * 5);
                        g.DrawBezier(pen, xx, yy, xx + side * 5, yy, xx + side * 7, yy - 4, xx + side * 6, yy - 7);
                        g.DrawBezier(pen, xx, yy, xx - side * 3, yy - 2, xx - side * 4, yy - 5, xx - side * 2, yy - 6);
                    }
                g.DrawLine(pen, x - 8, y + radius + 2, x + 8, y + radius + 2);
            }
        }
        private void DrawSeason(Graphics g, float x, float y, float radius)
        {
            using (Pen fine = new Pen(Art.Mix(Art.Gold, Background, 0.4), 1))
            {
                g.DrawEllipse(fine, x - radius, y - radius, radius * 2, radius * 2);
                for (int i = 0; i < 8; i++)
                {
                    double angle = i * Math.PI / 4;
                    g.DrawLine(fine, x + (float)Math.Cos(angle) * (radius + 3), y + (float)Math.Sin(angle) * (radius + 3), x + (float)Math.Cos(angle) * (radius + 5), y + (float)Math.Sin(angle) * (radius + 5));
                }
            }
            Art.Icon(g, game.Season == "Spring" ? "leaf" : game.Season == "Winter" ? "snow" : game.Season == "Autumn" ? "branch" : "sun", x - 10, y - 10, 20, Art.Gold);
        }
        private void Meter(Graphics g, float x, float y, float width, double proportion, Color color)
        {
            Art.Fill(g, Color.FromArgb(47, 58, 59), x, y, width, 3);
            float filled = width * (float)Math.Max(0, Math.Min(1, proportion));
            if (filled > 0) Art.Fill(g, color, x, y, filled, 3);
            for (int i = 1; i < 6; i++) Art.Line(g, Panel, 1, x + width * i / 6, y, x + width * i / 6, y + 3);
        }
        private void Command(string command)
        {
            if (BattleOverlayActive || adviserOpen || gatheringChoice || openingAnnouncement || StoryModeBlocking) return;
            if (!command.StartsWith("enable-", StringComparison.Ordinal) && !RequireManualOrders()) return;
            ClearMapTransient();
            StopAutoplay(null);
            command = CanonicalOrder(command);
            if (command == null || endingOpen) { Invalidate(); return; }
            if (game.IsOver) { status = "This story has ended. Read its history or begin a new story."; Invalidate(); return; }
            if (commands.Count >= MaximumCommands) { status = "The story record is full. Save this chronicle before beginning a new story."; Invalidate(); return; }
            JournalSnapshot before = StoryJournal.Capture(game);
            status = Execute(game, command); commands.Add(command); chronicleOffset = 0;
            if (command == "enable-encounters") map.InvalidateTerrain();
            SynchronizeUnitSelection();
            PresentBattleOrNotices(journal.Record(game, before, command, status), false);
        }
        private void SetAutoplaySpeed(int speed)
        {
            autoplaySpeed = Math.Max(0, Math.Min(AutoplayIntervals.Length - 1, speed));
            autoplayTimer.Interval = AutoplayIntervals[autoplaySpeed];
            status = "Autoplay pace: " + AutoplaySpeeds[autoplaySpeed] + ". One decision at a time; P or Esc returns control.";
            Invalidate();
        }
        private void ToggleAutoplay()
        {
            if (BlockingSheet) return;
            if (semiautomatic) { ContinueSemiautomatic(); return; }
            ClearMapTransient();
            if (autoplay) { StopAutoplay("You are guiding your people again."); return; }
            if (game.IsOver) { status = "This band's story has ended. Its chronicle remains."; Invalidate(); return; }
            if (commands.Count >= MaximumCommands) { status = "The story record is full. Save this chronicle before beginning a new story."; Invalidate(); return; }
            autoplay = true;
            if (followAutoplay) { selected = CurrentOrderBand.CellId; map.Focus(game.World.Cells[selected]); }
            autoplayTimer.Interval = AutoplayIntervals[autoplaySpeed]; autoplayTimer.Start();
            status = "Automatic mode is guiding your people. Press P or Take control to return to Manual.";
            Invalidate();
        }
        private void StopAutoplay(string message)
        {
            autoplay = false; autoplayTimer.Stop();
            if (!String.IsNullOrEmpty(message)) status = message;
            Invalidate();
        }
        private void AutoplayStep()
        {
            if (!autoplay || IsDisposed || dragging || map.IsNavigating) return;
            if (BattleOverlayActive) { AutoplayBattleStep(); return; }
            if (BlockingSheet) return;
            if (game.IsOver) { StopAutoplay("Autoplay has ended with this band's story. Its chronicle remains."); return; }
            if (commands.Count >= MaximumCommands) { StopAutoplay("Autoplay paused: the story record is full. You can save the complete chronicle."); return; }
            try
            {
                if (semiautomatic && StoryDecisionDue()) { OfferStoryDecision(); return; }
                AutoplayDecision decision = semiautomatic ? StoryDecisionPolicy.Choose(game, storyDirective) : AutoplayPolicy.Choose(game);
                if (decision == null) { StopAutoplay("Autoplay has finished. You are in control."); return; }
                HideMapHover();
                int followedActor = CurrentOrderBand.Id;
                int actorId; string actorOrder;
                if (game.TribesEnabled && TryBandCommand(decision.Command, out actorId, out actorOrder) && game.CanControlBand(actorId)) ArmMapCommandBand(actorId);
                int turn = game.Turn, actions = AvailableOrderActions, cell = CurrentOrderBand.CellId, entries = VisibleChronicle().Length;
                JournalSnapshot before = StoryJournal.Capture(game);
                string result = Execute(game, decision.Command);
                commands.Add(decision.Command);
                SynchronizeUnitSelection();
                PresentBattleOrNotices(journal.Record(game, before, decision.Command, result), true);
                if (chronicleOffset > 0) chronicleOffset = Math.Max(0, chronicleOffset + VisibleChronicle().Length - entries);
                if (followAutoplay && !game.IsOver && !BattleOverlayActive)
                {
                    Band followed = CurrentOrderBand;
                    bool refocus = followedActor != followed.Id || cell != followed.CellId || selected != followed.CellId;
                    selected = followed.CellId; inspectedBandId = followed.Id; selectedAnimalId = -1; inspectorPage = 1;
                    map.SelectedBandId = followed.Id; map.SelectedAnimalId = -1;
                    if (refocus) map.Focus(game.World.Cells[selected]);
                }
                status = (semiautomatic ? "Semiautomatic" : "Autoplay " + AutoplaySpeeds[autoplaySpeed]) + "  /  " + decision.Reason + "  " + result;
                if (!BattleOverlayActive && game.IsOver) StopAutoplay("Autoplay has ended with this band's story. Its chronicle remains.");
                else if (!BattleOverlayActive && game.Turn == turn && AvailableOrderActions == actions) StopAutoplay("Autoplay paused: " + result + " You can take the next action.");
                else Invalidate();
            }
            catch (Exception error) { StopAutoplay("Autoplay paused: " + error.Message); }
        }
        internal static string Execute(Game target, string command)
        {
            if (command == "enable-tactical-battles") return target.EnableTacticalBattles();
            if (command.StartsWith("battle-", StringComparison.Ordinal)) return target.ExecuteBattleCommand(command);
            int bandId; string order;
            if (TryBandCommand(command, out bandId, out order))
            {
                string verb = order.Split(':')[0];
                if (verb != "forage" && verb != "salt" && verb != "wood" && verb != "camp" && verb != "split" && verb != "hunt" && verb != "tame" && verb != "wait" &&
                    verb != "move" && verb != "slaughter" && verb != "attack-animal" && verb != "befriend-animal" && verb != "attack-band" &&
                    verb != "gather-invite" && verb != "gather-aid" && verb != "gather-return") throw new InvalidDataException("Unknown band order.");
                if (verb.StartsWith("gather-", StringComparison.Ordinal)) ValidateGatheringSyntax(order);
                return target.IssueBandCommand(bandId, order);
            }
            if (command == "enable-tribes") return target.EnableTribes();
            if (command == "enable-terrain") return target.EnableTerrainTravel();
            if (command == "enable-personalities") return target.EnableBandPersonalities();
            if (command == "enable-gatherings") return target.EnableGatherings();
            if (command == "enable-wood") return target.EnableWood();
            if (command == "enable-livestock") return target.EnableLivestock();
            if (command.StartsWith("slaughter:", StringComparison.Ordinal)) return target.SlaughterHerd(Int32.Parse(command.Substring(10), CultureInfo.InvariantCulture));
            if (command.StartsWith("attack-animal:", StringComparison.Ordinal)) return target.AttackAnimal(Int32.Parse(command.Substring(14), CultureInfo.InvariantCulture));
            if (command.StartsWith("befriend-animal:", StringComparison.Ordinal)) return target.BefriendAnimal(Int32.Parse(command.Substring(16), CultureInfo.InvariantCulture));
            if (command.StartsWith("attack-band:", StringComparison.Ordinal)) return target.AttackBand(Int32.Parse(command.Substring(12), CultureInfo.InvariantCulture));
            if (command == "enable-encounters") return target.EnableUnitEncounters();
            if (command == "enable-place-names") return target.EnableCulturalPlaceNames();
            if (command == "enable-salt") return target.EnableSaltEconomy();
            if (command == "salt") return target.GatherSalt();
            if (command == "wood") return target.GatherWood();
            if (command.StartsWith("move:", StringComparison.Ordinal)) return target.Move(Int32.Parse(command.Substring(5), CultureInfo.InvariantCulture));
            switch (command) { case "forage": return target.Forage(); case "hunt": return target.Hunt(); case "tame": return target.Tame(); case "camp": return target.Camp(); case "split": return target.Split(); case "end": return target.EndTurn(); default: throw new InvalidDataException("Unknown story command."); }
        }
        private void OnDown(object sender, MouseEventArgs e)
        {
            if (BattleOverlayActive)
            { if (GameViewport.Contains(e.Location)) HandleBattlePointerDown(Virtual(e.Location), e.Button); return; }
            if (e.Button == MouseButtons.Right) { OnRightDown(e); return; }
            if (e.Button != MouseButtons.Left || !GameViewport.Contains(e.Location)) return;
            PointF p = Virtual(e.Location);
            HideMapHover();
            if (HandleMapOverlayDown(p)) return;
            for (int i = buttons.Count - 1; i >= 0; i--) if (buttons[i].Bounds.Contains(p)) { buttons[i].Click(); return; }
            if (AdviserToastVisible && AdviserToastBounds.Contains(p)) return;
            if (BlockingSheet) return;
            if (page == 0 && map.Bounds.Contains(p)) { dragging = true; moved = false; dragStart = e.Location; lastMouse = e.Location; Capture = true; }
        }
        private void OnMove(object sender, MouseEventArgs e)
        {
            PointF p = Virtual(e.Location);
            if (BattleOverlayActive) { hoverPoint = p; UpdateBattleHover(p); return; }
            int oldHover = buttons.FindIndex(b => b.Bounds.Contains(hoverPoint)), newHover = buttons.FindIndex(b => b.Bounds.Contains(p));
            hoverPoint = p; Cursor = newHover >= 0 ? Cursors.Hand : page == 0 && map.Bounds.Contains(p) ? Cursors.SizeAll : Cursors.Default;
            if (oldHover != newHover) Invalidate();
            UpdateOrderPreview(p); UpdateMapHover(p);
            if (page == 0 && !BlockingSheet && newHover < 0 && !MapOverlayContains(p) && map.Bounds.Contains(p))
            {
                int opportunityKind;
                Cursor = map.PeekAnimal(p.X, p.Y) >= 0 || map.PickBand(p.X, p.Y) >= 0 || map.PickOpportunity(game, p.X, p.Y, out opportunityKind) >= 0 ? Cursors.Hand : map.OrderPreviewCell >= 0 ? Cursors.Cross : Cursors.SizeAll;
            }
            if (!dragging) return;
            if (Math.Abs(e.X - dragStart.X) + Math.Abs(e.Y - dragStart.Y) > 5) moved = true;
            float scale = GameViewport.Width / 1600f;
            if (moved && scale > 0) { map.IsNavigating = true; map.Longitude -= (e.X - lastMouse.X) * 0.003 / (map.Zoom * scale); map.Latitude += (e.Y - lastMouse.Y) * 0.003 / (map.Zoom * scale * map.RegionalVerticalScale); Invalidate(); }
            lastMouse = e.Location;
        }
        private void OnUp(object sender, MouseEventArgs e)
        {
            if (BattleOverlayActive) { dragging = false; Capture = false; return; }
            if (e.Button != MouseButtons.Left) return;
            if (!dragging) return; dragging = false; Capture = false; map.IsNavigating = false;
            if (!moved && !BlockingSheet && GameViewport.Contains(e.Location))
            {
                PointF p = Virtual(e.Location); int stackCell = map.PickUnitStack(p.X, p.Y);
                if (stackCell >= 0 && game.Explored.Contains(stackCell)) { OpenUnitStack(stackCell); Invalidate(); return; }
                int animalId = map.PickAnimal(p.X, p.Y), bandId = map.PickBand(p.X, p.Y), opportunityKind;
                int opportunityCell = map.PickOpportunity(game, p.X, p.Y, out opportunityKind);
                int id = opportunityCell >= 0 ? opportunityCell : map.Pick(p.X, p.Y);
                Beast animal = game.Beasts.FirstOrDefault(b => b.Id == animalId && b.Count > 0);
                Band band = game.Bands.FirstOrDefault(b => b.Id == bandId);
                if (animal != null && UnitVisible(animal.CellId)) { SelectAnimal(animal); status = "Animal unit selected. Inspect its temperament, then attack or approach."; }
                else if (band != null && band.Population > 0 && UnitVisible(band.CellId)) { selectedAnimalId = -1; inspectedBandId = band.Id; selected = band.CellId; inspectorPage = 1; status = "People selected. Open a ledger from their card for the fuller record."; }
                else if (id >= 0) { selectedAnimalId = -1; selected = id; inspectorPage = 0; status = game.CanMove(id) ? "Adjacent land selected. Press M to move or choose an encounter." : "Place selected. Its local record is open."; }
                if (id >= 0 || animal != null || band != null) ShowMapSelection();
            }
            Invalidate();
        }
        private void OnWheel(object sender, MouseEventArgs e)
        {
            if (BlockingSheet || !GameViewport.Contains(e.Location)) return;
            if (AdviserToastVisible && AdviserToastBounds.Contains(Virtual(e.Location))) return;
            if (page == 0) { PointF point = Virtual(e.Location); if (!map.Bounds.Contains(point) || MapOverlayContains(point) || buttons.Any(b => b.Bounds.Contains(point))) return; ClearMapTransient(); map.IsNavigating = true; map.Zoom = Math.Max(0.82, Math.Min(MapRenderer.MaximumZoom, map.Zoom * (e.Delta > 0 ? 1.12 : 1 / 1.12))); settleCamera.Stop(); settleCamera.Start(); }
            else if (page == 4) { if (storyChoicesVisible) storyChoiceOffset = Math.Max(0, Math.Min(Math.Max(0, storyChoices.Count - 1), storyChoiceOffset + (e.Delta > 0 ? 2 : -2))); else if (chronicleEvents) noticeOffset = Math.Max(0, Math.Min(journal.Notices.Count - 1, noticeOffset + (e.Delta > 0 ? 2 : -2))); else chronicleOffset = Math.Max(0, Math.Min(VisibleChronicle().Length - 1, chronicleOffset + (e.Delta > 0 ? 2 : -2))); }
            Invalidate();
        }
        private void OnKey(object sender, KeyEventArgs e)
        {
            if (IsFullscreenShortcut(e.KeyData)) { ToggleFullscreen(); e.Handled = true; e.SuppressKeyPress = true; return; }
            if (BattleOverlayActive) { HandleBattleKey(e); e.Handled = true; e.SuppressKeyPress = true; return; }
            if (StoryModeBlocking)
            {
                if (e.KeyCode == Keys.Escape || e.KeyCode == Keys.P) PauseStoryDecision();
                else if (e.Control && e.KeyCode == Keys.S) SaveStory();
                else if (storyEventOpen && pendingStoryEvent != null)
                {
                    if (e.KeyCode >= Keys.D1 && e.KeyCode <= Keys.D3) ChooseStoryOption(pendingStoryEvent, e.KeyCode - Keys.D1);
                    else if (e.KeyCode >= Keys.NumPad1 && e.KeyCode <= Keys.NumPad3) ChooseStoryOption(pendingStoryEvent, e.KeyCode - Keys.NumPad1);
                }
                e.Handled = true; e.SuppressKeyPress = true; return;
            }
            if (endingOpen) { if (e.KeyCode == Keys.Escape || e.KeyCode == Keys.Enter) DismissEnding(); e.Handled = true; e.SuppressKeyPress = true; return; }
            if (gatheringChoice) { if (e.KeyCode == Keys.Escape) CloseGatheringChoice(); e.Handled = true; e.SuppressKeyPress = true; return; }
            if (openingAnnouncement) { if (e.KeyCode == Keys.Escape || e.KeyCode == Keys.Enter) CloseOpeningAnnouncement(false); e.Handled = true; e.SuppressKeyPress = true; return; }
            if (encounterChoice) { if (e.KeyCode == Keys.Escape || e.KeyCode == Keys.P) CloseEncounterChoice(); else if (e.KeyCode == Keys.A) CommitEncounter(false); else if (e.KeyCode == Keys.B) CommitEncounter(true); e.Handled = true; e.SuppressKeyPress = true; return; }
            if (noticeModal) { if (e.KeyCode == Keys.Enter) CloseNotice(false, !resumeAfterNotice); else if (e.KeyCode == Keys.Escape || e.KeyCode == Keys.P && resumeAfterNotice) DismissNotice(false); e.Handled = true; e.SuppressKeyPress = true; return; }
            if (adviserOpen) { if (e.KeyCode == Keys.Escape || e.KeyCode == Keys.Enter) CloseAdvisers(); e.Handled = true; e.SuppressKeyPress = true; return; }
            if (e.KeyCode == Keys.C && !e.Control && !e.Alt) { OpenAdvisers(null); e.Handled = true; e.SuppressKeyPress = true; return; }
            if (e.KeyCode == Keys.Escape && page == 0 && (mapSelectionOpen || bandDetailsOpen || mapMenu != 0)) { ClearMapTransient(); buttons.Clear(); Invalidate(); e.Handled = true; e.SuppressKeyPress = true; return; }
            if (e.KeyCode == Keys.P && !e.Control && !e.Alt) { ToggleAutoplay(); e.Handled = true; e.SuppressKeyPress = true; }
            else if (e.KeyCode == Keys.N && !e.Control && !e.Alt) { NextReadyBand(); e.Handled = true; e.SuppressKeyPress = true; }
            else if (e.KeyCode == Keys.Escape) { StopAutoplay("You are guiding your people again."); if (fullscreen) SetFullscreen(false); e.Handled = true; e.SuppressKeyPress = true; }
            else if (e.KeyCode == Keys.Space) { if (semiautomatic) ContinueSemiautomatic(); else Command("end"); e.Handled = true; }
            else if (e.KeyCode == Keys.F) Command("forage"); else if (e.KeyCode == Keys.M) MoveSelection();
            else if (e.KeyCode == Keys.W) GatherHouseholdWood();
            else if (e.KeyCode == Keys.A) ChooseAttack(); else if (e.KeyCode == Keys.B) ChooseBefriend();
            else if (e.KeyCode == Keys.Home) { selectedAnimalId = -1; selected = CurrentOrderBand.CellId; inspectedBandId = CurrentOrderBand.Id; inspectorPage = 1; page = 0; map.Focus(game.World.Cells[selected]); ShowMapSelection(); }
            else if (e.KeyCode == Keys.G) { HideMapHover(); map.Grid = !map.Grid; Invalidate(); }
            else if (e.Control && e.KeyCode == Keys.S) SaveStory();
        }
        private string StoryDirectory
        {
            get
            {
                DirectoryInfo parent = Directory.GetParent(Application.StartupPath);
                return parent != null && String.Equals(parent.Name, "build", StringComparison.OrdinalIgnoreCase) ? parent.FullName : Application.StartupPath;
            }
        }
        private void SaveStory()
        {
            ClearMapTransient();
            StopAutoplay("Autoplay paused. You are in control.");
            using (SaveFileDialog dialog = new SaveFileDialog { Filter = "Clio story|*.clio", FileName = "First hearths.clio", InitialDirectory = StoryDirectory })
            {
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                try { WriteStory(dialog.FileName); status = "Story saved."; } catch (Exception ex) { status = "Could not save: " + ex.Message; }
            }
            Invalidate();
        }
        internal void WriteStory(string path)
        {
            if (commands.Count > MaximumCommands) throw new InvalidDataException("This prototype supports saving up to 20,000 commands per story.");
            // The header records the founding settings; logged upgrade commands preserve the outcomes of earlier saves.
            List<string> lines = StoryHeader.Write(starting, SerializeStoryModeSave());
            lines.AddRange(commands); string contents = String.Join(Environment.NewLine, lines) + Environment.NewLine;
            if (Encoding.UTF8.GetByteCount(contents) + 3 > 2000000) throw new InvalidDataException("Story exceeds this prototype's two-megabyte save limit.");
            string temporary = Path.GetFullPath(path) + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                File.WriteAllText(temporary, contents, Encoding.UTF8);
                if (File.Exists(path)) ReplaceStoryFile(temporary, path); else File.Move(temporary, path);
            }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
        }
        private static void ReplaceStoryFile(string temporary, string path)
        {
            // Only retry Windows failures that leave both original filenames intact.
            // Never delete a previous save to work around a failed atomic replacement.
            int[] delays = { 35, 80, 160 };
            for (int attempt = 0; ; attempt++)
            {
                try { File.Replace(temporary, path, null, true); return; }
                catch (IOException error)
                {
                    int code = System.Runtime.InteropServices.Marshal.GetHRForException(error) & 65535;
                    if (attempt >= delays.Length || (code != 32 && code != 33 && code != 1175) || !File.Exists(temporary) || !File.Exists(path)) throw;
                    System.Threading.Thread.Sleep(delays[attempt]);
                }
            }
        }
        private void LoadStory()
        {
            ClearMapTransient();
            StopAutoplay("Autoplay paused. You are in control.");
            using (OpenFileDialog dialog = new OpenFileDialog { Filter = "Clio story|*.clio", InitialDirectory = StoryDirectory })
            {
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                try
                {
                    ReadStory(dialog.FileName);
                    // Name the remembered land only after replaying the old rules.
                    if (!game.CulturalPlaceNames && commands.Count < MaximumCommands) Command("enable-place-names");
                    // Preserve every previous outcome before introducing a reserve
                    // and nearby sources for the surviving households.
                    if (!game.SaltEnabled && !game.IsOver && commands.Count < MaximumCommands) Command("enable-salt");
                    if (!game.TribesEnabled && !game.IsOver && commands.Count + (game.Rules == SimulationRules.Classic ? 2 : 1) <= MaximumCommands)
                    {
                        if (game.Rules == SimulationRules.Classic) Command("enable-encounters");
                        Command("enable-tribes");
                    }
                    if (!game.TerrainTravelEnabled && !game.IsOver && commands.Count < MaximumCommands) Command("enable-terrain");
                    if (!game.BandPersonalitiesEnabled && game.TribesEnabled && !game.IsOver && commands.Count < MaximumCommands) Command("enable-personalities");
                    if (!game.GatheringsEnabled && game.TribesEnabled && game.CulturalPlaceNames && !game.IsOver && commands.Count < MaximumCommands) Command("enable-gatherings");
                    EnableWoodForStory(); EnableLivestockForStory(); EnableBattlesForStory();
                    status = "Story restored. " + Timeline.Label(game, game.Turn) + (semiautomatic ? ". Semiautomatic is paused; Continue story when ready." : ". Manual control.");
                }
                catch (Exception ex) { status = "Could not load story: " + ex.Message; }
            }
            Invalidate();
        }
        internal void ReadStory(string path)
        {
            StopAutoplay(null);
            if (new FileInfo(path).Length > 2000000) throw new InvalidDataException("Story file is too large for this prototype.");
            string[] lines = File.ReadAllLines(path, Encoding.UTF8);
            StoryHeader header = StoryHeader.Read(lines);
            if (lines.Length - header.CommandStart > MaximumCommands) throw new InvalidDataException("Story exceeds prototype limits.");
            StoryModeSaveState stagedMode = header.Decisions == null ? new StoryModeSaveState() : ParseStoryModeSave(header.Decisions);
            Game restored = new Game(header.Settings);
            StoryJournal restoredJournal = new StoryJournal(restored);
            foreach (string command in lines.Skip(header.CommandStart)) { JournalSnapshot before = StoryJournal.Capture(restored); string result = Execute(restored, command); restoredJournal.Record(restored, before, command, result); }
            ValidateStoryModeSave(stagedMode, restored.Turn);
            game = restored; journal = restoredJournal; starting = header.Settings;
            ResetStoryMode(); ApplyStoryModeSave(stagedMode);
            Band restoredLeader = game.TribeLeaderBand ?? game.Player;
            commands.Clear(); commands.AddRange(lines.Skip(header.CommandStart)); selected = restoredLeader.CellId; map.Focus(game.World.Cells[selected]); map.Fog = true; chronicleOffset = 0; languagePage = 0;
            inspectorPage = 0; inspectedBandId = restoredLeader.Id; selectedAnimalId = -1; encounterChoice = false; page = 0; ClearNotices(true);
            culturePage = 0; ResetEconomyPage(); ResetUnitsPage(); ClearMapTransient(); ArmMapCommandBand(restoredLeader.Id);
            ClearReunionRoute(); ResetDiplomacy(); ResetGatheringUi(); ResetOpeningAnnouncement(); ResetEnding(); ReportEndingIfNeeded();
            ResetAdvisers();
            initialGuidanceOffered = true;
            status = "Story restored. " + Timeline.Label(game, game.Turn) + " · " + Timeline.ConditionLabel(game) + (semiautomatic ? ". Semiautomatic is paused; Continue story when ready." : ". Manual control.");
        }
        // Every story founded in the desktop app uses the complete current rule set.
        private static GameSettings NewStorySettings(int seed, LanguageStyle style, Ancestry ancestry, bool fourBands, string name, CultureTemplateId culture, HistoryPace pace)
        {
            return new GameSettings(seed, style, ancestry, fourBands, name)
            {
                FoundingCulture = culture, Pace = pace, Rules = SimulationRules.MobileUnits, CulturalPlaceNames = true, SaltEnabled = true,
                TribesEnabled = true, TerrainTravelEnabled = true, BandPersonalitiesEnabled = true, GatheringsEnabled = true
            };
        }
        private NewStoryForm CreateNewStoryDialog()
        { return new NewStoryForm(starting.Seed, starting.Style, starting.Ancestry, starting.FourBands, CultureTemplateId.Zhol, starting.Pace); }

        private void NewStory()
        {
            ClearMapTransient();
            StopAutoplay("Autoplay paused. You are in control.");
            using (NewStoryForm dialog = CreateNewStoryDialog())
            {
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                starting = NewStorySettings(dialog.WorldSeed, dialog.SoundStyle, dialog.FoundingAncestry, dialog.FourBands, dialog.BandName, dialog.Culture, dialog.Pace);
                game = new Game(starting);
                journal = new StoryJournal(game); ClearNotices(false); inspectorPage = 0; inspectedBandId = 0; selectedAnimalId = -1; encounterChoice = false;
                ClearReunionRoute(); ResetDiplomacy(); ResetGatheringUi(); ResetOpeningAnnouncement(); ResetEnding(); ResetStoryMode();
                commands.Clear(); selected = game.Player.CellId; chronicleOffset = 0; languagePage = 0;
                EnableWoodForStory(); EnableLivestockForStory(); EnableBattlesForStory();
                culturePage = 0; ResetEconomyPage(); ResetUnitsPage(); ClearMapTransient(); ArmMapCommandBand(game.Player.Id);
                page = 0; map.Focus(game.World.Cells[selected]); map.Zoom = MapRenderer.RegionalZoom; map.Fog = true;
                ResetAdvisers();
                status = "Fifty people. An open world. A new story begins."; ShowInitialGuidance(); Invalidate();
            }
        }
        internal void Smoke(string directory)
        {
            suppressNotices = true;
            map.AnimateUnits = false;
            VisualChecks.Run();
            Directory.CreateDirectory(directory);
            if (!map.Fog) throw new Exception("New stories must begin with known land only.");
            Render(Path.Combine(directory, "clio-world.png"));
            Render(Path.Combine(directory, "clio-known-start.png"));
            int picked = 0;
            foreach (ProjectedCell cell in map.Visible.Where(p => p.Depth > 0.25 && map.Bounds.Contains(p.Center)))
            { if (map.Pick(cell.Center.X, cell.Center.Y) != cell.Cell.Id) throw new Exception("Cell picking mismatch."); picked++; }
            if (picked < 20) throw new Exception("Insufficient visible cells in picking smoke check.");
            double originalZoom = map.Zoom;
            map.Zoom = 8.6; Render(Path.Combine(directory, "clio-terrain-detail.png")); map.Zoom = originalZoom;
            page = 1; economyPage = 0; Render(Path.Combine(directory, "clio-economy.png"));
            string[] economyViews = { "overview", "demographics", "resources", "trade", "finance" };
            for (int view = 0; view < economyViews.Length; view++) { economyPage = view; Render(Path.Combine(directory, "clio-economy-" + economyViews[view] + ".png")); }
            economyPage = 0;
            page = 2; culturePage = 0; Render(Path.Combine(directory, "clio-culture.png"));
            culturePage = 1; Render(Path.Combine(directory, "clio-languages.png")); culturePage = 0;
            page = 3; Render(Path.Combine(directory, "clio-units.png"));
            page = 5; Render(Path.Combine(directory, "clio-diplomacy.png"));
            page = 0; map.Fog = false; map.Zoom = 0.93;
            Render(Path.Combine(directory, "clio-globe.png"));
            Render(Path.Combine(directory, "clio-atlas.png"));
            map.Zoom = originalZoom; map.Fog = true;
            // This art-review scene has its own game. Illustrative positions never enter commands or a saved story.
            Game realGame = game; StoryJournal realJournal = journal; int realSelected = selected; string realStatus = status;
            try
            {
                GameSettings fixture = starting.Clone(); fixture.FourBands = true; fixture.BandPersonalitiesEnabled = false; fixture.GatheringsEnabled = false;
                game = new Game(fixture); journal = new StoryJournal(game);
                selected = game.Player.CellId;
                List<int> occupied = new List<int> { selected };
                foreach (Band band in game.Bands.Skip(1))
                {
                    Cell site = game.World.Cells.Where(c => c.IsLand && game.Explored.Contains(c.Id) && !occupied.Contains(c.Id))
                        .OrderByDescending(c => occupied.Min(id => 1 - Vec3.Dot(c.Center, game.World.Cells[id].Center))).First();
                    band.CellId = site.Id; band.Settled = band.Id % 2 == 1; band.HomeCell = band.Settled ? site.Id : -1;
                    occupied.Add(site.Id);
                }
                map.Focus(game.World.Cells[selected]); map.Layer = 4;
                presentationCaption = "ART REVIEW FIXTURE  /  FOUR POLITIES IN ILLUSTRATIVE POSITIONS";
                status = "Presentation fixture only. Separate simulation; illustrative band locations are never saved.";
                Render(Path.Combine(directory, "clio-four-polity-fixture.png"));
            }
            finally
            {
                game = realGame; journal = realJournal; selected = realSelected; status = realStatus; presentationCaption = "";
                map.Layer = 0; map.Focus(game.World.Cells[selected]);
            }
            Command("forage"); Command("camp"); Command("end"); Command("forage");
            int turn = game.Turn, pop = game.Player.Population; double food = game.Player.Food;
            string save = Path.Combine(directory, "smoke-story.clio"); WriteStory(save); Command("end"); map.Fog = false; ReadStory(save);
            if (game.Turn != turn || game.Player.Population != pop || game.Player.Food != food) throw new Exception("Save/replay state mismatch.");
            if (!map.Fog) throw new Exception("Restored stories must open on known land.");
            WriteStory(save);
            string bad = Path.Combine(directory, "invalid-smoke.clio"); File.WriteAllText(bad, "CLIO-STORY-999");
            bool rejected = false; try { ReadStory(bad); } catch (InvalidDataException) { rejected = true; } finally { File.Delete(bad); }
            if (!rejected || game.Turn != turn || game.Player.Food != food) throw new Exception("Invalid load changed the story.");
            page = 4; Render(Path.Combine(directory, "clio-chronicle.png"));
            chronicleEvents = false; Render(Path.Combine(directory, "clio-full-chronicle.png")); chronicleEvents = true;
            page = 0; ShowIntroduction(); Render(Path.Combine(directory, "clio-event.png")); DismissNotice(false); suppressNotices = false;
            Console.WriteLine("PASS: six pages, events, known-land start, atlas, terrain detail and isolated polity art fixture rendered; map picking, save/replay, save replacement and invalid load checked.");
        }
    }
}
