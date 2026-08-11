using System.Reflection;
using CommunityToolkit.Mvvm.ComponentModel;
using Progressing.Models;
using Progressing.Services;

namespace Progressing.ViewModels;

/// <summary>
/// 全局标签页 VM（技术实现说明书 §3.12）：开机自启、主题模式、版本信息。
/// </summary>
public partial class GlobalViewModel : TabViewModel
{
    private readonly ConfigService _configService;

    public override string Header => "全局";

    /// <summary>全局标签页不可重命名 / 删除 / 复制。</summary>
    public override bool IsGlobal => true;

    public GlobalViewModel(ConfigService configService)
    {
        _configService = configService;
    }

    /// <summary>开机自启开关。</summary>
    public bool AutoStart
    {
        get => AutoStartService.IsEnabled();
        set
        {
            if (AutoStartService.IsEnabled() == value)
                return;
            _configService.Config.AutoStart = value;
            AutoStartService.SetEnabled(value);
            _configService.MarkDirty();
            OnPropertyChanged();
        }
    }

    /// <summary>主题模式：跟随系统 / 浅色 / 深色（选择后即时换肤并持久化）。</summary>
    public AppTheme Theme
    {
        get => _configService.Config.Theme;
        set
        {
            if (_configService.Config.Theme == value)
                return;
            _configService.Config.Theme = value;
            _configService.MarkDirty();
            ThemeService.Apply(value);
            OnPropertyChanged();
        }
    }

    /// <summary>主题模式选项（预览卡片数据源）。</summary>
    public IReadOnlyList<ThemeOption> ThemeOptions { get; } = new[]
    {
        new ThemeOption(AppTheme.Light, "浅色"),
        new ThemeOption(AppTheme.Dark, "深色"),
        new ThemeOption(AppTheme.System, "跟随系统"),
    };

    /// <summary>最近使用颜色（≤10，全局持久化）。</summary>
    public List<string> RecentColors => _configService.Config.RecentColors;

    /// <summary>版本号。</summary>
    public string Version
    {
        get
        {
            var v = Assembly.GetExecutingAssembly().GetName().Version;
            return v is null ? "1.0.0" : $"{v.Major}.{v.Minor}.{v.Build}";
        }
    }
}

/// <summary>主题模式选项（供全局页预览卡片选择）。</summary>
public sealed record ThemeOption(AppTheme Value, string Name)
{
    /// <summary>预览图分支标识（与枚举名一致，供 XAML DataTrigger 匹配）。</summary>
    public string Key => Value.ToString();
}
