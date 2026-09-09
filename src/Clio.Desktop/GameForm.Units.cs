using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using Clio.Simulation;

namespace Clio.Desktop
{
    public sealed partial class GameForm
    {
        private const int UnitsPerPage = 5;
        private int unitsFilter, unitsRosterPage, unitsSelectedId = -1;
        private UnitKind unitsSelectedKind = UnitKind.Band;

        private sealed class UnitsRosterEntry
        {
            internal readonly UnitKind Kind;
            internal readonly int Id;
            internal UnitsRosterEntry(UnitKind kind, int id) { Kind = kind; Id = id; }
        }

        private void ResetUnitsPage()
        {
            unitsFilter = 0; unitsRosterPage = 0;
            unitsSelectedKind = UnitKind.Band; unitsSelectedId = game.TribeLeaderBand == null ? -1 : game.TribeLeaderBand.Id;
        }

        // Ownership and command are separate: a companion belongs to the household,
        // while its movement follows its owning band. Membership is rechecked each frame.
        private List<UnitsRosterEntry> UnitsRoster()
        {
            List<UnitsRosterEntry> roster = new List<UnitsRosterEntry>();
            if (unitsFilter != 2)
                foreach (Band band in game.ControlledBands.OrderByDescending(b => game.TribeLeaderBand != null && b.Id == game.TribeLeaderBand.Id).ThenBy(b => b.Id))
                    roster.Add(new UnitsRosterEntry(UnitKind.Band, band.Id));
            if (unitsFilter != 1)
                foreach (Beast animal in game.Beasts.Where(b => b.Domestic && game.CanControlBand(b.OwnerId) && b.Count > 0).OrderBy(b => b.OwnerId).ThenBy(b => b.Id))
                    roster.Add(new UnitsRosterEntry(UnitKind.Animal, animal.Id));
            return roster;
        }

        private void SynchronizeUnitsRoster(List<UnitsRosterEntry> roster)
        {
            unitsRosterPage = Math.Max(0, Math.Min(unitsRosterPage, Math.Max(0, (roster.Count - 1) / UnitsPerPage)));
            int index = roster.FindIndex(r => r.Kind == unitsSelectedKind && r.Id == unitsSelectedId);
            if (index >= 0) unitsRosterPage = index / UnitsPerPage;
            else if (roster.Count > 0)
            {
                UnitsRosterEntry first = roster[unitsRosterPage * UnitsPerPage];
                unitsSelectedKind = first.Kind; unitsSelectedId = first.Id;
            }
            else unitsSelectedId = -1;
        }

        private void SetUnitsFilter(int filter)
        {
            unitsFilter = filter; unitsRosterPage = 0;
            SynchronizeUnitsRoster(UnitsRoster()); Invalidate();
        }

        private void ChangeUnitsRosterPage(int direction)
        {
            List<UnitsRosterEntry> roster = UnitsRoster();
            if (roster.Count == 0) return;
            int pages = (roster.Count + UnitsPerPage - 1) / UnitsPerPage;
            unitsRosterPage = Math.Max(0, Math.Min(pages - 1, unitsRosterPage + direction));
            UnitsRosterEntry first = roster[unitsRosterPage * UnitsPerPage];
            unitsSelectedKind = first.Kind; unitsSelectedId = first.Id; Invalidate();
        }

        private void InspectRosterUnit(UnitKind kind, int id)
        {
            // Resolve again at click time: autoplay may have moved or lost a unit
            // since the last frame. A roster selection never issues a game order.
            if (kind == UnitKind.Band)
            {
                Band band = game.Bands.FirstOrDefault(b => b.Id == id && game.CanControlBand(b.Id));
                if (band == null || !UnitVisible(band.CellId)) return;
                selectedAnimalId = -1; inspectedBandId = band.Id; inspectorPage = 1; selected = band.CellId;
            }
            else
            {
                Beast animal = game.Beasts.FirstOrDefault(b => b.Id == id && b.Domestic && game.CanControlBand(b.OwnerId) && b.Count > 0);
                if (animal == null || !UnitVisible(animal.CellId)) return;
                if (game.TribesEnabled) ArmMapCommandBand(animal.OwnerId);
                SelectAnimal(animal);
            }
            page = 0; map.Focus(game.World.Cells[selected]); ShowMapSelection();
            status = kind == UnitKind.Band ? "This band is selected. Orders in the dock act on this household." : "Companions selected. Their condition and household benefits are open in their card.";
            Invalidate();
        }

        private void DrawUnits(Graphics g)
        {
            Surface(g, "Those who travel with you", game.TribesEnabled ? "Each tribal band has two actions per turn. Select a household to give orders; companions follow their own band." : "A roster of your band and its companions. Select a unit to read its condition or find it on the map.");
            List<UnitsRosterEntry> roster = UnitsRoster(); SynchronizeUnitsRoster(roster);
            Button(g, "All units", 44, 286, 112, 31, delegate { SetUnitsFilter(0); }, unitsFilter == 0, false);
            Button(g, "Bands", 166, 286, 142, 31, delegate { SetUnitsFilter(1); }, unitsFilter == 1, false);
            Button(g, "Companions", 318, 286, 172, 31, delegate { SetUnitsFilter(2); }, unitsFilter == 2, false);
            int bands = game.ControlledBands.Count();
            Beast[] companions = game.Beasts.Where(b => b.Domestic && game.CanControlBand(b.OwnerId) && b.Count > 0).ToArray();
            int animals = companions.Sum(b => b.Count);
            Typography.Line(g, bands + (bands == 1 ? " band" : " bands") + "  ·  " + companions.Length + (companions.Length == 1 ? " companion group" : " companion groups") + "  ·  " + animals.ToString("N0") + (animals == 1 ? " animal" : " animals"),
                new RectangleF(720, 286, 818, 31), 17, Art.Muted, TypeRole.Annotation, true, StringAlignment.Far);

            Art.Panel(g, new RectangleF(44, 334, 878, 440), Panel, false);
            Typography.Label(g, "Unit / assignment", new RectangleF(63, 346, 485, 25), 12, Art.Gold, .7f);
            Typography.Label(g, "Number", new RectangleF(616, 346, 107, 25), 12, Art.Muted, .6f, StringAlignment.Far);
            Typography.Label(g, "Condition", new RectangleF(750, 346, 148, 25), 12, Art.Muted, .6f, StringAlignment.Far);
            Art.Rule(g, 63, 379, 839);
            for (int i = 0; i < UnitsPerPage && unitsRosterPage * UnitsPerPage + i < roster.Count; i++)
                DrawUnitsRosterRow(g, roster[unitsRosterPage * UnitsPerPage + i], 389 + i * 66);
            if (roster.Count == 0) DrawUnitsEmptyRoster(g);
            Art.Rule(g, 63, 728, 839);
            int first = roster.Count == 0 ? 0 : unitsRosterPage * UnitsPerPage + 1;
            int last = Math.Min(roster.Count, (unitsRosterPage + 1) * UnitsPerPage);
            Typography.Line(g, first + "–" + last + " of " + roster.Count + (roster.Count == 1 ? " unit" : " units"), new RectangleF(179, 739, 598, 28), 15, Art.Muted, TypeRole.Annotation, true, StringAlignment.Center);
            if (unitsRosterPage > 0) Button(g, "Previous", 63, 740, 105, 28, delegate { ChangeUnitsRosterPage(-1); }, false, false);
            if (last < roster.Count) Button(g, "Next", 796, 740, 105, 28, delegate { ChangeUnitsRosterPage(1); }, false, false);

            UnitsRosterEntry current = roster.FirstOrDefault(r => r.Kind == unitsSelectedKind && r.Id == unitsSelectedId);
            DrawUnitsDetail(g, current);
        }

        private void DrawUnitsRosterRow(Graphics g, UnitsRosterEntry entry, float y)
        {
            Band band = entry.Kind == UnitKind.Band ? game.Bands.First(b => b.Id == entry.Id) : null;
            Beast animal = band == null ? game.Beasts.First(b => b.Id == entry.Id) : null;
            UnitProfile unit = band != null ? EncounterRules.Band(game, band) : EncounterRules.Animal(game, animal);
            RectangleF row = new RectangleF(56, y, 854, 60);
            bool active = entry.Kind == unitsSelectedKind && entry.Id == unitsSelectedId, hover = row.Contains(hoverPoint);
            if (active || hover) Art.Fill(g, active ? Color.FromArgb(43, 51, 46) : Color.FromArgb(31, 45, 47), row.X, row.Y, row.Width, row.Height);
            if (active) Art.Line(g, Art.Gold, 2, row.X, row.Y + 3, row.X, row.Bottom - 3);
            if (band != null) IdentityArt.DrawEmblem(g, game.TribeOf(band.Id), new RectangleF(70, y + 11, 37, 37), false);
            else IdentityArt.DrawAnimal(g, animal.Kind, new RectangleF(70, y + 14, 37, 28), Art.Gold, true);
            Typography.Line(g, band != null ? band.Name : AnimalUnitName(animal), new RectangleF(124, y + 1, 468, 31), 23, active ? Art.Gold : Art.Ink, TypeRole.Heading, true);
            string assignment = band != null ? UnitsBandAssignment(band) : "Follows " + game.Bands.First(b => b.Id == animal.OwnerId).Name;
            Typography.Line(g, assignment + "  ·  " + UnitsLocation(unit.CellId), new RectangleF(125, y + 31, 467, 23), 15, Art.Muted, TypeRole.Annotation, true);
            Typography.Line(g, unit.Count.ToString("N0"), new RectangleF(606, y + 4, 117, 34), 26, Art.Ink, TypeRole.Number, true, StringAlignment.Far);
            Typography.Line(g, band != null ? "people" : "animals", new RectangleF(606, y + 35, 117, 20), 13, Art.Muted, TypeRole.Annotation, true, StringAlignment.Far);
            bool encounters = game.Rules == SimulationRules.MobileUnits;
            Typography.Line(g, !encounters ? "Not recorded" : unit.Wounds == 0 ? "Uninjured" : unit.Wounds + (unit.Wounds == 1 ? " wound point" : " wound points"), new RectangleF(737, y + 7, 159, 28), 17,
                unit.Wounds > 0 ? Art.Gold : LedgerGreen, TypeRole.Annotation, true, StringAlignment.Far);
            if (encounters) Meter(g, 761, y + 43, 135, unit.MaxHealth <= 0 ? 0 : unit.CurrentHealth / (double)unit.MaxHealth, unit.Wounds > 0 ? Art.Gold : LedgerGreen);
            UnitKind kind = entry.Kind; int id = entry.Id;
            buttons.Add(new UiButton(row, delegate
            {
                if (!UnitsRoster().Any(r => r.Kind == kind && r.Id == id)) return;
                unitsSelectedKind = kind; unitsSelectedId = id;
                if (game.TribesEnabled) ArmMapCommandBand(kind == UnitKind.Band ? id : game.Beasts.First(b => b.Id == id).OwnerId);
                Invalidate();
            }));
        }

        private string UnitsLocation(int cell)
        {
            return UnitVisible(cell) ? game.Place(cell) : "Beyond remembered land";
        }

        private void DrawUnitsEmptyRoster(Graphics g)
        {
            Art.Icon(g, unitsFilter == 2 ? "heart" : "branch", 458, 421, 44, Art.Gold);
            Typography.Line(g, unitsFilter == 2 ? "No companions yet" : "No living units in this roster", new RectangleF(124, 485, 714, 39), 30, Art.Ink, TypeRole.Heading, true, StringAlignment.Center);
            Typography.Draw(g, unitsFilter == 2 ? "A shared life begins with peaceful contact. On the map, select an animal group to read its temperament and consider an approach." : "This record follows your own living band and the domestic groups that belong to it.",
                new RectangleF(181, 541, 601, 77), 19, Art.Muted, TypeRole.Annotation);
        }

        private void DrawUnitsDetail(Graphics g, UnitsRosterEntry entry)
        {
            Art.Panel(g, new RectangleF(940, 334, 598, 440), Panel, false);
            if (entry == null)
            {
                Typography.Label(g, "How command works", new RectangleF(963, 351, 549, 25), 12, Art.Gold, .8f);
                Typography.Line(g, "Households, a shared journey", new RectangleF(960, 389, 550, 45), 31, Art.Ink, TypeRole.Heading, true);
                Typography.Draw(g, "Your orders guide the bands in this roster. Domestic groups appear here as companions; they follow their own household when it travels.", new RectangleF(963, 467, 549, 88), 20, Art.Muted, TypeRole.Body);
                Art.Rule(g, 963, 588, 549);
                Typography.Draw(g, game.TribesEnabled ? game.BandPersonalitiesEnabled ? "Return to the leading household to renew kinship. A band's disposition and distance determine its separation deadline; select a band to read its current limits." : "Return to the leading band's camp to renew kinship. After sixteen turns apart, a household becomes an independent people." : "Daughter bands become independent peoples. Their banners and records remain on the map.", new RectangleF(963, 619, 549, 78), 18, Art.Muted, TypeRole.Annotation);
                if (game.TribeLeaderBand != null && UnitVisible(game.TribeLeaderBand.CellId))
                    Button(g, "Find your leading band", 963, 719, 549, 36, delegate { if (game.TribeLeaderBand != null) InspectRosterUnit(UnitKind.Band, game.TribeLeaderBand.Id); }, false, false);
                else Button(g, "Read their history", 963, 719, 549, 36, delegate { page = 4; Invalidate(); }, false, false);
                return;
            }

            Band band = entry.Kind == UnitKind.Band ? game.Bands.First(b => b.Id == entry.Id) : null;
            Beast animal = band == null ? game.Beasts.First(b => b.Id == entry.Id) : null;
            UnitProfile unit = band != null ? EncounterRules.Band(game, band) : EncounterRules.Animal(game, animal);
            bool encounters = game.Rules == SimulationRules.MobileUnits;
            if (band != null) IdentityArt.DrawEmblem(g, game.TribeOf(band.Id), new RectangleF(963, 354, 61, 61), false);
            else IdentityArt.DrawAnimal(g, animal.Kind, new RectangleF(963, 363, 61, 43), Art.Gold, true);
            Typography.Label(g, band != null ? "Band " + band.Id + " / " + (game.TribeLeaderBand != null && game.TribeLeaderBand.Id == band.Id ? "Leading household" : "Your tribe") : "Companion group " + animal.Id, new RectangleF(1041, 349, 470, 23), 11.5f, Art.Gold, .55f);
            Typography.Line(g, band != null ? band.Name : AnimalUnitName(animal), new RectangleF(1038, 376, 474, 42), 32, Art.Ink, TypeRole.Heading, true);
            Art.Rule(g, 963, 438, 549);
            UnitsDetailMetric(g, 963, band != null ? "People" : "Animals", unit.Count.ToString("N0"), Art.Ink);
            UnitsDetailMetric(g, 1150, "Health", encounters ? unit.CurrentHealth + " / " + unit.MaxHealth : "—", LedgerGreen);
            UnitsDetailMetric(g, 1337, "Fighting strength", encounters ? unit.Strength.ToString("0.0") : "—", Art.Gold);
            Typography.Line(g, "At " + UnitsLocation(unit.CellId), new RectangleF(963, 529, 549, 30), 22, Art.Ink, TypeRole.Heading, true);
            if (band != null && game.BandPersonalitiesEnabled)
            {
                Typography.Line(g, BandDispositionLabel(band) + " \u00b7 " + UnitsActionCount(band.Id), new RectangleF(963, 566, 549, 30), 23, Art.Gold, TypeRole.Heading, true);
                Typography.Draw(g, BandVoluntaryMovementNote(band), new RectangleF(963, 600, 549, 44), 17, Art.Muted, TypeRole.Body);
                Typography.Draw(g, UnitsReunionStatus(band.Id, true), new RectangleF(963, 650, 549, 58), 17, Art.Muted, TypeRole.Annotation);
            }
            else
            {
                string role = band != null ? game.TribesEnabled ? UnitsActionCount(band.Id) + (game.ActionsFor(band.Id) == 1 ? " remains" : " remain") + " this turn. " + UnitsReunionStatus(band.Id, true) : (band.Settled ? "An established hearth. " : "A wandering band. ") + "Orders in the command dock act on this band." :
                    !encounters ? "A domestic lineage in an earlier story. Mobile unit encounters can be enabled from the map." :
                    "These companions follow " + game.Bands.First(b => b.Id == animal.OwnerId).Name + " when that band moves. Their care is part of its household's needs.";
                Typography.Draw(g, role, new RectangleF(963, 572, 549, 66), 19, Art.Muted, TypeRole.Body);
                string note = band != null ? game.TribesEnabled ? "Meet at the leading band's current camp. Eight turns apart brings drift; sixteen brings separation." : (encounters ? "Strength includes companion support. Daughter bands govern themselves." : "Health and fighting strength are recorded when mobile unit encounters are enabled.") : DomesticLineageBenefit(animal);
                Typography.Draw(g, note, new RectangleF(963, 650, 549, 52), 17, Art.Gold, TypeRole.Annotation);
            }
            UnitKind kind = entry.Kind; int id = entry.Id;
            if (UnitVisible(unit.CellId))
            {
                bool reunion = band != null && game.TribesEnabled && game.TribeReunionCell >= 0;
                if (band != null && game.BandPersonalitiesEnabled)
                {
                    Button(g, "Select on map", 963, 719, 171, 36, delegate { InspectRosterUnit(kind, id); }, false, false);
                    DrawBandHoldControl(g, band, new RectangleF(1142, 719, 176, 36), "Hold this turn");
                    if (reunion) Button(g, "Find reunion", 1326, 719, 186, 36, delegate { OpenTribeReunion(id); }, false, false);
                }
                else
                {
                    Button(g, band != null ? "Select band on map" : "Inspect on map", 963, 719, reunion ? 269 : 549, 36, delegate { InspectRosterUnit(kind, id); }, false, false);
                    if (reunion) Button(g, "Find reunion camp", 1243, 719, 269, 36, delegate { OpenTribeReunion(id); }, false, false);
                }
            }
            else Typography.Line(g, "Outside the known map", new RectangleF(963, 719, 549, 36), 16, Art.Muted, TypeRole.Annotation, true, StringAlignment.Center);
        }

        private void UnitsDetailMetric(Graphics g, float x, string label, string value, Color color)
        {
            Typography.Label(g, label, new RectangleF(x, 455, 175, 22), 11.5f, Art.Muted, .55f);
            Typography.Line(g, value, new RectangleF(x - 2, 479, 179, 36), 29, color, TypeRole.Number, true);
        }

        private string UnitsBandAssignment(Band band)
        {
            if (!game.TribesEnabled) return "Your command";
            TribeMembership membership = game.TribeStatus(band.Id);
            return (membership != null && membership.IsLeader ? "Leader" : membership != null && membership.Drifting ? "Drifting" : "Tribal household") + " · " + UnitsActionCount(band.Id) +
                (game.BandPersonalitiesEnabled ? " · " + BandDispositionLabel(band) : "");
        }

        private string UnitsActionCount(int bandId)
        { int actions = game.ActionsFor(bandId); return actions + (actions == 1 ? " action" : " actions"); }

        private string UnitsReunionStatus(int id, bool detailed)
        {
            TribeMembership membership = game.TribeStatus(id);
            if (membership == null) return "This band no longer belongs to your tribe.";
            if (membership.IsLeader) return "The tribe reunites at this band's current camp.";
            if (game.BandPersonalitiesEnabled)
            {
                Band band = game.Bands.FirstOrDefault(b => b.Id == id);
                string distance = BandLeaderDistance(band);
                int left = Math.Max(0, membership.SecedeAfter - membership.TurnsAway);
                if (!detailed) return distance + ". " + (membership.SeparationBand == 0 ? "Contact renewed." : left + (left == 1 ? " turn" : " turns") + " until separation.");
                return distance + "; " + membership.TurnsAway + " turns since contact.\nDrift after " + membership.DriftAfter + "; separation after " + membership.SecedeAfter +
                    (membership.SeparationBand == 0 ? " turns apart." : " (" + left + " remaining).");
            }
            if (membership.TurnsAway == 0) return "Kinship renewed with the leading band.";
            if (membership.Drifting) return "Drifting: " + Math.Max(0, membership.SecedeAfter - membership.TurnsAway) + " turns remain to reunite before separation.";
            return membership.TurnsAway + " turns since reunion." + (detailed ? " Return before kinship begins to drift." : "");
        }

        private void OpenTribeReunion(int bandId)
        {
            if (!game.TribesEnabled || !game.CanControlBand(bandId)) return;
            int cell = game.TribeReunionCell;
            if (cell < 0 || !game.Explored.Contains(cell)) return;
            ArmMapCommandBand(bandId); page = 0;
            selected = cell; selectedAnimalId = -1; inspectedBandId = -1; inspectorPage = 0;
            map.Focus(game.World.Cells[cell]); ShowMapSelection();
            status = "The leading band's current camp is marked. Guide the selected household here to renew contact.";
            Invalidate();
        }
    }
}
