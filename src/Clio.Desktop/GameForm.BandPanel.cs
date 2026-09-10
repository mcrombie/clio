using System;
using System.Drawing;
using System.Linq;
using Clio.Simulation;

namespace Clio.Desktop
{
    public sealed partial class GameForm
    {
        private bool bandDetailsOpen;

        private void ToggleBandDetails()
        {
            if (BlockingSheet || game == null || game.IsOver) return;
            bool open = !bandDetailsOpen;
            ClearMapTransient(); page = 0; bandDetailsOpen = open;
            buttons.Clear(); Invalidate();
        }

        private void CloseBandDetails()
        { bandDetailsOpen = false; HideMapHover(); buttons.Clear(); Invalidate(); }

        // The card holds no simulation snapshot. Ownership and remaining actions
        // are read again whenever it is painted, including after succession.
        private RectangleF BandPanelBounds
        { get { return bandDetailsOpen && page == 0 && game != null && !game.IsOver ? game.BandPersonalitiesEnabled ? new RectangleF(32, 528, 356, 222) : new RectangleF(32, 564, 356, 186) : RectangleF.Empty; } }

        private bool BandPanelContains(PointF point)
        { return !BandPanelBounds.IsEmpty && BandPanelBounds.Contains(point); }

        private Band BandPanelActor
        { get { return game == null ? null : game.Bands.FirstOrDefault(b => b.Id == commandedBandId && game.CanControlBand(b.Id)); } }

        private void DrawBandPanel(Graphics g)
        {
            RectangleF box = BandPanelBounds;
            if (box.IsEmpty || BlockingSheet) return;
            Band actor = BandPanelActor;
            buttons.RemoveAll(button => button.Bounds.IntersectsWith(box));
            Art.Fill(g, Color.FromArgb(58, 0, 0, 0), box.X + 4, box.Y + 4, box.Width, box.Height);
            Art.Panel(g, box, Color.FromArgb(22, 34, 37), true);
            Typography.Label(g, actor == null ? "Choose your household" : "Selected band", new RectangleF(box.X + 15, box.Y + 7, 238, 20), 11.5f, Art.Gold, .6f);
            Button(g, "\u00d7", box.Right - 35, box.Y + 9, 26, 26, CloseBandDetails, false, false);
            MapTip("Close band details. Select the band badge to reopen them.");
            if (actor == null)
            {
                Typography.Line(g, "No band selected", new RectangleF(box.X + 15, box.Y + 27, 324, 35), 27, Art.Ink, TypeRole.Heading, true);
                Typography.Draw(g, "Select one of your banners to give orders. Each household has its own supplies and actions.", new RectangleF(box.X + 15, box.Y + 70, 325, 66), 17, Art.Muted, TypeRole.Annotation);
                DrawBandPanelButtons(g, box, false);
                return;
            }

            IdentityArt.DrawEmblem(g, game.TribeOf(actor.Id), new RectangleF(box.X + 14, box.Y + 30, 30, 30), false);
            Typography.Line(g, actor.Name, new RectangleF(box.X + 54, box.Y + 25, 175, 39), 27, Art.Ink, TypeRole.Heading, true);
            int actions = game.ActionsFor(actor.Id);
            Typography.Line(g, actions + " / 2", new RectangleF(box.Right - 113, box.Y + 12, 66, 33), 25, actions > 0 ? Art.Gold : Art.Muted, TypeRole.Number, false, StringAlignment.Center);
            Typography.Label(g, "Actions", new RectangleF(box.Right - 114, box.Y + 43, 69, 19), 10.5f, Art.Muted, .35f, StringAlignment.Center);
            Art.Line(g, Color.FromArgb(65, Art.Gold), .7f, box.X + 15, box.Y + 68, box.Right - 15, box.Y + 68);

            double needs = game.Upkeep(actor) + BandEconomy.DomesticEffects(game, actor).AnimalCare;
            double coverage = needs <= 0 ? 0 : actor.Food / needs;
            DrawBandPanelMetric(g, new RectangleF(box.X + 13, box.Y + 73, 89, 43), "People", actor.Population.ToString("N0"), Art.Ink, 1,
                "This household's living population. Demographics records the whole tribe's births, losses and departures.");
            DrawBandPanelMetric(g, new RectangleF(box.X + 109, box.Y + 73, 119, 43), "Food", needs <= 0 ? "\u2014" : coverage.ToString("0.0") + "\u00d7 needs", coverage < 1 ? BandPanelWarning : Art.Ink, 0,
                Math.Floor(actor.Food).ToString("N0") + " food reserves held by this band; people and companion care need " + needs.ToString("0.#") + " food each turn. Other bands carry their own reserves. Open this household's forecast.");
            RectangleF saltBox = new RectangleF(box.X + 236, box.Y + 73, 106, 43);
            if (game.SaltEnabled)
            {
                double reserve = SaltEconomy.ReserveTurns(actor);
                DrawBandPanelMetric(g, saltBox, "Salt", reserve.ToString("0.0") + " turns", reserve < 1 ? BandPanelWarning : Art.Ink, -1,
                    actor.Salt.ToString("0.#") + " salt; " + SaltEconomy.Need(actor).ToString("0.#") + " needed per turn. Open this household's salt ledger.");
            }
            else DrawBandPanelMetric(g, saltBox, "Cohesion", (actor.Cohesion * 100).ToString("0") + "%", Art.Ink, 0, "Read household condition and its forecast in Economy.");

            string reunion = BandPanelReunionLabel(actor);
            TribeMembership member = game.TribeStatus(actor.Id);
            Color reunionInk = member != null && member.Drifting ? BandPanelWarning : Art.Muted;
            if (game.BandPersonalitiesEnabled)
            {
                Typography.Line(g, BandDispositionLabel(actor) + " \u00b7 " + BandLeaderDistance(actor), new RectangleF(box.X + 15, box.Y + 118, box.Width - 30, 25), 17, Art.Gold, TypeRole.Annotation, true);
                Typography.Line(g, reunion, new RectangleF(box.X + 15, box.Y + 145, box.Width - 30, 25), 16, reunionInk, TypeRole.Annotation, true);
            }
            else
            {
                if (reunionRouteVisible) { reunion = ReunionRouteStatus; reunionInk = ReunionRouteAvailable ? Art.Gold : Art.Muted; }
                Typography.Line(g, reunion, new RectangleF(box.X + 15, box.Y + 119, box.Width - 30, 25), 16, reunionInk, TypeRole.Annotation, true);
            }
            DrawBandPanelButtons(g, box, game.TribesEnabled);
        }

        private static readonly Color BandPanelWarning = Color.FromArgb(221, 145, 113);

        private string BandPanelReunionLabel(Band actor)
        {
            if (!game.TribesEnabled) return "One household, two actions each turn";
            TribeMembership member = game.TribeStatus(actor.Id);
            if (member == null) return "Choose a living tribal household";
            if (member.IsLeader) return "Tribe leader \u00b7 the reunion place is here";
            if (actor.CellId == member.ReunionCellId) return "Together at the reunion place";
            int left = Math.Max(0, member.SecedeAfter - member.TurnsAway);
            return (game.BandPersonalitiesEnabled ? member.TurnsAway + " turns apart \u00b7 " : member.Drifting ? "Drifting \u00b7 " : "Reunion \u00b7 ") + left + (left == 1 ? " turn" : " turns") + " until separation";
        }

        private string BandDispositionLabel(Band actor)
        {
            BandDisposition disposition = actor == null || !game.BandPersonalitiesEnabled ? null : game.DispositionFor(actor.Id);
            return disposition == null ? "Unrecorded disposition" : disposition.Label;
        }

        private string BandLeaderDistance(Band actor)
        {
            TribeMembership member = actor == null ? null : game.TribeStatus(actor.Id);
            if (member == null) return "No tribal contact";
            if (member.IsLeader) return "Leading household";
            return member.SeparationBand == 0 ? "With the leader" : member.SeparationBand == 1 ? "1 hex from leader" : "2+ hexes from leader";
        }

        private string BandVoluntaryMovementNote(Band actor)
        {
            TribeMembership member = game.TribeStatus(actor.Id);
            if (member != null && member.IsLeader) return "The leading household never wanders on its own.";
            if (game.HasBandOrderThisTurn(actor.Id)) return "Directed this turn; no voluntary movement at the close.";
            if (game.ActionsFor(actor.Id) <= 0) return "No actions remain; this band cannot move on its own this turn.";
            BandDisposition disposition = game.DispositionFor(actor.Id);
            string tendency = disposition == null ? "" : disposition.Temperament == BandTemperament.Loyal ? "Usually stays close to the leader. " :
                disposition.Temperament == BandTemperament.Restless ? "Wanders, then seeks reunion as kinship drifts. " :
                disposition.Temperament == BandTemperament.Adventurous ? "Favors exploring farther from the leader. " : "Strongly favors its own path. ";
            return tendency + "No orders: may move once at this close.";
        }

        private void HoldBandThisTurn(int id)
        {
            if (BlockingSheet || !game.BandPersonalitiesEnabled || !game.CanControlBand(id) || game.ActionsFor(id) <= 0) return;
            ArmMapCommandBand(id); Command("band:" + id + ":wait");
        }

        private void DrawBandHoldControl(Graphics g, Band actor, RectangleF bounds, string label)
        {
            int id = actor == null ? -1 : actor.Id;
            bool available = !SemiautomaticMode && game.BandPersonalitiesEnabled && game.CanControlBand(id) && game.ActionsFor(id) > 0;
            EncounterAction(g, label, bounds, available, delegate { HoldBandThisTurn(id); }, false);
            string tip = SemiautomaticMode ? "Switch to Manual to give a direct hold order." : "Finish this band's remaining actions and keep it from wandering on its own this turn. Normal food and salt upkeep still applies.";
            if (!available) buttons.Add(new UiButton(bounds, delegate { status = tip; Invalidate(); }));
            MapTip(tip);
        }

        private void DrawBandPanelMetric(Graphics g, RectangleF bounds, string label, string value, Color ink, int ledger, string tip)
        {
            if (bounds.Contains(hoverPoint)) Art.Fill(g, Color.FromArgb(28, Art.Gold), bounds.X, bounds.Y, bounds.Width, bounds.Height);
            Typography.Label(g, label, new RectangleF(bounds.X + 2, bounds.Y, bounds.Width - 4, 17), 10.5f, Art.Muted, .45f);
            Typography.Line(g, value, new RectangleF(bounds.X + 2, bounds.Y + 16, bounds.Width - 4, 28), 21, ink, TypeRole.Number, true);
            buttons.Add(new UiButton(bounds, delegate
            {
                if (BandPanelActor == null || BlockingSheet) return;
                if (ledger < 0) OpenSaltEconomy(); else { ClearMapTransient(); OpenEconomy(ledger); }
            }) { Tip = tip });
        }

        private void DrawBandPanelButtons(Graphics g, RectangleF box, bool tribal)
        {
            if (game.BandPersonalitiesEnabled)
            {
                Button(g, "Next [N]", box.X + 13, box.Bottom - 37, 92, 29, NextReadyBand, false, false);
                MapTip("Select the next living household with actions left. No action is spent.");
                DrawBandHoldControl(g, BandPanelActor, new RectangleF(box.X + 113, box.Bottom - 37, 82, 29), "Hold");
                Button(g, reunionRouteVisible ? "Hide route" : "Reunion route", box.X + 203, box.Bottom - 37, 140, 29, ToggleReunionRoute, reunionRouteVisible, false);
                MapTip("Preview a route to the leader without moving. " + (reunionRouteVisible ? ReunionRouteStatus + ". " : "") + "Orders remain manual.");
                return;
            }
            Button(g, "Next band  [N]", box.X + 13, box.Y + 149, 157, 29, NextReadyBand, false, false);
            MapTip("Select the next living household with actions left. This does not spend an action or end the turn.");
            Button(g, tribal ? reunionRouteVisible ? "Hide reunion route" : "Reunion route" : "Find band", box.X + 181, box.Y + 149, 162, 29,
                tribal ? (Action)ToggleReunionRoute : FindBandForPanel, tribal && reunionRouteVisible, false);
            MapTip(tribal ? "Preview a safe route through remembered land to the leader. Orders remain manual; this does not move the band." : "Find your living leader, including when the household has spent its actions.");
        }

        private void FindBandForPanel()
        {
            if (BlockingSheet || game.IsOver) return;
            Band actor = BandPanelActor ?? game.TribeLeaderBand;
            if (actor == null || !game.CanControlBand(actor.Id)) return;
            ArmMapCommandBand(actor.Id); selectedAnimalId = -1; selected = actor.CellId;
            inspectedBandId = actor.Id; inspectorPage = 1;
            map.SelectedAnimalId = -1; map.SelectedBandId = actor.Id;
            map.Focus(game.World.Cells[actor.CellId]); ShowMapSelection(); Invalidate();
        }

        private void DrawTribeMapRibbon(Graphics g)
        {
            Band[] members = game.ControlledBands.ToArray();
            int ready = members.Count(b => game.ActionsFor(b.Id) > 0);
            DrawFloatingMapPanel(g, new RectangleF(16, 102, 358, 46));
            DrawFloatingMapPanel(g, new RectangleF(383, 102, 966, 46));
            DrawFloatingMapPanel(g, new RectangleF(1373, 102, 211, 46));
            RectangleF identity = new RectangleF(28, 106, 346, 37);
            IdentityArt.DrawEmblem(g, game.PlayerTribeId, new RectangleF(32, 108, 33, 33), false);
            Typography.Line(g, game.Player.Name, new RectangleF(79, 107, 281, 36), 28, Art.Ink, TypeRole.Heading, true);
            buttons.Add(new UiButton(identity, delegate { ClearMapTransient(); OpenEconomy(0); }) { Tip = "Your tribe. The ribbon summarizes all controlled households; the selected-band card shows the household receiving orders." });
            MapRibbonMetric(g, new RectangleF(387, 106, 154, 38), "People", game.TribePopulation.ToString("N0"), 60, Art.Ink, 1, "The living population of all controlled tribal households. Open the tribe's Demographics ledger.");
            MapRibbonMetric(g, new RectangleF(560, 106, 220, 38), "Food reserves", Math.Floor(members.Sum(b => b.Food)).ToString("N0"), 120, Art.Gold, 4, "Food held across all controlled bands. Each band carries its own reserves and spends food on its people's needs and companion care each turn. This total is not a shared stockpile. Open Finance for the full account.");
            RectangleF readiness = new RectangleF(799, 106, 275, 38);
            Typography.Label(g, "Bands", new RectangleF(readiness.X + 5, readiness.Y, 58, 38), 11, Art.Muted, .45f);
            Typography.Line(g, ready + " / " + members.Length + " ready", new RectangleF(readiness.X + 75, readiness.Y, 191, 38), 25, ready > 0 ? Art.Ink : Art.Muted, TypeRole.Number, true);
            buttons.Add(new UiButton(readiness, delegate { ClearMapTransient(); page = 3; Invalidate(); }) { Tip = ready + " households have actions left. Open Units to select any tribal band and read its reunion status." });
            RectangleF salt = new RectangleF(1090, 106, 238, 38);
            if (game.SaltEnabled)
            {
                int low = members.Count(b => SaltEconomy.ReserveTurns(b) < 2);
                Typography.Label(g, "Salt watch", new RectangleF(salt.X + 5, salt.Y, 101, 38), 11, Art.Muted, .45f);
                Typography.Line(g, members.Length == 0 ? "\u2014" : low == 0 ? "Supplied" : low + " low", new RectangleF(salt.X + 112, salt.Y, 120, 38), 24, low == 0 ? Art.Ink : BandPanelWarning, TypeRole.Number, true);
                buttons.Add(new UiButton(salt, delegate { OpenSaltEconomy(); }) { Tip = members.Length == 0 ? "No living controlled households remain." : low + " households hold less than two turns of salt. Open the selected household's salt ledger; reserves are carried separately." });
            }
            else MapRibbonMetric(g, salt, "Actions", members.Sum(b => game.ActionsFor(b.Id)).ToString("N0"), 101, Art.Ink, 0, "Remaining actions across the controlled tribe.");
            DrawMapAdviserEntries(g);
        }
    }
}
