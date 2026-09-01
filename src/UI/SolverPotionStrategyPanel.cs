using Godot;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.Fonts;
using MegaCrit.Sts2.Core.Models;

namespace CombatSolver;

internal sealed partial class SolverPotionStrategyPanel : PanelContainer
{
    private const float CardMinimumWidth = 136f;
    private readonly GridContainer _cards;
    private string? _renderedSignature;

    public SolverPotionStrategyPanel()
    {
        Name = "PotionStrategyPanel";
        Visible = false;
        MouseFilter = MouseFilterEnum.Stop;
        CustomMinimumSize = new Vector2(300, 0);
        SizeFlagsHorizontal = SizeFlags.ShrinkEnd;
        SizeFlagsVertical = SizeFlags.ExpandFill;
        AddThemeStyleboxOverride("panel", SolverUiTokens.CreateBox(
            SolverUiTokens.Palette.Surface,
            SolverUiTokens.Palette.BorderSubtle,
            SolverUiTokens.Radius.Medium,
            SolverUiTokens.Spacing.Sm,
            SolverUiTokens.Spacing.Sm));

        VBoxContainer layout = new()
        {
            Name = "PotionStrategyLayout",
            MouseFilter = MouseFilterEnum.Pass,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        };
        layout.AddThemeConstantOverride("separation", SolverUiTokens.Spacing.Sm);
        Label heading = SolverUiTokens.CreateLabel(
            "药水策略",
            SolverUiTokens.Type.Body,
            SolverUiTokens.Palette.TextPrimary,
            FontType.Bold);
        layout.AddChild(heading);

        _cards = new GridContainer
        {
            Name = "PotionCards",
            Columns = 2,
            MouseFilter = MouseFilterEnum.Pass,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        };
        _cards.AddThemeConstantOverride("h_separation", SolverUiTokens.Spacing.Sm);
        _cards.AddThemeConstantOverride("v_separation", SolverUiTokens.Spacing.Sm);
        ScrollContainer scroll = new()
        {
            Name = "PotionCardScroll",
            CustomMinimumSize = new Vector2(0, 176),
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
            VerticalScrollMode = ScrollContainer.ScrollMode.Auto,
            MouseFilter = MouseFilterEnum.Pass,
        };
        scroll.AddChild(_cards);
        layout.AddChild(scroll);
        AddChild(layout);
        Resized += UpdateGridColumns;
    }

    public event Action<int, string, SolverPotionDirective>? DirectiveChanged;

    internal int RowCountForTesting { get; private set; }
    internal bool RowsUseIconAndTextForTesting { get; private set; }
    internal bool UsesGridCardsForTesting { get; private set; }

    public void Refresh(CombatState? state, bool controlsDisabled)
    {
        Player? player = state == null ? null : LocalContext.GetMe(state);
        List<(int Slot, PotionModel Potion, SolverPotionDirective Directive, bool Searchable)> potions = [];
        if (player != null)
        {
            for (int slot = 0; slot < player.PotionSlots.Count; slot++)
            {
                PotionModel? potion = player.GetPotionAtSlotIndex(slot);
                if (potion == null)
                    continue;
                potions.Add((
                    slot,
                    potion,
                    SolverController.ResolvePotionDirective(state!, slot, potion.Id.Entry),
                    PotionOnUseSupport.CanSearch(potion)));
            }
        }

        string signature = string.Join('|', potions.Select(item =>
            $"{item.Slot}:{item.Potion.Id.Entry}:{item.Directive}:{item.Searchable}"))
            + $";disabled={controlsDisabled}";
        if (string.Equals(signature, _renderedSignature, StringComparison.Ordinal))
            return;
        _renderedSignature = signature;
        RebuildRows(potions, controlsDisabled);
    }

    public void Invalidate() => _renderedSignature = null;

    private void RebuildRows(
        IReadOnlyList<(int Slot, PotionModel Potion, SolverPotionDirective Directive, bool Searchable)> potions,
        bool controlsDisabled)
    {
        foreach (Node child in _cards.GetChildren())
        {
            _cards.RemoveChild(child);
            child.QueueFree();
        }

        RowCountForTesting = potions.Count;
        RowsUseIconAndTextForTesting = potions.Count > 0;
        UsesGridCardsForTesting = potions.Count > 0;
        if (potions.Count == 0)
        {
            Label empty = SolverUiTokens.CreateLabel(
                "当前没有药水",
                SolverUiTokens.Type.Body,
                SolverUiTokens.Palette.TextMuted);
            empty.CustomMinimumSize = new Vector2(0, 32);
            _cards.AddChild(empty);
            return;
        }

        foreach ((int slot, PotionModel potion, SolverPotionDirective directive, bool searchable) in potions)
            _cards.AddChild(CreatePotionCard(slot, potion, directive, searchable, controlsDisabled));
        Callable.From(UpdateGridColumns).CallDeferred();
    }

    private Control CreatePotionCard(
        int slot,
        PotionModel potion,
        SolverPotionDirective directive,
        bool searchable,
        bool controlsDisabled)
    {
        PanelContainer card = new()
        {
            Name = $"PotionStrategyCard{slot}",
            CustomMinimumSize = new Vector2(CardMinimumWidth, 154),
            MouseFilter = MouseFilterEnum.Pass,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        };
        card.AddThemeStyleboxOverride("panel", SolverUiTokens.CreateBox(
            SolverUiTokens.Palette.SurfaceRaised,
            SolverUiTokens.Palette.BorderSubtle,
            SolverUiTokens.Radius.Medium,
            SolverUiTokens.Spacing.Sm,
            SolverUiTokens.Spacing.Sm));
        VBoxContainer layout = new()
        {
            MouseFilter = MouseFilterEnum.Pass,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        };
        layout.AddThemeConstantOverride("separation", SolverUiTokens.Spacing.Xs);

        TextureRect icon = new()
        {
            Name = "PotionIcon",
            Texture = potion.Image,
            CustomMinimumSize = new Vector2(56, 56),
            SizeFlagsHorizontal = SizeFlags.ShrinkCenter,
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            MouseFilter = MouseFilterEnum.Ignore,
        };
        layout.AddChild(icon);

        Label title = SolverUiTokens.CreateLabel(
            $"#{slot + 1}  {potion.Title.GetFormattedText()}",
            SolverUiTokens.Type.Caption,
            SolverUiTokens.Palette.TextPrimary,
            FontType.Bold);
        title.Name = "PotionTitle";
        title.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        title.HorizontalAlignment = HorizontalAlignment.Center;
        title.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        title.CustomMinimumSize = new Vector2(0, 38);
        layout.AddChild(title);

        OptionButton input = CreateDirectiveInput();
        input.Name = "PotionDirective";
        if (!searchable)
        {
            input.AddItem("不可指定");
            input.Disabled = true;
        }
        else
        {
            input.AddItem("智能使用", (int)SolverPotionDirective.Smart);
            input.AddItem("强制使用", (int)SolverPotionDirective.Force);
            input.AddItem("禁用 / 保护", (int)SolverPotionDirective.Disabled);
            input.Selected = input.GetItemIndex((int)directive);
            input.Disabled = controlsDisabled;
            string potionId = potion.Id.Entry;
            input.ItemSelected += index =>
            {
                SolverPotionDirective selected =
                    (SolverPotionDirective)input.GetItemId((int)index);
                DirectiveChanged?.Invoke(slot, potionId, selected);
            };
        }
        input.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        layout.AddChild(input);
        card.AddChild(layout);
        return card;
    }

    private void UpdateGridColumns()
    {
        float availableWidth = Math.Max(CustomMinimumSize.X, Size.X)
            - SolverUiTokens.Spacing.Sm * 2f;
        _cards.Columns = Math.Clamp(
            (int)Math.Floor(availableWidth / (CardMinimumWidth + SolverUiTokens.Spacing.Sm)),
            1,
            4);
    }

    private static OptionButton CreateDirectiveInput()
    {
        OptionButton input = new()
        {
            FocusMode = FocusModeEnum.None,
            CustomMinimumSize = new Vector2(148, 32),
            SizeFlagsHorizontal = SizeFlags.ShrinkEnd,
            MouseDefaultCursorShape = CursorShape.PointingHand,
        };
        input.AddThemeFontSizeOverride("font_size", SolverUiTokens.Type.Body);
        input.AddThemeColorOverride("font_color", SolverUiTokens.Palette.TextPrimary);
        input.AddThemeColorOverride("font_disabled_color", SolverUiTokens.Palette.TextMuted);
        input.AddThemeStyleboxOverride("normal", SolverUiTokens.CreateBox(
            SolverUiTokens.IsLightTheme
                ? SolverUiTokens.Palette.Surface
                : SolverUiTokens.Palette.Background,
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
        if (SolverUiTokens.IsLightTheme)
        {
            input.AddThemeIconOverride(
                "arrow",
                SolverUiTokens.CreateChevronTexture(SolverUiTokens.Palette.TextSecondary));
        }
        SolverUiTokens.ApplyTextOutline(input);
        input.ApplyLocaleFontSubstitution(FontType.Bold, "font");
        return input;
    }
}
