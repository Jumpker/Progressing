using System.Runtime.InteropServices;

namespace Progressing.Services;

/// <summary>
/// 置顶守卫：监听系统前台窗口切换（EVENT_SYSTEM_FOREGROUND）。
/// 任务栏本身是置顶窗口，用户点击任务栏 / 切换窗口后任务栏会盖住同样置顶的进度条；
/// 因此前台一旦切换就回调，由外部对所有"置顶 + 可见"的进度条重新断言 HWND_TOPMOST，使其回到任务栏之上。
/// </summary>
public static class TopmostGuard
{
    private const uint EVENT_SYSTEM_FOREGROUND = 0x0003;
    private const uint WINEVENT_OUTOFCONTEXT = 0x0000;
    private const uint WINEVENT_SKIPOWNPROCESS = 0x0002;

    private delegate void WinEventDelegate(
        IntPtr hWinEventHook, uint eventType, IntPtr hwnd,
        int idObject, int idChild, uint dwEventThread, uint dwmsEventTime);

    [DllImport("user32.dll")]
    private static extern IntPtr SetWinEventHook(
        uint eventMin, uint eventMax, IntPtr hmodWinEventProc,
        WinEventDelegate pfnWinEventProc, uint idProcess, uint idThread, uint dwFlags);

    [DllImport("user32.dll")]
    private static extern bool UnhookWinEvent(IntPtr hWinEventHook);

    /// <summary>持有原生回调引用，防止被 GC 回收。</summary>
    private static WinEventDelegate? _callback;

    private static IntPtr _hook = IntPtr.Zero;

    /// <summary>开始监听（须在 UI 线程调用：WINEVENT_OUTOFCONTEXT 依赖线程消息泵回调）。</summary>
    public static void Start(Action onForegroundChanged)
    {
        if (_hook != IntPtr.Zero)
            return;

        _callback = (_, _, _, _, _, _, _) => onForegroundChanged();
        _hook = SetWinEventHook(
            EVENT_SYSTEM_FOREGROUND, EVENT_SYSTEM_FOREGROUND,
            IntPtr.Zero, _callback,
            0, 0, // 监听所有进程 / 线程
            WINEVENT_OUTOFCONTEXT | WINEVENT_SKIPOWNPROCESS);
    }

    /// <summary>停止监听并释放句柄。</summary>
    public static void Stop()
    {
        if (_hook != IntPtr.Zero)
        {
            UnhookWinEvent(_hook);
            _hook = IntPtr.Zero;
        }

        _callback = null;
    }
}
