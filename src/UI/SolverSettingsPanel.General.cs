using Godot;

namespace CombatSolver;

internal sealed partial class SolverSettingsPanel
{
    private CheckButton _solverEnabled = null!;
    private CheckButton _stopOnCombatEnd = null!;
    private CheckButton _stopOnDeathTurn = null!;
    private CheckButton _stopOnWorseRecalculation = null!;
    private OptionButton _searchCompletionNotificationPolicy = null!;
    private OptionButton _potionPolicy = null!;

    internal bool SearchCompletionNotificationSettingsConfiguredForTesting
        => _searchCompletionNotificationPolicy.GetItemId(
               _searchCompletionNotificationPolicy.Selected)
           == (int)ResolveSearchCompletionNotificationPolicy(SolverSettings.Current);

    internal bool ExerciseSearchCompletionNotificationPolicyForTesting()
    {
        SolverSettingsData original = SolverSettings.Current;
        try
        {
            SolverSettings.ApplyForTesting(original with
            {
                SearchCompletionNotificationsEnabled = false,
                SearchCompletionNotificationMode = SolverSearchCompletionNotificationMode.Always,
            });
            Reload();
            bool disabledLoaded = SelectedSearchCompletionNotificationPolicy()
                                  == SearchCompletionNotificationPolicy.Disabled;

            SolverSettings.ApplyForTesting(original with
            {
                SearchCompletionNotificationsEnabled = true,
                SearchCompletionNotificationMode =
                    SolverSearchCompletionNotificationMode.OnlyWhenGameInBackground,
            });
            Reload();
            bool backgroundLoaded = SelectedSearchCompletionNotificationPolicy()
                                    == SearchCompletionNotificationPolicy.BackgroundOnly;

            SolverSettings.ApplyForTesting(original with
            {
                SearchCompletionNotificationsEnabled = true,
                SearchCompletionNotificationMode = SolverSearchCompletionNotificationMode.Always,
            });
            Reload();
            bool alwaysLoaded = SelectedSearchCompletionNotificationPolicy()
                                == SearchCompletionNotificationPolicy.Always;
            return disabledLoaded && backgroundLoaded && alwaysLoaded;
        }
        finally
        {
            SolverSettings.ApplyForTesting(original);
            Reload();
        }
    }

    private Control CreateGeneralPage()
    {
        VBoxContainer content = CreatePageContent("GeneralSettingsPage");
        content.AddChild(CreateSectionHeading("求解器"));
        GridContainer solverGrid = CreateSettingsGrid();
        _solverEnabled = CreateToggle();
        _solverEnabled.Toggled += OnSolverEnabledToggled;
        AddBasicRow(solverGrid, "启用求解器", _solverEnabled);
        _potionPolicy = CreatePotionPolicyInput();
        AddBasicRow(solverGrid, "药水策略", _potionPolicy);
        _searchCompletionNotificationPolicy = CreateSearchCompletionNotificationPolicyInput();
        AddBasicRow(
            solverGrid,
            "搜索结束通知",
            _searchCompletionNotificationPolicy,
            "搜索成功、失败、停止或结果过期时发送 Windows 系统通知和提示音。可关闭、仅在游戏不处于前台时通知，或始终通知；其他平台不会调用 Windows 接口。");
        content.AddChild(solverGrid);

        content.AddChild(CreateSectionHeading("自动执行"));
        GridContainer executionGrid = CreateSettingsGrid();
        _stopOnCombatEnd = CreateToggle();
        _stopOnCombatEnd.Toggled += OnStopOnCombatEndToggled;
        AddBasicRow(executionGrid, "预计结束战斗时暂停", _stopOnCombatEnd);
        _stopOnDeathTurn = CreateToggle();
        _stopOnDeathTurn.Toggled += OnStopOnDeathTurnToggled;
        AddBasicRow(executionGrid, "死亡回合时暂停", _stopOnDeathTurn);
        _stopOnWorseRecalculation = CreateToggle();
        _stopOnWorseRecalculation.Toggled += OnStopOnWorseRecalculationToggled;
        AddBasicRow(executionGrid, "重算后战损增加时暂停", _stopOnWorseRecalculation);
        AddBasicRow(executionGrid, "自动出牌速度", CreateDeploymentFastModeInput());
        AddBasicRow(executionGrid, "牌间额外停顿（秒）", CreateOptionalDoubleInput(
            0d,
            data => data.DeploymentInterActionDelaySeconds,
            (data, value) => data with { DeploymentInterActionDelaySeconds = value },
            0d,
            3d));
        content.AddChild(executionGrid);
        return CreatePageScroll(content);
    }

    private void ReloadGeneralPage(SolverSettingsData data)
    {
        _solverEnabled.ButtonPressed = !data.SolverDisabled;
        _stopOnCombatEnd.ButtonPressed = data.StopFullAutoOnCombatEnd;
        _stopOnDeathTurn.ButtonPressed = data.StopFullAutoOnDeathTurn;
        _stopOnWorseRecalculation.ButtonPressed = data.StopFullAutoOnWorseRecalculation;
    }

    private OptionButton CreateSearchCompletionNotificationPolicyInput()
    {
        OptionButton input = CreateOptionInput(260);
        input.AddItem("关闭", (int)SearchCompletionNotificationPolicy.Disabled);
        input.AddItem("仅游戏不在前台（默认）", (int)SearchCompletionNotificationPolicy.BackgroundOnly);
        input.AddItem("始终通知", (int)SearchCompletionNotificationPolicy.Always);
        _reloadInputs.Add(data => input.Selected = input.GetItemIndex(
            (int)ResolveSearchCompletionNotificationPolicy(data)));
        input.ItemSelected += index =>
        {
            if (_loading)
                return;
            SearchCompletionNotificationPolicy policy =
                (SearchCompletionNotificationPolicy)input.GetItemId((int)index);
            SolverSettings.Update(SolverSettings.Current with
            {
                SearchCompletionNotificationsEnabled = policy != SearchCompletionNotificationPolicy.Disabled,
                SearchCompletionNotificationMode = policy == SearchCompletionNotificationPolicy.Always
                    ? SolverSearchCompletionNotificationMode.Always
                    : SolverSearchCompletionNotificationMode.OnlyWhenGameInBackground,
            });
            SetStatus("已保存并立即生效", SolverUiTokens.Palette.Success);
        };
        return input;
    }

    private OptionButton CreatePotionPolicyInput()
    {
        OptionButton input = CreateOptionInput();
        input.AddItem("禁用", (int)SolverPotionPolicy.Disabled);
        input.AddItem("智能（默认）", (int)SolverPotionPolicy.Smart);
        input.AddItem("至少用一瓶", (int)SolverPotionPolicy.RequireAtLeastOne);
        _reloadInputs.Add(data => input.Selected = input.GetItemIndex((int)data.PotionPolicy));
        input.ItemSelected += index =>
        {
            if (_loading)
                return;
            SolverPotionPolicy policy = (SolverPotionPolicy)input.GetItemId((int)index);
            SolverSettings.Update(SolverSettings.Current with { PotionPolicy = policy });
            SetStatus("已保存，下次搜索生效", SolverUiTokens.Palette.Success);
        };
        return input;
    }

    private OptionButton CreateDeploymentFastModeInput()
    {
        OptionButton input = CreateOptionInput();
        input.AddItem("跟随游戏（默认）", (int)SolverDeploymentFastMode.FollowGame);
        input.AddItem("正常", (int)SolverDeploymentFastMode.Normal);
        input.AddItem("快速", (int)SolverDeploymentFastMode.Fast);
        input.AddItem("瞬间", (int)SolverDeploymentFastMode.Instant);
        _reloadInputs.Add(data => input.Selected = input.GetItemIndex((int)data.DeploymentFastMode));
        input.ItemSelected += index =>
        {
            if (_loading)
                return;
            SolverDeploymentFastMode mode = (SolverDeploymentFastMode)input.GetItemId((int)index);
            SolverSettings.Update(SolverSettings.Current with { DeploymentFastMode = mode });
            SetStatus("已保存，下次执行生效", SolverUiTokens.Palette.Success);
        };
        return input;
    }

    private void OnSolverEnabledToggled(bool enabled)
    {
        if (_loading)
            return;
        SolverController.SetSolverDisabled(!enabled);
        SetStatus(enabled ? "求解器已启用" : "求解器已暂停", SolverUiTokens.Palette.Success);
    }

    private void OnStopOnCombatEndToggled(bool enabled)
    {
        if (_loading)
            return;
        SolverController.SetStopFullAutoOnCombatEnd(enabled);
        SetStatus("已保存并立即生效", SolverUiTokens.Palette.Success);
    }

    private void OnStopOnDeathTurnToggled(bool enabled)
    {
        if (_loading)
            return;
        SolverController.SetStopFullAutoOnDeathTurn(enabled);
        SetStatus("已保存并立即生效", SolverUiTokens.Palette.Success);
    }

    private void OnStopOnWorseRecalculationToggled(bool enabled)
    {
        if (_loading)
            return;
        SolverController.SetStopFullAutoOnWorseRecalculation(enabled);
        SetStatus("已保存并立即生效", SolverUiTokens.Palette.Success);
    }

    private static SearchCompletionNotificationPolicy ResolveSearchCompletionNotificationPolicy(
        SolverSettingsData data)
    {
        if (!data.SearchCompletionNotificationsEnabled)
            return SearchCompletionNotificationPolicy.Disabled;
        return data.SearchCompletionNotificationMode == SolverSearchCompletionNotificationMode.Always
            ? SearchCompletionNotificationPolicy.Always
            : SearchCompletionNotificationPolicy.BackgroundOnly;
    }

    private SearchCompletionNotificationPolicy SelectedSearchCompletionNotificationPolicy()
        => (SearchCompletionNotificationPolicy)_searchCompletionNotificationPolicy.GetItemId(
            _searchCompletionNotificationPolicy.Selected);

    private enum SearchCompletionNotificationPolicy
    {
        Disabled,
        BackgroundOnly,
        Always,
    }
}
