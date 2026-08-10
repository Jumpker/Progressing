using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Progressing.Core;
using Progressing.Models;

namespace Progressing.Controls;

/// <summary>
/// 备注表单行（产品设计书 §3.3.5）：起止时间 / 文案 / 颜色 / 上移 / 下移 / 删除。
/// 直接编辑一个 SegmentNote；任何变更通过 Changed 事件通知上层做重叠校验与落盘。
/// </summary>
public partial class SegmentEditor : UserControl
{
    /// <summary>任意字段变更（时间 / 文案 / 颜色）。</summary>
    public event EventHandler? Changed;

    /// <summary>请求删除本行。</summary>
    public event EventHandler? DeleteRequested;

    /// <summary>请求上移 / 下移。</summary>
    public event EventHandler? MoveUpRequested;

    public event EventHandler? MoveDownRequested;

    /// <summary>正在编辑的备注。</summary>
    public SegmentNote Note { get; private set; } = new();

    private bool _syncing;

    private TimeSpan _originalStart;
    private TimeSpan _originalEnd;
    private string _originalText = "";
    private string? _originalHex;
    private SegmentColor? _originalColor;

    /// <summary>当前实例的最近使用颜色（全局持久化，透传给取色弹窗）。</summary>
    public List<string> RecentColors { get; set; } = new();

    public SegmentEditor()
    {
        InitializeComponent();
        ColorButton.Background = new SolidColorBrush(Colors.Gray);
    }

    /// <summary>绑定备注并刷新控件。</summary>
    public void BindNote(SegmentNote note)
    {
        Note = note;
        _originalStart = note.StartTime;
        _originalEnd = note.EndTime;
        _originalText = note.Text;
        _originalHex = note.CustomHex;
        _originalColor = note.Color?.Clone();
        _syncing = true;
        StartBox.Time = note.StartTime;
        EndBox.Time = note.EndTime;
        TextInputBox.Text = note.Text;
        _syncing = false;
        RefreshColorSwatch();
    }

    /// <summary>回滚到绑定时的快照（冲突校验被拒绝时调用）。</summary>
    public void Revert()
    {
        Note.Start = _originalStart.ToString(@"hh\:mm");
        Note.End = _originalEnd.ToString(@"hh\:mm");
        Note.Text = _originalText;
        Note.CustomHex = _originalHex;
        Note.Color = _originalColor?.Clone();
        BindNote(Note);
    }

    private void StartBox_TimeChanged(object? sender, EventArgs e)
    {
        if (_syncing)
            return;

        Note.Start = FormatTime(StartBox.Time);
        RaiseChanged();
    }

    private void EndBox_TimeChanged(object? sender, EventArgs e)
    {
        if (_syncing)
            return;

        Note.End = FormatTime(EndBox.Time);
        RaiseChanged();
    }

    private void TextInputBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_syncing)
            return;

        Note.Text = TextInputBox.Text;
        RaiseChanged();
    }

    private void ColorButton_Click(object sender, RoutedEventArgs e)
    {
        var initial = ResolveColor();
        var picked = ColorPickerDialog.Pick(
            Window.GetWindow(this),
            initial,
            RecentColors);
        if (picked is null)
            return;

        ApplyColor(picked.Value);
        RaiseChanged();
    }

    private void MoveUp_Click(object sender, RoutedEventArgs e) => MoveUpRequested?.Invoke(this, EventArgs.Empty);

    private void MoveDown_Click(object sender, RoutedEventArgs e) => MoveDownRequested?.Invoke(this, EventArgs.Empty);

    private void Delete_Click(object sender, RoutedEventArgs e) => DeleteRequested?.Invoke(this, EventArgs.Empty);

    private void RaiseChanged() => Changed?.Invoke(this, EventArgs.Empty);

    private Color ResolveColor()
    {
        if (Note.Color is { Source: ColorSource.Custom } && !string.IsNullOrWhiteSpace(Note.CustomHex))
            return Palettes.FromHex(Note.CustomHex);

        if (Note.Color is { Source: ColorSource.Pool, PoolIndex: >= 0 } color
            && color.PoolIndex < Palettes.Pool.Count)
            return Palettes.Pool[color.PoolIndex.Value];

        return Palettes.Track;
    }

    /// <summary>应用新颜色：命中色池 → 存 Pool（手动方式，不占随机配额）；否则存 Custom。</summary>
    private void ApplyColor(Color color)
    {
        var hex = Palettes.ToHex(color);
        var poolIndex = FindPoolIndex(color);

        Note.CustomHex = poolIndex >= 0 ? null : hex;
        Note.Color = new SegmentColor
        {
            Source = poolIndex >= 0 ? ColorSource.Pool : ColorSource.Custom,
            PoolIndex = poolIndex >= 0 ? poolIndex : null,
            AssignedBy = ColorAssignedBy.Manual, // 手动选定不占随机配额
        };
        RefreshColorSwatch();
    }

    private void RefreshColorSwatch()
    {
        ColorButton.Background = new SolidColorBrush(ResolveColor());
    }

    private static int FindPoolIndex(Color color)
    {
        for (var i = 0; i < Palettes.Pool.Count; i++)
        {
            if (Palettes.Pool[i] == color)
                return i;
        }

        return -1;
    }

    private static string FormatTime(TimeSpan? t)
        => t is { } value ? value.ToString(@"hh\:mm") : "";
}
