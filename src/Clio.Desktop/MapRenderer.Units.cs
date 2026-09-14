using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using Clio.Simulation;

namespace Clio.Desktop
{
    internal sealed partial class MapRenderer
    {
        public int SelectedAnimalId = -1, SelectedBandId = -1, CommandedBandId = -1;
        public bool AnimateUnits = true;
        // Remain active until Draw has painted the final position, even after the clock expires.
        public bool Animating { get { return AnimateUnits && animationEndsAt != 0; } }
        private const double MoveSeconds = .58;
        private long animationEndsAt;
        private Game unitGame;
        private World unitWorld;
        private bool unitFog;
        private Region unitClip;
        private readonly List<Tuple<RectangleF, int>> animalTargets = new List<Tuple<RectangleF, int>>();
        private readonly Dictionary<int, UnitMotion> animalMotion = new Dictionary<int, UnitMotion>();
        private readonly Dictionary<int, UnitMotion> bandMotion = new Dictionary<int, UnitMotion>();
        private readonly HashSet<int> previousUnitCells = new HashSet<int>();
        private readonly List<RectangleF> unitLabelBounds = new List<RectangleF>();

        private sealed class UnitMotion
        {
            internal int CellId, FromCell;
            internal Vec3 From, To;
            internal PointF FromOffset, Offset;
            internal long Started;
            internal bool Moving;
        }
        private sealed class UnitMark
        {
            internal Beast Animal;
            internal Band Band;
            internal ProjectedCell Cell;
            internal PointF Position;
            internal float Width, Height;
            internal bool Detailed, Moving;
        }
        // Repeated clicks cycle every overlapping group. The stable selected ID is the cursor,
        // so rendering never needs to mutate the player's selection or simulation.
        public int PickAnimal(float x, float y)
        {
            if (!Bounds.Contains(x, y) || (unitClip != null && !unitClip.IsVisible(x, y))) return -1;
            List<int> hits = animalTargets.Where(t => t.Item1.Contains(x, y)).Select(t => t.Item2).Distinct().OrderBy(id => id).ToList();
            if (hits.Count == 0) return -1;
            int index = hits.IndexOf(SelectedAnimalId);
            return index < 0 ? hits[0] : hits[(index + 1) % hits.Count];
        }

        // Hover observes a stack without advancing its click-cycle selection.
        public int PeekAnimal(float x, float y)
        {
            if (!Bounds.Contains(x, y) || (unitClip != null && !unitClip.IsVisible(x, y))) return -1;
            int[] hits = animalTargets.Where(t => t.Item1.Contains(x, y)).Select(t => t.Item2).Distinct().OrderBy(id => id).ToArray();
            return hits.Contains(SelectedAnimalId) ? SelectedAnimalId : hits.Length > 0 ? hits[0] : -1;
        }

        private bool VisibleEndpoint(Game game, int cell)
        {
            ProjectedCell p;
            return Known(game, cell) && projected.TryGetValue(cell, out p) && p.Depth > .07 && Bounds.Contains(p.Center);
        }
        private static float Progress(UnitMotion motion, long now)
        {
            if (!motion.Moving) return 1;
            double progress = Math.Max(0, Math.Min(1, (now - motion.Started) / (MoveSeconds * Stopwatch.Frequency)));
            return (float)(progress * progress * (3 - 2 * progress));
        }
        private PointF UnitPosition(Game game, int id, int cell, PointF offset, Dictionary<int, UnitMotion> tracks, long now, out bool moving)
        {
            UnitMotion motion;
            Vec3 destination = game.World.Cells[cell].Center;
            if (!tracks.TryGetValue(id, out motion))
            {
                motion = new UnitMotion { CellId = cell, FromCell = cell, From = destination, To = destination, FromOffset = offset, Offset = offset };
                tracks[id] = motion;
            }
            if (motion.CellId != cell)
            {
                // A skipped chapter, a newly visible arrival, or a path through fog snaps into
                // its observed location. Only a witnessed step between adjacent cells moves.
                bool witnessed = AnimateUnits && VisibleEndpoint(game, motion.CellId) && VisibleEndpoint(game, cell)
                    && previousUnitCells.Contains(motion.CellId) && previousUnitCells.Contains(cell)
                    && game.World.Cells[motion.CellId].Neighbors.Contains(cell)
                    && (!motion.Moving || VisibleEndpoint(game, motion.FromCell));
                float priorProgress = Progress(motion, now);
                motion.From = witnessed ? (motion.From * (1 - priorProgress) + motion.To * priorProgress).Normalized() : destination;
                motion.FromOffset = witnessed ? Lerp(motion.FromOffset, motion.Offset, priorProgress) : offset;
                motion.FromCell = motion.CellId; motion.CellId = cell; motion.To = destination;
                motion.Started = now; motion.Moving = witnessed;
            }
            motion.Offset = offset;
            if (!AnimateUnits || !VisibleEndpoint(game, cell) || (motion.Moving && !VisibleEndpoint(game, motion.FromCell))) motion.Moving = false;
            float t = Progress(motion, now);
            if (t >= 1) motion.Moving = false;
            moving = motion.Moving;
            if (moving) animationEndsAt = Math.Max(animationEndsAt, motion.Started + (long)(MoveSeconds * Stopwatch.Frequency));
            PointF anchor = Project(moving ? (motion.From * (1 - t) + motion.To * t).Normalized() : destination);
            PointF slide = moving ? Lerp(motion.FromOffset, offset, t) : offset;
            return new PointF(anchor.X + slide.X, anchor.Y + slide.Y);
        }

        private void PrepareUnits(Game game)
        {
            if (unitGame != game || unitWorld != game.World || unitFog != Fog || !AnimateUnits)
            {
                animalMotion.Clear(); bandMotion.Clear(); previousUnitCells.Clear(); animationEndsAt = 0;
                unitGame = game; unitWorld = game.World; unitFog = Fog;
            }
            if (unitClip != null) unitClip.Dispose();
            using (GraphicsPath land = new GraphicsPath(FillMode.Winding))
            {
                foreach (ProjectedCell p in Visible) if (Known(game, p.Cell.Id) && p.Depth > .015) land.AddPolygon(p.Polygon);
                unitClip = new Region(); unitClip.MakeEmpty();
                if (land.PointCount > 0) unitClip.Union(land);
                unitClip.Intersect(Bounds);
            }
        }

        private void AddTarget(List<Tuple<RectangleF, int>> targets, RectangleF box, int id)
        {
            RectangleF clipped = RectangleF.Intersect(Bounds, box);
            if (clipped.Width > 0 && clipped.Height > 0 && unitClip.IsVisible(clipped)) targets.Add(Tuple.Create(clipped, id));
        }

        private void DrawLife(Graphics g, Game game, int selected)
        {
            PrepareUnits(game); animalTargets.Clear(); bandTargets.Clear(); unitStackTargets.Clear(); unitLabelBounds.Clear(); animationEndsAt = 0;
            long now = Stopwatch.GetTimestamp();
            List<UnitMark> animals = new List<UnitMark>(), bands = new List<UnitMark>();
            List<ProjectedCell> stacks = new List<ProjectedCell>();
            HashSet<int> liveAnimals = new HashSet<int>(), liveBands = new HashSet<int>();
            // Group once per frame. No ecology work, random draws or discovery updates occur here.
            // Wildlife remains observable in guided play even before animal
            // orders are introduced. The ordinary stack layout keeps it compact.
            var animalCells = game.Beasts.Where(b => b.Count > 0 && Known(game, b.CellId)).GroupBy(b => b.CellId).ToDictionary(b => b.Key, b => b.OrderBy(a => a.Id).ToList());
            var bandCells = game.Bands.Where(b => b.Population > 0 && Known(game, b.CellId)).GroupBy(b => b.CellId).ToDictionary(b => b.Key, b => b.OrderBy(a => a.Id).ToList());
            foreach (ProjectedCell p in Visible)
            {
                if (!Known(game, p.Cell.Id) || p.Depth < .015) continue;
                List<Beast> herd; List<Band> people;
                animalCells.TryGetValue(p.Cell.Id, out herd); bandCells.TryGetValue(p.Cell.Id, out people);
                int bandCount = people == null ? 0 : people.Count;
                int groupCount = bandCount + (herd == null ? 0 : herd.Count);
                bool crowded = bandCount > 1 || groupCount > 2 || p.Radius < 37 && groupCount > 1;
                if (crowded)
                {
                    // One representative party and, if space permits, one
                    // animal counter. The roster badge gives access to every
                    // real unit without expanding the map into a list of names.
                    stacks.Add(p);
                    if (people != null)
                    {
                        Band representative = people.FirstOrDefault(b => b.Id == CommandedBandId) ?? people.FirstOrDefault(b => b.Id == SelectedBandId) ??
                            people.FirstOrDefault(b => game.CanControlBand(b.Id)) ?? people[0];
                        bool moving;
                        PointF position = UnitPosition(game, representative.Id, p.Cell.Id, new PointF(0, herd != null ? -9 : 8), bandMotion, now, out moving);
                        liveBands.Add(representative.Id);
                        bands.Add(new UnitMark { Band = representative, Cell = p, Position = position, Detailed = Zoom >= 1.7, Moving = moving });
                    }
                    if (herd != null && (people == null || p.Radius >= 37))
                    {
                        Beast representative = herd.FirstOrDefault(b => b.Id == SelectedAnimalId) ??
                            herd.OrderByDescending(b => EncounterRules.Animal(game, b).Hostile).ThenBy(b => b.Id).First();
                        bool detailed = Zoom > 2 && p.Radius >= 22, moving;
                        float width = detailed ? Math.Min(58, Math.Max(40, p.Radius * .94f)) : Math.Min(27, Math.Max(15, p.Radius * 1.5f));
                        PointF position = UnitPosition(game, representative.Id, p.Cell.Id, new PointF(0, people != null ? Math.Min(30, p.Radius * .5f) : 0), animalMotion, now, out moving);
                        liveAnimals.Add(representative.Id);
                        animals.Add(new UnitMark { Animal = representative, Cell = p, Position = position, Width = width, Height = detailed ? 34 : width * .82f, Detailed = detailed, Moving = moving });
                    }
                    continue;
                }
                if (herd != null)
                {
                    bool detailed = Zoom > 2 && p.Radius >= 22;
                    float width = detailed ? Math.Min(58, Math.Max(40, p.Radius * .94f)) : Math.Min(27, Math.Max(15, p.Radius * 1.5f));
                    float height = detailed ? 34 : width * .82f;
                    float span = Math.Max(width * .75f, p.Radius * 1.6f);
                    int columns = Math.Min(herd.Count, Math.Max(1, Math.Min(3, (int)(span / (detailed ? 40 : width)))));
                    if (columns > 1) width = Math.Min(width, (span - (columns - 1) * 3) / columns);
                    int rows = (herd.Count + columns - 1) / columns;
                    float stepX = Math.Min(width + 3, span / Math.Max(1, columns));
                    float stepY = Math.Min(height + 3, p.Radius * (bandCount > 0 ? .65f : 1.05f) / Math.Max(1, rows - 1));
                    for (int i = 0; i < herd.Count; i++)
                    {
                        int row = i / columns, inRow = Math.Min(columns, herd.Count - row * columns);
                        PointF offset = new PointF((i % columns - (inRow - 1) * .5f) * stepX,
                            (row - (rows - 1) * .5f) * stepY + (bandCount > 0 ? Math.Min(30, p.Radius * .5f) : 0));
                        bool moving;
                        PointF position = UnitPosition(game, herd[i].Id, p.Cell.Id, offset, animalMotion, now, out moving);
                        liveAnimals.Add(herd[i].Id);
                        animals.Add(new UnitMark { Animal = herd[i], Cell = p, Position = position, Width = width, Height = height, Detailed = detailed, Moving = moving });
                    }
                }
                if (people != null)
                    for (int i = 0; i < people.Count; i++)
                    {
                        float spread = Math.Min(42, Math.Max(14, p.Radius * 1.3f / Math.Max(1, people.Count)));
                        PointF offset = new PointF((i - (people.Count - 1) * .5f) * spread, herd != null ? -9 : 8);
                        bool moving;
                        PointF position = UnitPosition(game, people[i].Id, p.Cell.Id, offset, bandMotion, now, out moving);
                        liveBands.Add(people[i].Id);
                        bands.Add(new UnitMark { Band = people[i], Cell = p, Position = position, Detailed = Zoom >= 1.7, Moving = moving });
                    }
            }
            foreach (int id in animalMotion.Keys.Where(id => !liveAnimals.Contains(id)).ToArray()) animalMotion.Remove(id);
            foreach (int id in bandMotion.Keys.Where(id => !liveBands.Contains(id)).ToArray()) bandMotion.Remove(id);
            previousUnitCells.Clear();
            foreach (ProjectedCell cell in Visible) if (VisibleEndpoint(game, cell.Cell.Id)) previousUnitCells.Add(cell.Cell.Id);

            GraphicsState state = g.Save(); g.SetClip(unitClip, CombineMode.Intersect);
            UnitMark commandMark = bands.FirstOrDefault(m => m.Band.Id == CommandedBandId);
            if (commandMark != null) unitLabelBounds.Add(new RectangleF(commandMark.Position.X - 100, commandMark.Position.Y - (commandMark.Detailed ? 84 : 53), 200, 28));
            foreach (UnitMark mark in bands.OrderBy(m => m.Position.Y)) DrawBandUnit(g, game, mark);
            // Selection always stays above a dense stack, while repeated picking cycles all IDs.
            foreach (UnitMark mark in animals.OrderBy(m => m.Animal.Id == SelectedAnimalId).ThenBy(m => m.Position.Y))
            {
                RectangleF box = new RectangleF(mark.Position.X - mark.Width * .5f, mark.Position.Y - mark.Height * .5f, mark.Width, mark.Height);
                UnitArt.DrawAnimalCounter(g, mark.Animal, EncounterRules.Animal(game, mark.Animal), box,
                    mark.Animal.Id == SelectedAnimalId, mark.Detailed, game.Rules == SimulationRules.MobileUnits);
                if (mark.Moving) DrawMotionMark(g, mark.Position, mark.Width, mark.Height, mark.Animal.Domestic ? UnitArt.MapAccent : UnitArt.MapRoute);
                AddTarget(animalTargets, box, mark.Animal.Id);
                if (mark.Animal.Id == SelectedAnimalId) AddTarget(animalTargets, new RectangleF(box.X - 20, box.Y - 19, box.Width + 40, 17), mark.Animal.Id);
            }
            foreach (ProjectedCell stack in stacks) DrawUnitStackBadge(g, game, stack);
            g.Restore(state);
        }

        private void DrawBandUnit(Graphics g, Game game, UnitMark mark)
        {
            Band band = mark.Band; float x = mark.Position.X, y = mark.Position.Y;
            int identity = game.TribeOf(band.Id);
            Color color = UnitArt.MapIdentity(identity);
            bool commanded = band.Id == CommandedBandId && game.CanControlBand(band.Id), inspecting = band.Id == SelectedBandId;
            RectangleF target = mark.Detailed ? new RectangleF(x - 25, y - 56, 63, 64) : new RectangleF(x - 14, y - 26, 28, 30);
            if (mark.Detailed) UnitArt.DrawBandParty(g, band, new PointF(x, y), (float)Math.Min(1.04, .69 + Zoom * .065), inspecting, commanded, mark.Moving, identity);
            else
            {
                if (inspecting || commanded)
                    using (Pen selected = new Pen(Color.FromArgb(225, commanded ? UnitArt.MapAccent : color), 2)) g.DrawEllipse(selected, x - 15, y - 5, 30, 9);
                IdentityArt.DrawEmblem(g, identity, new RectangleF(x - 13, y - 26, 26, 30), true);
                if (commanded)
                    using (Brush command = new SolidBrush(UnitArt.MapAccent))
                        g.FillPolygon(command, new[] { new PointF(x + 10, y - 27), new PointF(x + 13, y - 24), new PointF(x + 10, y - 21), new PointF(x + 7, y - 24) });
            }
            if (game.Rules == SimulationRules.MobileUnits)
            {
                UnitProfile profile = EncounterRules.Band(game, band);
                if (profile.Hostile)
                    using (Pen warning = new Pen(UnitArt.MapDanger, 1.7f))
                    {
                        g.DrawLine(warning, x - 18, y - 37, x - 7, y - 26);
                        g.DrawLine(warning, x - 18, y - 26, x - 7, y - 37);
                    }
                if (profile.Wounds > 0 || inspecting || commanded)
                {
                    float fraction = profile.MaxHealth <= 0 ? 0 : (float)profile.CurrentHealth / profile.MaxHealth;
                    using (Pen empty = new Pen(Color.FromArgb(90, UnitArt.MapInk), 3)) g.DrawLine(empty, x - 17, y + 5, x + 17, y + 5);
                    using (Pen life = new Pen(profile.Wounds > 0 ? UnitArt.MapDanger : UnitArt.MapHealthy, 2)) g.DrawLine(life, x - 17, y + 5, x - 17 + 34 * fraction, y + 5);
                }
            }
            AddTarget(bandTargets, target, band.Id);
            if (mark.Moving) DrawMotionMark(g, new PointF(x, y + 5), 36, 7, color);
            if (Zoom > 2.6 || game.CanControlBand(band.Id) || Layer == 4)
            {
                // Above the standard leaves room below it for separate animal counters.
                RectangleF label = new RectangleF(x - 96, y - (mark.Detailed ? 82 : 51), 192, 24);
                if (!commanded && unitLabelBounds.Any(b => b.IntersectsWith(label))) return;
                unitLabelBounds.Add(label);
                using (Brush plaque = new SolidBrush(Color.FromArgb(242, UnitArt.MapPaper))) g.FillRectangle(plaque, label);
                using (Pen rule = new Pen(Color.FromArgb(145, color), .8f))
                { g.DrawLine(rule, label.X + 8, label.Bottom - 1, label.Right - 8, label.Bottom - 1); g.DrawLine(rule, label.X + 2, label.Y + 4, label.X + 2, label.Bottom - 4); }
                if (commanded)
                    using (Brush command = new SolidBrush(UnitArt.MapAccent))
                        g.FillPolygon(command, new[] { new PointF(label.X + 15, label.Y + 8), new PointF(label.X + 19, label.Y + 12), new PointF(label.X + 15, label.Y + 16), new PointF(label.X + 11, label.Y + 12) });
                Typography.Line(g, band.Name, new RectangleF(label.X + (commanded ? 25 : 10), label.Y, label.Width - (commanded ? 35 : 20), label.Height), 15, color, TypeRole.Heading, true, StringAlignment.Center);
                AddTarget(bandTargets, label, band.Id);
            }
        }

        private static void DrawMotionMark(Graphics g, PointF point, float width, float height, Color color)
        {
            // A restrained wake makes automatic travel visible without inventing a route.
            using (Pen pen = new Pen(Color.FromArgb(160, color), 1.2f))
            {
                float y = point.Y + height * .5f + 3;
                g.DrawLine(pen, point.X - width * .25f, y, point.X + width * .25f, y);
                g.DrawLine(pen, point.X - width * .14f, y + 3, point.X + width * .14f, y + 3);
            }
        }
    }
}
