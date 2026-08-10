using System.Collections.ObjectModel;
using System.Windows.Threading;
using Progressing.Models;
using Progressing.Windows;

namespace Progressing.Services;

/// <summary>
/// 实例管理器（技术实现说明书 §3.5 / §3.11）：
/// - 依据配置创建 / 克隆 / 删除 / 重命名进度条实例；
/// - 上限 16 条；至少保留一条（最后一条不可删除）；
/// - 实例配置变更时通知对应窗口重绘并落盘；
/// - 向设置窗口广播实例列表变化（标签页刷新）。
/// </summary>
public class InstanceManager
{
    /// <summary>实例数量上限。</summary>
    public const int MaxInstances = 16;

    private readonly ConfigService _config;
    private readonly TimeService _timeService;
    private readonly List<BarWindow> _windows = new();

    /// <summary>全部实例窗口（顺序与配置一致）。</summary>
    public IReadOnlyList<BarWindow> Windows => _windows;

    /// <summary>实例列表 / 配置变化通知（设置窗口据此刷新标签页）。</summary>
    public event Action? InstancesChanged;

    public InstanceManager(ConfigService config, TimeService timeService)
    {
        _config = config;
        _timeService = timeService;
    }

    /// <summary>启动时依据配置创建全部实例；首次运行创建默认"进度条 1"并定位到底部居中。</summary>
    public void Initialize()
    {
        if (_config.FirstRun || _config.Config.Instances.Count == 0)
        {
            var defaultConfig = BarConfig.Default();
            _config.Config.Instances.Add(defaultConfig);
            CreateWindow(defaultConfig);
            _config.MarkDirty();
        }
        else
        {
            foreach (var config in _config.Config.Instances.ToList())
                CreateWindow(config);
        }
    }

    /// <summary>新建一条进度条：默认吸附主屏底部居中（与已有实例轻微错开），创建后进入重命名状态（由设置窗口处理）。</summary>
    public BarWindow Create()
    {
        var config = BarConfig.Default();
        config.Name = GenerateName();
        _config.Config.Instances.Add(config);
        var window = CreateWindow(config);
        CascadeOffset(window);
        _config.MarkDirty();
        InstancesChanged?.Invoke();
        return window;
    }

    /// <summary>新实例在底部居中基础上向左上错开，避免多条完全重叠。</summary>
    private void CascadeOffset(BarWindow window)
    {
        var offset = (_windows.Count - 1) * 24.0;
        if (offset <= 0)
            return;

        window.Left -= offset;
        window.Top -= offset;
        window.Config.Placement.X = window.Left;
        window.Config.Placement.Y = window.Top;
    }

    /// <summary>复制实例完整配置为新实例。</summary>
    public BarWindow Clone(BarWindow source)
    {
        var config = source.Config.Clone();
        config.Id = Guid.NewGuid().ToString("N");
        config.Name = source.Config.Name + " 副本";
        _config.Config.Instances.Add(config);
        var window = CreateWindow(config);
        _config.MarkDirty();
        InstancesChanged?.Invoke();
        return window;
    }

    /// <summary>删除实例；最后一条不可删除。</summary>
    public bool Delete(BarWindow window)
    {
        if (_windows.Count <= 1)
            return false;

        _windows.Remove(window);
        _config.Config.Instances.Remove(window.Config);
        window.Close();
        _config.MarkDirty();
        InstancesChanged?.Invoke();
        return true;
    }

    /// <summary>重命名实例。</summary>
    public void Rename(BarWindow window, string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return;

        window.Config.Name = name.Trim();
        _config.MarkDirty();
        InstancesChanged?.Invoke();
    }

    /// <summary>实例配置已变更：重绘窗口 + 刷新帧 + 落盘。</summary>
    public void NotifyChanged(BarWindow window)
    {
        window.ApplyConfig();
        window.RefreshFrame();
        _config.MarkDirty();
    }

    /// <summary>切换实例显示 / 隐藏。</summary>
    public void SetVisible(BarWindow window, bool visible)
    {
        window.Config.Visible = visible;
        window.ApplyConfig();
        _config.MarkDirty();
    }

    /// <summary>应用一次性位置预设。</summary>
    public void ApplyPreset(BarWindow window, PlacementPreset preset, string? monitorId)
    {
        window.ApplyPreset(preset, monitorId);
        _config.MarkDirty();
    }

    /// <summary>停止全部实例调度（退出时）。</summary>
    public void Shutdown()
    {
        foreach (var window in _windows)
        {
            window.Close();
        }

        _windows.Clear();
    }

    /// <summary>按配置创建窗口（不落盘，供启动时批量调用）。</summary>
    private BarWindow CreateWindow(BarConfig config)
    {
        var window = new BarWindow(config, _timeService);
        _windows.Add(window);
        return window;
    }

    private string GenerateName()
    {
        var used = _windows.Select(w => w.Config.Name).ToHashSet();
        for (var i = 1; i <= 999; i++)
        {
            var name = $"进度条 {i}";
            if (!used.Contains(name))
                return name;
        }

        return $"进度条 {Guid.NewGuid().ToString("N")[..4]}";
    }
}
