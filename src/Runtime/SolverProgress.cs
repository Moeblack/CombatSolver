namespace CombatSolver;

internal sealed record SolverInterimResult(
    bool Won,
    int OutstandingStolenResource,
    int ProjectedBattleHpLost,
    int StrategicHpDeficit,
    int PotionStrategicCost,
    int ProjectedBattlePotionCount,
    int EnemyHp,
    double Score);

internal sealed record SolverProgress(
    int StartTurnNumber,
    int CurrentTurnNumber,
    int CompletedTurnLayers,
    int PlayDepth,
    int ExpandedNodes,
    long ReviewedWorldlines,
    int MaxNodes,
    int FrontierNodes,
    int EndedNodes,
    long ElapsedMilliseconds,
    string Phase,
    SolverInterimResult? CurrentBestResult = null);
