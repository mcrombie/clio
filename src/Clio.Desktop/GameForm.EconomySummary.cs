using System;
using System.Drawing;
using System.Linq;
using Clio.Simulation;

namespace Clio.Desktop
{
    public sealed partial class GameForm
    {
        private void DrawEconomySummary(Graphics g)
        {
            Band own = CurrentOrderBand;
            EconomyForecast forecast = BandEconomy.Forecast(game, own);
            DomesticEconomy domestic = BandEconomy.DomesticEffects(game, own);
            double needs = game.Upkeep(own) + domestic.AnimalCare;
            bool active = EconomyActive;
            Band[] households = game.ControlledBands.ToArray();
            Band[] known = EconomyKnownPartners();
            int hostile = known.Count(b => EncounterRules.BandsHostile(game, own.Id, b.Id));
            int nearbyHostile = known.Count(b => EncounterRules.BandsHostile(game, own.Id, b.Id) &&
                (b.CellId == own.CellId || game.World.Cells[own.CellId].Neighbors.Contains(b.CellId)));
            int gatherings = game.Gatherings.Count(meeting => meeting.Active);

            Typography.Label(g, "This band's supplies", new RectangleF(42, 310, 1250, 23), 12, Art.Muted, .6f);
            string foodStatus = !active ? "Story ended" : forecast.HungerLosses > 0 ? "Food shortfall" : own.Food < needs * 2 ? "Limited reserve" : "Supplied";
            string foodTip = "Food / " + own.Name + "\n" +
                "Reserve: " + own.Food.ToString("0.0") + ". People need " + forecast.Upkeep.ToString("0.0") + " each turn; animal care needs " + domestic.AnimalCare.ToString("0.0") + ".\n" +
                "Current forecast: camp +" + forecast.CampFood.ToString("0.0") + (game.LivestockEnabled ? ", milk +" : ", cattle +") + forecast.CattleFood.ToString("0.0") +
                "; " + forecast.Spoilage.ToString("0.0") + " lost to spoilage. Ending food: " + forecast.EndingFood.ToString("0.0") + ".\n" +
                "Care is paid first, then production is added and people eat. Keep " + (game.Known("stores") ? "98%" : "95%") + " of what remains; food never falls below zero.\n" +
                "Before other units act. Click for food sources and the full calculation.";
            DrawEconomySummaryCard(g, new RectangleF(42, 342, 492, 168), "leaf", "Food", foodStatus,
                "Food needs at the next turn's close", forecast.HungerLosses > 0 ? SaltWarning : Art.Ink,
                foodTip, delegate { economyResourcePage = 0; OpenEconomy(2); });

            double saltTurns = SaltEconomy.ReserveTurns(own);
            string saltStatus = !game.SaltEnabled ? "Not tracked" : !active ? "Story ended" : own.SaltShortageTurns > 0 ? "Shortage" : saltTurns < 2 ? "Running low" : "Supplied";
            string saltTip = !game.SaltEnabled ? "Salt\nSalt needs are not enabled in this older story. Click to open the salt ledger." :
                "Salt / " + own.Name + "\n" +
                "Reserve: " + own.Salt.ToString("0.0") + "; need: " + SaltEconomy.Need(own).ToString("0.0") + " per turn (people / 10). This is " + saltTurns.ToString("0.0") + " turns at this band's current size.\n" +
                "Forecast use: " + forecast.SaltConsumed.ToString("0.0") + "; remaining: " + forecast.EndingSalt.ToString("0.0") + ". Current shortage streak: " + own.SaltShortageTurns + " turns.\n" +
                "Gathering here gives " + SaltEconomy.GatherYield(game, own).ToString("0.0") + " salt per action. Click for sources and shortage effects.";
            DrawEconomySummaryCard(g, new RectangleF(554, 342, 492, 168), "salt", "Salt", saltStatus,
                "Supply and gathering sources", game.SaltEnabled && active && saltTurns < 2 ? SaltWarning : Art.Ink,
                saltTip, OpenSaltEconomy);

            bool fuel = WoodEconomy.HasFuel(game, own);
            string woodStatus = !game.WoodEnabled ? "Not tracked" : !active ? "Story ended" : fuel ? "Fire supplied" : "Collect wood";
            string woodTip = !game.WoodEnabled ? "Wood\nWood is not enabled in this older story. Click to open Wood & fire." :
                "Wood / " + own.Name + "\n" +
                "Reserve: " + own.Wood.ToString("0.0") + "; fire need: " + WoodEconomy.FuelNeed(own).ToString("0.0") + " per turn (max of 1 or people x 0.04).\n" +
                "Forecast use: " + forecast.WoodConsumed.ToString("0.0") + "; remaining: " + forecast.EndingWood.ToString("0.0") + "; food saved: " + forecast.FireFoodSaved.ToString("0.0") + ". A supplied fire cuts base food needs by 10% (rounded up) and prevents cold exposure.\n" +
                "Collect " + WoodEconomy.GatherYield(game, own).ToString("0.0") + " wood here per action. A new camp uses " + WoodEconomy.CampCost.ToString("0") + " wood. Click for the full fuel account.";
            DrawEconomySummaryCard(g, new RectangleF(1066, 342, 492, 168), "wood", "Wood", woodStatus,
                "Cooking fires and camp building", game.WoodEnabled && active && !fuel ? SaltWarning : Art.Ink,
                woodTip, OpenWoodEconomy);

            Typography.Label(g, "Your people and their relationships", new RectangleF(42, 534, 1250, 23), 12, Art.Muted, .6f);
            int population = game.TribesEnabled ? game.TribePopulation : game.Player.Population;
            string peopleStatus = households.Length + (households.Length == 1 ? " band" : " bands");
            string peopleTip = "People\n" + population.ToString("N0") + " living people in " + households.Length + " controlled bands. Selected band: " + own.Name + " (" + own.Population + " people).\n" +
                "Recorded since the start: " + journal.Totals.Births + " births, " + journal.Totals.Deaths + " deaths, " + journal.Totals.PopulationDeparted + " people departing alive.\n" +
                "Selected band's current close forecast: " + forecast.Births + " new members and " + (forecast.HungerLosses + forecast.ExposureLosses + forecast.SaltLosses) +
                " upkeep losses, before other units act. Click for Demographics and recorded causes.";
            DrawEconomySummaryCard(g, new RectangleF(42, 566, 492, 168), "people", "People", peopleStatus,
                population.ToString("N0") + (game.TribesEnabled ? " people in your tribe" : " people in your household"), Art.Ink,
                peopleTip, delegate { OpenEconomy(1); });

            int peaceful = known.Length - hostile;
            string cooperationStatus = gatherings > 0 ? gatherings + (gatherings == 1 ? " active gathering" : " active gatherings") :
                peaceful > 0 ? peaceful + (peaceful == 1 ? " peaceful contact" : " peaceful contacts") : "No peaceful contacts";
            string cooperationTip = "Cooperation\n" + gatherings + " active invitations, meetings or return plans. " + known.Length + " other living households are currently observed in explored places.\n" +
                "Recorded first meetings: " + journal.Totals.GatheringsMet + "; return promises kept: " + journal.Totals.ReturnsFulfilled + ".\n" +
                "Gifts recorded: " + journal.Totals.GatheringFoodGiven.ToString("0.0") + " food and " + journal.Totals.GatheringSaltGiven.ToString("0.0") + " salt. There is no automatic trade income. Click for Trade and gathering accounts.";
            DrawEconomySummaryCard(g, new RectangleF(554, 566, 492, 168), "cooperate", "Cooperation", cooperationStatus,
                "Gatherings and shared supplies", Art.Ink, cooperationTip, delegate { OpenEconomy(3); });

            string conflictTip = "Conflict\n" + hostile + " hostile households are observed in explored places; " + nearbyHostile + " are on or beside " + own.Name + "'s hex. Unseen groups are not counted.\n" +
                "Recorded encounter deaths: " + EconomyEncounterDeaths() + ". This is total deaths minus hunger, exposure and salt deaths; it also includes losses while hunting or befriending animals.\n" +
                "Click for Demographics and recorded loss causes. Diplomacy shows known relations.";
            DrawEconomySummaryCard(g, new RectangleF(1066, 566, 492, 168), "conflict", "Conflict",
                hostile > 0 ? hostile + (hostile == 1 ? " hostile band" : " hostile bands") : "None observed",
                "Known threats and encounter losses", hostile > 0 ? SaltWarning : Art.Ink,
                conflictTip, delegate { OpenEconomy(1); });

            Typography.Line(g, "Hover for an explanation. Select a card for details.", new RectangleF(44, 769, 1014, 28), 17, Art.Muted, TypeRole.Annotation, true);
            Button(g, "Food accounts", 1334, 762, 224, 36, delegate { OpenEconomy(4); }, false, false);
            MapTip("Finance\nOpen the complete record of food received, spent, shared and lost, plus the selected band's next-turn forecast.");
        }

        private void DrawEconomySummaryCard(Graphics g, RectangleF bounds, string icon, string title, string value,
            string caption, Color ink, string tip, Action open)
        {
            bool hover = bounds.Contains(hoverPoint);
            Art.Panel(g, bounds, hover ? Color.FromArgb(32, 44, 47) : Panel, false);
            if (hover)
                using (Pen edge = new Pen(Color.FromArgb(150, Art.Gold), 1)) g.DrawRectangle(edge, bounds.X, bounds.Y, bounds.Width, bounds.Height);
            RectangleF glyph = new RectangleF(bounds.X + 23, bounds.Y + 21, 31, 31);
            if (icon == "salt") DrawSaltGlyph(g, glyph, Art.Gold);
            else if (icon == "wood") DrawWoodGlyph(g, glyph, Art.Gold);
            else Art.Icon(g, icon, glyph.X, glyph.Y, glyph.Width, Art.Gold);
            Typography.Line(g, title, new RectangleF(bounds.X + 72, bounds.Y + 18, bounds.Width - 150, 39), 25, Art.Gold, TypeRole.Heading, true);
            Typography.Line(g, "\u203a", new RectangleF(bounds.Right - 46, bounds.Y + 19, 22, 34), 27, hover ? Art.Gold : Art.Muted, TypeRole.Heading, true, StringAlignment.Far);
            Typography.Line(g, value, new RectangleF(bounds.X + 24, bounds.Y + 69, bounds.Width - 48, 43), 31, ink, TypeRole.Heading, true);
            Typography.Line(g, caption, new RectangleF(bounds.X + 26, bounds.Y + 124, bounds.Width - 52, 26), 17, Art.Muted, TypeRole.Annotation, true);
            buttons.Add(new UiButton(bounds, delegate { HideMapHover(); buttons.Clear(); open(); }) { Tip = tip });
        }
    }
}
