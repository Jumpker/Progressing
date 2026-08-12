using System.Runtime.InteropServices;
using System.Windows.Threading;

namespace Progressing.Services;

/// <summary>
/// 全屏监视器：检测是否有应用处于全屏（铺满整块显示器，含任务栏区域）。
/// 触发时机：前台窗口切换（SetWinEventHook，立即）+ 每秒轮询（覆盖"当前窗口原地进入全屏"，
/// 如浏览器视频全屏 / 无边框全屏游戏）。
/// 全屏状态变化时回调，由外部对所有进度条做临时隐藏 / 恢复（不动配置里的"显示进度条"）。
/// </summary>
public static class FullscreenWatcher
{
    private const uint EVENT_SYSTEM_FOREGROUND = 0x0003;
    private const uint WINEVENT_OUTOFCONTEXT = 0x0000;
    private const uint WINEVENT_SKIPOWNPROCESS = 0x0002;

    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(1);

    private delegate void WinEventDelegate(
        IntPtr hWinEventHook, uint eventType, IntPtr hwnd,
        int idObject, int idChild, uint dwEventThread, uint dwmsEventTime);

    [DllImport("user32.dll")]
    private static extern IntPtr SetWinEventHook(
        uint eventMin, uint eventMax, IntPtr hmodWinEventProc,
        WinEventDelegate pfnWinEventProc, uint idProcess, uint idThread, uint dwFlags);

    [DllImport("user32.dll")]
    private static extern bool UnhookWinEvent(IntPtr hWinEventHook);

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    private static Action<bool>? _onChanged;

    /// <summary>持有原生回调引用，防止被 GC 回收。</summary>
    private static WinEventDelegate? _callback;

    private static IntPtr _hook = IntPtr.Zero;
    private static DispatcherTimer? _pollTimer;

    /// <summary>当前是否处于全屏状态（最后检测结果）。</summary>
    public static bool IsFullscreen { get; private set; }

    /// <summary>开始监听（须在 UI 线程调用）；状态变化（含启动时首次检测）时回调 isFullscreen。</summary>
    public static void Start(Action<bool> onChanged)
    {
        _onChanged = onChanged;

        _callback = (_, _, hwnd, _, _, _, _) => Update(hwnd);
        _hook = SetWinEventHook(
            EVENT_SYSTEM_FOREGROUND, EVENT_SYSTEM_FOREGROUND,
            IntPtr.Zero, _callback,
            0, 0,
            WINEVENT_OUTOFCONTEXT | WINEVENT_SKIPOWNPROCESS);

        _pollTimer = new DispatcherTimer { Interval = PollInterval };
        _pollTimer.Tick += (_, _) => Update(GetForegroundWindow());
        _pollTimer.Start();

        Update(GetForegroundWindow()); // 启动即检测一次，保证初始状态正确
    }

    /// <summary>停止监听并释放句柄 / 定时器。</summary>
    public static void Stop()
    {
        if (_hook != IntPtr.Zero)
        {
            UnhookWinEvent(_hook);
            _hook = IntPtr.Zero;
        }

        _pollTimer?.Stop();
        _pollTimer = null;
        _callback = null;
        _onChanged = null;
    }

    /// <summary>按当前状态立即重新评估（"全屏隐藏"开关切换后调用，保证立即生效）。</summary>
    public static void ApplyNow() => _onChanged?.Invoke(IsFullscreen);

    private static void Update(IntPtr hwnd)
    {
        var fullscreen = Win32WindowHelper.IsFullscreenWindow(hwnd);
        if (fullscreen == IsFullscreen)
            return;

        IsFullscreen = fullscreen;
        _onChanged?.Invoke(fullscreen);
    }
}
