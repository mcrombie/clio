using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using Clio.Simulation;

namespace Clio.Desktop
{
    public sealed partial class GameForm
    {
        // A first-page presentation, never a command or a saved game rule.
        // ShowInitialGuidance owns the once-per-new-story gate; replay only resets it.
        private bool openingAnnouncement;

        private void ShowOpeningAnnouncement()
        {
            if (openingAnnouncement || game == null || game.IsOver) return;
            StopAutoplay(null); ClearMapTransient();
            dragging = false; Capture = false; map.IsNavigating = false; settleCamera.Stop();
            openingAnnouncement = true; buttons.Clear(); Invalidate();
        }

        private void ResetOpeningAnnouncement()
        { openingAnnouncement = false; buttons.Clear(); }

        private void CloseOpeningAnnouncement(bool meetCouncil)
        {
            if (!openingAnnouncement) return;
            openingAnnouncement = false; buttons.Clear(); HideMapHover();
            status = "Your story begins. Gather food, find salt, and choose your first paths.";
            if (meetCouncil)
            {
                // The opening reading is the only acknowledgement here. Supply
                // warnings remain unread when the player meets the four voices.
                OpenAdvisers("guidance:orientation"); MeetCouncil();
            }
            Invalidate();
        }

        private string OpeningNarrative()
        {
            Band band = game.Player;
            return "Your people speak " + game.Languages[band.LanguageId].Name + " and begin at " + game.Place(band.CellId) +
                ". Gather supplies, explore, and keep your bands connected.";
        }

        private void DrawOpeningAnnouncement(Graphics g)
        {
            if (!openingAnnouncement || game == null || game.IsOver) return;
            using (Brush veil = new SolidBrush(Art.PaperMode ? Color.FromArgb(110, MapPaper.MutedInk) : Color.FromArgb(210, 5, 12, 16))) g.FillRectangle(veil, 0, 0, 1600, 960);
            buttons.Clear();
            RectangleF folio = new RectangleF(216, 94, 1168, 774);
            Color paper = Color.FromArgb(224, 213, 185), ink = Color.FromArgb(42, 49, 44), secondary = Color.FromArgb(80, 82, 66);
            Color rule = Color.FromArgb(148, 125, 79);
            Art.Fill(g, Color.FromArgb(70, 0, 0, 0), folio.X + 8, folio.Y + 9, folio.Width, folio.Height);
            using (LinearGradientBrush parchment = new LinearGradientBrush(folio, Color.FromArgb(239, 228, 202), paper, 90))
                g.FillRectangle(parchment, folio);
            Art.Grain(g, folio);
            using (Pen edge = new Pen(rule, 1))
            {
                g.DrawRectangle(edge, folio.X + .5f, folio.Y + .5f, folio.Width - 1, folio.Height - 1);
                g.DrawRectangle(edge, folio.X + 10.5f, folio.Y + 10.5f, folio.Width - 21, folio.Height - 21);
            }

            Art.Icon(g, "history", 263, 125, 35, rule);
            Typography.Label(g, "The first hearths / Turn " + game.Turn, new RectangleF(313, 128, 960, 25), 13, secondary, 1.4f);
            Typography.Line(g, "The story of " + game.Player.Name, new RectangleF(258, 161, 1084, 62), 46, ink, TypeRole.Display, true);
            Typography.Draw(g, OpeningNarrative(), new RectangleF(261, 231, 1078, 58), 20, secondary, TypeRole.Body);
            Art.Line(g, rule, .9f, 263, 304, 1337, 304);

            Band band = game.Player;
            double need = game.Upkeep(band) + BandEconomy.DomesticEffects(game, band).AnimalCare;
            DrawOpeningMetric(g, 263, "Your people", band.Population.ToString("N0"), "One wandering household", ink, secondary);
            DrawOpeningMetric(g, 627, "Food reserves", Math.Floor(band.Food).ToString("N0"),
                need <= 0 ? "No food needed at present" : need.ToString("0.#") + (game.WoodEnabled && WoodEconomy.HasFuel(game, band) ? " needed per turn with fire" : " needed at each turn's end"), ink, secondary);
            DrawOpeningMetric(g, 991, game.SaltEnabled ? "Salt reserve" : "Cohesion", game.SaltEnabled ? band.Salt.ToString("0.#") : (band.Cohesion * 100).ToString("0") + "%",
                game.SaltEnabled ? SaltEconomy.ReserveTurns(band).ToString("0.0") + " turns at this population" : "Your household's common purpose", ink, secondary);
            Art.Line(g, Color.FromArgb(95, rule), .7f, 602, 328, 602, 410);
            Art.Line(g, Color.FromArgb(95, rule), .7f, 966, 328, 966, 410);

            DrawOpeningPurpose(g, 263, "leaf", "Sustain your people", "Gather food where you stand. Gather salt at a source. Your people consume both at each turn's end.", ink, secondary);
            DrawOpeningPurpose(g, 627, "move", "Explore the map", "Right-click nearby land to move your selected band. Two actions per band; difficult ground costs more.", ink, secondary);
            DrawOpeningPurpose(g, 991, "heart", "Grow your tribe", "Form daughter bands. Reunite with the leader to keep contact; long separation can lead to independence.", ink, secondary);

            RectangleF council = new RectangleF(239, 579, 1122, 189);
            Art.Panel(g, council, Color.FromArgb(26, 39, 41), false);
            Typography.Label(g, "Your advisers", new RectangleF(263, 589, 650, 23), 12, Art.Gold, .8f);
            for (int i = 0; i < CouncilPerspectives.All.Count; i++)
            {
                CounsellorProfile voice = CouncilPerspectives.All[i]; float x = 263 + i * 274;
                AdviserPortraits.Draw(g, new RectangleF(x, 629, 70, 70), voice.Id, Art.Gold);
                Typography.Line(g, voice.Name, new RectangleF(x + 82, 630, 177, 32), 21, Art.Ink, TypeRole.Heading, true);
                Typography.Draw(g, voice.Title, new RectangleF(x + 82, 665, 177, 37), 15, Art.Muted, TypeRole.Annotation);
            }
            Typography.Line(g, "They will explain developments and warn you of danger. Their priorities differ; you decide whom to follow.",
                new RectangleF(263, 718, 1074, 31), 19, Art.Ink, TypeRole.Annotation, false, StringAlignment.Center);

            Typography.Line(g, "Reading this page spends no turn or action.", new RectangleF(263, 791, 483, 36), 18, secondary, TypeRole.Annotation);
            Button(g, "Meet advisers", 765, 789, 234, 45, delegate { CloseOpeningAnnouncement(true); }, false, false);
            Button(g, "Begin our story", 1015, 789, 322, 45, delegate { CloseOpeningAnnouncement(false); }, true, false);
            Typography.Line(g, "Enter or Esc to begin", new RectangleF(1015, 839, 322, 18), 13, secondary, TypeRole.Utility, false, StringAlignment.Center);
        }

        private static void DrawOpeningMetric(Graphics g, float x, string label, string value, string detail, Color ink, Color secondary)
        {
            Typography.Label(g, label, new RectangleF(x, 320, 330, 23), 12, secondary, .8f);
            Typography.Line(g, value, new RectangleF(x, 344, 330, 44), 35, ink, TypeRole.Number);
            Typography.Line(g, detail, new RectangleF(x, 389, 330, 26), 17, secondary, TypeRole.Annotation);
        }

        private static void DrawOpeningPurpose(Graphics g, float x, string icon, string title, string body, Color ink, Color secondary)
        {
            Art.Icon(g, icon, x, 442, 25, secondary);
            Typography.Line(g, title, new RectangleF(x + 37, 434, 293, 38), 25, ink, TypeRole.Heading);
            Typography.Draw(g, body, new RectangleF(x, 481, 326, 85), 19, secondary, TypeRole.Body);
        }
    }
}
