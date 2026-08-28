using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Nodes;
using STS2RitsuLib.Patching.Models;

namespace CombatSolver;

internal sealed class SolverInputPatch : IPatchMethod
{
    public static string PatchId => "combat_solver_input";
    public static string Description => "I 键异步搜索，O 键自动执行当前回合路线";

    public static ModPatchTarget[] GetTargets() =>
    [
        new(typeof(NGame), nameof(NGame._Input), [typeof(InputEvent)]),
    ];

    [HarmonyPriority(Priority.First)]
    public static void Prefix(NGame __instance, InputEvent inputEvent)
    {
        if (!Entry.Enabled || inputEvent is not InputEventKey
            {
                Pressed: true,
                Echo: false,
            } keyEvent || keyEvent.Keycode is not (Key.I or Key.O))
            return;

        __instance.GetViewport()?.SetInputAsHandled();
        CombatState? state = CombatManager.Instance.DebugOnlyGetState();
        if (state == null || !CombatManager.Instance.IsInProgress)
        {
            SolverController.Reset("input_without_combat");
            Entry.Logger.Info("[CombatSolver/Test] REJECT reason=no_active_combat");
            return;
        }

        if (keyEvent.Keycode == Key.I)
            SolverController.RequestSearch(__instance, state, SearchReason.Manual);
        else
            SolverController.RequestDeploy(__instance, state);
    }
}
