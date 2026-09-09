using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using Clio.Simulation;

namespace Clio.Desktop
{
    public sealed partial class GameForm
    {
        private bool unitStackOpen;
        private int unitStackCell = -1, unitStackPage;
        private const int UnitStackPageSize = 7;

        private sealed class StackChoice
        {
            internal UnitKind Kind;
            internal int Id, Count;
            internal string Name, Detail;
            internal bool Controlled;
            internal BeastKind Animal;
        }

        private void ClearUnitStack()
        { unitStackOpen = false; unitStackCell = -1; unitStackPage = 0; }

        private void OpenUnitStack(int cell)
        {
            if (BlockingSheet || !MapCardKnown(cell)) return;
            ClearMapTransient(); page = 0; selected = cell;
            inspectedBandId = -1; selectedAnimalId = -1; inspectorPage = 0;
            ShowMapSelection();
            // ShowMapSelection may clear transient cards; establish this one last.
            unitStackCell = cell; unitStackPage = 0; unitStackOpen = true;
            buttons.Clear(); Invalidate();
        }

        private StackChoice[] UnitStackChoices()
        {
            if (!unitStackOpen || !MapCardKnown(unitStackCell)) return new StackChoice[0];
            List<StackChoice> choices = new List<StackChoice>();
            foreach (Band band in game.Bands.Where(b => b.Population > 0 && b.CellId == unitStackCell)
                .OrderByDescending(b => game.CanControlBand(b.Id)).ThenBy(b => b.Id))
            {
                bool own = game.CanControlBand(band.Id);
                choices.Add(new StackChoice { Kind = UnitKind.Band, Id = band.Id, Count = band.Population, Name = band.Name, Controlled = own,
                    Detail = own ? (band.Id == commandedBandId ? "Selected" : "Your tribe") + " · Band " + band.Id + " · " + UnitsActionCount(band.Id) +
                        (game.BandPersonalitiesEnabled ? " · " + BandDispositionLabel(band) : "") : "Independent people · Band " + band.Id });
            }
            foreach (Beast animal in game.Beasts.Where(b => b.Count > 0 && b.CellId == unitStackCell).OrderBy(b => b.Id))
                choices.Add(new StackChoice { Kind = UnitKind.Animal, Id = animal.Id, Count = animal.Count, Name = AnimalUnitName(animal), Animal = animal.Kind,
                    Detail = (animal.Domestic ? "Companions" : "Wild group") + " · Group " + animal.Id });
            return choices.ToArray();
        }

        private void DrawUnitStackCard(Graphics g)
        {
            if (!unitStackOpen || !mapSelectionOpen || page != 0 || BlockingSheet) return;
            RectangleF box = MapSelectionBounds;
            buttons.RemoveAll(button => button.Bounds.IntersectsWith(box));
            Art.Fill(g, Color.FromArgb(65, 0, 0, 0), box.X - 4, box.Y + 5, box.Width + 8, box.Height + 3);
            Art.Panel(g, box, Color.FromArgb(24, 35, 38), true);
            Typography.Label(g, "Together in this place", new RectangleF(box.X + 18, box.Y + 10, box.Width - 76, 22), 11, Art.Gold, .6f);
            bool known = MapCardKnown(unitStackCell);
            Typography.Line(g, known ? game.Place(unitStackCell) : "Beyond the known paths", new RectangleF(box.X + 17, box.Y + 32, box.Width - 34, 34), 26, Art.Ink, TypeRole.Heading, true);
            StackChoice[] choices = UnitStackChoices();
            int bandCount = choices.Count(c => c.Kind == UnitKind.Band), animalCount = choices.Length - bandCount;
            string count = known ? bandCount + (bandCount == 1 ? " band" : " bands") + " · " + animalCount + (animalCount == 1 ? " animal group" : " animal groups") : "No observed groups are recorded here.";
            Typography.Line(g, count, new RectangleF(box.X + 18, box.Y + 68, box.Width - 36, 22), 15, Art.Muted, TypeRole.Annotation, true);
            int pages = Math.Max(1, (choices.Length + UnitStackPageSize - 1) / UnitStackPageSize);
            unitStackPage = Math.Max(0, Math.Min(unitStackPage, pages - 1));
            for (int i = 0; i < Math.Min(UnitStackPageSize, choices.Length - unitStackPage * UnitStackPageSize); i++)
                DrawUnitStackRow(g, choices[unitStackPage * UnitStackPageSize + i], new RectangleF(box.X + 14, box.Y + 92 + i * 48, box.Width - 28, 46));
            if (choices.Length == 0)
                Typography.Draw(g, known ? "The groups that were here have moved on. Inspect the place or choose another map counter." : "Explore this place before reading its inhabitants.",
                    new RectangleF(box.X + 20, box.Y + 119, box.Width - 40, 89), 19, Art.Muted, TypeRole.Annotation);
            Art.Line(g, Color.FromArgb(67, Art.Gold), .7f, box.X + 18, box.Y + 431, box.Right - 18, box.Y + 431);
            if (pages > 1)
            {
                Button(g, "Previous", box.X + 14, box.Y + 442, 92, 32, delegate { ChangeUnitStackPage(-1); }, false, false);
                Typography.Line(g, (unitStackPage + 1) + " / " + pages, new RectangleF(box.X + 112, box.Y + 442, 115, 32), 18, Art.Muted, TypeRole.Number, false, StringAlignment.Center);
                Button(g, "Next", box.Right - 106, box.Y + 442, 92, 32, delegate { ChangeUnitStackPage(1); }, false, false);
            }
            else Button(g, "Read this place", box.X + 14, box.Y + 442, box.Width - 28, 32, ReadStackPlace, false, false);
            Button(g, "×", box.Right - 43, box.Y + 13, 27, 27, CloseMapSelection, false, false);
            MapTip("Close this list. Choosing a band or animal opens its individual record.");
        }

        private void DrawUnitStackRow(Graphics g, StackChoice choice, RectangleF row)
        {
            bool selectedBand = choice.Kind == UnitKind.Band && choice.Id == commandedBandId;
            bool hover = row.Contains(hoverPoint);
            Art.Fill(g, selectedBand ? Color.FromArgb(46, 52, 43) : hover ? Color.FromArgb(33, 46, 47) : Color.FromArgb(27, 39, 41), row.X, row.Y, row.Width, row.Height);
            if (selectedBand) Art.Line(g, Art.Gold, 2, row.X, row.Y + 5, row.X, row.Bottom - 5);
            if (choice.Kind == UnitKind.Band) IdentityArt.DrawEmblem(g, game.TribeOf(choice.Id), new RectangleF(row.X + 7, row.Y + 9, 28, 28), false);
            else IdentityArt.DrawAnimal(g, choice.Animal, new RectangleF(row.X + 6, row.Y + 12, 30, 24), Art.Gold, choice.Detail.StartsWith("Companions", StringComparison.Ordinal));
            Typography.Line(g, choice.Name, new RectangleF(row.X + 44, row.Y + 1, row.Width - 113, 25), 19, selectedBand ? Art.Gold : Art.Ink, TypeRole.Heading, true);
            Typography.Line(g, choice.Count.ToString("N0"), new RectangleF(row.Right - 68, row.Y + 1, 58, 27), 20, Art.Ink, TypeRole.Number, true, StringAlignment.Far);
            Typography.Line(g, choice.Detail, new RectangleF(row.X + 45, row.Y + 25, row.Width - 56, 20), 13.5f, Art.Muted, TypeRole.Annotation, true);
            UnitKind kind = choice.Kind; int id = choice.Id;
            buttons.Add(new UiButton(row, delegate { ChooseStackUnit(kind, id); })
            { Tip = choice.Name + (choice.Controlled ? ": select this household to give it orders." : ": inspect this group while keeping your current band in command.") });
        }

        private void ChangeUnitStackPage(int change)
        {
            if (!unitStackOpen || BlockingSheet) return;
            int pages = Math.Max(1, (UnitStackChoices().Length + UnitStackPageSize - 1) / UnitStackPageSize);
            unitStackPage = (unitStackPage + change + pages) % pages;
            buttons.Clear(); HideMapHover(); Invalidate();
        }

        private void ChooseStackUnit(UnitKind kind, int id)
        {
            if (BlockingSheet || !unitStackOpen) return;
            StackChoice choice = UnitStackChoices().FirstOrDefault(c => c.Kind == kind && c.Id == id);
            if (choice == null) { status = "That group has moved or left this place. Choose a group still listed here."; buttons.Clear(); Invalidate(); return; }
            int actor = commandedBandId, cell = unitStackCell;
            ClearUnitStack(); selected = cell;
            if (kind == UnitKind.Band)
            {
                inspectorPage = 1; inspectedBandId = id; selectedAnimalId = -1;
                if (game.CanControlBand(id)) ArmMapCommandBand(id);
                map.SelectedBandId = id; map.SelectedAnimalId = -1;
            }
            else
            {
                inspectorPage = 2; selectedAnimalId = id; inspectedBandId = -1;
                map.SelectedAnimalId = id; map.SelectedBandId = -1;
            }
            ShowMapSelection();
            // Earlier single-band stories also preserve the actor when choosing
            // a foreign or animal row from this observational list.
            if (!choice.Controlled) ArmMapCommandBand(game.CanControlBand(actor) ? actor : -1);
            buttons.Clear(); Invalidate();
        }

        private void ReadStackPlace()
        {
            if (!unitStackOpen || BlockingSheet) return;
            int cell = unitStackCell; ClearUnitStack(); selected = cell;
            inspectorPage = 0; inspectedBandId = -1; selectedAnimalId = -1;
            ShowMapSelection();
        }
    }
}
