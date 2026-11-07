using Godot;

namespace LumiFeast.Game.UI;

/// <summary>Shared look of the prototype UI: outlined text and rounded panels.</summary>
public static class UiCommon
{
    public const float ScreenWidth = 1920;
    public const float ScreenHeight = 1080;
    private const int Corner = 16;
    private const float OutlinePerFontSize = 0.22f;

    /// <summary>Text with a black outline, readable on any background.</summary>
    public static Label OutlinedLabel(int fontSize, string text = "")
    {
        var label = new Label { Text = text };
        label.AddThemeFontSizeOverride("font_size", fontSize);
        label.AddThemeColorOverride("font_outline_color", Colors.Black);
        label.AddThemeConstantOverride("outline_size", Mathf.RoundToInt(fontSize * OutlinePerFontSize));
        return label;
    }

    public static StyleBoxFlat RoundPanel(Color color, float margin) => new()
    {
        BgColor = color,
        CornerRadiusTopLeft = Corner,
        CornerRadiusTopRight = Corner,
        CornerRadiusBottomLeft = Corner,
        CornerRadiusBottomRight = Corner,
        ContentMarginLeft = margin,
        ContentMarginTop = margin * 0.8f,
        ContentMarginRight = margin,
        ContentMarginBottom = margin * 0.8f,
    };
}
