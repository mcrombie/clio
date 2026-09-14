using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using Clio.Simulation;

namespace Clio.Desktop
{
    public sealed partial class GameForm
    {
        private GuidedRegionDestination[] guidedTravelRoutes = new GuidedRegionDestination[0];
        private readonly List<Tuple<RectangleF, int>> guidedTravelRows = new List<Tuple<RectangleF, int>>();
        private int guidedPreviewCell = -1;
        private bool guidedTravelCameraSaved;
        private double guidedBeforeTravelLongitude, guidedBeforeTravelLatitude, guidedBeforeTravelZoom;

        private void BeginGuidedTravelPreview()
        {
            if (!guidedTravelCameraSaved)
            {
                guidedBeforeTravelLongitude = map.Longitude; guidedBeforeTravelLatitude = map.Latitude; guidedBeforeTravelZoom = map.Zoom;
                guidedTravelCameraSaved = true;
            }
            guidedTravelRoutes = game.GuidedDestinations();
            guidedPreviewCell = guidedTravelRoutes.Length > 0 ? guidedTravelRoutes[0].CellId : -1;
            guidedTravelRows.Clear(); guidedRoutePage = 0;
            map.SetBounds(campaignSurface);
            map.FitGuidedTravel(game, guidedTravelRoutes, GuidedTravelMapArea());
            buttons.Clear(); hoverPoint = new PointF(-1, -1); Invalidate();
        }

        private RectangleF GuidedTravelMapArea()
        {
            // Reserve only the drawer, rather than shrinking the entire world.
            float left = Math.Max(408, campaignSurface.Left + 20);
            return new RectangleF(left, campaignSurface.Top + 95, Math.Max(240, campaignSurface.Right - left - 28),
                Math.Max(200, campaignSurface.Height - 170));
        }

        private void EndGuidedTravelPreview(bool movedToRegion)
        {
            if (guidedTravelCameraSaved)
            {
                map.Longitude = guidedBeforeTravelLongitude; map.Latitude = guidedBeforeTravelLatitude; map.Zoom = guidedBeforeTravelZoom;
                if (movedToRegion) map.Focus(game.World.Cells[game.Player.CellId]);
                map.InvalidateTerrain();
            }
            guidedTravelCameraSaved = false; guidedTravel = false; guidedPreviewCell = -1;
            guidedTravelRoutes = new GuidedRegionDestination[0]; guidedTravelRows.Clear(); map.ClearGuidedTravelPreview();
            buttons.Clear(); hoverPoint = new PointF(-1, -1); Invalidate();
        }

        private void DrawGuidedTravelPreview(Graphics g)
        {
            map.DrawGuidedTravelPreview(g, game, guidedTravelRoutes, guidedPreviewCell);
            guidedTravelRows.Clear();
            GuidedSheet(g, new RectangleF(24, 112, 348, 584));
            Typography.Label(g, "Move / 1 influence", new RectangleF(44, 132, 305, 22), 12, MapPaper.Russet);
            Typography.Line(g, "Choose a region", new RectangleF(42, 168, 309, 40), 28, MapPaper.Ink, TypeRole.Heading, true);
            Typography.Draw(g, "Hover to see the route. Click a row or a numbered pin to travel there.",
                new RectangleF(45, 216, 299, 47), 17, MapPaper.MutedInk, TypeRole.Body);
            int pages = Math.Max(1, (guidedTravelRoutes.Length + 3) / 4);
            guidedRoutePage = Math.Max(0, Math.Min(pages - 1, guidedRoutePage));
            for (int i = guidedRoutePage * 4; i < Math.Min(guidedTravelRoutes.Length, guidedRoutePage * 4 + 4); i++)
            {
                GuidedRegionDestination route = guidedTravelRoutes[i];
                RectangleF bounds = new RectangleF(44, 282 + (i % 4) * 66, 307, 59);
                bool selectedRoute = route.CellId == guidedPreviewCell;
                MapPaper.Surface(g, bounds, false, selectedRoute);
                Typography.Line(g, (i + 1).ToString(), new RectangleF(bounds.X + 7, bounds.Y + 10, 28, 33), 23,
                    selectedRoute ? MapPaper.Russet : MapPaper.Ink, TypeRole.Number, false, StringAlignment.Center);
                Typography.Line(g, GuidedBearing(route.CellId), new RectangleF(bounds.X + 44, bounds.Y + 4, 249, 26), 19, MapPaper.Ink, TypeRole.Heading);
                Typography.Line(g, route.Name, new RectangleF(bounds.X + 44, bounds.Y + 31, 249, 22), 15, MapPaper.MutedInk, TypeRole.Body, true);
                guidedTravelRows.Add(Tuple.Create(bounds, route.CellId));
                buttons.Add(new UiButton(bounds, delegate { RunGuidedCommand("guided-move:" + route.CellId.ToString(CultureInfo.InvariantCulture)); }));
            }
            if (guidedTravelRoutes.Length == 0)
                Typography.Draw(g, "No neighboring land region is reachable. Your tribe can remain here and gather supplies.", new RectangleF(46, 300, 299, 129), 20, MapPaper.Ink, TypeRole.Body);
            Typography.Draw(g, "Solid ink: known route.\nDashes: uncharted ground ahead.", new RectangleF(46, 555, 299, 44), 16, MapPaper.MutedInk, TypeRole.Annotation);
            GuidedButton(g, "Back", new RectangleF(44, 625, 105, 44), delegate { EndGuidedTravelPreview(false); }, "Return to the map without spending influence. [Esc]");
            if (pages > 1)
            {
                GuidedButton(g, "‹", new RectangleF(179, 625, 40, 44), delegate { ChangeGuidedTravelPage(-1); }, "Previous routes.", guidedRoutePage > 0);
                Typography.Line(g, (guidedRoutePage + 1) + " / " + pages, new RectangleF(223, 625, 74, 44), 17, MapPaper.MutedInk, TypeRole.Body, false, StringAlignment.Center);
                GuidedButton(g, "›", new RectangleF(305, 625, 46, 44), delegate { ChangeGuidedTravelPage(1); }, "More routes.", guidedRoutePage + 1 < pages);
            }
            GuidedRegionDestination active = guidedTravelRoutes.FirstOrDefault(d => d.CellId == guidedPreviewCell);
            if (active != null)
            {
                RectangleF caption = new RectangleF(423, 29, 600, 68);
                GuidedSheet(g, caption, false);
                Typography.Line(g, GuidedBearing(active.CellId) + " · " + active.Name, new RectangleF(440, 36, 565, 29), 21, MapPaper.Ink, TypeRole.Heading, true);
                Typography.Line(g, active.Known ? "The pin marks your arrival. Only remembered land is outlined." : "The pin marks your arrival. The land and its resources remain unknown.",
                    new RectangleF(441, 69, 565, 21), 15, MapPaper.MutedInk, TypeRole.Body, true);
            }
            Typography.Line(g, "Drag to pan · Scroll to zoom", new RectangleF(1090, 905, 450, 31), 17, MapPaper.MutedInk, TypeRole.Annotation, false, StringAlignment.Far);
        }

        private void ChangeGuidedTravelPage(int step)
        {
            guidedRoutePage += step;
            int index = guidedRoutePage * 4;
            if (index >= 0 && index < guidedTravelRoutes.Length) guidedPreviewCell = guidedTravelRoutes[index].CellId;
        }

        private bool UpdateGuidedTravelHover(PointF point)
        {
            if (!guidedTravel || dragging) return false;
            int cell = -1;
            foreach (Tuple<RectangleF, int> row in guidedTravelRows)
                if (row.Item1.Contains(point)) { cell = row.Item2; break; }
            if (cell < 0 && !guidedPanels.Any(panel => panel.Contains(point))) cell = map.PickGuidedArrival(point);
            if (cell < 0) return false;
            if (cell != guidedPreviewCell)
            {
                guidedPreviewCell = cell;
                int index = Array.FindIndex(guidedTravelRoutes, route => route.CellId == cell);
                if (index >= 0) guidedRoutePage = index / 4;
                Invalidate();
            }
            return true;
        }

        private bool TryGuidedTravelMapClick(PointF point)
        {
            if (!guidedTravel || guidedPanels.Any(panel => panel.Contains(point))) return false;
            int cell = map.PickGuidedArrival(point);
            if (cell < 0) return false;
            RunGuidedCommand("guided-move:" + cell.ToString(CultureInfo.InvariantCulture)); return true;
        }
    }
}
