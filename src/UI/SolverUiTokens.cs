using Godot;
using MegaCrit.Sts2.Core.Localization.Fonts;

namespace CombatSolver;

internal enum SolverButtonStyle
{
    Secondary,
    Primary,
    Positive,
    Danger,
}

internal static class SolverUiTokens
{
    public const string BugReportUploadInstruction = "请在设置中点击“上传问题包”提交日志。";
    public static string BugReportUploadInstructionRichText
        => $"[color={Palette.WarningHex}]{BugReportUploadInstruction}[/color]";

    public static class Spacing
    {
        public const int Xxs = 2;
        public const int Xs = 4;
        public const int Sm = 8;
        public const int Md = 12;
        public const int Lg = 16;
    }

    public static class Radius
    {
        public const int Small = 6;
        public const int Medium = 8;
        public const int Pill = 10;
        public const int Large = 12;
    }

    public static class Type
    {
        public const int Title = 15;
        public const int Metric = 14;
        public const int Body = 13;
        public const int Caption = 12;
        public const int Outline = 2;
    }

    public static class Size
    {
        public const float PanelMargin = 24f;
        public const float ExpandedMaxWidth = 820f;
        public const float ExpandedMaxHeight = 440f;
        public const float ExpandedMinWidth = 560f;
        public const float CollapsedWidth = 520f;
        public const float CollapsedHeight = 120f;
        public const float RouteViewportHeight = 148f;
        public const float RouteViewportHeightWithDetails = 96f;
        public const float RouteRowHeight = 44f;
        public const float ActionPillHeight = 28f;
        public const float TurnColumnWidth = 88f;
        public const float OutcomeColumnWidth = 146f;
        public const float ButtonHeight = 34f;
    }

    public static class Palette
    {
        public static readonly Color Background = Color.FromHtml("101216f5");
        public static readonly Color Surface = Color.FromHtml("191c22fa");
        public static readonly Color SurfaceRaised = Color.FromHtml("23272ffb");
        public static readonly Color SurfaceHover = Color.FromHtml("2b3039ff");
        public static readonly Color Border = Color.FromHtml("3a404aeb");
        public static readonly Color BorderSubtle = Color.FromHtml("2a2f38cc");
        public static readonly Color Accent = Color.FromHtml("5c9fc7ff");
        public static readonly Color AccentHover = Color.FromHtml("73b4d8ff");
        public static readonly Color TextPrimary = Color.FromHtml("eef1f6ff");
        public static readonly Color TextSecondary = Color.FromHtml("b8c0ccff");
        public static readonly Color TextMuted = Color.FromHtml("858f9fff");
        public static readonly Color TextOutline = Color.FromHtml("080a0de6");
        public static readonly Color Warning = Color.FromHtml("d6a34cff");
        public static readonly Color Danger = Color.FromHtml("e26666ff");
        public static readonly Color Success = Color.FromHtml("69b77dff");
        public static readonly Color Positive = Color.FromHtml("34764fff");
        public static readonly Color PositiveHover = Color.FromHtml("428d60ff");

        public static readonly Color Attack = Color.FromHtml("d96363ff");
        public static readonly Color AttackBackground = Color.FromHtml("2b171bf8");
        public static readonly Color Skill = Color.FromHtml("5b91d1ff");
        public static readonly Color SkillBackground = Color.FromHtml("172235f8");
        public static readonly Color Power = Color.FromHtml("d7a84fff");
        public static readonly Color PowerBackground = Color.FromHtml("2b2417f8");
        public static readonly Color Negative = Color.FromHtml("9b70c9ff");
        public static readonly Color NegativeBackground = Color.FromHtml("241b30f8");
        public static readonly Color Potion = Color.FromHtml("55b9a5ff");
        public static readonly Color PotionBackground = Color.FromHtml("152a27f8");
        public static readonly Color KillBackground = Color.FromHtml("14291ffb");

        public const string TextSecondaryHex = "#b8c0cc";
        public const string TextMutedHex = "#858f9f";
        public const string AccentHex = "#5c9fc7";
        public const string WarningHex = "#d6a34c";
        public const string DangerHex = "#e26666";
        public const string SuccessHex = "#69b77d";
    }

    public static StyleBoxFlat CreateBox(
        Color background,
        Color border,
        int radius = Radius.Medium,
        int horizontalPadding = Spacing.Sm,
        int verticalPadding = Spacing.Sm,
        int borderWidth = 1,
        bool shadow = false)
    {
        return new StyleBoxFlat
        {
            BgColor = background,
            BorderColor = border,
            BorderWidthLeft = borderWidth,
            BorderWidthTop = borderWidth,
            BorderWidthRight = borderWidth,
            BorderWidthBottom = borderWidth,
            CornerRadiusTopLeft = radius,
            CornerRadiusTopRight = radius,
            CornerRadiusBottomRight = radius,
            CornerRadiusBottomLeft = radius,
            ContentMarginLeft = horizontalPadding,
            ContentMarginTop = verticalPadding,
            ContentMarginRight = horizontalPadding,
            ContentMarginBottom = verticalPadding,
            ShadowColor = shadow ? new Color(0f, 0f, 0f, 0.52f) : Godot.Colors.Transparent,
            ShadowSize = shadow ? 10 : 0,
        };
    }

    public static Label CreateLabel(
        string text,
        int fontSize,
        Color color,
        FontType fontType = FontType.Regular,
        int outlineSize = Type.Outline,
        Color? outlineColor = null)
    {
        Label label = new()
        {
            Text = text,
            VerticalAlignment = VerticalAlignment.Center,
            SizeFlagsVertical = Control.SizeFlags.ShrinkCenter,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        label.AddThemeFontSizeOverride("font_size", fontSize);
        label.AddThemeColorOverride("font_color", color);
        ApplyTextOutline(label, outlineSize, outlineColor);
        label.ApplyLocaleFontSubstitution(fontType, "font");
        return label;
    }

    public static RichTextLabel CreateRichText(
        int fontSize,
        int outlineSize = Type.Outline,
        Color? outlineColor = null)
    {
        RichTextLabel label = new()
        {
            BbcodeEnabled = true,
            FitContent = false,
            ScrollActive = false,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            VerticalAlignment = VerticalAlignment.Center,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        label.AddThemeFontSizeOverride("normal_font_size", fontSize);
        label.AddThemeFontSizeOverride("bold_font_size", fontSize);
        label.AddThemeFontSizeOverride("italics_font_size", fontSize);
        label.AddThemeFontSizeOverride("bold_italics_font_size", fontSize);
        label.AddThemeFontSizeOverride("mono_font_size", fontSize);
        label.AddThemeColorOverride("default_color", Palette.TextPrimary);
        ApplyTextOutline(label, outlineSize, outlineColor);
        label.ApplyLocaleFontSubstitution(FontType.Regular, "normal_font");
        label.ApplyLocaleFontSubstitution(FontType.Bold, "bold_font");
        label.ApplyLocaleFontSubstitution(FontType.Italic, "italics_font");
        return label;
    }

    public static Button CreateButton(string text, SolverButtonStyle style)
    {
        Button button = new()
        {
            Text = text,
            FocusMode = Control.FocusModeEnum.None,
            MouseDefaultCursorShape = Control.CursorShape.PointingHand,
            CustomMinimumSize = new Vector2(0, Size.ButtonHeight),
        };
        button.AddThemeFontSizeOverride("font_size", Type.Body);
        button.AddThemeColorOverride("font_color", Palette.TextPrimary);
        button.AddThemeColorOverride("font_hover_color", Godot.Colors.White);
        button.AddThemeColorOverride("font_pressed_color", Godot.Colors.White);
        button.AddThemeColorOverride("font_disabled_color", Palette.TextMuted);
        ApplyTextOutline(button);
        ApplyButtonStyle(button, style);
        return button;
    }

    public static void ApplyButtonStyle(Button button, SolverButtonStyle style)
    {
        (Color background, Color border, Color hover) = style switch
        {
            SolverButtonStyle.Primary => (Palette.Accent.Darkened(0.12f), Palette.Accent.Lightened(0.08f), Palette.AccentHover),
            SolverButtonStyle.Positive => (Palette.Positive, Palette.Success, Palette.PositiveHover),
            SolverButtonStyle.Danger => (Palette.Danger.Darkened(0.22f), Palette.Danger, Palette.Danger.Lightened(0.08f)),
            _ => (Palette.SurfaceRaised, Palette.Border, Palette.SurfaceHover),
        };
        button.AddThemeStyleboxOverride("normal", CreateBox(background, border, Radius.Medium, Spacing.Sm, Spacing.Xs));
        button.AddThemeStyleboxOverride("hover", CreateBox(hover, border.Lightened(0.12f), Radius.Medium, Spacing.Sm, Spacing.Xs));
        button.AddThemeStyleboxOverride("pressed", CreateBox(background.Darkened(0.16f), border, Radius.Medium, Spacing.Sm, Spacing.Xs));
        button.AddThemeStyleboxOverride("disabled", CreateBox(Palette.Background, Palette.BorderSubtle, Radius.Medium, Spacing.Sm, Spacing.Xs));
        button.ApplyLocaleFontSubstitution(
            style is SolverButtonStyle.Primary or SolverButtonStyle.Positive or SolverButtonStyle.Danger
                ? FontType.Bold
                : FontType.Regular,
            "font");
    }

    public static void ApplyTextOutline(
        Control control,
        int outlineSize = Type.Outline,
        Color? outlineColor = null)
    {
        if (outlineSize <= 0)
            return;
        control.AddThemeConstantOverride("outline_size", outlineSize);
        control.AddThemeColorOverride("font_outline_color", outlineColor ?? Palette.TextOutline);
    }
}
