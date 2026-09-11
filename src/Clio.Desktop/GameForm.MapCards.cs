using System;
using System.Drawing;
using System.Linq;
using Clio.Simulation;

namespace Clio.Desktop
{
    public sealed partial class GameForm
    {
        private bool mapCardExpanded;
        private RectangleF MapSelectionBounds { get { return new RectangleF(1194, 228, 368, unitStackOpen ? 484 : mapCardExpanded ? 524 : 256); } }

        private void ToggleMapCardDetails()
        {
            if (!mapSelectionOpen || unitStackOpen || BlockingSheet) return;
            mapCardExpanded = !mapCardExpanded;
            HideMapHover(); buttons.Clear(); Invalidate();
        }

        private bool MapCardKnown(int cell)
        { return cell >= 0 && cell < game.World.Cells.Length && game.Explored.Contains(cell); }

        private Band MapCardBand(int id)
        {
            Band band = game.Bands.FirstOrDefault(b => b.Id == id);
            return band != null && MapCardKnown(band.CellId) && band.Population > 0 ? band : null;
        }

        private Beast MapCardAnimal(int id)
        {
            Beast animal = game.Beasts.FirstOrDefault(b => b.Id == id);
            return animal != null && MapCardKnown(animal.CellId) && animal.Count > 0 ? animal : null;
        }

        private void DrawMapHover(Graphics g, int kind, int id, PointF anchor)
        {
            if (page != 0 || BlockingSheet || id < 0) return;
            if (kind >= MapRenderer.FoodOpportunity) { DrawOpportunityNote(g, kind, id, anchor); return; }
            string title = "Beyond the known paths";
            string summary = "Not discovered by your people.", hint = "Click for details";
            Color summaryInk = Art.Ink;
            if (kind == 0)
            {
                if (id >= game.World.Cells.Length) return;
                Cell cell = game.World.Cells[id];
                if (MapCardKnown(id))
                {
                    title = game.Place(id);
                    summary = cell.IsLand ? cell.Terrain + " · +" + game.ForageYield(id, CurrentOrderBand).ToString("0") + " food / gather" : "Open water · No land route";
                    SaltSource salt = MapCardSaltSource(id);
                    if (salt != SaltSource.None) summary = cell.Terrain + " · " + SaltSourceLabel(salt);
                    if (cell.IsLand && game.Rules == SimulationRules.MobileUnits && EncounterRules.HostileAt(game, id, CurrentOrderBand.Id))
                    {
                        summary = cell.Terrain + " · Hostile groups here"; summaryInk = BandPanelWarning;
                    }
                }
                else
                {
                    title = map.Fog ? "Beyond the known paths" : cell.Terrain.ToString();
                }
            }
            else if (kind == 1)
            {
                Band band = MapCardBand(id);
                if (band != null)
                {
                    title = band.Name;
                    summary = band.Population.ToString("N0") + " people · " + (game.CanControlBand(band.Id) ? UnitsActionCount(band.Id) : "Independent people");
                    hint = game.CanControlBand(band.Id) ? "Click to select · Details on demand" : "Click for details and encounters";
                }
            }
            else if (kind == 2)
            {
                Beast animal = MapCardAnimal(id);
                if (animal != null)
                {
                    title = AnimalUnitName(animal);
                    summary = animal.Count.ToString("N0") + (animal.Count == 1 ? " animal" : " animals");
                    if (game.Rules == SimulationRules.MobileUnits)
                    {
                        UnitProfile unit = EncounterRules.Animal(game, animal);
                        summary += " · " + UnitTemperament(unit);
                        if (unit.Hostile) summaryInk = BandPanelWarning;
                    }
                    else summary += animal.Domestic ? " · Domestic" : " · Wild";
                    hint = "Click for details and encounters";
                }
            }
            else return;

            RectangleF mapArea = map.Bounds;
            float x = anchor.X + 21, y = anchor.Y + 21;
            const float width = 330;
            const float height = 111;
            if (x + width > mapArea.Right - 12) x = anchor.X - width - 21;
            if (y + height > mapArea.Bottom - 12) y = anchor.Y - height - 21;
            x = Math.Max(mapArea.Left + 12, Math.Min(x, mapArea.Right - width - 12));
            y = Math.Max(mapArea.Top + 12, Math.Min(y, mapArea.Bottom - height - 12));
            RectangleF box = new RectangleF(x, y, width, height);
            if (mapSelectionOpen && box.IntersectsWith(MapSelectionBounds))
            {
                x = Math.Max(mapArea.Left + 12, MapSelectionBounds.Left - width - 14);
                box = new RectangleF(x, y, width, height);
            }
            Art.Fill(g, Art.PaperMode ? Color.FromArgb(20, MapPaper.Ink) : Color.FromArgb(54, 0, 0, 0), box.X + 4, box.Y + 5, box.Width, box.Height);
            Art.Panel(g, box, Color.FromArgb(24, 35, 38), false);
            Art.Line(g, Art.Gold, 1, x + 14, y, x + 62, y);
            Typography.Line(g, title, new RectangleF(x + 13, y + 10, 304, 34), 25, Art.Ink, TypeRole.Heading, true);
            Typography.Line(g, summary, new RectangleF(x + 15, y + 49, 300, 24), 15, summaryInk, TypeRole.Body, true);
            Typography.Line(g, hint, new RectangleF(x + 15, y + 80, 300, 21), 14, Art.Gold, TypeRole.Annotation, true);
        }

        private string[] MapOpportunityText(int kind, int cell)
        {
            if (!MapCardKnown(cell) || !map.IsOpportunityVisible(game, kind, cell)) return null;
            string title, meaning, action;
            if (kind == MapRenderer.ExplorationOpportunity)
            {
                int unknown = MapRenderer.FrontierUnknownNeighbors(game, cell);
                title = "Exploration opportunity";
                meaning = "Blue compass: known land beside " + unknown + (unknown == 1 ? " unexplored hex." : " unexplored hexes.");
                action = "Travel here to extend your people's map. The compass marks a place to explore.";
            }
            else if (kind == MapRenderer.FoodOpportunity)
            {
                title = "Food opportunity";
                meaning = "Food gathered per action: +" + game.ForageYield(cell, CurrentOrderBand).ToString("0") + ", " + MapRenderer.GatheringValue(game, CurrentOrderBand, cell).ToString("0.0") + " times this band's food needs.";
                action = "Move here, then use Gather food. Moving alone collects no food.";
            }
            else if (kind == MapRenderer.SaltOpportunity)
            {
                title = "Salt source";
                meaning = SaltSourceLabel(MapCardSaltSource(cell)) + ". Your people remember this deposit.";
                action = "Bring this band here, then use Gather salt. Gathering costs 1 action.";
            }
            else return null;
            string travel;
            if (game.IsOver) travel = "This story has ended. You can still inspect the remembered land.";
            else if (!HasCommandBand) travel = "Select one of your bands to see its route and movement cost.";
            else if (cell == CurrentOrderBand.CellId) travel = "Your selected band is here. " + game.ActionsFor(CurrentOrderBand.Id) + " actions remain.";
            else if (!game.World.Cells[CurrentOrderBand.CellId].Neighbors.Contains(cell)) travel = "Beyond one step. Reach this place through adjacent known land.";
            else
            {
                int cost = MapTravelCost(CurrentOrderBand, cell), available = game.ActionsFor(CurrentOrderBand.Id);
                if (available < cost) travel = "Move: " + cost + (cost == 1 ? " action" : " actions") + "; " + available + " available. End the turn or choose another band.";
                else if (RightMoveReason(cell).Length > 0) travel = RightMoveReason(cell);
                else travel = "Right-click: move for " + cost + (cost == 1 ? " action" : " actions") + ". " + available + " available.";
            }
            return new[] { title, game.Place(cell), meaning, action, travel };
        }

        private RectangleF OpportunityNoteBounds(PointF anchor)
        {
            const float width = 350, height = 119;
            RectangleF area = map.Bounds;
            float x = anchor.X + 23, y = anchor.Y + 23;
            if (x + width > area.Right - 12) x = anchor.X - width - 23;
            if (y + height > area.Bottom - 12) y = anchor.Y - height - 23;
            x = Math.Max(area.Left + 12, Math.Min(x, area.Right - width - 12));
            y = Math.Max(area.Top + 12, Math.Min(y, area.Bottom - height - 12));
            if (mapSelectionOpen && new RectangleF(x, y, width, height).IntersectsWith(MapSelectionBounds))
                x = Math.Max(area.Left + 12, MapSelectionBounds.Left - width - 14);
            return new RectangleF(x, y, width, height);
        }

        private void DrawOpportunityNote(Graphics g, int kind, int cell, PointF anchor)
        {
            string[] text = MapOpportunityText(kind, cell);
            if (text == null) return;
            RectangleF box = OpportunityNoteBounds(anchor);
            Color ink = kind == MapRenderer.FoodOpportunity ? MapRenderer.FoodInterestInk : kind == MapRenderer.ExplorationOpportunity ? MapRenderer.FrontierInterestInk : Art.PaperMode ? MapPaper.Blue : LandscapeSalt;
            Art.Fill(g, Art.PaperMode ? Color.FromArgb(20, MapPaper.Ink) : Color.FromArgb(64, 0, 0, 0), box.X + 4, box.Y + 5, box.Width, box.Height);
            Art.Panel(g, box, Color.FromArgb(24, 35, 38), false);
            Art.Line(g, ink, 2, box.X + 15, box.Y, box.Right - 15, box.Y);
            DrawLandscapeMark(g, kind == MapRenderer.FoodOpportunity ? 0 : kind == MapRenderer.SaltOpportunity ? 1 : 2, new RectangleF(box.X + 16, box.Y + 13, 19, 19), ink);
            Typography.Line(g, text[0], new RectangleF(box.X + 43, box.Y + 9, 289, 32), 25, Art.Ink, TypeRole.Heading, true);
            string summary = kind == MapRenderer.FoodOpportunity ? "+" + game.ForageYield(cell, CurrentOrderBand).ToString("0") + " food per Gather action" :
                kind == MapRenderer.SaltOpportunity ? SaltSourceLabel(MapCardSaltSource(cell)) + " · Gather salt here" :
                MapRenderer.FrontierUnknownNeighbors(game, cell) + " nearby hexes to discover";
            Typography.Line(g, summary, new RectangleF(box.X + 16, box.Y + 49, 318, 24), 16, ink, TypeRole.Body, true);
            Typography.Line(g, "Map marker · Click for details", new RectangleF(box.X + 16, box.Y + 85, 318, 21), 14, Art.Muted, TypeRole.Annotation, true);
        }

        private string MapPlaceCaution(int cell)
        {
            if (!MapCardKnown(cell) || !game.World.Cells[cell].IsLand) return "";
            if (game.Rules != SimulationRules.MobileUnits) return "Inspect wildlife before approaching";
            var outsiders = game.Bands.Where(b => b.CellId == cell && b.Population > 0 && !game.CanControlBand(b.Id)).Select(b => EncounterRules.Band(game, b));
            var wildlife = game.Beasts.Where(b => b.CellId == cell && b.Count > 0 && (!b.Domestic || !game.CanControlBand(b.OwnerId))).Select(b => EncounterRules.Animal(game, b));
            UnitProfile[] groups = outsiders.Concat(wildlife).ToArray();
            if (groups.Length == 0) return "No outside groups observed here";
            bool stronger = CurrentOrderBand.Population > 0 && groups.Max(p => p.Strength) > EncounterRules.Band(game, CurrentOrderBand).Strength * 1.05;
            if (EncounterRules.HostileAt(game, cell, CurrentOrderBand.Id)) return "Danger: hostile groups here";
            return stronger ? "Strong groups here · Review approach" : "Groups here · Review before entering";
        }

        private void DrawMapSelectionCard(Graphics g)
        {
            if (!mapSelectionOpen || page != 0 || BlockingSheet) return;
            if (unitStackOpen) { DrawUnitStackCard(g); return; }
            RectangleF bounds = MapSelectionBounds;
            buttons.RemoveAll(b => b.Bounds.IntersectsWith(bounds));
            Art.Fill(g, Art.PaperMode ? Color.FromArgb(23, MapPaper.Ink) : Color.FromArgb(65, 0, 0, 0), bounds.X - 4, bounds.Y + 5, bounds.Width + 8, bounds.Height + 3);
            Art.Panel(g, bounds, Color.FromArgb(24, 35, 38), true);
            if (!mapCardExpanded) DrawMapCompactCard(g, bounds);
            else if (inspectorPage == 1) DrawMapBandCard(g, MapCardBand(inspectedBandId));
            else if (inspectorPage == 2) DrawMapAnimalCard(g, MapCardAnimal(selectedAnimalId));
            else DrawMapPlaceCard(g);
            Button(g, mapCardExpanded ? "Less detail" : "More details", bounds.X + 19, bounds.Bottom - 36, bounds.Width - 38, 25, ToggleMapCardDetails, false, false);
            MapTip(mapCardExpanded ? "Return to the compact summary." : "Expand the complete record, explanations and additional actions.");
            Button(g, "×", 1519, 241, 27, 27, CloseMapSelection, false, false);
            MapTip("Close this record and return to the map. Escape also closes it.");
        }

        private void MapCardHeading(Graphics g, string label, string title, string subtitle)
        {
            Typography.Label(g, label, new RectangleF(1213, 242, 291, 23), 11, Art.Gold, .65f);
            FittedTitle(g, title, new RectangleF(1210, 279, 334, 57), 31, Art.Ink);
            Typography.Line(g, subtitle, new RectangleF(1213, 335, 331, 25), 16, Art.Muted, TypeRole.Annotation, true);
            Art.Rule(g, 1213, 370, 331);
        }

        private void MapCardMetric(Graphics g, float x, string label, string value, Color color)
        {
            Typography.Label(g, label, new RectangleF(x, 382, 161, 23), 10.5f, Art.Muted, .5f);
            Typography.Line(g, value, new RectangleF(x - 2, 401, 165, 41), 34, color, TypeRole.Number, true);
        }

        private void MapCardPair(Graphics g, string label, string value, float y, Color color)
        {
            Typography.Line(g, label, new RectangleF(1213, y, 169, 26), 15, Art.Muted, TypeRole.Body, true);
            Typography.Line(g, value, new RectangleF(1388, y, 156, 26), 18, color, TypeRole.Number, true, StringAlignment.Far);
        }

        private string MapCardRoute(int cell)
        {
            if (MapCardKnown(cell) && !game.World.Cells[cell].IsLand) return "No land route";
            if (cell == CurrentOrderBand.CellId) return "Your selected band is here";
            if (game.TerrainTravelEnabled && game.World.Cells[CurrentOrderBand.CellId].Neighbors.Contains(cell) && MapCardKnown(cell))
            {
                int cost = MapTravelCost(CurrentOrderBand, cell);
                return MapTravelKind(CurrentOrderBand.CellId, cell) + " · " + cost + (cost == 1 ? " action" : " actions");
            }
            return game.World.Cells[CurrentOrderBand.CellId].Neighbors.Contains(cell) ? "Adjacent to your selected band" : "Farther along the paths";
        }

        private void DrawMapPlaceCard(Graphics g)
        {
            if (selected < 0 || selected >= game.World.Cells.Length) { DrawMapLostCard(g, false); return; }
            Cell cell = game.World.Cells[selected];
            if (!MapCardKnown(selected))
            {
                MapCardHeading(g, map.Fog ? "Unexplored" : "Atlas only", map.Fog ? "Beyond the paths" : cell.Terrain.ToString(), "This hex has not been discovered");
                Art.Icon(g, "globe", 1357, 413, 45, Art.Gold);
                Typography.Draw(g, "Your people have no account of this place. The atlas does not discover it or give it a remembered name.", new RectangleF(1213, 489, 331, 94), 20, Art.Ink, TypeRole.Annotation);
                Typography.Draw(g, "Travel toward the edge of the known land to extend their map.", new RectangleF(1213, 626, 331, 60), 17, Art.Muted, TypeRole.Body);
                return;
            }
            MapCardHeading(g, "A place remembered", game.Place(selected), cell.Terrain + "  ·  Region " + cell.RegionId);
            MapCardMetric(g, 1213, "Gathering / action", cell.IsLand ? "+" + game.ForageYield(selected, CurrentOrderBand).ToString("0") : "—", Art.Gold);
            MapCardMetric(g, 1384, "Ground recovery", cell.IsLand ? ((1 - game.Depletion[selected]) * 100).ToString("0") + "%" : "—", LedgerGreen);
            SaltSource source = MapCardSaltSource(selected);
            string climate = cell.Temperature < .25 ? "Cold" : cell.Temperature > .72 ? "Hot" : "Temperate";
            if (source == SaltSource.None)
            {
                MapCardPair(g, "Climate", climate, 452, Art.Ink);
                MapCardPair(g, "Moisture", (cell.Moisture * 100).ToString("0") + "%", 482, Art.Ink);
            }
            else
            {
                MapCardPair(g, "Salt source", SaltSourceLabel(source), 452, Art.Gold);
                MapCardPair(g, "Climate / moisture", climate + " · " + (cell.Moisture * 100).ToString("0") + "%", 482, Art.Ink);
            }
            Typography.Line(g, cell.IsLand ? MapCardRoute(selected) : "Open water · Land travel ends here", new RectangleF(1213, 516, 331, 27), 17, Art.Gold, TypeRole.Annotation, true);
            PlaceKnowledge place = game.ObserverKnownPlace(selected);
            string memory = place == null ? "A name carried in this story's earlier map." :
                (place.Acquisition == PlaceAcquisition.Discovered ? "Named in your people's own tongue" : place.Acquisition == PlaceAcquisition.Shared ? "A foreign name learned through contact" : "A name inherited from the parent people") + " in " + Timeline.Label(game, place.LearnedTurn).ToLowerInvariant() + ".";
            int frontierCount = MapRenderer.FrontierUnknownNeighbors(game, selected);
            double gatheringValue = MapRenderer.GatheringValue(game, CurrentOrderBand, selected);
            string interest = gatheringValue >= MapRenderer.StrongGatheringValue ? "Gather: " + gatheringValue.ToString("0.0") + "× needs" : "";
            if (frontierCount > 0) interest += (interest.Length > 0 ? " · " : "Frontier: ") + frontierCount + " unseen neighbors";
            Typography.Draw(g, interest.Length == 0 ? memory : interest + ".\n" + memory, new RectangleF(1213, 552, 331, 62), interest.Length == 0 ? 18 : 15.5f, Art.Muted, TypeRole.Annotation);
            if (source == SaltSource.None)
            {
                Button(g, "Resources", 1213, 625, 161, 31, delegate { ClearMapTransient(); OpenEconomy(2); }, false, false);
                MapTip("Open Economy / Resources to inspect gathering, supplies and companion benefits.");
            }
            else
            {
                Button(g, "Salt reserves", 1213, 625, 161, 31, delegate { ClearMapTransient(); OpenSaltEconomy(); }, false, false);
                MapTip("Read salt reserves, household needs and the sources known to your people.");
            }
            Button(g, "Language", 1384, 625, 160, 31, delegate { OpenMapCardLanguage(CurrentOrderBand.LanguageId); }, false, false);
            MapTip("Open Culture / Language to read the words spoken by your people.");
            if (HasCommandBand && cell.IsLand && RightMoveReason(selected).Length == 0)
            {
                int cellId = selected;
                int cost = MapTravelCost(CurrentOrderBand, cellId);
                Button(g, game.TerrainTravelEnabled ? "Move / meet  ·  " + cost + (cost == 1 ? " action" : " actions") : "Move / meet  [M]", 1213, 666, 331, 31, delegate { MapCardMove(cellId); }, true, false);
                MapTip("Move this household to the adjacent hex for " + cost + (cost == 1 ? " action. " : " actions. ") + "An occupied destination opens an encounter review first. Food and salt require a separate gathering action.");
            }
            else if (game.TerrainTravelEnabled && HasCommandBand && cell.IsLand && selected != CurrentOrderBand.CellId && game.World.Cells[CurrentOrderBand.CellId].Neighbors.Contains(selected) && MapTravelCost(CurrentOrderBand, selected) > game.ActionsFor(CurrentOrderBand.Id))
                Typography.Line(g, "Needs " + MapTravelCost(CurrentOrderBand, selected) + " actions · " + game.ActionsFor(CurrentOrderBand.Id) + " left this turn", new RectangleF(1213, 666, 331, 31), 16, Art.Gold, TypeRole.Annotation, true, StringAlignment.Center);
            else if (source != SaltSource.None && selected == CurrentOrderBand.CellId) DrawMapSaltGatherButton(g, selected, 1213, 666, 331);
            else if (source != SaltSource.None)
                Typography.Line(g, "Bring your band here to gather salt", new RectangleF(1213, 666, 331, 31), 16, Art.Muted, TypeRole.Annotation, true, StringAlignment.Center);
            else if (game.TerrainTravelEnabled && cell.IsLand && selected == CurrentOrderBand.CellId && HasCommandBand)
            {
                if (game.ActionsFor(CurrentOrderBand.Id) > 0)
                {
                    int actorId = CurrentOrderBand.Id, cellId = selected;
                    Button(g, "Gather food  +" + game.ForageYield(cellId, CurrentOrderBand).ToString("0"), 1213, 666, 331, 31, delegate
                    {
                        if (!HasCommandBand || CurrentOrderBand.Id != actorId || CurrentOrderBand.CellId != cellId || game.ActionsFor(actorId) <= 0 || !MapCardKnown(cellId)) return;
                        Command("forage");
                    }, true, false);
                    MapTip("Spend one household action gathering here. The forecast includes current conditions and depletion; simply visiting awards no food.");
                }
                else Typography.Line(g, "End the turn to gather here", new RectangleF(1213, 666, 331, 31), 16, Art.Muted, TypeRole.Annotation, true, StringAlignment.Center);
            }
        }

        private void DrawMapBandCard(Graphics g, Band band)
        {
            if (band == null) { DrawMapLostCard(g, true); return; }
            bool own = game.CanControlBand(band.Id);
            MapCardHeading(g, own ? game.TribesEnabled && game.TribeLeaderBand != null && game.TribeLeaderBand.Id == band.Id ? "Your tribal leader" : "Your people" : "An independent people", band.Name, own && game.TribesEnabled ? "" : band.Ancestry + "  ·  " + (band.Settled ? "Established hearth" : "Wandering band"));
            if (own && game.TribesEnabled)
            {
                Typography.Line(g, "Find the tribe's reunion camp  ›", new RectangleF(1213, 335, 331, 25), 16, Art.Gold, TypeRole.Annotation, true);
                int reunionBand = band.Id;
                buttons.Add(new UiButton(new RectangleF(1213, 335, 331, 25), delegate { OpenTribeReunion(reunionBand); }) { Tip = "Locate the leading band's present tile while keeping this household selected for movement." });
            }
            MapCardMetric(g, 1213, "People", band.Population.ToString("N0"), Art.Ink);
            MapCardMetric(g, 1384, "Food reserves", band.Food.ToString("0"), Art.Gold);
            MapCardPair(g, "Cohesion", (band.Cohesion * 100).ToString("0") + "%", 452, Art.Ink);
            bool encounters = game.Rules == SimulationRules.MobileUnits;
            UnitProfile unit = encounters ? EncounterRules.Band(game, band) : null;
            MapCardPair(g, "Health", encounters ? unit.CurrentHealth + " / " + unit.MaxHealth : "Not recorded", 482, LedgerGreen);
            MapCardPair(g, "Fighting strength", encounters ? unit.Strength.ToString("0.0") : "Not recorded", 512, Art.Gold);
            LanguageProfile language = game.Languages.First(l => l.Id == band.LanguageId);
            if (own)
            {
                string account = game.SaltEnabled ? "Speaks " + language.Name + ". Salt reserve: " + band.Salt.ToString("0.0") + " / " + SaltEconomy.ReserveTurns(band).ToString("0.0") + " turns of need." :
                    "Speaks " + language.Name + ". Your orders guide this band; its domestic companions travel with it.";
                if (game.TribesEnabled)
                {
                    Typography.Line(g, UnitsActionCount(band.Id) + (game.SaltEnabled ? "  ·  Salt: " + SaltEconomy.ReserveTurns(band).ToString("0.0") + " turns" : "  ·  " + language.Name), new RectangleF(1213, 548, 331, 25), 17, Art.Gold, TypeRole.Annotation, true);
                    Typography.Draw(g, UnitsReunionStatus(band.Id, false), new RectangleF(1213, 575, 331, 39), 15, Art.Muted, TypeRole.Annotation);
                    int reunionBand = band.Id;
                    buttons.Add(new UiButton(new RectangleF(1213, 573, 331, 43), delegate { OpenTribeReunion(reunionBand); }) { Tip = game.BandPersonalitiesEnabled ? "Find the leader's current hex. Reunite there before this band's personality-dependent deadline, shown in Units." : "Find the leading band's current camp. Return this household there to renew contact before sixteen turns apart." });
                }
                else Typography.Draw(g, account, new RectangleF(1213, 555, 331, 57), 18, Art.Muted, TypeRole.Annotation);
                int householdId = band.Id;
                Button(g, "Economy", 1213, 625, 161, 31, delegate { if (!game.CanControlBand(householdId)) return; ArmMapCommandBand(householdId); ClearMapTransient(); OpenEconomy(0); }, false, false);
                MapTip("Read this band's population, food reserves and needs per turn. Each band carries and spends its own food.");
                Button(g, "Units", 1384, 625, 160, 31, OpenMapCardUnits, false, false);
                MapTip("Open the roster with your band selected. Companions are listed beside it.");
                int languageId = band.LanguageId;
                if (game.SaltEnabled)
                {
                    Button(g, "Salt reserves", 1213, 666, 161, 31, delegate { if (!game.CanControlBand(householdId)) return; ArmMapCommandBand(householdId); ClearMapTransient(); OpenSaltEconomy(); }, false, false);
                    MapTip("Read your household's salt reserve, supply and use per turn.");
                    if (MapCardSaltSource(band.CellId) != SaltSource.None)
                    {
                        if (HasCommandBand && CurrentOrderBand.Id == householdId) DrawMapSaltGatherButton(g, band.CellId, 1384, 666, 160);
                        else
                        {
                            Button(g, "Select this band", 1384, 666, 160, 31, delegate { InspectRosterUnit(UnitKind.Band, householdId); }, false, false);
                            MapTip("Select this household before gathering salt from its current source.");
                        }
                    }
                    else
                    {
                        Button(g, "Language", 1384, 666, 160, 31, delegate { OpenMapCardLanguage(languageId); }, false, false);
                        MapTip("Read this language's vocabulary in Culture / Language.");
                    }
                }
                else
                {
                    Button(g, "Language: " + language.Name, 1213, 666, 331, 31, delegate { OpenMapCardLanguage(languageId); }, false, false);
                    MapTip("Read this language's vocabulary in Culture / Language.");
                }
            }
            else
            {
                string relation = encounters && unit.Hostile ? "Hostile" : "Neutral";
                double understanding = LanguageGenerator.Intelligibility(language, game.Languages.First(l => l.Id == CurrentOrderBand.LanguageId), 0);
                string account = relation + " · " + (understanding * 100).ToString("0") + "% shared understanding.";
                if (encounters) account += " " + EncounterRules.Outlook(game, CurrentOrderBand.Id, UnitKind.Band, band.Id).Summary + ".";
                Typography.Draw(g, account, new RectangleF(1213, 551, 331, 70), 16, Art.Muted, TypeRole.Annotation);
                int languageId = band.LanguageId, id = band.Id;
                Button(g, "Language", 1213, 625, 161, 31, delegate { OpenMapCardLanguage(languageId); }, false, false);
                MapTip("Read this people's known language alongside the other living tongues.");
                Button(g, "Contact & trade", 1384, 625, 160, 31, delegate { ClearMapTransient(); OpenEconomy(3); }, false, false);
                MapTip("Open Economy / Trade to review known peoples and the effects of peaceful contact.");
                if (encounters)
                {
                    Button(g, "Review encounter", 1213, 666, 331, 31, delegate { OpenMapCardEncounter(UnitKind.Band, id); }, true, false);
                    MapTip("Inspect the attack outlook and costs. Opening the review spends no action.");
                }
            }
        }

        private void DrawMapAnimalCard(Graphics g, Beast animal)
        {
            if (animal == null) { DrawMapLostCard(g, true); return; }
            bool own = animal.Domestic && game.CanControlBand(animal.OwnerId), encounters = game.Rules == SimulationRules.MobileUnits;
            bool livestock = game.LivestockEnabled && animal.Domestic && LivestockEconomy.IsLivestock(animal);
            bool ownDogs = game.LivestockEnabled && own && animal.Kind == BeastKind.Wolves;
            UnitProfile unit = encounters ? EncounterRules.Animal(game, animal) : null;
            MapCardHeading(g, own ? livestock ? "Your livestock" : "Your companions" : animal.Domestic ? "Another people's companions" : "A wild animal group", AnimalUnitName(animal), LivestockEconomy.DisplayName(game, animal) + "  ·  Group " + animal.Id);
            MapCardMetric(g, 1213, "Animals", animal.Count.ToString("N0"), Art.Ink);
            MapCardMetric(g, 1384, "Health", encounters ? unit.CurrentHealth + " / " + unit.MaxHealth : "—", LedgerGreen);
            MapCardPair(g, livestock ? "Milk / turn" : "Temperament", livestock ? "+" + LivestockEconomy.MilkFood(game, animal).ToString("0.0") + " food" : encounters ? UnitTemperament(unit) : animal.Domestic ? "Domestic" : "Wild", 452, livestock ? LedgerGreen : Art.Ink);
            Band dogOwner = ownDogs ? game.Bands.FirstOrDefault(b => b.Id == animal.OwnerId) : null;
            string huntingAid = dogOwner == null ? "0" : (BandEconomy.DomesticEffects(game, dogOwner).HuntingBonus * 100).ToString("0");
            MapCardPair(g, livestock ? "Next slaughter" : ownDogs ? "Band hunting support" : "Fighting strength", livestock ? "+" + LivestockEconomy.MeatFood(game, animal).ToString("0.0") + " food" : ownDogs ? "+" + huntingAid + (encounters ? "% strength" : " points") : encounters ? unit.Strength.ToString("0.0") : "Not recorded", 482, Art.Gold);
            if (animal.Domestic)
                MapCardPair(g, "Care / turn", BandEconomy.AnimalCare(game, animal).ToString("0.0"), 512, Art.Ink);
            else if (encounters)
                MapCardPair(g, LivestockEconomy.CanDomesticate(game, animal) ? "Trust / peaceful chance" : "Domestication", LivestockEconomy.CanDomesticate(game, animal) ? animal.PositiveContacts + "/" + unit.TrustThreshold + "  ·  " + (unit.FriendChance * 100).ToString("0.#") + "%" : "Not possible", 512, Art.Gold);
            string account = animal.Domestic ? DomesticLineageBenefit(animal) + ". " + (own ? "Travels with your household." : "Belongs to another people.") :
                encounters ? unit.Hostile ? "Hostile: an approach can cause severe injury. Review the offering and risks before choosing." : "An offering may build trust or cause injury. Review the cost and risks before approaching." : "An earlier story. Enable moving encounters in Map views for targeted attack and befriending.";
            if (livestock) account = own ? LivestockHarvestSummary(animal) : "Milk and meat belong to this herd's owner. More living animals produce more milk; slaughter reduces future output.";
            else if (ownDogs) account = "Dogs support this band's attacks on wild animals. They do not produce food, help gathering or add strength against other peoples.";
            else if (game.LivestockEnabled && animal.Kind == BeastKind.Deer) account = "Deer cannot be domesticated. Hunt this group or leave it wild.";
            Typography.Draw(g, account, new RectangleF(1213, 551, 331, 70), 16, Art.Muted, TypeRole.Annotation);
            int cellId = animal.CellId, id = animal.Id;
            if (own)
            {
                Button(g, "Units", 1213, 625, 161, 31, OpenMapCardUnits, false, false);
                MapTip("Open your roster with this companion group selected.");
                int ownerId = animal.OwnerId;
                Button(g, "Resources", 1384, 625, 160, 31, delegate { if (!game.CanControlBand(ownerId)) return; ArmMapCommandBand(ownerId); ClearMapTransient(); OpenEconomy(2); }, false, false);
                MapTip("See the benefits and care costs of your household's domestic groups.");
            }
            else if (encounters)
            {
                Button(g, "Review encounter", 1213, 625, 331, 31, delegate { OpenMapCardEncounter(UnitKind.Animal, id); }, true, false);
                MapTip("Review attack or peaceful approach, costs and injury risks. Opening the review spends no action.");
            }
            if (own && livestock) DrawLivestockHarvestButton(g, animal, new RectangleF(1213, 666, 331, 31));
            else
            {
                Button(g, "View this place", 1213, 666, 331, 31, delegate { OpenMapCardPlace(cellId); }, false, false);
                MapTip("Read the named hex beneath this group, including terrain and gathering conditions.");
            }
        }

        private string LivestockHarvestSummary(Beast herd)
        {
            int killed = LivestockEconomy.SlaughterCount(game, herd);
            double milk = LivestockEconomy.MilkFood(game, herd);
            double after = herd.Count <= 0 ? 0 : milk * (herd.Count - killed) / herd.Count;
            return "Slaughter " + killed + " of " + herd.Count + " for +" + LivestockEconomy.MeatFood(game, herd).ToString("0.0") +
                " food. Milk falls from " + milk.ToString("0.0") + " to " + after.ToString("0.0") + " per turn. Uses 1 action from this herd's owner.";
        }

        private string LivestockHarvestReason(Beast herd)
        {
            if (SemiautomaticMode) return "In Semiautomatic, choose a herd decision or switch to Manual to slaughter this group directly.";
            Band owner = game.Bands.FirstOrDefault(b => b.Id == herd.OwnerId);
            if (!game.LivestockEnabled || !LivestockEconomy.IsLivestock(herd)) return "This group is not livestock.";
            if (game.IsOver || herd.Count <= 0 || owner == null || owner.Population <= 0) return "This herd has no living owner available.";
            if (!game.CanControlBand(owner.Id)) return "This herd belongs to another people.";
            if (owner.CellId != herd.CellId) return "The owning band must share this herd's hex.";
            if (game.ActionsFor(owner.Id) <= 0) return "The owning band has no actions left this turn.";
            return "";
        }

        private void DrawLivestockHarvestButton(Graphics g, Beast herd, RectangleF bounds)
        {
            int id = herd.Id;
            Band owner = game.Bands.FirstOrDefault(b => b.Id == herd.OwnerId);
            string reason = LivestockHarvestReason(herd);
            bool enabled = reason.Length == 0 && LivestockEconomy.CanSlaughter(game, owner, herd);
            string label = "Slaughter " + LivestockEconomy.SlaughterCount(game, herd) + " · +" + LivestockEconomy.MeatFood(game, herd).ToString("0") + " food";
            EncounterAction(g, label, bounds, enabled, delegate { HarvestLivestock(id); }, enabled);
            if (!enabled) buttons.Add(new UiButton(bounds, delegate { status = reason.Length > 0 ? reason : "This herd cannot be slaughtered now."; Invalidate(); }));
            MapTip(LivestockHarvestSummary(herd) + (owner == null ? "" : " Owner: " + owner.Name + ".") + (reason.Length == 0 ? " Applies immediately." : "\n" + reason));
        }

        private void HarvestLivestock(int id)
        {
            if (!RequireManualOrders()) return;
            Beast herd = game.Beasts.FirstOrDefault(b => b.Id == id && b.Count > 0);
            if (herd == null || !UnitVisible(herd.CellId)) return;
            string reason = LivestockHarvestReason(herd);
            Band owner = game.Bands.FirstOrDefault(b => b.Id == herd.OwnerId);
            if (reason.Length > 0 || !LivestockEconomy.CanSlaughter(game, owner, herd))
            { status = reason.Length > 0 ? reason : "This herd cannot be slaughtered now."; Invalidate(); return; }
            ArmMapCommandBand(owner.Id);
            Command("slaughter:" + id);
            if (herd.Count > 0 && !BlockingSheet) { SelectAnimal(herd); page = 0; ShowMapSelection(); }
        }

        private void DrawMapLostCard(Graphics g, bool unit)
        {
            MapCardHeading(g, "No current account", unit ? "Their path is lost to sight" : "Beyond the known paths", "Choose another place or counter");
            Art.Icon(g, "globe", 1357, 414, 45, Art.Muted);
            Typography.Draw(g, unit ? "This unit is no longer observed. Select a visible counter before giving an order." : "No account of this place has reached your people.", new RectangleF(1213, 492, 331, 100), 20, Art.Ink, TypeRole.Annotation);
            Button(g, "Close this record", 1213, 666, 331, 31, CloseMapSelection, false, false);
            MapTip("Return to the map and select another place or living unit.");
        }

        private void OpenMapCardLanguage(int languageId)
        {
            ClearMapTransient(); page = 2; culturePage = 1; languagePreview = false;
            Band[] known = game.Bands.Where(b => b.Population > 0 && (!map.Fog || game.Explored.Contains(b.CellId))).ToArray();
            LanguageProfile[] living = game.Languages.Where(l => known.Any(b => b.LanguageId == l.Id)).ToArray();
            int index = Array.FindIndex(living, l => l.Id == languageId);
            languagePage = index < 0 ? 0 : index / 3; Invalidate();
        }

        private void OpenMapCardUnits()
        {
            unitsFilter = 0;
            unitsSelectedKind = inspectorPage == 2 ? UnitKind.Animal : UnitKind.Band;
            unitsSelectedId = inspectorPage == 2 ? selectedAnimalId : inspectedBandId;
            ClearMapTransient(); page = 3; Invalidate();
        }

        private void OpenMapCardPlace(int cell)
        {
            if (!MapCardKnown(cell)) return;
            selected = cell; selectedAnimalId = -1; inspectedBandId = -1; inspectorPage = 0; ShowMapSelection(); Invalidate();
        }

        private void OpenMapCardEncounter(UnitKind kind, int id)
        {
            if (kind == UnitKind.Band ? MapCardBand(id) == null : MapCardAnimal(id) == null) return;
            OpenEncounterChoice(kind, id);
        }

        private void MapCardMove(int cell)
        {
            if (!HasCommandBand || !MapCardKnown(cell) || RightMoveReason(cell).Length != 0) return;
            selected = cell; selectedAnimalId = -1; inspectedBandId = -1; inspectorPage = 0; MoveSelection();
        }

        private SaltSource MapCardSaltSource(int cell)
        { return game.SaltEnabled && MapCardKnown(cell) ? SaltEconomy.Source(game, cell) : SaltSource.None; }

        private void DrawMapSaltGatherButton(Graphics g, int cell, float x, float y, float width)
        {
            if (HasCommandBand && cell == CurrentOrderBand.CellId && MapCardSaltSource(cell) != SaltSource.None && SaltEconomy.CanGather(game, CurrentOrderBand))
            {
                int actorId = CurrentOrderBand.Id;
                Button(g, "Gather salt  +" + SaltEconomy.GatherYield(game, CurrentOrderBand).ToString("0.0"), x, y, width, 31, delegate { if (HasCommandBand && CurrentOrderBand.Id == actorId) MapCardGatherSalt(cell); }, true, false);
                MapTip("Spend one action gathering salt at this source. Your household consumes salt as turns pass.");
            }
            else
            {
                string note = game.IsOver ? "This band's story has ended" : !HasCommandBand ? "Select your band to gather salt" : game.ActionsFor(CurrentOrderBand.Id) <= 0 ? "End the turn to gather salt" : "Salt cannot be gathered here now";
                Typography.Line(g, note, new RectangleF(x, y, width, 31), 15, Art.Muted, TypeRole.Annotation, true, StringAlignment.Center);
            }
        }

        private void MapCardGatherSalt(int cell)
        {
            if (page != 0 || BlockingSheet || !HasCommandBand || !MapCardKnown(cell) || cell != CurrentOrderBand.CellId ||
                MapCardSaltSource(cell) == SaltSource.None || !SaltEconomy.CanGather(game, CurrentOrderBand)) return;
            Command("salt");
        }
    }
}
