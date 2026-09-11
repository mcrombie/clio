using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using Clio.Simulation;

namespace Clio.Desktop
{
    public sealed partial class GameForm
    {
        private bool campaignSystemMenuOpen;
        private bool CampaignSystemMenuOpen { get { return page == 0 && campaignSystemMenuOpen; } }
        private RectangleF CampaignSystemMenuBounds { get { return new RectangleF(14, 68, 286, 286); } }

        private void CloseCampaignSystemMenu() { campaignSystemMenuOpen = false; }

        private bool CampaignHeaderContains(PointF point)
        {
            return page == 0 && (new RectangleF(14, 12, 286, 46).Contains(point) ||
                new RectangleF(418, 12, 686, 46).Contains(point) ||
                new RectangleF(1218, 12, 368, 46).Contains(point) ||
                new RectangleF(1416, 68, 80, 38).Contains(point) ||
                new RectangleF(1506, 68, 80, 38).Contains(point) ||
                CampaignSystemMenuOpen && CampaignSystemMenuBounds.Contains(point));
        }

        private void ToggleCampaignSystemMenu()
        {
            if (BlockingSheet || page != 0) return;
            bool open = !campaignSystemMenuOpen;
            ClearMapTransient(); campaignSystemMenuOpen = open;
            dragging = false; Capture = false; map.IsNavigating = false; settleCamera.Stop();
            buttons.Clear(); Invalidate();
        }

        private void DrawCampaignHeader(Graphics g)
        {
            Band[] bands = game.ControlledBands.Where(b => b.Population > 0).ToArray();
            Band leader = game.TribeLeaderBand ?? game.Player;
            Art.Panel(g, new RectangleF(14, 12, 286, 46), Color.FromArgb(24, 34, 37), false);
            CampaignRoundButton(g, "menu", new RectangleF(18, 14, 42, 42), ToggleCampaignSystemMenu,
                campaignSystemMenuOpen, "Clio menu\nSave, load, start a new story or change fullscreen. Escape closes the menu.");
            RectangleF identity = new RectangleF(68, 14, 224, 42);
            if (identity.Contains(hoverPoint)) Art.Fill(g, Color.FromArgb(35, 48, 49), identity.X, identity.Y, identity.Width, identity.Height);
            IdentityArt.DrawEmblem(g, game.TribeOf(leader.Id), new RectangleF(75, 20, 30, 30), false);
            Typography.Line(g, leader.Name, new RectangleF(114, 17, 173, 35), 23, Art.Ink, TypeRole.Heading, true);
            buttons.Add(new UiButton(identity, delegate { OpenEconomy(0); })
            { Tip = leader.Name + " / " + bands.Length + " bands\nYour tribe's leader and shared identity. Supplies belong to individual households. Click for the economic overview." });

            Art.Panel(g, new RectangleF(418, 12, 686, 46), Color.FromArgb(24, 34, 37), false);
            for (int divider = 1; divider < 4; divider++)
                Art.Line(g, Color.FromArgb(85, Art.Muted), 1, 418 + divider * 171.5f, 23, 418 + divider * 171.5f, 47);

            int population = bands.Sum(b => b.Population);
            HistoryPoint previous = journal.Timeline.LastOrDefault(t => t.Turn < game.Turn);
            int change = previous == null ? 0 : population - previous.Population;
            CampaignMetric(g, 418, "people", "People", population.ToString("N0"), false,
                "People / " + population.ToString("N0") + " in your tribe\n" +
                (previous == null ? "Your starting population." : "Net change since turn " + previous.Turn + ": " + change.ToString("+0;-0;0") + ". Includes births, deaths and bands leaving the tribe.") +
                "\nClick for Demographics.", delegate { OpenEconomy(1); });

            Band foodBand = bands.OrderBy(HudFoodTurns).FirstOrDefault();
            int hungry = bands.Count(b => HudFoodTurns(b) < 1);
            CampaignMetric(g, 589.5f, "leaf", "Food", Math.Floor(bands.Sum(b => b.Food)).ToString("N0"), hungry > 0,
                "Food / " + bands.Sum(b => b.Food).ToString("0.0") + " total reserves\n" +
                (foodBand == null ? "No living household remains." : "Lowest reserve: " + foodBand.Name + ", " + HudFoodTurns(foodBand).ToString("0.0") + " turns of people's needs and animal care, before milk or camp income. " + hungry + " bands hold less than one turn.") +
                "\nGather at food markers. Reserves are carried separately; one well-supplied band cannot cover another automatically. Click to inspect the least supplied band.", delegate { HudSupply(foodBand, 0); });

            Band saltBand = bands.OrderBy(SaltEconomy.ReserveTurns).FirstOrDefault();
            int lowSalt = game.SaltEnabled ? bands.Count(b => SaltEconomy.ReserveTurns(b) < 2) : 0;
            CampaignMetric(g, 761, "salt", "Salt", game.SaltEnabled ? Math.Floor(bands.Sum(b => b.Salt)).ToString("N0") : "--", lowSalt > 0,
                "Salt / " + (game.SaltEnabled ? bands.Sum(b => b.Salt).ToString("0.0") + " total reserves" : "not tracked in this story") + "\n" +
                (saltBand == null ? "No living household remains." : !game.SaltEnabled ? "Open the salt ledger to read about salt needs." : "Lowest reserve: " + saltBand.Name + ", " + SaltEconomy.ReserveTurns(saltBand).ToString("0.0") + " turns. " + lowSalt + " bands have less than two turns.") +
                "\nFind crystal markers, then Gather salt. Click for the least supplied band's sources and shortage effects.", delegate { HudSupply(saltBand, 1); });

            Band woodBand = bands.OrderBy(WoodEconomy.ReserveTurns).FirstOrDefault();
            int unheated = game.WoodEnabled ? bands.Count(b => !WoodEconomy.HasFuel(game, b)) : 0;
            CampaignMetric(g, 932.5f, "wood", "Wood", game.WoodEnabled ? Math.Floor(bands.Sum(b => b.Wood)).ToString("N0") : "--", unheated > 0,
                "Wood / " + (game.WoodEnabled ? bands.Sum(b => b.Wood).ToString("0.0") + " total reserves" : "not tracked in this story") + "\n" +
                (woodBand == null ? "No living household remains." : !game.WoodEnabled ? "Open the wood ledger to read about fires and camps." : "Lowest reserve: " + woodBand.Name + ", " + WoodEconomy.ReserveTurns(woodBand).ToString("0.0") + " turns of fuel. " + unheated + " bands lack a full fire.") +
                "\nWood supplies fires and camp construction. Missing fuel removes fire benefits; it is not a separate survival penalty. Click for gathering and use.", delegate { HudSupply(woodBand, 2); });

            string[] labels = { "Map", "Economy", "Culture", "Units", "Diplomacy", "History" };
            string[] symbols = { "globe", "leaf", "quill", "branch", "cooperate", "history" };
            string[] help = {
                "Explore the land and give orders to your selected band.",
                "Read population, resources, trade and food accounts.",
                "Read your people's practices and language.",
                "Organize bands, inspect their supplies and renew tribal contact.",
                "Read relationships with independent peoples as they emerge.",
                "Read events, adviser reports and your decisions."
            };
            int[] pages = { 0, 1, 2, 3, 5, 4 };
            for (int i = 0; i < pages.Length; i++)
            {
                int target = pages[i];
                CampaignRoundButton(g, symbols[i], new RectangleF(1226 + i * 62, 13, 44, 44), delegate
                {
                    if (target == 1) OpenEconomy(0);
                    else if (target == 3) OpenHudUnits();
                    else if (target == 4) OpenHudHistory();
                    else { ClearMapTransient(); page = target; buttons.Clear(); Invalidate(); }
                }, page == target, labels[i] + "\n" + help[i]);
            }
            int advice = game.IsOver ? 0 : adviserReports.Count(AdviserUnread);
            CampaignCue(g, new RectangleF(1416, 68, 80, 38), "adviser", advice, delegate { OpenAdvisers(null); },
                "Advisers [C]\n" + advice + " unread items. Read explanations and competing recommendations. Adviser frequency is in Watch settings.");
            CampaignCue(g, new RectangleF(1506, 68, 80, 38), "history", UnreadNoticeCount(), OpenHudHistory,
                "Events\n" + UnreadNoticeCount() + " unread events. Open History to read what changed and why.");
        }

        private void CampaignMetric(Graphics g, float x, string icon, string label, string value, bool shortage, string tip, Action click)
        {
            RectangleF bounds = new RectangleF(x + 3, 15, 165.5f, 40);
            if (bounds.Contains(hoverPoint)) Art.Fill(g, Color.FromArgb(36, 49, 49), bounds.X, bounds.Y, bounds.Width, bounds.Height);
            RectangleF glyph = new RectangleF(x + 12, 24, 23, 23);
            if (icon == "salt") DrawSaltGlyph(g, glyph, Art.Gold);
            else if (icon == "wood") DrawWoodGlyph(g, glyph, Art.Gold);
            else Art.Icon(g, icon, glyph.X, glyph.Y, glyph.Width, Art.Gold);
            Typography.Label(g, label, new RectangleF(x + 45, 16, 113, 14), 10, Art.Muted, .4f);
            Typography.Line(g, value, new RectangleF(x + 44, 28, shortage ? 90 : 115, 27), 23,
                shortage ? BandPanelWarning : Art.Ink, TypeRole.Number, true);
            if (shortage)
                Typography.Line(g, "!", new RectangleF(x + 138, 27, 19, 27), 20, BandPanelWarning, TypeRole.Number, true, StringAlignment.Center);
            buttons.Add(new UiButton(bounds, click) { Tip = tip });
        }

        private void CampaignRoundButton(Graphics g, string icon, RectangleF bounds, Action click, bool active, string tip)
        {
            bool hover = bounds.Contains(hoverPoint);
            using (Brush shadow = new SolidBrush(Color.FromArgb(105, 0, 0, 0)))
                g.FillEllipse(shadow, bounds.X + 1, bounds.Y + 3, bounds.Width, bounds.Height);
            using (LinearGradientBrush fill = new LinearGradientBrush(bounds,
                active ? Color.FromArgb(81, 74, 49) : hover ? Color.FromArgb(51, 63, 62) : Color.FromArgb(33, 46, 49),
                Color.FromArgb(17, 28, 32), 90)) g.FillEllipse(fill, bounds);
            using (Pen ring = new Pen(active || hover ? Art.Gold : Color.FromArgb(113, 116, 94), active ? 1.8f : 1))
                g.DrawEllipse(ring, bounds.X + 1, bounds.Y + 1, bounds.Width - 2, bounds.Height - 2);
            Color ink = active || hover ? Art.Ink : Art.Gold;
            if (icon == "menu")
                for (int line = 0; line < 3; line++) Art.Line(g, ink, 1.7f, bounds.X + 12, bounds.Y + 13 + line * 7, bounds.Right - 12, bounds.Y + 13 + line * 7);
            else Art.Icon(g, icon, bounds.X + (bounds.Width - 24) / 2, bounds.Y + (bounds.Height - 24) / 2, 24, ink);
            buttons.Add(new UiButton(bounds, click) { Tip = tip });
        }

        private void CampaignCue(Graphics g, RectangleF bounds, string icon, int count, Action click, string tip)
        {
            Art.Panel(g, bounds, bounds.Contains(hoverPoint) ? Color.FromArgb(39, 50, 49) : Color.FromArgb(24, 34, 37), false);
            Art.Icon(g, icon, bounds.X + 9, bounds.Y + 8, 22, count > 0 ? Art.Gold : Art.Muted);
            Typography.Line(g, count > 0 ? count.ToString("N0") : "--", new RectangleF(bounds.X + 36, bounds.Y + 4, bounds.Width - 43, 30),
                19, count > 0 ? Art.Gold : Art.Muted, TypeRole.Number, true, StringAlignment.Center);
            buttons.Add(new UiButton(bounds, click) { Tip = tip });
        }

        private void DrawCampaignSystemMenu(Graphics g)
        {
            if (!CampaignSystemMenuOpen || BlockingSheet) return;
            RectangleF box = CampaignSystemMenuBounds;
            buttons.RemoveAll(button => button.Bounds.IntersectsWith(box));
            Art.Fill(g, Color.FromArgb(100, 0, 0, 0), box.X + 4, box.Y + 5, box.Width, box.Height);
            Art.Panel(g, box, Color.FromArgb(24, 34, 37), true);
            Typography.Line(g, "Clio", new RectangleF(32, 81, 195, 31), 26, Art.Ink, TypeRole.Heading, true);
            Button(g, "\u00d7", 254, 82, 28, 28, delegate { CloseCampaignSystemMenu(); buttons.Clear(); Invalidate(); }, false, false);
            MapTip("Close the menu. Escape also closes it.");
            Button(g, "Save story", 32, 123, 250, 36, delegate { CloseCampaignSystemMenu(); SaveStory(); }, false, false);
            MapTip("Save the current story and its recorded decisions.");
            Button(g, "Load story", 32, 169, 250, 36, delegate { CloseCampaignSystemMenu(); LoadStory(); }, false, false);
            MapTip("Open a previously saved story.");
            Button(g, "New story", 32, 215, 250, 36, delegate { CloseCampaignSystemMenu(); NewStory(); }, false, false);
            MapTip("Choose the founding settings for a new people.");
            Button(g, fullscreen ? "Return to window [F11]" : "Fullscreen [F11]", 32, 261, 250, 36,
                delegate { CloseCampaignSystemMenu(); ToggleFullscreen(); }, false, false);
            MapTip("Toggle fullscreen. F11 or Alt+Enter works at any time.");
            RectangleF version = new RectangleF(32, 308, 250, 29);
            Typography.Line(g, "Clio / " + BuildIdentity, version, 14, Art.Muted, TypeRole.Utility, false);
            buttons.Add(new UiButton(version, delegate { })
            { Tip = "Running Clio " + BuildIdentity + ". The desktop shortcut opens the latest completed build. An already-open game keeps its current version until you save and reopen." });
        }
    }
}
