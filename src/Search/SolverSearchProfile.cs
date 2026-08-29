namespace CombatSolver;

internal enum SolverSearchPhase
{
    Short,
    Deep,
}

internal sealed record SolverSearchProfile(
    SolverSearchPhase Phase,
    int BeamWidth,
    int MaxExpandedNodes,
    int MaxCardBranchesPerNode,
    int MaxPileChoiceBranchesPerAction,
    int MaxHandChoiceBranchesPerAction,
    int SoftTimeBudgetMilliseconds)
{
    public static SolverSearchProfile Short { get; } = new(
        SolverSearchPhase.Short,
        BeamWidth: 18,
        MaxExpandedNodes: 2_400,
        MaxCardBranchesPerNode: 14,
        MaxPileChoiceBranchesPerAction: 6,
        MaxHandChoiceBranchesPerAction: 8,
        SoftTimeBudgetMilliseconds: 8_000);

    public static SolverSearchProfile Deep { get; } = new(
        SolverSearchPhase.Deep,
        BeamWidth: 45,
        MaxExpandedNodes: 12_000,
        MaxCardBranchesPerNode: 24,
        MaxPileChoiceBranchesPerAction: 12,
        MaxHandChoiceBranchesPerAction: 16,
        SoftTimeBudgetMilliseconds: 120_000);
}
