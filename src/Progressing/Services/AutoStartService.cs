using Microsoft.Win32;

namespace Progressing.Services;

/// <summary>
/// 开机自启：写入 / 删除注册表 HKCU\Software\Microsoft\Windows\CurrentVersion\Run → 键 "Progressing"。
/// 默认关闭，仅在设置中显式开启 / 关闭。
/// </summary>
public static class AutoStartService
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "Progressing";

    /// <summary>当前是否已开启开机自启。</summary>
    public static bool IsEnabled()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: false);
            return key?.GetValue(ValueName) is string value && value.Length > 0;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>开启或关闭开机自启。</summary>
    public static void SetEnabled(bool enabled)
    {
        try
        {
            if (enabled)
            {
                var exe = Environment.ProcessPath;
                if (string.IsNullOrEmpty(exe))
                    return;

                // 命令行启动可执行文件；路径含空格时以引号包裹
                using var key = Registry.CurrentUser.CreateSubKey(RunKeyPath);
                key.SetValue(ValueName, $"\"{exe}\"");
            }
            else
            {
                using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true);
                key?.DeleteValue(ValueName, throwOnMissingValue: false);
            }
        }
        catch
        {
            // 注册表写入失败不阻断主流程（仅影响自启功能）
        }
    }
}
