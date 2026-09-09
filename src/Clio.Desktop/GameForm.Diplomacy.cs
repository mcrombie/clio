using System;
using System.Drawing;
using System.Linq;
using Clio.Simulation;

namespace Clio.Desktop
{
    public sealed partial class GameForm
    {
        private int diplomacySelectedTribe = -1, diplomacyListPage, diplomacyDetailPage;
        private const int DiplomacyPerPage = 5;

        private void ResetDiplomacy()
        { diplomacySelectedTribe = -1; diplomacyListPage = diplomacyDetailPage = 0; gatheringSelectedId = -1; }

        private void SelectDiplomacyPolity(int tribe)
        {
            if (page != 5 || BlockingSheet) return;
            DiplomacyState report = DiplomacyReport.Evaluate(game);
            if (!report.Unlocked || !report.Polities.Any(p => p.TribeId == tribe)) return;
            diplomacySelectedTribe = tribe; diplomacyDetailPage = 0; gatheringSelectedId = -1; gatheringSiteId = -1; buttons.Clear(); Invalidate();
        }

        private void ChangeDiplomacyPage(int direction)
        {
            if (page != 5 || BlockingSheet) return;
            DiplomacyState report = DiplomacyReport.Evaluate(game);
            diplomacyListPage = Math.Max(0, Math.Min(Math.Max(0, (report.Polities.Count - 1) / DiplomacyPerPage), diplomacyListPage + direction));
            DiplomacyPolity first = report.Polities.Skip(diplomacyListPage * DiplomacyPerPage).FirstOrDefault();
            diplomacySelectedTribe = first == null ? -1 : first.TribeId; buttons.Clear(); Invalidate();
        }

        private void InspectDiplomacyPolity(int tribe)
        {
            if (page != 5 || BlockingSheet) return;
            DiplomacyState report = DiplomacyReport.Evaluate(game);
            DiplomacyPolity polity = report.Unlocked ? report.Polities.FirstOrDefault(p => p.TribeId == tribe) : null;
            if (polity == null) { status = "This people is no longer observed in the known land."; buttons.Clear(); Invalidate(); return; }
            StopAutoplay("Diplomacy: their observed band is selected. You are in control.");
            ClearMapTransient(); page = 0; selected = polity.CellId; inspectedBandId = polity.RepresentativeBandId;
            inspectorPage = 1; selectedAnimalId = -1;
            map.Focus(game.World.Cells[selected]); ShowMapSelection(); Invalidate();
        }

        private bool DiplomacyNeighbor(DiplomacyPolity polity)
        {
            return game.Bands.Where(b => polity.KnownBandIds.Contains(b.Id) && b.Population > 0 && game.Explored.Contains(b.CellId))
                .Any(b => game.ControlledBands.Any(own => own.CellId == b.CellId || game.World.Cells[own.CellId].Neighbors.Contains(b.CellId)));
        }

        private void DrawDiplomacy(Graphics g)
        {
            DiplomacyState report = DiplomacyReport.Evaluate(game);
            bool populated = report.Unlocked && report.Polities.Count > 0;
            Surface(g, "Diplomacy", populated ? "The peoples beyond our hearth, and the ties between us." : "");
            // A shared household is still part of the tribe. Keep the page blank
            // until an actual secession creates an independent people.
            if (!populated) { if (DrawGatheringArchiveWithoutPolity(g, report.Unlocked)) return; ResetDiplomacy(); return; }
            Gathering remembered = game.Gatherings.FirstOrDefault(r => r.Id == gatheringSelectedId);
            if (remembered != null && !report.Polities.Any(p => p.TribeId == remembered.GuestTribeId))
            { DrawGatheringArchiveWithoutPolity(g, true); return; }
            diplomacyListPage = Math.Max(0, Math.Min(diplomacyListPage, (report.Polities.Count - 1) / DiplomacyPerPage));
            DiplomacyPolity[] rows = report.Polities.Skip(diplomacyListPage * DiplomacyPerPage).Take(DiplomacyPerPage).ToArray();
            DiplomacyPolity chosen = report.Polities.FirstOrDefault(p => p.TribeId == diplomacySelectedTribe);
            if (chosen == null) { chosen = rows[0]; diplomacySelectedTribe = chosen.TribeId; }
            Typography.Label(g, "Known peoples", new RectangleF(45, 298, 474, 23), 12, Art.Gold, .7f);
            Typography.Line(g, report.Polities.Count + (report.Polities.Count == 1 ? " independent polity" : " independent polities"), new RectangleF(1050, 296, 480, 26), 17, Art.Muted, TypeRole.Annotation, true, StringAlignment.Far);
            for (int i = 0; i < rows.Length; i++)
            {
                DiplomacyPolity polity = rows[i]; RectangleF row = new RectangleF(44, 336 + i * 76, 474, 66);
                bool selectedPolity = polity.TribeId == diplomacySelectedTribe;
                Art.Panel(g, row, selectedPolity ? Color.FromArgb(40, 49, 46) : Panel, false);
                IdentityArt.DrawEmblem(g, polity.TribeId, new RectangleF(row.X + 13, row.Y + 15, 36, 36), false);
                Typography.Line(g, polity.Name, new RectangleF(row.X + 65, row.Y + 5, 393, 33), 25, Art.Ink, TypeRole.Heading, true);
                Typography.Line(g, (polity.Relation == DiplomaticRelation.Enemy ? "Enemy" : "Neutral") + (polity.FromPlayerTribe ? " / A people of our lineage" : " / Independent people"), new RectangleF(row.X + 67, row.Y + 37, 389, 23), 16, polity.Relation == DiplomaticRelation.Enemy ? BandPanelWarning : Art.Muted, TypeRole.Annotation, true);
                int tribe = polity.TribeId; buttons.Add(new UiButton(row, delegate { SelectDiplomacyPolity(tribe); }));
            }
            if (report.Polities.Count > DiplomacyPerPage)
            {
                Button(g, "Previous", 44, 738, 133, 32, delegate { ChangeDiplomacyPage(-1); }, false, false);
                Typography.Line(g, (diplomacyListPage + 1) + " / " + ((report.Polities.Count + DiplomacyPerPage - 1) / DiplomacyPerPage), new RectangleF(183, 738, 166, 32), 18, Art.Muted, TypeRole.Number, false, StringAlignment.Center);
                Button(g, "Next", 385, 738, 133, 32, delegate { ChangeDiplomacyPage(1); }, false, false);
            }
            DrawDiplomacyDetail(g, chosen);
        }
    }
}
