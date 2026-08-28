using MegaCrit.Sts2.Core.Combat;

namespace CombatSolver;

internal sealed partial class UnattendedTestRunner
{
    private static async Task AssertSearchPolicySnapshotAsync(CombatState combat)
    {
        SolverSettingsData originalSettings = SolverSettings.Current;
        SolverSettingsSnapshot settings = SolverSettings.Capture();
        SearchPolicySnapshot capturedPolicy = SolverController.CaptureSearchPolicy(
            settings,
            includeTurnSetup: false,
            theftPolicy: SolverController.ResolveTheftPolicy(combat)) with
        {
            ShortProfile = settings.ShortProfile with
            {
                MaxExpandedNodes = Math.Min(settings.ShortProfile.MaxExpandedNodes, 250),
                SoftTimeBudgetMilliseconds = 120_000,
            },
            ForceShortOnly = true,
            VerifyIncrementalSearch = false,
            MeasurePhasePerformance = false,
            ShortBudgetOverrideMilliseconds = null,
            DeepBudgetOverrideMilliseconds = null,
        };
        SolverDisplayNames displayNames = SolverDisplayNames.Capture(combat);
        BattleDamageSnapshot battleDamage = BattleDamageTracker.Observe(combat);
        CombatRootSnapshot rootSnapshot = CombatRootSnapshot.Capture(combat);

        SolverResult beforeMutation = await Task.Run(() => CombatSearchCoordinator.Solve(
            rootSnapshot,
            displayNames,
            battleDamage,
            capturedPolicy,
            CancellationToken.None,
            progressCallback: null));

        SolverPotionPolicy changedPotionPolicy = capturedPolicy.PotionPolicy == SolverPotionPolicy.Disabled
            ? SolverPotionPolicy.RequireAtLeastOne
            : SolverPotionPolicy.Disabled;
        try
        {
            SolverSettings.ApplyForTesting(originalSettings with
            {
                PotionPolicy = changedPotionPolicy,
                EnableDetailedDiagnosticLogs = !capturedPolicy.DetailedDiagnostics,
            });
            SolverResult afterMutation = await Task.Run(() => CombatSearchCoordinator.Solve(
                rootSnapshot,
                displayNames,
                battleDamage,
                capturedPolicy,
                CancellationToken.None,
                progressCallback: null));
            AssertEquivalentSearchResults(beforeMutation, afterMutation);
        }
        finally
        {
            SolverSettings.ApplyForTesting(originalSettings);
        }
    }

    private static void AssertEquivalentSearchResults(SolverResult expected, SolverResult actual)
    {
        bool equivalent = expected.BestNode.Actions.SequenceEqual(actual.BestNode.Actions)
            && expected.ExpandedNodes == actual.ExpandedNodes
            && expected.TransitionCount == actual.TransitionCount
            && expected.ChoiceBranchesEvaluated == actual.ChoiceBranchesEvaluated
            && expected.TranspositionBranchesPruned == actual.TranspositionBranchesPruned
            && expected.PotionCount == actual.PotionCount
            && expected.BoundaryReason == actual.BoundaryReason
            && expected.Snapshot.HasRisk == actual.Snapshot.HasRisk
            && expected.Snapshot.PlayerDead == actual.Snapshot.PlayerDead
            && expected.Snapshot.AllEnemiesDead == actual.Snapshot.AllEnemiesDead
            && expected.Snapshot.PlayerHp == actual.Snapshot.PlayerHp
            && expected.Snapshot.ProjectedPlayerHp == actual.Snapshot.ProjectedPlayerHp
            && expected.Snapshot.PlayerBlock == actual.Snapshot.PlayerBlock
            && expected.Snapshot.EnemyHp == actual.Snapshot.EnemyHp
            && expected.Snapshot.AliveEnemyCount == actual.Snapshot.AliveEnemyCount
            && expected.Snapshot.Energy == actual.Snapshot.Energy
            && expected.Snapshot.Stars == actual.Snapshot.Stars
            && expected.Snapshot.HandCount == actual.Snapshot.HandCount
            && expected.Snapshot.OutstandingStolenResource == actual.Snapshot.OutstandingStolenResource
            && expected.Snapshot.Turn == actual.Snapshot.Turn
            && expected.Snapshot.ShufflesCrossed == actual.Snapshot.ShufflesCrossed
            && expected.Snapshot.BoundaryReason == actual.Snapshot.BoundaryReason
            && expected.Snapshot.PredictionGaps.SequenceEqual(actual.Snapshot.PredictionGaps);
        if (equivalent)
            return;
        throw new InvalidOperationException(
            "相同 SearchPolicySnapshot 在全局设置改变后产生了不同结果：" +
            $"expected_actions={DescribeActions(expected)} actual_actions={DescribeActions(actual)} " +
            $"expected_expanded={expected.ExpandedNodes} actual_expanded={actual.ExpandedNodes} " +
            $"expected_transitions={expected.TransitionCount} actual_transitions={actual.TransitionCount} " +
            $"expected_boundary={expected.BoundaryReason} actual_boundary={actual.BoundaryReason}。");
    }

    private static string DescribeActions(SolverResult result)
        => string.Join(',', result.BestNode.Actions.Select(action => action.Kind switch
        {
            PlanActionKind.PlayCard => $"card:{action.CardId}:{action.CardOccurrence}:{action.TargetCombatId}",
            PlanActionKind.UsePotion => $"potion:{action.PotionId}:{action.PotionSlot}:{action.TargetCombatId}",
            PlanActionKind.EndTurn => "end",
            _ => throw new ArgumentOutOfRangeException(nameof(action.Kind), action.Kind, null),
        }));
}
