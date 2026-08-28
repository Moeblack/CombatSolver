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
    private readonly OptionButton _potionPolicy;
    private readonly OptionButton _performancePreset;
    private readonly Button _exportBugReport;
    private readonly Label _status;
    private readonly List<Action<SolverSettingsData>> _reloadInputs = [];
    private readonly List<Func<bool>> _commitInputs = [];
    private bool _loading;

    public event Action? ResetPositionRequested;

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
        _potionPolicy = CreatePotionPolicyInput();
        AddBasicRow(basicGrid, "本场药水策略", _potionPolicy);
        _performancePreset = CreatePerformancePresetInput();
        AddBasicRow(basicGrid, "性能预设", _performancePreset);
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
            16d));
        _exportBugReport = SolverUiTokens.CreateButton("导出问题包", SolverButtonStyle.Secondary);
        _exportBugReport.CustomMinimumSize = new Vector2(126, SolverUiTokens.Size.ButtonHeight);
        _exportBugReport.Pressed += OnExportBugReportPressed;
        AddBasicRow(basicGrid, "问题反馈", _exportBugReport);
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
            120d);
        AddIntRow(searchGrid, "Beam 宽度", SolverSearchProfile.Short.BeamWidth,
            data => data.ShortBeamWidth,
            (data, value) => AsCustomPerformance(data with { ShortBeamWidth = value }),
            SolverSearchProfile.Deep.BeamWidth,
            data => data.DeepBeamWidth,
            (data, value) => AsCustomPerformance(data with { DeepBeamWidth = value }), 1, 512);
        AddIntRow(searchGrid, "节点上限", SolverSearchProfile.Short.MaxExpandedNodes,
            data => data.ShortMaxExpandedNodes,
            (data, value) => AsCustomPerformance(data with { ShortMaxExpandedNodes = value }),
            SolverSearchProfile.Deep.MaxExpandedNodes,
            data => data.DeepMaxExpandedNodes,
            (data, value) => AsCustomPerformance(data with { DeepMaxExpandedNodes = value }), 100, 100_000);
        AddIntRow(searchGrid, "单节点出牌分支", SolverSearchProfile.Short.MaxCardBranchesPerNode,
            data => data.ShortMaxCardBranchesPerNode,
            (data, value) => AsCustomPerformance(data with { ShortMaxCardBranchesPerNode = value }),
            SolverSearchProfile.Deep.MaxCardBranchesPerNode,
            data => data.DeepMaxCardBranchesPerNode,
            (data, value) => AsCustomPerformance(data with { DeepMaxCardBranchesPerNode = value }), 1, 100);
        settingsContent.AddChild(searchGrid);
        scroll.AddChild(settingsContent);
        root.AddChild(scroll);

        Label hint = SolverUiTokens.CreateLabel(
            "选择预设会成套填充；修改任一算力数值自动切到自定义。留空使用灰色中档默认值。",
            SolverUiTokens.Type.Caption,
            SolverUiTokens.Palette.TextMuted);
        root.AddChild(hint);
        _status = SolverUiTokens.CreateLabel(
            string.Empty,
            SolverUiTokens.Type.Caption,
            SolverUiTokens.Palette.Success,
            FontType.Bold);
        _status.CustomMinimumSize = new Vector2(0, 20);
        root.AddChild(_status);
        AddChild(root);
        Reload();
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
        int maximum)
    {
        grid.AddChild(CreateRowLabel(label));
        grid.AddChild(CreateIntInput(shortDefault, getShort, setShort, minimum, maximum));
        grid.AddChild(CreateIntInput(deepDefault, getDeep, setDeep, minimum, maximum));
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
        double maximum)
    {
        grid.AddChild(CreateRowLabel(label));
        grid.AddChild(CreateDoubleInput(shortDefault, getShort, setShort, minimum, maximum));
        grid.AddChild(CreateDoubleInput(deepDefault, getDeep, setDeep, minimum, maximum));
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

    private OptionButton CreatePerformancePresetInput()
    {
        OptionButton input = new()
        {
            FocusMode = FocusModeEnum.None,
            CustomMinimumSize = new Vector2(260, 32),
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            MouseDefaultCursorShape = CursorShape.PointingHand,
        };
        input.AddItem("低档（2 / 20 秒，4 GB）", (int)SolverPerformancePreset.Low);
        input.AddItem("中档（5 / 60 秒，6 GB）", (int)SolverPerformancePreset.Medium);
        input.AddItem("高档（8 / 120 秒，8 GB）", (int)SolverPerformancePreset.High);
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
                return Save(input, setter(SolverSettings.Current, null));
            }
            if (!int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int value)
                || value < minimum || value > maximum)
            {
                ShowInvalid(input, $"请输入 {minimum}–{maximum} 的整数");
                return false;
            }
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
                return Save(input, setter(SolverSettings.Current, null));
            }
            if (!double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out double value)
                || value < minimum || value > maximum)
            {
                ShowInvalid(input, $"请输入 {minimum:0.###}–{maximum:0.###} 的数字");
                return false;
            }
            return Save(input, setter(SolverSettings.Current, value));
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

    private static void AddBasicRow(GridContainer grid, string label, Control input)
    {
        grid.AddChild(CreateRowLabel(label, 250));
        grid.AddChild(input);
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
        _exportBugReport.Disabled = true;
        _status.AddThemeColorOverride("font_color", SolverUiTokens.Palette.TextSecondary);
        _status.Text = "正在打包日志和当前战斗…";
        TaskHelper.RunSafely(ExportBugReportAsync());
    }

    private async Task ExportBugReportAsync()
    {
        try
        {
            string path = await CombatBugReportExporter.ExportCurrentAsync();
            OS.ShellShowInFileManager(path);
            _status.AddThemeColorOverride("font_color", SolverUiTokens.Palette.Success);
            _status.Text = $"已导出到桌面：{Path.GetFileName(path)}";
        }
        catch (Exception ex)
        {
            Entry.Logger.Error($"[CombatSolver/Test] BUG_REPORT_EXPORT_FAILED exception={ex}");
            _status.AddThemeColorOverride("font_color", SolverUiTokens.Palette.Danger);
            _status.Text = $"导出失败：{ex.Message}";
        }
        finally
        {
            _exportBugReport.Disabled = false;
        }
    }

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
