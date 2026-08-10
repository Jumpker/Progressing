using System.Text.Json.Serialization;

namespace Progressing.Models;

/// <summary>
/// 指针配置：图标来源 / 指向方向（横放与竖放分别存储，切换方向互不干扰）/ 大小。
/// </summary>
public class PointerConfig
{
    /// <summary>图标来源：内置 / 自定义文件。</summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public PointerSource Source { get; set; } = PointerSource.Builtin;

    /// <summary>自定义 PNG / SVG 文件路径；Source 为 Builtin 时为 null。</summary>
    public string? FilePath { get; set; }

    /// <summary>指针显示尺寸（建议 16 ~ 32px）。</summary>
    public double Size { get; set; } = 16.0;

    /// <summary>横放时生效的指向。</summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public PointerDirection HorizontalDirection { get; set; } = PointerDirection.Up;

    /// <summary>竖放时生效的指向。</summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public PointerDirection VerticalDirection { get; set; } = PointerDirection.Left;

    /// <summary>深拷贝。</summary>
    public PointerConfig Clone() => new()
    {
        Source = Source,
        FilePath = FilePath,
        Size = Size,
        HorizontalDirection = HorizontalDirection,
        VerticalDirection = VerticalDirection,
    };

    public static PointerConfig Default() => new()
    {
        Source = PointerSource.Builtin,
        FilePath = null,
        Size = 16.0,
        HorizontalDirection = PointerDirection.Up,
        VerticalDirection = PointerDirection.Left,
    };
}
