using System.Diagnostics;

namespace CombatSolver;

internal enum SearchMetricPhase
{
    Fork,
    Action,
    CardExecution,
    CardPostProcessing,
    PotionExecution,
    RoundAdvance,
    RoundPlayerEnd,
    RoundEndSimulation,
    RoundFlush,
    RoundPlayerEndPowers,
    RoundEnemyTurn,
    RoundEnemyStart,
    RoundEnemyMoves,
    RoundEnemyEndPowers,
    RoundPlayerStart,
    RoundDraw,
    Snapshot,
    ThreatProjection,
    Fingerprint,
    PileFingerprint,
    PileFingerprintMiss,
    CardFingerprintMiss,
    CombatFingerprint,
    Prune,
    FinalSelection,
}

internal readonly record struct SearchMeasurement(long Timestamp, long AllocatedBytes)
{
    public static SearchMeasurement Disabled => new(0, 0);
}

internal sealed class SearchPerformanceMetrics(bool enabled)
{
    private readonly long[] _ticks = new long[Enum.GetValues<SearchMetricPhase>().Length];
    private readonly long[] _allocatedBytes = new long[Enum.GetValues<SearchMetricPhase>().Length];

    public SearchMeasurement Begin()
        => enabled
            ? new SearchMeasurement(Stopwatch.GetTimestamp(), GC.GetAllocatedBytesForCurrentThread())
            : SearchMeasurement.Disabled;

    public SearchMeasurementScope Measure(SearchMetricPhase phase)
        => new(this, phase, Begin());

    public void End(SearchMetricPhase phase, SearchMeasurement measurement)
    {
        if (!enabled)
            return;
        int index = (int)phase;
        _ticks[index] += Stopwatch.GetTimestamp() - measurement.Timestamp;
        _allocatedBytes[index] += GC.GetAllocatedBytesForCurrentThread() - measurement.AllocatedBytes;
    }

    public SearchPhaseMetric Snapshot(SearchMetricPhase phase)
    {
        int index = (int)phase;
        return new SearchPhaseMetric(
            Stopwatch.GetElapsedTime(0, _ticks[index]),
            _allocatedBytes[index]);
    }
}

internal readonly struct SearchMeasurementScope(
    SearchPerformanceMetrics owner,
    SearchMetricPhase phase,
    SearchMeasurement measurement) : IDisposable
{
    public void Dispose() => owner.End(phase, measurement);
}

internal readonly record struct SearchPhaseMetric(TimeSpan Elapsed, long AllocatedBytes);
