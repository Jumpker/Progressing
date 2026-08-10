using System.Text.Json.Serialization;

namespace Progressing.Models;

/// <summary>
/// 备注颜色的来源与取用方式：
/// Source=Pool 时用 PoolIndex 索引预置色池；Source=Custom 时用 CustomHex 自定义颜色。
/// AssignedBy 仅对 Source=Pool 有意义，用于区分"随机取用占用色池配额"与"手动选定不占配额"。
/// </summary>
public class SegmentColor
{
    /// <summary>颜色来源：预置色池 / 自定义。</summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public ColorSource Source { get; set; } = ColorSource.Pool;

    /// <summary>预置色池索引；Source=Custom 时为 null。</summary>
    public int? PoolIndex { get; set; }

    /// <summary>取用方式：随机 / 手动；仅 Source=Pool 时有意义。</summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public ColorAssignedBy AssignedBy { get; set; } = ColorAssignedBy.Random;

    /// <summary>深拷贝。</summary>
    public SegmentColor Clone() => new()
    {
        Source = Source,
        PoolIndex = PoolIndex,
        AssignedBy = AssignedBy,
    };
}

/// <summary>
/// 时间段备注：一段起止 hh:mm + 文案 + 颜色。同一进度条内不允许重叠（首尾相接允许）。
/// </summary>
public class SegmentNote
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    /// <summary>起始 hh:mm（00:00 ~ 23:59，须 start &lt; end）。</summary>
    public string Start { get; set; } = "00:00";

    /// <summary>结束 hh:mm（须 start &lt; end）。</summary>
    public string End { get; set; } = "00:00";

    /// <summary>文案；允许为空串。</summary>
    public string Text { get; set; } = "";

    /// <summary>颜色信息。</summary>
    public SegmentColor? Color { get; set; }

    /// <summary>Source=Custom 时的 HEX 色值；Pool 时为 null。</summary>
    public string? CustomHex { get; set; }

    /// <summary>起始时刻（解析 Start，失败回退 00:00）。</summary>
    [JsonIgnore]
    public TimeSpan StartTime => ParseTime(Start);

    /// <summary>结束时刻（解析 End，失败回退 00:00）。</summary>
    [JsonIgnore]
    public TimeSpan EndTime => ParseTime(End);

    /// <summary>解析 hh:mm；非法输入返回 00:00。</summary>
    private static TimeSpan ParseTime(string s)
        => TimeSpan.TryParseExact(s, "hh\\:mm", null, out var t) ? t : TimeSpan.Zero;

    /// <summary>深拷贝。</summary>
    public SegmentNote Clone() => new()
    {
        Id = Id,
        Start = Start,
        End = End,
        Text = Text,
        Color = Color?.Clone(),
        CustomHex = CustomHex,
    };
}
