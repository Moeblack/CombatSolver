using MegaCrit.Sts2.Core.Combat;

namespace CombatSolver;

internal static class CombatSearchCoordinator
{
    public static SolverResult Solve(
        CombatRootSnapshot root,
        SolverDisplayNames displayNames,
        BattleDamageSnapshot battleDamage,
        SearchPolicySnapshot policy,
        CancellationToken cancellationToken,
        Action<SolverProgress>? progressCallback)
    {
        SolverSearchProfile shortProfile = policy.ShortProfile;
        if (policy.ShortBudgetOverrideMilliseconds is { } shortBudget)
            shortProfile = shortProfile with { SoftTimeBudgetMilliseconds = shortBudget };
        if (policy.ForceShortOnly)
        {
            SolverResult shortResult = new CombatBeamSolver(
                root,
                displayNames,
                battleDamage,
                policy,
                cancellationToken,
                progressCallback,
                shortProfile).Solve();
            PopulateSingleSessionTotals(shortResult, shortProfile.SoftTimeBudgetMilliseconds, deepTriggered: false);
            shortResult = AuditRequiredPotionUse(
                root,
                displayNames,
                battleDamage,
                policy,
                cancellationToken,
                progressCallback,
                shortProfile,
                shortCheckpointMilliseconds: null,
                primary: shortResult);
            shortResult = AuditSmartPotionUse(
                root,
                displayNames,
                battleDamage,
                policy,
                cancellationToken,
                progressCallback,
                shortProfile,
                shortCheckpointMilliseconds: null,
                primary: shortResult);
            if (policy.MeasurePhasePerformance)
                policy.Diagnostics.Info(SolverDiagnostics.DescribeSearchPhasePerformance(shortResult));
            return shortResult;
        }

        // 普通搜索只建立一次根状态。深化宽度从一开始就是候选超集；短预算仅作为
        // UI/统计检查点。搜索空间在检查点前耗尽时会自然提前返回，否则原地继续，
        // 不再从根重复分叉、回放并保留两套模拟图。
        SolverSearchProfile deepProfile = policy.DeepProfile;
        if (policy.DeepBudgetOverrideMilliseconds is { } deepBudget)
            deepProfile = deepProfile with { SoftTimeBudgetMilliseconds = deepBudget };
        if (root.IsActEndingBoss && deepProfile.BeamWidth < 45)
        {
            policy.Diagnostics.Info(
                $"[CombatSolver/Test] ACT_ENDING_BOSS_SEARCH_OVERRIDE " +
                $"beam={deepProfile.BeamWidth}->45 reason=preserve_survival_routes");
            deepProfile = deepProfile with { BeamWidth = 45 };
        }
        SolverResult result = new CombatBeamSolver(
            root,
            displayNames,
            battleDamage,
            policy,
            cancellationToken,
            progressCallback,
            deepProfile,
            shortCheckpointMilliseconds: shortProfile.SoftTimeBudgetMilliseconds).Solve();
        if (policy.MeasurePhasePerformance)
            policy.Diagnostics.Info(SolverDiagnostics.DescribeSearchPhasePerformance(result));
        bool deepTriggered = result.Elapsed.TotalMilliseconds > shortProfile.SoftTimeBudgetMilliseconds;
        result.SearchPhase = deepTriggered ? SolverSearchPhase.Deep : SolverSearchPhase.Short;
        result.DeepSearchTriggered = deepTriggered;
        result.DeepSearchImprovedResult = false;
        result.SingleSessionSearch = true;
        PopulateSingleSessionTotals(result, shortProfile.SoftTimeBudgetMilliseconds, deepTriggered);
        result = AuditRequiredPotionUse(
            root,
            displayNames,
            battleDamage,
            policy,
            cancellationToken,
            progressCallback,
            deepProfile,
            shortProfile.SoftTimeBudgetMilliseconds,
            result);
        result = AuditSmartPotionUse(
            root,
            displayNames,
            battleDamage,
            policy,
            cancellationToken,
            progressCallback,
            deepProfile,
            shortProfile.SoftTimeBudgetMilliseconds,
            result);
        policy.Diagnostics.Info(
            $"[CombatSolver/Test] SEARCH_SESSION mode=single_anytime " +
            $"short_checkpoint_ms={shortProfile.SoftTimeBudgetMilliseconds} " +
            $"total_budget_ms={deepProfile.SoftTimeBudgetMilliseconds}");
        return result;
    }

    private static SolverResult AuditRequiredPotionUse(
        CombatRootSnapshot root,
        SolverDisplayNames displayNames,
        BattleDamageSnapshot battleDamage,
        SearchPolicySnapshot policy,
        CancellationToken cancellationToken,
        Action<SolverProgress>? progressCallback,
        SolverSearchProfile profile,
        int? shortCheckpointMilliseconds,
        SolverResult primary)
    {
        if (policy.PotionPolicy != SolverPotionPolicy.RequireAtLeastOne
            || battleDamage.PotionsUsedSoFar > 0
            || primary.PotionCount <= 1)
        {
            return primary;
        }

        policy.Diagnostics.Info(
            $"[CombatSolver/Test] REQUIRED_POTION_AUDIT start potion_count={primary.PotionCount} " +
            $"reported_saved={primary.PotionHpSaved} required={primary.PotionHpRequired}");
        SolverResult potionFree = new CombatBeamSolver(
            root,
            displayNames,
            battleDamage,
            policy,
            cancellationToken,
            progressCallback,
            profile,
            shortCheckpointMilliseconds,
            SolverPotionPolicy.Disabled).Solve();
        bool auditDeepTriggered = shortCheckpointMilliseconds is { } checkpoint
            && potionFree.Elapsed.TotalMilliseconds > checkpoint;
        potionFree.SearchPhase = auditDeepTriggered ? SolverSearchPhase.Deep : SolverSearchPhase.Short;
        potionFree.DeepSearchTriggered = auditDeepTriggered;
        potionFree.DeepSearchImprovedResult = false;
        potionFree.SingleSessionSearch = true;
        PopulateSingleSessionTotals(
            potionFree,
            shortCheckpointMilliseconds ?? profile.SoftTimeBudgetMilliseconds,
            auditDeepTriggered);

        bool potionFreeWon = potionFree.Snapshot.AllEnemiesDead
            && !potionFree.Snapshot.PlayerDead
            && potionFree.Snapshot.ProjectedPlayerHp > 0;
        if (!potionFreeWon)
        {
            MergeAuditTotals(primary, primary, potionFree);
            policy.Diagnostics.Info(
                "[CombatSolver/Test] REQUIRED_POTION_AUDIT result potion_free_won=False " +
                "selected=multi_potion_rescue");
            return primary;
        }

        PotionFreePolicyBaseline baseline = new(
            Won: true,
            HpDeficit: potionFree.Snapshot.CumulativePlayerHpLost
                + Math.Max(0, root.InitialPlayerMaxHp - potionFree.Snapshot.PlayerMaxHp),
            PlayerHp: potionFree.Snapshot.PlayerHp);
        SolverResult audited = new CombatBeamSolver(
            root,
            displayNames,
            battleDamage,
            policy,
            cancellationToken,
            progressCallback,
            profile,
            shortCheckpointMilliseconds,
            SolverPotionPolicy.RequireAtLeastOne,
            baseline,
            maximumPotionUses: 1).Solve();
        bool auditedDeepTriggered = shortCheckpointMilliseconds is { } auditedCheckpoint
            && audited.Elapsed.TotalMilliseconds > auditedCheckpoint;
        audited.SearchPhase = auditedDeepTriggered ? SolverSearchPhase.Deep : SolverSearchPhase.Short;
        audited.DeepSearchTriggered = auditedDeepTriggered;
        audited.DeepSearchImprovedResult = false;
        audited.SingleSessionSearch = true;
        PopulateSingleSessionTotals(
            audited,
            shortCheckpointMilliseconds ?? profile.SoftTimeBudgetMilliseconds,
            auditedDeepTriggered);
        MergeAuditTotals(audited, primary, potionFree, audited);
        policy.Diagnostics.Info(
            $"[CombatSolver/Test] REQUIRED_POTION_AUDIT result potion_free_won=True " +
            $"baseline_hp_deficit={baseline.HpDeficit} selected_potion_count={audited.PotionCount} " +
            $"selected_saved={audited.PotionHpSaved} selected_required={audited.PotionHpRequired}");
        return audited;
    }

    private static SolverResult AuditSmartPotionUse(
        CombatRootSnapshot root,
        SolverDisplayNames displayNames,
        BattleDamageSnapshot battleDamage,
        SearchPolicySnapshot policy,
        CancellationToken cancellationToken,
        Action<SolverProgress>? progressCallback,
        SolverSearchProfile profile,
        int? shortCheckpointMilliseconds,
        SolverResult primary)
    {
        if (policy.PotionPolicy != SolverPotionPolicy.Smart
            || primary.PotionCount == 0)
        {
            return primary;
        }

        policy.Diagnostics.Info(
            $"[CombatSolver/Test] SMART_POTION_AUDIT start potion_count={primary.PotionCount} " +
            $"reported_saved={primary.PotionHpSaved} required={primary.PotionHpRequired}");
        SolverResult potionFree = new CombatBeamSolver(
            root,
            displayNames,
            battleDamage,
            policy,
            cancellationToken,
            progressCallback,
            profile,
            shortCheckpointMilliseconds,
            SolverPotionPolicy.Disabled).Solve();
        bool auditDeepTriggered = shortCheckpointMilliseconds is { } checkpoint
            && potionFree.Elapsed.TotalMilliseconds > checkpoint;
        potionFree.SearchPhase = auditDeepTriggered ? SolverSearchPhase.Deep : SolverSearchPhase.Short;
        potionFree.DeepSearchTriggered = auditDeepTriggered;
        potionFree.DeepSearchImprovedResult = false;
        potionFree.SingleSessionSearch = true;
        PopulateSingleSessionTotals(
            potionFree,
            shortCheckpointMilliseconds ?? profile.SoftTimeBudgetMilliseconds,
            auditDeepTriggered);

        bool potionFreeWon = potionFree.Snapshot.AllEnemiesDead
            && !potionFree.Snapshot.PlayerDead
            && potionFree.Snapshot.ProjectedPlayerHp > 0;
        int correctedSaved = primary.BestNode.Actions.Any(action =>
                action.Kind == PlanActionKind.UsePotion
                && string.Equals(action.PotionId, "AMBERGRIS", StringComparison.Ordinal))
            ? Math.Max(0, primary.Snapshot.PlayerHp - potionFree.Snapshot.PlayerHp)
            : Math.Max(0, potionFree.ProjectedBattleHpLost - primary.ProjectedBattleHpLost);
        SolverResult selected;
        bool potionProtectsMoreLoot = policy.TheftPolicy == SolverTheftPolicy.PreserveResources
            && primary.OutstandingStolenResource < potionFree.OutstandingStolenResource;
        if (!potionProtectsMoreLoot && potionFreeWon && correctedSaved < primary.PotionHpRequired)
        {
            selected = potionFree;
        }
        else
        {
            primary.PotionHpSaved = potionFreeWon ? correctedSaved : primary.PotionHpSaved;
            selected = primary;
        }
        MergeAuditTotals(selected, primary, potionFree);
        policy.Diagnostics.Info(
            $"[CombatSolver/Test] SMART_POTION_AUDIT result potion_free_won={potionFreeWon} " +
            $"corrected_saved={correctedSaved} required={primary.PotionHpRequired} " +
            $"potion_protects_more_loot={potionProtectsMoreLoot} " +
            $"selected={(ReferenceEquals(selected, potionFree) ? "potion_free" : "potion_route")}");
        return selected;
    }

    private static void MergeAuditTotals(
        SolverResult selected,
        params SolverResult[] searches)
    {
        TimeSpan shortElapsed = searches.Aggregate(TimeSpan.Zero, (sum, result) => sum + result.ShortSearchElapsed);
        TimeSpan deepElapsed = searches.Aggregate(TimeSpan.Zero, (sum, result) => sum + result.DeepSearchElapsed);
        TimeSpan totalElapsed = searches.Aggregate(TimeSpan.Zero, (sum, result) => sum + result.TotalSearchElapsed);
        long allocated = searches.Sum(result => result.TotalWorkerAllocatedBytes);
        int shortExpanded = searches.Sum(result => result.ShortExpandedNodes);
        int deepExpanded = searches.Sum(result => result.DeepExpandedNodes);
        int shortTransitions = searches.Sum(result => result.ShortTransitionCount);
        int deepTransitions = searches.Sum(result => result.DeepTransitionCount);
        int gen0 = searches.Sum(result => result.TotalGen0Collections);
        int gen1 = searches.Sum(result => result.TotalGen1Collections);
        int gen2 = searches.Sum(result => result.TotalGen2Collections);
        TimeSpan gcPause = searches.Aggregate(TimeSpan.Zero, (sum, result) => sum + result.TotalGcPauseDuration);
        TimeSpan maxGcPause = searches.Max(result => result.TotalMaxObservedGcPause);
        bool deepTriggered = searches.Any(result => result.DeepSearchTriggered);
        selected.SingleSessionSearch = false;
        selected.ShortSearchElapsed = shortElapsed;
        selected.DeepSearchElapsed = deepElapsed;
        selected.TotalSearchElapsed = totalElapsed;
        selected.TotalWorkerAllocatedBytes = allocated;
        selected.ShortExpandedNodes = shortExpanded;
        selected.DeepExpandedNodes = deepExpanded;
        selected.ShortTransitionCount = shortTransitions;
        selected.DeepTransitionCount = deepTransitions;
        selected.TotalGen0Collections = gen0;
        selected.TotalGen1Collections = gen1;
        selected.TotalGen2Collections = gen2;
        selected.TotalGcPauseDuration = gcPause;
        selected.TotalMaxObservedGcPause = maxGcPause;
        selected.DeepSearchTriggered = deepTriggered;
        selected.SearchPhase = deepTriggered ? SolverSearchPhase.Deep : SolverSearchPhase.Short;
    }

    private static void PopulateSingleSessionTotals(
        SolverResult result,
        int shortCheckpointMilliseconds,
        bool deepTriggered)
    {
        double shortMilliseconds = deepTriggered
            ? Math.Min(result.Elapsed.TotalMilliseconds, shortCheckpointMilliseconds)
            : result.Elapsed.TotalMilliseconds;
        result.ShortSearchElapsed = TimeSpan.FromMilliseconds(shortMilliseconds);
        result.DeepSearchElapsed = result.Elapsed - result.ShortSearchElapsed;
        result.TotalSearchElapsed = result.Elapsed;
        result.TotalWorkerAllocatedBytes = result.WorkerAllocatedBytes;
        result.TotalGen0Collections = result.Gen0Collections;
        result.TotalGen1Collections = result.Gen1Collections;
        result.TotalGen2Collections = result.Gen2Collections;
        result.TotalGcPauseDuration = result.GcPauseDuration;
        result.TotalMaxObservedGcPause = result.MaxObservedGcPause;
        result.ShortExpandedNodes = deepTriggered ? 0 : result.ExpandedNodes;
        result.DeepExpandedNodes = deepTriggered ? result.ExpandedNodes : 0;
        result.ShortTransitionCount = deepTriggered ? 0 : result.TransitionCount;
        result.DeepTransitionCount = deepTriggered ? result.TransitionCount : 0;
    }
}
