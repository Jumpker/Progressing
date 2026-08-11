using System.Windows;
using System.Windows.Media;
using Progressing.Core;
using Progressing.Models;

namespace Progressing.Controls;

/// <summary>
/// 进度条自绘控件（纯代码 OnRender）。绘制顺序：轨道 → 彩色段 → 时间标注 → 边框 → 编辑模式高亮。
/// 控件本地坐标系：横放时轨道占 (0,0,Length,Width)，竖放时占 (0,0,Width,Length)。
/// 仅当配置变化时重绘（InvalidateVisual 由 BarWindow 按需触发）。
/// </summary>
public class BarControl : FrameworkElement
{
    private const double EditHighlightThickness = 2.0;

    private BarConfig? _config;
    private SegmentNote? _active;
    private Rect _pointerRect;
    private bool _editMode;

    /// <summary>绑定实例配置；配置变化时调用并重绘。</summary>
    public void Bind(BarConfig config)
    {
        _config = config;
        InvalidateVisual();
    }

    /// <summary>当前生效备注与指针包围盒（进度条本地坐标）；用于时间标注。</summary>
    public void UpdateActive(SegmentNote? active, Rect pointerRect)
    {
        _active = active;
        _pointerRect = pointerRect;
        InvalidateVisual();
    }

    /// <summary>编辑模式：显示高亮轮廓。</summary>
    public void SetEditMode(bool enabled)
    {
        _editMode = enabled;
        InvalidateVisual();
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        if (_config is null)
            return new Size(0, 0);

        return _config.Orientation == BarOrientation.Horizontal
            ? new Size(_config.Length, _config.Width)
            : new Size(_config.Width, _config.Length);
    }

    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);
        if (_config is null)
            return;

        var config = _config;
        var barRect = GetBarRect(config);

        // 1. 轨道
        BarRenderer.DrawTrack(dc, barRect);

        // 2. 彩色段（时间上相接的段之间无缝：相接处直角、仅进度条外端胶囊；不沿用每段独立描边）
        var notes = config.Notes;
        for (var i = 0; i < notes.Count; i++)
        {
            var note = notes[i];
            var color = ResolveNoteColor(note);
            var rect = GetSegmentRect(config, note);
            var (axisStart, axisEnd) = AxisExtent(rect, config.Orientation);

            // 镜像后时间顺序与空间顺序相反：物理左侧相邻的是"时间更晚"的段。
            // 相接判定基于时间（上一段结束 == 本段开始），与镜像无关。
            var adjacentLeft = config.Mirrored
                ? i + 1 < notes.Count && TimeNear(notes[i + 1].StartTime, note.EndTime)
                : i > 0 && TimeNear(notes[i - 1].EndTime, note.StartTime);
            var adjacentRight = config.Mirrored
                ? i > 0 && TimeNear(notes[i - 1].EndTime, note.StartTime)
                : i + 1 < notes.Count && TimeNear(notes[i + 1].StartTime, note.EndTime);

            // 圆头只在进度条两端（映射坐标 0 / Length）或自由端出现；相接处为直角
            var roundStart = axisStart <= 0.5 || !adjacentLeft;
            var roundEnd = axisEnd >= config.Length - 0.5 || !adjacentRight;

            // 相接处防抗锯齿接缝：后画的段盖住先画段伸出的部分。
            // 非镜像：时间序 = 空间序，延伸右端；镜像：时间序与空间序相反，延伸左端。
            if (config.Mirrored)
            {
                if (!roundStart)
                {
                    rect = config.Orientation == BarOrientation.Horizontal
                        ? new Rect(rect.X - 0.75, rect.Y, rect.Width + 0.75, rect.Height)
                        : new Rect(rect.X, rect.Y - 0.75, rect.Width, rect.Height + 0.75);
                }
            }
            else if (!roundEnd)
            {
                rect = config.Orientation == BarOrientation.Horizontal
                    ? new Rect(rect.X, rect.Y, rect.Width + 0.75, rect.Height)
                    : new Rect(rect.X, rect.Y, rect.Width, rect.Height + 0.75);
            }

            BarRenderer.DrawSegment(dc, rect, color, config.Orientation, roundStart, roundEnd);
        }

        // 3. 时间标注（指针命中时间段时）
        if (_active is not null)
            DrawTimeLabels(dc, config, _active);

        // 4. 边框（覆盖整体外缘，含彩色段）
        if (config.Border.Enabled)
            BarRenderer.DrawBorder(dc, barRect, Palettes.FromHex(config.Border.Color), config.Border.Width);

        // 5. 编辑模式高亮轮廓
        if (_editMode)
        {
            var highlightPen = FrozenPen(new SolidColorBrush(Palettes.EditHighlight), EditHighlightThickness);
            var outer = new Rect(
                barRect.X - EditHighlightThickness,
                barRect.Y - EditHighlightThickness,
                barRect.Width + EditHighlightThickness * 2,
                barRect.Height + EditHighlightThickness * 2);
            // 圆头半径取短边的一半，竖放时高亮轮廓才不会变成橄榄形
            var radius = Math.Min(outer.Width, outer.Height) / 2;
            dc.DrawRoundedRectangle(null, highlightPen, outer, radius, radius);
        }
    }

    private static Rect GetBarRect(BarConfig config)
        => config.Orientation == BarOrientation.Horizontal
            ? new Rect(0, 0, config.Length, config.Width)
            : new Rect(0, 0, config.Width, config.Length);

    /// <summary>段矩形沿进度条轴向的起止位置（横放取 X、竖放取 Y）。</summary>
    private static (double Start, double End) AxisExtent(Rect rect, BarOrientation orientation)
        => orientation == BarOrientation.Horizontal
            ? (rect.X, rect.X + rect.Width)
            : (rect.Y, rect.Y + rect.Height);

    /// <summary>两个时刻是否"首尾相接"（同一分钟，即 hh:mm 精度相等）。</summary>
    private static bool TimeNear(TimeSpan a, TimeSpan b)
        => (a - b).Duration() < TimeSpan.FromMinutes(1);

    private static Rect GetSegmentRect(BarConfig config, SegmentNote note)
    {
        var start = TimeMapper.Map(note.StartTime, config.Length, config.Mirrored);
        var end = TimeMapper.Map(note.EndTime, config.Length, config.Mirrored);
        var a = Math.Min(start, end);
        var b = Math.Max(start, end);

        return config.Orientation == BarOrientation.Horizontal
            ? new Rect(a, 0, Math.Max(0, b - a), config.Width)
            : new Rect(0, a, config.Width, Math.Max(0, b - a));
    }

    private void DrawTimeLabels(DrawingContext dc, BarConfig config, SegmentNote active)
    {
        var startText = active.Start;
        var endText = active.End;
        var startPos = TimeMapper.Map(active.StartTime, config.Length, config.Mirrored);
        var endPos = TimeMapper.Map(active.EndTime, config.Length, config.Mirrored);

        var startSize = BarRenderer.MeasureLabel(startText, LabelLayoutSolver.DefaultFontSize);
        var endSize = BarRenderer.MeasureLabel(endText, LabelLayoutSolver.DefaultFontSize);
        if (startSize.IsEmpty || endSize.IsEmpty)
            return;

        var result = LabelLayoutSolver.Solve(new LabelLayoutSolver.Input
        {
            StartWidth = startSize.Width,
            StartHeight = startSize.Height,
            EndWidth = endSize.Width,
            EndHeight = endSize.Height,
            StartPos = startPos,
            EndPos = endPos,
            AxisLength = config.Length,
            PointerRect = _pointerRect,
            IsVertical = config.Orientation == BarOrientation.Vertical,
            Mirrored = config.Mirrored,
        });

        BarRenderer.DrawTimeLabel(dc, startText, result.StartRect, result.FontSize);
        BarRenderer.DrawTimeLabel(dc, endText, result.EndRect, result.FontSize);
    }

    /// <summary>解析备注颜色：Custom 用自定义 HEX；Pool 用色池索引；非法回退灰底。</summary>
    public static Color ResolveNoteColor(SegmentNote note)
    {
        if (note.Color is { Source: ColorSource.Custom } && !string.IsNullOrWhiteSpace(note.CustomHex))
            return Palettes.FromHex(note.CustomHex);

        if (note.Color is { Source: ColorSource.Pool, PoolIndex: >= 0 } color
            && color.PoolIndex < Palettes.Pool.Count)
            return Palettes.Pool[color.PoolIndex.Value];

        return Palettes.Track;
    }

    private static Pen FrozenPen(Brush brush, double thickness)
    {
        brush.Freeze();
        var pen = new Pen(brush, thickness);
        pen.Freeze();
        return pen;
    }
}
