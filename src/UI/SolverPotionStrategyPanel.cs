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
    private readonly VBoxContainer _rows;
    private string? _renderedSignature;

    public SolverPotionStrategyPanel()
    {
        Name = "PotionStrategyPanel";
        Visible = false;
        MouseFilter = MouseFilterEnum.Stop;
        SizeFlagsHorizontal = SizeFlags.ExpandFill;
        AddThemeStyleboxOverride("panel", SolverUiTokens.CreateBox(
            SolverUiTokens.Palette.Surface,
            SolverUiTokens.Palette.BorderSubtle,
            SolverUiTokens.Radius.Medium,
            SolverUiTokens.Spacing.Sm,
            SolverUiTokens.Spacing.Sm));
        _rows = new VBoxContainer
        {
            Name = "PotionRows",
            MouseFilter = MouseFilterEnum.Pass,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        };
        _rows.AddThemeConstantOverride("separation", SolverUiTokens.Spacing.Xs);
        AddChild(_rows);
    }

    public event Action<int, string, SolverPotionDirective>? DirectiveChanged;

    internal int RowCountForTesting { get; private set; }
    internal bool RowsUseIconAndTextForTesting { get; private set; }

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
        foreach (Node child in _rows.GetChildren())
        {
            _rows.RemoveChild(child);
            child.QueueFree();
        }

        RowCountForTesting = potions.Count;
        RowsUseIconAndTextForTesting = potions.Count > 0;
        if (potions.Count == 0)
        {
            Label empty = SolverUiTokens.CreateLabel(
                "当前没有药水",
                SolverUiTokens.Type.Body,
                SolverUiTokens.Palette.TextMuted);
            empty.CustomMinimumSize = new Vector2(0, 32);
            _rows.AddChild(empty);
            return;
        }

        foreach ((int slot, PotionModel potion, SolverPotionDirective directive, bool searchable) in potions)
            _rows.AddChild(CreatePotionRow(slot, potion, directive, searchable, controlsDisabled));
    }

    private Control CreatePotionRow(
        int slot,
        PotionModel potion,
        SolverPotionDirective directive,
        bool searchable,
        bool controlsDisabled)
    {
        HBoxContainer row = new()
        {
            Name = $"PotionStrategyRow{slot}",
            CustomMinimumSize = new Vector2(0, 38),
            MouseFilter = MouseFilterEnum.Pass,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        };
        row.AddThemeConstantOverride("separation", SolverUiTokens.Spacing.Sm);

        TextureRect icon = new()
        {
            Name = "PotionIcon",
            Texture = potion.Image,
            CustomMinimumSize = new Vector2(32, 32),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            MouseFilter = MouseFilterEnum.Ignore,
        };
        row.AddChild(icon);

        Label title = SolverUiTokens.CreateLabel(
            $"{slot + 1}  {potion.Title.GetFormattedText()}",
            SolverUiTokens.Type.Body,
            SolverUiTokens.Palette.TextPrimary,
            FontType.Bold);
        title.Name = "PotionTitle";
        title.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        title.ClipText = true;
        row.AddChild(title);

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
        row.AddChild(input);
        return row;
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
        input.ApplyLocaleFontSubstitution(FontType.Regular, "font");
        return input;
    }
}
