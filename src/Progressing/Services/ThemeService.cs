using System.Windows;
using Microsoft.Win32;
using Progressing.Models;

namespace Progressing.Services;

/// <summary>
/// 主题模式服务：运行时在浅色 / 深色颜色令牌字典（Colors.Light / Colors.Dark）间热替换。
/// 所有界面画刷均以 DynamicResource 引用，替换字典后全局即时生效。
/// "跟随系统"读取注册表 AppsUseLightTheme 判定系统深浅色，由 App 监听系统主题变化并重新调用 Apply。
/// </summary>
public static class ThemeService
{
    private const string LightColors = "Resources/Colors.Light.xaml";
    private const string DarkColors = "Resources/Colors.Dark.xaml";

    /// <summary>当前生效的主题模式（用户选择，含"跟随系统"语义）。</summary>
    public static AppTheme Current { get; private set; } = AppTheme.System;

    /// <summary>应用主题模式：按模式选择浅色 / 深色令牌字典并热替换。</summary>
    public static void Apply(AppTheme theme)
    {
        Current = theme;
        var dark = theme == AppTheme.Dark || (theme == AppTheme.System && IsSystemDark());
        SwapColors(dark ? DarkColors : LightColors);
    }

    /// <summary>系统当前是否为深色外观（注册表 AppsUseLightTheme：0 = 深色，1 = 浅色）。</summary>
    public static bool IsSystemDark()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            return key?.GetValue("AppsUseLightTheme") is int value && value == 0;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>按 Source 定位当前颜色令牌字典，原地替换（保持合并顺序不变）。</summary>
    private static void SwapColors(string uri)
    {
        var merged = Application.Current?.Resources.MergedDictionaries;
        if (merged is null)
            return;

        var colors = merged.FirstOrDefault(d =>
            d.Source is { } source && source.OriginalString.Contains("Colors.", StringComparison.OrdinalIgnoreCase));

        if (colors is null)
        {
            // 防御：颜色字典尚未注册时插入到最前
            merged.Insert(0, new ResourceDictionary { Source = new Uri(uri, UriKind.Relative) });
            return;
        }

        var index = merged.IndexOf(colors);
        merged.Remove(colors);
        merged.Insert(index, new ResourceDictionary { Source = new Uri(uri, UriKind.Relative) });
    }
}
