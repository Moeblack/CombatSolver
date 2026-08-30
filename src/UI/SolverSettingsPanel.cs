using System.Globalization;
using Godot;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Localization.Fonts;

namespace CombatSolver;

internal sealed partial class SolverSettingsPanel : PanelContainer
{
    private readonly CheckButton _solverDisabled;
    private readonly CheckButton _stopOnCombatEnd;
    private readonly CheckButton _stopOnDeathTurn;
    private readonly CheckButton _stopOnWorseRecalculation;
    private readonly CheckButton _detailedDiagnosticLogs;
    private readonly CheckButton _searchCompletionNotificationsEnabled;
    private readonly OptionButton _searchCompletionNotificationMode;
    private readonly OptionButton _potionPolicy;
    private readonly OptionButton _performancePreset;
    private readonly Button _exportBugReport;
    private readonly Button _uploadBugReport;
    private readonly ProgressBar _uploadProgress;
    private readonly Label _status;
    private readonly List<Action<SolverSettingsData>> _reloadInputs = [];
    private readonly List<Func<bool>> _commitInputs = [];
    private BugReportUploadDialog? _uploadDialog;
    private CancellationTokenSource? _uploadCancellation;
    private bool _loading;
    private volatile bool _exportInProgress;
    private volatile bool _uploadInProgress;
    private volatile bool _uploadCancelRequested;
    private long _uploadBytesSent;
    private long _uploadTotalBytes;
    private int _lastRenderedUploadPercentage = -1;
    private string? _uploadSubmissionId;
    private UploadCompletion? _uploadCompletion;

    public event Action? ResetPositionRequested;

    internal bool UploadProgressConfiguredForTesting
        => !_uploadProgress.Visible
           && _uploadProgress.MinValue == 0
           && _uploadProgress.MaxValue == 100
           && !_uploadProgress.ShowPercentage
           && MapUploadProgressBarValue(100) == 95
           && FormatUploadProgressStatus(1024, 1024, 100).Contains(
               "等待服务器确认",
               StringComparison.Ordinal)
           && _uploadBugReport.Text == "上传问题包";
    internal bool SearchCompletionNotificationSettingsConfiguredForTesting
        => _searchCompletionNotificationsEnabled.ButtonPressed
           == SolverSettings.Current.SearchCompletionNotificationsEnabled
           && _searchCompletionNotificationMode.GetItemId(
               _searchCompletionNotificationMode.Selected)
           == (int)SolverSettings.Current.SearchCompletionNotificationMode;

    public SolverSettingsPanel()
    {
        Name = "SolverSettingsPanel";
        MouseFilter = MouseFilterEnum.Pass;
        SizeFlagsHorizontal = SizeFlags.ExpandFill;
        AddThemeStyleboxOverride("panel", SolverUiTokens.CreateBox(
            SolverUiTokens.Palette.Surface,
            SolverUiTokens.Palette.BorderSubtle,
            SolverUiTokens.Radius.Medium,
            SolverUiTokens.Spacing.Md,
            SolverUiTokens.Spacing.Md));

        VBoxContainer root = new()
        {
            MouseFilter = MouseFilterEnum.Pass,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        };
        root.AddThemeConstantOverride("separation", SolverUiTokens.Spacing.Sm);

        HBoxContainer heading = new()
        {
            MouseFilter = MouseFilterEnum.Pass,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        };
        Label title = SolverUiTokens.CreateLabel(
            "求解器设置",
            SolverUiTokens.Type.Title,
            SolverUiTokens.Palette.TextPrimary,
            FontType.Bold);
        title.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        heading.AddChild(title);
        Button reset = SolverUiTokens.CreateButton("恢复默认", SolverButtonStyle.Secondary);
        reset.CustomMinimumSize = new Vector2(96, SolverUiTokens.Size.ButtonHeight);
        reset.Pressed += ResetDefaults;
        heading.AddChild(reset);
        root.AddChild(heading);

        ScrollContainer scroll = new()
        {
            CustomMinimumSize = new Vector2(0, 310),
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ShrinkBegin,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
            VerticalScrollMode = ScrollContainer.ScrollMode.Auto,
            MouseFilter = MouseFilterEnum.Pass,
        };
        VBoxContainer settingsContent = new()
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            MouseFilter = MouseFilterEnum.Pass,
        };
        settingsContent.AddThemeConstantOverride("separation", SolverUiTokens.Spacing.Md);

        settingsContent.AddChild(CreateSectionHeading("基础与执行设置"));
        GridContainer basicGrid = new()
        {
            Columns = 2,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            MouseFilter = MouseFilterEnum.Pass,
        };
        basicGrid.AddThemeConstantOverride("h_separation", SolverUiTokens.Spacing.Lg);
        basicGrid.AddThemeConstantOverride("v_separation", SolverUiTokens.Spacing.Sm);
        _solverDisabled = CreateToggle();
        _solverDisabled.Toggled += OnSolverDisabledToggled;
        AddBasicRow(basicGrid, "禁用求解器", _solverDisabled);
        _stopOnCombatEnd = CreateToggle();
        _stopOnCombatEnd.Toggled += OnStopOnCombatEndToggled;
        AddBasicRow(basicGrid, "全自动在预计结束战斗时暂停", _stopOnCombatEnd);
        _stopOnDeathTurn = CreateToggle();
        _stopOnDeathTurn.Toggled += OnStopOnDeathTurnToggled;
        AddBasicRow(basicGrid, "死亡回合时暂停", _stopOnDeathTurn);
        _stopOnWorseRecalculation = CreateToggle();
        _stopOnWorseRecalculation.Toggled += OnStopOnWorseRecalculationToggled;
        AddBasicRow(basicGrid, "重算后预计战损增加时暂停", _stopOnWorseRecalculation);
        _detailedDiagnosticLogs = CreateToggle();
        _detailedDiagnosticLogs.Toggled += OnDetailedDiagnosticLogsToggled;
        AddBasicRow(basicGrid, "详细诊断日志", _detailedDiagnosticLogs);
        _searchCompletionNotificationsEnabled = CreateToggle();
        _searchCompletionNotificationsEnabled.Toggled += OnSearchCompletionNotificationsToggled;
        AddBasicRow(
            basicGrid,
            "搜索结束通知",
            _searchCompletionNotificationsEnabled,
            "搜索成功、失败、停止或结果过期时发送系统通知。Windows 使用原生通知及系统提示音；其他平台不会调用 Windows 通知接口。");
        _searchCompletionNotificationMode = CreateSearchCompletionNotificationModeInput();
        AddBasicRow(
            basicGrid,
            "通知时机",
            _searchCompletionNotificationMode,
            "“仅游戏不在前台”适合切到其他窗口等待计算；“始终通知”在游戏位于前台时也会显示并播放系统通知音。默认仅在游戏不在前台时通知。");
        _potionPolicy = CreatePotionPolicyInput();
        AddBasicRow(basicGrid, "本场药水策略", _potionPolicy);
        _performancePreset = CreatePerformancePresetInput();
        AddBasicRow(basicGrid, "性能预设", _performancePreset);
        AddBasicRow(
            basicGrid,
            "搜索并行度",
            CreateSearchParallelismInput(),
            "关闭时使用纯单线程搜索；2–8 表示同时展开的候选数量，实际不会超过可用逻辑处理器。提高可能加快大型搜索，也会增加 CPU、峰值内存和帧率压力。默认按可用逻辑处理器自动选择 4、2 或单线程；遇到疑似并行问题时请先上传问题包，再切换为关闭。");
        AddBasicRow(basicGrid, "自动出牌速度", CreateDeploymentFastModeInput());
        AddBasicRow(basicGrid, "牌间额外停顿（秒）", CreateDoubleInput(
            0d,
            data => data.DeploymentInterActionDelaySeconds,
            (data, value) => data with { DeploymentInterActionDelaySeconds = value },
            0d,
            3d));
        AddBasicRow(basicGrid, "No-GC 预算（GB）", CreateDoubleInput(
            SolverSettings.DefaultNoGcRegionBudgetGigabytes,
            data => data.NoGcRegionBudgetGigabytes,
            (data, value) => AsCustomPerformance(data with { NoGcRegionBudgetGigabytes = value }),
            1d,
            16d),
            "搜索期间为延迟垃圾回收预留的内存预算。提高后可减少长搜索中的回收与卡顿，但需要更多可用内存；超过机器余量可能触发系统换页。");
        HBoxContainer bugReportRow = new()
        {
            MouseFilter = MouseFilterEnum.Pass,
        };
        bugReportRow.AddThemeConstantOverride("separation", SolverUiTokens.Spacing.Sm);
        _uploadBugReport = SolverUiTokens.CreateButton("上传问题包", SolverButtonStyle.Secondary);
        _uploadBugReport.CustomMinimumSize = new Vector2(126, SolverUiTokens.Size.ButtonHeight);
        _uploadBugReport.Pressed += OnUploadBugReportPressed;
        bugReportRow.AddChild(_uploadBugReport);
        _exportBugReport = SolverUiTokens.CreateButton("导出问题包", SolverButtonStyle.Secondary);
        _exportBugReport.CustomMinimumSize = new Vector2(126, SolverUiTokens.Size.ButtonHeight);
        _exportBugReport.Pressed += OnExportBugReportPressed;
        bugReportRow.AddChild(_exportBugReport);
        AddBasicRow(basicGrid, "问题反馈", bugReportRow);
        AddBasicRow(
            basicGrid,
            "反馈联系QQ（选填）",
            CreateContactQqInput(),
            "上传问题包时随附，方便开发者回访；只需填一次，弹窗里会自动带上。");
        settingsContent.AddChild(basicGrid);

        settingsContent.AddChild(CreateSectionHeading("搜索策略配置"));
        GridContainer searchGrid = new()
        {
            Columns = 3,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            MouseFilter = MouseFilterEnum.Pass,
        };
        searchGrid.AddThemeConstantOverride("h_separation", SolverUiTokens.Spacing.Md);
        searchGrid.AddThemeConstantOverride("v_separation", SolverUiTokens.Spacing.Sm);
        AddGridHeader(searchGrid, "配置项");
        AddGridHeader(searchGrid, "快搜");
        AddGridHeader(searchGrid, "深搜");

        AddDoubleRow(
            searchGrid,
            "时间上限（秒）",
            SolverSearchProfile.Short.SoftTimeBudgetMilliseconds / 1000d,
            data => data.ShortTimeLimitSeconds,
            (data, value) => AsCustomPerformance(data with { ShortTimeLimitSeconds = value }),
            SolverSearchProfile.Deep.SoftTimeBudgetMilliseconds / 1000d,
            data => data.DeepTimeLimitSeconds,
            (data, value) => AsCustomPerformance(data with { DeepTimeLimitSeconds = value }),
            0.1d,
            600d,
            "搜索达到该时间后停止当前阶段。提高后可搜索更久，可能找到更好路线，也会更晚显示结果；快搜负责先给结果，深搜负责继续优化。");
        AddIntRow(searchGrid, "Beam 宽度", SolverSearchProfile.Short.BeamWidth,
            data => data.ShortBeamWidth,
            (data, value) => AsCustomPerformance(data with { ShortBeamWidth = value }),
            SolverSearchProfile.Deep.BeamWidth,
            data => data.DeepBeamWidth,
            (data, value) => AsCustomPerformance(data with { DeepBeamWidth = value }), 1, 512,
            "每层保留的候选路线数量。提高后更不容易过早淘汰好路线，但会明显增加计算量和内存占用。");
        AddIntRow(searchGrid, "节点上限", SolverSearchProfile.Short.MaxExpandedNodes,
            data => data.ShortMaxExpandedNodes,
            (data, value) => AsCustomPerformance(data with { ShortMaxExpandedNodes = value }),
            SolverSearchProfile.Deep.MaxExpandedNodes,
            data => data.DeepMaxExpandedNodes,
            (data, value) => AsCustomPerformance(data with { DeepMaxExpandedNodes = value }), 100, 100_000,
            "单次搜索最多展开的状态数量。提高后搜索范围更大，也会增加耗时和内存占用。");
        AddIntRow(searchGrid, "单节点出牌分支", SolverSearchProfile.Short.MaxCardBranchesPerNode,
            data => data.ShortMaxCardBranchesPerNode,
            (data, value) => AsCustomPerformance(data with { ShortMaxCardBranchesPerNode = value }),
            SolverSearchProfile.Deep.MaxCardBranchesPerNode,
            data => data.DeepMaxCardBranchesPerNode,
            (data, value) => AsCustomPerformance(data with { DeepMaxCardBranchesPerNode = value }), 1, 100,
            "每个状态最多继续尝试的出牌动作数量。提高后能覆盖更多出牌顺序，但会放大后续搜索量。");
        settingsContent.AddChild(searchGrid);
        scroll.AddChild(settingsContent);
        root.AddChild(scroll);

        Label hint = SolverUiTokens.CreateLabel(
            "选择预设会成套填充；修改任一算力数值自动切到自定义。留空使用灰色中档默认值。",
            SolverUiTokens.Type.Caption,
            SolverUiTokens.Palette.TextMuted);
        root.AddChild(hint);
        _uploadProgress = new ProgressBar
        {
            Visible = false,
            MinValue = 0,
            MaxValue = 100,
            Value = 0,
            ShowPercentage = false,
            CustomMinimumSize = new Vector2(0, 18),
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        };
        root.AddChild(_uploadProgress);
        _status = SolverUiTokens.CreateLabel(
            string.Empty,
            SolverUiTokens.Type.Caption,
            SolverUiTokens.Palette.Success,
            FontType.Bold);
        _status.CustomMinimumSize = new Vector2(0, 20);
        root.AddChild(_status);
        AddChild(root);
        SetProcess(false);
        Reload();
    }

    public override void _Process(double delta)
    {
        if (!_uploadInProgress)
            return;
        if (TryApplyUploadCompletion())
            return;
        if (_uploadCancelRequested)
            return;
        long total = Interlocked.Read(ref _uploadTotalBytes);
        long sent = Interlocked.Read(ref _uploadBytesSent);
        if (total <= 0)
            return;
        int percentage = (int)Math.Clamp(sent * 100L / total, 0L, 100L);
        if (percentage == _lastRenderedUploadPercentage)
            return;
        _lastRenderedUploadPercentage = percentage;
        _uploadProgress.Value = MapUploadProgressBarValue(percentage);
        _status.Text = FormatUploadProgressStatus(sent, total, percentage);
    }

    public override void _ExitTree()
    {
        _uploadCancellation?.Cancel();
        if (_uploadDialog != null && GodotObject.IsInstanceValid(_uploadDialog))
            _uploadDialog.QueueFree();
    }

    public void Reload()
    {
        _loading = true;
        SolverSettingsData data = SolverSettings.Current;
        _solverDisabled.ButtonPressed = data.SolverDisabled;
        _stopOnCombatEnd.ButtonPressed = data.StopFullAutoOnCombatEnd;
        _stopOnDeathTurn.ButtonPressed = data.StopFullAutoOnDeathTurn;
        _stopOnWorseRecalculation.ButtonPressed = data.StopFullAutoOnWorseRecalculation;
        _detailedDiagnosticLogs.ButtonPressed = data.EnableDetailedDiagnosticLogs;
        _searchCompletionNotificationsEnabled.ButtonPressed = data.SearchCompletionNotificationsEnabled;
        _searchCompletionNotificationMode.Selected = (int)data.SearchCompletionNotificationMode;
        _potionPolicy.Selected = (int)data.PotionPolicy;
        _performancePreset.Selected = (int)SolverSettings.ResolvePerformancePreset(data);
        foreach (Action<SolverSettingsData> reload in _reloadInputs)
            reload(data);
        _status.Text = string.Empty;
        _loading = false;
    }

    public bool CommitPending()
    {
        foreach (Func<bool> commit in _commitInputs)
        {
            if (!commit())
                return false;
        }
        return true;
    }

    internal bool ExercisePerformancePresetPersistenceForTesting()
    {
        SolverSettingsData original = SolverSettings.Current;
        try
        {
            SolverSettingsData preset = SolverSettings.ApplyPerformancePreset(
                original,
                SolverPerformancePreset.High);
            SolverSettings.ApplyForTesting(preset);
            Reload();
            return CommitPending()
                   && SolverSettings.ResolvePerformancePreset(SolverSettings.Current)
                   == SolverPerformancePreset.High;
        }
        finally
        {
            SolverSettings.ApplyForTesting(original);
            Reload();
        }
    }

    private void AddIntRow(
        GridContainer grid,
        string label,
        int shortDefault,
        Func<SolverSettingsData, int?> getShort,
        Func<SolverSettingsData, int?, SolverSettingsData> setShort,
        int deepDefault,
        Func<SolverSettingsData, int?> getDeep,
        Func<SolverSettingsData, int?, SolverSettingsData> setDeep,
        int minimum,
        int maximum,
        string tooltip)
    {
        Label rowLabel = CreateRowLabel(label);
        LineEdit shortInput = CreateIntInput(shortDefault, getShort, setShort, minimum, maximum);
        LineEdit deepInput = CreateIntInput(deepDefault, getDeep, setDeep, minimum, maximum);
        ApplyTooltip(rowLabel, tooltip);
        ApplyTooltip(shortInput, tooltip);
        ApplyTooltip(deepInput, tooltip);
        grid.AddChild(rowLabel);
        grid.AddChild(shortInput);
        grid.AddChild(deepInput);
    }

    private void AddDoubleRow(
        GridContainer grid,
        string label,
        double shortDefault,
        Func<SolverSettingsData, double?> getShort,
        Func<SolverSettingsData, double?, SolverSettingsData> setShort,
        double deepDefault,
        Func<SolverSettingsData, double?> getDeep,
        Func<SolverSettingsData, double?, SolverSettingsData> setDeep,
        double minimum,
        double maximum,
        string tooltip)
    {
        Label rowLabel = CreateRowLabel(label);
        LineEdit shortInput = CreateDoubleInput(shortDefault, getShort, setShort, minimum, maximum);
        LineEdit deepInput = CreateDoubleInput(deepDefault, getDeep, setDeep, minimum, maximum);
        ApplyTooltip(rowLabel, tooltip);
        ApplyTooltip(shortInput, tooltip);
        ApplyTooltip(deepInput, tooltip);
        grid.AddChild(rowLabel);
        grid.AddChild(shortInput);
        grid.AddChild(deepInput);
    }

    private OptionButton CreateDeploymentFastModeInput()
    {
        OptionButton input = new()
        {
            FocusMode = FocusModeEnum.None,
            CustomMinimumSize = new Vector2(126, 32),
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            MouseDefaultCursorShape = CursorShape.PointingHand,
        };
        input.AddItem("跟随游戏（默认）", (int)SolverDeploymentFastMode.FollowGame);
        input.AddItem("正常", (int)SolverDeploymentFastMode.Normal);
        input.AddItem("快速", (int)SolverDeploymentFastMode.Fast);
        input.AddItem("瞬间", (int)SolverDeploymentFastMode.Instant);
        input.AddThemeFontSizeOverride("font_size", SolverUiTokens.Type.Body);
        input.AddThemeColorOverride("font_color", SolverUiTokens.Palette.TextPrimary);
        input.AddThemeStyleboxOverride("normal", SolverUiTokens.CreateBox(
            SolverUiTokens.Palette.Background,
            SolverUiTokens.Palette.BorderSubtle,
            SolverUiTokens.Radius.Small,
            SolverUiTokens.Spacing.Sm,
            SolverUiTokens.Spacing.Xs));
        input.AddThemeStyleboxOverride("hover", SolverUiTokens.CreateBox(
            SolverUiTokens.Palette.SurfaceRaised,
            SolverUiTokens.Palette.Accent,
            SolverUiTokens.Radius.Small,
            SolverUiTokens.Spacing.Sm,
            SolverUiTokens.Spacing.Xs));
        SolverUiTokens.ApplyTextOutline(input);
        input.ApplyLocaleFontSubstitution(FontType.Regular, "font");
        _reloadInputs.Add(data => input.Selected = (int)data.DeploymentFastMode);
        input.ItemSelected += index =>
        {
            if (_loading)
                return;
            SolverDeploymentFastMode mode = (SolverDeploymentFastMode)input.GetItemId((int)index);
            SolverSettings.Update(SolverSettings.Current with { DeploymentFastMode = mode });
            _status.AddThemeColorOverride("font_color", SolverUiTokens.Palette.Success);
            _status.Text = "已保存，下次执行生效";
        };
        return input;
    }

    private OptionButton CreateSearchCompletionNotificationModeInput()
    {
        OptionButton input = new()
        {
            FocusMode = FocusModeEnum.None,
            CustomMinimumSize = new Vector2(126, 32),
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            MouseDefaultCursorShape = CursorShape.PointingHand,
        };
        input.AddItem(
            "仅游戏不在前台（默认）",
            (int)SolverSearchCompletionNotificationMode.OnlyWhenGameInBackground);
        input.AddItem("始终通知", (int)SolverSearchCompletionNotificationMode.Always);
        input.AddThemeFontSizeOverride("font_size", SolverUiTokens.Type.Body);
        input.AddThemeColorOverride("font_color", SolverUiTokens.Palette.TextPrimary);
        input.AddThemeStyleboxOverride("normal", SolverUiTokens.CreateBox(
            SolverUiTokens.Palette.Background,
            SolverUiTokens.Palette.BorderSubtle,
            SolverUiTokens.Radius.Small,
            SolverUiTokens.Spacing.Sm,
            SolverUiTokens.Spacing.Xs));
        input.AddThemeStyleboxOverride("hover", SolverUiTokens.CreateBox(
            SolverUiTokens.Palette.SurfaceRaised,
            SolverUiTokens.Palette.Accent,
            SolverUiTokens.Radius.Small,
            SolverUiTokens.Spacing.Sm,
            SolverUiTokens.Spacing.Xs));
        SolverUiTokens.ApplyTextOutline(input);
        input.ApplyLocaleFontSubstitution(FontType.Regular, "font");
        input.ItemSelected += index =>
        {
            if (_loading)
                return;
            SolverSearchCompletionNotificationMode mode =
                (SolverSearchCompletionNotificationMode)input.GetItemId((int)index);
            SolverSettings.Update(SolverSettings.Current with
            {
                SearchCompletionNotificationMode = mode,
            });
            _status.AddThemeColorOverride("font_color", SolverUiTokens.Palette.Success);
            _status.Text = "已保存并立即生效";
        };
        return input;
    }

    private OptionButton CreatePotionPolicyInput()
    {
        OptionButton input = new()
        {
            FocusMode = FocusModeEnum.None,
            CustomMinimumSize = new Vector2(126, 32),
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            MouseDefaultCursorShape = CursorShape.PointingHand,
        };
        input.AddItem("禁用", (int)SolverPotionPolicy.Disabled);
        input.AddItem("智能（默认）", (int)SolverPotionPolicy.Smart);
        input.AddItem("至少用一瓶", (int)SolverPotionPolicy.RequireAtLeastOne);
        input.AddThemeFontSizeOverride("font_size", SolverUiTokens.Type.Body);
        input.AddThemeColorOverride("font_color", SolverUiTokens.Palette.TextPrimary);
        input.AddThemeStyleboxOverride("normal", SolverUiTokens.CreateBox(
            SolverUiTokens.Palette.Background,
            SolverUiTokens.Palette.BorderSubtle,
            SolverUiTokens.Radius.Small,
            SolverUiTokens.Spacing.Sm,
            SolverUiTokens.Spacing.Xs));
        input.AddThemeStyleboxOverride("hover", SolverUiTokens.CreateBox(
            SolverUiTokens.Palette.SurfaceRaised,
            SolverUiTokens.Palette.Accent,
            SolverUiTokens.Radius.Small,
            SolverUiTokens.Spacing.Sm,
            SolverUiTokens.Spacing.Xs));
        SolverUiTokens.ApplyTextOutline(input);
        input.ApplyLocaleFontSubstitution(FontType.Regular, "font");
        _reloadInputs.Add(data => input.Selected = (int)data.PotionPolicy);
        input.ItemSelected += index =>
        {
            if (_loading)
                return;
            SolverPotionPolicy policy = (SolverPotionPolicy)input.GetItemId((int)index);
            SolverSettings.Update(SolverSettings.Current with { PotionPolicy = policy });
            _status.AddThemeColorOverride("font_color", SolverUiTokens.Palette.Success);
            _status.Text = "已保存，下次搜索生效";
        };
        return input;
    }

    private OptionButton CreateSearchParallelismInput()
    {
        OptionButton input = new()
        {
            FocusMode = FocusModeEnum.None,
            CustomMinimumSize = new Vector2(126, 32),
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            MouseDefaultCursorShape = CursorShape.PointingHand,
        };
        input.AddItem("关闭（单线程）", 1);
        for (int degree = 2; degree <= SolverWeights.MaximumSearchMaxDegreeOfParallelism; degree++)
            input.AddItem(degree.ToString(CultureInfo.InvariantCulture), degree);
        input.AddThemeFontSizeOverride("font_size", SolverUiTokens.Type.Body);
        input.AddThemeColorOverride("font_color", SolverUiTokens.Palette.TextPrimary);
        input.AddThemeStyleboxOverride("normal", SolverUiTokens.CreateBox(
            SolverUiTokens.Palette.Background,
            SolverUiTokens.Palette.BorderSubtle,
            SolverUiTokens.Radius.Small,
            SolverUiTokens.Spacing.Sm,
            SolverUiTokens.Spacing.Xs));
        input.AddThemeStyleboxOverride("hover", SolverUiTokens.CreateBox(
            SolverUiTokens.Palette.SurfaceRaised,
            SolverUiTokens.Palette.Accent,
            SolverUiTokens.Radius.Small,
            SolverUiTokens.Spacing.Sm,
            SolverUiTokens.Spacing.Xs));
        SolverUiTokens.ApplyTextOutline(input);
        input.ApplyLocaleFontSubstitution(FontType.Regular, "font");
        _reloadInputs.Add(data =>
        {
            int degree = data.SearchMaxDegreeOfParallelism
                ?? SolverWeights.DefaultSearchMaxDegreeOfParallelism;
            input.Selected = input.GetItemIndex(degree);
        });
        input.ItemSelected += index =>
        {
            if (_loading)
                return;
            int degree = input.GetItemId((int)index);
            SolverSettings.Update(SolverSettings.Current with
            {
                SearchMaxDegreeOfParallelism = degree,
            });
            _status.AddThemeColorOverride("font_color", SolverUiTokens.Palette.Success);
            _status.Text = degree == 1
                ? "并行搜索已关闭，下次搜索使用单线程"
                : $"搜索并行度已设为 {degree}，下次搜索生效";
        };
        return input;
    }

    private OptionButton CreatePerformancePresetInput()
    {
        OptionButton input = new()
        {
            FocusMode = FocusModeEnum.None,
            CustomMinimumSize = new Vector2(260, 32),
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            MouseDefaultCursorShape = CursorShape.PointingHand,
        };
        input.AddItem("低档（5 / 60 秒，6 GB）", (int)SolverPerformancePreset.Low);
        input.AddItem("中档（8 / 120 秒，8 GB）", (int)SolverPerformancePreset.Medium);
        input.AddItem("高档（12 / 180 秒，12 GB）", (int)SolverPerformancePreset.High);
        input.AddItem("极高（20 / 300 秒，16 GB）", (int)SolverPerformancePreset.VeryHigh);
        input.AddItem("自定义", (int)SolverPerformancePreset.Custom);
        input.AddThemeFontSizeOverride("font_size", SolverUiTokens.Type.Body);
        input.AddThemeColorOverride("font_color", SolverUiTokens.Palette.TextPrimary);
        input.AddThemeStyleboxOverride("normal", SolverUiTokens.CreateBox(
            SolverUiTokens.Palette.Background,
            SolverUiTokens.Palette.BorderSubtle,
            SolverUiTokens.Radius.Small,
            SolverUiTokens.Spacing.Sm,
            SolverUiTokens.Spacing.Xs));
        input.AddThemeStyleboxOverride("hover", SolverUiTokens.CreateBox(
            SolverUiTokens.Palette.SurfaceRaised,
            SolverUiTokens.Palette.Accent,
            SolverUiTokens.Radius.Small,
            SolverUiTokens.Spacing.Sm,
            SolverUiTokens.Spacing.Xs));
        SolverUiTokens.ApplyTextOutline(input);
        input.ApplyLocaleFontSubstitution(FontType.Regular, "font");
        input.ItemSelected += index =>
        {
            if (_loading)
                return;
            SolverPerformancePreset preset = (SolverPerformancePreset)input.GetItemId((int)index);
            SolverSettings.Update(SolverSettings.ApplyPerformancePreset(SolverSettings.Current, preset));
            Reload();
            _status.AddThemeColorOverride("font_color", SolverUiTokens.Palette.Success);
            _status.Text = "性能预设已保存，下次搜索生效";
        };
        return input;
    }

    private LineEdit CreateIntInput(
        int defaultValue,
        Func<SolverSettingsData, int?> getter,
        Func<SolverSettingsData, int?, SolverSettingsData> setter,
        int minimum,
        int maximum)
    {
        LineEdit input = CreateInput(defaultValue.ToString(CultureInfo.InvariantCulture));
        _reloadInputs.Add(data => input.Text = getter(data)?.ToString(CultureInfo.InvariantCulture) ?? string.Empty);
        bool Commit()
        {
            string text = input.Text.Trim();
            if (text.Length == 0)
            {
                if (getter(SolverSettings.Current) == null)
                    return KeepUnchanged(input);
                return Save(input, setter(SolverSettings.Current, null));
            }
            if (!int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int value)
                || value < minimum || value > maximum)
            {
                ShowInvalid(input, $"请输入 {minimum}–{maximum} 的整数");
                return false;
            }
            if (getter(SolverSettings.Current) == value)
                return KeepUnchanged(input);
            return Save(input, setter(SolverSettings.Current, value));
        }
        input.FocusExited += () => Commit();
        input.TextSubmitted += _ => Commit();
        _commitInputs.Add(Commit);
        return input;
    }

    private LineEdit CreateDoubleInput(
        double defaultValue,
        Func<SolverSettingsData, double?> getter,
        Func<SolverSettingsData, double?, SolverSettingsData> setter,
        double minimum,
        double maximum)
    {
        LineEdit input = CreateInput(SolverSettings.FormatSeconds(defaultValue));
        _reloadInputs.Add(data => input.Text = getter(data) is { } value
            ? SolverSettings.FormatSeconds(value)
            : string.Empty);
        bool Commit()
        {
            string text = input.Text.Trim();
            if (text.Length == 0)
            {
                if (getter(SolverSettings.Current) == null)
                    return KeepUnchanged(input);
                return Save(input, setter(SolverSettings.Current, null));
            }
            if (!double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out double value)
                || value < minimum || value > maximum)
            {
                ShowInvalid(input, $"请输入 {minimum:0.###}–{maximum:0.###} 的数字");
                return false;
            }
            if (getter(SolverSettings.Current) is { } current && current.Equals(value))
                return KeepUnchanged(input);
            return Save(input, setter(SolverSettings.Current, value));
        }
        input.FocusExited += () => Commit();
        input.TextSubmitted += _ => Commit();
        _commitInputs.Add(Commit);
        return input;
    }

    private LineEdit CreateContactQqInput()
    {
        LineEdit input = CreateInput("未设置");
        _reloadInputs.Add(data => input.Text = data.ReporterContactQq ?? string.Empty);
        bool Commit()
        {
            string text = input.Text.Trim();
            if (text.Length > 64)
            {
                ShowInvalid(input, "联系QQ最长 64 个字符");
                return false;
            }
            return Save(input, SolverSettings.Current with
            {
                ReporterContactQq = text.Length == 0 ? null : text,
            });
        }
        input.FocusExited += () => Commit();
        input.TextSubmitted += _ => Commit();
        _commitInputs.Add(Commit);
        return input;
    }

    private static LineEdit CreateInput(string defaultText)
    {
        LineEdit input = new()
        {
            PlaceholderText = defaultText,
            ClearButtonEnabled = true,
            SelectAllOnFocus = true,
            CustomMinimumSize = new Vector2(126, 32),
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        };
        input.AddThemeFontSizeOverride("font_size", SolverUiTokens.Type.Body);
        input.AddThemeColorOverride("font_color", SolverUiTokens.Palette.TextPrimary);
        input.AddThemeColorOverride("font_placeholder_color", SolverUiTokens.Palette.TextMuted);
        input.AddThemeColorOverride("caret_color", SolverUiTokens.Palette.Accent);
        input.AddThemeStyleboxOverride("normal", SolverUiTokens.CreateBox(
            SolverUiTokens.Palette.Background,
            SolverUiTokens.Palette.BorderSubtle,
            SolverUiTokens.Radius.Small,
            SolverUiTokens.Spacing.Sm,
            SolverUiTokens.Spacing.Xs));
        input.AddThemeStyleboxOverride("focus", SolverUiTokens.CreateBox(
            SolverUiTokens.Palette.SurfaceRaised,
            SolverUiTokens.Palette.Accent,
            SolverUiTokens.Radius.Small,
            SolverUiTokens.Spacing.Sm,
            SolverUiTokens.Spacing.Xs));
        SolverUiTokens.ApplyTextOutline(input);
        input.ApplyLocaleFontSubstitution(FontType.Regular, "font");
        input.TextChanged += _ => input.AddThemeColorOverride("font_color", SolverUiTokens.Palette.TextPrimary);
        return input;
    }

    private static Label CreateRowLabel(string text, float minimumWidth = 170f)
    {
        Label label = SolverUiTokens.CreateLabel(
            text,
            SolverUiTokens.Type.Body,
            SolverUiTokens.Palette.TextSecondary,
            FontType.Bold);
        label.CustomMinimumSize = new Vector2(minimumWidth, 32);
        return label;
    }

    private static CheckButton CreateToggle()
    {
        CheckButton toggle = new()
        {
            FocusMode = FocusModeEnum.None,
            MouseDefaultCursorShape = CursorShape.PointingHand,
            CustomMinimumSize = new Vector2(126, 32),
            SizeFlagsHorizontal = SizeFlags.ShrinkEnd,
        };
        toggle.AddThemeColorOverride("font_color", SolverUiTokens.Palette.TextPrimary);
        toggle.AddThemeColorOverride("font_hover_color", Colors.White);
        SolverUiTokens.ApplyTextOutline(toggle);
        return toggle;
    }

    private static void AddBasicRow(
        GridContainer grid,
        string label,
        Control input,
        string? tooltip = null)
    {
        Label rowLabel = CreateRowLabel(label, 250);
        if (!string.IsNullOrEmpty(tooltip))
        {
            ApplyTooltip(rowLabel, tooltip);
            ApplyTooltip(input, tooltip);
        }
        grid.AddChild(rowLabel);
        grid.AddChild(input);
    }

    private static void ApplyTooltip(Control control, string tooltip)
    {
        control.TooltipText = tooltip;
        if (control is Label)
            control.MouseFilter = MouseFilterEnum.Pass;
    }

    private static Control CreateSectionHeading(string text)
    {
        HBoxContainer row = new()
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            MouseFilter = MouseFilterEnum.Ignore,
        };
        row.AddThemeConstantOverride("separation", SolverUiTokens.Spacing.Sm);
        row.AddChild(SolverUiTokens.CreateLabel(
            text,
            SolverUiTokens.Type.Body,
            SolverUiTokens.Palette.TextPrimary,
            FontType.Bold));
        ColorRect divider = new()
        {
            Color = SolverUiTokens.Palette.BorderSubtle,
            CustomMinimumSize = new Vector2(0, 1),
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ShrinkCenter,
            MouseFilter = MouseFilterEnum.Ignore,
        };
        row.AddChild(divider);
        return row;
    }

    private static void AddGridHeader(GridContainer grid, string text)
    {
        Label label = SolverUiTokens.CreateLabel(
            text,
            SolverUiTokens.Type.Caption,
            SolverUiTokens.Palette.TextMuted,
            FontType.Bold);
        label.HorizontalAlignment = HorizontalAlignment.Center;
        grid.AddChild(label);
    }

    private bool Save(LineEdit input, SolverSettingsData data)
    {
        if (_loading)
            return true;
        if (data != SolverSettings.Current)
            SolverSettings.Update(data);
        _performancePreset.Selected = (int)SolverSettings.ResolvePerformancePreset(data);
        input.AddThemeColorOverride("font_color", SolverUiTokens.Palette.TextPrimary);
        _status.AddThemeColorOverride("font_color", SolverUiTokens.Palette.Success);
        _status.Text = "已保存，下次搜索生效";
        return true;
    }

    private static bool KeepUnchanged(LineEdit input)
    {
        input.AddThemeColorOverride("font_color", SolverUiTokens.Palette.TextPrimary);
        return true;
    }

    private void ShowInvalid(LineEdit input, string message)
    {
        input.AddThemeColorOverride("font_color", SolverUiTokens.Palette.Danger);
        _status.AddThemeColorOverride("font_color", SolverUiTokens.Palette.Danger);
        _status.Text = message;
    }

    private void OnStopOnCombatEndToggled(bool enabled)
    {
        if (_loading)
            return;
        SolverController.SetStopFullAutoOnCombatEnd(enabled);
        _status.AddThemeColorOverride("font_color", SolverUiTokens.Palette.Success);
        _status.Text = "已保存并立即生效";
    }

    private void OnSolverDisabledToggled(bool disabled)
    {
        if (_loading)
            return;
        SolverController.SetSolverDisabled(disabled);
        _status.AddThemeColorOverride("font_color", SolverUiTokens.Palette.Success);
        _status.Text = disabled ? "求解器已暂停" : "求解器已启用";
    }

    private void OnStopOnDeathTurnToggled(bool enabled)
    {
        if (_loading)
            return;
        SolverController.SetStopFullAutoOnDeathTurn(enabled);
        _status.AddThemeColorOverride("font_color", SolverUiTokens.Palette.Success);
        _status.Text = "已保存并立即生效";
    }

    private void OnDetailedDiagnosticLogsToggled(bool enabled)
    {
        if (_loading)
            return;
        SolverSettings.Update(SolverSettings.Current with { EnableDetailedDiagnosticLogs = enabled });
        _status.AddThemeColorOverride("font_color", SolverUiTokens.Palette.Success);
        _status.Text = "已保存，下次搜索生效";
    }

    private void OnSearchCompletionNotificationsToggled(bool enabled)
    {
        if (_loading)
            return;
        SolverSettings.Update(SolverSettings.Current with
        {
            SearchCompletionNotificationsEnabled = enabled,
        });
        _status.AddThemeColorOverride("font_color", SolverUiTokens.Palette.Success);
        _status.Text = "已保存并立即生效";
    }

    private void OnStopOnWorseRecalculationToggled(bool enabled)
    {
        if (_loading)
            return;
        SolverController.SetStopFullAutoOnWorseRecalculation(enabled);
        _status.AddThemeColorOverride("font_color", SolverUiTokens.Palette.Success);
        _status.Text = "已保存并立即生效";
    }

    private void OnExportBugReportPressed()
    {
        if (_exportInProgress || _uploadInProgress || HasOpenUploadDialog())
            return;
        _exportInProgress = true;
        RefreshBugReportControls();
        _status.AddThemeColorOverride("font_color", SolverUiTokens.Palette.TextSecondary);
        _status.Text = "正在打包日志和当前战斗…";
        TaskHelper.RunSafely(ExportBugReportAsync());
    }

    private async Task ExportBugReportAsync()
    {
        try
        {
            string path = await CombatBugReportExporter.ExportCurrentAsync();
            PostUi(() =>
            {
                OS.ShellShowInFileManager(path);
                _status.AddThemeColorOverride("font_color", SolverUiTokens.Palette.Success);
                _status.Text = $"已导出到桌面：{Path.GetFileName(path)}";
            });
        }
        catch (Exception ex)
        {
            Entry.Logger.Error($"[CombatSolver/Test] BUG_REPORT_EXPORT_FAILED exception={ex}");
            PostUi(() =>
            {
                _status.AddThemeColorOverride("font_color", SolverUiTokens.Palette.Danger);
                _status.Text = $"导出失败：{DescribeUiFailure(ex)}";
            });
        }
        finally
        {
            _exportInProgress = false;
            PostUi(RefreshBugReportControls);
        }
    }

    private void OnUploadBugReportPressed()
    {
        if (_uploadInProgress)
        {
            _uploadCancelRequested = true;
            _uploadCancellation?.Cancel();
            Entry.Logger.Info(
                $"[CombatSolver/Test] BUG_REPORT_UPLOAD_CANCEL_REQUESTED submission_id={_uploadSubmissionId ?? "unknown"}");
            _status.AddThemeColorOverride("font_color", SolverUiTokens.Palette.Warning);
            _status.Text = "正在取消上传…";
            RefreshBugReportControls();
            return;
        }
        if (_exportInProgress || HasOpenUploadDialog())
            return;
        _uploadProgress.Visible = false;
        BugReportUploadDialog dialog = new(SolverSettings.Current.ReporterContactQq ?? string.Empty);
        _uploadDialog = dialog;
        dialog.UploadConfirmed += OnUploadConfirmed;
        dialog.DialogClosed += () => OnUploadDialogClosed(dialog);
        GetTree().Root.AddChild(dialog);
        RefreshBugReportControls();
    }

    private void OnUploadConfirmed(string description)
    {
        if (_uploadInProgress || _exportInProgress)
            return;
        _uploadInProgress = true;
        _uploadCancelRequested = false;
        _uploadCancellation = new CancellationTokenSource();
        Interlocked.Exchange(ref _uploadCompletion, null);
        Interlocked.Exchange(ref _uploadBytesSent, 0);
        Interlocked.Exchange(ref _uploadTotalBytes, 0);
        _lastRenderedUploadPercentage = -1;
        _uploadProgress.Value = 0;
        _uploadProgress.Visible = true;
        SetProcess(true);
        RefreshBugReportControls();
        _status.AddThemeColorOverride("font_color", SolverUiTokens.Palette.TextSecondary);
        _status.Text = "正在打包问题包…";
        string submissionId = Guid.NewGuid().ToString("N");
        _uploadSubmissionId = submissionId;
        TaskHelper.RunSafely(UploadBugReportAsync(
            description,
            submissionId,
            _uploadCancellation.Token));
    }

    private async Task UploadBugReportAsync(
        string description,
        string submissionId,
        CancellationToken cancellationToken)
    {
        string? path = null;
        try
        {
            string descriptionWithClassification = SolverController.BuildBugReportDescription(description);
            string uploadDescription = CombatBugReportDescription.AppendSubmissionId(
                descriptionWithClassification,
                submissionId);
            path = await CombatBugReportExporter.ExportCurrentAsync();
            cancellationToken.ThrowIfCancellationRequested();
            FileInfo archive = new(path);
            Interlocked.Exchange(ref _uploadBytesSent, 0);
            Interlocked.Exchange(ref _uploadTotalBytes, archive.Length);
            Entry.Logger.Info(
                $"[CombatSolver/Test] BUG_REPORT_UPLOAD_STARTED submission_id={submissionId} zip_bytes={archive.Length}");
            string contactQq = SolverSettings.Current.ReporterContactQq ?? string.Empty;
            DirectProgress<CombatBugReportUploadProgress> progress = new(value =>
            {
                Interlocked.Exchange(ref _uploadBytesSent, value.BytesSent);
                Interlocked.Exchange(ref _uploadTotalBytes, value.TotalBytes);
            });
            CombatBugReportUploadReceipt receipt = await CombatBugReportUploader.UploadAsync(
                path,
                uploadDescription,
                contactQq,
                submissionId,
                progress,
                cancellationToken);
            Entry.Logger.Info(
                $"[CombatSolver/Test] BUG_REPORT_UPLOAD_CONFIRMED submission_id={submissionId} report_id={receipt.ReportId} status={(int)receipt.StatusCode} zip_bytes={receipt.SizeBytes}");
            string? cleanupWarning = DeleteUploadedArchive(path);
            PublishUploadCompletion(new UploadCompletion(
                UploadCompletionKind.Succeeded,
                cleanupWarning == null
                    ? $"已上传 {FormatByteCount(receipt.SizeBytes)}，反馈编号：{receipt.ReportId}"
                    : $"已上传 {FormatByteCount(receipt.SizeBytes)}，反馈编号：{receipt.ReportId}；{cleanupWarning}",
                receipt.SizeBytes));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            Entry.Logger.Info($"[CombatSolver/Test] BUG_REPORT_UPLOAD_CANCELED submission_id={submissionId}");
            PublishUploadCompletion(new UploadCompletion(
                UploadCompletionKind.Canceled,
                path == null
                    ? "上传已取消"
                    : "上传已取消；未收到服务器确认，问题包已保留",
                0));
        }
        catch (TaskCanceledException ex)
        {
            Entry.Logger.Error(
                $"[CombatSolver/Test] BUG_REPORT_UPLOAD_UNCONFIRMED submission_id={submissionId} exception={ex}");
            PublishUploadCompletion(new UploadCompletion(
                UploadCompletionKind.Failed,
                "上传结果未确认；问题包已保留，请勿立即重复提交",
                0));
        }
        catch (Exception ex)
        {
            Entry.Logger.Error(
                $"[CombatSolver/Test] BUG_REPORT_UPLOAD_FAILED submission_id={submissionId} exception={ex}");
            PublishUploadCompletion(new UploadCompletion(
                UploadCompletionKind.Failed,
                path == null
                    ? $"打包失败：{DescribeUiFailure(ex)}"
                    : $"上传未完成：{DescribeUiFailure(ex)}（问题包已保留）",
                0));
        }
    }

    private void PublishUploadCompletion(UploadCompletion completion)
        => Interlocked.Exchange(ref _uploadCompletion, completion);

    private bool TryApplyUploadCompletion()
    {
        UploadCompletion? completion = Interlocked.Exchange(ref _uploadCompletion, null);
        if (completion == null)
            return false;
        string submissionId = _uploadSubmissionId ?? "unknown";
        _uploadInProgress = false;
        _uploadCancelRequested = false;
        CancellationTokenSource? completedCancellation = _uploadCancellation;
        _uploadCancellation = null;
        _uploadSubmissionId = null;
        completedCancellation?.Dispose();
        SetProcess(false);
        _uploadProgress.Value = 0;
        _uploadProgress.Visible = false;
        _status.AddThemeColorOverride(
            "font_color",
            completion.Kind switch
            {
                UploadCompletionKind.Succeeded => SolverUiTokens.Palette.Success,
                UploadCompletionKind.Canceled => SolverUiTokens.Palette.Warning,
                _ => SolverUiTokens.Palette.Danger,
            });
        _status.Text = completion.Message;
        RefreshBugReportControls();
        Entry.Logger.Info(
            $"[CombatSolver/Test] BUG_REPORT_UPLOAD_UI_COMPLETED submission_id={submissionId} kind={completion.Kind} confirmed_bytes={completion.ConfirmedBytes}");
        return true;
    }

    internal bool ExerciseUploadCompletionTransitionForTesting()
    {
        if (_uploadInProgress || _exportInProgress || HasOpenUploadDialog())
            return false;

        CancellationTokenSource successCancellation = new();
        _uploadInProgress = true;
        _uploadCancelRequested = false;
        _uploadCancellation = successCancellation;
        _uploadSubmissionId = "test_success";
        SetProcess(true);
        RefreshBugReportControls();
        PublishUploadCompletion(new UploadCompletion(
            UploadCompletionKind.Succeeded,
            "测试上传成功",
            1024));
        bool successRemainsActiveUntilConsumed = _uploadInProgress
                                                 && _uploadBugReport.Text == "取消上传";
        bool successApplied = TryApplyUploadCompletion();
        bool successReturnedToIdle = successApplied
                                     && !_uploadInProgress
                                     && _uploadCancellation == null
                                     && !_uploadProgress.Visible
                                     && _uploadBugReport.Text == "上传问题包";

        CancellationTokenSource canceledCancellation = new();
        _uploadInProgress = true;
        _uploadCancelRequested = true;
        _uploadCancellation = canceledCancellation;
        _uploadSubmissionId = "test_canceled";
        SetProcess(true);
        RefreshBugReportControls();
        PublishUploadCompletion(new UploadCompletion(
            UploadCompletionKind.Canceled,
            "测试上传已取消",
            0));
        bool cancellationRemainsActiveUntilConsumed = _uploadInProgress
                                                      && _uploadBugReport.Disabled
                                                      && _uploadBugReport.Text == "正在取消…";
        bool cancellationApplied = TryApplyUploadCompletion();
        bool cancellationReturnedToIdle = cancellationApplied
                                          && !_uploadInProgress
                                          && _uploadCancellation == null
                                          && !_uploadProgress.Visible
                                          && _uploadBugReport.Text == "上传问题包";
        return successRemainsActiveUntilConsumed
               && successReturnedToIdle
               && cancellationRemainsActiveUntilConsumed
               && cancellationReturnedToIdle;
    }

    private void OnUploadDialogClosed(BugReportUploadDialog dialog)
    {
        if (!ReferenceEquals(_uploadDialog, dialog))
            return;
        _uploadDialog = null;
        if (CanUpdateUi())
            RefreshBugReportControls();
    }

    private bool HasOpenUploadDialog()
        => _uploadDialog != null && GodotObject.IsInstanceValid(_uploadDialog);

    private void RefreshBugReportControls()
    {
        bool dialogOpen = HasOpenUploadDialog();
        _exportBugReport.Disabled = _exportInProgress || _uploadInProgress || dialogOpen;
        if (_uploadInProgress)
        {
            _uploadBugReport.Disabled = _uploadCancelRequested;
            _uploadBugReport.Text = _uploadCancelRequested ? "正在取消…" : "取消上传";
            SolverUiTokens.ApplyButtonStyle(_uploadBugReport, SolverButtonStyle.Danger);
            return;
        }
        _uploadBugReport.Disabled = _exportInProgress || dialogOpen;
        _uploadBugReport.Text = "上传问题包";
        SolverUiTokens.ApplyButtonStyle(_uploadBugReport, SolverButtonStyle.Secondary);
    }

    private bool CanUpdateUi()
        => GodotObject.IsInstanceValid(this)
           && GodotObject.IsInstanceValid(_status)
           && GodotObject.IsInstanceValid(_uploadBugReport)
           && GodotObject.IsInstanceValid(_uploadProgress);

    private void PostUi(Action action)
        => SolverDispatcher.Post(() =>
        {
            if (CanUpdateUi())
                action();
        });

    private static string? DeleteUploadedArchive(string path)
    {
        try
        {
            File.Delete(path);
            return null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Entry.Logger.Warn($"[CombatSolver/Test] BUG_REPORT_UPLOAD_CLEANUP_FAILED path={path} exception={ex}");
            return "本地问题包未能删除";
        }
    }

    private static string DescribeUiFailure(Exception exception)
        => CombatBugReportDescription.NormalizeDetail(exception.GetBaseException().Message)
           ?? exception.GetType().Name;

    private static string FormatByteCount(long bytes)
    {
        if (bytes < 1024)
            return $"{bytes} B";
        if (bytes < 1024L * 1024)
            return $"{bytes / 1024d:0.##} KB";
        if (bytes < 1024L * 1024 * 1024)
            return $"{bytes / (1024d * 1024):0.##} MB";
        return $"{bytes / (1024d * 1024 * 1024):0.##} GB";
    }

    private static double MapUploadProgressBarValue(int transmittedPercentage)
        => transmittedPercentage >= 100
            ? 95
            : Math.Min(94, transmittedPercentage * 0.95);

    private static string FormatUploadProgressStatus(long sent, long total, int percentage)
        => sent >= total
            ? $"已发送 {FormatByteCount(total)}，正在等待服务器确认…"
            : $"正在上传… {FormatByteCount(sent)} / {FormatByteCount(total)}（{percentage}%）";

    private sealed class DirectProgress<T>(Action<T> report) : IProgress<T>
    {
        public void Report(T value) => report(value);
    }

    private enum UploadCompletionKind
    {
        Succeeded,
        Canceled,
        Failed,
    }

    private sealed record UploadCompletion(
        UploadCompletionKind Kind,
        string Message,
        long ConfirmedBytes);

    private void ResetDefaults()
    {
        bool wasDisabled = SolverController.SolverDisabled;
        SolverSettings.ResetToDefaults();
        SolverSettingsSnapshot defaults = SolverSettings.Capture();
        SolverController.ApplyPersistentSettings(defaults);
        if (wasDisabled != defaults.SolverDisabled)
            SolverController.SetSolverDisabled(defaults.SolverDisabled, persist: false);
        SolverOverlay.RefreshControls();
        ResetPositionRequested?.Invoke();
        Reload();
        _status.Text = "已恢复默认设置";
    }

    private static SolverSettingsData AsCustomPerformance(SolverSettingsData data)
        => data with { PerformancePreset = SolverPerformancePreset.Custom };
}
