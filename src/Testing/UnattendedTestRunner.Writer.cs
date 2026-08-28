using System.Diagnostics;
using System.Text.Json;
using MegaCrit.Sts2.Core.Nodes;

namespace CombatSolver;

internal sealed partial class UnattendedTestRunner
{
    private readonly record struct RuntimeMemorySnapshot(
        long ManagedHeapBytes,
        long ManagedFragmentedBytes,
        long WorkingSetBytes,
        long PrivateMemoryBytes);

    private sealed class Writer(
        UnattendedTestRequest request,
        Stopwatch stopwatch,
        IReadOnlyList<string> completedChecks)
    {
        public RuntimeMemorySnapshot Write(
            string status,
            string stage,
            string characterId,
            string encounterId,
            bool combatEnded,
            int startedTurn,
            int finishedTurn,
            string? error = null)
        {
            RuntimeMemorySnapshot memory = CaptureRuntimeMemory();
            WriteResult(new UnattendedTestResult
            {
                RunId = request.RunId,
                ScenarioId = request.ScenarioId,
                Status = status,
                Stage = stage,
                CharacterId = characterId,
                EncounterId = encounterId,
                Seed = request.Seed,
                ElapsedMilliseconds = stopwatch.Elapsed.TotalMilliseconds,
                MainThread = NGame.IsMainThread(),
                CombatEnded = combatEnded,
                StartedTurn = startedTurn,
                FinishedTurn = finishedTurn,
                ManagedHeapBytes = memory.ManagedHeapBytes,
                ManagedFragmentedBytes = memory.ManagedFragmentedBytes,
                WorkingSetBytes = memory.WorkingSetBytes,
                PrivateMemoryBytes = memory.PrivateMemoryBytes,
                CompletedChecks = completedChecks.ToArray(),
                Error = error,
            });
            return memory;
        }

        private static RuntimeMemorySnapshot CaptureRuntimeMemory()
        {
            GCMemoryInfo gc = GC.GetGCMemoryInfo();
            using Process process = Process.GetCurrentProcess();
            return new RuntimeMemorySnapshot(
                gc.HeapSizeBytes,
                gc.FragmentedBytes,
                process.WorkingSet64,
                process.PrivateMemorySize64);
        }

        private static void WriteResult(UnattendedTestResult result)
        {
            string resultPath = UnattendedTestFiles.GlobalPath(UnattendedTestFiles.ResultUri);
            string tempPath = resultPath + ".tmp";
            File.WriteAllText(tempPath, JsonSerializer.Serialize(result, UnattendedTestFiles.JsonOptions));
            File.Move(tempPath, resultPath, true);
        }
    }
}
