using System.Windows;
using System.Windows.Media;

namespace Progressing.Core;

/// <summary>
/// 进度条绘制管线（技术实现说明书 §3.3）：轨道 → 彩色段 → 边框 → 时间标注。
/// 所有 Brush / Pen / Typeface 均 Freeze 缓存复用；坐标一律 DIP。
/// </summary>
public static class BarRenderer
{
    private static readonly Brush TrackBrush = Freeze(new SolidColorBrush(Palettes.Track));
    private static readonly Typeface LabelTypeface =
        new(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);

    /// <summary>绘制灰色轨道（胶囊形）。</summary>
    public static void DrawTrack(DrawingContext dc, Rect barRect)
    {
        // 圆头半径取短边的一半：横放 = 高/2，竖放 = 宽/2（否则竖放会画成两头尖的拉长橄榄形）
        var radius = Math.Min(barRect.Width, barRect.Height) / 2;
        dc.DrawRoundedRectangle(TrackBrush, null, barRect, radius, radius);
    }

    /// <summary>绘制彩色段（圆头、与轨道同厚；color 为 null 时跳过）。</summary>
    public static void DrawSegment(DrawingContext dc, Rect segmentRect, Color color, Pen? borderPen)
    {
        if (segmentRect.Width <= 0 && segmentRect.Height <= 0)
            return;

        var brush = Freeze(new SolidColorBrush(color));
        // 圆头半径取短边的一半，竖放段才不会画成橄榄形
        var radius = Math.Min(segmentRect.Width, segmentRect.Height) / 2;
        dc.DrawRoundedRectangle(brush, borderPen, segmentRect, radius, radius);
    }

    /// <summary>绘制轨道边框（外缘描边）。</summary>
    public static void DrawBorder(DrawingContext dc, Rect barRect, Color color, double thickness)
    {
        var pen = FreezePen(new SolidColorBrush(color), thickness);
        // 圆头半径取短边的一半，竖放时与轨道一致
        var radius = Math.Min(barRect.Width, barRect.Height) / 2;
        // 外缘描边：将矩形向外扩半线宽，使笔画居中于边缘
        var outer = new Rect(
            barRect.X - thickness / 2,
            barRect.Y - thickness / 2,
            barRect.Width + thickness,
            barRect.Height + thickness);
        dc.DrawRoundedRectangle(null, pen, outer, radius + thickness / 2, radius + thickness / 2);
    }

    /// <summary>绘制时间标注（水平居中于 rect）。</summary>
    public static void DrawTimeLabel(DrawingContext dc, string text, Rect rect, double fontSize)
    {
        if (string.IsNullOrEmpty(text))
            return;

        var formatted = new FormattedText(
            text,
            System.Globalization.CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            LabelTypeface,
            fontSize,
            Freeze(new SolidColorBrush(Palettes.LabelGray)),
            1.0);

        // 垂直居中
        var y = rect.Y + (rect.Height - formatted.Height) / 2;
        dc.DrawText(formatted, new Point(rect.X, y));
    }

    /// <summary>测量时间标注文本包围盒（供 LabelLayoutSolver 使用）。</summary>
    public static Size MeasureLabel(string text, double fontSize)
    {
        if (string.IsNullOrEmpty(text))
            return Size.Empty;

        var formatted = new FormattedText(
            text,
            System.Globalization.CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            LabelTypeface,
            fontSize,
            Freeze(new SolidColorBrush(Palettes.LabelGray)),
            1.0);

        return new Size(formatted.Width, formatted.Height);
    }

    private static Brush Freeze(Brush b)
    {
        b.Freeze();
        return b;
    }

    private static Pen FreezePen(Brush b, double thickness)
    {
        var p = new Pen(b, thickness);
        p.Freeze();
        return p;
    }
}
