namespace CombatSolver;

internal sealed partial class CombatBeamSolver
{
    private sealed class FinalPlanOrdering(
        SolverPotionPolicy potionPolicy,
        SolverTheftPolicy? theftPolicy,
        PotionFreePolicyBaseline? potionFreePolicyBaseline,
        int initialPlayerMaxHp,
        SearchDiagnosticsSink diagnostics,
        bool detailedDiagnostics,
        BattleDamageSnapshot battleDamage)
    {
        public FinalPlanSelection Select(
            IReadOnlyList<(SearchNode Node, SimulationSnapshot Snapshot, RouteAnnotations Annotations)> evaluated,
            int initialHp,
            bool emitDiagnostics)
        {
            var policyCandidates = evaluated
                .Select(candidate =>
                {
                    SearchFeatures features = SearchFeatures.Capture(candidate.Node);
                    int sold = features.FutureSoldHp;
                    int battleSold = battleDamage.SoldHpCommitted + sold;
                    int potionCount = features.PotionCount;
                    int ambergrisCount = candidate.Node.Actions.Count(action =>
                        action.Kind == PlanActionKind.UsePotion
                        && string.Equals(action.PotionId, "AMBERGRIS", StringComparison.Ordinal));
                    int hpDeficit = Math.Max(0, initialHp - candidate.Snapshot.PlayerHp);
                    int strategicHpDeficit = hpDeficit;
                    int strategicSold = battleSold;
                    int policyHpDeficit = strategicHpDeficit
                        + (potionPolicy == SolverPotionPolicy.RequireAtLeastOne
                            ? PotionUsePolicy.AdditionalRequiredUseStrategicHpCost(
                                candidate.Node.PotionStrategicCost)
                            : 0);
                    return (candidate.Node, candidate.Snapshot, candidate.Annotations, Features: features,
                        FutureSold: sold, BattleSold: battleSold, PotionCount: potionCount, HpDeficit: hpDeficit,
                        StrategicHpDeficit: strategicHpDeficit, PolicyHpDeficit: policyHpDeficit,
                        StrategicSold: strategicSold, PotionStrategicCost: candidate.Node.PotionStrategicCost,
                        AmbergrisCount: ambergrisCount, Score: features.Score);
                })
                .ToList();
            if (emitDiagnostics && detailedDiagnostics)
            {
                foreach (var potionGroup in policyCandidates
                             .GroupBy(candidate => candidate.PotionCount)
                             .OrderBy(group => group.Key))
                {
                    var diagnostic = potionGroup
                        .OrderByDescending(candidate => candidate.Features.AllEnemiesDead)
                        .ThenByDescending(candidate => candidate.Features.ProjectedPlayerHp)
                        .ThenBy(candidate => candidate.Features.EnemyHp)
                        .ThenByDescending(candidate => candidate.Score)
                        .First();
                    diagnostics.Info(
                        $"[CombatSolver/Debug] POTION_FINAL_CANDIDATE count={potionGroup.Key} " +
                        $"won={diagnostic.Features.AllEnemiesDead} hp={diagnostic.Snapshot.PlayerHp} " +
                        $"projected_hp={diagnostic.Features.ProjectedPlayerHp} " +
                        $"enemy_hp={diagnostic.Features.EnemyHp} " +
                        $"actions={string.Join(',', diagnostic.Node.Actions.Select(CombatBeamSolver.PolicyActionToken))}");
                }
            }
            int potionFreeBaselineIndex = policyCandidates
                .Select((candidate, index) => (Candidate: candidate, Index: index))
                .Where(item => item.Candidate.PotionCount == 0)
                .OrderByDescending(item => item.Candidate.Features.AllEnemiesDead)
                .ThenBy(item => theftPolicy == SolverTheftPolicy.PreserveResources
                    ? item.Candidate.Features.OutstandingStolenResource
                    : 0)
                .ThenBy(item => item.Candidate.StrategicHpDeficit)
                .ThenBy(item => CombatBeamSolver.PolicyBoundaryRank(item.Candidate.Features.BoundaryReason))
                .ThenBy(item => item.Candidate.Features.EnemyHp)
                .ThenByDescending(item => item.Candidate.Score)
                .ThenBy(item => item.Candidate.StrategicSold)
                .ThenBy(item => item.Candidate.Features.ActionCount)
                .Select(item => item.Index)
                .DefaultIfEmpty(-1)
                .First();
            bool hasPotionFreeBaseline = potionFreeBaselineIndex >= 0;
            bool potionFreeWon = hasPotionFreeBaseline
                && policyCandidates[potionFreeBaselineIndex].Features.AllEnemiesDead;
            int potionFreeStrategicHpDeficit = hasPotionFreeBaseline
                ? policyCandidates[potionFreeBaselineIndex].StrategicHpDeficit
                : initialHp;
            int potionFreePlayerHp = hasPotionFreeBaseline
                ? policyCandidates[potionFreeBaselineIndex].Snapshot.PlayerHp
                : 0;
            int potionFreeOutstandingResource = hasPotionFreeBaseline
                ? policyCandidates[potionFreeBaselineIndex].Features.OutstandingStolenResource
                : int.MaxValue;
            if (potionFreePolicyBaseline is { } auditedBaseline)
            {
                hasPotionFreeBaseline = true;
                potionFreeWon = auditedBaseline.Won;
                potionFreeStrategicHpDeficit = auditedBaseline.HpDeficit;
                potionFreePlayerHp = auditedBaseline.PlayerHp;
            }
            if (emitDiagnostics)
            {
                if (potionFreeBaselineIndex >= 0)
                {
                    var potionFreeBaseline = policyCandidates[potionFreeBaselineIndex];
                    diagnostics.Info(
                        $"[CombatSolver/Test] POLICY_BASELINE kind=potion_free " +
                        $"won={potionFreeWon} hp_deficit={potionFreeBaseline.HpDeficit} " +
                        $"enemy_hp={potionFreeBaseline.Features.EnemyHp} " +
                        $"boundary={potionFreeBaseline.Features.BoundaryReason} " +
                        $"actions={string.Join(',', potionFreeBaseline.Node.Actions.Select(CombatBeamSolver.PolicyActionToken))}");
                }
                else
                {
                    diagnostics.Info(
                        $"[CombatSolver/Test] POLICY_BASELINE kind=potion_free missing=true " +
                        $"won=false hp_deficit={initialHp}");
                }
                if (potionFreePolicyBaseline is { } baselineOverride)
                {
                    diagnostics.Info(
                        $"[CombatSolver/Test] POLICY_BASELINE_OVERRIDE kind=potion_free " +
                        $"won={baselineOverride.Won} hp_deficit={baselineOverride.HpDeficit}");
                }
            }
            var selected = policyCandidates
                .Where(candidate =>
                    (PotionUsePolicy.IsEligible(
                         potionPolicy,
                         candidate.PotionCount,
                         candidate.PotionStrategicCost,
                         potionFreeWon,
                         potionFreeStrategicHpDeficit,
                         candidate.Features.AllEnemiesDead,
                         candidate.StrategicHpDeficit)
                     || theftPolicy == SolverTheftPolicy.PreserveResources
                        && candidate.PotionCount > 0
                        && candidate.Features.OutstandingStolenResource < potionFreeOutstandingResource)
                    && PotionUsePolicy.MeetsAmbergrisRestriction(
                        hasPotionFreeBaseline,
                        candidate.AmbergrisCount,
                        candidate.PotionStrategicCost,
                        initialPlayerMaxHp,
                        potionFreePlayerHp,
                        candidate.Snapshot.PlayerHp))
                .OrderByDescending(candidate => candidate.Features.AllEnemiesDead)
                .ThenBy(candidate => theftPolicy == SolverTheftPolicy.PreserveResources
                    ? candidate.Features.OutstandingStolenResource
                    : 0)
                .ThenBy(candidate => candidate.PolicyHpDeficit)
                .ThenBy(candidate => candidate.StrategicHpDeficit)
                .ThenBy(candidate => CombatBeamSolver.PolicyBoundaryRank(candidate.Features.BoundaryReason))
                .ThenBy(candidate => candidate.PotionCount)
                .ThenBy(candidate => candidate.StrategicSold)
                .ThenBy(candidate => candidate.Features.EnemyHp)
                .ThenByDescending(candidate => candidate.Score)
                .ThenBy(candidate => candidate.Features.ActionCount)
                .ToList();
            if (selected.Count == 0)
            {
                throw new PotionPolicyUnsatisfiedException(
                    potionPolicy == SolverPotionPolicy.RequireAtLeastOne
                        ? "本场药水策略要求至少使用一瓶，但搜索没有找到可执行的用药路线。"
                        : "本场药水策略没有可执行路线。");
            }
            var selectedCandidate = selected[0];
            int potionBranchesRejected = policyCandidates.Count(candidate =>
                candidate.PotionCount > 0
                && (!(PotionUsePolicy.IsEligible(
                          potionPolicy,
                          candidate.PotionCount,
                          candidate.PotionStrategicCost,
                          potionFreeWon,
                          potionFreeStrategicHpDeficit,
                          candidate.Features.AllEnemiesDead,
                          candidate.StrategicHpDeficit)
                      || theftPolicy == SolverTheftPolicy.PreserveResources
                         && candidate.Features.OutstandingStolenResource < potionFreeOutstandingResource)
                    || !PotionUsePolicy.MeetsAmbergrisRestriction(
                        hasPotionFreeBaseline,
                        candidate.AmbergrisCount,
                        candidate.PotionStrategicCost,
                        initialPlayerMaxHp,
                        potionFreePlayerHp,
                        candidate.Snapshot.PlayerHp)));
            int potionHpSaved = selectedCandidate.PotionCount == 0
                ? 0
                : selectedCandidate.AmbergrisCount > 0
                    ? Math.Max(0, selectedCandidate.Snapshot.PlayerHp - potionFreePlayerHp)
                    : PotionUsePolicy.HpSaved(
                        potionFreeStrategicHpDeficit,
                        selectedCandidate.StrategicHpDeficit);
            int potionHpRequired = PotionUsePolicy.EffectiveStrategicHpCost(
                selectedCandidate.PotionStrategicCost,
                selectedCandidate.AmbergrisCount,
                initialPlayerMaxHp);
            if (potionPolicy == SolverPotionPolicy.RequireAtLeastOne)
            {
                potionHpRequired = PotionUsePolicy.AdditionalRequiredUseStrategicHpCost(
                    potionHpRequired);
            }
            return new FinalPlanSelection(
                new FinalPlanCandidate(
                    selectedCandidate.Node,
                    selectedCandidate.Snapshot,
                    selectedCandidate.Annotations,
                    selectedCandidate.Features,
                    selectedCandidate.FutureSold,
                    selectedCandidate.BattleSold,
                    selectedCandidate.PotionCount,
                    selectedCandidate.Score),
                potionBranchesRejected,
                potionHpSaved,
                potionHpRequired);
        }
    }
}
