using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Nodes;

namespace CombatSolver;

internal sealed partial class UnattendedTestRunner
{
    private async Task AssertControllerSessionLifecycleAsync(CombatState combat)
    {
        NGame host = NGame.Instance
            ?? throw new InvalidOperationException("控制器会话测试找不到 NGame。");
        if (SolverController.SolverDisabled)
            throw new InvalidOperationException("控制器会话测试要求求解器初始启用。");

        SolverController.RequestSearch(host, combat, SearchReason.Manual);
        if (!SolverController.IsSearching)
            throw new InvalidOperationException("控制器没有建立搜索会话。");
        if (SolverOverlay.ExecuteButtonTextForTesting != "停止计算")
            throw new InvalidOperationException("搜索期间执行按钮没有切换为停止计算。");
        if (!SolverOverlay.MessageWrappingEnabledForTesting)
            throw new InvalidOperationException("求解器消息区域没有启用自动换行。");

        SolverController.StopSearchByUser(host);
        if (SolverController.IsSearching
            || SolverController.IsDeploying
            || SolverController.FullAutoEnabled
            || !SolverController.AutomaticSearchPaused
            || SolverController.CurrentResultForBugReport != null)
        {
            throw new InvalidOperationException("用户停止搜索后仍残留活动会话、路线或自动计算状态。");
        }

        SolverController.RequestSearch(host, combat, SearchReason.AutoTurnStart);
        if (SolverController.IsSearching || !SolverController.AutomaticSearchPaused)
            throw new InvalidOperationException("用户停止后，自动回合入口重新启动了搜索。");

        SolverController.RecordManualProjectionComparisonForTesting(7, 3);
        SolverOverlay.RefreshControls();
        if (!SolverController.ManualRouteImprovementDetected
            || SolverController.LastManualProjectionComparisonForTesting?.Difference != -4
            || !SolverOverlay.ManualRouteImprovementVisibleForTesting)
        {
            throw new InvalidOperationException("手操降低预计战损后没有记录比较结果并显示绿色反馈提示。");
        }

        SolverController.RequestSearch(host, combat, SearchReason.Manual);
        if (!SolverController.IsSearching || SolverController.AutomaticSearchPaused)
            throw new InvalidOperationException("重新计算没有恢复当前及后续回合搜索。");
        SolverController.CancelSearchForTesting();

        await NextFrameAsync();
        await NextFrameAsync();
        if (SolverController.IsSearching
            || SolverController.CurrentResultForBugReport != null
            || SolverController.LastSearchFailureForTesting != null)
        {
            throw new InvalidOperationException("已取消搜索的回调重新写入了控制器状态。");
        }
    }
}
