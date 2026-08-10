using System.Text.Json.Serialization;

namespace Progressing.Models;

/// <summary>
/// 备注文字样式：基准方向 / 排列方向 / 大小 / 颜色 / 文字边框。
/// </summary>
public class TextStyleConfig
{
    /// <summary>基准方向（上 / 下 / 左 / 右）。</summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public TextAnchor Anchor { get; set; } = TextAnchor.Top;

    /// <summary>排列方向（横排 / 竖排）。</summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public TextArrangement Arrangement { get; set; } = TextArrangement.Horizontal;

    /// <summary>文字大小（DIP）。</summary>
    public double FontSize { get; set; } = 26.0;

    public string Color { get; set; } = "#2D82BC";

    /// <summary>文字边框；默认开启、黑色。</summary>
    public BorderConfig Border { get; set; } = BorderConfig.TextDefault();

    /// <summary>深拷贝。</summary>
    public TextStyleConfig Clone() => new()
    {
        Anchor = Anchor,
        Arrangement = Arrangement,
        FontSize = FontSize,
        Color = Color,
        Border = Border.Clone(),
    };

    public static TextStyleConfig Default() => new()
    {
        Anchor = TextAnchor.Top,
        Arrangement = TextArrangement.Horizontal,
        FontSize = 26.0,
        Color = "#2D82BC",
        Border = BorderConfig.TextDefault(),
    };
}
