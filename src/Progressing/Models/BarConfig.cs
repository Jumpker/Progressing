using System.Text.Json.Serialization;

namespace Progressing.Models;

/// <summary>
/// 单条进度条的完整配置：外观 / 位置 / 指针 / 备注 / 文字样式 / 随机色占用记录。
/// </summary>
public class BarConfig
{
    /// <summary>实例唯一标识。</summary>
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    public string Name { get; set; } = "进度条 1";

    /// <summary>显示 / 隐藏（隐藏即停：逻辑全部停止）。</summary>
    public bool Visible { get; set; } = true;

    /// <summary>方向。</summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public BarOrientation Orientation { get; set; } = BarOrientation.Horizontal;

    /// <summary>镜像：以进度条中心为对称轴翻转时间轴。</summary>
    public bool Mirrored { get; set; }

    /// <summary>长度（200 ~ 2000 DIP）。</summary>
    public double Length { get; set; } = 600.0;

    /// <summary>宽度 / 厚度（2 ~ 10 DIP）。</summary>
    public double Width { get; set; } = 4.0;

    /// <summary>透明度（0 ~ 100，默认 100）。</summary>
    public int Opacity { get; set; } = 100;

    /// <summary>置顶。</summary>
    public bool Topmost { get; set; } = true;

    /// <summary>边框。</summary>
    public BorderConfig Border { get; set; } = BorderConfig.Default();

    /// <summary>位置。</summary>
    public Placement Placement { get; set; } = Placement.Default();

    /// <summary>指针。</summary>
    public PointerConfig Pointer { get; set; } = PointerConfig.Default();

    /// <summary>时间段备注（不重叠；首尾相接允许）。</summary>
    public List<SegmentNote> Notes { get; set; } = new();

    /// <summary>随机取色占用记录（每实例独立）。</summary>
    public List<int> ColorPoolUsed { get; set; } = new();

    /// <summary>备注文字样式。</summary>
    public TextStyleConfig TextStyle { get; set; } = TextStyleConfig.Default();

    /// <summary>文字容器拖拽偏移（DIP），切锚点时保留。</summary>
    public Point2D TextOffset { get; set; } = new();

    /// <summary>深拷贝。</summary>
    public BarConfig Clone() => new()
    {
        Id = Id,
        Name = Name,
        Visible = Visible,
        Orientation = Orientation,
        Mirrored = Mirrored,
        Length = Length,
        Width = Width,
        Opacity = Opacity,
        Topmost = Topmost,
        Border = Border.Clone(),
        Placement = Placement.Clone(),
        Pointer = Pointer.Clone(),
        Notes = Notes.Select(n => n.Clone()).ToList(),
        ColorPoolUsed = new List<int>(ColorPoolUsed),
        TextStyle = TextStyle.Clone(),
        TextOffset = new Point2D { X = TextOffset.X, Y = TextOffset.Y },
    };

    public static BarConfig Default() => new()
    {
        Id = Guid.NewGuid().ToString("N"),
        Name = "进度条 1",
        Visible = true,
        Orientation = BarOrientation.Horizontal,
        Mirrored = false,
        Length = 600.0,
        Width = 4.0,
        Opacity = 100,
        Topmost = true,
        Border = BorderConfig.Default(),
        // 默认吸附主屏底部居中（一次性预设：启动定位后解析为 X/Y 并复位）
        Placement = new Placement { Preset = PlacementPreset.BottomCenter },
        Pointer = PointerConfig.Default(),
        Notes = new List<SegmentNote>(),
        ColorPoolUsed = new List<int>(),
        TextStyle = TextStyleConfig.Default(),
        TextOffset = new Point2D(),
    };
}

/// <summary>2D 偏移（文字容器拖拽偏移等）。</summary>
public class Point2D
{
    public double X { get; set; }

    public double Y { get; set; }
}
