using System;
using System.Drawing;
using Clio.Simulation;

namespace Clio.Desktop
{
    public sealed partial class GameForm
    {
        private void DrawOverviewBar(Graphics g) { DrawCompactStatus(g); }
        private void DrawCommandDock(Graphics g)
        {
            DrawFloatingMapPanel(g, new RectangleF(16, 860, 1568, 59));
            Button(g, "Return to map", 32, 868, 166, 43, delegate { ClearMapTransient(); page = 0; buttons.Clear(); Invalidate(); }, false, false);
            MapTip("Return to the map to give orders. Your current selection is kept.");
            Typography.Line(g, CurrentOrderBand.Name, new RectangleF(224, 871, 450, 35), 22, Art.Ink, TypeRole.Heading, true);
            if (semiautomatic)
            {
                Button(g, "Decisions", 778, 868, 172, 43, OpenStoryChoiceHistory, false, false);
                MapTip("Read chosen directions and their consequences.");
            }
            DrawCompactPlayControls(g);
        }
    }
}
