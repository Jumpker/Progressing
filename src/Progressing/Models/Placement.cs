using System.Text.Json.Serialization;

namespace Progressing.Models;

/// <summary>
/// 位置配置：preset 为一次性快捷定位（选中即解析为 X/Y 并复位 null）；
/// 任何拖拽 / 微调 / 坐标输入都会将 preset 置空（见技术实现说明书约定 16）。
/// 坐标一律使用虚拟屏幕坐标（DIP）。
/// </summary>
public class Placement
{
    /// <summary>一次性预设；null 表示自定义定位。</summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public PlacementPreset? Preset { get; set; }

    /// <summary>目标显示器标识（选择 preset 时临时生效；null 表示主显示器）。</summary>
    public string? MonitorId { get; set; }

    public double X { get; set; }

    public double Y { get; set; }

    /// <summary>深拷贝。</summary>
    public Placement Clone() => new()
    {
        Preset = Preset,
        MonitorId = MonitorId,
        X = X,
        Y = Y,
    };

    public static Placement Default() => new() { Preset = null, MonitorId = null, X = 0, Y = 0 };
}
