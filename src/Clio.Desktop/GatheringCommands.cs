using System;
using System.Globalization;
using System.IO;

namespace Clio.Desktop
{
    public sealed partial class GameForm
    {
        private static void ValidateGatheringSyntax(string order)
        {
            string[] parts = order.Split(':'); int id, site; double food, salt;
            bool valid = parts.Length == 3 && parts[0] == "gather-invite" &&
                Int32.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out id) &&
                Int32.TryParse(parts[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out site) ||
                parts.Length == 2 && parts[0] == "gather-return" && Int32.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out id) ||
                parts.Length == 4 && parts[0] == "gather-aid" && Int32.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out id) &&
                Double.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out food) && !Double.IsNaN(food) && !Double.IsInfinity(food) &&
                Double.TryParse(parts[3], NumberStyles.Float, CultureInfo.InvariantCulture, out salt) && !Double.IsNaN(salt) && !Double.IsInfinity(salt);
            if (!valid) throw new InvalidDataException("Malformed gathering order.");
        }
    }
}
