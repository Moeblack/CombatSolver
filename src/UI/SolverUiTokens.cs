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
    public const string ParallelSearchFailureInstruction =
        "请先在设置中点击“上传问题包”提交日志，再将“搜索并行度”改为“关闭（单线程）”后重试。";
    public static string SearchFailureInstructionRichText(bool parallelSearchWasEnabled)
        => $"[color={Palette.WarningHex}]" +
           $"{(parallelSearchWasEnabled ? ParallelSearchFailureInstruction : BugReportUploadInstruction)}[/color]";
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
        public const int Small = 4;
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
        public const int Outline = 0;
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
        public const float ButtonHeight = 32f;
    }

    public static class Palette
    {
        // 浅色主题
        public static readonly Color Background = Color.FromHtml("f3f3f3ff");
        public static readonly Color Surface = Color.FromHtml("ffffffff");
        public static readonly Color SurfaceRaised = Color.FromHtml("f7f9fbff");
        public static readonly Color SurfaceHover = Color.FromHtml("efefefff");
        public static readonly Color Border = Color.FromHtml("e0e0e0ff");
        public static readonly Color BorderSubtle = Color.FromHtml("eaeaeaff");
        public static readonly Color Accent = Color.FromHtml("0078d4ff");
        public static readonly Color AccentHover = Color.FromHtml("106ebeff");
        public static readonly Color TextPrimary = Color.FromHtml("1b1b1bff");
        public static readonly Color TextSecondary = Color.FromHtml("5f5f5fff");
        public static readonly Color TextMuted = Color.FromHtml("8a8a8aff");
        public static readonly Color TextOutline = Color.FromHtml("ffffff00");
        public static readonly Color Warning = Color.FromHtml("9d5d00ff");
        public static readonly Color Danger = Color.FromHtml("c42b1cff");
        public static readonly Color Success = Color.FromHtml("0f7b0fff");
        public static readonly Color Positive = Color.FromHtml("107c10ff");
        public static readonly Color PositiveHover = Color.FromHtml("0e6e0eff");

        public static readonly Color Attack = Color.FromHtml("c42b1cff");
        public static readonly Color AttackBackground = Color.FromHtml("fce9e7ff");
        public static readonly Color Skill = Color.FromHtml("0067c0ff");
        public static readonly Color SkillBackground = Color.FromHtml("eaf2faff");
        public static readonly Color Power = Color.FromHtml("9d5d00ff");
        public static readonly Color PowerBackground = Color.FromHtml("fbf1dcff");
        public static readonly Color Negative = Color.FromHtml("6b4fa0ff");
        public static readonly Color NegativeBackground = Color.FromHtml("f1ebf8ff");
        public static readonly Color Potion = Color.FromHtml("00786cff");
        public static readonly Color PotionBackground = Color.FromHtml("e5f4f1ff");
        public static readonly Color KillBackground = Color.FromHtml("e7f4ecff");

        public const string TextSecondaryHex = "#5f5f5f";
        public const string TextMutedHex = "#8a8a8a";
        public const string AccentHex = "#0078d4";
        public const string WarningHex = "#9d5d00";
        public const string DangerHex = "#c42b1c";
        public const string SuccessHex = "#0f7b0f";
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
            ShadowColor = shadow ? new Color(0f, 0f, 0f, 0.14f) : Godot.Colors.Transparent,
            ShadowSize = shadow ? 16 : 0,
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
        button.AddThemeColorOverride("font_hover_color", Palette.TextPrimary);
        button.AddThemeColorOverride("font_pressed_color", Palette.TextPrimary);
        button.AddThemeColorOverride("font_disabled_color", Palette.TextMuted);
        ApplyTextOutline(button);
        ApplyButtonStyle(button, style);
        return button;
    }

    public static void ApplyButtonStyle(Button button, SolverButtonStyle style)
    {
        (Color background, Color border, Color hover, Color pressed, Color font) = style switch
        {
            SolverButtonStyle.Primary => (
                Palette.Accent,
                Palette.Accent,
                Palette.AccentHover,
                Palette.Accent.Darkened(0.22f),
                Godot.Colors.White),
            SolverButtonStyle.Positive => (
                Palette.Positive,
                Palette.Positive,
                Palette.PositiveHover,
                Palette.Positive.Darkened(0.18f),
                Godot.Colors.White),
            SolverButtonStyle.Danger => (
                Palette.Danger,
                Palette.Danger,
                Palette.Danger.Lightened(0.08f),
                Palette.Danger.Darkened(0.18f),
                Godot.Colors.White),
            _ => (
                Palette.Surface,
                Color.FromHtml("8a8a8aff"),
                Palette.SurfaceHover,
                Palette.Border,
                Palette.TextPrimary),
        };
        button.AddThemeStyleboxOverride("normal", CreateBox(background, border, Radius.Small, Spacing.Sm, Spacing.Xs));
        button.AddThemeStyleboxOverride("hover", CreateBox(hover, border, Radius.Small, Spacing.Sm, Spacing.Xs));
        button.AddThemeStyleboxOverride("pressed", CreateBox(pressed, border, Radius.Small, Spacing.Sm, Spacing.Xs));
        button.AddThemeStyleboxOverride("disabled", CreateBox(Palette.Background, Palette.BorderSubtle, Radius.Small, Spacing.Sm, Spacing.Xs));
        button.AddThemeColorOverride("font_color", font);
        button.AddThemeColorOverride("font_hover_color", font);
        button.AddThemeColorOverride("font_pressed_color", font);
        button.ApplyLocaleFontSubstitution(
            style is SolverButtonStyle.Primary or SolverButtonStyle.Positive or SolverButtonStyle.Danger
                ? FontType.Bold
                : FontType.Regular,
            "font");
    }

    public static Texture2D CreateCircleTexture(Color color, int size = 12)
    {
        Image image = Image.CreateEmpty(size, size, false, Image.Format.Rgba8);
        float center = (size - 1) / 2f;
        float radius = center;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = x - center;
                float dy = y - center;
                if (dx * dx + dy * dy <= radius * radius)
                    image.SetPixel(x, y, color);
            }
        }
        return ImageTexture.CreateFromImage(image);
    }

    public static Texture2D CreateChevronTexture(Color color, int width = 10, int height = 5)
    {
        Image image = Image.CreateEmpty(width, height, false, Image.Format.Rgba8);
        int half = width / 2;
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                if (x >= half - 1 - y && x <= half + y)
                    image.SetPixel(x, y, color);
            }
        }
        return ImageTexture.CreateFromImage(image);
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

