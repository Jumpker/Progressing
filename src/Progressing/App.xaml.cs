using System.IO;
using System.Threading;
using System.Windows;
using System.Windows.Threading;
using Microsoft.Win32;
using Progressing.Models;
using Progressing.Services;
using Progressing.ViewModels;
using Progressing.Windows;

namespace Progressing;

/// <summary>
/// 应用入口（技术实现说明书 §3.11）：
/// 启动时初始化 配置 / 计时 / 自启 / 实例管理器 / 托盘；首次运行弹出气泡提示；
/// 退出时立即落盘并释放托盘。
/// </summary>
public partial class App : Application
{
    private static Mutex? _mutex;
    private EventWaitHandle? _wakeEvent;

    private ConfigService? _config;
    private InstanceManager? _manager;
    private TrayService? _tray;
    private MainViewModel? _mainVm;
    private SettingsWindow? _settingsWindow;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // 单实例保护：重复启动时通知已有实例弹出设置窗口，然后本实例退出
        if (!AcquireSingleInstance())
        {
            SignalExistingInstance();
            Shutdown();
            return;
        }

        // 兜底异常日志：写入运行目录 error.log，便于诊断
        DispatcherUnhandledException += (_, args) =>
        {
            LogError("Dispatcher", args.Exception);
            args.Handled = false;
        };
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
            LogError("AppDomain", args.ExceptionObject as Exception);
        TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            LogError("UnobservedTask", args.Exception);
            args.SetObserved();
        };

        try
        {
            InitializeApp();
        }
        catch (Exception ex)
        {
            LogError("Startup", ex);
            throw;
        }
    }

    private void InitializeApp()
    {
        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        _config = new ConfigService();
        ThemeService.Apply(_config.Config.Theme); // 启动即按配置应用主题（跟随系统 / 浅色 / 深色）

        var timeService = new TimeService();
        _manager = new InstanceManager(_config, timeService);
        _manager.Initialize();

        _mainVm = new MainViewModel(_manager, _config);

        _tray = new TrayService();
        _tray.OpenSettingsRequested += ShowSettings;
        _tray.NewInstanceRequested += NewInstance;
        _tray.ExitRequested += ExitApp;

        if (_config.FirstRun)
            _tray.ShowFirstRunBalloon();

        // 监听系统深浅色变化："跟随系统"模式下实时同步
        SystemEvents.UserPreferenceChanged += OnSystemThemeChanged;

        // 置顶守卫：前台切换到任务栏等场景时，把所有"置顶 + 可见"的进度条重新断言为置顶，
        // 避免任务栏（本身是置顶窗口）盖住进度条
        TopmostGuard.Start(() => _manager?.AssertAllTopmost());

        // 全屏监视器：全屏视频 / 游戏时自动隐藏进度条（开关见全局设置"全屏隐藏"，默认开启）
        FullscreenWatcher.Start(fullscreen =>
            _manager?.SetFullscreenHidden(fullscreen && _config!.Config.HideOnFullscreen));

        // 监听"再次启动"信号：收到后弹出设置窗口（单实例模式）
        StartWakeListener();
    }

    /// <summary>单实例互斥量（Local 前缀 = 仅当前登录会话，不同用户互不影响）。</summary>
    private const string SingleInstanceMutexName = @"Local\Progressing.SingleInstance";

    /// <summary>唤醒信号：第二实例启动时通知主实例弹出设置窗口。</summary>
    private const string WakeEventName = @"Local\Progressing.SingleInstanceWake";

    /// <summary>尝试获取单实例互斥量；成功返回 true（成为主实例）。</summary>
    private bool AcquireSingleInstance()
    {
        try
        {
            _mutex = new Mutex(false, SingleInstanceMutexName);
            return _mutex.WaitOne(TimeSpan.Zero);
        }
        catch (AbandonedMutexException)
        {
            return true; // 前一个实例已崩溃：接管互斥量，继续作为主实例
        }
        catch
        {
            return true; // 互斥量异常时放行，避免影响正常启动
        }
    }

    /// <summary>通知已在运行的实例弹出设置窗口。</summary>
    private void SignalExistingInstance()
    {
        try
        {
            using var evt = EventWaitHandle.OpenExisting(WakeEventName);
            evt.Set();
        }
        catch
        {
            // 主实例未在监听（如版本差异）：忽略，直接退出即可
        }
    }

    /// <summary>主实例后台监听"再次启动"信号，收到后切回 UI 线程弹出设置窗口。</summary>
    private void StartWakeListener()
    {
        _wakeEvent = new EventWaitHandle(false, EventResetMode.AutoReset, WakeEventName);
        var thread = new Thread(() =>
        {
            try
            {
                while (_wakeEvent.WaitOne())
                    Dispatcher.BeginInvoke(() => ShowSettings());
            }
            catch
            {
                // 应用退出（句柄被释放）时终止监听线程
            }
        }) { IsBackground = true };
        thread.Start();
    }

    /// <summary>系统主题外观变化（深/浅色切换）时，跟随系统模式下即时换肤。</summary>
    private void OnSystemThemeChanged(object? sender, UserPreferenceChangedEventArgs e)
    {
        if (e.Category != UserPreferenceCategory.General)
            return;

        // 事件在系统线程触发，切回 UI 线程再应用
        Dispatcher.BeginInvoke(() =>
        {
            if (_config?.Config.Theme == AppTheme.System)
                ThemeService.Apply(AppTheme.System);
        });
    }

    private static void LogError(string tag, Exception? ex)
    {
        try
        {
            File.AppendAllText(
                Path.Combine(AppContext.BaseDirectory, "error.log"),
                $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {tag}\n{ex}\n\n");
        }
        catch
        {
            // 日志写入失败时静默
        }
    }

    private void ShowSettings()
    {
        _settingsWindow ??= new SettingsWindow(_mainVm!);
        _settingsWindow.Show();
        _settingsWindow.Activate();
    }

    private void NewInstance()
    {
        _mainVm!.AddInstanceCommand.Execute(null);
        ShowSettings();
    }

    private void ExitApp()
    {
        _config?.SaveNow();
        _settingsWindow?.AllowClose();
        _manager?.Shutdown();
        Shutdown();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        SystemEvents.UserPreferenceChanged -= OnSystemThemeChanged;
        TopmostGuard.Stop();
        FullscreenWatcher.Stop();
        _config?.SaveNow();
        _tray?.Dispose();
        _wakeEvent?.Dispose(); // 释放唤醒句柄，监听线程随之退出
        _mutex?.Dispose();     // 释放互斥量，下次启动可正常成为主实例
        base.OnExit(e);
    }
}
