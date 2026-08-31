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
            shortResult = AuditOpeningPowerUse(
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
        result = AuditOpeningPowerUse(
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

    private static SolverResult AuditOpeningPowerUse(
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
        int primaryDeficit = StrategicHpDeficit(root, primary);
        if (primaryDeficit == 0
            || policy.PotionPolicy == SolverPotionPolicy.RequireAtLeastOne
                && battleDamage.PotionsUsedSoFar == 0)
            return primary;

        IReadOnlyList<PlanAction> openingPowers = new CombatBeamSolver(
                root,
                displayNames,
                battleDamage,
                policy,
                cancellationToken,
                progressCallback,
                profile,
                shortCheckpointMilliseconds)
            .BuildOpeningPowerActions();
        IReadOnlyList<PlanAction> openingPotions = policy.PotionPolicy == SolverPotionPolicy.Disabled
            ? []
            : new CombatBeamSolver(
                    root,
                    displayNames,
                    battleDamage,
                    policy,
                    cancellationToken,
                    progressCallback,
                    profile,
                    shortCheckpointMilliseconds,
                    potionPolicyOverride: SolverPotionPolicy.RequireAtLeastOne,
                    maximumPotionUses: 1)
                .BuildOpeningPotionActions();
        List<(PlanAction Potion, PlanAction Power)> potionPowerPairs = [];
        foreach (PlanAction openingPotion in openingPotions)
        {
            IReadOnlyList<PlanAction> powers = new CombatBeamSolver(
                    root,
                    displayNames,
                    battleDamage,
                    policy,
                    cancellationToken,
                    progressCallback,
                    profile,
                    shortCheckpointMilliseconds,
                    potionPolicyOverride: SolverPotionPolicy.RequireAtLeastOne,
                    maximumPotionUses: 1)
                .BuildPowerActionsAfterPrefix([openingPotion]);
            foreach (PlanAction power in powers)
            {
                potionPowerPairs.Add((openingPotion, power));
                if (potionPowerPairs.Count == 4)
                    break;
            }
            if (potionPowerPairs.Count == 4)
                break;
        }
        if (openingPowers.Count == 0 && potionPowerPairs.Count == 0)
            return primary;

        List<SolverResult> searches = [primary];
        SolverResult selected = primary;
        foreach (PlanAction openingPower in openingPowers)
        {
            SolverResult posterior = new CombatBeamSolver(
                root,
                displayNames,
                battleDamage,
                policy,
                cancellationToken,
                progressCallback,
                profile,
                shortCheckpointMilliseconds,
                fixedPrefixActions: [openingPower]).Solve();
            bool posteriorDeepTriggered = shortCheckpointMilliseconds is { } checkpoint
                && posterior.Elapsed.TotalMilliseconds > checkpoint;
            posterior.SearchPhase = posteriorDeepTriggered
                ? SolverSearchPhase.Deep
                : SolverSearchPhase.Short;
            posterior.DeepSearchTriggered = posteriorDeepTriggered;
            posterior.DeepSearchImprovedResult = false;
            posterior.SingleSessionSearch = true;
            PopulateSingleSessionTotals(
                posterior,
                shortCheckpointMilliseconds ?? profile.SoftTimeBudgetMilliseconds,
                posteriorDeepTriggered);
            searches.Add(posterior);

            bool posteriorWon = posterior.Snapshot.AllEnemiesDead
                && !posterior.Snapshot.PlayerDead
                && posterior.Snapshot.ProjectedPlayerHp > 0;
            bool selectedWon = selected.Snapshot.AllEnemiesDead
                && !selected.Snapshot.PlayerDead
                && selected.Snapshot.ProjectedPlayerHp > 0;
            int posteriorDeficit = StrategicHpDeficit(root, posterior);
            int selectedDeficit = StrategicHpDeficit(root, selected);
            if (posteriorWon
                && (!selectedWon
                    || posteriorDeficit < selectedDeficit
                    || posteriorDeficit == selectedDeficit
                        && (posterior.PotionCount < selected.PotionCount
                            || posterior.PotionCount == selected.PotionCount
                                && posterior.BestNode.Score > selected.BestNode.Score)))
            {
                selected = posterior;
            }
            policy.Diagnostics.Info(
                $"[CombatSolver/Test] OPENING_POWER_POSTERIOR card={openingPower.CardId} " +
                $"won={posteriorWon} hp_deficit={posteriorDeficit} " +
                $"selected={ReferenceEquals(selected, posterior)}");

            PlanAction? offensiveFollowUp = new CombatBeamSolver(
                    root,
                    displayNames,
                    battleDamage,
                    policy,
                    cancellationToken,
                    progressCallback,
                    profile,
                    shortCheckpointMilliseconds)
                .BuildOpeningPowerOffensiveFollowUp(openingPower);
            if (offensiveFollowUp == null)
                continue;

            SolverResult linkedPosterior = new CombatBeamSolver(
                root,
                displayNames,
                battleDamage,
                policy,
                cancellationToken,
                progressCallback,
                profile,
                shortCheckpointMilliseconds,
                fixedPrefixActions: [openingPower, offensiveFollowUp]).Solve();
            bool linkedDeepTriggered = shortCheckpointMilliseconds is { } linkedCheckpoint
                && linkedPosterior.Elapsed.TotalMilliseconds > linkedCheckpoint;
            linkedPosterior.SearchPhase = linkedDeepTriggered
                ? SolverSearchPhase.Deep
                : SolverSearchPhase.Short;
            linkedPosterior.DeepSearchTriggered = linkedDeepTriggered;
            linkedPosterior.DeepSearchImprovedResult = false;
            linkedPosterior.SingleSessionSearch = true;
            PopulateSingleSessionTotals(
                linkedPosterior,
                shortCheckpointMilliseconds ?? profile.SoftTimeBudgetMilliseconds,
                linkedDeepTriggered);
            searches.Add(linkedPosterior);

            bool linkedWon = linkedPosterior.Snapshot.AllEnemiesDead
                && !linkedPosterior.Snapshot.PlayerDead
                && linkedPosterior.Snapshot.ProjectedPlayerHp > 0;
            selectedWon = selected.Snapshot.AllEnemiesDead
                && !selected.Snapshot.PlayerDead
                && selected.Snapshot.ProjectedPlayerHp > 0;
            int linkedDeficit = StrategicHpDeficit(root, linkedPosterior);
            selectedDeficit = StrategicHpDeficit(root, selected);
            if (linkedWon
                && (!selectedWon
                    || linkedDeficit < selectedDeficit
                    || linkedDeficit == selectedDeficit
                        && (linkedPosterior.PotionCount < selected.PotionCount
                            || linkedPosterior.PotionCount == selected.PotionCount
                                && linkedPosterior.BestNode.Score > selected.BestNode.Score)))
            {
                selected = linkedPosterior;
            }
            policy.Diagnostics.Info(
                $"[CombatSolver/Test] OPENING_POWER_LINK_POSTERIOR " +
                $"cards={openingPower.CardId}+{offensiveFollowUp.CardId} " +
                $"won={linkedWon} hp_deficit={linkedDeficit} " +
                $"selected={ReferenceEquals(selected, linkedPosterior)}");
        }

        foreach ((PlanAction openingPotion, PlanAction postPotionPower) in potionPowerPairs)
        {
            PlanAction[] jointPrefix = [openingPotion, postPotionPower];
            SolverResult jointPosterior = new CombatBeamSolver(
                root,
                displayNames,
                battleDamage,
                policy,
                cancellationToken,
                progressCallback,
                profile,
                shortCheckpointMilliseconds,
                potionPolicyOverride: SolverPotionPolicy.RequireAtLeastOne,
                maximumPotionUses: Math.Max(1, primary.PotionCount),
                fixedPrefixActions: jointPrefix).Solve();
            bool jointDeepTriggered = shortCheckpointMilliseconds is { } jointCheckpoint
                && jointPosterior.Elapsed.TotalMilliseconds > jointCheckpoint;
            jointPosterior.SearchPhase = jointDeepTriggered
                ? SolverSearchPhase.Deep
                : SolverSearchPhase.Short;
            jointPosterior.DeepSearchTriggered = jointDeepTriggered;
            jointPosterior.DeepSearchImprovedResult = false;
            jointPosterior.SingleSessionSearch = true;
            PopulateSingleSessionTotals(
                jointPosterior,
                shortCheckpointMilliseconds ?? profile.SoftTimeBudgetMilliseconds,
                jointDeepTriggered);
            searches.Add(jointPosterior);

            bool jointWon = jointPosterior.Snapshot.AllEnemiesDead
                && !jointPosterior.Snapshot.PlayerDead
                && jointPosterior.Snapshot.ProjectedPlayerHp > 0;
            bool selectedWon = selected.Snapshot.AllEnemiesDead
                && !selected.Snapshot.PlayerDead
                && selected.Snapshot.ProjectedPlayerHp > 0;
            int jointDeficit = StrategicHpDeficit(root, jointPosterior);
            int selectedDeficit = StrategicHpDeficit(root, selected);
            if (jointWon
                && (!selectedWon
                    || jointDeficit < selectedDeficit
                    || jointDeficit == selectedDeficit
                        && (jointPosterior.PotionCount < selected.PotionCount
                            || jointPosterior.PotionCount == selected.PotionCount
                                && jointPosterior.BestNode.Score > selected.BestNode.Score)))
            {
                selected = jointPosterior;
            }
            policy.Diagnostics.Info(
                $"[CombatSolver/Test] POTION_POWER_POSTERIOR " +
                $"potion={openingPotion.PotionId} power={postPotionPower.CardId} " +
                $"won={jointWon} hp_deficit={jointDeficit} " +
                $"selected={ReferenceEquals(selected, jointPosterior)}");
        }

        MergeAuditTotals(selected, searches.ToArray());
        return selected;
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
        if (policy.PotionPolicy != SolverPotionPolicy.Smart)
            return primary;
        if (primary.PotionCount == 0)
        {
            return AuditMissedSmartPotionUse(
                root,
                displayNames,
                battleDamage,
                policy,
                cancellationToken,
                progressCallback,
                profile,
                shortCheckpointMilliseconds,
                primary);
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
        PotionFreePolicyBaseline baseline = new(
            Won: potionFreeWon,
            HpDeficit: StrategicHpDeficit(root, potionFree),
            PlayerHp: potionFree.Snapshot.PlayerHp);
        List<SolverResult> searches = [primary, potionFree];
        SolverResult selected = primary;
        PlanAction[] posteriorPrefix = PrefixThroughFirstPotion(primary);
        if (posteriorPrefix.Length > 0)
        {
            SolverResult posterior = new CombatBeamSolver(
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
                maximumPotionUses: primary.PotionCount,
                fixedPrefixActions: posteriorPrefix).Solve();
            bool posteriorDeepTriggered = shortCheckpointMilliseconds is { } posteriorCheckpoint
                && posterior.Elapsed.TotalMilliseconds > posteriorCheckpoint;
            posterior.SearchPhase = posteriorDeepTriggered
                ? SolverSearchPhase.Deep
                : SolverSearchPhase.Short;
            posterior.DeepSearchTriggered = posteriorDeepTriggered;
            posterior.DeepSearchImprovedResult = false;
            posterior.SingleSessionSearch = true;
            PopulateSingleSessionTotals(
                posterior,
                shortCheckpointMilliseconds ?? profile.SoftTimeBudgetMilliseconds,
                posteriorDeepTriggered);
            searches.Add(posterior);

            bool posteriorWon = posterior.Snapshot.AllEnemiesDead
                && !posterior.Snapshot.PlayerDead
                && posterior.Snapshot.ProjectedPlayerHp > 0;
            int posteriorDeficit = StrategicHpDeficit(root, posterior);
            int primaryDeficit = StrategicHpDeficit(root, primary);
            if (posteriorWon
                && (posteriorDeficit < primaryDeficit
                    || posteriorDeficit == primaryDeficit
                        && (posterior.PotionCount < primary.PotionCount
                            || posterior.PotionCount == primary.PotionCount
                                && posterior.BestNode.Score > primary.BestNode.Score)))
            {
                selected = posterior;
            }
            policy.Diagnostics.Info(
                $"[CombatSolver/Test] SMART_POTION_POSTERIOR prefix_actions={posteriorPrefix.Length} " +
                $"won={posteriorWon} hp_deficit={posteriorDeficit} " +
                $"selected={ReferenceEquals(selected, posterior)}");
        }
        PlanAction? selectedFirstPotion = primary.BestNode.Actions
            .FirstOrDefault(action => action.Kind == PlanActionKind.UsePotion);
        PlanAction? fullRedrawAction = selectedFirstPotion == null
            ? null
            : new CombatBeamSolver(
                    root,
                    displayNames,
                    battleDamage,
                    policy,
                    cancellationToken,
                    progressCallback,
                    profile,
                    shortCheckpointMilliseconds,
                    SolverPotionPolicy.Smart,
                    baseline,
                    maximumPotionUses: primary.PotionCount)
                .BuildOpeningFullRedrawPotionAction(selectedFirstPotion);
        if (fullRedrawAction != null)
        {
            SolverResult fullRedrawPosterior = new CombatBeamSolver(
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
                maximumPotionUses: primary.PotionCount,
                fixedPrefixActions: [fullRedrawAction]).Solve();
            bool fullRedrawDeepTriggered = shortCheckpointMilliseconds is { } redrawCheckpoint
                && fullRedrawPosterior.Elapsed.TotalMilliseconds > redrawCheckpoint;
            fullRedrawPosterior.SearchPhase = fullRedrawDeepTriggered
                ? SolverSearchPhase.Deep
                : SolverSearchPhase.Short;
            fullRedrawPosterior.DeepSearchTriggered = fullRedrawDeepTriggered;
            fullRedrawPosterior.DeepSearchImprovedResult = false;
            fullRedrawPosterior.SingleSessionSearch = true;
            PopulateSingleSessionTotals(
                fullRedrawPosterior,
                shortCheckpointMilliseconds ?? profile.SoftTimeBudgetMilliseconds,
                fullRedrawDeepTriggered);
            searches.Add(fullRedrawPosterior);

            bool redrawWon = fullRedrawPosterior.Snapshot.AllEnemiesDead
                && !fullRedrawPosterior.Snapshot.PlayerDead
                && fullRedrawPosterior.Snapshot.ProjectedPlayerHp > 0;
            bool selectedWon = selected.Snapshot.AllEnemiesDead
                && !selected.Snapshot.PlayerDead
                && selected.Snapshot.ProjectedPlayerHp > 0;
            int redrawDeficit = StrategicHpDeficit(root, fullRedrawPosterior);
            int selectedDeficit = StrategicHpDeficit(root, selected);
            if (redrawWon
                && (!selectedWon
                    || redrawDeficit < selectedDeficit
                    || redrawDeficit == selectedDeficit
                        && (fullRedrawPosterior.PotionCount < selected.PotionCount
                            || fullRedrawPosterior.PotionCount == selected.PotionCount
                                && fullRedrawPosterior.BestNode.Score > selected.BestNode.Score)))
            {
                selected = fullRedrawPosterior;
            }
            policy.Diagnostics.Info(
                $"[CombatSolver/Test] SMART_POTION_FULL_REDRAW_POSTERIOR " +
                $"won={redrawWon} hp_deficit={redrawDeficit} " +
                $"selected={ReferenceEquals(selected, fullRedrawPosterior)}");

            if (primary.PotionCount > 1)
            {
                SolverResult singlePotionPosterior = new CombatBeamSolver(
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
                    maximumPotionUses: 1,
                    fixedPrefixActions: [fullRedrawAction]).Solve();
                bool singlePotionDeepTriggered = shortCheckpointMilliseconds is { } singleCheckpoint
                    && singlePotionPosterior.Elapsed.TotalMilliseconds > singleCheckpoint;
                singlePotionPosterior.SearchPhase = singlePotionDeepTriggered
                    ? SolverSearchPhase.Deep
                    : SolverSearchPhase.Short;
                singlePotionPosterior.DeepSearchTriggered = singlePotionDeepTriggered;
                singlePotionPosterior.DeepSearchImprovedResult = false;
                singlePotionPosterior.SingleSessionSearch = true;
                PopulateSingleSessionTotals(
                    singlePotionPosterior,
                    shortCheckpointMilliseconds ?? profile.SoftTimeBudgetMilliseconds,
                    singlePotionDeepTriggered);
                searches.Add(singlePotionPosterior);

                bool singleWon = singlePotionPosterior.Snapshot.AllEnemiesDead
                    && !singlePotionPosterior.Snapshot.PlayerDead
                    && singlePotionPosterior.Snapshot.ProjectedPlayerHp > 0;
                selectedWon = selected.Snapshot.AllEnemiesDead
                    && !selected.Snapshot.PlayerDead
                    && selected.Snapshot.ProjectedPlayerHp > 0;
                int singleDeficit = StrategicHpDeficit(root, singlePotionPosterior);
                selectedDeficit = StrategicHpDeficit(root, selected);
                if (singleWon
                    && (!selectedWon
                        || singleDeficit < selectedDeficit
                        || singleDeficit == selectedDeficit
                            && (singlePotionPosterior.PotionCount < selected.PotionCount
                                || singlePotionPosterior.PotionCount == selected.PotionCount
                                    && singlePotionPosterior.BestNode.Score > selected.BestNode.Score)))
                {
                    selected = singlePotionPosterior;
                }
                policy.Diagnostics.Info(
                    $"[CombatSolver/Test] SMART_POTION_FULL_REDRAW_SINGLE_POSTERIOR " +
                    $"won={singleWon} hp_deficit={singleDeficit} " +
                    $"selected={ReferenceEquals(selected, singlePotionPosterior)}");
            }
        }
        for (int maximumPotionUses = selected.PotionCount - 1;
             potionFreeWon && maximumPotionUses >= 1 && selected.PotionCount > 1;
             maximumPotionUses--)
        {
            SolverResult fewerPotions = new CombatBeamSolver(
                root,
                displayNames,
                battleDamage,
                policy,
                cancellationToken,
                progressCallback,
                profile,
                shortCheckpointMilliseconds,
                SolverPotionPolicy.Smart,
                baseline,
                maximumPotionUses).Solve();
            bool fewerPotionsDeepTriggered = shortCheckpointMilliseconds is { } marginalCheckpoint
                && fewerPotions.Elapsed.TotalMilliseconds > marginalCheckpoint;
            fewerPotions.SearchPhase = fewerPotionsDeepTriggered
                ? SolverSearchPhase.Deep
                : SolverSearchPhase.Short;
            fewerPotions.DeepSearchTriggered = fewerPotionsDeepTriggered;
            fewerPotions.DeepSearchImprovedResult = false;
            fewerPotions.SingleSessionSearch = true;
            PopulateSingleSessionTotals(
                fewerPotions,
                shortCheckpointMilliseconds ?? profile.SoftTimeBudgetMilliseconds,
                fewerPotionsDeepTriggered);
            searches.Add(fewerPotions);

            bool fewerPotionsWon = fewerPotions.Snapshot.AllEnemiesDead
                && !fewerPotions.Snapshot.PlayerDead
                && fewerPotions.Snapshot.ProjectedPlayerHp > 0;
            if (!fewerPotionsWon || fewerPotions.PotionCount >= selected.PotionCount)
                continue;

            int marginalHpSaved = Math.Max(
                0,
                StrategicHpDeficit(root, fewerPotions) - StrategicHpDeficit(root, selected));
            int marginalHpRequired = Math.Max(
                0,
                selected.PotionHpRequired - fewerPotions.PotionHpRequired);
            bool additionalPotionsProtectLoot = policy.TheftPolicy == SolverTheftPolicy.PreserveResources
                && selected.OutstandingStolenResource < fewerPotions.OutstandingStolenResource;
            policy.Diagnostics.Info(
                $"[CombatSolver/Test] SMART_POTION_MARGINAL_AUDIT " +
                $"more={selected.PotionCount} fewer={fewerPotions.PotionCount} " +
                $"marginal_saved={marginalHpSaved} marginal_required={marginalHpRequired} " +
                $"protects_more_loot={additionalPotionsProtectLoot} " +
                $"selected={(additionalPotionsProtectLoot || marginalHpSaved >= marginalHpRequired ? "more" : "fewer")}");
            if (!additionalPotionsProtectLoot && marginalHpSaved < marginalHpRequired)
                selected = fewerPotions;
        }

        int correctedSaved = CorrectedPotionHpSaved(root, selected, potionFree);
        bool potionProtectsMoreLoot = policy.TheftPolicy == SolverTheftPolicy.PreserveResources
            && selected.OutstandingStolenResource < potionFree.OutstandingStolenResource;
        if (!potionProtectsMoreLoot && potionFreeWon && correctedSaved < selected.PotionHpRequired)
        {
            selected = potionFree;
        }
        else
        {
            selected.PotionHpSaved = potionFreeWon ? correctedSaved : selected.PotionHpSaved;
        }
        MergeAuditTotals(selected, [.. searches]);
        policy.Diagnostics.Info(
            $"[CombatSolver/Test] SMART_POTION_AUDIT result potion_free_won={potionFreeWon} " +
            $"corrected_saved={correctedSaved} required={selected.PotionHpRequired} " +
            $"potion_protects_more_loot={potionProtectsMoreLoot} " +
            $"selected={(ReferenceEquals(selected, potionFree) ? "potion_free" : "potion_route")}");
        return selected;
    }

    private static SolverResult AuditMissedSmartPotionUse(
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
        bool primaryWon = primary.Snapshot.AllEnemiesDead
            && !primary.Snapshot.PlayerDead
            && primary.Snapshot.ProjectedPlayerHp > 0;
        int primaryHpDeficit = StrategicHpDeficit(root, primary);
        if (root.SearchablePotionCount == 0
            || primaryWon && primaryHpDeficit == 0)
        {
            return primary;
        }

        PotionFreePolicyBaseline baseline = new(
            primaryWon,
            primaryHpDeficit,
            primary.Snapshot.PlayerHp);
        policy.Diagnostics.Info(
            $"[CombatSolver/Test] SMART_POTION_INTERVENTION start " +
            $"potion_free_won={primaryWon} hp_deficit={primaryHpDeficit} " +
            $"searchable_potions={root.SearchablePotionCount}");
        SolverResult forcedPotion;
        try
        {
            forcedPotion = new CombatBeamSolver(
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
        }
        catch (PotionPolicyUnsatisfiedException)
        {
            policy.Diagnostics.Info(
                "[CombatSolver/Test] SMART_POTION_INTERVENTION result forced_route_missing=true selected=potion_free");
            return primary;
        }

        bool forcedDeepTriggered = shortCheckpointMilliseconds is { } checkpoint
            && forcedPotion.Elapsed.TotalMilliseconds > checkpoint;
        forcedPotion.SearchPhase = forcedDeepTriggered
            ? SolverSearchPhase.Deep
            : SolverSearchPhase.Short;
        forcedPotion.DeepSearchTriggered = forcedDeepTriggered;
        forcedPotion.DeepSearchImprovedResult = false;
        forcedPotion.SingleSessionSearch = true;
        PopulateSingleSessionTotals(
            forcedPotion,
            shortCheckpointMilliseconds ?? profile.SoftTimeBudgetMilliseconds,
            forcedDeepTriggered);

        List<SolverResult> searches = [primary, forcedPotion];
        PlanAction[] prefix = PrefixThroughFirstPotion(forcedPotion);
        if (prefix.Length > 0)
        {
            SolverResult posterior = new CombatBeamSolver(
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
                maximumPotionUses: 1,
                fixedPrefixActions: prefix).Solve();
            bool posteriorDeepTriggered = shortCheckpointMilliseconds is { } posteriorCheckpoint
                && posterior.Elapsed.TotalMilliseconds > posteriorCheckpoint;
            posterior.SearchPhase = posteriorDeepTriggered
                ? SolverSearchPhase.Deep
                : SolverSearchPhase.Short;
            posterior.DeepSearchTriggered = posteriorDeepTriggered;
            posterior.DeepSearchImprovedResult = false;
            posterior.SingleSessionSearch = true;
            PopulateSingleSessionTotals(
                posterior,
                shortCheckpointMilliseconds ?? profile.SoftTimeBudgetMilliseconds,
                posteriorDeepTriggered);
            searches.Add(posterior);

            bool posteriorWon = posterior.Snapshot.AllEnemiesDead
                && !posterior.Snapshot.PlayerDead
                && posterior.Snapshot.ProjectedPlayerHp > 0;
            bool forcedSearchWon = forcedPotion.Snapshot.AllEnemiesDead
                && !forcedPotion.Snapshot.PlayerDead
                && forcedPotion.Snapshot.ProjectedPlayerHp > 0;
            int posteriorDeficit = StrategicHpDeficit(root, posterior);
            int forcedDeficit = StrategicHpDeficit(root, forcedPotion);
            if (posteriorWon
                && (!forcedSearchWon
                    || posteriorDeficit < forcedDeficit
                    || posteriorDeficit == forcedDeficit && posterior.BestNode.Score > forcedPotion.BestNode.Score))
            {
                forcedPotion = posterior;
            }
            policy.Diagnostics.Info(
                $"[CombatSolver/Test] SMART_POTION_POSTERIOR prefix_actions={prefix.Length} " +
                $"won={posteriorWon} hp_deficit={posteriorDeficit} " +
                $"selected={ReferenceEquals(forcedPotion, posterior)}");
        }

        bool forcedWon = forcedPotion.Snapshot.AllEnemiesDead
            && !forcedPotion.Snapshot.PlayerDead
            && forcedPotion.Snapshot.ProjectedPlayerHp > 0;
        int hpSaved = CorrectedPotionHpSaved(root, forcedPotion, primary);
        int ambergrisCount = forcedPotion.BestNode.Actions.Count(action =>
            action.Kind == PlanActionKind.UsePotion
            && string.Equals(action.PotionId, "AMBERGRIS", StringComparison.Ordinal));
        int strategicHpCost = forcedPotion.BestNode.Actions
            .Where(action => action.Kind == PlanActionKind.UsePotion && action.PotionId != null)
            .Sum(action => PotionUsePolicy.StrategicHpCost(action.PotionId!));
        int hpRequired = PotionUsePolicy.EffectiveStrategicHpCost(
            strategicHpCost,
            ambergrisCount,
            root.InitialPlayerMaxHp);
        if (ambergrisCount == 0 && primaryWon)
            hpRequired = PotionUsePolicy.SmartRequiredHpSaved(hpRequired, primaryHpDeficit);
        bool protectsMoreLoot = policy.TheftPolicy == SolverTheftPolicy.PreserveResources
            && forcedPotion.OutstandingStolenResource < primary.OutstandingStolenResource;
        bool selectPotion = forcedWon
            && (!primaryWon || protectsMoreLoot || hpSaved >= hpRequired);
        forcedPotion.PotionHpSaved = hpSaved;
        forcedPotion.PotionHpRequired = hpRequired;
        SolverResult selected = selectPotion ? forcedPotion : primary;
        MergeAuditTotals(selected, [.. searches]);
        policy.Diagnostics.Info(
            $"[CombatSolver/Test] SMART_POTION_INTERVENTION result " +
            $"forced_won={forcedWon} saved={hpSaved} required={hpRequired} " +
            $"protects_more_loot={protectsMoreLoot} " +
            $"selected={(selectPotion ? "potion_route" : "potion_free")}");
        return selected;
    }

    private static PlanAction[] PrefixThroughFirstPotion(SolverResult result)
    {
        IReadOnlyList<PlanAction> actions = result.BestNode.Actions;
        for (int index = 0; index < actions.Count; index++)
        {
            PlanAction action = actions[index];
            if (action.Turn != result.StartTurnNumber
                || action.Kind == PlanActionKind.EndTurn
                || action.EndsPlayerTurn)
            {
                return [];
            }
            if (action.Kind == PlanActionKind.UsePotion)
                return actions.Take(index + 1).ToArray();
        }
        return [];
    }

    private static int CorrectedPotionHpSaved(
        CombatRootSnapshot root,
        SolverResult potionRoute,
        SolverResult potionFree)
        => potionRoute.BestNode.Actions.Any(action =>
                action.Kind == PlanActionKind.UsePotion
                && string.Equals(action.PotionId, "AMBERGRIS", StringComparison.Ordinal))
            ? Math.Max(0, potionRoute.Snapshot.PlayerHp - potionFree.Snapshot.PlayerHp)
            : Math.Max(
                0,
                StrategicHpDeficit(root, potionFree) - StrategicHpDeficit(root, potionRoute));

    private static int StrategicHpDeficit(CombatRootSnapshot root, SolverResult result)
        => result.Snapshot.CumulativePlayerHpLost
            + Math.Max(0, root.InitialPlayerMaxHp - result.Snapshot.PlayerMaxHp);

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
