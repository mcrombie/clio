using System;
using System.Globalization;
using System.IO;
using System.Linq;
using Clio.Simulation;

namespace Clio.Desktop
{
    public sealed partial class GameForm
    {
        // Selecting a unit remains presentation state. Only a recorded order
        // identifies an actor to the simulation.
        private Band CurrentOrderBand
        {
            get
            {
                if (!game.TribesEnabled) return game.Player;
                Band selectedBand = game.Bands.FirstOrDefault(b => b.Id == commandedBandId && game.CanControlBand(b.Id));
                return selectedBand ?? game.TribeLeaderBand ?? game.Player;
            }
        }
        private int CurrentOrderActions { get { return game.TribesEnabled ? game.ActionsFor(CurrentOrderBand.Id) : game.Actions; } }
        private int AvailableOrderActions { get { return game.TribesEnabled ? game.ControlledBands.Sum(b => game.ActionsFor(b.Id)) : game.Actions; } }

        private static bool TryBandCommand(string command, out int bandId, out string order)
        {
            bandId = -1; order = command;
            if (!command.StartsWith("band:", StringComparison.Ordinal)) return false;
            int split = command.IndexOf(':', 5);
            if (split < 6 || !Int32.TryParse(command.Substring(5, split - 5), NumberStyles.None, CultureInfo.InvariantCulture, out bandId) || bandId < 0 || split == command.Length - 1)
                throw new InvalidDataException("Invalid band order.");
            order = command.Substring(split + 1); return true;
        }
        private string CanonicalOrder(string command)
        {
            if (!game.TribesEnabled || command == "end" || command.StartsWith("enable-", StringComparison.Ordinal) || command.StartsWith("band:", StringComparison.Ordinal)) return command;
            if (!HasCommandBand) { status = "Select one of your tribe's living bands before giving an order."; return null; }
            return "band:" + commandedBandId.ToString(CultureInfo.InvariantCulture) + ":" + command;
        }
    }
}
