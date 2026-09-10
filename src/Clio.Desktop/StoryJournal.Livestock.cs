using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using Clio.Simulation;

namespace Clio.Desktop
{
    internal sealed partial class StoryJournal
    {
        private List<StoryNotice> RecordLivestockEnabled(Game game, JournalSnapshot before)
        {
            int first = Notices.Count;
            Totals.Commands++;
            JournalAnimal[] released = before.Animals.Where(a => a.Domestic && a.Kind == BeastKind.Deer &&
                before.Households.Any(b => b.Id == a.OwnerId) && game.Beasts.Any(b => b.Id == a.Id && b.Count > 0 && !b.Domestic)).ToArray();
            Totals.DomesticGroupsReleased += released.Length;
            Totals.DomesticAnimalsReleased += released.Sum(a => a.Count);
            string releasedText = released.Length == 0 ? "Deer remain wild and cannot be domesticated." :
                released.Sum(a => a.Count) + " formerly domestic deer have returned to the wild at their current locations.";
            AddTribalNotice(game, game.TribeLeaderBand ?? game.Player, StoryNoticeKind.Livestock, "Milk, meat and hunting dogs",
                "Cattle and goats now supply milk in proportion to herd size. Dogs help hunting, without producing food or improving gathering.",
                "Slaughtering livestock provides meat immediately but reduces future milk and animal care. " + releasedText,
                "Inspect animal counts, milk and care in Economy / Resources. Select an owned cattle or goat herd to see the cost and yield of slaughter.", false);
            List<StoryNotice> added = Notices.Skip(first).ToList();
            LastCommandNotices = new ReadOnlyCollection<StoryNotice>(added.ToArray());
            LastCommandPopulationBefore = LastCommandPopulationAfter = game.TribesEnabled ? game.TribePopulation : game.Player.Population;
            return added;
        }

        private void RecordLivestockAction(Game game, JournalSnapshot before, string command, bool ended)
        {
            if (!game.LivestockEnabled || !before.LivestockEnabled || ended) return;
            int actorId = game.Player.Id;
            string order = command;
            if (order.StartsWith("band:", StringComparison.Ordinal))
            {
                int separator = order.IndexOf(':', 5);
                if (separator <= 5 || !Int32.TryParse(order.Substring(5, separator - 5), NumberStyles.Integer, CultureInfo.InvariantCulture, out actorId)) return;
                order = order.Substring(separator + 1);
            }
            int animalId;
            if (!order.StartsWith("slaughter:", StringComparison.Ordinal) || !Int32.TryParse(order.Substring(10), NumberStyles.Integer, CultureInfo.InvariantCulture, out animalId)) return;
            Band actor = game.Bands.FirstOrDefault(b => b.Id == actorId);
            JournalHousehold priorBand = before.Households.FirstOrDefault(b => b.Id == actorId);
            JournalAnimal priorAnimal = before.Animals.FirstOrDefault(a => a.Id == animalId && a.Domestic && a.OwnerId == actorId &&
                (a.Kind == BeastKind.Aurochs || a.Kind == BeastKind.Goats));
            if (actor == null || priorBand == null || priorAnimal == null) return;
            Beast remaining = game.Beasts.FirstOrDefault(a => a.Id == animalId);
            int taken = priorAnimal.Count - (remaining == null ? 0 : remaining.Count);
            double meat = actor.Food - priorBand.Food;
            if (taken <= 0 || meat <= 0) return;
            Totals.SlaughterActions++;
            Totals.LivestockSlaughtered += taken;
            Totals.LivestockMeatProduced += meat;
            string animals = priorAnimal.Kind == BeastKind.Aurochs ? "cattle" : "goats";
            Notices.Add(new StoryNotice(nextId++, game.Turn, actor.CellId, StoryNoticeKind.Livestock, "Livestock provides meat",
                actor.Name + " slaughters " + taken + " " + animals + " for " + Number(meat) + " food.",
                (remaining == null ? 0 : remaining.Count) + " animals remain in this herd. Its milk output is now " +
                    Number(remaining == null ? 0 : LivestockEconomy.MilkFood(game, remaining)) + " food per turn.",
                "Meat helps immediately, but a smaller herd gives less milk in future turns. Slaughter costs one action and is recorded separately from hunting.",
                Totals.SlaughterActions == 1, priorAnimal.Kind, -1, actorId, -1, animalId));
        }

        private void RecordLivestockDomestication(Game game, Band owner, Beast animal)
        {
            DomesticEconomy effects = BandEconomy.DomesticEffects(game, owner);
            string name = LivestockEconomy.DisplayName(game, animal);
            string impact, advice;
            if (LivestockEconomy.IsLivestock(animal))
            {
                impact = "This herd produces " + Number(LivestockEconomy.MilkFood(game, animal)) + " milk food per turn. " +
                    "Current slaughter preview: " + LivestockEconomy.SlaughterCount(game, animal) + " animals for " + Number(LivestockEconomy.MeatFood(game, animal)) + " food.";
                advice = "Milk grows with herd size. Meat requires slaughter, which reduces future milk and care. Inspect this herd in Units or the Resources ledger.";
            }
            else if (animal.Kind == BeastKind.Wolves)
            {
                impact = "This band's dogs now provide +" + Number(effects.HuntingBonus * 100) +
                    (game.Rules == SimulationRules.MobileUnits ? "% strength when attacking animals." : " percentage points to hunting chance.") +
                    " Dogs do not produce food or improve gathering.";
                advice = "Dogs help hunting and still need food for care. Their benefits belong to the band that owns them.";
            }
            else
            {
                impact = "Inspect this band's companion effects and food care in Resources.";
                advice = "Animal benefits and care depend on species and count. Companions follow their owning band.";
            }
            impact += " All of this band's animal care: " + Number(effects.AnimalCare) + " food per turn.";
            Notices.Add(new StoryNotice(nextId++, game.Turn, animal.CellId, StoryNoticeKind.Domestication,
                animal.Kind == BeastKind.Wolves ? "Dogs join your band" : "A domestic herd joins your band",
                animal.Count + " " + name.ToLowerInvariant() + " form a domestic group with " + owner.Name + ".",
                impact, advice, true, animal.Kind, -1, owner.Id, -1, animal.Id));
        }
    }
}
