using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using Clio.Simulation;

namespace Clio.Desktop
{
    public enum AdviserRole { Economic, Military, Guidance }
    public enum AdvisorySeverity { Watch, Warning, Urgent, Critical }
    public enum AdviserAction { GatherSalt, FindSalt, GatherFood, ReviewEconomy, InspectThreat, ReviewMap, ReviewUnits, ReviewDiplomacy, ReviewAnimals, ReviewCulture, ReviewGathering, ReviewWood }

    public sealed class Advisory
    {
        public readonly string Key, Title, Explanation, Summary, Counsel, SubjectName;
        public readonly AdviserRole Role;
        public readonly AdvisorySeverity Severity;
        public readonly int ActorBandId, TargetBandId, CellId;
        public readonly AdviserAction RecommendedAction;
        public readonly int Turn;
        public readonly int GuidancePriority;
        public readonly bool RequiresControl;
        public bool IsGuidance { get { return Role == AdviserRole.Guidance; } }

        internal Advisory(string key, AdviserRole role, AdvisorySeverity severity, Band actor, int target, int cell,
            string title, string explanation, AdviserAction action, string summary = null, string subject = null)
        {
            Key = key; Role = role; Severity = severity; ActorBandId = actor.Id;
            TargetBandId = target; CellId = cell; Title = title; Explanation = explanation; RecommendedAction = action;
            Summary = summary ?? explanation; Counsel = ""; Turn = -1;
            SubjectName = subject ?? actor.Name;
        }

        internal Advisory(string key, int turn, int actor, int target, int cell, string title, string summary,
            string explanation, string counsel, AdviserAction action, int priority = 0, bool requiresControl = false, string subject = "")
        {
            Key = key; Role = AdviserRole.Guidance; Severity = AdvisorySeverity.Watch; Turn = turn;
            ActorBandId = actor; TargetBandId = target; CellId = cell; Title = title; Summary = summary;
            Explanation = explanation; Counsel = counsel; RecommendedAction = action;
            GuidancePriority = priority; RequiresControl = requiresControl;
            SubjectName = subject;
        }
    }

    /// <summary>
    /// Read-only advice about current household forecasts and observed enemies.
    /// It issues no orders, discovers no places, and retains no stale alerts.
    /// </summary>
    public static class AdviserReport
    {
        public const double LowSaltTurns = 2;
        public const double LowFoodTurnsAfterClose = 1;

        public static ReadOnlyCollection<Advisory> Evaluate(Game game)
        {
            List<Advisory> result = new List<Advisory>();
            if (game == null || game.IsOver) return result.AsReadOnly();
            Band[] households = game.ControlledBands.Where(b => b.Population > 0).OrderBy(b => b.Id).ToArray();
            foreach (Band band in households)
            {
                EconomyForecast forecast = BandEconomy.Forecast(game, band);
                AddSalt(result, game, band, forecast);
                AddFood(result, game, band, forecast);
                AddWood(result, game, band, forecast);
            }
            AddMilitary(result, game, households);
            return result.OrderByDescending(a => a.Severity).ThenBy(a => a.Role).ThenBy(a => a.ActorBandId)
                .ThenBy(a => a.TargetBandId).ThenBy(a => a.Key, StringComparer.Ordinal).ToList().AsReadOnly();
        }

        private static void AddWood(List<Advisory> result, Game game, Band band, EconomyForecast forecast)
        {
            if (!game.WoodEnabled || WoodEconomy.ReserveTurns(band) >= 2) return;
            double reserve = WoodEconomy.ReserveTurns(band);
            result.Add(new Advisory("economic:wood:" + band.Id, AdviserRole.Economic, AdvisorySeverity.Watch,
                band, -1, band.CellId, band.Name + ": wood is running low",
                "This band has " + Number(band.Wood) + " wood, enough for " + Number(reserve) + " turns of fire. A full fire needs " + Number(forecast.WoodNeed) +
                " wood per turn. Without fuel, food needs return to their normal level and fires no longer prevent exposure. Food and salt remain the first priorities.",
                AdviserAction.ReviewWood, "Wood covers " + Number(reserve) + " turns of fire. Once food and salt are secure, collect wood to keep the cooking and warmth benefits."));
        }

        private static void AddSalt(List<Advisory> result, Game game, Band band, EconomyForecast forecast)
        {
            if (!game.SaltEnabled || forecast.SaltNeed <= 0) return;
            double reserve = SaltEconomy.ReserveTurns(band);
            bool deficient = forecast.EndingSaltShortageTurns > 0;
            if (!deficient && reserve > LowSaltTurns) return;
            bool canGather = SaltEconomy.CanGather(game, band);
            string explanation;
            if (deficient)
            {
                explanation = "At current stocks, the next close cannot meet the full need of " + Number(forecast.SaltNeed) +
                    " salt. It would be deficient turn " + forecast.EndingSaltShortageTurns + ": growth is blocked, cohesion falls and later gathering is reduced.";
                if (forecast.SaltLosses > 0) explanation += " The current forecast loses " + People(forecast.SaltLosses) + " to salt shortage.";
            }
            else explanation = "Salt covers " + Number(reserve) + " turns at this population. Each close needs " + Number(forecast.SaltNeed) +
                ". Replenish before less than one turn remains; salt has no passive income.";
            result.Add(new Advisory("economic:salt:" + band.Id, AdviserRole.Economic, forecast.SaltLosses > 0 ? AdvisorySeverity.Critical : deficient ? AdvisorySeverity.Urgent : AdvisorySeverity.Warning,
                band, -1, band.CellId, band.Name + (deficient ? ": salt will run short" : ": salt is running low"), explanation,
                canGather ? AdviserAction.GatherSalt : AdviserAction.FindSalt,
                deficient ? "The next turn cannot meet this household's salt needs. " + (forecast.SaltLosses > 0 ? "The current forecast includes " + People(forecast.SaltLosses) + " lost to shortage." : "Restore a full turn's supply before the shortage deepens.") :
                    "This household has " + Number(reserve) + " turns of salt left. Gather at a known coast or spring; supplies do not replenish themselves."));
        }

        private static void AddFood(List<Advisory> result, Game game, Band band, EconomyForecast forecast)
        {
            bool hunger = forecast.HungerLosses > 0;
            if (!hunger && (forecast.NetFood >= 0 || forecast.EndingFood >= forecast.Upkeep * LowFoodTurnsAfterClose)) return;
            string explanation;
            if (hunger)
            {
                explanation = "At the current location, reserves plus camp and herd output, after companion care, cannot cover the full " +
                    Number(forecast.Upkeep) + " needs. Closing the turn under these conditions loses " + People(forecast.HungerLosses) + " to hunger.";
            }
            else explanation = "After current output, care, household needs and spoilage, food reserves fall below one turn's current need of " +
                Number(forecast.Upkeep) + ". Rebuild supplies before another close.";
            bool canGather = game.ActionsFor(band.Id) > 0 && game.ForageYield(band.CellId, band) > 0;
            result.Add(new Advisory("economic:food:" + band.Id, AdviserRole.Economic, hunger ? AdvisorySeverity.Critical : AdvisorySeverity.Warning,
                band, -1, band.CellId, band.Name + (hunger ? ": hunger at the next close" : ": food reserves are narrowing"), explanation,
                canGather ? AdviserAction.GatherFood : AdviserAction.ReviewEconomy,
                hunger ? "The next close cannot cover this household's food needs. The current forecast loses " + People(forecast.HungerLosses) + " to hunger. Review supplies before ending the turn." :
                    "After the next close, food will cover less than one turn's needs. Review this household's output and gather while actions remain."));
        }

        private static void AddMilitary(List<Advisory> result, Game game, Band[] households)
        {
            if (game.Rules != SimulationRules.MobileUnits) return;
            // The visibility gate precedes all enemy labels and neighborhood reads.
            // Atlas display mode never changes this observer's information boundary.
            foreach (Band enemy in game.Bands.Where(b => b.Population > 0 && !game.CanControlBand(b.Id) && game.Explored.Contains(b.CellId)).OrderBy(b => b.Id))
            {
                Band[] nearby = households.Where(b => EncounterRules.BandsHostile(game, b.Id, enemy.Id) &&
                    (b.CellId == enemy.CellId || game.World.Cells[b.CellId].Neighbors.Contains(enemy.CellId)))
                    .OrderBy(b => b.CellId == enemy.CellId ? 0 : 1).ThenBy(b => b.Id).ToArray();
                if (nearby.Length == 0) continue;
                Band actor = nearby[0]; bool together = actor.CellId == enemy.CellId;
                string explanation = enemy.Name + " is already hostile to your tribe and " + (together ? "shares " + actor.Name + "'s hex." : "is one hex from " + actor.Name + ".");
                if (nearby.Length > 1) explanation += " " + nearby.Length + " of your bands are within one hex of this group.";
                explanation += " Inspect the encounter before advancing or ending the turn; this warning does not predict an attack.";
                result.Add(new Advisory("military:band:" + enemy.Id, AdviserRole.Military, together ? AdvisorySeverity.Urgent : AdvisorySeverity.Warning,
                    actor, enemy.Id, enemy.CellId, enemy.Name + (together ? ": hostile people on shared ground" : ": hostile people nearby"), explanation, AdviserAction.InspectThreat,
                    together ? "A known hostile people shares ground with your tribe. Check both bands before choosing to hold, withdraw or fight. An attack is possible, not certain." :
                        "A known hostile people is one hex from your tribe. Inspect the approach before advancing or ending the turn. This warning does not predict an attack.", enemy.Name));
            }
        }

        private static string Number(double value) { return value.ToString("0.#", CultureInfo.InvariantCulture); }
        private static string People(int count) { return count + (count == 1 ? " person" : " people"); }
    }
}
