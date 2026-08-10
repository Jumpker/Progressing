using System.Drawing;
using System.Windows;
using System.Windows.Controls;
using Hardcodet.Wpf.TaskbarNotification;

namespace Progressing.Services;

/// <summary>
/// 系统托盘 NotifyIcon 封装（技术实现说明书 §3.9）：
/// 左键单击开设置；右键菜单：新建进度条 / 打开设置 / 退出（不二次确认）。
/// </summary>
public class TrayService : IDisposable
{
    private readonly TaskbarIcon _tray;

    /// <summary>请求打开设置窗口。</summary>
    public event Action? OpenSettingsRequested;

    /// <summary>请求新建进度条。</summary>
    public event Action? NewInstanceRequested;

    /// <summary>请求退出程序。</summary>
    public event Action? ExitRequested;

    public TrayService()
    {
        _tray = new TaskbarIcon
        {
            ToolTipText = "Progressing 每日进度条",
            Icon = LoadIcon(),
            ContextMenu = BuildMenu(),
        };

        _tray.TrayLeftMouseUp += (_, _) => OpenSettingsRequested?.Invoke();
    }

    /// <summary>首次运行的气泡提示。</summary>
    public void ShowFirstRunBalloon()
        => _tray.ShowBalloonTip("Progressing", "右键托盘图标开始配置", BalloonIcon.Info);

    private ContextMenu BuildMenu()
    {
        var menu = new ContextMenu();

        var newItem = new MenuItem { Header = "新建进度条" };
        newItem.Click += (_, _) => NewInstanceRequested?.Invoke();
        menu.Items.Add(newItem);

        var settingsItem = new MenuItem { Header = "打开设置" };
        settingsItem.Click += (_, _) => OpenSettingsRequested?.Invoke();
        menu.Items.Add(settingsItem);

        menu.Items.Add(new Separator());

        var exitItem = new MenuItem { Header = "退出" };
        exitItem.Click += (_, _) => ExitRequested?.Invoke();
        menu.Items.Add(exitItem);

        return menu;
    }

    private static Icon LoadIcon()
    {
        try
        {
            var uri = new Uri("pack://application:,,,/Assets/Tray/Progressing.ico");
            var stream = Application.GetResourceStream(uri)?.Stream;
            if (stream is not null)
                return new Icon(stream);
        }
        catch
        {
            // 素材缺失时回退到运行时绘制的图标
        }

        return RuntimeFallbackIcon.Create();
    }

    public void Dispose()
    {
        _tray.Dispose();
    }
}

/// <summary>内置素材缺失时的运行时回退图标（简化水滴箭头造型）。</summary>
internal static class RuntimeFallbackIcon
{
    public static Icon Create()
    {
        using var bmp = new Bitmap(32, 32);
        using (var g = Graphics.FromImage(bmp))
        {
            g.Clear(Color.Transparent);
            using var pen = new Pen(Color.FromArgb(255, 90, 90, 90), 4f);
            g.DrawLine(pen, 2, 16, 30, 16); // 进度条
            g.FillEllipse(Brushes.OrangeRed, 20, 11, 10, 10); // 指针圆点
        }

        return Icon.FromHandle(bmp.GetHicon());
    }
}
