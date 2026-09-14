using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;
using Clio.Simulation;

namespace Clio.Desktop
{
    public sealed partial class GameForm
    {
        // This presentation belongs to the saved guided ruleset. It never changes
        // an older story's rules or exposes the two-orders-per-band controls.
        private bool GuidedGame { get { return game != null && game.GuidedOpening; } }
        private bool guidedReport = true, guidedTravel, guidedSupplies, guidedMenu, guidedDetails;
        private int guidedRoutePage;
        private string guidedResult = "";
        private readonly List<RectangleF> guidedPanels = new List<RectangleF>();
        private bool GuidedModal { get { return game.Turn == 1 || game.IsOver || game.Turn == 2 && guidedReport && !guidedTravel; } }

        private void ResetGuidedPresentation()
        {
            StopFirstAdviserVoice();
            guidedTravelCameraSaved = false; EndGuidedTravelPreview(false);
            if (!game.GuidedWildlifeEnabled && commands.Count < MaximumCommands)
            { Execute(game, "enable-guided-wildlife"); commands.Add("enable-guided-wildlife"); }
            ResetStoryMode(); ClearNotices(true); ResetOpeningAnnouncement(); ResetEnding();
            adviserOpen = false; encounterChoice = false; gatheringChoice = false;
            ClearMapTransient(); HideMapHover();
            page = 0; selected = game.Player.CellId; selectedAnimalId = -1;
            guidedReport = game.Turn != 2 || game.Influence > 0; guidedTravel = guidedSupplies = guidedMenu = guidedDetails = false;
            guidedRoutePage = 0; guidedResult = "";
            ResetGuidedReports(); map.Zoom = MapRenderer.GuidedOpeningZoom;
            map.Fog = true; map.Grid = false; map.Layer = 0; map.PlaceLabels = false;
            map.SelectedBandId = game.Player.Id; map.SelectedAnimalId = -1;
            map.InvalidateTerrain(); buttons.Clear(); Invalidate(); SpeakGuidedAdviser();
        }

        private void RunGuidedCommand(string command)
        {
            if (game.IsOver) return;
            bool end = command == "end", gather = command == "guided-gather";
            int destination = -1;
            bool move = command.StartsWith("guided-move:", StringComparison.Ordinal) &&
                Int32.TryParse(command.Substring(12), NumberStyles.None, CultureInfo.InvariantCulture, out destination);
            if (!end && !gather && !move) return;
            if (!end && (game.Turn == 1 || game.Influence < 1)) return;
            if (move && !game.CanGuidedMoveToCell(destination)) return;
            if (commands.Count >= MaximumCommands)
            {
                guidedResult = "This experimental story has reached its command limit. Save it, then begin another story.";
                guidedReport = true; Invalidate(); return;
            }
            JournalSnapshot before = StoryJournal.Capture(game);
            GuidedObservation observation = CaptureGuidedObservation(); StopFirstAdviserVoice();
            guidedResult = Execute(game, command); status = guidedResult;
            commands.Add(command); journal.Record(game, before, command, guidedResult);
            selected = game.Player.CellId;
            if (guidedTravelCameraSaved) EndGuidedTravelPreview(move);
            if (move) map.Focus(game.World.Cells[selected]);
            map.InvalidateTerrain(); map.OrderPreviewCell = -1;
            guidedTravel = guidedSupplies = guidedMenu = guidedDetails = false;
            UpdateGuidedReport(observation, command);
            buttons.Clear(); hoverPoint = new PointF(-1, -1); Invalidate(); SpeakGuidedAdviser();
        }

        private static string GuidedAmount(double value)
        { return value.ToString("0.#", CultureInfo.InvariantCulture); }

        private void GuidedSheet(Graphics g, RectangleF bounds, bool ornament = true)
        {
            using (Brush shadow = new SolidBrush(Color.FromArgb(28, 67, 46, 25)))
                g.FillRectangle(shadow, bounds.X + 5, bounds.Y + 6, bounds.Width, bounds.Height);
            MapPaper.Surface(g, bounds, ornament); guidedPanels.Add(bounds);
        }

        private void GuidedButton(Graphics g, string label, RectangleF bounds, Action click,
            string tip, bool enabled = true, string icon = null, bool primary = false)
        {
            bool hover = enabled && bounds.Contains(hoverPoint);
            MapPaper.Surface(g, bounds, false, primary || hover);
            Color ink = enabled ? MapPaper.Ink : MapPaper.DisabledInk;
            if (icon != null) Art.Icon(g, icon, bounds.X + 13, bounds.Y + (bounds.Height - 24) / 2, 24, enabled ? MapPaper.Russet : ink);
            Typography.Line(g, label, new RectangleF(bounds.X + (icon == null ? 10 : 44), bounds.Y,
                bounds.Width - (icon == null ? 20 : 54), bounds.Height), 18, ink, TypeRole.Action, true,
                icon == null ? StringAlignment.Center : StringAlignment.Near);
            buttons.Add(new UiButton(bounds, enabled ? click : (Action)delegate { }) { Tip = tip });
        }

        private void DrawGuidedFrame(Graphics g)
        {
            guidedPanels.Clear();
            map.SetBounds(campaignSurface); map.GuidedPresentation = true; map.IntroPresentation = game.Turn == 1;
            map.OrderPreviewCell = -1; map.SelectedAnimalId = -1; map.SelectedBandId = game.Player.Id;
            map.Draw(g, game, game.Player.CellId);
            if (game.Turn == 1 || game.IsOver) { DrawGuidedOpening(g); DrawFirstAdviserVoiceControl(g); DrawGuidedTooltip(g); return; }
            if (guidedTravel) { DrawGuidedTravelPreview(g); DrawFirstAdviserVoiceControl(g); DrawGuidedTooltip(g); return; }
            if (game.Turn == 2 && guidedReport) { DrawGuidedInfluenceLesson(g); DrawFirstAdviserVoiceControl(g); DrawGuidedTooltip(g); return; }

            GuidedSheet(g, new RectangleF(24, 20, 310, 63), false);
            Typography.Line(g, game.Player.Name, new RectangleF(43, 25, 222, 30), 28, MapPaper.Ink, TypeRole.Heading, true);
            Typography.Label(g, "Turn " + game.Turn, new RectangleF(44, 55, 206, 18), 11, MapPaper.MutedInk);
            if (game.Turn >= 3) GuidedButton(g, "\u2261", new RectangleF(280, 31, 40, 40), delegate { guidedMenu = !guidedMenu; guidedSupplies = false; }, "Save, load, start a story, or change the map view.");
            GuidedSheet(g, new RectangleF(617, 20, 366, 63), false);
            Art.Icon(g, "sun", 636, 38, 26, MapPaper.Russet);
            Typography.Line(g, game.Influence + " influence", new RectangleF(675, 24, 218, 33), 25, MapPaper.Ink, TypeRole.Heading);
            Typography.Line(g, "+1 each turn", new RectangleF(675, 55, 220, 19), 14, MapPaper.MutedInk, TypeRole.Body);
            if (game.Turn >= 3)
            {
                GuidedButton(g, "Supplies", new RectangleF(1238, 22, 144, 46), delegate { guidedSupplies = !guidedSupplies; guidedMenu = false; guidedReport = false; },
                    "Food, salt and wood: reserves, needs and the regional gathering forecast.", true, "leaf");
                GuidedButton(g, "First Adviser", new RectangleF(1392, 22, 183, 46), ToggleGuidedReport,
                    "Ask your First Adviser what matters now.");
            }
            bool canSpend = game.Influence > 0;
            GuidedButton(g, "Gather  \u00b7  1", new RectangleF(24, 870, 184, 60), delegate { RunGuidedCommand("guided-gather"); }, GuidedGatherTip(), canSpend, "leaf");
            GuidedButton(g, "Move  \u00b7  1", new RectangleF(218, 870, 184, 60), OpenGuidedTravel,
                "Spend one influence to move your whole tribe to a neighboring region. Choose a route after clicking. [M]", canSpend, "move");
            GuidedButton(g, "End turn", new RectangleF(1385, 864, 191, 68), delegate { RunGuidedCommand("end"); },
                "Your people use food, salt and firewood. You gain one influence; unused influence carries forward. [Space]", true, null, true);
            Typography.Line(g, "SPACE", new RectangleF(1410, 934, 142, 18), 10, MapPaper.MutedInk, TypeRole.Utility, false, StringAlignment.Center);
            string bottom = String.IsNullOrEmpty(guidedResult) ? game.GuidedRegionName() : guidedResult;
            Typography.Draw(g, bottom, new RectangleF(426, 886, 926, 53), 17, MapPaper.Ink, TypeRole.Body);
            if (guidedReport && game.Turn >= 3) DrawGuidedAdviser(g);
            if (guidedSupplies) DrawGuidedSupplies(g);
            if (guidedMenu) DrawGuidedMenu(g);
            DrawGuidedAnimalCard(g);
            DrawFirstAdviserVoiceControl(g);
            DrawGuidedTooltip(g);
        }

        private void GuidedDimMap(Graphics g)
        {
            using (Brush quiet = new SolidBrush(Color.FromArgb(102, MapPaper.Ground))) g.FillRectangle(quiet, campaignSurface);
            Typography.Label(g, "Clio / " + game.Player.Name + " / Turn " + game.Turn,
                new RectangleF(248, 104, 1104, 26), 13, MapPaper.Russet);
        }

        private void DrawGuidedOpening(Graphics g)
        {
            GuidedDimMap(g);
            GuidedSheet(g, new RectangleF(246, 150, 1108, 647));
            FirstAdviserArt.Draw(g, new RectangleF(281, 244, 251, 251));
            Typography.Line(g, "First Adviser", new RectangleF(282, 513, 250, 40), 26, MapPaper.Ink, TypeRole.Heading, false, StringAlignment.Center);
            Typography.Line(g, "A word before we begin", new RectangleF(280, 559, 254, 26), 17, MapPaper.MutedInk, TypeRole.Annotation, false, StringAlignment.Center);
            Typography.Label(g, game.IsOver ? "The last page" : "The first evening", new RectangleF(574, 193, 705, 28), 13, MapPaper.Russet);
            Typography.Line(g, game.IsOver ? "The hearth has gone cold" : "Let morning find us ready", new RectangleF(570, 235, 740, 65), 41, MapPaper.Ink, TypeRole.Display, true);
            string text = game.IsOver ? "Your people's journey ends on turn " + game.Turn + ". The tribe could no longer sustain itself. Its travels and choices remain in this story. A new beginning can take a different path." :
                "Fifty people have placed their trust in you. Tonight, that is enough. The first matters of the tribe begin in the morning, after rest.\n\nBed early; rise with the light. A rested chief is more likely to meet the day's troubles with a clear head. Even wisdom benefits from a full night's sleep.";
            Typography.Draw(g, text, new RectangleF(575, 323, 705, 258), 23, MapPaper.Ink, TypeRole.Body);
            Art.Line(g, MapPaper.Rule, .8f, 576, 608, 1284, 608);
            Typography.Draw(g, game.IsOver ? "Save this story to keep its record, or begin again." : "No decisions tonight. Your people rest; no supplies are spent.",
                new RectangleF(576, 631, 702, 50), 19, MapPaper.MutedInk, TypeRole.Annotation);
            if (game.IsOver)
            {
                GuidedButton(g, "Save story", new RectangleF(576, 710, 190, 51), SaveStory, "Keep this story.");
                GuidedButton(g, "New story", new RectangleF(1068, 710, 216, 51), NewStory, "Begin again.", true, null, true);
            }
            else GuidedButton(g, "End turn  [Space]", new RectangleF(1008, 710, 276, 51), delegate { RunGuidedCommand("end"); }, "Rest until morning.", true, null, true);
        }

        private void DrawGuidedInfluenceLesson(Graphics g)
        {
            GuidedDimMap(g); GuidedSheet(g, new RectangleF(236, 146, 1128, 684));
            FirstAdviserArt.Draw(g, new RectangleF(274, 241, 206, 206));
            Typography.Line(g, "First Adviser", new RectangleF(270, 466, 219, 37), 24, MapPaper.Ink, TypeRole.Heading, false, StringAlignment.Center);
            Typography.Label(g, "Morning / " + game.Influence + " influence available", new RectangleF(521, 185, 768, 25), 13, MapPaper.Russet);
            Typography.Line(g, "Your word carries weight", new RectangleF(515, 224, 781, 61), 40, MapPaper.Ink, TypeRole.Display, true);
            Typography.Draw(g, "You are chief of the tribe. That gives you one influence each turn: the power to direct our common effort. Unspent influence carries forward.\n\nFor now, choose between two uses. Each costs one influence. We need not make government any more complicated before breakfast.",
                new RectangleF(522, 303, 758, 191), 22, MapPaper.Ink, TypeRole.Body);
            DrawGuidedChoice(g, new RectangleF(274, 537, 493, 175), "Gather", "Bring back food, wood and salt according to what this region offers. One order gathers them together.",
                "leaf", delegate { RunGuidedCommand("guided-gather"); }, GuidedGatherTip());
            DrawGuidedChoice(g, new RectangleF(792, 537, 534, 175), "Move", "Lead the tribe to a neighboring region. Choose a route; discover what lies along it.",
                "move", OpenGuidedTravel, "One influence moves the tribe between regions, each made of several hexes. [M]");
            Typography.Line(g, "Or keep your influence for later.", new RectangleF(276, 755, 653, 32), 18, MapPaper.MutedInk, TypeRole.Annotation);
            GuidedButton(g, "End turn  [Space]", new RectangleF(1056, 745, 270, 48), delegate { RunGuidedCommand("end"); },
                "Bank this influence. Supplies are used at turn's end; the next turn grants another point.");
        }

        private void DrawGuidedChoice(Graphics g, RectangleF bounds, string title, string text, string icon, Action click, string tip)
        {
            MapPaper.Surface(g, bounds, false, bounds.Contains(hoverPoint));
            Art.Icon(g, icon, bounds.X + 22, bounds.Y + 22, 29, MapPaper.Russet);
            Typography.Line(g, title, new RectangleF(bounds.X + 65, bounds.Y + 16, bounds.Width - 165, 40), 29, MapPaper.Ink, TypeRole.Heading);
            Typography.Line(g, "1 influence", new RectangleF(bounds.Right - 129, bounds.Y + 28, 112, 26), 16, MapPaper.Russet, TypeRole.Body);
            Typography.Draw(g, text, new RectangleF(bounds.X + 23, bounds.Y + 75, bounds.Width - 46, 80), 20, MapPaper.Ink, TypeRole.Body);
            buttons.Add(new UiButton(bounds, click) { Tip = tip });
        }

        private string GuidedGatherTip()
        {
            GuidedResourceYield yield = game.GuidedGatherForecast();
            return "Gather for 1 influence: +" + GuidedAmount(yield.Food) + " food, +" + GuidedAmount(yield.Wood) +
                " wood, +" + GuidedAmount(yield.Salt) + " salt. Regional abundance and depletion determine the yield. [F]";
        }

        private void OpenGuidedTravel()
        {
            if (game.Turn == 1 || game.Influence < 1 || game.IsOver) return;
            guidedTravel = true; guidedRoutePage = 0; guidedSupplies = guidedMenu = false;
            guidedAnimalCell = -1; StopFirstAdviserVoice(); BeginGuidedTravelPreview();
        }

        private string GuidedBearing(int cellId)
        {
            Vec3 from = game.World.Cells[game.Player.CellId].Center, to = game.World.Cells[cellId].Center;
            double lat1 = Math.Asin(from.Y), lat2 = Math.Asin(to.Y), delta = Math.Atan2(to.X, to.Z) - Math.Atan2(from.X, from.Z);
            double angle = Math.Atan2(Math.Sin(delta) * Math.Cos(lat2), Math.Cos(lat1) * Math.Sin(lat2) - Math.Sin(lat1) * Math.Cos(lat2) * Math.Cos(delta));
            string[] names = { "North", "Northeast", "East", "Southeast", "South", "Southwest", "West", "Northwest" };
            return names[((int)Math.Round(angle * 4 / Math.PI) + 8) % 8];
        }

        private void DrawGuidedAdviser(Graphics g)
        {
            string title; string advice = GuidedSituationAdvice(out title);
            float bodySize = 19;
            while (bodySize > 17 && g.MeasureString(advice, Typography.Font(TypeRole.Body, bodySize), 386).Height > 320) bodySize -= .5f;
            float bodyHeight = Math.Max(108, Math.Min(320, g.MeasureString(advice, Typography.Font(TypeRole.Body, bodySize), 386).Height + 8));
            float height = 240 + bodyHeight + (guidedDetails ? 154 : 0);
            GuidedSheet(g, new RectangleF(1134, 96, 442, height));
            FirstAdviserArt.Draw(g, new RectangleF(1155, 118, 88, 88));
            Typography.Line(g, "First Adviser", new RectangleF(1259, 123, 264, 36), 25, MapPaper.Ink, TypeRole.Heading);
            Typography.Line(g, "Counsel for turn " + game.Turn, new RectangleF(1261, 165, 265, 29), 17, MapPaper.MutedInk, TypeRole.Annotation);
            GuidedButton(g, "\u00d7", new RectangleF(1531, 108, 31, 29), delegate { guidedReport = false; StopFirstAdviserVoice(); }, "Dismiss. Reopen with First Adviser. [Esc]");
            Typography.Line(g, title, new RectangleF(1158, 224, 392, 37), 26, MapPaper.Ink, TypeRole.Heading, true);
            Typography.Draw(g, advice, new RectangleF(1159, 277, 386, bodyHeight), bodySize, MapPaper.Ink, TypeRole.Body);
            if (guidedDetails)
            {
                Art.Line(g, MapPaper.Rule, .8f, 1160, 277 + bodyHeight + 8, 1548, 277 + bodyHeight + 8);
                Typography.Draw(g, "Each turn adds one influence. Gather and Move each cost one. Unspent influence carries forward.\n\nFood feeds the tribe, salt sustains health, and wood fuels cooking fires. The Supplies panel shows the current amounts.", new RectangleF(1159, 277 + bodyHeight + 20, 386, 129), 18, MapPaper.MutedInk, TypeRole.Body);
            }
            GuidedButton(g, guidedDetails ? "Less" : "Explain more", new RectangleF(1338, 96 + height - 55, 209, 36), delegate { guidedDetails = !guidedDetails; }, "Explain influence and regional orders.");
        }

        private void DrawGuidedSupplies(Graphics g)
        {
            GuidedSheet(g, new RectangleF(1040, 96, 536, 470));
            Typography.Line(g, "The tribe's supplies", new RectangleF(1065, 116, 447, 43), 30, MapPaper.Ink, TypeRole.Heading);
            GuidedButton(g, "\u00d7", new RectangleF(1531, 108, 31, 29), delegate { guidedSupplies = false; }, "Close supplies. [Esc]");
            Typography.Line(g, game.Player.Population + " people", new RectangleF(1067, 170, 442, 28), 21, MapPaper.Ink, TypeRole.Body);
            Typography.Label(g, "Reserve", new RectangleF(1190, 218, 100, 24), 11, MapPaper.MutedInk);
            Typography.Label(g, "Per turn", new RectangleF(1335, 218, 100, 24), 11, MapPaper.MutedInk);
            string[] labels = { "Food", "Salt", "Wood" };
            double[] stock = { game.Player.Food, game.Player.Salt, game.Player.Wood };
            double[] needs = { game.Upkeep(game.Player), SaltEconomy.Need(game.Player), WoodEconomy.FuelNeed(game.Player) };
            for (int i = 0; i < 3; i++)
            {
                float y = 255 + i * 47;
                Typography.Line(g, labels[i], new RectangleF(1067, y, 108, 35), 22, MapPaper.Ink, TypeRole.Body);
                Typography.Line(g, GuidedAmount(stock[i]), new RectangleF(1190, y, 138, 35), 25, stock[i] < needs[i] * 2 ? MapPaper.Warning : MapPaper.Ink, TypeRole.Number);
                Typography.Line(g, "-" + GuidedAmount(needs[i]), new RectangleF(1335, y, 135, 35), 22, MapPaper.MutedInk, TypeRole.Number);
            }
            Art.Line(g, MapPaper.Rule, .8f, 1067, 405, 1544, 405);
            GuidedResourceYield yield = game.GuidedGatherForecast();
            Typography.Draw(g, "Gather here: +" + GuidedAmount(yield.Food) + " food, +" + GuidedAmount(yield.Wood) + " wood, +" + GuidedAmount(yield.Salt) + " salt.",
                new RectangleF(1067, 422, 471, 69), 21, MapPaper.Ink, TypeRole.Body);
            Typography.Draw(g, yield.Salt <= 0 ? "No salt source in this region. A different region may offer one." : "Amounts reflect this region's abundance and depleted ground.",
                new RectangleF(1067, 502, 471, 45), 17, MapPaper.MutedInk, TypeRole.Annotation);
        }

        private void DrawGuidedMenu(Graphics g)
        {
            GuidedSheet(g, new RectangleF(24, 99, 310, 393));
            GuidedButton(g, "Save story", new RectangleF(42, 121, 274, 43), SaveStory, "Save your story and influence. [Ctrl+S]");
            GuidedButton(g, "Load story", new RectangleF(42, 176, 274, 43), LoadStory, "Load a saved story. Older stories keep their original modes. [Ctrl+O]");
            GuidedButton(g, "New story", new RectangleF(42, 231, 274, 43), NewStory, "Guided beginning is the default. Uncheck it in a new story to use the earlier gameplay modes.");
            GuidedButton(g, map.Fog ? "Show full atlas" : "Show known land", new RectangleF(42, 286, 274, 43), delegate { map.Fog = !map.Fog; map.InvalidateTerrain(); }, "Toggle the full world for development, or return to known land.");
            GuidedButton(g, "Find the tribe", new RectangleF(42, 341, 274, 43), delegate { map.Focus(game.World.Cells[game.Player.CellId]); guidedMenu = false; }, "Center the map on your people. [Home]");
            Typography.Line(g, "Guided beginning / " + BuildIdentity, new RectangleF(43, 412, 275, 23), 13, MapPaper.MutedInk, TypeRole.Body, true);
            Typography.Line(g, "Drag to pan / Scroll to zoom / F11", new RectangleF(43, 448, 275, 24), 13, MapPaper.MutedInk, TypeRole.Body, true);
        }

        private void DrawGuidedTooltip(Graphics g)
        {
            if (dragging) return;
            UiButton button = buttons.LastOrDefault(b => b.Bounds.Contains(hoverPoint));
            if (button == null || String.IsNullOrEmpty(button.Tip)) return;
            float width = 398, height = 113;
            float x = Math.Max(20, Math.Min(1580 - width, hoverPoint.X + 19));
            float y = hoverPoint.Y + 28;
            if (y + height > 846) y = hoverPoint.Y - height - 20;
            RectangleF bounds = new RectangleF(x, Math.Max(90, y), width, height);
            MapPaper.Surface(g, bounds, false);
            Typography.Draw(g, button.Tip, RectangleF.Inflate(bounds, -16, -14), 17, MapPaper.Ink, TypeRole.Body);
        }

        private void GuidedPointerDown(MouseEventArgs e)
        {
            if (!InputViewport.Contains(e.Location)) return;
            PointF p = Virtual(e.Location);
            if (e.Button == MouseButtons.Left)
            {
                UiButton button = buttons.LastOrDefault(b => b.Bounds.Contains(p));
                if (button != null) { button.Click(); if (!guidedReport || guidedTravel || guidedMenu || guidedSupplies) StopFirstAdviserVoice(); buttons.Clear(); Invalidate(); return; }
            }
            if (GuidedModal || guidedPanels.Any(b => b.Contains(p))) return;
            if (e.Button == MouseButtons.Right)
            {
                if (guidedTravel) { TryGuidedTravelMapClick(p); return; }
                int cell = map.Pick(p.X, p.Y);
                if (cell >= 0 && game.Explored.Contains(cell) && game.CanGuidedMoveToCell(cell))
                    RunGuidedCommand("guided-move:" + cell.ToString(CultureInfo.InvariantCulture));
                return;
            }
            if (e.Button != MouseButtons.Left || !map.Bounds.Contains(p)) return;
            dragging = true; moved = false; dragStart = lastMouse = e.Location; Capture = true;
        }

        private void GuidedPointerMove(MouseEventArgs e)
        {
            PointF p = Virtual(e.Location);
            int previous = buttons.FindLastIndex(b => b.Bounds.Contains(hoverPoint));
            int current = buttons.FindLastIndex(b => b.Bounds.Contains(p));
            hoverPoint = p;
            bool routeHover = UpdateGuidedTravelHover(p);
            bool animalHover = !guidedTravel && !GuidedModal && (map.PeekAnimal(p.X, p.Y) >= 0 || map.PickUnitStack(p.X, p.Y) >= 0);
            Cursor = current >= 0 || routeHover || animalHover ? Cursors.Hand : GuidedModal || guidedPanels.Any(b => b.Contains(p)) ? Cursors.Default : Cursors.SizeAll;
            if (previous != current || current >= 0) Invalidate();
            if (!dragging) return;
            if (Math.Abs(e.X - dragStart.X) + Math.Abs(e.Y - dragStart.Y) > 5) moved = true;
            float scale = GameViewport.Width / 1600f;
            if (moved && scale > 0)
            {
                map.IsNavigating = true;
                map.Longitude -= (e.X - lastMouse.X) * .003 / (map.Zoom * scale);
                map.Latitude += (e.Y - lastMouse.Y) * .003 / (map.Zoom * scale * map.RegionalVerticalScale);
                Invalidate();
            }
            lastMouse = e.Location;
        }

        private void GuidedPointerUp(MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left || !dragging) return;
            if (!moved)
            {
                if (guidedTravel) TryGuidedTravelMapClick(Virtual(e.Location));
                else TryGuidedAnimalClick(Virtual(e.Location));
            }
            dragging = false; Capture = false; map.IsNavigating = false; Invalidate();
        }

        private void GuidedWheel(MouseEventArgs e)
        {
            PointF p = Virtual(e.Location);
            if (GuidedModal || !InputViewport.Contains(e.Location) || guidedPanels.Any(b => b.Contains(p)) || buttons.Any(b => b.Bounds.Contains(p))) return;
            map.IsNavigating = true;
            map.Zoom = Math.Max(.82, Math.Min(MapRenderer.MaximumZoom, map.Zoom * (e.Delta > 0 ? 1.12 : 1 / 1.12)));
            settleCamera.Stop(); settleCamera.Start(); Invalidate();
        }

        private void GuidedKey(KeyEventArgs e)
        {
            // Turn one has a single way forward. Window controls remain available.
            e.Handled = true; e.SuppressKeyPress = true;
            if (e.KeyCode == Keys.V) { ToggleFirstAdviserVoice(); return; }
            if (game.Turn == 1 && !game.IsOver)
            { if (e.KeyCode == Keys.Space || e.KeyCode == Keys.Enter) RunGuidedCommand("end"); return; }
            if (e.Control && e.KeyCode == Keys.S) { SaveStory(); return; }
            if (e.Control && e.KeyCode == Keys.O) { LoadStory(); return; }
            if (e.KeyCode == Keys.Escape)
            {
                if (guidedTravel) EndGuidedTravelPreview(false);
                else if (game.Turn >= 3) { guidedReport = guidedSupplies = guidedMenu = false; guidedAnimalCell = -1; StopFirstAdviserVoice(); }
            }
            else if (!game.IsOver && !guidedTravel)
            {
                if (e.KeyCode == Keys.Space || e.KeyCode == Keys.Enter) RunGuidedCommand("end");
                else if (e.KeyCode == Keys.F) RunGuidedCommand("guided-gather");
                else if (e.KeyCode == Keys.M) OpenGuidedTravel();
                else if (e.KeyCode == Keys.Home) map.Focus(game.World.Cells[game.Player.CellId]);
                else if (e.KeyCode == Keys.C && game.Turn >= 3) ToggleGuidedReport();
            }
            buttons.Clear(); Invalidate();
        }
    }
}
