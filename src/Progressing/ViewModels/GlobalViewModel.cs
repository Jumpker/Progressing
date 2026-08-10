using System.Reflection;
using CommunityToolkit.Mvvm.ComponentModel;
using Progressing.Models;
using Progressing.Services;

namespace Progressing.ViewModels;

/// <summary>
/// 全局标签页 VM（技术实现说明书 §3.12）：开机自启、版本信息。
/// </summary>
public partial class GlobalViewModel : TabViewModel
{
    private readonly AppConfig _config;

    public override string Header => "全局";

    /// <summary>全局标签页不可重命名 / 删除 / 复制。</summary>
    public override bool IsGlobal => true;

    public GlobalViewModel(AppConfig config)
    {
        _config = config;
    }

    /// <summary>开机自启开关。</summary>
    public bool AutoStart
    {
        get => AutoStartService.IsEnabled();
        set
        {
            if (AutoStartService.IsEnabled() == value)
                return;
            _config.AutoStart = value;
            AutoStartService.SetEnabled(value);
            OnPropertyChanged();
        }
    }

    /// <summary>最近使用颜色（≤10，全局持久化）。</summary>
    public List<string> RecentColors => _config.RecentColors;

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
