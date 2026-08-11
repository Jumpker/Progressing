using System.Windows.Media;

namespace Progressing.Core;

/// <summary>
/// 全局配色常量：进度条灰底、文字 / 指针 / 标注默认色、内置 10 色鲜明色池（产品设计书 §5）。
/// 桌面进度条配色与 Resources/Theme.xaml 的 UI 令牌保持同族（现代蓝 #1677FF）。
/// </summary>
public static class Palettes
{
    /// <summary>进度条默认灰底（中性冷灰，衬托彩色段）。</summary>
    public static readonly Color Track = FromHex("#DEE1E7");

    /// <summary>备注文字 / 指针 / 时间标注默认色（中性石板灰）。</summary>
    public static readonly Color Ink = FromHex("#4E5969");

    /// <summary>时间标注中性灰。</summary>
    public static readonly Color LabelGray = FromHex("#86909C");

    /// <summary>编辑模式高亮轮廓色（品牌现代蓝）。</summary>
    public static readonly Color EditHighlight = FromHex("#1677FF");

    /// <summary>内置色池（10 色，鲜明多色系：珊瑚红 → 玫红，按色相环分布）。</summary>
    public static readonly IReadOnlyList<Color> Pool =
    [
        FromHex("#F06B5A"), // 珊瑚红
        FromHex("#F59A3C"), // 蜜橙
        FromHex("#E0A800"), // 金盏黄
        FromHex("#6BC047"), // 青柠绿
        FromHex("#2FB578"), // 翠绿
        FromHex("#21B5B5"), // 湖青
        FromHex("#38A0F0"), // 天蓝
        FromHex("#5B6DF0"), // 靛蓝
        FromHex("#8B6FE8"), // 紫罗兰
        FromHex("#E25A9C"), // 玫红
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
