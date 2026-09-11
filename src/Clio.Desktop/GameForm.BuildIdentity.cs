using System.Drawing;

namespace Clio.Desktop
{
    public sealed partial class GameForm
    {
        // Compiled into the application so a copied or pinned old executable
        // cannot advertise the build selected by today's launcher record.
        internal const string BuildIdentity = "vellum-34";
        internal const string BuildWindowTitle = "Clio — The living atlas [" + BuildIdentity + "]";

        private void DrawBuildIdentity(Graphics g)
        {
            RectangleF bounds = new RectangleF(1200, 930, 144, 25);
            Typography.Line(g, BuildIdentity, bounds, 12, Art.Muted, TypeRole.Utility, false, StringAlignment.Far);
            buttons.Add(new UiButton(bounds, delegate { })
            {
                Tip = "Running Clio " + BuildIdentity + ". The desktop and taskbar shortcuts open the latest completed build. An already-open game keeps its current version until you save and reopen."
            });
        }
    }
}
