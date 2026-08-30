using System.Diagnostics;
using System.Runtime;

namespace CombatSolver;

// Owns process-wide GC mode and combat-end reclamation; it is not part of the search algorithm.
internal static class SearchGcPolicy
{
    private const long BackgroundReclaimThresholdBytes = 256L * 1024 * 1024;
    private const int ReclaimReferenceReleaseDelayMilliseconds = 250;
    private const int ReclaimCompletionTimeoutMilliseconds = 30_000;
    private const int ConcurrentSearchExitPollMilliseconds = 10;
    private static readonly Lock Gate = new();
    private static int _activeSearches;
    private static GCLatencyMode _previousMode;
    private static bool _latencyModeOwned;
    private static bool _noGcRegionActive;
    private static bool _reclaimRequired;
    private static bool _reclaimRequested;
    private static bool _reclaimActive;
    private static string _reclaimReason = "unspecified";
    private static TaskCompletionSource? _reclaimCompletion;
    private static Task _reclaimTask = Task.CompletedTask;
    private static long _noGcRegionAllocatedBytesAtStart;
    private static long _noGcRegionBudgetBytes;
    private static long _noGcRegionLohBudgetBytes;
    private static long _largestSearchAllocatedBytes;
    private static int _rolloverCountForTesting;
    private static int _budgetChangeRebuildCountForTesting;
    private static int _budgetChangeWaitCountForTesting;
    internal static int RolloverCountForTesting
    {
        get
        {
            lock (Gate)
                return _rolloverCountForTesting;
        }
    }
    internal static int BudgetChangeRebuildCountForTesting
    {
        get
        {
            lock (Gate)
                return _budgetChangeRebuildCountForTesting;
        }
    }
    internal static int BudgetChangeWaitCountForTesting
    {
        get
        {
            lock (Gate)
                return _budgetChangeWaitCountForTesting;
        }
    }
    internal static long CurrentNoGcRegionBudgetBytesForTesting
    {
        get
        {
            lock (Gate)
                return _noGcRegionBudgetBytes;
        }
    }

    private enum NoGcRegionStartOutcome
    {
        Started,
        InsufficientMemory,
        RegionSizeUnsupported,
    }

    internal static void ResetCountersForTesting()
    {
        lock (Gate)
        {
            _rolloverCountForTesting = 0;
            _budgetChangeRebuildCountForTesting = 0;
            _budgetChangeWaitCountForTesting = 0;
        }
    }

    public static IDisposable EnterLowLatencySearch(
        long noGcRegionBudgetBytes,
        SearchMemoryPressureSignal memoryPressureSignal,
        CancellationToken cancellationToken)
    {
        if (noGcRegionBudgetBytes <= 0)
            throw new ArgumentOutOfRangeException(nameof(noGcRegionBudgetBytes));
        ArgumentNullException.ThrowIfNull(memoryPressureSignal);
        long noGcRegionLohBudgetBytes = Math.Max(
            256L * 1024 * 1024,
            noGcRegionBudgetBytes / 6);
        bool budgetChangeLogged = false;
        while (true)
        {
            Task? reclaimTask = null;
            bool waitForActiveSearchExit = false;
            lock (Gate)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (_reclaimActive || _reclaimRequested)
                {
                    reclaimTask = _reclaimTask;
                }
                else
                {
                    long allocatedBytesAtEntry = GC.GetTotalAllocatedBytes(precise: false);
                    bool requestedBudgetDiffers = (_noGcRegionActive || _activeSearches > 0)
                        && (_noGcRegionBudgetBytes != noGcRegionBudgetBytes
                            || _noGcRegionLohBudgetBytes != noGcRegionLohBudgetBytes);
                    if (_activeSearches > 0 && requestedBudgetDiffers)
                    {
                        // A process-wide No-GC region cannot be resized while another search uses it.
                        // Wait without registering a reclaim request: doing so could deadlock if the
                        // active search reaches its own in-search memory checkpoint before exiting.
                        waitForActiveSearchExit = true;
                    }
                    else if (_activeSearches == 0)
                    {
                        if (_noGcRegionActive)
                        {
                            if (GCSettings.LatencyMode == GCLatencyMode.NoGCRegion)
                            {
                                if (requestedBudgetDiffers)
                                {
                                    _budgetChangeRebuildCountForTesting++;
                                    _reclaimRequired = true;
                                    Entry.Logger.Info(
                                        $"[CombatSolver/Test] GC_NO_GC_REGION_BUDGET_CHANGED " +
                                        $"previous={_noGcRegionBudgetBytes} requested={noGcRegionBudgetBytes} " +
                                        "reclaim=background_non_compacting");
                                    reclaimTask = RequestReclaimLocked("no_gc_region_budget_changed");
                                }
                                else
                                {
                                    long allocated = Math.Max(
                                        0,
                                        allocatedBytesAtEntry - _noGcRegionAllocatedBytesAtStart);
                                    long remaining = Math.Max(0, _noGcRegionBudgetBytes - allocated);
                                    long required = checked(
                                        _largestSearchAllocatedBytes + _largestSearchAllocatedBytes / 4);
                                    if (_largestSearchAllocatedBytes > 0 && remaining < required)
                                    {
                                        _rolloverCountForTesting++;
                                        _reclaimRequired = true;
                                        Entry.Logger.Info(
                                            $"[CombatSolver/Test] GC_NO_GC_REGION_ROLLOVER " +
                                            $"allocated={allocated} remaining={remaining} required={required} " +
                                            "reclaim=background_non_compacting");
                                        reclaimTask = RequestReclaimLocked("no_gc_region_rollover");
                                    }
                                    else
                                    {
                                        ConfigureSearchMemoryLimit(
                                            memoryPressureSignal,
                                            allocatedBytesAtEntry,
                                            remaining,
                                            _noGcRegionBudgetBytes,
                                            _noGcRegionLohBudgetBytes);
                                        _activeSearches++;
                                        Entry.Logger.Info(
                                            "[CombatSolver/Test] GC_LATENCY policy=combat_scoped_no_gc_region_reuse");
                                        return new SearchScope(allocatedBytesAtEntry, memoryPressureSignal);
                                    }
                                }
                            }
                            else
                            {
                                _noGcRegionActive = false;
                                _reclaimRequired = true;
                                RestoreLatencyModeLocked();
                                Entry.Logger.Warn(
                                    "[CombatSolver/Test] GC_LATENCY no_gc_region_exhausted=true " +
                                    "reclaim=background_non_compacting");
                                reclaimTask = RequestReclaimLocked("no_gc_region_exhausted");
                            }
                        }
                        else if (_reclaimRequired)
                        {
                            reclaimTask = RequestReclaimLocked("before_next_search");
                        }
                        else
                        {
                            _previousMode = GCSettings.LatencyMode;
                            _latencyModeOwned = true;
                            NoGcRegionStartOutcome startOutcome = TryStartNoGcRegion(
                                noGcRegionBudgetBytes,
                                noGcRegionLohBudgetBytes);
                            _noGcRegionActive = startOutcome == NoGcRegionStartOutcome.Started;
                            _noGcRegionBudgetBytes = noGcRegionBudgetBytes;
                            _noGcRegionLohBudgetBytes = noGcRegionLohBudgetBytes;
                            if (_noGcRegionActive)
                            {
                                _noGcRegionAllocatedBytesAtStart = GC.GetTotalAllocatedBytes(precise: false);
                                Entry.Logger.Info(
                                    $"[CombatSolver/Test] GC_LATENCY policy=combat_scoped_no_gc_region " +
                                    $"budget={noGcRegionBudgetBytes} loh_budget={noGcRegionLohBudgetBytes} " +
                                    $"current={GCSettings.LatencyMode}");
                            }
                            else
                            {
                                GCSettings.LatencyMode = GCLatencyMode.SustainedLowLatency;
                                Entry.Logger.Info(
                                    $"[CombatSolver/Test] GC_LATENCY policy=no_gc_region_unavailable " +
                                    $"reason={FormatStartOutcome(startOutcome)} " +
                                    $"budget={noGcRegionBudgetBytes} loh_budget={noGcRegionLohBudgetBytes} " +
                                    $"fallback={GCSettings.LatencyMode}");
                            }
                            ConfigureSearchMemoryLimit(
                                memoryPressureSignal,
                                allocatedBytesAtEntry,
                                noGcRegionBudgetBytes,
                                noGcRegionBudgetBytes,
                                noGcRegionLohBudgetBytes);
                            _activeSearches++;
                            return new SearchScope(allocatedBytesAtEntry, memoryPressureSignal);
                        }
                    }
                    else
                    {
                        long remaining = _noGcRegionActive
                            ? Math.Max(
                                0,
                                _noGcRegionBudgetBytes - Math.Max(
                                    0,
                                    allocatedBytesAtEntry - _noGcRegionAllocatedBytesAtStart))
                            : noGcRegionBudgetBytes;
                        ConfigureSearchMemoryLimit(
                            memoryPressureSignal,
                            allocatedBytesAtEntry,
                            remaining,
                            _noGcRegionActive ? _noGcRegionBudgetBytes : noGcRegionBudgetBytes,
                            _noGcRegionActive ? _noGcRegionLohBudgetBytes : noGcRegionLohBudgetBytes);
                        _activeSearches++;
                        return new SearchScope(allocatedBytesAtEntry, memoryPressureSignal);
                    }
                }
            }

            if (waitForActiveSearchExit)
            {
                if (!budgetChangeLogged)
                {
                    lock (Gate)
                        _budgetChangeWaitCountForTesting++;
                    Entry.Logger.Info(
                        $"[CombatSolver/Test] GC_NO_GC_REGION_BUDGET_WAIT " +
                        $"requested={noGcRegionBudgetBytes} reason=active_search");
                    budgetChangeLogged = true;
                }
                cancellationToken.ThrowIfCancellationRequested();
                Thread.Sleep(ConcurrentSearchExitPollMilliseconds);
                continue;
            }

            (reclaimTask ?? throw new InvalidOperationException("GC 回收状态缺少完成任务。"))
                .WaitAsync(cancellationToken)
                .GetAwaiter()
                .GetResult();
        }
    }

    public static Task ReclaimIfPendingAsync(string reason)
    {
        lock (Gate)
        {
            if (_reclaimActive)
            {
                _reclaimRequired = true;
                _reclaimReason = reason;
                return WaitForReclaimChainAsync(_reclaimTask);
            }
            if (_reclaimRequested)
                return WaitForReclaimChainAsync(_reclaimTask);
            if (_activeSearches == 0 && !_noGcRegionActive && !_reclaimRequired)
                return Task.CompletedTask;
            _reclaimRequired = true;
            return WaitForReclaimChainAsync(RequestReclaimLocked(reason));
        }
    }

    private static async Task WaitForReclaimChainAsync(Task checkpoint)
    {
        while (true)
        {
            await checkpoint;
            lock (Gate)
            {
                if (!_reclaimActive && !_reclaimRequested)
                    return;
                checkpoint = _reclaimTask;
            }
        }
    }

    private static Task RequestReclaimLocked(string reason)
    {
        if (!_reclaimActive && !_reclaimRequested)
        {
            _reclaimRequested = true;
            _reclaimReason = reason;
            _reclaimCompletion = new TaskCompletionSource(
                TaskCreationOptions.RunContinuationsAsynchronously);
            _reclaimTask = _reclaimCompletion.Task;
        }
        if (!_reclaimActive && _activeSearches == 0)
            StartReclaimLocked();
        return _reclaimTask;
    }

    private static void StartReclaimLocked()
    {
        if (!_reclaimRequested || _activeSearches != 0)
            throw new InvalidOperationException("GC 回收只能在请求已登记且搜索线程退出后启动。");

        TaskCompletionSource completion = _reclaimCompletion
            ?? throw new InvalidOperationException("GC 回收请求缺少完成信号。");
        string reason = _reclaimReason;
        bool endNoGcRegion = _noGcRegionActive
            && GCSettings.LatencyMode == GCLatencyMode.NoGCRegion;
        bool restoreLatencyMode = _latencyModeOwned;
        GCLatencyMode previousMode = _previousMode;
        _reclaimRequested = false;
        _reclaimActive = true;
        _reclaimRequired = false;
        _noGcRegionActive = false;
        _latencyModeOwned = false;
        _noGcRegionAllocatedBytesAtStart = 0;
        _noGcRegionBudgetBytes = 0;
        _noGcRegionLohBudgetBytes = 0;
        _largestSearchAllocatedBytes = 0;

        _ = Task.Run(async () =>
        {
            Exception? failure = null;
            try
            {
                long liveBefore = GC.GetTotalMemory(forceFullCollection: false);
                using Process processBefore = Process.GetCurrentProcess();
                long workingSetBefore = processBefore.WorkingSet64;
                long privateBefore = processBefore.PrivateMemorySize64;
                TimeSpan pauseBefore = GC.GetTotalPauseDuration();
                Stopwatch stopwatch = Stopwatch.StartNew();
                if (endNoGcRegion)
                    GC.EndNoGCRegion();
                if (restoreLatencyMode)
                    GCSettings.LatencyMode = previousMode;

                await Task.Delay(ReclaimReferenceReleaseDelayMilliseconds);
                GCMemoryInfo completedCollection = await CollectGeneration2InBackgroundAsync();
                stopwatch.Stop();
                GCMemoryInfo memory = GC.GetGCMemoryInfo();
                using Process processAfter = Process.GetCurrentProcess();
                processAfter.Refresh();
                Entry.Logger.Info(
                    $"[CombatSolver/Test] HEAP_RECLAIM reason={reason} " +
                    $"mode=background_non_compacting no_gc_region_ended={endNoGcRegion} " +
                    $"elapsed_ms={stopwatch.Elapsed.TotalMilliseconds:F1} " +
                    $"gc_pause_delta_ms={(GC.GetTotalPauseDuration() - pauseBefore).TotalMilliseconds:F1} " +
                    $"concurrent={completedCollection.Concurrent} compacted={completedCollection.Compacted} " +
                    $"managed_live_before={liveBefore} managed_live_after={GC.GetTotalMemory(false)} " +
                    $"managed_heap_after={memory.HeapSizeBytes} fragmented_after={memory.FragmentedBytes} " +
                    $"working_set_before={workingSetBefore} working_set_after={processAfter.WorkingSet64} " +
                    $"private_before={privateBefore} private_after={processAfter.PrivateMemorySize64}");
            }
            catch (Exception ex)
            {
                failure = ex;
            }
            finally
            {
                lock (Gate)
                {
                    _reclaimActive = false;
                    _reclaimCompletion = null;
                    if (failure == null && _reclaimRequired)
                        RequestReclaimLocked(_reclaimReason);
                }
                if (failure == null)
                    completion.SetResult();
                else
                    completion.SetException(failure);
            }
        });
    }

    private static async Task<GCMemoryInfo> CollectGeneration2InBackgroundAsync()
    {
        long backgroundIndexBefore = GC.GetGCMemoryInfo(GCKind.Background).Index;
        long fullBlockingIndexBefore = GC.GetGCMemoryInfo(GCKind.FullBlocking).Index;
        GC.Collect(
            GC.MaxGeneration,
            GCCollectionMode.Forced,
            blocking: false,
            compacting: false);

        long deadline = Environment.TickCount64 + ReclaimCompletionTimeoutMilliseconds;
        while (true)
        {
            GCMemoryInfo background = GC.GetGCMemoryInfo(GCKind.Background);
            GCMemoryInfo fullBlocking = GC.GetGCMemoryInfo(GCKind.FullBlocking);
            if (background.Index > backgroundIndexBefore || fullBlocking.Index > fullBlockingIndexBefore)
            {
                return background.Index > fullBlocking.Index
                    ? background
                    : fullBlocking;
            }
            if (Environment.TickCount64 >= deadline)
            {
                throw new TimeoutException(
                    $"后台 Gen2 回收在 {ReclaimCompletionTimeoutMilliseconds} ms 内没有完成。");
            }
            await Task.Delay(25);
        }
    }

    private static void ExitLowLatencySearch(long allocatedBytesAtEntry)
    {
        lock (Gate)
        {
            long allocatedBytes = Math.Max(
                0,
                GC.GetTotalAllocatedBytes(precise: false) - allocatedBytesAtEntry);
            _largestSearchAllocatedBytes = Math.Max(_largestSearchAllocatedBytes, allocatedBytes);
            if (allocatedBytes >= BackgroundReclaimThresholdBytes)
                _reclaimRequired = true;
            if (--_activeSearches != 0)
                return;

            bool noGcRegionExhausted = _noGcRegionActive
                && GCSettings.LatencyMode != GCLatencyMode.NoGCRegion;
            if (noGcRegionExhausted)
            {
                _noGcRegionActive = false;
                _reclaimRequired = true;
                RestoreLatencyModeLocked();
                Entry.Logger.Warn(
                    "[CombatSolver/Test] GC_LATENCY no_gc_region_exhausted_before_search_exit=true " +
                    $"process_allocated_delta={Math.Max(0, GC.GetTotalAllocatedBytes(false) - _noGcRegionAllocatedBytesAtStart)} " +
                    "reclaim=background_non_compacting");
                RequestReclaimLocked("no_gc_region_exhausted");
                return;
            }

            if (_reclaimRequested)
            {
                StartReclaimLocked();
                return;
            }
            if (_noGcRegionActive)
            {
                Entry.Logger.Info(
                    "[CombatSolver/Test] GC_LATENCY no_gc_region_retained_until_combat_reset=true");
                return;
            }
            RestoreLatencyModeLocked();
            Entry.Logger.Info(
                $"[CombatSolver/Test] GC_LATENCY exit restored={GCSettings.LatencyMode} " +
                $"entry={_previousMode}");
        }
    }

    private static void ConfigureSearchMemoryLimit(
        SearchMemoryPressureSignal signal,
        long allocatedBytesAtEntry,
        long remainingRegionBytes,
        long regionBudgetBytes,
        long lohBudgetBytes)
    {
        long smallObjectBudgetBytes = Math.Max(1, regionBudgetBytes - lohBudgetBytes);
        long smallObjectLimitBytes = smallObjectBudgetBytes / 5 * 4;
        long remainingLimitBytes = Math.Max(1, remainingRegionBytes / 4 * 3);
        long allocationLimitBytes = Math.Max(1, Math.Min(smallObjectLimitBytes, remainingLimitBytes));
        signal.Configure(
            allocatedBytesAtEntry,
            allocationLimitBytes,
            cancellationToken => ReclaimWithinSearch(
                signal,
                regionBudgetBytes,
                lohBudgetBytes,
                cancellationToken));
        Entry.Logger.Info(
            $"[CombatSolver/Test] GC_SEARCH_ALLOCATION_LIMIT limit={allocationLimitBytes} " +
            $"remaining_region={remainingRegionBytes} region_budget={regionBudgetBytes} " +
            $"loh_budget={lohBudgetBytes}");
    }

    private static void ReclaimWithinSearch(
        SearchMemoryPressureSignal signal,
        long regionBudgetBytes,
        long lohBudgetBytes,
        CancellationToken cancellationToken)
    {
        TaskCompletionSource checkpointCompletion;
        bool endNoGcRegion;
        bool restoreLatencyMode;
        GCLatencyMode previousMode;
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            lock (Gate)
            {
                if (_activeSearches == 1 && !_reclaimActive && !_reclaimRequested)
                {
                    checkpointCompletion = new TaskCompletionSource(
                        TaskCreationOptions.RunContinuationsAsynchronously);
                    _reclaimActive = true;
                    _reclaimCompletion = checkpointCompletion;
                    _reclaimTask = checkpointCompletion.Task;
                    endNoGcRegion = _noGcRegionActive
                        && GCSettings.LatencyMode == GCLatencyMode.NoGCRegion;
                    restoreLatencyMode = _latencyModeOwned;
                    previousMode = _previousMode;
                    _noGcRegionActive = false;
                    _latencyModeOwned = false;
                    break;
                }
            }
            Thread.Sleep(ConcurrentSearchExitPollMilliseconds);
        }

        Exception? failure = null;
        NoGcRegionStartOutcome restartOutcome = NoGcRegionStartOutcome.InsufficientMemory;
        GCMemoryInfo completedCollection = default;
        long liveBefore = GC.GetTotalMemory(forceFullCollection: false);
        using Process processBefore = Process.GetCurrentProcess();
        long workingSetBefore = processBefore.WorkingSet64;
        long privateBefore = processBefore.PrivateMemorySize64;
        TimeSpan pauseBefore = GC.GetTotalPauseDuration();
        Stopwatch stopwatch = Stopwatch.StartNew();
        try
        {
            if (endNoGcRegion)
                GC.EndNoGCRegion();
            if (restoreLatencyMode)
                GCSettings.LatencyMode = previousMode;
            completedCollection = CollectGeneration2InBackgroundAsync().GetAwaiter().GetResult();
            cancellationToken.ThrowIfCancellationRequested();

            lock (Gate)
            {
                cancellationToken.ThrowIfCancellationRequested();
                _previousMode = GCSettings.LatencyMode;
                _latencyModeOwned = true;
                restartOutcome = TryStartNoGcRegion(
                    regionBudgetBytes,
                    lohBudgetBytes);
                _noGcRegionActive = restartOutcome == NoGcRegionStartOutcome.Started;
                _noGcRegionAllocatedBytesAtStart = GC.GetTotalAllocatedBytes(precise: false);
                _noGcRegionBudgetBytes = regionBudgetBytes;
                _noGcRegionLohBudgetBytes = lohBudgetBytes;
                if (!_noGcRegionActive)
                    GCSettings.LatencyMode = GCLatencyMode.SustainedLowLatency;
                ConfigureSearchMemoryLimit(
                    signal,
                    _noGcRegionAllocatedBytesAtStart,
                    regionBudgetBytes,
                    regionBudgetBytes,
                    lohBudgetBytes);
                _reclaimRequired = false;
            }
        }
        catch (Exception ex)
        {
            failure = ex;
        }
        finally
        {
            stopwatch.Stop();
            using Process processAfter = Process.GetCurrentProcess();
            processAfter.Refresh();
            Entry.Logger.Info(
                $"[CombatSolver/Test] HEAP_RECLAIM reason=in_search_memory_checkpoint " +
                $"mode=background_non_compacting no_gc_region_ended={endNoGcRegion} " +
                $"no_gc_region_restart={FormatStartOutcome(restartOutcome)} " +
                $"elapsed_ms={stopwatch.Elapsed.TotalMilliseconds:F1} " +
                $"gc_pause_delta_ms={(GC.GetTotalPauseDuration() - pauseBefore).TotalMilliseconds:F1} " +
                $"concurrent={completedCollection.Concurrent} compacted={completedCollection.Compacted} " +
                $"managed_live_before={liveBefore} managed_live_after={GC.GetTotalMemory(false)} " +
                $"working_set_before={workingSetBefore} working_set_after={processAfter.WorkingSet64} " +
                $"private_before={privateBefore} private_after={processAfter.PrivateMemorySize64}");
            lock (Gate)
            {
                _reclaimActive = false;
                _reclaimCompletion = null;
            }
            if (failure == null || failure is OperationCanceledException)
                checkpointCompletion.SetResult();
            else
                checkpointCompletion.SetException(failure);
        }

        if (failure != null)
            throw failure;
    }

    private static NoGcRegionStartOutcome TryStartNoGcRegion(
        long totalSize,
        long lohSize)
    {
        try
        {
            return GC.TryStartNoGCRegion(
                totalSize,
                lohSize,
                disallowFullBlockingGC: true)
                ? NoGcRegionStartOutcome.Started
                : NoGcRegionStartOutcome.InsufficientMemory;
        }
        catch (ArgumentOutOfRangeException exception) when (exception.ParamName == "totalSize")
        {
            // The maximum SOH reservation is runtime-specific and has no public query API.
            return NoGcRegionStartOutcome.RegionSizeUnsupported;
        }
    }

    private static string FormatStartOutcome(NoGcRegionStartOutcome outcome)
        => outcome switch
        {
            NoGcRegionStartOutcome.Started => "started",
            NoGcRegionStartOutcome.InsufficientMemory => "insufficient_memory",
            NoGcRegionStartOutcome.RegionSizeUnsupported => "region_size_unsupported",
            _ => throw new ArgumentOutOfRangeException(nameof(outcome)),
        };

    private static void RestoreLatencyModeLocked()
    {
        if (!_latencyModeOwned)
            return;
        GCSettings.LatencyMode = _previousMode;
        _latencyModeOwned = false;
    }

    private sealed class SearchScope(
        long allocatedBytesAtEntry,
        SearchMemoryPressureSignal memoryPressureSignal) : IDisposable
    {
        private bool _disposed;

        public void Dispose()
        {
            if (_disposed)
                return;
            _disposed = true;
            memoryPressureSignal.Disable();
            ExitLowLatencySearch(allocatedBytesAtEntry);
        }
    }
}
