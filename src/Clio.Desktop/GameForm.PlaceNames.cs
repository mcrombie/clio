using System;
using System.Drawing;
using System.Linq;
using Clio.Simulation;

namespace Clio.Desktop
{
    public sealed partial class GameForm
    {
        private void DrawPlaceNameInspector(Graphics g)
        {
            PlaceKnowledge place = game.KnownPlace(game.Player.Id, selected);
            Typography.Label(g, "A name remembered", new RectangleF(1294, 257, 269, 25), 12, Art.Gold);
            FittedTitle(g, game.Place(selected), new RectangleF(1291, 292, 274, 84), 32, Art.Ink);
            Art.Rule(g, 1294, 394, 269);
            if (place == null)
            {
                Typography.Draw(g, game.CulturalPlaceNames ? "Your people have no name for this hex yet. Seeing it in the atlas does not discover it." : "This story uses the earlier place labels. Begin recording names to give every remembered hex a name in your people's tongue.",
                    new RectangleF(1295, 422, 267, 137), 20, Art.Ink, TypeRole.Annotation);
                if (!game.CulturalPlaceNames)
                    Button(g, "Begin recording names", 1295, 602, 267, 39, delegate { Command("enable-place-names"); }, true, false);
                return;
            }
            string source = place.Acquisition == PlaceAcquisition.Discovered ? "Named by your people" :
                place.Acquisition == PlaceAcquisition.Shared ? "Learned from another people" : "Carried from the parent people";
            Typography.Line(g, source, new RectangleF(1294, 416, 269, 31), 21, Art.Gold, TypeRole.Heading, true);
            Typography.Line(g, "Remembered in " + Timeline.Label(game, place.LearnedTurn).ToLowerInvariant(), new RectangleF(1295, 453, 267, 26), 16, Art.Muted, TypeRole.Annotation);
            Band donor = game.Bands.FirstOrDefault(b => b.Id == place.LearnedFromBandId);
            string account;
            if (place.Acquisition == PlaceAcquisition.Discovered)
            {
                LanguageProfile tongue = game.Languages.FirstOrDefault(l => l.Id == place.OriginLanguageId);
                account = "Your people found this hex firsthand and named it" + (tongue == null ? " in their own tongue." : " in " + tongue.Name + ".") + " The name stays in their memory as their language develops.";
            }
            else if (place.Acquisition == PlaceAcquisition.Shared)
                account = (donor == null ? "Another people passed on this place name." : donor.Name + " passed on this place name.") + " Your people keep the word exactly as they heard it, including when they later arrive here.";
            else
                account = "The daughter people carried this name with them when they separated. Their inherited map keeps the names already known to the parent people.";
            Typography.Draw(g, account, new RectangleF(1295, 501, 267, 144), 18, Art.Ink, TypeRole.Body);
            Art.Rule(g, 1294, 665, 269);
            Typography.Draw(g, "Independent discovery makes a local name. Peaceful contact passes on names for places still unknown. An existing name is never overwritten.",
                new RectangleF(1295, 688, 267, 90), 15, Art.Muted, TypeRole.Annotation);
        }
    }
}
