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

        SolverController.CancelSearchForTesting();
        if (SolverController.IsSearching
            || SolverController.IsDeploying
            || SolverController.FullAutoEnabled
            || SolverController.CurrentResultForBugReport != null)
        {
            throw new InvalidOperationException("取消搜索后仍残留活动会话或路线。");
        }

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
