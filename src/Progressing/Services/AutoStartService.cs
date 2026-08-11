using System.IO;
using Microsoft.Win32;

namespace Progressing.Services;

/// <summary>
/// 开机自启：在用户"启动"文件夹（shell:startup）中创建指向 Progressing.exe 的快捷方式（.lnk）。
/// 不再写注册表、无需管理员权限；对新手更透明——在"任务管理器 → 启动"或
/// shell:startup 文件夹中都能直接看到并删除该快捷方式。
/// 默认关闭，仅在设置中显式开启 / 关闭。
/// </summary>
public static class AutoStartService
{
    private const string ShortcutName = "Progressing.lnk";

    /// <summary>启动文件夹中快捷方式的完整路径。</summary>
    public static string ShortcutPath =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Startup), ShortcutName);

    /// <summary>当前是否已开启开机自启（以快捷方式是否存在为准）。</summary>
    public static bool IsEnabled() => File.Exists(ShortcutPath);

    /// <summary>开启或关闭开机自启。</summary>
    public static void SetEnabled(bool enabled)
    {
        try
        {
            if (enabled)
            {
                CreateShortcut();
                RemoveLegacyRegistryEntry(); // 老版本注册表入口：避免与新快捷方式并存导致重复启动
            }
            else
            {
                DeleteShortcut();
                RemoveLegacyRegistryEntry();
            }
        }
        catch
        {
            // 快捷方式创建 / 删除失败不阻断主流程（仅影响自启功能）
        }
    }

    /// <summary>创建指向当前可执行文件的 .lnk 快捷方式（自动识别安装路径）。</summary>
    private static void CreateShortcut()
    {
        var exe = Environment.ProcessPath;
        if (string.IsNullOrEmpty(exe))
            return;

        // 通过 Windows 自带的 WScript.Shell 生成快捷方式：无需额外依赖、无需管理员权限
        var shellType = Type.GetTypeFromProgID("WScript.Shell");
        if (shellType is null)
            return;

        dynamic shell = Activator.CreateInstance(shellType)!;
        dynamic shortcut = shell!.CreateShortcut(ShortcutPath);
        shortcut.TargetPath = exe;
        var dir = Path.GetDirectoryName(exe);
        if (!string.IsNullOrEmpty(dir))
            shortcut.WorkingDirectory = dir;
        shortcut.Description = "Progressing 开机自启";
        shortcut.Save();
    }

    private static void DeleteShortcut()
    {
        if (File.Exists(ShortcutPath))
            File.Delete(ShortcutPath);
    }

    /// <summary>清理旧版注册表 Run 键入口（仅删除，不再写入注册表）。</summary>
    private static void RemoveLegacyRegistryEntry()
    {
        using var key = Registry.CurrentUser.OpenSubKey(
            @"Software\Microsoft\Windows\CurrentVersion\Run", writable: true);
        key?.DeleteValue("Progressing", throwOnMissingValue: false);
    }
}
