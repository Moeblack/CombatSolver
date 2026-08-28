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
        BeamWidth: 12,
        MaxExpandedNodes: 1_200,
        MaxCardBranchesPerNode: 10,
        MaxPileChoiceBranchesPerAction: 4,
        MaxHandChoiceBranchesPerAction: 6,
        SoftTimeBudgetMilliseconds: 5_000);

    public static SolverSearchProfile Deep { get; } = new(
        SolverSearchPhase.Deep,
        BeamWidth: 30,
        MaxExpandedNodes: 6_000,
        MaxCardBranchesPerNode: 16,
        MaxPileChoiceBranchesPerAction: 8,
        MaxHandChoiceBranchesPerAction: 10,
        SoftTimeBudgetMilliseconds: 60_000);
}
