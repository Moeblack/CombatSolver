using System.Diagnostics;
using System.Runtime.CompilerServices;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Events;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Models.Relics;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.ValueProps;
using CombatSolver.Engine.Common;
using CombatSolver.Engine.InCombat.Mirrors;
using CombatSolver.Engine.InCombat.Simulation;
using BufferCard = MegaCrit.Sts2.Core.Models.Cards.Buffer;

namespace CombatSolver;


internal sealed partial class CombatBeamSolver
{
    private List<SearchNode> AnnotateTurnOutcomes(List<SearchNode> ended)
    {
        if (ended.Count == 0)
            return ended;

        List<PendingTurnOutcome> pending = [];
        foreach (SearchNode node in ended)
        {
            SearchNode parent = node.Parent
                ?? throw new InvalidOperationException("回合结果节点没有父节点。");
            PlanAction action = node.Action
                ?? throw new InvalidOperationException("非根搜索节点缺少动作。");
            SearchNode turnStart = FindTurnStart(parent);
            bool endedByTurn = action.Kind == PlanActionKind.EndTurn || node.Turn > parent.Turn;
            int actualBlock = endedByTurn ? parent.Snapshot.PlayerBlock : node.Snapshot.PlayerBlock;
            int energyLeft = endedByTurn ? parent.Snapshot.Energy : node.Snapshot.Energy;
            bool comparable = node.Snapshot.BoundaryReason is not (
                SearchBoundaryReason.UnsupportedEffect or SearchBoundaryReason.PendingChoice);
            pending.Add(new PendingTurnOutcome(
                node,
                turnStart,
                action.Turn,
                Math.Max(0, turnStart.Snapshot.PlayerHp - node.Snapshot.PlayerHp),
                actualBlock,
                energyLeft,
                CurrentTurnPotionSlotsUsed(turnStart, node),
                comparable));
        }

        int availableFutureSoldHp = Math.Max(0, SoldHpThreshold() - battleDamage.SoldHpCommitted);
        List<SearchNode> annotated = [];
        foreach (IGrouping<(int Turn, StateFingerprint State, ulong PotionSlotsUsed), PendingTurnOutcome> group in pending.GroupBy(
                     item => (item.Turn, item.TurnStart.StateKey, item.PotionSlotsUsed)))
        {
            PendingTurnOutcome[] comparable = group.Where(item => item.IsComparable).ToArray();
            int minimumHpLost = comparable.Length == 0 ? 0 : comparable.Min(item => item.HpLost);
            int maxBlock = group.Max(item => item.ActualBlock);
            foreach (PendingTurnOutcome outcome in group)
            {
                int soldThisTurn = outcome.IsComparable
                    ? Math.Max(0, outcome.HpLost - minimumHpLost)
                    : 0;
                int previousSold = outcome.Node.Parent!.FutureSoldHp;
                int futureSold = previousSold + soldThisTurn;
                if (futureSold > availableFutureSoldHp)
                {
                    _run.SoldHpBranchesPruned++;
                    continue;
                }

                double scoreWithoutSoldPenalty = outcome.Node.Score
                    - outcome.Node.FutureSoldHp * SoldHpPenalty();
                SearchNode annotatedNode = outcome.Node with
                {
                    FutureSoldHp = futureSold,
                    Score = ApplySoldHpPenalty(scoreWithoutSoldPenalty, futureSold),
                    Outcome = new TurnOutcome(
                        outcome.Turn,
                        outcome.HpLost,
                        soldThisTurn,
                        maxBlock,
                        outcome.ActualBlock,
                        outcome.EnergyLeft),
                };
                annotated.Add(annotatedNode);
            }
        }
        return annotated;
    }

    private static ulong CurrentTurnPotionSlotsUsed(SearchNode turnStart, SearchNode outcome)
    {
        ulong slots = 0;
        for (SearchNode? node = outcome; node != null && !ReferenceEquals(node, turnStart); node = node.Parent)
        {
            PlanAction action = node.Action
                ?? throw new InvalidOperationException("卖血统计动作链提前抵达根节点。");
            if (action.Kind != PlanActionKind.UsePotion)
                continue;
            if ((uint)action.PotionSlot >= 64u)
                throw new InvalidOperationException($"药水槽位超出卖血分组范围：{action.PotionSlot}。");
            slots |= 1UL << action.PotionSlot;
        }
        return slots;
    }

    private RouteAnnotations BuildRouteAnnotations(SearchNode best)
    {
        List<SearchNode> path = [];
        for (SearchNode? node = best; node?.Parent != null; node = node.Parent)
            path.Add(node);
        path.Reverse();

        Dictionary<int, int> losses = [];
        Dictionary<int, int> sold = [];
        Dictionary<int, int> maxBlock = [];
        Dictionary<int, int> actualBlock = [];
        Dictionary<int, int> energy = [];
        Dictionary<int, IReadOnlyList<string>> kills = [];
        ulong aliveMask = root.InitialAliveEnemyMask;
        int? combatEndedTurn = null;
        int? deathTurn = null;

        foreach (SearchNode node in path)
        {
            SearchNode parent = node.Parent!;
            PlanAction action = node.Action
                ?? throw new InvalidOperationException("路线标注节点缺少动作。");
            ulong newlyKilledMask = aliveMask & ~node.Snapshot.AliveEnemyMask;
            if (action.IsExecutable && newlyKilledMask != 0)
            {
                List<string> newlyKilled = [];
                for (int enemyIndex = 0; enemyIndex < root.Enemies.Count; enemyIndex++)
                {
                    if ((newlyKilledMask & (1UL << enemyIndex)) != 0)
                        newlyKilled.Add(displayNames.Creature(root.Enemies[enemyIndex]));
                }
                kills[node.ActionCount - 1] = newlyKilled;
            }
            aliveMask = node.Snapshot.AliveEnemyMask;

            if (node.Outcome is { } outcome)
            {
                losses[outcome.Turn] = outcome.HpLost;
                actualBlock[outcome.Turn] = outcome.ActualBlock;
                maxBlock[outcome.Turn] = outcome.MaxBlock;
                sold[outcome.Turn] = outcome.SoldHp;
                energy[outcome.Turn] = outcome.EnergyLeft;
            }
            if (combatEndedTurn == null
                && node.Snapshot.AllEnemiesDead
                && node.Snapshot.BoundaryReason != SearchBoundaryReason.UnsupportedEffect)
            {
                combatEndedTurn = action.Turn;
            }
            if (deathTurn == null && node.Snapshot.PlayerDead)
                deathTurn = action.Turn;
        }
        return new RouteAnnotations(losses, sold, maxBlock, actualBlock, energy, kills, combatEndedTurn, deathTurn);
    }

    private static SearchNode FindTurnStart(SearchNode node)
    {
        SearchNode current = node;
        while (current.Parent is { } parent && parent.Turn == current.Turn)
            current = parent;
        return current;
    }

    private int SoldHpThreshold()
    {
        if (_theftPolicy == SolverTheftPolicy.PreserveResources)
            return root.InitialPlayerMaxHp;
        if (_isActEndingBoss)
            return Math.Max(0, root.InitialPlayerMaxHp - 1);
        return root.EncounterRoomType switch
        {
            RoomType.Boss => SolverWeights.BossSoldHpThreshold,
            RoomType.Elite => SolverWeights.EliteSoldHpThreshold,
            _ => SolverWeights.NormalSoldHpThreshold,
        };
    }

    private double ApplySoldHpPenalty(double score, int futureSoldHp)
        => score + futureSoldHp * SoldHpPenalty();

    private static double SoldHpPenalty()
        => SolverWeights.SoldHpPenalty;

    private IReadOnlyList<CachedContinuation> BuildContinuations(SearchNode best)
    {
        List<CachedContinuation> continuations = [];
        List<SearchNode> path = [];
        for (SearchNode? node = best; node?.Parent != null; node = node.Parent)
            path.Add(node);
        path.Reverse();
        for (int pathIndex = 0; pathIndex < path.Count; pathIndex++)
        {
            SearchNode node = path[pathIndex];
            PlanAction action = node.Action
                ?? throw new InvalidOperationException("续用路径节点缺少动作。");
            if (action.Kind != PlanActionKind.EndTurn
                || node.Snapshot.PlayerDead
                || node.Snapshot.AllEnemiesDead
                || node.Snapshot.BoundaryReason != SearchBoundaryReason.None)
            {
                continue;
            }
            bool hasPlannedNextTurn = path
                .Skip(pathIndex + 1)
                .Any(later => later.Action?.Turn == node.Turn);
            if (!hasPlannedNextTurn)
                continue;
            int forecastOffset = node.Turn - _startTurnNumber;
            ContinuationStamp? expected = node.Snapshot.Continuation;
            if (expected == null)
            {
                SimulationSnapshot replayed = Replay(node.Actions);
                expected = ContinuationStamp.CapturePredicted(
                    _player,
                    replayed.Simulator,
                    node.Turn,
                    _forecast,
                    _startTurnNumber);
                replayed.ReleaseSimulator();
            }
            continuations.Add(new CachedContinuation(expected, node.Turn, forecastOffset));
        }
        return continuations;
    }

}
