using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows.Threading;
using Progressing.Models;

namespace Progressing.Services;

/// <summary>
/// 配置持久化（技术实现说明书 §3.10）：
/// - 路径 %APPDATA%\Progressing\config.json；
/// - 防抖保存：MarkDirty 后 500ms 合并写盘；
/// - 原子写：先写 .tmp 再 File.Replace 覆盖；失败兜底直拷；
/// - 容错加载：主文件损坏时回退 .bak，仍失败回退默认配置，不阻塞启动；
/// - 首次运行（无配置文件）标记 FirstRun，由上层创建默认实例。
/// </summary>
public class ConfigService : IDisposable
{
    private const int DebounceMs = 500;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly string _path;
    private readonly string _backupPath;
    private readonly string _tempPath;
    private readonly DispatcherTimer? _debounceTimer;
    private bool _dirty;
    private bool _persistEnabled = true;

    /// <summary>当前配置（单源真相：实例窗口与设置窗口共享其中的 BarConfig 对象）。</summary>
    public AppConfig Config { get; private set; }

    /// <summary>是否为首次运行（无配置文件），用于创建默认"进度条 1"。</summary>
    public bool FirstRun { get; }

    public ConfigService()
    {
        var dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Progressing");
        try
        {
            Directory.CreateDirectory(dir);
            _path = Path.Combine(dir, "config.json");
            _backupPath = Path.Combine(dir, "config.json.bak");
            _tempPath = Path.Combine(dir, "config.json.tmp");
        }
        catch
        {
            // 配置目录不可写（权限/沙箱等）：降级为仅内存模式，不阻塞启动
            _persistEnabled = false;
            _path = Path.Combine(Path.GetTempPath(), "Progressing", "config.json");
            _backupPath = _path + ".bak";
            _tempPath = _path + ".tmp";
        }

        (Config, FirstRun) = Load();

        _debounceTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(DebounceMs) };
        _debounceTimer.Tick += (_, _) =>
        {
            _debounceTimer.Stop();
            SaveNow();
        };
    }

    /// <summary>标记配置已变更：500ms 防抖后合并写盘。</summary>
    public void MarkDirty()
    {
        _dirty = true;
        if (_debounceTimer is null)
            return;

        _debounceTimer.Stop();
        _debounceTimer.Start();
    }

    /// <summary>立即落盘（应用退出时调用；防抖计时器未触发也能保存）。</summary>
    public void SaveNow()
    {
        if (!_persistEnabled)
        {
            _dirty = false;
            return;
        }

        if (!_dirty && File.Exists(_path))
            return;

        _dirty = false;
        SaveToDisk(Config);
    }

    private (AppConfig, bool firstRun) Load()
    {
        if (!File.Exists(_path))
            return (new AppConfig(), true);

        if (TryRead(_path, out var config))
            return (config, false);

        // 主文件损坏：尝试 .bak
        if (TryRead(_backupPath, out var backup))
            return (backup, false);

        return (new AppConfig(), false);
    }

    private static bool TryRead(string path, out AppConfig config)
    {
        try
        {
            var json = File.ReadAllText(path);
            var parsed = JsonSerializer.Deserialize<AppConfig>(json, JsonOptions);
            if (parsed is not null)
            {
                Normalize(parsed);
                config = parsed;
                return true;
            }
        }
        catch
        {
            // 解析失败 → 走备份 / 默认
        }

        config = new AppConfig();
        return false;
    }

    /// <summary>对加载结果做防御性修复：null 嵌套对象补齐，保证后续使用无 NRE。</summary>
    private static void Normalize(AppConfig config)
    {
        // 防御非法主题值（config.json 被手动改坏时回退默认浅色）
        if (!Enum.IsDefined(typeof(AppTheme), config.Theme))
            config.Theme = AppTheme.Light;

        config.RecentColors ??= new List<string>();
        config.Instances ??= new List<BarConfig>();
        foreach (var instance in config.Instances)
        {
            instance.Border ??= BorderConfig.Default();
            instance.Placement ??= Placement.Default();
            instance.Pointer ??= PointerConfig.Default();
            instance.TextStyle ??= TextStyleConfig.Default();
            instance.TextStyle.Border ??= BorderConfig.TextDefault();
            // 旧默认值升级：旧文字色 #5A5A5A / #2D82BC → 新默认 #4D9DDA；边框旧默认（关 / 黑 / 1px）→ 开启
            if (instance.TextStyle.Color is "#5A5A5A" or "#2D82BC")
                instance.TextStyle.Color = "#4D9DDA";
            if (instance.TextStyle.Border is { Enabled: false, Color: "#000000", Width: 1.0 })
                instance.TextStyle.Border.Enabled = true;
            if (instance.TextStyle.FontSize <= 0)
                instance.TextStyle.FontSize = 26.0;
            instance.Notes ??= new List<SegmentNote>();
            instance.ColorPoolUsed ??= new List<int>();
            instance.TextOffset ??= new Point2D();
        }
    }

    private void SaveToDisk(AppConfig config)
    {
        try
        {
            if (File.Exists(_path))
                File.Copy(_path, _backupPath, overwrite: true);

            var json = JsonSerializer.Serialize(config, JsonOptions);
            File.WriteAllText(_tempPath, json);
            if (File.Exists(_path))
                File.Replace(_tempPath, _path, destinationBackupFileName: null);
            else
                File.Move(_tempPath, _path);
        }
        catch
        {
            // 兜底：直接覆盖写（非原子，但至少保证配置不丢失）
            try
            {
                var json = JsonSerializer.Serialize(config, JsonOptions);
                File.WriteAllText(_path, json);
            }
            catch
            {
                // 磁盘异常等极端情况：静默，不阻断主流程
            }
        }
    }

    public void Dispose()
    {
        _debounceTimer?.Stop();
        _dirty = false;
    }
}
