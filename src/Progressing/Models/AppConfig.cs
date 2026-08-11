namespace Progressing.Models;

/// <summary>
/// 全局配置（对应 config.json 根对象）：版本 / 开机自启 / 最近使用颜色 / 实例列表。
/// </summary>
public class AppConfig
{
    public int Version { get; set; } = 1;

    /// <summary>界面主题模式（跟随系统 / 浅色 / 深色），默认浅色。</summary>
    public AppTheme Theme { get; set; } = AppTheme.Light;

    /// <summary>开机自启（注册表 Run 键）。</summary>
    public bool AutoStart { get; set; }

    /// <summary>颜色弹窗"最近使用颜色"（≤10 个，全局持久化）。</summary>
    public List<string> RecentColors { get; set; } = new();

    /// <summary>实例列表。</summary>
    public List<BarConfig> Instances { get; set; } = new();

    /// <summary>深拷贝。</summary>
    public AppConfig Clone() => new()
    {
        Version = Version,
        Theme = Theme,
        AutoStart = AutoStart,
        RecentColors = new List<string>(RecentColors),
        Instances = Instances.Select(i => i.Clone()).ToList(),
    };
}
