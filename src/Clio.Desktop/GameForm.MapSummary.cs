using System;
using System.Drawing;
using System.Linq;
using Clio.Simulation;

namespace Clio.Desktop
{
    public sealed partial class GameForm
    {
        private void DrawMapCompactCard(Graphics g, RectangleF box)
        {
            if (inspectorPage == 1)
            {
                Band band = MapCardBand(inspectedBandId);
                if (band != null) { DrawCompactBand(g, box, band); return; }
            }
            else if (inspectorPage == 2)
            {
                Beast animal = MapCardAnimal(selectedAnimalId);
                if (animal != null) { DrawCompactAnimal(g, box, animal); return; }
            }
            else if (MapCardKnown(selected)) { DrawCompactPlace(g, box, selected); return; }

            CompactCardHeading(g, box, inspectorPage == 0 ? "Unexplored land" : "Out of sight", "No current account");
            Typography.Draw(g, inspectorPage == 0 ? "Explore farther to learn about this place." : "Select a visible group to inspect it.",
                new RectangleF(box.X + 19, box.Y + 90, box.Width - 38, 64), 18, Art.Muted, TypeRole.Body);
        }

        private void CompactCardHeading(Graphics g, RectangleF box, string title, string subtitle)
        {
            Typography.Line(g, title, new RectangleF(box.X + 16, box.Y + 12, box.Width - 70, 36), 29, Art.Ink, TypeRole.Heading, true);
            Typography.Line(g, subtitle, new RectangleF(box.X + 19, box.Y + 54, box.Width - 38, 23), 15, Art.Muted, TypeRole.Body, true);
            Art.Line(g, Color.FromArgb(55, Art.Gold), .7f, box.X + 19, box.Y + 82, box.Right - 19, box.Y + 82);
        }

        private void CompactCardState(Graphics g, RectangleF box, string text, Color ink)
        {
            Typography.Draw(g, text, new RectangleF(box.X + 19, box.Y + 132, box.Width - 38, 42), 16, ink, TypeRole.Body);
        }

        private void CompactCardMetric(Graphics g, RectangleF box, int slot, string icon, string value, Color ink, string tip, Beast animal = null)
        {
            RectangleF bounds = new RectangleF(box.X + 16 + slot * 111, box.Y + 90, 105, 33);
            if (bounds.Contains(hoverPoint)) Art.Fill(g, Color.FromArgb(25, Art.Gold), bounds.X, bounds.Y, bounds.Width, bounds.Height);
            RectangleF glyph = new RectangleF(bounds.X + 3, bounds.Y + 5, 23, 23);
            if (animal != null) IdentityArt.DrawAnimal(g, animal.Kind, glyph, ink, animal.Domestic);
            else if (icon == "salt") DrawLandscapeMark(g, 1, glyph, ink);
            else if (icon == "people")
            {
                using (Brush brush = new SolidBrush(ink))
                {
                    g.FillEllipse(brush, glyph.X + 8, glyph.Y + 1, 7, 7);
                    g.FillEllipse(brush, glyph.X + 1, glyph.Y + 5, 5, 5);
                    g.FillEllipse(brush, glyph.X + 17, glyph.Y + 5, 5, 5);
                }
                using (Pen pen = new Pen(ink, 2))
                {
                    g.DrawArc(pen, glyph.X + 6, glyph.Y + 10, 11, 15, 180, 180);
                    g.DrawArc(pen, glyph.X, glyph.Y + 12, 7, 10, 180, 180);
                    g.DrawArc(pen, glyph.X + 16, glyph.Y + 12, 7, 10, 180, 180);
                }
            }
            else if (icon == "milk")
            {
                using (Pen pen = new Pen(ink, 1.5f))
                {
                    g.DrawLines(pen, new[] { new PointF(glyph.X + 3, glyph.Y + 6), new PointF(glyph.X + 5, glyph.Bottom - 2),
                        new PointF(glyph.Right - 5, glyph.Bottom - 2), new PointF(glyph.Right - 3, glyph.Y + 6) });
                    g.DrawEllipse(pen, glyph.X + 3, glyph.Y + 3, glyph.Width - 6, 6);
                    g.DrawArc(pen, glyph.Right - 5, glyph.Y + 8, 8, 9, 260, 210);
                }
            }
            else Art.Icon(g, icon, glyph.X, glyph.Y, glyph.Width, ink);
            Typography.Line(g, value, new RectangleF(bounds.X + 32, bounds.Y, 72, 33), 24, ink, TypeRole.Number, true);
            buttons.Add(new UiButton(bounds, ToggleMapCardDetails) { Tip = tip + " Click for the complete record." });
        }

        private void CompactCardAction(Graphics g, RectangleF box, string title, bool enabled, Action action, string explanation)
        {
            RectangleF bounds = new RectangleF(box.X + 19, box.Y + 179, box.Width - 38, 31);
            EncounterAction(g, title, bounds, enabled, action, enabled);
            if (!enabled) buttons.Add(new UiButton(bounds, delegate { status = explanation; Invalidate(); }));
            MapTip(explanation);
        }

        private void DrawCompactPlace(Graphics g, RectangleF box, int cellId)
        {
            Cell cell = game.World.Cells[cellId];
            SaltSource salt = MapCardSaltSource(cellId);
            CompactCardHeading(g, box, game.Place(cellId), cell.Terrain + (salt == SaltSource.None ? "" : " · " + SaltSourceLabel(salt)));
            CompactCardMetric(g, box, 0, "leaf", cell.IsLand ? "+" + game.ForageYield(cellId, CurrentOrderBand).ToString("0") : "—", Art.Gold,
                "Food gathered per action by the selected band. Moving here does not gather food.");
            CompactCardMetric(g, box, 1, "sun", cell.IsLand ? ((1 - game.Depletion[cellId]) * 100).ToString("0") + "%" : "—", LedgerGreen,
                "Ground recovery: repeated gathering depletes this land; leaving it to rest restores its yield.");
            bool here = HasCommandBand && CurrentOrderBand.CellId == cellId;
            bool adjacent = HasCommandBand && game.World.Cells[CurrentOrderBand.CellId].Neighbors.Contains(cellId);
            CompactCardMetric(g, box, 2, "move", here ? "0" : adjacent && cell.IsLand ? MapTravelCost(CurrentOrderBand, cellId).ToString() : "—", Art.Ink,
                "Actions needed for one move into this hex. " + MapCardRoute(cellId) + ".");
            string caution = MapPlaceCaution(cellId);
            CompactCardState(g, box, caution.StartsWith("Danger:", StringComparison.Ordinal) ? caution : cell.IsLand ? MapCardRoute(cellId) : "Open water · No land route",
                caution.StartsWith("Danger:", StringComparison.Ordinal) ? BandPanelWarning : Art.Muted);
            if (here && salt != SaltSource.None && !SemiautomaticMode)
                DrawMapSaltGatherButton(g, cellId, box.X + 19, box.Y + 179, box.Width - 38);
            else if (here || !cell.IsLand || SemiautomaticMode)
                CompactCardAction(g, box, "Resources", true, delegate { ClearMapTransient(); OpenEconomy(2); }, "Read the household's food, salt, wood and companion accounts.");
            else
            {
                string reason = RightMoveReason(cellId);
                CompactCardAction(g, box, "Move here", HasCommandBand && reason.Length == 0, delegate { MapCardMove(cellId); },
                    reason.Length > 0 ? reason : "Move the selected band here. The movement cost shown above is paid immediately.");
            }
        }

        private void DrawCompactBand(Graphics g, RectangleF box, Band band)
        {
            bool own = game.CanControlBand(band.Id);
            CompactCardHeading(g, box, band.Name, own ? "Your band" : "Independent people");
            CompactCardMetric(g, box, 0, "people", band.Population.ToString("N0"), Art.Ink, "Living people in this band.");
            CompactCardMetric(g, box, 1, "leaf", band.Food.ToString("0"), Art.Gold, "Food reserves carried by this band. Each band carries its own supplies.");
            if (game.SaltEnabled)
                CompactCardMetric(g, box, 2, "salt", SaltEconomy.ReserveTurns(band).ToString("0.0"), SaltEconomy.ReserveTurns(band) < 1 ? BandPanelWarning : Art.Ink,
                    "Turns of salt remaining: " + band.Salt.ToString("0.#") + " in reserve; " + SaltEconomy.Need(band).ToString("0.#") + " used each turn.");
            else CompactCardMetric(g, box, 2, "heart", (band.Cohesion * 100).ToString("0") + "%", Art.Ink, "Cohesion measures how well this band holds together.");
            string state = own ? UnitsActionCount(band.Id) : band.Settled ? "Established hearth" : "Wandering band";
            if (own && game.TribesEnabled) state += "\n" + BandPanelReunionLabel(band);
            CompactCardState(g, box, state, Art.Muted);
            int id = band.Id;
            if (own && game.TribesEnabled)
                CompactCardAction(g, box, "Find reunion camp", true, delegate { OpenTribeReunion(id); }, "Locate the leader's current hex. Return there before this band's separation deadline to renew contact.");
            else if (own)
                CompactCardAction(g, box, "Economy", true, delegate { ArmMapCommandBand(id); ClearMapTransient(); OpenEconomy(0); }, "Read this band's supplies, population and forecast.");
            else if (game.Rules == SimulationRules.MobileUnits)
                CompactCardAction(g, box, "Review encounter", true, delegate { OpenMapCardEncounter(UnitKind.Band, id); }, "Review contact or combat. Opening the review spends no action.");
            else CompactCardAction(g, box, "Contact & trade", true, delegate { ClearMapTransient(); OpenEconomy(3); }, "Review known peoples and peaceful contact.");
        }

        private void DrawCompactAnimal(Graphics g, RectangleF box, Beast animal)
        {
            bool own = animal.Domestic && game.CanControlBand(animal.OwnerId);
            bool livestock = game.LivestockEnabled && animal.Domestic && LivestockEconomy.IsLivestock(animal);
            bool mobile = game.Rules == SimulationRules.MobileUnits;
            UnitProfile unit = mobile ? EncounterRules.Animal(game, animal) : null;
            CompactCardHeading(g, box, AnimalUnitName(animal), own ? livestock ? "Your livestock" : "Your companions" : animal.Domestic ? "Another people's companions" : mobile ? UnitTemperament(unit) : "Wild animals");
            CompactCardMetric(g, box, 0, "", animal.Count.ToString("N0"), Art.Ink, "Living animals in this group.", animal);
            if (livestock)
            {
                CompactCardMetric(g, box, 1, "milk", "+" + LivestockEconomy.MilkFood(game, animal).ToString("0.#"), LedgerGreen, "Milk food per turn while this herd shares its living owner's hex. Larger herds provide more milk.");
                CompactCardMetric(g, box, 2, "leaf", "−" + BandEconomy.AnimalCare(game, animal).ToString("0.#"), Art.Gold, "Food spent on this herd's care per turn.");
                CompactCardState(g, box, own ? "Slaughter uses 1 action and reduces the herd's future milk." : "This herd's milk and meat belong to its owner.", Art.Muted);
            }
            else
            {
                CompactCardMetric(g, box, 1, "heart", mobile ? unit.CurrentHealth.ToString() : "—", LedgerGreen, mobile ? "Current health: " + unit.CurrentHealth + " of " + unit.MaxHealth + "." : "Health is not recorded in this earlier story.");
                bool dogs = game.LivestockEnabled && own && animal.Kind == BeastKind.Wolves;
                Band owner = dogs ? game.Bands.FirstOrDefault(b => b.Id == animal.OwnerId) : null;
                CompactCardMetric(g, box, 2, "hunt", dogs && owner != null ? "+" + (BandEconomy.DomesticEffects(game, owner).HuntingBonus * 100).ToString("0") + "%" : mobile ? unit.Strength.ToString("0.0") : "—", Art.Gold,
                    dogs ? "Hunting support for the owning band against wild animals. Dogs add no gathering, food output or combat strength against other peoples." : "This group's fighting strength. Review the encounter before attacking.");
                string state = dogs ? "Hunting support · No food output" : game.LivestockEnabled && animal.Kind == BeastKind.Deer ? "Cannot be domesticated" : own ? "Travels with its owning band" : mobile && unit.Hostile ? "Hostile · Approaching risks injury" : "Review the cost and risk before approaching";
                CompactCardState(g, box, state, mobile && unit.Hostile && !own ? BandPanelWarning : Art.Muted);
            }
            int id = animal.Id, cellId = animal.CellId;
            if (own && livestock) DrawLivestockHarvestButton(g, animal, new RectangleF(box.X + 19, box.Y + 179, box.Width - 38, 31));
            else if (own) CompactCardAction(g, box, "Units", true, OpenMapCardUnits, "Read this companion group's condition and owner in Units.");
            else if (mobile) CompactCardAction(g, box, "Review encounter", true, delegate { OpenMapCardEncounter(UnitKind.Animal, id); }, "Review attack or peaceful approach, costs and injury risks. Opening the review spends no action.");
            else CompactCardAction(g, box, "View this place", true, delegate { OpenMapCardPlace(cellId); }, "Inspect the terrain beneath this group.");
        }
    }
}
