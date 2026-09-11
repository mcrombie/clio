using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using Clio.Simulation;

namespace Clio.Desktop
{
    public sealed partial class GameForm
    {
        // Only one map menu is open at a time. These are presentation choices;
        // none of the view controls advances the simulation or consumes an action.
        private int mapMenu;

        private RectangleF MapMenuBounds
        {
            get
            {
                if (page != 0) return RectangleF.Empty;
                if (mapMenu == 1) return new RectangleF(14, 112, 460, 302);
                if (mapMenu == 3) return new RectangleF(1094, game.Rules == SimulationRules.Classic ? 205 : 249, 474, game.Rules == SimulationRules.Classic ? 583 : 539);
                if (mapMenu == 4) return new RectangleF(14, 112, 470, 438);
                return RectangleF.Empty;
            }
        }

        private bool MapMenuContains(PointF point)
        {
            if (page != 0 || mapMenu == 0) return false;
            // Menu triggers belong to the open menu's interaction region. The
            // parent can dispatch their existing buttons in one click instead
            // of consuming the press only to dismiss the previous menu.
            return MapMenuBounds.Contains(point) || CampaignToolbarBounds.Contains(point) || CampaignWatchBounds.Contains(point);
        }

        private void CloseMapMenus()
        { mapMenu = 0; }

        private void ToggleMapMenu(int menu)
        {
            if (BlockingSheet) return;
            int next = mapMenu == menu ? 0 : menu;
            ClearMapTransient(); mapMenu = next; buttons.Clear(); Invalidate();
        }

        private void MapTip(string text)
        { if (buttons.Count > 0) buttons[buttons.Count - 1].Tip = text; }

        private void DrawMapRibbon(Graphics g)
        {
            if (game.TribesEnabled) { DrawTribeMapRibbon(g); return; }
            bool salt = game.SaltEnabled;
            DrawFloatingMapPanel(g, new RectangleF(16, 102, salt ? 358 : 480, 46));
            DrawFloatingMapPanel(g, new RectangleF(salt ? 383 : 502, 102, salt ? 966 : 847, 46));
            DrawFloatingMapPanel(g, new RectangleF(1373, 102, 211, 46));
            RectangleF identity = new RectangleF(28, 106, salt ? 346 : 464, 37);
            if (identity.Contains(hoverPoint)) Art.Fill(g, Art.PaperMode ? MapPaper.HoverWash : Color.FromArgb(34, 46, 49), identity.X, identity.Y, identity.Width, identity.Height);
            IdentityArt.DrawEmblem(g, game.TribeOf(CurrentOrderBand.Id), new RectangleF(32, 108, 33, 33), false);
            Typography.Line(g, CurrentOrderBand.Name, new RectangleF(79, 107, salt ? 281 : 399, 36), 28, Art.Ink, TypeRole.Heading, true);
            buttons.Add(new UiButton(identity, delegate { ClearMapTransient(); OpenEconomy(0); }));
            MapTip("Open your household's economic overview.");

            MapRibbonMetric(g, new RectangleF(salt ? 387 : 515, 106, salt ? 154 : 175, 38), "People", CurrentOrderBand.Population.ToString("N0"), salt ? 60 : 70, Art.Ink, 1,
                game.TribesEnabled ? "People in the selected band. Open Demographics for your whole tribe's population record." : "Population, recorded births and deaths, and daughter bands.");
            MapRibbonMetric(g, new RectangleF(salt ? 560 : 708, 106, salt ? 220 : 260, 38), "Food reserves", Math.Floor(CurrentOrderBand.Food).ToString("N0"), 120, Art.Gold, 4,
                "Food held by this band. Its people and companions consume food each turn; travel and offerings also spend it. Open Finance for the full account.");
            double needs = game.Upkeep(CurrentOrderBand) + BandEconomy.DomesticEffects(game, CurrentOrderBand).AnimalCare;
            double security = needs <= 0 ? 0 : CurrentOrderBand.Food / needs;
            MapRibbonMetric(g, new RectangleF(salt ? 799 : 990, 106, salt ? 275 : 323, 38), "Food security", needs <= 0 ? "\u2014" : security.ToString("0.0") + "\u00d7 needs", salt ? 100 : 126,
                security < 1 && needs > 0 ? Art.PaperMode ? MapPaper.Warning : Color.FromArgb(221, 145, 113) : Art.Ink, 0,
                "Current provisions divided by your people's needs and companion care. Open Economy for the full forecast.");
            if (salt) DrawMapSaltReserve(g, new RectangleF(1090, 106, 238, 38));
            DrawMapAdviserEntries(g);
        }

        private void MapRibbonMetric(Graphics g, RectangleF bounds, string label, string value, float labelWidth, Color color, int ledger, string tip)
        {
            if (bounds.Contains(hoverPoint)) Art.Fill(g, Art.PaperMode ? MapPaper.HoverWash : Color.FromArgb(34, 46, 49), bounds.X, bounds.Y, bounds.Width, bounds.Height);
            Typography.Label(g, label, new RectangleF(bounds.X + 5, bounds.Y, labelWidth - 9, bounds.Height), 11, Art.Muted, .45f);
            Typography.Line(g, value, new RectangleF(bounds.X + labelWidth, bounds.Y, bounds.Width - labelWidth - 6, bounds.Height), 27, color, TypeRole.Number, true);
            buttons.Add(new UiButton(bounds, delegate { ClearMapTransient(); OpenEconomy(ledger); })); MapTip(tip);
        }

        private void DrawMapToolbar(Graphics g)
        {
            // A small symbol key; the full legend is one click away.
            DrawCampaignBacking(g, CampaignToolbarBounds);
            MapIconButton(g, "globe", CampaignToolbarIconBounds(0), delegate { ToggleMapMenu(1); }, mapMenu == 1 || !map.Fog, false,
                "Map views / " + (map.Fog ? "Known land" : "Whole atlas") + "\nChoose terrain, food, regions, speech or polities; toggle the grid and known land. Scroll over terrain to zoom in or out; drag to roam.");
            MapIconButton(g, "locate", CampaignToolbarIconBounds(1), FocusCommandBandQuietly, false, false,
                "Find your band [Home]\nCenter the map on the selected band. Click its banner to inspect it.");
            MapIconButton(g, "leaf", CampaignToolbarIconBounds(2), delegate { CloseMapMenus(); map.Layer = map.Layer == 1 ? 0 : 1; Invalidate(); }, map.Layer == 1, false,
                "Find food\nToggle the food map. Bright leaf markers show strong gathering places; move there and Gather to collect food.");
            MapIconButton(g, "salt", CampaignToolbarIconBounds(3), OpenSaltEconomy, false, false,
                "Find salt\nCrystal markers locate salt. Open the list of known sources and your band's reserves.");
            MapIconButton(g, "explore", CampaignToolbarIconBounds(4), delegate { CloseMapMenus(); map.Layer = 0; map.InterestHighlights = true; Invalidate(); }, false, false,
                "Explore\nShow terrain and useful-place markers. Blue compasses stand beside unknown land; move to those hexes to discover more. The book opens the symbol guide.");
            MapIconButton(g, "book", CampaignToolbarIconBounds(5), delegate { ToggleMapMenu(4); }, mapMenu == 4, false,
                "Symbol guide\nLearn food, salt and exploration markers, and mountain and river movement costs.");
        }

        private void FocusCommandBandQuietly()
        {
            ClearMapTransient(); page = 0;
            Band actor = CurrentOrderBand;
            if (game.CanControlBand(actor.Id)) ArmMapCommandBand(actor.Id);
            selected = actor.CellId; map.Focus(game.World.Cells[selected]); buttons.Clear(); Invalidate();
        }

        private void DrawMapDock(Graphics g)
        {
            DrawCampaignDock(g);
        }

        private void DrawCompactPlayControls(Graphics g)
        {
            Button(g, SemiautomaticMode ? "Semiautomatic" : AutomaticMode ? "Automatic" : "Manual", 1000, 868, 206, 43,
                delegate { CloseMapMenus(); OpenPlayMode(); }, SemiautomaticMode || AutomaticMode, false);
            MapTip("Gameplay mode\nManual: choose individual orders. Semiautomatic: choose story directions. Automatic: watch your people act. Click to change mode.");
            MapIconButton(g, "settings", new RectangleF(1220, 868, 44, 43), delegate { if (page != 0) { ClearMapTransient(); page = 0; } ToggleMapMenu(3); }, mapMenu == 3, false,
                "Watch settings\nAdviser frequency, autoplay speed, event pauses and camera following.");
            if (SemiautomaticMode)
            {
                Button(g, autoplay ? "Pause story [P]" : "Continue story", 1280, 868, 288, 43, ContinueSemiautomatic, true, false);
                MapTip("Continue or pause your bands carrying out the chosen direction. New decisions appear when needed.");
            }
            else if (AutomaticMode)
            {
                Button(g, "Take control [P]", 1280, 868, 288, 43, ToggleAutoplay, true, false);
                MapTip("Stop Automatic and return to Manual.");
            }
            else
            {
                DrawMapEndTurn(g, new RectangleF(1280, 868, 288, 43));
                MapTip("End turn [Space]\nResolve supplies, growth and encounters, then restore each band's actions.");
            }
        }

        private void DrawMapActionIcons(Graphics g)
        {
            Band actor = CurrentOrderBand;
            bool unavailable = SemiautomaticMode || CurrentOrderActions <= 0 || game.TribesEnabled && !HasCommandBand;
            string availability = SemiautomaticMode ? "\nSemiautomatic: choose story directions, or switch to Manual to give this order." : game.TribesEnabled && !HasCommandBand ? "\nSelect your band's banner first." : CurrentOrderActions <= 0 ? "\nNo actions left: select another band or end the turn." : "";
            MapIconButton(g, "leaf", CampaignActionBounds(0), delegate { CloseMapMenus(); Command("forage"); }, false, unavailable,
                "Gather food [F] / 1 action\nGather at your band's current place. Repeated gathering depletes the ground." + availability);
            MapIconButton(g, "move", CampaignActionBounds(1), delegate { CloseMapMenus(); MoveSelection(); }, false, unavailable,
                "Move / meet [M]\n" + MapTravelHelp() + availability);
            MapIconButton(g, "hunt", CampaignActionBounds(2), delegate { CloseMapMenus(); ChooseAttack(); }, false, unavailable,
                (game.Rules == SimulationRules.MobileUnits ? "Attack [A] / 1 action here; 1\u20132 when approaching\nReview an attack on the selected animal group or band. The review shows the exact action and travel food cost. Combat can cost lives." :
                    "Hunt [A] / 1 action\nHunt an animal group at your band's place. The outcome can cost lives.") + availability);
            MapIconButton(g, "heart", CampaignActionBounds(3), delegate { CloseMapMenus(); ChooseBefriend(); }, false, unavailable,
                (game.Rules == SimulationRules.MobileUnits ? "Befriend [B] / 1 action here; 1\u20132 when approaching\nReview the exact action, travel food and offering costs before approaching the selected animal group. Danger and trust depend on that group." :
                    "Befriend [B] / 1 action and 18 food\nApproach nearby wolves or aurochs. Keep one turn of food for your people beyond the offering.") + availability);
            bool campWoodMissing = game.WoodEnabled && actor.Wood < WoodEconomy.CampCost;
            string campReason = availability.Length > 0 ? availability : actor.Settled ? "\nYour band already has a camp here." : actor.Food < 30 ? "\nYou need at least 30 food reserves." : campWoodMissing ? "\nGather wood first: a camp needs " + WoodEconomy.CampCost + " wood." : "";
            MapIconButton(g, "camp", CampaignActionBounds(4), delegate { CloseMapMenus(); Command("camp"); }, false, unavailable || actor.Settled || actor.Food < 30 || campWoodMissing,
                "Make camp / 1 action, 30 food" + (game.WoodEnabled ? " and " + WoodEconomy.CampCost + " wood" : "") + "\nBuild a camp here. It produces food at turn end; moving leaves it behind. Firewood is a separate turn-end expense." + campReason);
            string splitReason = availability.Length > 0 ? availability : actor.Population < 80 ? "\nNeeds at least 80 people; this band has " + actor.Population + "." :
                actor.Food < game.Upkeep(actor) * 2 ? "\nNeeds food reserves of at least " + (game.Upkeep(actor) * 2).ToString("0") + "." : "";
            MapIconButton(g, "branch", CampaignActionBounds(5), delegate { CloseMapMenus(); Command("split"); }, false, unavailable || actor.Population < 80 || actor.Food < game.Upkeep(actor) * 2,
                (game.TribesEnabled ? "Form a tribal band / 1 action\n" : "Form a daughter band / 1 action\n") +
                "Needs 80 people, food for twice upkeep, and a safe neighboring home. One-third of your people leave with their share of supplies." + splitReason);
            bool saltHere = game.SaltEnabled && SaltEconomy.Source(game, actor.CellId) != SaltSource.None;
            bool gatherSalt = !SemiautomaticMode && HasCommandBand && SaltEconomy.CanGather(game, actor);
            string saltTip = !game.SaltEnabled ? "Salt resources\nOpen the salt ledger to enable salt needs in this older story." :
                !HasCommandBand ? "Gather salt / 1 action\nSelect your band at a coastal salt source or salt spring. Click to find known sources." :
                !saltHere ? "Gather salt / 1 action\nNo source at this band's place. Click to find a known coastal salt source or salt spring." :
                "Gather salt / 1 action\nCollect " + SaltEconomy.GatherYield(game, actor).ToString("0.0") + " salt here: five turns at the band's current size." + availability;
            MapIconButton(g, "salt", CampaignActionBounds(6), delegate
                { CloseMapMenus(); if (!SemiautomaticMode && HasCommandBand && SaltEconomy.CanGather(game, CurrentOrderBand)) GatherHouseholdSalt(); else OpenSaltEconomy(); },
                gatherSalt, false, SemiautomaticMode ? "Salt ledger\nRead reserves and known sources. Switch to Manual to gather directly." : saltTip);
            MapIconButton(g, "wood", CampaignActionBounds(7), delegate
                { CloseMapMenus(); if (!SemiautomaticMode && HasCommandBand && WoodEconomy.CanGather(game, CurrentOrderBand)) GatherHouseholdWood(); else OpenWoodEconomy(); },
                false, false, SemiautomaticMode ? "Wood ledger\nRead reserves, fuel needs and available wood. Switch to Manual to gather directly." : WoodOrderTip());
        }

        private void MapIconButton(Graphics g, string icon, RectangleF bounds, Action click, bool active, bool disabled, string tip)
        {
            bool hover = bounds.Contains(hoverPoint);
            Color fill = active ? Color.FromArgb(69, 67, 47) : hover ? Color.FromArgb(40, 52, 53) : Color.FromArgb(29, 41, 45);
            if (Art.PaperMode)
            {
                using (GraphicsPath shape = new GraphicsPath())
                { shape.AddEllipse(bounds); MapPaper.Shape(g, shape, bounds, active, hover); }
            }
            else if (page == 0)
            {
                using (Brush wash = new SolidBrush(Color.FromArgb(active || hover ? 246 : 220, fill))) g.FillEllipse(wash, bounds);
                using (Pen edge = new Pen(active || hover ? Art.Gold : Color.FromArgb(130, Border), active ? 1.5f : .8f)) g.DrawEllipse(edge, bounds);
            }
            else
            {
                Art.Fill(g, fill, bounds.X, bounds.Y, bounds.Width, bounds.Height);
                using (Pen edge = new Pen(active || hover ? Art.Gold : Border, 1)) g.DrawRectangle(edge, bounds.X, bounds.Y, bounds.Width, bounds.Height);
            }
            Color ink = disabled ? Art.PaperMode ? MapPaper.DisabledInk : Color.FromArgb(108, 117, 112) : Art.Gold;
            float glyphSize = Math.Min(25, bounds.Height - 13);
            RectangleF glyph = new RectangleF(bounds.X + (bounds.Width - glyphSize) / 2, bounds.Y + (bounds.Height - glyphSize) / 2, glyphSize, glyphSize);
            if (icon == "explore") MapRenderer.DrawFrontierInterestIcon(g, glyph);
            else if (icon == "salt") DrawSaltGlyph(g, glyph, ink);
            else if (icon == "wood") DrawWoodGlyph(g, glyph, ink);
            else Art.Icon(g, icon, glyph.X, glyph.Y, glyph.Width, ink);
            buttons.Add(new UiButton(bounds, delegate { if (disabled) { status = tip.Replace('\n', ' '); Invalidate(); } else click(); }) { Tip = tip });
        }

        private void DrawMapMenus(Graphics g)
        {
            RectangleF bounds = MapMenuBounds;
            if (bounds.IsEmpty || BlockingSheet) return;
            // Remove click targets before painting the opaque menu, including
            // any card target underneath it. Empty menu space is consumed by
            // the parent form's MapMenuContains input guard.
            buttons.RemoveAll(button => button.Bounds.IntersectsWith(bounds));
            Art.Fill(g, Art.PaperMode ? Color.FromArgb(24, MapPaper.Ink) : Color.FromArgb(82, 0, 0, 0), bounds.X + 5, bounds.Y + 5, bounds.Width, bounds.Height);
            Art.Panel(g, bounds, Color.FromArgb(23, 35, 40), true);
            Typography.Line(g, mapMenu == 1 ? "Read the map" : mapMenu == 4 ? "Reading the landscape" : "Watching the story", new RectangleF(bounds.X + 19, bounds.Y + 10, bounds.Width - 74, 31), 25, Art.Ink, TypeRole.Heading, true);
            Button(g, "\u00d7", bounds.Right - 43, bounds.Y + 11, 28, 28, delegate { CloseMapMenus(); buttons.Clear(); Invalidate(); }, false, false);
            MapTip("Close this menu. Escape also closes map menus.");
            if (mapMenu == 1) DrawMapViewMenu(g, bounds);
            else if (mapMenu == 4) DrawLandscapeGuide(g, bounds);
            else DrawMapWatchMenu(g, bounds);
        }

        private void DrawMapViewMenu(Graphics g, RectangleF bounds)
        {
            string[] layers = { "Terrain", "Food", "Regions", "Speech", "Polities" };
            string[] descriptions = {
                "Landforms, forests, waters and the paths between them.",
                "Compare the gathering potential of different places.",
                "Connected geographic regions of many hexes. Borders and colors mark landscape regions, not polity ownership.",
                "The languages carried by known peoples.",
                "Peoples and their presence. Land has no owned borders."
            };
            for (int i = 0; i < layers.Length; i++)
            {
                int layer = i;
                Button(g, layers[i], bounds.X + 20 + i % 2 * 218, bounds.Y + 56 + i / 2 * 44, 202, 36,
                    delegate { map.Layer = layer; Invalidate(); }, map.Layer == i, false);
                MapTip(descriptions[i]);
            }
            Button(g, map.PlaceLabels ? "Place names: shown" : "Place names: on hover", bounds.X + 238, bounds.Y + 144, 202, 36,
                delegate { map.PlaceLabels = !map.PlaceLabels; Invalidate(); }, map.PlaceLabels, false);
            MapTip("Keep terrain clear with names on hover, or display every remembered place name on the map. Your people's naming record is unchanged.");
            Art.Rule(g, bounds.X + 20, bounds.Y + 191, bounds.Width - 40);
            Button(g, map.Grid ? "Grid: visible" : "Grid: hidden", bounds.X + 20, bounds.Y + 202, 202, 34,
                delegate { map.Grid = !map.Grid; Invalidate(); }, map.Grid, false);
            MapTip("Show or hide the borders of each hex. The same places and movement rules remain.");
            Button(g, map.Fog ? "Known land only" : "Whole atlas", bounds.X + 238, bounds.Y + 202, 202, 34,
                delegate { map.Fog = !map.Fog; SynchronizeUnitSelection(); Invalidate(); }, map.Fog, false);
            MapTip("Show explored land or the complete atlas. Atlas view does not discover places for your people.");
            Typography.Draw(g, descriptions[Math.Max(0, Math.Min(4, map.Layer))], new RectangleF(bounds.X + 20, bounds.Y + 252, bounds.Width - 40, 37), 16, Art.Muted, TypeRole.Annotation);
        }

        private void DrawMapWatchMenu(Graphics g, RectangleF bounds)
        {
            Button(g, AutomaticMode ? "Automatic: on / Take control [P]" : "Start Automatic mode",
                bounds.X + 20, bounds.Y + 51, bounds.Width - 40, 34,
                delegate { CloseMapMenus(); if (AutomaticMode) ToggleAutoplay(); else SelectAutomaticMode(); }, AutomaticMode, false);
            MapTip("Automatic handles every band's actions and advances turns without story choices. Press P to return to Manual. Settings below control speed and interruptions.");
            Typography.Label(g, SemiautomaticMode ? "Story pace" : "Autoplay pace", new RectangleF(bounds.X + 20, bounds.Y + 98, bounds.Width - 40, 22), 11.5f, Art.Gold, .5f);
            for (int i = 0; i < AutoplaySpeeds.Length; i++)
            {
                int speed = i;
                Button(g, (i == 0 ? "1\u00d7" : i == 1 ? "3\u00d7" : "10\u00d7"), bounds.X + 20 + i * 146, bounds.Y + 125, 138, 34,
                    delegate { SetAutoplaySpeed(speed); }, autoplaySpeed == i, false);
                MapTip("Change the interval between autoplay decisions. This does not change how much time a turn represents.");
            }
            Button(g, pauseOnEvents ? "Major events: pause to read" : "Major events: keep flowing", bounds.X + 20, bounds.Y + 175, bounds.Width - 40, 36,
                delegate { pauseOnEvents = !pauseOnEvents; Invalidate(); }, pauseOnEvents, false);
            MapTip("Choose whether autoplay pauses for major events. Every event remains in the archive either way.");
            Button(g, followAutoplay ? "Follow your band: on" : "Follow your band: off", bounds.X + 20, bounds.Y + 221, bounds.Width - 40, 36,
                delegate
                {
                    followAutoplay = !followAutoplay;
                    if (autoplay && followAutoplay) { selected = CurrentOrderBand.CellId; selectedAnimalId = -1; inspectedBandId = CurrentOrderBand.Id; inspectorPage = 1; map.Focus(game.World.Cells[selected]); }
                    Invalidate();
                }, followAutoplay, false);
            MapTip("Keep the map centered on your people as autoplay moves them. Turn this off to watch another place.");
            Art.Rule(g, bounds.X + 20, bounds.Y + 278, bounds.Width - 40);
            Typography.Label(g, "Adviser frequency", new RectangleF(bounds.X + 20, bounds.Y + 293, bounds.Width - 40, 23), 12, Art.Gold, .5f);
            DrawAdviserFrequencyChoices(g, bounds.X + 20, bounds.Y + 326, bounds.Width - 33);
            Typography.Draw(g, FrequencyDescription(adviserFrequency), new RectangleF(bounds.X + 20, bounds.Y + 376, bounds.Width - 40, 91), 19, Art.Ink, TypeRole.Annotation);
            Typography.Draw(g, "Remembered between games. C opens the council.\nEscape closes this menu. No action is spent.", new RectangleF(bounds.X + 20, bounds.Y + 473, bounds.Width - 40, 45), 16, Art.Muted, TypeRole.Annotation);
            if (game.Rules == SimulationRules.Classic)
            {
                Button(g, "Enable unit encounters", bounds.X + 20, bounds.Y + 527, bounds.Width - 40, 36,
                    delegate { CloseMapMenus(); Command("enable-encounters"); }, false, false);
                MapTip("Enable moving animal groups and targeted encounters in this older story. The upgrade is recorded for replay.");
            }
        }
    }
}
