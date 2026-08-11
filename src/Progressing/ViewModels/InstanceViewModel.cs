using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Progressing.Core;
using Progressing.Models;
using Progressing.Services;
using Progressing.Windows;

namespace Progressing.ViewModels;

/// <summary>
/// 单条进度条实例标签页 VM（技术实现说明书 §3.12）：
/// 外观 / 位置 / 指针 / 时间段 / 文字样式分区，所有字段变更即时下发并防抖落盘。
/// </summary>
public partial class InstanceViewModel : TabViewModel
{
    private readonly InstanceManager _manager;

    /// <summary>实例窗口（与 Config 同引用）。</summary>
    public BarWindow Window { get; }

    /// <summary>本条进度条配置（ConfigService 单源共享引用）。</summary>
    public BarConfig Config => Window.Config;

    public override string Header => Config.Name;

    public InstanceViewModel(BarWindow window, InstanceManager manager)
    {
        Window = window;
        _manager = manager;
        Window.EditModeChanged += (_, _) => OnPropertyChanged(nameof(IsEditMode));

        Monitors.Add(new MonitorItem(null, "主显示器"));
        foreach (var m in Win32WindowHelper.EnumerateMonitors())
            Monitors.Add(new MonitorItem(m.DeviceName, $"{m.DeviceName} ({m.WorkArea.Width / m.DpiScale:0}×{m.WorkArea.Height / m.DpiScale:0})"));

        RefreshNotes();
    }

    /// <summary>重命名后刷新标签标题。</summary>
    public void NotifyHeaderChanged() => OnPropertyChanged(nameof(Header));

    /// <summary>左侧导航分区索引（0 外观 / 1 位置 / 2 指针 / 3 时间段 / 4 文字样式 / 5 其它）。</summary>
    [ObservableProperty]
    private int _sectionIndex;

    // ---------------- 外观 ----------------

    public bool Visible
    {
        get => Config.Visible;
        set
        {
            Config.Visible = value;
            _manager.SetVisible(Window, value);
        }
    }

    // ---------------- 位置 ----------------

    public BarOrientation Orientation
    {
        get => Config.Orientation;
        set { if (Config.Orientation != value) { Config.Orientation = value; OnPropertyChanged(); Save(); } }
    }

    public bool Mirrored
    {
        get => Config.Mirrored;
        set { if (Config.Mirrored != value) { Config.Mirrored = value; OnPropertyChanged(); Save(); } }
    }

    public double Length
    {
        get => Config.Length;
        set { Config.Length = Clamp(value, 200, 2000); OnPropertyChanged(); Save(); }
    }

    public double Width
    {
        get => Config.Width;
        set { Config.Width = Clamp(value, 2, 10); OnPropertyChanged(); Save(); }
    }

    public int Opacity
    {
        get => Config.Opacity;
        set { Config.Opacity = Math.Clamp(value, 0, 100); OnPropertyChanged(); Save(); }
    }

    public bool Topmost
    {
        get => Config.Topmost;
        set { if (Config.Topmost != value) { Config.Topmost = value; OnPropertyChanged(); Save(); } }
    }

    public bool BorderEnabled
    {
        get => Config.Border.Enabled;
        set { if (Config.Border.Enabled != value) { Config.Border.Enabled = value; OnPropertyChanged(); Save(); } }
    }

    /// <summary>进度条边框颜色（HEX）。</summary>
    public string BorderColor
    {
        get => Config.Border.Color;
        set { Config.Border.Color = value; OnPropertyChanged(); Save(); }
    }

    /// <summary>显示器下拉项。</summary>
    public ObservableCollection<MonitorItem> Monitors { get; } = new();

    /// <summary>进度条定位预设下拉项（选中即应用并复位）。</summary>
    public IReadOnlyList<PlacementPresetItem> Presets { get; } = new[]
    {
        new PlacementPresetItem("顶部居中", PlacementPreset.TopCenter),
        new PlacementPresetItem("底部居中", PlacementPreset.BottomCenter),
        new PlacementPresetItem("左侧居中", PlacementPreset.LeftCenter),
        new PlacementPresetItem("右侧居中", PlacementPreset.RightCenter),
    };

    /// <summary>文字定位预设下拉项（一次性：选中即设锚点、清零拖拽偏移并复位）。</summary>
    public IReadOnlyList<LabeledValue<TextAnchor>> TextPositionPresets { get; } = new[]
    {
        new LabeledValue<TextAnchor>("进度条上方", TextAnchor.Top),
        new LabeledValue<TextAnchor>("进度条下方", TextAnchor.Bottom),
        new LabeledValue<TextAnchor>("进度条左侧", TextAnchor.Left),
        new LabeledValue<TextAnchor>("进度条右侧", TextAnchor.Right),
    };

    /// <summary>选中的显示器（应用预设时临时生效；null = 主显示器）。</summary>
    [ObservableProperty]
    private MonitorItem? _selectedMonitor;

    /// <summary>一次性位置预设（选中即解析为 X/Y 落盘并复位）。</summary>
    [ObservableProperty]
    private PlacementPresetItem? _selectedPreset;

    /// <summary>一次性文字定位预设（选中即设锚点、清零拖拽偏移并复位）。</summary>
    [ObservableProperty]
    private LabeledValue<TextAnchor>? _selectedTextPreset;

    // ---------------- 下拉选项集合 ----------------

    public IReadOnlyList<LabeledValue<PointerSource>> PointerSourceOptions { get; } = new[]
    {
        new LabeledValue<PointerSource>("内置", PointerSource.Builtin),
        new LabeledValue<PointerSource>("自定义文件", PointerSource.File),
    };

    public IReadOnlyList<LabeledValue<TextAnchor>> TextAnchorOptions { get; } = new[]
    {
        new LabeledValue<TextAnchor>("上方", TextAnchor.Top),
        new LabeledValue<TextAnchor>("下方", TextAnchor.Bottom),
        new LabeledValue<TextAnchor>("左侧", TextAnchor.Left),
        new LabeledValue<TextAnchor>("右侧", TextAnchor.Right),
    };

    public IReadOnlyList<LabeledValue<TextArrangement>> TextArrangementOptions { get; } = new[]
    {
        new LabeledValue<TextArrangement>("横排", TextArrangement.Horizontal),
        new LabeledValue<TextArrangement>("竖排", TextArrangement.Vertical),
    };

    public IReadOnlyList<LabeledValue<PointerDirection>> HorizontalDirectionOptions { get; } = new[]
    {
        new LabeledValue<PointerDirection>("向上", PointerDirection.Up),
        new LabeledValue<PointerDirection>("向下", PointerDirection.Down),
    };

    public IReadOnlyList<LabeledValue<PointerDirection>> VerticalDirectionOptions { get; } = new[]
    {
        new LabeledValue<PointerDirection>("向左", PointerDirection.Left),
        new LabeledValue<PointerDirection>("向右", PointerDirection.Right),
    };

    partial void OnSelectedPresetChanged(PlacementPresetItem? value)
    {
        if (value is null)
            return;

        _manager.ApplyPreset(Window, value.Value, SelectedMonitor?.DeviceName);
        SelectedPreset = null; // 一次性：解析即复位
    }

    /// <summary>
    /// 一次性文字定位预设：设锚点并清零拖拽偏移，文字立即吸附到进度条对应一侧。
    /// 锚点定位按横竖放各自布局（见 PositionNoteContainer），因此任意方向都成立。
    /// </summary>
    partial void OnSelectedTextPresetChanged(LabeledValue<TextAnchor>? value)
    {
        if (value is null)
            return;

        Config.TextStyle.Anchor = value.Value;
        Config.TextOffset = new Point2D(); // 取消手动拖拽偏移，回到预设基准位置
        OnPropertyChanged(nameof(TextAnchor)); // 与文字样式分区的"基准方向"联动
        Save();
        SelectedTextPreset = null; // 一次性：应用即复位
    }

    public bool IsEditMode => Window.IsEditMode;

    /// <summary>以进度条几何中心为原点顺时针旋转 90°（横放 ↔ 竖放），并刷新依赖方向的 UI。</summary>
    public void Rotate()
    {
        Window.RotateClockwise();
        OnPropertyChanged(nameof(Orientation));
        Save();
    }

    [RelayCommand]
    private void ToggleEditMode()
    {
        if (Window.IsEditMode)
            Window.ExitEditMode();
        else
            Window.EnterEditMode();
    }

    // ---------------- 指针 ----------------

    public PointerSource PointerSource
    {
        get => Config.Pointer.Source;
        set { if (Config.Pointer.Source != value) { Config.Pointer.Source = value; OnPropertyChanged(); Save(); } }
    }

    public string? PointerFilePath
    {
        get => Config.Pointer.FilePath;
        set { Config.Pointer.FilePath = string.IsNullOrWhiteSpace(value) ? null : value.Trim(); OnPropertyChanged(); Save(); }
    }

    public double PointerSize
    {
        get => Config.Pointer.Size;
        set { Config.Pointer.Size = Clamp(value, 8, 64); OnPropertyChanged(); Save(); }
    }

    public PointerDirection HorizontalDirection
    {
        get => Config.Pointer.HorizontalDirection;
        set { if (Config.Pointer.HorizontalDirection != value) { Config.Pointer.HorizontalDirection = value; OnPropertyChanged(); Save(); } }
    }

    public PointerDirection VerticalDirection
    {
        get => Config.Pointer.VerticalDirection;
        set { if (Config.Pointer.VerticalDirection != value) { Config.Pointer.VerticalDirection = value; OnPropertyChanged(); Save(); } }
    }

    // ---------------- 时间段备注 ----------------

    public ObservableCollection<SegmentNote> Notes { get; } = new();

    private void RefreshNotes()
    {
        Notes.Clear();
        foreach (var note in Config.Notes)
            Notes.Add(note);
    }

    /// <summary>新增备注：随机取色（色池未用完前不重复，规则见产品设计书 §3.4）+ 默认时段 1 小时。</summary>
    public void AddNote()
    {
        var poolIndex = new ColorPoolService(Config.ColorPoolUsed).PickRandomIndex();
        var note = new SegmentNote
        {
            Id = Guid.NewGuid().ToString("N"),
            Start = "00:00",
            End = "01:00",
            Text = "备注",
            Color = new SegmentColor
            {
                Source = ColorSource.Pool,
                PoolIndex = poolIndex,
                AssignedBy = ColorAssignedBy.Random, // 随机取用，删除时归还色池配额
            },
            CustomHex = null,
        };
        Config.Notes.Add(note);
        Notes.Add(note);
        Save();
    }

    /// <summary>删除备注（归还随机色池配额）。</summary>
    public void RemoveNote(SegmentNote note)
    {
        if (!Config.Notes.Remove(note))
            return;
        ReleaseColor(note);
        Notes.Remove(note);
        Save();
    }

    /// <summary>上移 / 下移备注（delta = -1 / +1）。</summary>
    public void MoveNote(SegmentNote note, int delta)
    {
        var i = Config.Notes.IndexOf(note);
        var j = i + delta;
        if (i < 0 || j < 0 || j >= Config.Notes.Count)
            return;
        (Config.Notes[i], Config.Notes[j]) = (Config.Notes[j], Config.Notes[i]);
        Notes.Move(i, j);
        Save();
    }

    /// <summary>删除 / 颜色改为自定义时，归还随机取用占用的色池配额。</summary>
    private void ReleaseColor(SegmentNote note)
    {
        if (note.Color is { Source: ColorSource.Pool, AssignedBy: ColorAssignedBy.Random, PoolIndex: >= 0 } color)
        {
            Config.ColorPoolUsed.Remove(color.PoolIndex.Value);
        }
    }

    // ---------------- 备注文字样式 ----------------

    public TextAnchor TextAnchor
    {
        get => Config.TextStyle.Anchor;
        set { if (Config.TextStyle.Anchor != value) { Config.TextStyle.Anchor = value; OnPropertyChanged(); Save(); } }
    }

    public TextArrangement TextArrangement
    {
        get => Config.TextStyle.Arrangement;
        set { if (Config.TextStyle.Arrangement != value) { Config.TextStyle.Arrangement = value; OnPropertyChanged(); Save(); } }
    }

    public double TextFontSize
    {
        get => Config.TextStyle.FontSize;
        set
        {
            var v = Clamp(value, 8, 72);
            if (Math.Abs(Config.TextStyle.FontSize - v) < 0.001)
                return;
            Config.TextStyle.FontSize = v;
            OnPropertyChanged();
            Save();
        }
    }

    public string TextColor
    {
        get => Config.TextStyle.Color;
        set { Config.TextStyle.Color = value; OnPropertyChanged(); Save(); }
    }

    public bool TextBorderEnabled
    {
        get => Config.TextStyle.Border.Enabled;
        set { if (Config.TextStyle.Border.Enabled != value) { Config.TextStyle.Border.Enabled = value; OnPropertyChanged(); Save(); } }
    }

    public string TextBorderColor
    {
        get => Config.TextStyle.Border.Color;
        set { Config.TextStyle.Border.Color = value; OnPropertyChanged(); Save(); }
    }

    // ---------------- 其它 ----------------

    /// <summary>恢复默认配置（保留名称与 Id）。</summary>
    [RelayCommand]
    private void ResetToDefaults()
    {
        var name = Config.Name;
        var id = Config.Id;
        var defaults = BarConfig.Default();
        defaults.Name = name;
        defaults.Id = id;
        defaults.Visible = Config.Visible;

        // 逐字段覆盖（保留引用）
        var c = Config;
        c.Orientation = defaults.Orientation;
        c.Mirrored = defaults.Mirrored;
        c.Length = defaults.Length;
        c.Width = defaults.Width;
        c.Opacity = defaults.Opacity;
        c.Topmost = defaults.Topmost;
        c.Border = defaults.Border;
        c.Pointer = defaults.Pointer;
        c.Notes = defaults.Notes;
        c.ColorPoolUsed.Clear();
        c.TextStyle = defaults.TextStyle;
        c.TextOffset = defaults.TextOffset;

        RefreshNotes();
        OnPropertyChanged(string.Empty);
        Save();
    }

    // ---------------- 落盘 ----------------

    /// <summary>配置已变更：下发窗口重绘 + 防抖落盘。</summary>
    public void Save() => _manager.NotifyChanged(Window);

    private static double Clamp(double v, double min, double max) => Math.Clamp(v, min, max);
}

/// <summary>显示器下拉项。</summary>
public sealed record MonitorItem(string? DeviceName, string Label)
{
    public override string ToString() => Label;
}

/// <summary>进度条定位预设下拉项。</summary>
public sealed record PlacementPresetItem(string Label, PlacementPreset Value)
{
    public override string ToString() => Label;
}

/// <summary>带中文标签的枚举下拉项（SelectedValuePath="Value" 双向绑定）。</summary>
public sealed record LabeledValue<T>(string Label, T Value)
{
    public override string ToString() => Label;
}
