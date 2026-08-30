using HarmonyLib;
using MegaCrit.Sts2.Core.Models;
using STS2RitsuLib.Patching.Models;

namespace CombatSolver;

internal sealed class BaseLibCloneConcurrencyPatch : IPatchMethod
{
    private const string BaseLibClonePatchTypeName =
        "BaseLib.Utils.ICloneableField+CloneSpireFields";
    private static readonly object CloneGate = new();
    private static readonly Lazy<bool> BaseLibClonePatchLoaded = new(
        () => AppDomain.CurrentDomain.GetAssemblies().Any(
            assembly => assembly.GetType(BaseLibClonePatchTypeName, throwOnError: false) != null));

    public static string PatchId => "combat_solver_baselib_clone_concurrency";
    public static string Description => "求解并行克隆期间串行执行 BaseLib 的模型克隆扩展";

    public static ModPatchTarget[] GetTargets() =>
    [
        new(typeof(AbstractModel), nameof(AbstractModel.MutableClone), Type.EmptyTypes),
    ];

    [HarmonyPriority(Priority.First)]
    public static void Prefix(out bool __state)
    {
        __state = SimulationNotificationIsolation.IsActive && BaseLibClonePatchLoaded.Value;
        if (__state)
            Monitor.Enter(CloneGate);
    }

    [HarmonyPriority(Priority.Last)]
    public static Exception? Finalizer(Exception? __exception, bool __state)
    {
        if (__state)
            Monitor.Exit(CloneGate);
        return __exception;
    }
}
