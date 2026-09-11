using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using Clio.Simulation;

namespace Clio.Desktop
{
    // Focused regressions for discovery masking and presentation identity. Fixtures never enter a player's game.
    internal static class VisualChecks
    {
        private const int Seed = 73421;
        internal static void Run()
        {
            Stopwatch total = Stopwatch.StartNew();
            double regional = 0, warmed = 0, cached = 0, navigating = 0, atlas = 0, ignored;
            using (MapRenderer map = new MapRenderer())
            {
                map.AnimateUnits = false;
                Require(map.Fog, "New renderers must default to known land.");
                foreach (int layer in new[] { 0, 3, 4 })
                {
                    Game game = NewGame();
                    map.Focus(game.World.Cells[game.Player.CellId]); map.Zoom = 4.4; map.Layer = layer; map.Fog = true;
                    string state = State(game);
                    map.InvalidateTerrain();
                    string known = RenderHash(map, game, out ignored);
                    if (layer == 0)
                    {
                        regional = ignored;
                        Require(known == RenderHash(map, game, out cached), "Cached redraw changed known-land pixels.");
                        map.InvalidateTerrain(); Require(known == RenderHash(map, game, out warmed), "Regenerated terrain changed known-land pixels.");
                        map.IsNavigating = true; RenderHash(map, game, out navigating); map.IsNavigating = false;
                        Require(known == RenderHash(map, game, out ignored), "Camera preview changed the final illustration.");
                    }
                    map.Fog = false; map.InvalidateTerrain();
                    string exposed = RenderHash(map, game, out ignored);
                    Require(known != exposed, "Atlas must expose the hidden map in layer " + layer + ".");
                    if (layer == 0)
                    {
                        map.Zoom = .93; map.InvalidateTerrain(); RenderHash(map, game, out atlas); map.Zoom = 4.4;
                    }
                    map.Fog = true; map.InvalidateTerrain();
                    Require(known == RenderHash(map, game, out ignored), "Atlas/known-land roundtrip changed pixels in layer " + layer + ".");
                    Require(state == State(game), "Rendering or discovery toggles changed simulation state.");

                    ChangeUnknownWorld(game);
                    string changedState = State(game);
                    // Direct fixture mutations do not necessarily change normal renderer cache keys.
                    map.InvalidateTerrain();
                    Require(known == RenderHash(map, game, out ignored), "Unexplored terrain, inhabitants, or overlay influence leaked in layer " + layer + ".");
                    map.Fog = false; map.InvalidateTerrain();
                    Require(exposed != RenderHash(map, game, out ignored), "Atlas did not reveal changed hidden content in layer " + layer + ".");
                    Require(changedState == State(game), "Drawing the adversarial fixture changed its simulation state.");
                }
            }
            CheckFissionIdentity();
            Console.WriteLine("PASS: exact-pixel known-land privacy in Terrain, Speech and Polities; hidden-neighbor terrain/band/wildlife mutations; Atlas positive controls; toggle/state invariance including RNG; actual daughter-band identity reconstruction.");
            Console.WriteLine(String.Format(CultureInfo.InvariantCulture,
                "Observed map draw times at seed {0}, 1600x960: first regional {1:0.0} ms; warmed full redraw {5:0.0} ms; camera preview {6:0.0} ms; cached regional {2:0.0} ms; atlas overview {3:0.0} ms. Visual checks total {4:0.00} s. These are local single-draw observations, not a frame-rate guarantee.",
                Seed, regional, cached, atlas, total.Elapsed.TotalSeconds, warmed, navigating));
        }
        private static Game NewGame()
        { return new Game(new GameSettings(Seed, LanguageStyle.Flowing, Ancestry.Human, false, "")); }

        private static void ChangeUnknownWorld(Game game)
        {
            // The extra band sits immediately outside discovery, close enough to affect an incorrectly filtered influence overlay.
            Cell neighbor = game.Explored.SelectMany(id => game.World.Cells[id].Neighbors).Distinct()
                .Where(id => !game.Explored.Contains(id) && game.World.Cells[id].IsLand)
                .Select(id => game.World.Cells[id])
                .OrderByDescending(cell => Vec3.Dot(cell.Center, game.World.Cells[game.Player.CellId].Center)).First();
            foreach (Cell cell in game.World.Cells.Where(cell => !game.Explored.Contains(cell.Id)))
            {
                cell.Terrain = cell.IsLand ? Terrain.Ocean : Terrain.Desert;
                cell.Elevation = -cell.Elevation + .031;
                cell.Moisture = (cell.Moisture + .37) % 1;
                cell.Temperature = (cell.Temperature + .43) % 1;
                cell.Forage = (cell.Forage + .61) % 1;
                cell.RegionId += 997;
            }
            // Retain a plausible land position for the adversarial band without making it discovered.
            neighbor.Terrain = Terrain.Desert;
            foreach (Beast beast in game.Beasts.Where(beast => !game.Explored.Contains(beast.CellId)))
            { beast.Count = 0; beast.CellId = neighbor.Id; beast.Kind = BeastKind.Dragon; }
            game.Beasts.Add(new Beast { Id = game.Beasts.Count, CellId = neighbor.Id, Count = 1, Kind = BeastKind.Dragon });
            int language = game.Languages.Count;
            game.Languages.Add(LanguageGenerator.Branch(game.Languages[0], language, Seed + 191));
            game.Bands.Add(new Band { Id = 5, Name = "Unobserved test people", CellId = neighbor.Id, Population = 72, Food = 900, LanguageId = language });
            Require(!game.Explored.Contains(neighbor.Id) && neighbor.Neighbors.Any(game.Explored.Contains), "Hidden overlay source must be just outside discovery.");
        }

        private static string RenderHash(MapRenderer map, Game game, out double milliseconds)
        {
            using (Bitmap bitmap = new Bitmap(1600, 960, PixelFormat.Format32bppArgb))
            {
                using (Graphics g = Graphics.FromImage(bitmap))
                {
                    g.Clear(Color.Black); g.SmoothingMode = SmoothingMode.AntiAlias;
                    g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
                    Stopwatch timer = Stopwatch.StartNew(); map.Draw(g, game, game.Player.CellId);
                    milliseconds = timer.Elapsed.TotalMilliseconds;
                }
                return PixelHash(bitmap);
            }
        }
        private static string PixelHash(Bitmap bitmap)
        {
            BitmapData data = bitmap.LockBits(new Rectangle(0, 0, bitmap.Width, bitmap.Height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            try
            {
                byte[] pixels = new byte[Math.Abs(data.Stride) * bitmap.Height];
                Marshal.Copy(data.Scan0, pixels, 0, pixels.Length);
                using (SHA256 hash = SHA256.Create()) return Convert.ToBase64String(hash.ComputeHash(pixels));
            }
            finally { bitmap.UnlockBits(data); }
        }
        private static Game FissionFixture()
        {
            Game game = NewGame(); game.Player.Population = 120; game.Player.Food = 1200;
            game.Split();
            Require(game.Bands.Count == 2 && game.Bands[1].LanguageId == game.Player.LanguageId, "Actual fission must create a distinct band sharing the parent language.");
            return game;
        }
        private static void CheckFissionIdentity()
        {
            Game first = FissionFixture(), reconstructed = FissionFixture();
            Band parent = first.Player, daughter = first.Bands[1], replayed = reconstructed.Bands[1];
            Require(parent.Id != daughter.Id && IdentityArt.ColorFor(parent.Id) != IdentityArt.ColorFor(daughter.Id), "The newly formed daughter must have a distinct identity despite shared language.");
            string original = EmblemHash(daughter.Id);
            Require(original != EmblemHash(parent.Id), "Actual parent and daughter emblems must look different.");
            Require(original == EmblemHash(replayed.Id) && IdentityArt.ColorFor(daughter.Id) == IdentityArt.ColorFor(replayed.Id), "Reconstructed daughter-band identity must match its original drawing.");
        }
        private static string EmblemHash(int id)
        {
            using (Bitmap bitmap = new Bitmap(80, 80, PixelFormat.Format32bppArgb))
            {
                using (Graphics g = Graphics.FromImage(bitmap))
                { g.Clear(Color.Black); g.SmoothingMode = SmoothingMode.AntiAlias; IdentityArt.DrawEmblem(g, id, new RectangleF(8, 8, 64, 64), false); }
                return PixelHash(bitmap);
            }
        }
        private static string State(Game game)
        {
            StringBuilder value = new StringBuilder();
            value.Append(game.Seed).Append('/').Append(game.Turn).Append('/').Append(game.Actions);
            foreach (Band band in game.Bands)
                value.Append('|').Append(band.Id).Append('/').Append(band.CellId).Append('/').Append(band.Population).Append('/').Append(band.LanguageId)
                    .Append('/').Append(band.Food.ToString("R", CultureInfo.InvariantCulture)).Append('/').Append(band.Cohesion.ToString("R", CultureInfo.InvariantCulture));
            foreach (Beast beast in game.Beasts) value.Append('|').Append(beast.Id).Append('/').Append(beast.CellId).Append('/').Append(beast.Count);
            foreach (int id in game.Explored.OrderBy(id => id)) value.Append(',').Append(id);
            foreach (double depletion in game.Depletion) value.Append('/').Append(depletion.ToString("R", CultureInfo.InvariantCulture));
            value.Append('|').Append(game.Languages.Count).Append('/').Append(game.Chronicle.Count);
            foreach (FieldInfo field in typeof(Game).GetFields(BindingFlags.NonPublic | BindingFlags.Instance).OrderBy(field => field.Name))
            {
                if (field.FieldType == typeof(uint) || field.FieldType == typeof(int)) value.Append('|').Append(field.Name).Append('=').Append(field.GetValue(game));
            }
            return value.ToString();
        }
        private static void Require(bool condition, string message)
        { if (!condition) throw new InvalidOperationException("Visual check failed: " + message); }
    }
}
