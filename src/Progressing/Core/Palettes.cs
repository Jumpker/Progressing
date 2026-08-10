using System.Windows.Media;

namespace Progressing.Core;

/// <summary>
/// 全局配色常量：进度条灰底、文字 / 指针 / 标注默认色、内置 10 色柔和色池（产品设计书 §5）。
/// </summary>
public static class Palettes
{
    /// <summary>进度条默认灰底（暖灰）。</summary>
    public static readonly Color Track = FromHex("#E3E0DB");

    /// <summary>备注文字 / 指针 / 时间标注默认色（石板灰）。</summary>
    public static readonly Color Ink = FromHex("#5A5A5A");

    /// <summary>时间标注中性灰。</summary>
    public static readonly Color LabelGray = FromHex("#8A8A8A");

    /// <summary>编辑模式高亮轮廓色。</summary>
    public static readonly Color EditHighlight = FromHex("#4A90D9");

    /// <summary>内置色池（10 色，低饱和中高明度马卡龙色系）。</summary>
    public static readonly IReadOnlyList<Color> Pool =
    [
        FromHex("#F7C9BE"), // 蜜桃粉
        FromHex("#E3C4C4"), // 玫瑰粉
        FromHex("#F5C29E"), // 杏橙
        FromHex("#FBF0C2"), // 奶油黄
        FromHex("#C9EDE1"), // 薄荷绿
        FromHex("#B9C7A0"), // 鼠尾草绿
        FromHex("#CFE7F5"), // 天蓝
        FromHex("#A9D3EA"), // 雾蓝
        FromHex("#D9CBF2"), // 薰衣草
        FromHex("#C4B3E0"), // 丁香紫
    ];

    public static Color FromHex(string hex)
    {
        hex = hex.TrimStart('#');
        if (hex.Length != 6)
            return Colors.Gray;
        var r = Convert.ToByte(hex.Substring(0, 2), 16);
        var g = Convert.ToByte(hex.Substring(2, 2), 16);
        var b = Convert.ToByte(hex.Substring(4, 2), 16);
        return Color.FromRgb(r, g, b);
    }

    public static string ToHex(Color c) => $"#{c.R:X2}{c.G:X2}{c.B:X2}";
}
