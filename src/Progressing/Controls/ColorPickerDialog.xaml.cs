using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using Progressing.Core;

namespace Progressing.Controls;

/// <summary>
/// 通用颜色选择弹窗（产品设计书 §3.8.3）：
/// 系统预置色池（色块，点击即选）→ 自定义（色相滑块 + 饱和度/明度方块 + HEX 输入）→ 最近使用（≤10）。
/// 自定义颜色在确认时记入"最近使用"，全局持久化。
/// </summary>
public partial class ColorPickerDialog : Window
{
    /// <summary>最终选中的颜色；取消时为 null。</summary>
    public Color? SelectedColor { get; private set; }

    private readonly List<string> _recent;
    private bool _syncing;

    private double _hue;
    private double _sat = 0.5;
    private double _val = 1.0;
    private bool _draggingSb;

    private ColorDialogSwatch? _poolVm;
    private ColorDialogSwatch? _recentVm;

    public ColorPickerDialog(Color initial, List<string> recentColors)
    {
        InitializeComponent();
        _recent = recentColors;

        // 系统色池
        _poolVm = new ColorDialogSwatch();
        PoolList.ItemsSource = _poolVm.Items;
        for (var i = 0; i < Palettes.Pool.Count; i++)
        {
            var color = Palettes.Pool[i];
            _poolVm.Add(color, () => Select(Palettes.ToHex(color)));
        }

        // 最近使用
        _recentVm = new ColorDialogSwatch();
        RecentList.ItemsSource = _recentVm.Items;
        foreach (var hex in recentColors)
        {
            var c = Palettes.FromHex(hex);
            _recentVm.Add(c, () => Select(hex));
        }

        // 初始颜色
        var (h, s, v) = RgbToHsv(initial);
        _hue = h;
        _sat = s;
        _val = v;
        _syncing = true;
        HueSlider.Value = h;
        _syncing = false;
        Refresh();
    }

    /// <summary>弹窗取色：返回选中的颜色或 null。</summary>
    public static Color? Pick(Window owner, Color initial, List<string> recentColors)
    {
        var dialog = new ColorPickerDialog(initial, recentColors)
        {
            Owner = owner,
        };
        return dialog.ShowDialog() == true ? dialog.SelectedColor : null;
    }

    /// <summary>拖拽标题栏移动窗口（无边框窗口的手动拖动支持）。</summary>
    private void DragStrip_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Left || IsOnInteractiveElement(e.OriginalSource))
            return;
        DragMove();
    }

    /// <summary>原点击目标是否位于可交互控件上（按钮 / 输入框等），是则不触发窗口拖动。</summary>
    private static bool IsOnInteractiveElement(object source)
    {
        for (var d = source as DependencyObject; d is not null; d = VisualTreeHelper.GetParent(d))
        {
            if (d is ButtonBase or TextBoxBase)
                return true;
        }

        return false;
    }

    private void Select(string hex)
    {
        SelectedColor = Palettes.FromHex(hex);
        DialogResult = true;
    }

    private void HueSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_syncing || !IsLoaded)
            return;

        _hue = HueSlider.Value;
        Refresh();
    }

    private void SbCanvas_MouseDown(object sender, MouseButtonEventArgs e)
    {
        _draggingSb = true;
        UpdateSb(e.GetPosition(SbCanvas));
        SbCanvas.CaptureMouse();
    }

    private void SbCanvas_MouseMove(object sender, MouseEventArgs e)
    {
        if (_draggingSb)
            UpdateSb(e.GetPosition(SbCanvas));
    }

    private void SbCanvas_MouseUp(object sender, MouseButtonEventArgs e)
    {
        _draggingSb = false;
        SbCanvas.ReleaseMouseCapture();
    }

    private void UpdateSb(Point pos)
    {
        _sat = Math.Clamp(pos.X / SbCanvas.ActualWidth, 0, 1);
        _val = Math.Clamp(1 - pos.Y / SbCanvas.ActualHeight, 0, 1);
        Refresh();
    }

    private void HexInput_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        if (_syncing)
            return;

        var text = HexInput.Text.TrimStart('#');
        if (text.Length == 6 && byte.TryParse(text[..2], System.Globalization.NumberStyles.HexNumber, null, out _))
        {
            var color = Palettes.FromHex(text);
            var (h, s, v) = RgbToHsv(color);
            _hue = h;
            _sat = s;
            _val = v;
            _syncing = true;
            HueSlider.Value = h;
            _syncing = false;
            Refresh();
        }
    }

    private void Refresh()
    {
        var color = HsvToRgb(_hue, _sat, _val);

        SbBase.Fill = new SolidColorBrush(HsvToRgb(_hue, 1.0, 1.0));
        SbMarker.Visibility = Visibility.Visible;
        var markerX = _sat * Math.Max(0, SbCanvas.ActualWidth - 10);
        var markerY = (1 - _val) * Math.Max(0, SbCanvas.ActualHeight - 10);
        Canvas.SetLeft(SbMarker, markerX);
        Canvas.SetTop(SbMarker, markerY);

        PreviewSwatch.Background = new SolidColorBrush(color);
        _syncing = true;
        var hex = Palettes.ToHex(color);
        if (HexInput.Text != hex)
            HexInput.Text = hex;
        _syncing = false;
    }

    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        if (HexInput.Text.TrimStart('#') is { Length: 6 } hex6
            && byte.TryParse(hex6[..2], System.Globalization.NumberStyles.HexNumber, null, out _))
        {
            var hex = "#" + hex6;
            SelectedColor = Palettes.FromHex(hex);
            AddRecent(hex);
        }

        DialogResult = SelectedColor is not null;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }

    /// <summary>记入最近使用：去重移前，最多 10 个。</summary>
    private void AddRecent(string hex)
    {
        _recent.RemoveAll(c => string.Equals(c, hex, StringComparison.OrdinalIgnoreCase));
        _recent.Insert(0, hex);
        if (_recent.Count > 10)
            _recent.RemoveRange(10, _recent.Count - 10);
    }

    private static (double h, double s, double v) RgbToHsv(Color c)
    {
        var r = c.R / 255.0;
        var g = c.G / 255.0;
        var b = c.B / 255.0;
        var max = Math.Max(r, Math.Max(g, b));
        var min = Math.Min(r, Math.Min(g, b));
        var d = max - min;

        double h = 0;
        if (d > 0)
        {
            if (max == r) h = 60 * (((g - b) / d) % 6);
            else if (max == g) h = 60 * ((b - r) / d + 2);
            else h = 60 * ((r - g) / d + 4);
        }

        if (h < 0) h += 360;
        var s = max == 0 ? 0 : d / max;
        return (h, s, max);
    }

    private static Color HsvToRgb(double h, double s, double v)
    {
        var c = v * s;
        var x = c * (1 - Math.Abs((h / 60.0) % 2 - 1));
        var m = v - c;

        (double r, double g, double b) = h switch
        {
            < 60 => (c, x, 0.0),
            < 120 => (x, c, 0.0),
            < 180 => (0.0, c, x),
            < 240 => (0.0, x, c),
            < 300 => (x, 0.0, c),
            _ => (c, 0.0, x),
        };

        return Color.FromRgb(
            (byte)Math.Round((r + m) * 255),
            (byte)Math.Round((g + m) * 255),
            (byte)Math.Round((b + m) * 255));
    }

    /// <summary>色块数据项（池色 / 最近使用共用）。</summary>
    private sealed class ColorDialogSwatch
    {
        public List<SwatchItem> Items { get; } = new();

        public void Add(Color color, Action select)
            => Items.Add(new SwatchItem { Hex = Palettes.ToHex(color), Brush = new SolidColorBrush(color), Select = select });
    }

    private sealed class SwatchItem
    {
        public required string Hex { get; init; }

        public required Brush Brush { get; init; }

        public required Action Select { get; init; }

        public ICommand SelectCommand => new DelegateCommand(Select);
    }

    private sealed class DelegateCommand : ICommand
    {
        private readonly Action _action;

        public DelegateCommand(Action action) => _action = action;

        // CanExecute 恒为 true，无需对外通知
        public event EventHandler? CanExecuteChanged { add { } remove { } }

        public bool CanExecute(object? parameter) => true;

        public void Execute(object? parameter) => _action();
    }
}
