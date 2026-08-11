using System.Text.Json.Serialization;

namespace Progressing.Models;

/// <summary>进度条方向（横放 / 竖放）。</summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum BarOrientation
{
    Horizontal,
    Vertical,
}

/// <summary>镜像开关的布尔语义由 BarConfig.Mirrored 表达；此处无额外枚举。</summary>

/// <summary>备注文字基准方向（相对进度条）。</summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum TextAnchor
{
    Top,
    Bottom,
    Left,
    Right,
}

/// <summary>备注文字排列方向（横排 / 竖排）。</summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum TextArrangement
{
    Horizontal,
    Vertical,
}

/// <summary>指针图标来源。</summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum PointerSource
{
    Builtin,
    File,
}

/// <summary>备注颜色来源：预置色池 / 自定义颜色。</summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ColorSource
{
    Pool,
    Custom,
}

/// <summary>颜色取用方式：随机取色 / 手动选定。</summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ColorAssignedBy
{
    Random,
    Manual,
}

/// <summary>位置预设（一次性定位并固化，见技术实现说明书约定 16）。</summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum PlacementPreset
{
    TopCenter,
    BottomCenter,
    LeftCenter,
    RightCenter,
}

/// <summary>界面主题模式：跟随系统 / 浅色 / 深色。</summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum AppTheme
{
    System,
    Light,
    Dark,
}
