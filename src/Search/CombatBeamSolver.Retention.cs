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
    private SearchNode RefreshReleasedFallback(SearchNode fallback)
    {
        if (fallback.Snapshot.HasSimulator)
            return fallback;
        SimulationSnapshot? turnSetupRoot = _includeTurnSetup
            ? ReplayTurnSetup(fallback.GetTurnSetupChoices())
            : null;
        SimulationSnapshot snapshot;
        try
        {
            snapshot = Replay(
                fallback.Actions,
                turnSetupRoot,
                _startTurnNumber,
                priorActionCount: 0);
        }
        finally
        {
            turnSetupRoot?.ReleaseSimulator();
        }
        return fallback with
        {
            Score = snapshot.Score,
            StateKey = snapshot.StateKey,
            HasPredictionRisk = snapshot.HasRisk,
            BoundaryReason = snapshot.BoundaryReason,
            IsTerminal = snapshot.PlayerDead
                || snapshot.AllEnemiesDead
                || snapshot.BoundaryReason != SearchBoundaryReason.None,
            Snapshot = snapshot,
        };
    }

    private List<SearchNode> Prune(IEnumerable<SearchNode> nodes)
    {
        SearchMeasurement measurement = _run.Performance.Begin();
        try
        {
            return Retention.RankBest(nodes, _profile.BeamWidth, preserveDefensiveRoute: true);
        }
        finally
        {
            _run.Performance.End(SearchMetricPhase.Prune, measurement);
        }
    }

    private void CaptureContinuation(SearchNode node)
    {
        if (node.Action is not { } action
            || action.Kind != PlanActionKind.EndTurn && !action.EndsPlayerTurn
            || node.Snapshot.Continuation != null
            || node.Snapshot.PlayerDead
            || node.Snapshot.AllEnemiesDead
            || node.Snapshot.BoundaryReason != SearchBoundaryReason.None)
        {
            return;
        }
        node.Snapshot.SetContinuation(ContinuationStamp.CapturePredicted(
            _player,
            node.Snapshot.Simulator,
            node.Turn,
            _forecast,
            _startTurnNumber));
    }

    private static void ValidateHistoricalSimulatorsReleased(IReadOnlyList<SearchNode> candidates)
    {
        foreach (SearchNode candidate in candidates)
        {
            for (SearchNode? parent = candidate.Parent; parent != null; parent = parent.Parent)
            {
                if (parent.Snapshot.HasSimulator)
                    throw new InvalidOperationException("历史搜索节点仍在保留完整模拟器。");
            }
        }
    }

    private static void ReleaseDroppedSnapshots(
        IReadOnlyList<SearchNode> candidates,
        IReadOnlyList<SearchNode> retained)
    {
        foreach (SearchNode candidate in candidates)
        {
            bool keepSnapshot = false;
            foreach (SearchNode survivor in retained)
            {
                if (!ReferenceEquals(candidate.Snapshot, survivor.Snapshot))
                    continue;
                keepSnapshot = true;
                break;
            }
            if (!keepSnapshot)
                candidate.Snapshot.ReleaseSimulator();
        }
    }

    private static string SummarizePotionCandidates(IEnumerable<SearchNode> nodes)
    {
        string summary = string.Join(';', nodes
            .GroupBy(node => node.PotionCount)
            .OrderBy(group => group.Key)
            .Select(group =>
                $"{group.Key}:{group.Count()}:hp{group.Max(node => node.Snapshot.ProjectedPlayerHp)}:" +
                $"enemy{group.Min(node => node.Snapshot.EnemyHp)}"));
        return string.IsNullOrEmpty(summary) ? "-" : summary;
    }

    private static string SummarizeDiagnosticRoutes(
        IEnumerable<SearchNode> nodes,
        int limit)
    {
        string summary = string.Join(';', nodes
            .Take(limit)
            .Select(node =>
                $"{string.Join('>', node.Actions.Select(PolicyActionToken))}:" +
                $"score{node.Score:F0}:hp{node.Snapshot.ProjectedPlayerHp}:" +
                $"enemy{node.Snapshot.EnemyHp}:hand{node.Snapshot.HandCount}/" +
                $"{node.Snapshot.ReachableHandValue}/{node.Snapshot.ZeroCostPlayableCount}:" +
                $"traits{node.Traits}"));
        return string.IsNullOrEmpty(summary) ? "-" : summary;
    }

    private static string SummarizePotionChoiceTargets(
        IEnumerable<SearchNode> nodes,
        string potionId)
    {
        string summary = string.Join(',', nodes
            .Select(node => node.Actions.LastOrDefault(action =>
                action.Kind == PlanActionKind.UsePotion
                && string.Equals(action.PotionId, potionId, StringComparison.Ordinal))?.Choice)
            .Where(choice => choice != null)
            .Select(choice => choice!.Cards.Count == 0
                ? "skip"
                : string.Join('+', choice.Cards.Select(card => card.CardId)))
            .GroupBy(cardIds => cardIds, StringComparer.Ordinal)
            .OrderBy(group => group.Key, StringComparer.Ordinal)
            .Select(group => $"{group.Key}:{group.Count()}"));
        return string.IsNullOrEmpty(summary) ? "-" : summary;
    }

    private static RoutingChoiceSignature? CurrentTurnRoutingChoice(SearchNode node)
        => BeamRetentionPolicy.CurrentTurnRoutingChoice(node);

    private StandPatEvaluation EvaluateStandPat(SearchNode node)
    {
        if (_run.StandPatCache.TryGetValue(node.StateKey, out StandPatEvaluation cached))
            return cached;
        SimulationSnapshot end = ReplayAction(node, new PlanAction(PlanActionKind.EndTurn, node.Turn));
        StandPatEvaluation evaluation = new(
            end.AllEnemiesDead,
            Math.Max(0, node.Snapshot.EnemyHp - end.EnemyHp),
            end.ProjectedPlayerHp,
            end.Energy * 16
                + end.Stars * 8
                + end.HandCount
                + end.ReachableHandValue
                + end.FutureResourceValue
                + end.OstyHp * 16
                + end.OstyMaxHp * 4);
        end.ReleaseSimulator();
        _run.StandPatCache.Add(node.StateKey, evaluation);
        _run.StandPatProbes++;
        return evaluation;
    }

    private static int PolicyBoundaryRank(SearchBoundaryReason reason)
        => reason switch
        {
            SearchBoundaryReason.None => 0,
            SearchBoundaryReason.NoCards or SearchBoundaryReason.Shuffle
                or SearchBoundaryReason.TurnLimit or SearchBoundaryReason.NodeLimit
                or SearchBoundaryReason.TimeLimit => 1,
            SearchBoundaryReason.PendingChoice => 2,
            SearchBoundaryReason.UnsupportedEffect => 3,
            SearchBoundaryReason.EventDefeat => 4,
            _ => throw new ArgumentOutOfRangeException(nameof(reason), reason, null),
        };

    private static string PolicyActionToken(PlanAction action)
        => action.Kind switch
        {
            PlanActionKind.PlayCard => action.Choice == null
                ? $"{action.Turn}:C:{action.CardId}"
                : $"{action.Turn}:C:{action.CardId}[{string.Join(',', action.Choice.Cards.Select(card => card.CardId))}]",
            PlanActionKind.UsePotion => action.Choice == null
                ? $"{action.Turn}:P:{action.PotionId}"
                : $"{action.Turn}:P:{action.PotionId}[{string.Join(',', action.Choice.Cards.Select(card => card.CardId))}]",
            PlanActionKind.EndTurn => action.TurnStartChoices is not { Count: > 0 }
                ? $"{action.Turn}:E"
                : $"{action.Turn}:E:" + string.Join(';', action.TurnStartChoices.Select(choice =>
                    $"{choice.SourceId}={string.Join(',', choice.Cards.Select(card => card.CardId))}")),
            _ => throw new ArgumentOutOfRangeException(nameof(action), action.Kind, null),
        };

}
