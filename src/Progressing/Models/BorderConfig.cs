namespace Progressing.Models;

/// <summary>
/// 边框配置：开关 / 颜色 / 宽度（默认 1px）。
/// </summary>
public class BorderConfig
{
    public bool Enabled { get; set; }

    public string Color { get; set; } = "#000000";

    public double Width { get; set; } = 1.0;

    /// <summary>深拷贝。</summary>
    public BorderConfig Clone() => new()
    {
        Enabled = Enabled,
        Color = Color,
        Width = Width,
    };

    public static BorderConfig Default() => new() { Enabled = false, Color = "#000000", Width = 1.0 };

    /// <summary>备注文字边框默认：开启、黑色。</summary>
    public static BorderConfig TextDefault() => new() { Enabled = true, Color = "#000000", Width = 1.0 };
}
