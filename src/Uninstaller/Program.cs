using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32;

namespace Progressing.Uninstaller;

/// <summary>
/// Progressing 卸载程序：双击运行后自动删除程序目录与用户数据（含开机自启快捷方式），
/// 不触碰用户其它任何文件。
/// 必须与 Progressing.exe 同目录，否则拒绝执行（安全护栏，防止误删其它文件夹）。
/// </summary>
internal static class Program
{
    // ---------- user32 MessageBox（原生调用，避免引入 WinForms，利于单文件发布） ----------
    private const uint MbOk = 0x0000;
    private const uint MbYesNo = 0x0004;
    private const uint MbIconQuestion = 0x0020;
    private const uint MbIconInformation = 0x0040;
    private const uint MbDefButton2 = 0x0100;
    private const uint IdYes = 6;

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int MessageBoxW(IntPtr hWnd, string text, string caption, uint type);

    private static int Main()
    {
        // 安全护栏：仅当与 Progressing.exe 同目录时才执行
        var appDir = Path.GetDirectoryName(Environment.ProcessPath ?? string.Empty) ?? string.Empty;
        if (string.IsNullOrEmpty(appDir) || !File.Exists(Path.Combine(appDir, "Progressing.exe")))
        {
            MessageBoxW(0,
                "未找到 Progressing.exe。\n请把 uninstall.exe 放在 Progressing.exe 旁边再运行（拒绝执行，以免误删其它文件）。",
                "卸载 Progressing", MbOk | MbIconInformation);
            return 1;
        }

        // 确认（默认光标在"否"上，防止误回车）
        if (MessageBoxW(0,
                "将删除 Progressing 的程序文件、你的全部设置与数据（含 %APPDATA%\\Progressing），此操作不可恢复。\n\n确定卸载吗？",
                "卸载 Progressing", MbYesNo | MbIconQuestion | MbDefButton2) != IdYes)
        {
            return 0; // 用户取消
        }

        // 1. 结束正在运行的 Progressing
        foreach (var p in Process.GetProcessesByName("Progressing"))
        {
            try { p.Kill(); } catch { /* 进程可能刚退出 */ }
        }

        // 2. 立即删除用户数据目录 %APPDATA%\Progressing
        var dataDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Progressing");
        TryDeleteDirectory(dataDir);

        // 3. 立即删除开机自启快捷方式（启动文件夹）
        var startupShortcut = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.Startup), "Progressing.lnk");
        TryDeleteFile(startupShortcut);

        // 4. 清理旧版注册表 Run 键入口（仅删除）
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Run", writable: true);
            key?.DeleteValue("Progressing", throwOnMissingValue: false);
        }
        catch { }

        // 5. 程序目录需等本进程退出后才能删除（exe 无法删除运行中的自身），交给后台进程
        MessageBoxW(0, "Progressing 已卸载，程序目录即将自动删除。", "卸载 Progressing", MbOk | MbIconInformation);
        ScheduleCleanup(appDir, dataDir, startupShortcut);
        return 0;
    }

    /// <summary>
    /// 退出后由后台 PowerShell 延迟执行最终清理：程序目录 + 数据目录（兜底重试）+ 快捷方式。
    /// 使用 -EncodedCommand 规避所有引号转义问题；PowerShell 工作目录设为系统目录，
    /// 避免占用程序目录导致删除失败。
    /// </summary>
    private static void ScheduleCleanup(string appDir, string dataDir, string startupShortcut)
    {
        try
        {
            var script =
                "Start-Sleep -Seconds 3;" +
                $"Remove-Item -LiteralPath '{appDir}' -Recurse -Force -ErrorAction SilentlyContinue;" +
                $"Remove-Item -LiteralPath '{dataDir}' -Recurse -Force -ErrorAction SilentlyContinue;" +
                $"Remove-Item -LiteralPath '{startupShortcut}' -Force -ErrorAction SilentlyContinue";
            var encoded = Convert.ToBase64String(Encoding.Unicode.GetBytes(script));

            var psi = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = $"-NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -EncodedCommand {encoded}",
                UseShellExecute = false,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden,
                WorkingDirectory = Environment.GetFolderPath(Environment.SpecialFolder.Windows),
            };
            Process.Start(psi);
        }
        catch
        {
            // 后台清理失败不影响已完成的卸载流程
        }
    }

    private static void TryDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
                Directory.Delete(path, recursive: true);
        }
        catch
        {
            // 文件占用等情况：由后台清理脚本兜底重试
        }
    }

    private static void TryDeleteFile(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch { }
    }
}
