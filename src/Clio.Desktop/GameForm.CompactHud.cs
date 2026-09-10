using System;
using System.Drawing;
using System.Linq;
using Clio.Simulation;

namespace Clio.Desktop
{
    public sealed partial class GameForm
    {
        // One shared strip on every page. Values cover the tribe; shortages are
        // checked per household so a well-stocked leader cannot hide hungry kin.
        private void DrawCompactStatus(Graphics g)
        {
            DrawFloatingMapPanel(g, new RectangleF(16, 102, 1568, 46));
            Band[] bands = game.ControlledBands.Where(b => b.Population > 0).ToArray();
            Band leader = game.TribeLeaderBand ?? game.Player;
            RectangleF identity = new RectangleF(27, 107, 260, 36);
            if (identity.Contains(hoverPoint)) Art.Fill(g, Color.FromArgb(34, 46, 49), identity.X, identity.Y, identity.Width, identity.Height);
            IdentityArt.DrawEmblem(g, game.TribeOf(leader.Id), new RectangleF(32, 109, 30, 30), false);
            Typography.Line(g, leader.Name, new RectangleF(72, 108, 199, 34), 25, Art.Ink, TypeRole.Heading, true);
            buttons.Add(new UiButton(identity, delegate { OpenEconomy(0); }) { Tip = "Your people / " + bands.Length + " bands\nThe symbols summarize your tribe. Supplies are carried separately by each band. Hover for an explanation; click for its full record." });
            int population = bands.Sum(b => b.Population);
            HistoryPoint previous = journal.Timeline.LastOrDefault(t => t.Turn < game.Turn);
            int change = previous == null ? 0 : population - previous.Population;
            HudMetric(g, 298, "people", population.ToString("N0"), change == 0 ? "" : change.ToString("+0;-0"), change < 0 ? BandPanelWarning : LedgerGreen,
                "People / " + population + " in your tribe\n" + (previous == null ? "Your starting population." : "Net change since turn " + previous.Turn + ": " + change.ToString("+0;-0;0") + ". Includes births, deaths and bands leaving your tribe.") + "\nClick for Demographics.", delegate { OpenEconomy(1); });
            Band foodBand = bands.OrderBy(HudFoodTurns).FirstOrDefault();
            int hungry = bands.Count(b => HudFoodTurns(b) < 1);
            HudMetric(g, 444, "leaf", Math.Floor(bands.Sum(b => b.Food)).ToString("N0"), hungry > 0 ? "!" : "", BandPanelWarning,
                "Food / tribe's total reserves\n" + (foodBand == null ? "No living household." : "Lowest reserve: " + foodBand.Name + ", " + HudFoodTurns(foodBand).ToString("0.0") + " turns of people's needs and animal care, before milk or camp income.") +
                "\nGather at food markers. Each band carries its own food. Click to inspect the least supplied band.", delegate { HudSupply(foodBand, 0); });
            Band saltBand = bands.OrderBy(SaltEconomy.ReserveTurns).FirstOrDefault();
            int lowSalt = game.SaltEnabled ? bands.Count(b => SaltEconomy.ReserveTurns(b) < 2) : 0;
            HudMetric(g, 590, "salt", game.SaltEnabled ? Math.Floor(bands.Sum(b => b.Salt)).ToString("N0") : "--", lowSalt > 0 ? "!" : "", BandPanelWarning,
                "Salt / tribe's total reserves\n" + (saltBand == null || !game.SaltEnabled ? "Salt needs are not currently tracked." : "Lowest reserve: " + saltBand.Name + ", " + SaltEconomy.ReserveTurns(saltBand).ToString("0.0") + " turns. " + lowSalt + " bands have less than two turns.") +
                "\nFind crystal markers, then Gather salt. Click for sources and shortage effects.", delegate { HudSupply(saltBand, 1); });
            Band woodBand = bands.OrderBy(WoodEconomy.ReserveTurns).FirstOrDefault();
            int unheated = game.WoodEnabled ? bands.Count(b => !WoodEconomy.HasFuel(game, b)) : 0;
            HudMetric(g, 736, "wood", game.WoodEnabled ? Math.Floor(bands.Sum(b => b.Wood)).ToString("N0") : "--", unheated > 0 ? "!" : "", Art.Gold,
                "Wood / tribe's total reserves\n" + (woodBand == null || !game.WoodEnabled ? "No fuel is currently tracked." : "Lowest reserve: " + woodBand.Name + ", " + WoodEconomy.ReserveTurns(woodBand).ToString("0.0") + " turns of fuel. " + unheated + " bands lack a full fire.") +
                "\nWood helps fires and camps. A shortage removes the bonus; it is not a separate survival penalty. Click for gathering and use.", delegate { HudSupply(woodBand, 2); });
            int drifting = game.TribesEnabled ? bands.Count(b => game.TribeStatus(b.Id) != null && game.TribeStatus(b.Id).Drifting) : 0;
            HudMetric(g, 882, "cooperate", drifting > 0 ? drifting + " apart" : "Stable", drifting > 0 ? "!" : "", BandPanelWarning,
                "Cooperation / tribal contact\n" + drifting + " bands are drifting from the tribe. Reunite on the leader's hex to renew contact. Stable means no band is currently drifting; it does not mean everyone occupies one place.\nClick Units for household personalities and reunion deadlines. Diplomacy holds relationships with other peoples.", OpenHudUnits);
            int enemies = game.Bands.Count(b => b.Population > 0 && !game.CanControlBand(b.Id) && game.Explored.Contains(b.CellId) && bands.Any(own => EncounterRules.BandsHostile(game, own.Id, b.Id)));
            HudMetric(g, 1028, "conflict", enemies == 0 ? "--" : enemies.ToString(), enemies > 0 ? "!" : "", BandPanelWarning,
                "Conflict / " + enemies + " observed hostile bands\nOnly known living groups are counted. Wildlife can still be dangerous. Red encounter notices mark losses; open a report to read casualties and the actual outcome.\nClick for encounter history.", OpenHudHistory);
            int ready = bands.Count(b => game.ActionsFor(b.Id) > 0);
            HudMetric(g, 1174, "branch", ready + "/" + bands.Length, "", Art.Gold,
                "Bands ready / " + ready + " of " + bands.Length + " have actions left\nEach band receives two actions per turn. Click for the next ready band [N].", NextReadyBand);
            int advice = game.IsOver ? 0 : adviserReports.Count(AdviserUnread);
            HudCue(g, new RectangleF(1327, 108, 116, 34), "adviser", advice, delegate { OpenAdvisers(null); }, "Advisers [C]\n" + advice + " unread items. Read explanations and competing recommendations. Frequency is in Watch settings.");
            HudCue(g, new RectangleF(1451, 108, 120, 34), "history", UnreadNoticeCount(), OpenHudHistory, "History and events\nRead what changed, why it happened and what you can do next.");
        }

        private double HudFoodTurns(Band band)
        {
            double need = game.Upkeep(band) + BandEconomy.DomesticEffects(game, band).AnimalCare;
            return need <= 0 ? 0 : band.Food / need;
        }
        private void HudSupply(Band band, int resource)
        {
            if (band != null && game.CanControlBand(band.Id)) ArmMapCommandBand(band.Id);
            if (resource == 1) OpenSaltEconomy(); else if (resource == 2) OpenWoodEconomy(); else OpenEconomy(0);
        }
        private void OpenHudUnits()
        { ClearMapTransient(); page = 3; unitsFilter = 0; buttons.Clear(); Invalidate(); }
        private void OpenHudHistory()
        { ClearMapTransient(); page = 4; storyChoicesVisible = false; chronicleEvents = true; noticeOffset = 0; buttons.Clear(); Invalidate(); }
        private void HudMetric(Graphics g, float x, string icon, string value, string change, Color accent, string tip, Action click)
        {
            RectangleF box = new RectangleF(x, 108, 138, 34);
            if (box.Contains(hoverPoint)) Art.Fill(g, Color.FromArgb(34, 46, 49), box.X, box.Y, box.Width, box.Height);
            RectangleF glyph = new RectangleF(x + 4, 113, 23, 23);
            if (icon == "salt") DrawSaltGlyph(g, glyph, Art.Gold);
            else if (icon == "wood") DrawWoodGlyph(g, glyph, Art.Gold);
            else Art.Icon(g, icon, glyph.X, glyph.Y, glyph.Width, Art.Gold);
            Typography.Line(g, value, new RectangleF(x + 35, 108, change.Length == 0 ? 94 : 68, 34), 22, change == "!" ? accent : Art.Ink, TypeRole.Number, true);
            if (change.Length > 0) Typography.Line(g, change, new RectangleF(x + 103, 111, 31, 28), 14, accent, TypeRole.Number, true, StringAlignment.Far);
            buttons.Add(new UiButton(box, click) { Tip = tip });
        }
        private void HudCue(Graphics g, RectangleF box, string icon, int count, Action click, string tip)
        {
            bool hover = box.Contains(hoverPoint);
            Art.Fill(g, hover ? Color.FromArgb(43, 52, 51) : Color.FromArgb(25, 37, 40), box.X, box.Y, box.Width, box.Height);
            Art.Icon(g, icon, box.X + 12, box.Y + 6, 22, count > 0 ? Art.Gold : Art.Muted);
            Typography.Line(g, count > 0 ? count.ToString("N0") : "--", new RectangleF(box.X + 45, box.Y, box.Width - 56, box.Height), 19, count > 0 ? Art.Gold : Art.Muted, TypeRole.Number, true);
            buttons.Add(new UiButton(box, click) { Tip = tip });
        }
    }
}
