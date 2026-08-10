using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Progressing.Controls;
using Progressing.Core;
using Progressing.Models;
using Progressing.Services;

namespace Progressing.Windows;

/// <summary>
/// 进度条实例窗口 = 透明置顶窗口 + 一份 BarConfig + 1s 调度器（技术实现说明书 §3.5）。
/// 职责：
/// - 按配置自绘并布局（轨道 / 指针 / 文字容器 / 时间标注）；
/// - 每秒补间指针、切换生效备注、跨日重置（隐藏即停）；
/// - 位置编辑模式：临时取消鼠标穿透 + 强制不透明 + 高亮，可拖窗口与文字容器。
/// </summary>
public partial class BarWindow : Window
{
    private const double TextGap = 6.0;         // 文字容器与指针 / 进度条之间的间距
    private const double EditModePadding = 240.0; // 编辑模式下四周留白，供文字容器自由拖动而不被窗口裁剪

    private static readonly TimeSpan TickInterval = TimeSpan.FromSeconds(1);

    private readonly TimeService _timeService;
    private readonly DispatcherTimer _timer;
    private readonly PointerAnimator _animator;
    private readonly BarControl _bar;

    private bool _editMode;
    private bool _isDraggingText;
    private Point _dragStartScreen;
    private double _textOffsetStartX;
    private double _textOffsetStartY;
    private SegmentNote? _activeNote;

    /// <summary>本条进度条的配置（与 ConfigService 中的对象同引用）。</summary>
    public BarConfig Config { get; }

    /// <summary>是否处于位置编辑模式。</summary>
    public bool IsEditMode => _editMode;

    /// <summary>编辑模式进入 / 退出通知（设置窗口据此切换按钮文案）。</summary>
    public event EventHandler? EditModeChanged;

    /// <summary>轨道在窗口内的偏移（计算指针包围盒用）。</summary>
    private double BarOffsetX => Canvas.GetLeft(BarHost);

    private double BarOffsetY => Canvas.GetTop(BarHost);

    public BarWindow(BarConfig config, TimeService timeService)
    {
        InitializeComponent();

        Config = config;
        _timeService = timeService;

        _animator = new PointerAnimator(PointerTranslate, config.Orientation == BarOrientation.Horizontal);
        _bar = BarHost;

        _timer = new DispatcherTimer { Interval = TickInterval };
        _timer.Tick += OnTick;

        KeyDown += OnWindowKeyDown;
        MouseLeftButtonDown += OnWindowMouseDown;
        MouseMove += OnWindowMouseMove;
        MouseLeftButtonUp += OnWindowMouseUp;
        LostMouseCapture += (_, _) => EndDrag();

        ApplyConfig();
        ApplyInitialPlacement();
        ApplyConfig(); // 位置确定后按显示器/尺寸重新布局
    }

    #region 布局与配置

    /// <summary>
    /// 依据当前配置重建窗口尺寸与元素布局。位置（Left/Top）不在此处改动——
    /// 位置由 ApplyInitialPlacement / 预设 / 拖拽编辑负责。
    /// </summary>
    public void ApplyConfig()
    {
        var c = Config;
        var isHorizontal = c.Orientation == BarOrientation.Horizontal;
        var textBand = TextBandHeight(c.TextStyle);
        var labelBand = LabelLayoutSolver.DefaultFontSize * 1.5 + LabelLayoutSolver.LabelGap;
        var pad = _editMode ? EditModePadding : 0;

        // 四周预留空间：文字带（按锚点）+ 时间标注带（横放标注在上 / 竖放标注在左）；
        // 编辑模式再叠加四周留白，保证文字容器可拖到任意位置且不被窗口裁剪
        var topSpace = (isHorizontal && c.TextStyle.Anchor == TextAnchor.Top ? textBand + TextGap : 0)
                     + (isHorizontal ? labelBand : 0) + pad;
        var bottomSpace = (isHorizontal && c.TextStyle.Anchor == TextAnchor.Bottom ? textBand + TextGap : 0) + pad;
        var leftSpace = (!isHorizontal && c.TextStyle.Anchor == TextAnchor.Left ? textBand + TextGap : 0)
                      + (!isHorizontal ? labelBand : 0) + pad;
        var rightSpace = (!isHorizontal && c.TextStyle.Anchor == TextAnchor.Right ? textBand + TextGap : 0) + pad;

        var length = Math.Clamp(c.Length, 200, 2000);

        if (isHorizontal)
        {
            Width = length;
            Height = topSpace + c.Width + bottomSpace;
            Canvas.SetLeft(BarHost, 0);
            Canvas.SetTop(BarHost, topSpace);
        }
        else
        {
            Width = leftSpace + c.Width + rightSpace;
            Height = length;
            Canvas.SetLeft(BarHost, leftSpace);
            Canvas.SetTop(BarHost, 0);
        }

        SetupPointer(isHorizontal, topSpace, bottomSpace, leftSpace, rightSpace);
        ApplyNoteContainerStyle();
        PositionNoteContainer();

        // 外观
        Opacity = _editMode ? 1.0 : c.Opacity / 100.0;
        Topmost = c.Topmost;
        Win32WindowHelper.SetClickThrough(this, !_editMode);

        _bar.Bind(c);
        _bar.SetEditMode(_editMode);

        // 可见性 → 隐藏即停
        if (c.Visible)
        {
            Show();
            _timer.Start();
        }
        else
        {
            _timer.Stop();
            Hide();
        }

        // 配置变更后指针跳到当前时刻（不播动画，避免跳变感）
        JumpPointerToNow();
    }

    /// <summary>应用初始 / 预设位置（仅首帧与预设命令调用）。
    /// 带一次性预设时解析为目标显示器坐标；旧配置坐标从未设置（0,0）时吸附主屏底部居中。</summary>
    public void ApplyInitialPlacement()
    {
        var p = Config.Placement;
        if (p.Preset is { } preset)
        {
            ApplyPreset(preset, p.MonitorId);
        }
        else if (p.X == 0 && p.Y == 0)
        {
            // 从未定位过的旧配置（默认角点）：迁移吸附到主屏底部居中
            ApplyPreset(PlacementPreset.BottomCenter, null);
        }
        else
        {
            Left = p.X;
            Top = p.Y;
        }
    }

    /// <summary>应用一次性位置预设：解析为目标显示器上的坐标并固化（清空 Preset）。</summary>
    public void ApplyPreset(PlacementPreset preset, string? monitorId)
    {
        var monitor = FindMonitor(monitorId);
        var x = Config.Placement.X;
        var y = Config.Placement.Y;

        // 物理像素 → DIP
        var scale = monitor.DpiScale;
        var wa = monitor.WorkArea;
        var waX = Win32WindowHelper.PxToDip(wa.X, scale);
        var waY = Win32WindowHelper.PxToDip(wa.Y, scale);
        var waW = Win32WindowHelper.PxToDip(wa.Width, scale);
        var waH = Win32WindowHelper.PxToDip(wa.Height, scale);

        switch (preset)
        {
            case PlacementPreset.TopCenter:
                x = waX + (waW - Width) / 2;
                y = waY;
                break;
            case PlacementPreset.BottomCenter:
                x = waX + (waW - Width) / 2;
                y = waY + waH - Height;
                break;
            case PlacementPreset.LeftCenter:
                x = waX;
                y = waY + (waH - Height) / 2;
                break;
            case PlacementPreset.RightCenter:
                x = waX + waW - Width;
                y = waY + (waH - Height) / 2;
                break;
        }

        Config.Placement.X = x;
        Config.Placement.Y = y;
        Config.Placement.Preset = null;
        Left = x;
        Top = y;
    }

    private void SetupPointer(bool isHorizontal, double topSpace, double bottomSpace, double leftSpace, double rightSpace)
    {
        var c = Config;
        var size = c.Pointer.Size;
        PointerImage.Width = size;
        PointerImage.Height = size;

        if (c.Pointer.Source == PointerSource.Builtin)
        {
            PointerImage.Source = IconService.LoadBuiltinPointer();
            // 内置实心水滴尖端朝上（0° 基准），旋转使尖端指向目标方向：
            // 横放 Up/Down → 0°/180°，竖放 Left/Right → -90°/90°
            PointerRotate.Angle = isHorizontal
                ? (c.Pointer.HorizontalDirection == PointerDirection.Up ? 0 : 180)
                : (c.Pointer.VerticalDirection == PointerDirection.Left ? -90 : 90);
        }
        else
        {
            PointerImage.Source = IconService.LoadFile(c.Pointer.FilePath) ?? IconService.LoadBuiltinPointer();
            PointerRotate.Angle = 0; // 自定义图片始终保持原始方向
        }

        // 指针与进度条重叠：跨轴居中压在进度条上
        if (isHorizontal)
        {
            var barY = topSpace;
            Canvas.SetLeft(PointerImage, 0); // X 由补间平移驱动
            Canvas.SetTop(PointerImage, barY + (c.Width - size) / 2);
        }
        else
        {
            var barX = leftSpace;
            Canvas.SetLeft(PointerImage, barX + (c.Width - size) / 2);
            Canvas.SetTop(PointerImage, 0); // Y 由补间平移驱动
        }

        PointerImage.Visibility = Visibility.Visible;
    }

    private void ApplyNoteContainerStyle()
    {
        var c = Config;
        NoteText.Foreground = FreezeBrush(c.TextStyle.Color);
        NoteText.FontSize = c.TextStyle.FontSize;

        var borderEnabled = c.TextStyle.Border.Enabled;
        NoteContainer.BorderBrush = borderEnabled ? FreezeBrush(c.TextStyle.Border.Color) : null;
        NoteContainer.BorderThickness = borderEnabled ? new Thickness(c.TextStyle.Border.Width) : new Thickness(0);

        // 排列方向：竖排 = 顺时针旋转 90°（自上而下阅读）
        NoteText.LayoutTransform = c.TextStyle.Arrangement == TextArrangement.Vertical
            ? new RotateTransform(90)
            : null;
    }

    /// <summary>
    /// 定位文字容器：基准方向 + 拖拽偏移叠加（产品设计书 §3.3.3）。
    /// 默认偏移 0 时：横放上方居中 / 竖放左侧垂直居中。
    /// </summary>
    private void PositionNoteContainer()
    {
        var c = Config;
        NoteContainer.UpdateLayout();
        var w = NoteContainer.ActualWidth;
        var h = NoteContainer.ActualHeight;

        var isHorizontal = c.Orientation == BarOrientation.Horizontal;
        var barX = BarOffsetX;
        var barY = BarOffsetY;
        var ox = c.TextOffset.X;
        var oy = c.TextOffset.Y;

        double x, y;
        if (isHorizontal)
        {
            switch (c.TextStyle.Anchor)
            {
                case TextAnchor.Top:
                {
                    var above = barY - TextGap;
                    x = (Width - w) / 2 + ox;
                    y = above - h + oy;
                    break;
                }

                case TextAnchor.Bottom:
                {
                    var below = barY + c.Width + TextGap;
                    x = (Width - w) / 2 + ox;
                    y = below + oy;
                    break;
                }

                case TextAnchor.Left:
                    x = barX - TextGap - w + ox;
                    y = barY + c.Width / 2 - h / 2 + oy;
                    break;

                default: // Right
                    x = barX + c.Length + TextGap + ox;
                    y = barY + c.Width / 2 - h / 2 + oy;
                    break;
            }
        }
        else
        {
            switch (c.TextStyle.Anchor)
            {
                case TextAnchor.Left:
                {
                    var left = barX - TextGap;
                    x = left - w + ox;
                    y = barY + (Height - h) / 2 + oy;
                    break;
                }

                case TextAnchor.Right:
                {
                    var right = barX + c.Width + TextGap;
                    x = right + ox;
                    y = barY + (Height - h) / 2 + oy;
                    break;
                }

                case TextAnchor.Top:
                    x = barX + c.Width / 2 - w / 2 + ox;
                    y = barY - TextGap - h + oy;
                    break;

                default: // Bottom
                    x = barX + c.Width / 2 - w / 2 + ox;
                    y = barY + c.Length + TextGap + oy;
                    break;
            }
        }

        Canvas.SetLeft(NoteContainer, x);
        Canvas.SetTop(NoteContainer, y);
    }

    #endregion

    #region 运行时调度

    private void OnTick(object? sender, EventArgs e)
    {
        if (!Config.Visible)
            return;

        if (_timeService.ConsumeDayChanged())
        {
            ResetDay();
            return;
        }

        var now = _timeService.TimeOfDay;
        var target = TimeMapper.Map(now, Config.Length, Config.Mirrored) - Config.Pointer.Size / 2;
        _animator.AnimateTo(target);

        var active = Config.Notes.FirstOrDefault(n => n.StartTime <= now && now < n.EndTime);
        SetActiveNote(active);
    }

    /// <summary>指针跳到当前时刻（配置变更 / 启动时调用，不播动画）。</summary>
    private void JumpPointerToNow()
    {
        var target = TimeMapper.Map(_timeService.TimeOfDay, Config.Length, Config.Mirrored) - Config.Pointer.Size / 2;
        _animator.JumpTo(target);
    }

    /// <summary>每日重置：指针归位 0:00，备注 / 时间标注清空（逻辑照常运行）。</summary>
    public void ResetDay()
    {
        var target = TimeMapper.Map(TimeSpan.Zero, Config.Length, Config.Mirrored) - Config.Pointer.Size / 2;
        _animator.JumpTo(target);
        SetActiveNote(null);
    }

    /// <summary>切换当前生效备注并刷新文字容器与时间标注。</summary>
    private void SetActiveNote(SegmentNote? note)
    {
        _activeNote = note;

        if (note is null)
        {
            NoteText.Text = "";
            if (!_editMode)
                NoteContainer.Visibility = Visibility.Collapsed;
            _bar.UpdateActive(null, Rect.Empty);
            return;
        }

        NoteText.Text = note.Text;
        NoteContainer.Visibility = Visibility.Visible;
        PositionNoteContainer();
        _bar.UpdateActive(note, ComputePointerRectInBar());
    }

    /// <summary>指针包围盒（进度条本地坐标，供时间标注避让）。</summary>
    private Rect ComputePointerRectInBar()
    {
        var size = Config.Pointer.Size;
        var isHorizontal = Config.Orientation == BarOrientation.Horizontal;
        return isHorizontal
            ? new Rect(_animator.Current, Canvas.GetTop(PointerImage) - BarOffsetY, size, size)
            : new Rect(Canvas.GetLeft(PointerImage) - BarOffsetX, _animator.Current, size, size);
    }

    /// <summary>手动刷新当前帧（设置变更后调用，保证文字 / 标注即时更新）。</summary>
    public void RefreshFrame()
    {
        if (!Config.Visible)
            return;

        var now = _timeService.TimeOfDay;
        var active = Config.Notes.FirstOrDefault(n => n.StartTime <= now && now < n.EndTime);
        SetActiveNote(active);
    }

    #endregion

    #region 位置编辑模式

    /// <summary>进入位置编辑模式：取消鼠标穿透、强制不透明、高亮、文字容器强制显示。</summary>
    public void EnterEditMode()
    {
        if (_editMode)
            return;

        // 记录进入前进度条的屏幕位置，展开留白后仍保持原位
        var barScreenX = Left + BarOffsetX;
        var barScreenY = Top + BarOffsetY;

        _editMode = true;
        TextDragHandle.Visibility = Visibility.Visible;
        if (_activeNote is null)
        {
            NoteText.Text = "文字容器（拖动调整显示位置）";
            NoteContainer.Visibility = Visibility.Visible;
        }

        ApplyConfig(); // 含编辑留白 / 取消穿透 / 强制不透明 / 高亮

        // 进度条保持在进入前的屏幕位置（窗口向左上平移一个留白）
        Left = barScreenX - BarOffsetX;
        Top = barScreenY - BarOffsetY;

        // 让本窗口可接收键盘（Esc 退出编辑）
        Activate();
        Root.Focus();
        Root.Focusable = true;
        Keyboard.Focus(Root);

        EditModeChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>退出位置编辑模式：恢复穿透、透明度、隐藏手柄，落盘位置与偏移。</summary>
    public void ExitEditMode()
    {
        if (!_editMode)
            return;

        // 记录退出前进度条的屏幕位置，恢复紧凑尺寸后仍保持原位
        var barScreenX = Left + BarOffsetX;
        var barScreenY = Top + BarOffsetY;

        _editMode = false;
        TextDragHandle.Visibility = Visibility.Collapsed;

        ApplyConfig(); // 恢复紧凑尺寸 / 穿透 / 透明度 / 取消高亮

        // 进度条保持在编辑结束时的屏幕位置
        Left = barScreenX - BarOffsetX;
        Top = barScreenY - BarOffsetY;

        // 窗口收紧后把文字容器约束回可视区域，避免被裁切消失
        ClampNoteIntoWindow();
        if (_activeNote is null)
            NoteContainer.Visibility = Visibility.Collapsed;

        // 落盘位置与文字偏移
        Config.Placement.X = Left;
        Config.Placement.Y = Top;
        Config.Placement.Preset = null;

        EditModeChanged?.Invoke(this, EventArgs.Empty);
    }

    private void OnWindowKeyDown(object sender, KeyEventArgs e)
    {
        if (_editMode && e.Key == Key.Escape)
        {
            e.Handled = true;
            ExitEditMode();
        }
    }

    private void OnWindowMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (!_editMode || e.ChangedButton != MouseButton.Left)
            return;

        var pos = e.GetPosition(this);
        if (IsOnNoteContainer(pos))
        {
            _isDraggingText = true;
            _textOffsetStartX = Config.TextOffset.X;
            _textOffsetStartY = Config.TextOffset.Y;
            _dragStartScreen = e.GetPosition(null);
            CaptureMouse();
            e.Handled = true;
        }
        else
        {
            // 拖整条进度条：交给系统原生拖拽。透明分层窗口逐帧设 Left/Top 会整窗重合成，
            // 导致频闪 / 重影（旧帧残留 = "两个进度条"）；DragMove 仅移动窗口位图，平滑无闪。
            // DragMove 阻塞到松开左键后返回，此时窗口已在最终位置。
            DragMove();
            Config.Placement.X = Left;
            Config.Placement.Y = Top;
            Config.Placement.Preset = null;
            e.Handled = true;
        }
    }

    private void OnWindowMouseMove(object sender, MouseEventArgs e)
    {
        if (!_editMode || !_isDraggingText)
            return;

        var current = e.GetPosition(null);
        var dx = current.X - _dragStartScreen.X;
        var dy = current.Y - _dragStartScreen.Y;

        Config.TextOffset.X = _textOffsetStartX + dx;
        Config.TextOffset.Y = _textOffsetStartY + dy;
        ClampNoteIntoWindow(); // 约束在窗口可视区域内，拖出边界也不会消失
        e.Handled = true;
    }

    private void OnWindowMouseUp(object sender, MouseButtonEventArgs e)
        => EndDrag();

    private void EndDrag()
    {
        if (!_isDraggingText)
            return;

        _isDraggingText = false;
        if (IsMouseCaptured)
            ReleaseMouseCapture();
    }

    /// <summary>把文字容器约束在窗口可视区域内（拖拽 / 退出编辑时调用），避免移出窗口被裁剪消失。</summary>
    private void ClampNoteIntoWindow()
    {
        if (NoteContainer.Visibility != Visibility.Visible)
            return;

        PositionNoteContainer();
        var x = Canvas.GetLeft(NoteContainer);
        var y = Canvas.GetTop(NoteContainer);
        var w = NoteContainer.ActualWidth;
        var h = NoteContainer.ActualHeight;

        var maxX = Math.Max(0.0, Width - w);
        var maxY = Math.Max(0.0, Height - h);
        var newX = Math.Clamp(x, 0.0, maxX);
        var newY = Math.Clamp(y, 0.0, maxY);
        if (Math.Abs(newX - x) < 0.001 && Math.Abs(newY - y) < 0.001)
            return;

        // 容器位置与 TextOffset 线性相关，差值直接叠加回偏移量
        Config.TextOffset.X += newX - x;
        Config.TextOffset.Y += newY - y;
        PositionNoteContainer();
    }

    private bool IsOnNoteContainer(Point windowPos)
    {
        if (NoteContainer.Visibility != Visibility.Visible)
            return false;

        var rect = new Rect(
            Canvas.GetLeft(NoteContainer),
            Canvas.GetTop(NoteContainer),
            NoteContainer.ActualWidth,
            NoteContainer.ActualHeight);
        return rect.Contains(windowPos);
    }

    #endregion

    private static Brush FreezeBrush(string hex)
    {
        var brush = new SolidColorBrush(Palettes.FromHex(hex));
        brush.Freeze();
        return brush;
    }

    /// <summary>文字容器（含边框）预留带高估算：随字体大小变化。</summary>
    private static double TextBandHeight(TextStyleConfig ts) => ts.FontSize * 1.5 + 8.0;

    private static Win32WindowHelper.MonitorInfo FindMonitor(string? deviceName)
    {
        var monitors = Win32WindowHelper.EnumerateMonitors();
        if (deviceName is not null)
        {
            var match = monitors.FirstOrDefault(m =>
                string.Equals(m.DeviceName, deviceName, StringComparison.OrdinalIgnoreCase));
            if (match is not null)
                return match;
        }

        // 未指定显示器时默认主显示器
        return Win32WindowHelper.PrimaryMonitor();
    }
}
