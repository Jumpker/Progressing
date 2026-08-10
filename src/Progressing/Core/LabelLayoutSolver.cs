using System.Windows;

namespace Progressing.Core;

/// <summary>
/// 时间段起止时间标注的避让布局（产品设计书 §3.3.4 / 技术实现说明书 §3.8）。
/// 纯几何计算，无文本测量依赖：调用方负责把文本测成包围盒尺寸传入。
/// 坐标系为进度条本地坐标：横放时轴 = X（0..Length），竖放时轴 = Y。
/// </summary>
public static class LabelLayoutSolver
{
    public const double DefaultFontSize = 10.0;
    public const double ReducedFontSize = 8.0;
    public const double MaxSeparateShift = 8.0;   // 互相重叠时反向错开的最大位移
    public const double PointerSeparateShift = 6.0; // 与指针重叠时的错开位移
    public const double Margin = 3.0;             // 边界收拢边距
    public const double LabelGap = 3.0;           // 标注与进度条边缘的间距（横放在上方 / 竖放在左侧）

    public sealed class Input
    {
        public required double StartWidth { get; init; }
        public required double StartHeight { get; init; }
        public required double EndWidth { get; init; }
        public required double EndHeight { get; init; }
        public required double StartPos { get; init; }
        public required double EndPos { get; init; }
        public required double AxisLength { get; init; }
        public Rect PointerRect { get; init; }
        public bool IsVertical { get; init; }
        public bool Mirrored { get; init; }
        public double MarginValue { get; init; } = Margin;
    }

    public sealed class Output
    {
        public required Rect StartRect { get; init; }
        public required Rect EndRect { get; init; }
        public required double FontSize { get; init; }
    }

    public static Output Solve(Input input)
    {
        var dir = input.Mirrored ? -1.0 : 1.0; // 时间递增方向（沿轴）
        var font = DefaultFontSize;

        var startRect = Clamp(MakeRect(input, input.StartPos, input.StartWidth, input.StartHeight), input);
        var endRect = Clamp(MakeRect(input, input.EndPos, input.EndWidth, input.EndHeight), input);

        // 步骤 2：两标注互相重叠 → 先反向错开，仍重叠则缩小字号重排
        if (startRect.IntersectsWith(endRect))
        {
            startRect = Clamp(OffsetAxis(startRect, -dir * MaxSeparateShift, input.IsVertical), input);
            endRect = Clamp(OffsetAxis(endRect, +dir * MaxSeparateShift, input.IsVertical), input);
        }

        if (startRect.IntersectsWith(endRect))
        {
            font = ReducedFontSize;
            var scale = ReducedFontSize / DefaultFontSize;
            startRect = Clamp(MakeRect(input, input.StartPos, input.StartWidth * scale, input.StartHeight * scale), input);
            endRect = Clamp(MakeRect(input, input.EndPos, input.EndWidth * scale, input.EndHeight * scale), input);
        }

        // 步骤 3：与指针重叠 → 沿指针运动方向的反方向错开
        if (!input.PointerRect.IsEmpty)
        {
            if (startRect.IntersectsWith(input.PointerRect))
                startRect = Clamp(OffsetAxis(startRect, -dir * PointerSeparateShift, input.IsVertical), input);
            if (endRect.IntersectsWith(input.PointerRect))
                endRect = Clamp(OffsetAxis(endRect, -dir * PointerSeparateShift, input.IsVertical), input);
        }

        return new Output { StartRect = startRect, EndRect = endRect, FontSize = font };
    }

    /// <summary>以轴位置为中心、位于进度条外侧（横放上方 / 竖放左侧）构建标注包围盒。</summary>
    private static Rect MakeRect(Input input, double axisPos, double w, double h)
    {
        return input.IsVertical
            ? new Rect(-w - LabelGap, axisPos - h / 2, w, h)
            : new Rect(axisPos - w / 2, -h - LabelGap, w, h);
    }

    /// <summary>沿轴平移。</summary>
    private static Rect OffsetAxis(Rect rect, double delta, bool isVertical)
        => isVertical ? new Rect(rect.X, rect.Y + delta, rect.Width, rect.Height)
                      : new Rect(rect.X + delta, rect.Y, rect.Width, rect.Height);

    /// <summary>沿轴方向收入 [Margin, AxisLength - Margin]，保证标注整体不越界。</summary>
    private static Rect Clamp(Rect rect, Input input)
    {
        if (input.IsVertical)
        {
            var minY = input.MarginValue;
            var maxY = input.AxisLength - input.MarginValue - rect.Height;
            var y = Math.Min(Math.Max(rect.Y, minY), Math.Max(minY, maxY));
            return new Rect(rect.X, y, rect.Width, rect.Height);
        }

        var minX = input.MarginValue;
        var maxX = input.AxisLength - input.MarginValue - rect.Width;
        var x = Math.Min(Math.Max(rect.X, minX), Math.Max(minX, maxX));
        return new Rect(x, rect.Y, rect.Width, rect.Height);
    }
}
