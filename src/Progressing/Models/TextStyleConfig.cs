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
    public double FontSize { get; set; } = 28.9;

    /// <summary>文字加粗。</summary>
    public bool Bold { get; set; }

    public string Color { get; set; } = "#4D9DDA";

    /// <summary>文字边框；默认开启、黑色。</summary>
    public BorderConfig Border { get; set; } = BorderConfig.TextDefault();

    /// <summary>深拷贝。</summary>
    public TextStyleConfig Clone() => new()
    {
        Anchor = Anchor,
        Arrangement = Arrangement,
        FontSize = FontSize,
        Bold = Bold,
        Color = Color,
        Border = Border.Clone(),
    };

    public static TextStyleConfig Default() => new()
    {
        Anchor = TextAnchor.Top,
        Arrangement = TextArrangement.Horizontal,
        FontSize = 28.9,
        Bold = false,
        Color = "#4D9DDA",
        Border = BorderConfig.TextDefault(),
    };
}
