using System.Windows;
using System.Windows.Media;
using Progressing.Models;

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

    /// <summary>
    /// 绘制彩色段：圆头仅出现在进度条两端，与相邻时间段相接处为直角（避免胶囊圆头露出轨道缝隙）。
    /// 填充使用沿横截面的轻微渐变，增强现代质感。
    /// </summary>
    public static void DrawSegment(DrawingContext dc, Rect segmentRect, Color color,
        BarOrientation orientation, bool roundStart, bool roundEnd)
    {
        if (segmentRect.Width <= 0 && segmentRect.Height <= 0)
            return;

        // 圆头半径取短边的一半，竖放段才不会画成橄榄形
        var radius = Math.Min(segmentRect.Width, segmentRect.Height) / 2;

        // 四个圆角半径：相接处为直角（0），进度条两端为胶囊圆头
        double rTL = 0, rTR = 0, rBR = 0, rBL = 0;
        if (orientation == BarOrientation.Horizontal)
        {
            if (roundStart) { rTL = radius; rBL = radius; }
            if (roundEnd) { rTR = radius; rBR = radius; }
        }
        else
        {
            if (roundStart) { rTL = radius; rTR = radius; }
            if (roundEnd) { rBL = radius; rBR = radius; }
        }

        var brush = CreateSegmentBrush(color, orientation);
        DrawRectWithCorners(dc, segmentRect, brush, null, rTL, rTR, rBR, rBL);
    }

    /// <summary>沿进度条横截面的轻微渐变（横放：上亮下暗；竖放：左亮右暗），营造柔和现代感。</summary>
    private static Brush CreateSegmentBrush(Color color, BarOrientation orientation)
    {
        var light = Lighten(color, 0.07);
        var dark = Darken(color, 0.07);
        var brush = new LinearGradientBrush(
            light,
            dark,
            orientation == BarOrientation.Horizontal ? 90 : 0);
        brush.Freeze();
        return brush;
    }

    private static Color Lighten(Color c, double factor)
        => Color.FromRgb(
            (byte)(c.R + (255 - c.R) * factor),
            (byte)(c.G + (255 - c.G) * factor),
            (byte)(c.B + (255 - c.B) * factor));

    private static Color Darken(Color c, double factor)
        => Color.FromRgb(
            (byte)(c.R * (1 - factor)),
            (byte)(c.G * (1 - factor)),
            (byte)(c.B * (1 - factor)));

    /// <summary>绘制四个圆角可独立配置的矩形（0 = 直角）。</summary>
    private static void DrawRectWithCorners(DrawingContext dc, Rect rect, Brush fill, Pen? stroke,
        double rTL, double rTR, double rBR, double rBL)
    {
        var x = rect.X;
        var y = rect.Y;
        var w = rect.Width;
        var h = rect.Height;

        var geometry = new StreamGeometry();
        using (var ctx = geometry.Open())
        {
            ctx.BeginFigure(new Point(x + rTL, y), true, true);
            ctx.LineTo(new Point(x + w - rTR, y), true, false);
            if (rTR > 0)
                ctx.ArcTo(new Point(x + w, y + rTR), new Size(rTR, rTR), 0, false, SweepDirection.Clockwise, true, false);
            ctx.LineTo(new Point(x + w, y + h - rBR), true, false);
            if (rBR > 0)
                ctx.ArcTo(new Point(x + w - rBR, y + h), new Size(rBR, rBR), 0, false, SweepDirection.Clockwise, true, false);
            ctx.LineTo(new Point(x + rBL, y + h), true, false);
            if (rBL > 0)
                ctx.ArcTo(new Point(x, y + h - rBL), new Size(rBL, rBL), 0, false, SweepDirection.Clockwise, true, false);
            ctx.LineTo(new Point(x, y + rTL), true, false);
            if (rTL > 0)
                ctx.ArcTo(new Point(x + rTL, y), new Size(rTL, rTL), 0, false, SweepDirection.Clockwise, true, false);
        }

        geometry.Freeze();
        dc.DrawGeometry(fill, stroke, geometry);
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
