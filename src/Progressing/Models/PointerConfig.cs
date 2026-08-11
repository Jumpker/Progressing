using System.Text.Json.Serialization;

namespace Progressing.Models;

/// <summary>
/// 指针配置：图标来源 / 自定义文件路径 / 大小。
/// 内置箭头方向由 BarWindow 按"时间增长方向"自动决定（横放向右、竖放向下，镜像反向），无需配置。
/// </summary>
public class PointerConfig
{
    /// <summary>图标来源：内置 / 自定义文件。</summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public PointerSource Source { get; set; } = PointerSource.Builtin;

    /// <summary>自定义 PNG / SVG 文件路径；Source 为 Builtin 时为 null。</summary>
    public string? FilePath { get; set; }

    /// <summary>指针显示尺寸（建议 16 ~ 32px）。</summary>
    public double Size { get; set; } = 38.0;

    /// <summary>深拷贝。</summary>
    public PointerConfig Clone() => new()
    {
        Source = Source,
        FilePath = FilePath,
        Size = Size,
    };

    public static PointerConfig Default() => new()
    {
        Source = PointerSource.Builtin,
        FilePath = null,
        Size = 38.0,
    };
}
