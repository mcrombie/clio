using System;
using System.Drawing;
using System.Linq;
using Clio.Simulation;

namespace Clio.Desktop
{
    public sealed partial class GameForm
    {
        private int economyResourcePage, saltSourcePage;
        private static Color SaltWarning { get { return Art.PaperMode ? MapPaper.Warning : Color.FromArgb(221, 145, 113); } }

        private void OpenSaltEconomy()
        { ClearMapTransient(); page = 1; economyPage = 2; economyResourcePage = 1; Invalidate(); }

        private static string SaltSourceLabel(SaltSource source)
        { return source == SaltSource.Coastal ? "Coastal salt" : source == SaltSource.Spring ? "Salt spring" : "No source"; }

        private int[] VisibleSaltSources()
        {
            int here = CurrentOrderBand.CellId;
            return SaltEconomy.KnownSources(game).Where(id => game.Explored.Contains(id))
                .OrderBy(id => id == here ? 0 : game.World.Cells[here].Neighbors.Contains(id) ? 1 : 2).ThenBy(id => id).ToArray();
        }

        private void InspectSaltSource(int cell)
        {
            // Revalidate when a button is used; the previous paint may be stale.
            if (!game.SaltEnabled || !SaltEconomy.KnownSources(game).Contains(cell)) { OpenSaltEconomy(); return; }
            selected = cell; selectedAnimalId = -1; inspectedBandId = -1; inspectorPage = 0; page = 0;
            map.Focus(game.World.Cells[cell]); ShowMapSelection(); Invalidate();
        }

        private void GatherHouseholdSalt()
        {
            Band band = CurrentOrderBand;
            if (!SaltEconomy.CanGather(game, band)) { OpenSaltEconomy(); return; }
            ArmMapCommandBand(band.Id); CloseMapMenus(); Command("salt");
        }

        private void DrawResourceTabs(Graphics g)
        {
            Art.Line(g, Border, 1, 863, 267, 863, 289);
            Button(g, "Food & care", 881, 262, 139, 32, delegate { economyResourcePage = 0; Invalidate(); }, economyResourcePage == 0, false);
            Button(g, "Salt", 1028, 262, 83, 32, delegate { economyResourcePage = 1; Invalidate(); }, economyResourcePage == 1, false);
            Button(g, "Wood", 1119, 262, 91, 32, delegate { economyResourcePage = 2; Invalidate(); }, economyResourcePage == 2, false);
            Typography.Line(g, CurrentOrderBand.Name + "  /  " + Timeline.Label(game, game.Turn), new RectangleF(1225, 262, 331, 31), 17, Art.Muted, TypeRole.Annotation, true, StringAlignment.Far);
        }

        private void DrawMapSaltReserve(Graphics g, RectangleF bounds)
        {
            if (bounds.Contains(hoverPoint)) Art.Fill(g, Art.PaperMode ? MapPaper.HoverWash : Color.FromArgb(34, 46, 49), bounds.X, bounds.Y, bounds.Width, bounds.Height);
            Typography.Label(g, "Salt", new RectangleF(bounds.X + 5, bounds.Y, 43, bounds.Height), 11, Art.Muted, .45f);
            double reserve = SaltEconomy.ReserveTurns(CurrentOrderBand);
            Typography.Line(g, SaltEconomy.Need(CurrentOrderBand) <= 0 ? "\u2014" : reserve.ToString("0.0") + " turns",
                new RectangleF(bounds.X + 54, bounds.Y, bounds.Width - 60, bounds.Height), 27,
                reserve < 1 || CurrentOrderBand.SaltShortageTurns > 0 ? SaltWarning : Art.Ink, TypeRole.Number, true);
            buttons.Add(new UiButton(bounds, OpenSaltEconomy));
            MapTip("Salt reserve at your current population. Open Resources for demand, shortages and known sources.");
        }

        private void DrawMapSaltOrder(Graphics g, RectangleF bounds)
        {
            bool gather = HasCommandBand && SaltEconomy.CanGather(game, CurrentOrderBand);
            Button(g, game.SaltEnabled ? "Gather salt" : "Salt resources", bounds.X, bounds.Y, bounds.Width, bounds.Height,
                delegate { if (HasCommandBand && SaltEconomy.CanGather(game, CurrentOrderBand)) GatherHouseholdSalt(); else OpenSaltEconomy(); }, gather, false);
            DrawSaltGlyph(g, new RectangleF(bounds.X + 16, bounds.Y + 10, 23, 24), Art.Gold);
            MapTip(gather ? "Gather five turns of salt at this source. Uses one action; need grows with population." :
                game.SaltEnabled ? "Open the salt ledger to find a known source. Select your band at a source to gather." : "Add salt reserves and gathering to this older story from the resource ledger.");
        }

        private static void DrawSaltGlyph(Graphics g, RectangleF bounds, Color ink)
        {
            using (Pen edge = new Pen(ink, Math.Max(.8f, bounds.Width / 28)))
            using (Brush wash = new SolidBrush(Color.FromArgb(42, ink)))
            for (int i = 0; i < 3; i++)
            {
                float x = bounds.X + bounds.Width * (.08f + i * .31f), w = bounds.Width * .25f;
                float bottom = bounds.Bottom - bounds.Height * .08f, top = bounds.Y + bounds.Height * (i == 1 ? .05f : i == 0 ? .37f : .45f);
                PointF[] crystal = { new PointF(x, top + w * .55f), new PointF(x + w * .48f, top), new PointF(x + w, top + w * .44f),
                    new PointF(x + w, bottom - w * .12f), new PointF(x + w * .44f, bottom), new PointF(x, bottom - w * .3f) };
                g.FillPolygon(wash, crystal); g.DrawPolygon(edge, crystal);
                g.DrawLine(edge, x + w * .48f, top + w * .58f, x + w * .44f, bottom);
                g.DrawLine(edge, x, top + w * .55f, x + w * .48f, top + w * .58f);
                g.DrawLine(edge, x + w, top + w * .44f, x + w * .48f, top + w * .58f);
            }
        }

        private void DrawSaltResources(Graphics g)
        {
            if (!game.SaltEnabled) { DrawUntrackedSalt(g); return; }
            Band band = CurrentOrderBand; EconomyForecast forecast = BandEconomy.Forecast(game, band);
            int[] sources = VisibleSaltSources(); double need = SaltEconomy.Need(band);
            Color condition = band.SaltShortageTurns > 0 || SaltEconomy.ReserveTurns(band) < 1 ? SaltWarning : Art.Ink;
            LedgerMetric(g, 42, "Salt reserve", band.Salt.ToString("N1"), "Salt travels with your household", Art.Gold);
            LedgerMetric(g, 348, "Supply at current size", need <= 0 ? "\u2014" : SaltEconomy.ReserveTurns(band).ToString("0.0") + " turns", "Coverage changes as the people grow", condition);
            LedgerMetric(g, 654, "Needed each turn", need.ToString("0.0"), "One salt unit for every ten people", Art.Ink);
            LedgerMetric(g, 960, "After this turn", forecast.EndingSalt.ToString("0.0"), "Remaining reserve at current conditions", forecast.EndingSaltShortageTurns > 0 ? SaltWarning : LedgerGreen);
            LedgerMetric(g, 1266, "Known sources", sources.Length.ToString("N0"), "Coastal salt and springs in explored land", Art.Ink);

            EconomyPanel(g, 42, 484, "I  /  The salt account");
            EconomyAmount(g, 63, 465, 442, "Starting reserve", forecast.StartingSalt, false);
            EconomyAmount(g, 63, 499, 442, "Salt supplied this turn", -forecast.SaltConsumed, true);
            EconomyAmount(g, 63, 533, 442, "Projected remaining salt", forecast.EndingSalt, false);
            Art.Rule(g, 63, 576, 441);
            EconomyRow(g, 63, 592, 442, "Consecutive deficient turns", band.SaltShortageTurns.ToString("N0"), condition);
            EconomyRow(g, 63, 627, 442, "Current gathering strength", (SaltEconomy.GatheringMultiplier(game, band) * 100).ToString("0") + "%", condition);
            EconomyRow(g, 63, 662, 442, "Salt deaths in this forecast", forecast.SaltLosses.ToString("N0"), forecast.SaltLosses > 0 ? SaltWarning : Art.Ink);
            Typography.Draw(g, band.SaltShortageTurns > 0 ? "The shortage ends after a fully supplied turn. Gathering alone does not clear the penalties." : "Forecast before other units act. Salt has no food-preservation bonus; it sustains the people themselves.", new RectangleF(63, 711, 442, 53), 16, Art.Muted, TypeRole.Annotation);

            EconomyPanel(g, 542, 486, "II  /  Replenish the reserve");
            SaltSource local = SaltEconomy.Source(game, band.CellId);
            DrawSaltGlyph(g, new RectangleF(563, 466, 35, 36), Art.Gold);
            Typography.Line(g, local == SaltSource.None ? "No source at your hearth" : SaltSourceLabel(local), new RectangleF(613, 458, 394, 49), 29, Art.Ink, TypeRole.Heading, true);
            string gathering = local == SaltSource.None ? "Travel to a known coastal source or salt spring. Gathering there uses one action and supplies five turns at your current size." :
                "One action here gathers " + SaltEconomy.GatherYield(game, band).ToString("0.0") + " salt: five turns of demand for your " + band.Population.ToString("N0") + " people.";
            Typography.Draw(g, gathering, new RectangleF(563, 523, 444, 65), 18, Art.Ink, TypeRole.Annotation);
            if (SaltEconomy.CanGather(game, band))
                Button(g, "Gather salt  +" + SaltEconomy.GatherYield(game, band).ToString("0.0"), 563, 600, 444, 40, GatherHouseholdSalt, true, false);
            else if (local != SaltSource.None)
                Typography.Line(g, game.IsOver ? "The household's story has ended" : "End the turn to gather again", new RectangleF(563, 601, 444, 38), 20, Art.Muted, TypeRole.Annotation, true);
            else if (sources.Length > 0)
            {
                int source = sources[0];
                Button(g, "Inspect a known source", 563, 600, 444, 40, delegate { InspectSaltSource(source); }, false, false);
            }
            else Typography.Line(g, "Explore the land to find salt", new RectangleF(563, 601, 444, 38), 20, Art.Muted, TypeRole.Annotation);
            Art.Rule(g, 563, 660, 444);
            Typography.Label(g, "When supply runs short", new RectangleF(563, 671, 444, 23), 12, Art.Gold, .5f);
            Typography.Draw(g, "Deficient turns cut gathering to 90%, 80%, then 70%; cohesion falls and births pause. Deaths begin on the fourth consecutive deficient turn.", new RectangleF(563, 704, 444, 59), 16, Art.Muted, TypeRole.Annotation);

            EconomyPanel(g, 1044, 514, "III  /  Sources your people know");
            if (sources.Length == 0)
            {
                DrawSaltGlyph(g, new RectangleF(1234, 489, 110, 106), Art.Muted);
                Typography.Draw(g, "No salt source is charted yet. Explore the land for coastal salt and mineral springs.", new RectangleF(1066, 636, 471, 81), 23, Art.Ink, TypeRole.Annotation);
            }
            else
            {
                saltSourcePage = Math.Min(saltSourcePage, (sources.Length - 1) / 5);
                for (int i = 0; i < 5 && saltSourcePage * 5 + i < sources.Length; i++)
                {
                    int cell = sources[saltSourcePage * 5 + i];
                    DrawSaltSourceLink(g, cell, new RectangleF(1065, 464 + i * 49, 472, 45));
                }
                if (sources.Length > 5) LedgerPager(g, 1065, 731, 472, saltSourcePage, sources.Length, 5, delegate(int next) { saltSourcePage = next; });
                else Typography.Line(g, "Select a source to inspect its place on the map.", new RectangleF(1065, 731, 472, 28), 17, Art.Muted, TypeRole.Annotation, true);
            }
        }

        private void DrawSaltSourceLink(Graphics g, int cell, RectangleF bounds)
        {
            if (bounds.Contains(hoverPoint)) Art.Fill(g, Art.PaperMode ? MapPaper.HoverWash : Color.FromArgb(36, 48, 49), bounds.X, bounds.Y, bounds.Width, bounds.Height);
            DrawSaltGlyph(g, new RectangleF(bounds.X + 2, bounds.Y + 7, 26, 28), Art.Gold);
            Typography.Line(g, game.Place(cell), new RectangleF(bounds.X + 41, bounds.Y - 1, bounds.Width - 68, 27), 20, Art.Ink, TypeRole.Heading, true);
            bool adjacent = game.World.Cells[CurrentOrderBand.CellId].Neighbors.Contains(cell);
            int cost = adjacent ? TravelRules.MoveCost(game, CurrentOrderBand, CurrentOrderBand.CellId, cell) : 0;
            string reach = cell == CurrentOrderBand.CellId ? "Your people are here" : adjacent && cost > 0 ? "Adjacent / " + cost + (cost == 1 ? " action" : " actions") + " to enter" : "In the remembered land";
            Typography.Line(g, SaltSourceLabel(SaltEconomy.Source(game, cell)) + "  /  " + reach, new RectangleF(bounds.X + 43, bounds.Y + 24, bounds.Width - 70, 21), 14, Art.Muted, TypeRole.Annotation, true);
            Typography.Line(g, "\u203a", new RectangleF(bounds.Right - 23, bounds.Y, 22, bounds.Height), 23, Art.Gold, TypeRole.Heading, false, StringAlignment.Center);
            buttons.Add(new UiButton(bounds, delegate { InspectSaltSource(cell); }));
        }

        private void DrawUntrackedSalt(Graphics g)
        {
            Art.Panel(g, new RectangleF(42, 305, 1516, 469), Panel, false);
            DrawSaltGlyph(g, new RectangleF(100, 388, 162, 158), Art.Gold);
            Typography.Label(g, "An older household record", new RectangleF(323, 363, 1124, 29), 13, Art.Gold, .8f);
            Typography.Line(g, "Salt is not tracked in this story", new RectangleF(319, 410, 1129, 60), 42, Art.Ink, TypeRole.Heading, true);
            Typography.Draw(g, "Enable salt to add a reserve your people must replenish at coastal sources and inland springs. Each living household begins with four turns of supply; the earlier record stays intact.", new RectangleF(323, 489, 1008, 86), 24, Art.Ink, TypeRole.Annotation);
            Button(g, "Enable salt needs", 325, 610, 299, 45, delegate { Command("enable-salt"); OpenSaltEconomy(); }, true, false);
            Typography.Draw(g, "Salt supports gathering, cohesion and growth. Long shortages can take lives. This rule adds no food-preservation bonus.", new RectangleF(325, 693, 1116, 57), 18, Art.Muted, TypeRole.Body);
        }
    }
}
