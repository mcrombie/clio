using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using Clio.Simulation;

namespace Clio.Desktop
{
    public sealed partial class GameForm
    {
        private static readonly Color LandscapeSalt = Color.FromArgb(212, 224, 209);
        private static readonly Color LandscapePaths = MapRenderer.FrontierInterestInk;

        private void OpenLandscapeGuide()
        {
            if (BlockingSheet) return;
            ClearMapTransient(); page = 0; mapMenu = 4; buttons.Clear(); Invalidate();
        }

        private void DrawFloatingMapPanel(Graphics g, RectangleF bounds)
        {
            Art.Fill(g, Color.FromArgb(35, 0, 0, 0), bounds.X + 2, bounds.Y + 3, bounds.Width, bounds.Height);
            Art.Panel(g, bounds, Color.FromArgb(24, 34, 37), false);
            Art.Line(g, Color.FromArgb(47, Art.Gold), .7f, bounds.Left + 13, bounds.Top, bounds.Right - 13, bounds.Top);
        }

        private string MapTravelHelp()
        {
            return game.TerrainTravelEnabled ?
                "Adjacent move: 1 action; mountains or river crossings: 2. Review occupied places before entering." :
                "Adjacent moves cost 1 action and provisions. Select a destination or right-click known land.";
        }

        private void DrawLandscapeKey(Graphics g)
        {
            RectangleF bounds = new RectangleF(332, 174, 407, 34);
            DrawFloatingMapPanel(g, bounds);
            if (mapMenu == 4 || bounds.Contains(hoverPoint)) Art.Fill(g, Color.FromArgb(30, Art.Gold), bounds.X, bounds.Y, bounds.Width, bounds.Height);
            bool useful = game.TerrainTravelEnabled && map.InterestHighlights && map.Layer <= 1;
            DrawLandscapeMark(g, 0, new RectangleF(345, 182, 18, 18), useful ? Art.Gold : Art.Muted);
            Typography.Line(g, "Food", new RectangleF(371, 176, 83, 30), 17, useful ? Art.Ink : Art.Muted, TypeRole.Annotation);
            DrawLandscapeMark(g, 1, new RectangleF(466, 181, 20, 20), game.SaltEnabled ? LandscapeSalt : Art.Muted);
            Typography.Line(g, "Salt", new RectangleF(496, 176, 76, 30), 17, game.SaltEnabled ? Art.Ink : Art.Muted, TypeRole.Annotation);
            DrawLandscapeMark(g, 2, new RectangleF(581, 181, 20, 20), useful && map.Layer == 0 ? LandscapePaths : Art.Muted);
            Typography.Line(g, "Explore", new RectangleF(612, 176, 75, 30), 17, useful && map.Layer == 0 ? Art.Ink : Art.Muted, TypeRole.Annotation);
            Typography.Line(g, mapMenu == 4 ? "\u2039" : "\u203a", new RectangleF(704, 175, 22, 30), 21, Art.Gold, TypeRole.Heading, false, StringAlignment.Center);
            buttons.Add(new UiButton(bounds, delegate { ToggleMapMenu(4); }));
            MapTip("Hover a leaf, crystal or blue compass on the map for its opportunity and movement cost. Click to inspect that place.");
        }

        private static void DrawLandscapeMark(Graphics g, int kind, RectangleF bounds, Color ink)
        {
            if (kind == 0) MapRenderer.DrawFoodInterestIcon(g, bounds);
            else if (kind == 1) MapRenderer.DrawSaltSourceIcon(g, SaltSource.Spring, bounds, true);
            else MapRenderer.DrawFrontierInterestIcon(g, bounds);
            if (ink == Art.Muted) Art.Fill(g, Color.FromArgb(88, Art.Background), bounds.X, bounds.Y, bounds.Width, bounds.Height);
        }

        private void DrawLandscapeGuide(Graphics g, RectangleF bounds)
        {
            LandscapeGuideRow(g, bounds, 0, 56, "Food opportunity", "A leaf marks at least twice this band's food needs per Gather. Move here, then Gather food to collect provisions.", Art.Gold);
            LandscapeGuideRow(g, bounds, 1, 130, "Salt source", "A crystal marks known coastal salt or a salt spring. Travel to the source, then spend an action to gather salt.", LandscapeSalt);
            LandscapeGuideRow(g, bounds, 2, 204, "Exploration opportunity", "A blue compass marks a known hex beside unknown land. Hover for a travel note; move here to extend your map.", LandscapePaths);
            Art.Rule(g, bounds.X + 20, bounds.Y + 286, bounds.Width - 40);
            Typography.Label(g, "The effort of travel", new RectangleF(bounds.X + 20, bounds.Y + 298, bounds.Width - 40, 24), 12, Art.Gold, .6f);
            Typography.Draw(g, game.TerrainTravelEnabled ?
                "Ordinary step: 1 action. Entering mountains or crossing a river: 2, even when both apply. Mountains remain passable." :
                "This story uses 1 action per land step. Enable mountain and river costs below; past turns keep their original rules.",
                new RectangleF(bounds.X + 20, bounds.Y + 331, bounds.Width - 40, 43), 16, Art.Ink, TypeRole.Annotation);
            Button(g, map.InterestHighlights ? "Useful glows: on" : "Useful glows: off", bounds.X + 20, bounds.Y + 388, 209, 34,
                delegate { map.InterestHighlights = !map.InterestHighlights; Invalidate(); }, map.InterestHighlights, false);
            MapTip("Show food opportunities on Terrain and Food, and frontier paths on Terrain. Salt markers remain available.");
            if (!game.TerrainTravelEnabled)
            {
                Button(g, "Enable terrain travel", bounds.X + 241, bounds.Y + 388, 209, 34,
                    delegate { CloseMapMenus(); Command("enable-terrain"); }, false, false);
                MapTip("Record the new travel rule: mountains and river crossings cost two actions. Earlier turns keep their original outcomes.");
            }
            else
            {
                Button(g, "Salt resources", bounds.X + 241, bounds.Y + 388, 209, 34, OpenSaltEconomy, false, false);
                MapTip("Open the selected household's salt reserve, demand and known sources.");
            }
        }

        private void LandscapeGuideRow(Graphics g, RectangleF bounds, int kind, float y, string title, string description, Color color)
        {
            DrawLandscapeMark(g, kind, new RectangleF(bounds.X + 21, bounds.Y + y + 7, 26, 26), color);
            Typography.Line(g, title, new RectangleF(bounds.X + 60, bounds.Y + y - 3, bounds.Width - 81, 28), 22, Art.Ink, TypeRole.Heading, true);
            Typography.Draw(g, description, new RectangleF(bounds.X + 61, bounds.Y + y + 26, bounds.Width - 82, 43), 15, Art.Muted, TypeRole.Annotation);
        }

        private void DrawMapEndTurn(Graphics g, RectangleF bounds)
        {
            bool hover = bounds.Contains(hoverPoint);
            using (LinearGradientBrush wash = new LinearGradientBrush(bounds, Color.FromArgb(hover ? 96 : 73, 66, 44), Color.FromArgb(42, 43, 35), 0f))
                g.FillRectangle(wash, bounds);
            Art.Line(g, Color.FromArgb(160, Art.Gold), 1, bounds.Left, bounds.Top, bounds.Right, bounds.Top);
            Art.Line(g, Color.FromArgb(90, Art.Gold), 1, bounds.Left, bounds.Bottom, bounds.Right, bounds.Bottom);
            Typography.Line(g, "End turn", new RectangleF(bounds.X + 12, bounds.Y + 1, bounds.Width - 65, 26), 22, Art.Ink, TypeRole.Heading, true);
            Typography.Label(g, "Space", new RectangleF(bounds.X + 14, bounds.Y + 26, bounds.Width - 70, 16), 11, Art.Gold, .9f);
            float cx = bounds.Right - 26, cy = bounds.Y + bounds.Height / 2;
            using (Pen rim = new Pen(Art.Gold, 1.2f)) g.DrawEllipse(rim, cx - 18, cy - 18, 36, 36);
            using (Pen fine = new Pen(Color.FromArgb(98, Art.Gold), .7f)) g.DrawEllipse(fine, cx - 14.5f, cy - 14.5f, 29, 29);
            Art.Icon(g, "sun", cx - 9, cy - 9, 18, Art.Gold);
            buttons.Add(new UiButton(bounds, delegate { CloseMapMenus(); Command("end"); }));
        }
    }
}
