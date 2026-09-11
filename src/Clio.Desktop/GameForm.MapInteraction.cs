using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace Clio.Desktop
{
    public sealed partial class GameForm
    {
        private bool mapSelectionOpen, mapHoverVisible;
        private int hoverKind = -1, hoverId = -1;
        private string hoverTip = "";
        private PointF mapHoverAnchor;
        private readonly Timer mapHoverTimer = new Timer { Interval = 420 };

        private void ShowMapSelection()
        {
            SyncMapCommandSelection();
            CloseMapMenus(); HideMapHover(); bandDetailsOpen = false; mapCardExpanded = false; mapSelectionOpen = true;
            buttons.Clear(); Invalidate();
        }
        private void CloseMapSelection()
        { mapSelectionOpen = false; mapCardExpanded = false; ClearUnitStack(); HideMapHover(); buttons.Clear(); Invalidate(); }
        private void ClearMapTransient()
        { mapSelectionOpen = false; mapCardExpanded = false; bandDetailsOpen = false; ClearUnitStack(); CloseMapMenus(); CloseCampaignSystemMenu(); HideMapHover(); map.OrderPreviewCell = -1; }
        private void HideMapHover()
        {
            bool highlighted = map.HoverOpportunityCell >= 0;
            mapHoverTimer.Stop(); mapHoverVisible = false; hoverKind = -1; hoverId = -1; hoverTip = "";
            map.HoverOpportunityKind = -1; map.HoverOpportunityCell = -1;
            if (highlighted) Invalidate();
        }
        private RectangleF MapToastBounds
        { get { return NotificationCueBounds(58); } }
        private bool MapOverlayContains(PointF point)
        {
            return page == 0 && (CampaignHeaderContains(point) || CampaignDockContains(point) || CampaignFeedbackContains(point)) ||
                AdviserToastVisible && AdviserToastBounds.Contains(point) || BandPanelContains(point) || MapMenuContains(point) || mapSelectionOpen && MapSelectionBounds.Contains(point) ||
                RoutineNoticeVisible && MapToastBounds.Contains(point);
        }
        // Overlay backgrounds consume presses too. Empty space on a card must never
        // select the terrain beneath it or begin a drag.
        private bool HandleMapOverlayDown(PointF point)
        {
            if (page != 0 || BlockingSheet) return false;
            if (MapOverlayContains(point))
            {
                for (int i = buttons.Count - 1; i >= 0; i--)
                    if (buttons[i].Bounds.Contains(point)) { buttons[i].Click(); return true; }
                return true;
            }
            if (mapMenu != 0 || CampaignSystemMenuOpen)
            {
                // A visible toolbar/dock control can be used immediately. A
                // background press only dismisses, without selecting through it.
                for (int i = buttons.Count - 1; i >= 0; i--)
                    if (buttons[i].Bounds.Contains(point)) { buttons[i].Click(); return true; }
                CloseMapMenus(); CloseCampaignSystemMenu(); buttons.Clear(); Invalidate(); return true;
            }
            if (mapSelectionOpen && map.Bounds.Contains(point) && !buttons.Any(b => b.Bounds.Contains(point))) CloseMapSelection();
            if (bandDetailsOpen && map.Bounds.Contains(point) && !buttons.Any(b => b.Bounds.Contains(point))) CloseBandDetails();
            return false;
        }
        private void UpdateMapHover(PointF point)
        {
            if (BlockingSheet || page == 0 && (dragging || map.IsNavigating))
            { HideMapHover(); return; }
            int kind = -1, id = -1; string tip = "";
            UiButton control = buttons.LastOrDefault(b => b.Bounds.Contains(point));
            if (control != null)
            {
                if (!String.IsNullOrEmpty(control.Tip)) { kind = 3; id = buttons.IndexOf(control); tip = control.Tip; }
            }
            else if (page == 0 && !MapOverlayContains(point) && map.Bounds.Contains(point))
            {
                int stackCell = map.PickUnitStack(point.X, point.Y);
                if (stackCell >= 0 && game.Explored.Contains(stackCell))
                {
                    kind = 3; id = stackCell; tip = "Several groups share this place. Click the count to choose a band or inspect wildlife; every group remains separate.";
                }
                else
                {
                id = map.PeekAnimal(point.X, point.Y);
                if (!game.Beasts.Any(b => b.Id == id && b.Count > 0 && game.Explored.Contains(b.CellId))) id = -1;
                if (id >= 0) kind = 2;
                else { id = map.PickBand(point.X, point.Y);
                    if (!game.Bands.Any(b => b.Id == id && b.Population > 0 && game.Explored.Contains(b.CellId))) id = -1;
                    if (id >= 0) kind = 1;
                    else { id = map.PickOpportunity(game, point.X, point.Y, out kind);
                        if (id < 0) { id = map.Pick(point.X, point.Y); if (id >= 0) kind = 0; } } }
                }
            }
            if (kind < 0) { bool shown = mapHoverVisible; HideMapHover(); if (shown) Invalidate(); return; }
            if (kind == hoverKind && id == hoverId && tip == hoverTip) return;
            HideMapHover(); hoverKind = kind; hoverId = id; hoverTip = tip; mapHoverAnchor = point;
            if (kind >= MapRenderer.FoodOpportunity) { map.HoverOpportunityKind = kind; map.HoverOpportunityCell = id; }
            mapHoverTimer.Start(); Invalidate();
        }
        private void RevealMapHover()
        {
            mapHoverTimer.Stop();
            if (BlockingSheet || page == 0 && (dragging || map.IsNavigating) || hoverKind < 0 || page != 0 && hoverKind != 3) return;
            if (hoverKind == 1 && !game.Bands.Any(b => b.Id == hoverId && b.Population > 0 && game.Explored.Contains(b.CellId)) ||
                hoverKind == 2 && !game.Beasts.Any(b => b.Id == hoverId && b.Count > 0 && game.Explored.Contains(b.CellId)) ||
                hoverKind >= MapRenderer.FoodOpportunity && !map.IsOpportunityVisible(game, hoverKind, hoverId))
            { HideMapHover(); return; }
            mapHoverVisible = true; Invalidate();
        }
        private void DrawMapHoverOverlay(Graphics g)
        {
            if (!mapHoverVisible || BlockingSheet || page == 0 && (dragging || map.IsNavigating) || page != 0 && hoverKind != 3) return;
            if (hoverKind != 3) { DrawMapHover(g, hoverKind, hoverId, mapHoverAnchor); return; }
            RectangleF box = MapTooltipBounds(g, hoverTip, mapHoverAnchor);
            Art.Panel(g, box, Color.FromArgb(28, 40, 43), false);
            Art.Line(g, Art.Gold, 1, box.X, box.Y, box.Right, box.Y);
            Typography.Draw(g, hoverTip, new RectangleF(box.X + 15, box.Y + 12, box.Width - 30, box.Height - 24), 16, Art.Ink, TypeRole.Body);
        }

        private RectangleF MapTooltipBounds(Graphics g, string text, PointF anchor)
        {
            // Fit the explanation before positioning it. Longer supply and unit
            // tips need more than the old three-line box, especially for learners.
            float height;
            using (StringFormat format = new StringFormat { Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.LineLimit })
                height = (float)Math.Ceiling(g.MeasureString(text ?? "", Typography.Font(TypeRole.Body, 16), new SizeF(350, 10000), format).Height) + 24;
            height = Math.Max(76, Math.Min(420, height));
            float x = Math.Max(24, Math.Min(1196, anchor.X + 18));
            float y = anchor.Y > 780 ? anchor.Y - height - 13 : anchor.Y + 25;
            return new RectangleF(x, Math.Max(103, Math.Min(914 - height, y)), 380, height);
        }
    }
}
