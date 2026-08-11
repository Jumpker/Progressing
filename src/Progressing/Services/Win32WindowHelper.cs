using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace Progressing.Services;

/// <summary>
/// Win32 P/Invoke 封装：鼠标穿透（WS_EX_TRANSPARENT）与显示器枚举（虚拟屏幕物理坐标 + 每显示器 DPI）。
/// </summary>
public static class Win32WindowHelper
{
    private const int GWL_EXSTYLE = -20;
    private const int WS_EX_TRANSPARENT = 0x00000020;
    private const int WS_EX_LAYERED = 0x00080000;

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;

        public int Width => Right - Left;
        public int Height => Bottom - Top;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct MONITORINFOEX
    {
        public int cbSize;
        public RECT rcMonitor;
        public RECT rcWork;
        public uint dwFlags;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string szDevice;
    }

    private delegate bool MonitorEnumProc(IntPtr hMonitor, IntPtr hdcMonitor, ref RECT lprcMonitor, IntPtr dwData);

    [DllImport("user32.dll")]
    private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll")]
    private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

    [DllImport("user32.dll")]
    private static extern bool EnumDisplayMonitors(IntPtr hdc, IntPtr lprcClip, MonitorEnumProc lpfnEnum, IntPtr dwData);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFOEX lpmi);

    [DllImport("shcore.dll")]
    private static extern int GetDpiForMonitor(IntPtr hmonitor, int dpiType, out uint dpiX, out uint dpiY);

    private const int MDT_EFFECTIVE_DPI = 0;

    /// <summary>MONITORINFOF_PRIMARY：主显示器标记。</summary>
    private const int MONITORINFOF_PRIMARY = 1;

    /// <summary>一台显示器的信息（物理像素坐标，DIP 需按 DpiScale 换算）。</summary>
    public sealed record MonitorInfo(string DeviceName, Rect Bounds, Rect WorkArea, double DpiScale, bool IsPrimary);

    /// <summary>开关鼠标穿透。WPF AllowsTransparency 已隐含 WS_EX_LAYERED，再叠加 WS_EX_TRANSPARENT 即可点击穿透。</summary>
    public static void SetClickThrough(Window window, bool enabled)
    {
        var hwnd = new WindowInteropHelper(window).Handle;
        if (hwnd == IntPtr.Zero)
            return;

        var ex = GetWindowLong(hwnd, GWL_EXSTYLE);
        if (enabled)
            ex |= WS_EX_TRANSPARENT;
        else
            ex &= ~WS_EX_TRANSPARENT;
        ex |= WS_EX_LAYERED;

        SetWindowLong(hwnd, GWL_EXSTYLE, ex);
    }

    /// <summary>枚举全部显示器（物理像素坐标）。</summary>
    public static List<MonitorInfo> EnumerateMonitors()
    {
        var list = new List<MonitorInfo>();
        EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, (hMonitor, _, ref _, _) =>
        {
            var info = new MONITORINFOEX { cbSize = Marshal.SizeOf<MONITORINFOEX>() };
            if (GetMonitorInfo(hMonitor, ref info))
            {
                var scale = GetDpiScale(hMonitor);
                list.Add(new MonitorInfo(
                    info.szDevice,
                    new Rect(info.rcMonitor.Left, info.rcMonitor.Top, info.rcMonitor.Width, info.rcMonitor.Height),
                    new Rect(info.rcWork.Left, info.rcWork.Top, info.rcWork.Width, info.rcWork.Height),
                    scale,
                    (info.dwFlags & MONITORINFOF_PRIMARY) != 0));
            }

            return true;
        }, IntPtr.Zero);
        return list;
    }

    /// <summary>主显示器（带 MONITORINFOF_PRIMARY 标记的那台；异常时退回系统工作区）。</summary>
    public static MonitorInfo PrimaryMonitor()
        => EnumerateMonitors().FirstOrDefault(m => m.IsPrimary)
           ?? new MonitorInfo("primary", SystemParameters.WorkArea, SystemParameters.WorkArea, 1.0, true);

    /// <summary>包含指定 DIP 点的显示器（按各显示器自身缩放换算后判定；找不到时退回主显示器）。</summary>
    public static MonitorInfo MonitorAt(double dipX, double dipY)
    {
        foreach (var m in EnumerateMonitors())
        {
            if (m.Bounds.Contains(new Point(dipX * m.DpiScale, dipY * m.DpiScale)))
                return m;
        }

        return PrimaryMonitor();
    }

    /// <summary>物理像素 → DIP（按该显示器缩放系数）。</summary>
    public static double PxToDip(double px, double dpiScale) => px / dpiScale;

    private static double GetDpiScale(IntPtr hMonitor)
    {
        if (GetDpiForMonitor(hMonitor, MDT_EFFECTIVE_DPI, out var dpiX, out _) == 0 && dpiX > 0)
            return dpiX / 96.0;
        return 1.0;
    }
}
