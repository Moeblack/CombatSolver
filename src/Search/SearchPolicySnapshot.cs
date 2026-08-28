namespace CombatSolver;

internal sealed record SearchPolicySnapshot(
    SolverSearchProfile ShortProfile,
    SolverSearchProfile DeepProfile,
    SolverPotionPolicy PotionPolicy,
    bool DetailedDiagnostics,
    bool VerifyIncrementalSearch,
    bool ForceShortOnly,
    bool MeasurePhasePerformance,
    int? ShortBudgetOverrideMilliseconds,
    int? DeepBudgetOverrideMilliseconds,
    bool IncludeTurnSetup,
    SolverTheftPolicy? TheftPolicy,
    SearchDiagnosticsSink Diagnostics,
    SearchFramePressureSignal FramePressureSignal,
    SearchMemoryPressureSignal MemoryPressureSignal);
