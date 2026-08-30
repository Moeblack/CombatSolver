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
        if (!SolverOverlay.SearchCompletionNotificationSettingsConfiguredForTesting)
            throw new InvalidOperationException("搜索结束通知三态选项没有按持久化设置加载。");
        if (!SolverOverlay.SettingsTabsConfiguredForTesting
            || !SolverOverlay.ExerciseSettingsTabSwitchingForTesting())
        {
            throw new InvalidOperationException("设置页没有按常规、性能、反馈三页独立切换。");
        }
        if (!SolverOverlay.ExerciseSearchCompletionNotificationPolicyForTesting())
            throw new InvalidOperationException("搜索结束通知三态选项不能无损回读旧设置字段。");
        SolverSettingsData notificationDefaults = new();
        if (!notificationDefaults.SearchCompletionNotificationsEnabled
            || notificationDefaults.SearchCompletionNotificationMode
            != SolverSearchCompletionNotificationMode.OnlyWhenGameInBackground
            || SearchCompletionNotifier.ShouldNotifyForTesting(
                enabled: false,
                mode: SolverSearchCompletionNotificationMode.Always,
                gameForeground: false)
            || SearchCompletionNotifier.ShouldNotifyForTesting(
                enabled: true,
                mode: SolverSearchCompletionNotificationMode.OnlyWhenGameInBackground,
                gameForeground: true)
            || !SearchCompletionNotifier.ShouldNotifyForTesting(
                enabled: true,
                mode: SolverSearchCompletionNotificationMode.OnlyWhenGameInBackground,
                gameForeground: false)
            || !SearchCompletionNotifier.ShouldNotifyForTesting(
                enabled: true,
                mode: SolverSearchCompletionNotificationMode.Always,
                gameForeground: true))
        {
            throw new InvalidOperationException("搜索结束通知的默认值或前台判断不正确。");
        }
        SolverSettingsData originalNotificationSettings = SolverSettings.Current;
        try
        {
            SolverSettings.ApplyForTesting(originalNotificationSettings with
            {
                SearchCompletionNotificationsEnabled = true,
                SearchCompletionNotificationMode = SolverSearchCompletionNotificationMode.Always,
            });
            int requestsBefore = SearchCompletionNotifier.RequestCountForTesting;
            int nativeBefore = SearchCompletionNotifier.NativeNotificationCountForTesting;
            SearchCompletionNotifier.Notify(SearchCompletionNotificationKind.Succeeded);
            if (SearchCompletionNotifier.RequestCountForTesting != requestsBefore + 1
                || SearchCompletionNotifier.NativeNotificationCountForTesting != nativeBefore)
            {
                throw new InvalidOperationException("Headless 搜索结束通知没有停在原生平台调用之前。");
            }
        }
        finally
        {
            SolverSettings.ApplyForTesting(originalNotificationSettings);
        }
        if (!SolverOverlay.ExerciseUploadCompletionTransitionForTesting())
            throw new InvalidOperationException("上传任务结束前按钮状态提前切回空闲，可能重新打开确认弹窗。");
        if (!SolverOverlay.ExercisePerformancePresetPersistenceForTesting())
            throw new InvalidOperationException("性能预设在未修改算力参数时被误判为自定义。");
        if (SolverWeights.ResolveDefaultSearchMaxDegreeOfParallelism(1) != 1
            || SolverWeights.ResolveDefaultSearchMaxDegreeOfParallelism(2) != 2
            || SolverWeights.ResolveDefaultSearchMaxDegreeOfParallelism(3) != 2
            || SolverWeights.ResolveDefaultSearchMaxDegreeOfParallelism(4) != 4
            || SolverWeights.ResolveDefaultSearchMaxDegreeOfParallelism(32) != 4)
        {
            throw new InvalidOperationException("默认搜索并行度没有按逻辑处理器数量解析为 1/2/4。");
        }
        string parallelFailure = SolverController.FormatSearchFailureForTesting(
            new InvalidOperationException("parallel failure"),
            parallelSearchWasEnabled: true);
        string serialFailure = SolverController.FormatSearchFailureForTesting(
            new InvalidOperationException("serial failure"),
            parallelSearchWasEnabled: false);
        if (!parallelFailure.Contains("上传问题包", StringComparison.Ordinal)
            || !parallelFailure.Contains("关闭（单线程）", StringComparison.Ordinal)
            || !serialFailure.Contains("上传问题包", StringComparison.Ordinal)
            || serialFailure.Contains("关闭（单线程）", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("搜索失败提示没有按本次请求的并行状态提供恢复建议。");
        }

        int stopNotificationRequestsBefore = SearchCompletionNotifier.RequestCountForTesting;
        int stopNativeNotificationsBefore = SearchCompletionNotifier.NativeNotificationCountForTesting;
        SolverController.StopSearchByUser(host);
        if (SolverController.IsSearching
            || SolverController.IsDeploying
            || SolverController.FullAutoEnabled
            || !SolverController.AutomaticSearchPaused
            || SolverController.CurrentResultForBugReport != null)
        {
            throw new InvalidOperationException("用户停止搜索后仍残留活动会话、路线或自动计算状态。");
        }
        if (SearchCompletionNotifier.RequestCountForTesting != stopNotificationRequestsBefore + 1
            || SearchCompletionNotifier.NativeNotificationCountForTesting
            != stopNativeNotificationsBefore)
        {
            throw new InvalidOperationException("用户停止搜索后没有产生一次受 headless 保护的结束通知。");
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
            $"CombatSolver 版本：{CombatBugReportDescription.CurrentModVersion}",
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
