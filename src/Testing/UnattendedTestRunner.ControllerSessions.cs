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
        if (!SolverOverlay.UploadProgressConfiguredForTesting)
            throw new InvalidOperationException("在线问题包上传没有配置可视化进度条和单实例按钮初始状态。");

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
        string liveDescription = SolverController.BuildBugReportDescription("玩家现场描述");
        if (!liveDescription.Contains("玩家现场描述", StringComparison.Ordinal)
            || !liveDescription.Contains("找到更优世界线", StringComparison.Ordinal)
            || !liveDescription.Contains("预计战损 7 → 3", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("在线问题描述没有读取本场手操改线信号。");
        }

        AssertBugReportAutomaticClassification();
        await AssertBugReportUploadBoundariesAsync();

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

    private static void AssertBugReportAutomaticClassification()
    {
        CombatBugReportIssueLedger issues = new();
        foreach (CombatBugReportIssueKind kind in Enum.GetValues<CombatBugReportIssueKind>())
            issues.Record(kind, "分类测试");
        CombatBugReportClassificationSnapshot snapshot = new(
            StateMismatchReplans: 1,
            DeploymentDriftReplans: 2,
            ContinuationMissingReplans: 3,
            PlanExhaustedReplans: 4,
            ManualDivergenceReplans: 5,
            issues.Snapshot());
        string description = CombatBugReportDescription.AppendAutomaticClassification(
            "玩家填写的问题描述",
            snapshot);
        string[] expectedClassifications =
        [
            "玩家填写的问题描述",
            "【CombatSolver 自动分类】",
            "计划外重算：3 次（状态不一致 1，执行漂移 2）",
            "续接路线缺失后重算：3 次",
            "本回合路线耗尽后重算：4 次",
            "手操偏离原路线后重算：5 次",
            "找到更优世界线",
            "手操后预计战损上升",
            "重算后预计战损上升",
            "搜索初始化失败",
            "第三方 Mod 不兼容",
            "计算失败",
            "搜索动作回放失败",
            "搜索内存或容量错误",
            "药水策略未满足",
            "计算期间状态变化，过期结果已丢弃",
            "自动执行中止",
            "回合准备选牌失败",
            "回合准备计划与实机状态不一致",
            "未计划的选牌",
            "选牌页面执行失败",
            "遗物标注回放与选中状态不一致",
            "等待游戏状态超时",
            "存在尚未支持的战斗语义",
            "全自动因重算后战损上升而暂停",
            "全自动因预计本回合死亡而暂停",
            "全自动因结束回合实机复核将死亡而暂停",
            "全自动因结束回合实机复核战损上升而暂停",
        ];
        foreach (string expected in expectedClassifications)
        {
            if (!description.Contains(expected, StringComparison.Ordinal))
                throw new InvalidOperationException($"在线问题描述缺少自动分类：{expected}。");
        }
    }
}
