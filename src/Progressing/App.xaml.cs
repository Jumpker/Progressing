using System.IO;
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
    private ConfigService? _config;
    private InstanceManager? _manager;
    private TrayService? _tray;
    private MainViewModel? _mainVm;
    private SettingsWindow? _settingsWindow;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

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
        _config?.SaveNow();
        _tray?.Dispose();
        base.OnExit(e);
    }
}
