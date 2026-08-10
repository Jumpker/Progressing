using System.Windows;
using Progressing.Core;

namespace Progressing.Core.Tests;

/// <summary>时间标注避让：互叠反向错开→缩字号重排；与指针重叠错开；贴边收进边界。</summary>
public class LabelLayoutSolverTests
{
    private static LabelLayoutSolver.Input Input(
        double startPos, double endPos,
        double startW = 30, double startH = 12, double endW = 30, double endH = 12,
        double length = 600,
        Rect? pointer = null, bool vertical = false, bool mirrored = false)
        => new()
        {
            StartWidth = startW,
            StartHeight = startH,
            EndWidth = endW,
            EndHeight = endH,
            StartPos = startPos,
            EndPos = endPos,
            AxisLength = length,
            PointerRect = pointer ?? Rect.Empty,
            IsVertical = vertical,
            Mirrored = mirrored,
        };

    [Fact]
    public void NoOverlap_KeepsDefaultFontSize()
    {
        var result = LabelLayoutSolver.Solve(Input(100, 300));

        Assert.Equal(LabelLayoutSolver.DefaultFontSize, result.FontSize);
        Assert.False(result.StartRect.IntersectsWith(result.EndRect));
    }

    [Fact]
    public void OverlappingLabels_AreSeparatedWithoutShrink()
    {
        // 两标注位置紧贴（宽度 30，间距 20 → 必然重叠）
        var result = LabelLayoutSolver.Solve(Input(100, 120, startW: 30, endW: 30));

        Assert.Equal(LabelLayoutSolver.DefaultFontSize, result.FontSize);
        Assert.False(result.StartRect.IntersectsWith(result.EndRect));
    }

    [Fact]
    public void HeavyOverlap_ShrinksFontAndReflows()
    {
        // 严重重叠（错开 8px 仍重叠）：缩字号并重排（尽力而为，不保证彻底分离）
        var result = LabelLayoutSolver.Solve(Input(100, 110, startW: 40, endW: 40, startH: 12, endH: 12));

        Assert.Equal(LabelLayoutSolver.ReducedFontSize, result.FontSize);
        Assert.Equal(40 * (LabelLayoutSolver.ReducedFontSize / LabelLayoutSolver.DefaultFontSize), result.StartRect.Width, 3);
        Assert.Equal(40 * (LabelLayoutSolver.ReducedFontSize / LabelLayoutSolver.DefaultFontSize), result.EndRect.Width, 3);
    }

    [Fact]
    public void Clamps_KeepLabelsInsideAxis()
    {
        var result = LabelLayoutSolver.Solve(Input(0, 600, startW: 30, endW: 30));

        Assert.True(result.StartRect.X >= LabelLayoutSolver.Margin);
        Assert.True(result.EndRect.Right <= 600 - LabelLayoutSolver.Margin);
    }

    [Fact]
    public void PointerOverlap_OffsetOppositeToPointerMotion()
    {
        // 指针与进度条重叠（跨轴居中于 4px 厚轨道，中心 y=-6）；标注在上方，被指针覆盖时沿反方向错开
        var pointer = new Rect(285, -6, 16, 16);
        var result = LabelLayoutSolver.Solve(Input(300, 450, pointer: pointer));

        Assert.Equal(300 - 15 - LabelLayoutSolver.PointerSeparateShift, result.StartRect.X, 3);
        Assert.Equal(450 - 15, result.EndRect.X, 3); // 未与指针重叠的标注保持原位
        Assert.Equal(-12 - LabelLayoutSolver.LabelGap, result.StartRect.Y, 3); // 标注位于进度条上方
    }

    [Fact]
    public void VerticalAxis_WorksSymmetric()
    {
        var result = LabelLayoutSolver.Solve(Input(100, 300, vertical: true, startW: 12, startH: 30, endW: 12, endH: 30));

        Assert.False(result.StartRect.IntersectsWith(result.EndRect));
        Assert.True(result.StartRect.Y >= LabelLayoutSolver.Margin);
        Assert.True(result.EndRect.Bottom <= 600 - LabelLayoutSolver.Margin);
    }
}
