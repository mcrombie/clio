using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;

namespace Clio.Simulation
{
    public enum BattlePhase { Deployment, Fighting, Finished }

    public sealed class BattleFormation
    {
        public int Id { get; internal set; }
        public int Side { get; internal set; }
        public UnitKind SourceKind { get; internal set; }
        public int SourceId { get; internal set; }
        public string Name { get; internal set; }
        public int CellId { get; internal set; }
        public int StartingCount { get; internal set; }
        public int Count { get; internal set; }
        public int Wounds { get; internal set; }
        public int HealthPerMember { get; internal set; }
        public int Actions { get; internal set; }
        public bool Defending { get; internal set; }
        public bool Alive { get { return Count > 0; } }
        public int CurrentHealth { get { return Math.Max(0, Count * HealthPerMember - Wounds); } }
        public int MaxHealth { get { return StartingCount * HealthPerMember; } }
        public double Strength { get { return Count <= 0 ? 0 : PowerPerMember * Count * Math.Sqrt(Math.Max(0, 1 - Wounds / (Count * (double)HealthPerMember))); } }
        internal double PowerPerMember;
    }

    public sealed class BattleResult
    {
        public string Outcome { get; internal set; }
        public string Summary { get; internal set; }
        public int WinnerSide { get; internal set; }
        public int PlayerLosses { get; internal set; }
        public int EnemyLosses { get; internal set; }
        public double FoodRecovered { get; internal set; }
        public int FoodRecipientSide { get; internal set; }
        public int Rounds { get; internal set; }
        public bool Retreated { get; internal set; }
        public int EncounterId { get; internal set; }
        public int CellId { get; internal set; }
        public UnitKind AttackerKind { get; internal set; }
        public int AttackerId { get; internal set; }
        public UnitKind DefenderKind { get; internal set; }
        public int DefenderId { get; internal set; }
        public ReadOnlyCollection<int> SupportEncounterIds { get; internal set; }
    }

    internal sealed class BattleParticipant
    {
        internal UnitFrame Before;
        internal int Side;
        internal double Cohesion;
    }

    public sealed class BattleState
    {
        public int Id { get; internal set; }
        public int Turn { get; internal set; }
        public int Round { get; internal set; }
        public int RoundLimit { get; internal set; }
        public int RegionId { get; internal set; }
        public int CenterCellId { get; internal set; }
        public bool PlayerAttacking { get; internal set; }
        public bool EndTurnPending { get; internal set; }
        public string PlayerName { get; internal set; }
        public string EnemyName { get; internal set; }
        public BattlePhase Phase { get; internal set; }
        public BattleResult Result { get; internal set; }
        public UnitKind AttackerKind { get; internal set; }
        public int AttackerId { get; internal set; }
        public UnitKind DefenderKind { get; internal set; }
        public int DefenderId { get; internal set; }
        public ReadOnlyCollection<int> Cells { get; internal set; }
        public ReadOnlyCollection<int> PlayerDeploymentCells { get; internal set; }
        public ReadOnlyCollection<int> EnemyDeploymentCells { get; internal set; }
        public ReadOnlyCollection<BattleFormation> Formations { get { return Pieces.AsReadOnly(); } }
        public ReadOnlyCollection<string> Log { get { return Messages.AsReadOnly(); } }
        internal readonly List<BattleFormation> Pieces = new List<BattleFormation>();
        internal readonly List<string> Messages = new List<string>();
        internal readonly List<BattleParticipant> Participants = new List<BattleParticipant>();
        internal int AttackerSide;
    }

    public sealed class BattleStrikePreview
    {
        public bool Allowed { get; internal set; }
        public string Reason { get; internal set; }
        public int Damage { get; internal set; }
        public int Retaliation { get; internal set; }
        public bool HighGround { get; internal set; }
        public bool ForestCover { get; internal set; }
        public bool Flanking { get; internal set; }
        public bool RiverCrossing { get; internal set; }
    }

    /// <summary>Deterministic tactical rules shared by manual and automatic battles.</summary>
    public static class BattleRules
    {
        public const int ActionsPerRound = 3;
        public static int MoveCost(Game game, int from, int to)
        {
            if (game == null || from < 0 || to < 0 || from >= game.World.Cells.Length || to >= game.World.Cells.Length ||
                !game.World.Cells[to].IsLand || !game.World.Cells[from].Neighbors.Contains(to)) return 0;
            Terrain ground = game.World.Cells[to].Terrain;
            if (ground == Terrain.Mountains || TravelRules.HasRiver(game, from, to)) return 3;
            return ground == Terrain.Forest || ground == Terrain.Wetland || ground == Terrain.Ice ? 2 : 1;
        }
        public static bool CanDeploy(Game game, int pieceId, int cell)
        {
            BattleState battle = game == null ? null : game.Battle;
            BattleFormation piece = battle == null ? null : battle.Pieces.Find(p => p.Id == pieceId);
            return battle != null && battle.Phase == BattlePhase.Deployment && piece != null && piece.Side == 0 && piece.Alive &&
                battle.PlayerDeploymentCells.Contains(cell) && !battle.Pieces.Any(p => p.Id != pieceId && p.Alive && p.CellId == cell);
        }
        public static bool CanMove(Game game, int pieceId, int cell)
        { return CanMoveSide(game, pieceId, cell, 0); }
        internal static bool CanMoveSide(Game game, int pieceId, int cell, int side)
        {
            BattleState battle = game == null ? null : game.Battle;
            BattleFormation piece = battle == null ? null : battle.Pieces.Find(p => p.Id == pieceId);
            if (battle == null || battle.Phase != BattlePhase.Fighting || piece == null || piece.Side != side || !piece.Alive ||
                !battle.Cells.Contains(cell) || battle.Pieces.Any(p => p.Alive && p.CellId == cell)) return false;
            int cost = MoveCost(game, piece.CellId, cell);
            return cost > 0 && cost <= piece.Actions;
        }
        public static bool CanStrike(Game game, int pieceId, int targetId)
        { return StrikePreview(game, pieceId, targetId).Allowed; }
        public static BattleStrikePreview StrikePreview(Game game, int pieceId, int targetId)
        { return PreviewSide(game, pieceId, targetId, 0); }
        internal static BattleStrikePreview PreviewSide(Game game, int pieceId, int targetId, int side)
        {
            BattleStrikePreview result = new BattleStrikePreview { Reason = "Choose an active formation and an adjacent enemy." };
            BattleState battle = game == null ? null : game.Battle;
            BattleFormation actor = battle == null ? null : battle.Pieces.Find(p => p.Id == pieceId);
            BattleFormation target = battle == null ? null : battle.Pieces.Find(p => p.Id == targetId);
            if (battle == null || battle.Phase != BattlePhase.Fighting || actor == null || target == null || !actor.Alive || !target.Alive ||
                actor.Side != side || actor.Side == target.Side) return result;
            if (actor.Actions <= 0) { result.Reason = "This formation has spent its action points. End the round."; return result; }
            if (actor.CellId != target.CellId && !game.World.Cells[actor.CellId].Neighbors.Contains(target.CellId))
            { result.Reason = "Move beside the enemy before striking."; return result; }
            Cell from = game.World.Cells[actor.CellId], to = game.World.Cells[target.CellId];
            result.HighGround = from.Elevation > to.Elevation + .015;
            result.ForestCover = to.Terrain == Terrain.Forest;
            result.RiverCrossing = TravelRules.HasRiver(game, from.Id, to.Id);
            result.Flanking = battle.Pieces.Any(p => p.Alive && p.Id != actor.Id && p.Side == actor.Side &&
                (p.CellId == target.CellId || to.Neighbors.Contains(p.CellId)));
            double multiplier = (result.HighGround ? 1.20 : 1) * (result.ForestCover ? .80 : 1) *
                (result.RiverCrossing ? .80 : 1) * (result.Flanking ? 1.20 : 1) * (target.Defending ? .65 : 1);
            result.Damage = Math.Min(target.CurrentHealth, Math.Max(1, (int)Math.Ceiling(actor.Strength * .55 * multiplier)));
            int remainingCount = target.Count - Math.Min(target.Count, (target.Wounds + result.Damage) / target.HealthPerMember);
            int remainingWounds = remainingCount == 0 ? 0 : (target.Wounds + result.Damage) % target.HealthPerMember;
            double reply = remainingCount == 0 ? 0 : target.PowerPerMember * remainingCount * Math.Sqrt(Math.Max(0, 1 - remainingWounds / (remainingCount * (double)target.HealthPerMember)));
            result.Retaliation = remainingCount == 0 ? 0 : Math.Min(actor.CurrentHealth, Math.Max(1, (int)Math.Ceiling(reply * .18 * (actor.Defending ? .65 : 1))));
            result.Allowed = true; result.Reason = "Strike costs 1 action point. Damage and surviving retaliation are shown before the order.";
            return result;
        }
    }

    public sealed partial class Game
    {
        private bool tacticalBattlesEnabled;
        private BattleState battle;
        private int battleSequence, battleApproachCell = -1;
        public bool TacticalBattlesEnabled { get { return tacticalBattlesEnabled; } }
        public BattleState Battle { get { return battle; } }
        public bool BattleLocked { get { return battle != null; } }

        public string EnableTacticalBattles()
        {
            if (TacticalBattlesEnabled) return "Regional tactical battles are already enabled.";
            if (Rules != SimulationRules.MobileUnits) InitializeEncounters(true);
            tacticalBattlesEnabled = true;
            return "Attacks and incoming fights now open a regional battlefield. Deploy, move and fight, or resolve the same battle automatically.";
        }

        private bool BattlePlayerUnit(UnitKind kind, int id)
        {
            if (kind == UnitKind.Band) return IsPlayerTribe(id);
            Beast animal = Beasts.Find(b => b.Id == id);
            return animal != null && animal.Domestic && IsPlayerTribe(animal.OwnerId);
        }

        private string BeginRegionalBattle(UnitKind attackerKind, int attackerId, UnitKind defenderKind, int defenderId)
        {
            if (battle != null) return "Finish the current battle before another engagement.";
            UnitFrame attacker = Frame(attackerKind, attackerId), defender = Frame(defenderKind, defenderId);
            bool playerAttacks = BattlePlayerUnit(attackerKind, attackerId);
            BattleState next = new BattleState { Id = ++battleSequence, Turn = Turn, Round = 0, RoundLimit = 12,
                RegionId = World.Cells[defender.Cell].RegionId, CenterCellId = defender.Cell, PlayerAttacking = playerAttacks,
                EndTurnPending = battleTurnPending, PlayerName = playerAttacks ? attacker.Name : defender.Name,
                EnemyName = playerAttacks ? defender.Name : attacker.Name, Phase = BattlePhase.Deployment,
                AttackerKind = attackerKind, AttackerId = attackerId, DefenderKind = defenderKind, DefenderId = defenderId,
                AttackerSide = playerAttacks ? 0 : 1 };
            int approach = battleApproachCell >= 0 ? battleApproachCell : attacker.Cell; battleApproachCell = -1;
            List<int> cells = RegionalBattleCells(defender.Cell, approach);
            next.Cells = cells.AsReadOnly();
            Vec3 center = World.Cells[defender.Cell].Center;
            int directionCell = approach == defender.Cell ? cells.Where(id => id != defender.Cell).DefaultIfEmpty(defender.Cell).First() : approach;
            Vec3 direction = World.Cells[directionCell].Center - center;
            if (!playerAttacks) direction = -direction;
            int zoneSize = Math.Max(1, cells.Count / 3);
            int[] ordered = cells.OrderByDescending(id => Vec3.Dot(World.Cells[id].Center - center, direction)).ThenBy(id => id).ToArray();
            next.PlayerDeploymentCells = ordered.Take(zoneSize).ToList().AsReadOnly();
            next.EnemyDeploymentCells = ordered.Reverse().Take(zoneSize).ToList().AsReadOnly();
            AddBattleParticipant(next, attacker, next.AttackerSide);
            AddBattleParticipant(next, defender, 1 - next.AttackerSide);
            AddBattleSupport(next, attacker, next.AttackerSide, cells, zoneSize);
            AddBattleSupport(next, defender, 1 - next.AttackerSide, cells, zoneSize);
            foreach (int side in new[] { 0, 1 })
            {
                int[] deployment = (side == 0 ? next.PlayerDeploymentCells : next.EnemyDeploymentCells).ToArray();
                BattleParticipant[] members = next.Participants.Where(p => p.Side == side).ToArray(); int used = 0;
                for (int index = 0; index < members.Length; index++)
                {
                    BattleParticipant member = members[index]; UnitFrame source = member.Before;
                    int groups = Math.Min(3, Math.Max(1, (source.Count + 19) / 20));
                    groups = Math.Min(groups, Math.Max(1, deployment.Length - used - (members.Length - index - 1)));
                    UnitProfile profile = source.Kind == UnitKind.Band ? EncounterRules.Band(this, Bands.Find(b => b.Id == source.Id)) : EncounterRules.Animal(this, Beasts.Find(b => b.Id == source.Id));
                    double strength = profile.Strength;
                    if (source.Kind == UnitKind.Band && attackerKind == UnitKind.Band && defenderKind == UnitKind.Animal && side == next.AttackerSide)
                        strength = EncounterRules.HuntingStrength(this, Bands.Find(b => b.Id == source.Id));
                    double vigor = Math.Sqrt(Math.Max(0, 1 - source.Wounds / (Math.Max(1, source.Count) * (double)source.Health)));
                    double power = vigor <= 0 ? 0 : strength / Math.Max(1, source.Count) / vigor;
                    int assignedWounds = 0;
                    for (int group = 0; group < groups; group++)
                    {
                        int count = source.Count / groups + (group < source.Count % groups ? 1 : 0);
                        int wounds = group == groups - 1 ? source.Wounds - assignedWounds : (int)Math.Floor(source.Wounds * count / (double)Math.Max(1, source.Count));
                        next.Pieces.Add(new BattleFormation { Id = next.Pieces.Count, Side = side, SourceKind = source.Kind, SourceId = source.Id,
                            Name = source.Name + (groups > 1 ? " / " + (group + 1) : ""), CellId = deployment[Math.Min(used++, deployment.Length - 1)],
                            StartingCount = count, Count = count, Wounds = wounds, HealthPerMember = source.Health, PowerPerMember = power, Actions = 0 });
                        assignedWounds += wounds;
                    }
                }
            }
            if (attackerKind == UnitKind.Band && defenderKind == UnitKind.Band) Encounters.Wars.Add(EncounterState.Pair(attackerId, defenderId));
            Encounters.Write(attackerKind, attackerId).LastAttackTurn = Turn;
            Beast targetedAnimal = defenderKind == UnitKind.Animal ? Beasts.Find(b => b.Id == defenderId) : attackerKind == UnitKind.Animal ? Beasts.Find(b => b.Id == attackerId) : null;
            if (targetedAnimal != null)
            {
                Encounters.Write(UnitKind.Animal, targetedAnimal.Id).HostileUntil = Turn + 5;
                Encounters.Write(UnitKind.Animal, targetedAnimal.Id).LastAttackTurn = Turn;
                if (attackerKind == UnitKind.Band) targetedAnimal.PositiveContacts = Math.Max(0, targetedAnimal.PositiveContacts - 2);
                if (targetedAnimal.Domestic && attackerKind == UnitKind.Band && targetedAnimal.OwnerId != attackerId)
                    Encounters.Wars.Add(EncounterState.Pair(attackerId, targetedAnimal.OwnerId));
            }
            next.Messages.Add("Deploy on highlighted ground. Each formation has 3 tactical action points per round; these parties remain part of their original world units.");
            if (next.Participants.Count > 2) next.Messages.Add("Nearby bands of the same people have joined as support.");
            battle = next;
            return "A regional battle begins between " + next.PlayerName + " and " + next.EnemyName + ". Deploy your formations or choose automatic resolution.";
        }

        private List<int> RegionalBattleCells(int center, int approach)
        {
            HashSet<int> regions = new HashSet<int> { World.Cells[center].RegionId, World.Cells[approach].RegionId };
            List<int> cells = new List<int>(); HashSet<int> seen = new HashSet<int>(); Queue<int> queue = new Queue<int>();
            queue.Enqueue(center); seen.Add(center);
            while (queue.Count > 0 && cells.Count < 49)
            {
                int cell = queue.Dequeue(); cells.Add(cell);
                foreach (int neighbor in World.Cells[cell].Neighbors.OrderBy(id => id))
                    if (World.Cells[neighbor].IsLand && regions.Contains(World.Cells[neighbor].RegionId) && seen.Add(neighbor)) queue.Enqueue(neighbor);
            }
            // A very small coastal region needs enough contiguous land for two
            // deployment zones. Extend only across its immediate land boundary.
            if (cells.Count < 12)
            {
                queue = new Queue<int>(cells); seen = new HashSet<int>(cells);
                while (queue.Count > 0 && cells.Count < 12)
                {
                    int cell = queue.Dequeue();
                    foreach (int neighbor in World.Cells[cell].Neighbors.OrderBy(id => id))
                    {
                        if (!World.Cells[neighbor].IsLand || !seen.Add(neighbor)) continue;
                        cells.Add(neighbor); queue.Enqueue(neighbor); if (cells.Count >= 12) break;
                    }
                }
            }
            return cells;
        }

        private void AddBattleParticipant(BattleState state, UnitFrame source, int side)
        {
            Band band = source.Kind == UnitKind.Band ? Bands.Find(b => b.Id == source.Id) : null;
            state.Participants.Add(new BattleParticipant { Before = source, Side = side, Cohesion = band == null ? 1 : band.Cohesion });
        }
        private void AddBattleSupport(BattleState state, UnitFrame primary, int side, List<int> cells, int zoneSize)
        {
            int owner = primary.Kind == UnitKind.Band ? primary.Id : Beasts.Find(b => b.Id == primary.Id).OwnerId;
            if (owner < 0) return;
            int tribe = TribeOf(owner);
            foreach (Band support in Bands.Where(b => b.Population > 0 && TribeOf(b.Id) == tribe && cells.Contains(b.CellId) &&
                (b.CellId == primary.Cell || World.Cells[primary.Cell].Neighbors.Contains(b.CellId))).OrderBy(b => b.Id))
            {
                if (state.Participants.Count(p => p.Side == side) >= Math.Min(3, zoneSize)) break;
                if (state.Participants.Any(p => p.Before.Kind == UnitKind.Band && p.Before.Id == support.Id)) continue;
                if (side == 0 && !IsPlayerTribe(support.Id)) continue;
                AddBattleParticipant(state, Frame(UnitKind.Band, support.Id), side);
            }
        }

        public string ExecuteBattleCommand(string command)
        {
            if (battle == null) return "No regional battle is active.";
            string[] parts = (command ?? "").Split(':'); int one, two;
            if (parts[0] == "battle-close" && parts.Length == 1) return BattleClose();
            if (parts[0] == "battle-auto" && parts.Length == 1) return BattleAuto();
            if (parts[0] == "battle-start" && parts.Length == 1) return BattleStart();
            if ((parts[0] == "battle-round" || parts[0] == "battle-end-round") && parts.Length == 1) return BattleRound();
            if (parts[0] == "battle-retreat" && parts.Length == 1) return BattleRetreat();
            if (parts.Length == 2 && Int32.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out one) && parts[0] == "battle-defend") return BattleDefend(one);
            if (parts.Length != 3 || !Int32.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out one) ||
                !Int32.TryParse(parts[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out two)) return "Choose a valid battle order.";
            if (parts[0] == "battle-deploy") return BattleDeploy(one, two);
            if (parts[0] == "battle-move") return BattleMove(one, two);
            if (parts[0] == "battle-strike") return BattleStrike(one, two);
            return "Choose a valid battle order.";
        }
        public string BattleDeploy(int pieceId, int cell)
        {
            if (!BattleRules.CanDeploy(this, pieceId, cell)) return "Choose unoccupied ground in your deployment zone.";
            battle.Pieces.Find(p => p.Id == pieceId).CellId = cell;
            return "Formation deployed. Start the battle when ready.";
        }
        public string BattleStart()
        {
            if (battle == null || battle.Phase != BattlePhase.Deployment) return "Deployment has already ended.";
            battle.Phase = BattlePhase.Fighting; battle.Round = 1;
            ResetBattleSide(0); ResetBattleSide(1); battle.Messages.Add("Round 1. Your formations act, then the opposing formations respond.");
            return "Battle begins. Move, strike an adjacent enemy, or defend; end the round when ready.";
        }
        public string BattleMove(int pieceId, int cell)
        {
            if (!BattleRules.CanMove(this, pieceId, cell)) return "Choose an unoccupied adjacent battlefield hex that this formation can afford to enter.";
            MoveBattlePiece(battle.Pieces.Find(p => p.Id == pieceId), cell);
            return "Formation moved.";
        }
        private void MoveBattlePiece(BattleFormation piece, int cell)
        { piece.Actions -= BattleRules.MoveCost(this, piece.CellId, cell); piece.CellId = cell; piece.Defending = false; }
        public string BattleStrike(int pieceId, int targetId)
        {
            BattleStrikePreview preview = BattleRules.StrikePreview(this, pieceId, targetId);
            if (!preview.Allowed) return preview.Reason;
            ApplyBattleStrike(battle.Pieces.Find(p => p.Id == pieceId), battle.Pieces.Find(p => p.Id == targetId), preview);
            return battle.Phase == BattlePhase.Finished ? battle.Result.Summary : "Strike: " + preview.Damage + " damage; " + preview.Retaliation + " retaliation.";
        }
        private void ApplyBattleStrike(BattleFormation actor, BattleFormation target, BattleStrikePreview preview)
        {
            actor.Actions--; actor.Defending = false;
            DamageBattlePiece(target, preview.Damage); DamageBattlePiece(actor, preview.Retaliation);
            battle.Messages.Add(actor.Name + " strikes " + target.Name + ": " + preview.Damage + " damage, " + preview.Retaliation + " retaliation.");
            if (battle.Messages.Count > 80) battle.Messages.RemoveAt(0);
            CheckBattleEnd();
        }
        private static void DamageBattlePiece(BattleFormation piece, int damage)
        {
            if (damage <= 0) return;
            int total = Math.Min(piece.Count * piece.HealthPerMember, piece.Wounds + Math.Max(0, damage));
            int losses = Math.Min(piece.Count, total / piece.HealthPerMember);
            piece.Count -= losses; piece.Wounds = piece.Count <= 0 ? 0 : total % piece.HealthPerMember;
            if (!piece.Alive) { piece.Actions = 0; piece.Defending = false; }
        }
        public string BattleDefend(int pieceId)
        {
            BattleFormation piece = battle == null ? null : battle.Pieces.Find(p => p.Id == pieceId);
            if (battle == null || battle.Phase != BattlePhase.Fighting || piece == null || piece.Side != 0 || !piece.Alive || piece.Actions <= 0)
                return "Choose one of your formations with action points remaining.";
            piece.Defending = true; piece.Actions = 0;
            return "Formation holds its ground. Incoming strike damage is reduced until its next round.";
        }
        private void ResetBattleSide(int side)
        { foreach (BattleFormation piece in battle.Pieces.Where(p => p.Side == side && p.Alive)) { piece.Actions = BattleRules.ActionsPerRound; piece.Defending = false; } }
        public string BattleRound()
        {
            if (battle == null || battle.Phase != BattlePhase.Fighting) return "Start a deployed battle before ending a round.";
            ResetBattleSide(1);
            RunBattleSide(1);
            if (battle.Phase == BattlePhase.Finished) return battle.Result.Summary;
            if (battle.Round >= battle.RoundLimit) { FinishBattle(-1, false); return battle.Result.Summary; }
            battle.Round++; ResetBattleSide(0);
            battle.Messages.Add("Round " + battle.Round + ". Your formations can act again.");
            return "Round " + battle.Round + ": three action points per surviving formation.";
        }
        public string BattleRetreat()
        {
            if (battle == null || battle.Phase == BattlePhase.Finished) return "This battle has already ended.";
            if (!CanRetreatBattle()) return "Your surviving units have no open retreat route on the world map. Fight or resolve the battle automatically.";
            FinishBattle(1, true); return battle.Result.Summary;
        }
        public bool CanRetreatBattle()
        {
            return battle != null && battle.Phase != BattlePhase.Finished && battle.Participants.Where(p => p.Side == 0 &&
                battle.Pieces.Any(f => f.SourceKind == p.Before.Kind && f.SourceId == p.Before.Id && f.Alive)).All(p => BattleRetreatCell(p) >= 0);
        }

        private int BattleRetreatCell(BattleParticipant participant)
        {
            UnitFrame source = Frame(participant.Before.Kind, participant.Before.Id);
            return World.Cells[source.Cell].Neighbors.Where(id => World.Cells[id].IsLand &&
                (participant.Side != 0 || Explored.Contains(id)) &&
                !battle.Participants.Any(p => p.Side != participant.Side &&
                    Frame(p.Before.Kind, p.Before.Id).Count > 0 && Frame(p.Before.Kind, p.Before.Id).Cell == id) &&
                (source.Kind != UnitKind.Band || !EncounterRules.HostileAt(this, id, source.Id)))
                .OrderByDescending(id => World.Cells[id].Forage).ThenBy(id => id).DefaultIfEmpty(-1).First();
        }
        public string BattleAuto()
        {
            if (battle == null) return "No battle is active.";
            if (battle.Phase == BattlePhase.Deployment) BattleStart();
            while (battle.Phase == BattlePhase.Fighting)
            {
                RunBattleSide(0);
                if (battle.Phase == BattlePhase.Fighting) BattleRound();
            }
            return battle.Result.Summary;
        }
        private void RunBattleSide(int side)
        {
            foreach (BattleFormation piece in battle.Pieces.Where(p => p.Side == side).OrderBy(p => p.Id).ToArray())
            {
                int guard = 0;
                while (battle.Phase == BattlePhase.Fighting && piece.Alive && piece.Actions > 0 && guard++ < 6)
                {
                    BattleFormation target = battle.Pieces.Where(p => p.Alive && p.Side != side && BattleRules.PreviewSide(this, piece.Id, p.Id, side).Allowed)
                        .OrderBy(p => p.CurrentHealth).ThenBy(p => p.Id).FirstOrDefault();
                    if (target != null) { ApplyBattleStrike(piece, target, BattleRules.PreviewSide(this, piece.Id, target.Id, side)); continue; }
                    int step = BattleApproachStep(piece);
                    if (step >= 0 && BattleRules.CanMoveSide(this, piece.Id, step, side)) { MoveBattlePiece(piece, step); continue; }
                    piece.Defending = true; piece.Actions = 0;
                }
                if (battle.Phase == BattlePhase.Finished) break;
            }
        }
        private int BattleApproachStep(BattleFormation piece)
        {
            HashSet<int> occupied = new HashSet<int>(battle.Pieces.Where(p => p.Alive && p.Id != piece.Id).Select(p => p.CellId));
            HashSet<int> goals = new HashSet<int>(battle.Pieces.Where(p => p.Alive && p.Side != piece.Side).SelectMany(p => World.Cells[p.CellId].Neighbors)
                .Where(id => battle.Cells.Contains(id) && !occupied.Contains(id)));
            Dictionary<int, int> costs = new Dictionary<int, int> { { piece.CellId, 0 } }, first = new Dictionary<int, int> { { piece.CellId, -1 } };
            HashSet<int> open = new HashSet<int> { piece.CellId }, closed = new HashSet<int>();
            while (open.Count > 0)
            {
                int cell = open.OrderBy(id => costs[id]).ThenBy(id => id).First(); open.Remove(cell); closed.Add(cell);
                if (cell != piece.CellId && goals.Contains(cell)) return first[cell];
                foreach (int next in World.Cells[cell].Neighbors.OrderBy(id => id))
                {
                    if (!battle.Cells.Contains(next) || occupied.Contains(next) || closed.Contains(next)) continue;
                    int cost = BattleRules.MoveCost(this, cell, next); if (cost <= 0) continue;
                    int value = costs[cell] + cost, old;
                    if (!costs.TryGetValue(next, out old) || value < old)
                    { costs[next] = value; first[next] = cell == piece.CellId ? next : first[cell]; open.Add(next); }
                }
            }
            return -1;
        }
        private void CheckBattleEnd()
        {
            if (!battle.Pieces.Any(p => p.Side == 0 && p.Alive)) FinishBattle(1, false);
            else if (!battle.Pieces.Any(p => p.Side == 1 && p.Alive)) FinishBattle(0, false);
        }

        private void FinishBattle(int winner, bool retreat)
        {
            if (battle == null || battle.Phase == BattlePhase.Finished) return;
            BattleState state = battle;
            foreach (BattleParticipant participant in state.Participants)
            {
                BattleFormation[] pieces = state.Pieces.Where(p => p.SourceKind == participant.Before.Kind && p.SourceId == participant.Before.Id).ToArray();
                int count = pieces.Sum(p => p.Count), wounds = pieces.Sum(p => p.Wounds);
                if (participant.Before.Kind == UnitKind.Band)
                {
                    Band band = Bands.Find(b => b.Id == participant.Before.Id); band.Population = count;
                    band.Cohesion = Math.Max(.1, participant.Cohesion - .025 - (participant.Before.Count - count) * .008 - (participant.Side != winner && winner >= 0 ? .04 : 0));
                }
                else Beasts.Find(b => b.Id == participant.Before.Id).Count = count;
                Encounters.Write(participant.Before.Kind, participant.Before.Id).Wounds = count <= 0 ? 0 : Math.Min(wounds, count * participant.Before.Health - 1);
                Encounters.Write(participant.Before.Kind, participant.Before.Id).LastEncounterTurn = Turn;
            }
            double foodBefore = Player.Food, recovered = 0;
            BattleParticipant attacker = state.Participants.First(p => p.Before.Kind == state.AttackerKind && p.Before.Id == state.AttackerId);
            BattleParticipant defender = state.Participants.First(p => p.Before.Kind == state.DefenderKind && p.Before.Id == state.DefenderId);
            Band huntingBand = state.AttackerKind == UnitKind.Band ? Bands.Find(b => b.Id == state.AttackerId) : null;
            if (huntingBand != null && huntingBand.Population > 0 && state.DefenderKind == UnitKind.Animal && winner != 1 - state.AttackerSide)
            {
                Beast animal = Beasts.Find(b => b.Id == state.DefenderId);
                int kills = defender.Before.Count - animal.Count;
                recovered = kills * (animal.Kind == BeastKind.Dragon ? 160 : animal.Kind == BeastKind.Mammoths ? 85 : animal.Kind == BeastKind.Aurochs ? 38 : animal.Kind == BeastKind.Wolves ? 12 : animal.Kind == BeastKind.Goats ? 8 : 28);
                huntingBand.Food += recovered;
                if (IsPlayerTribe(huntingBand.Id) && recovered > 0) { hunts++; UpdateKnowledge(); }
            }
            if (state.AttackerKind == UnitKind.Band && state.DefenderKind == UnitKind.Band && huntingBand != null && huntingBand.Population > 0)
            {
                Band victim = Bands.Find(b => b.Id == state.DefenderId);
                if (victim.Population <= 0) { recovered = victim.Food; victim.Food = 0; huntingBand.Food += recovered; }
            }
            int ownLosses = state.Participants.Where(p => p.Side == 0).Sum(p => p.Before.Count) - state.Pieces.Where(p => p.Side == 0).Sum(p => p.Count);
            int enemyLosses = state.Participants.Where(p => p.Side == 1).Sum(p => p.Before.Count) - state.Pieces.Where(p => p.Side == 1).Sum(p => p.Count);
            string outcome = retreat ? "Retreat" : winner == 0 ? "Victory" : winner == 1 ? "Defeat" : "Disengagement";
            int foodSide = recovered > 0 && huntingBand != null ? (IsPlayerTribe(huntingBand.Id) ? 0 : 1) : -1;
            string summary = outcome + ". " + ownLosses + " members of your side and " + enemyLosses + " of the opposing side were lost. " +
                (recovered > 0 ? (foodSide == 0 ? "Your side recovered " : "The enemy recovered ") + recovered.ToString("0.#", CultureInfo.InvariantCulture) + " food. " : "") +
                (winner < 0 ? "The round limit was reached; survivors disengage and withdraw where an open route remains." : "Surviving wounds remain with the world units.");
            AddEncounter(EncounterKind.Attack, attacker.Before, defender.Before, foodBefore, recovered, 0, 0, 0, 0, 0, 0,
                outcome.ToLowerInvariant(), "Regional battle: " + outcome.ToLowerInvariant(), summary);
            int primaryRecord = Encounters.History.Last().Id; List<int> supportRecords = new List<int>();
            foreach (BattleParticipant participant in state.Participants.Where(p => p != attacker && p != defender))
            {
                AddEncounter(EncounterKind.BattleSupport, participant.Before, null, Player.Food, 0, 0, 0, 0, 0, 0, 0,
                    "support", "Supporting formation returns", participant.Before.Name + " returns from the same regional battle with its surviving members and wounds.");
                supportRecords.Add(Encounters.History.Last().Id);
            }
            state.Result = new BattleResult { Outcome = outcome, Summary = summary, WinnerSide = winner, PlayerLosses = ownLosses,
                EnemyLosses = enemyLosses, FoodRecovered = recovered, FoodRecipientSide = foodSide, Rounds = state.Round, Retreated = retreat,
                EncounterId = primaryRecord, SupportEncounterIds = supportRecords.AsReadOnly(), CellId = state.CenterCellId,
                AttackerKind = state.AttackerKind, AttackerId = state.AttackerId, DefenderKind = state.DefenderKind, DefenderId = state.DefenderId };
            state.Phase = BattlePhase.Finished; state.Messages.Add(summary);
            // The tactical party positions do not create new bands or scatter
            // world units over the region. Only losing/disengaging units retreat.
            foreach (BattleParticipant participant in state.Participants.Where(p => winner < 0 || p.Side != winner))
            {
                int destination = BattleRetreatCell(participant);
                if (destination < 0) continue;
                if (participant.Before.Kind == UnitKind.Band)
                {
                    Band band = Bands.Find(b => b.Id == participant.Before.Id);
                    if (band.Population > 0) RelocateBand(band, destination, true, EncounterKind.Retreat);
                }
                else
                {
                    Beast animal = Beasts.Find(b => b.Id == participant.Before.Id);
                    if (animal.Count > 0) RelocateAnimal(animal, destination, EncounterKind.Retreat);
                }
            }
            ReleaseOwnerlessCompanions(); ReconcileTribe(false);
            if (GatheringsEnabled) ReconcileGatherings(false);
        }
        public string BattleClose()
        {
            if (battle == null || battle.Phase != BattlePhase.Finished) return "Resolve the battle before returning to the world.";
            string summary = battle.Result.Summary; battle = null;
            return battleTurnPending ? ResumeTacticalTurn() : summary;
        }
    }
}
