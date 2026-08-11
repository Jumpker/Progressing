using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;

namespace Progressing.Windows;

/// <summary>
/// 通用确认弹窗（无边框圆角 + 标题栏拖拽）：询问用户是否执行破坏性操作（如删除时间段）。
/// </summary>
public partial class ConfirmDialog : Window
{
    public bool Confirmed { get; private set; }

    public ConfirmDialog(string message, string title = "确认", string okText = "删除")
    {
        InitializeComponent();
        TitleText.Text = title;
        MessageText.Text = message;
        OkButton.Content = okText;
    }

    public static bool Ask(Window owner, string message, string title = "确认", string okText = "删除")
    {
        var dialog = new ConfirmDialog(message, title, okText)
        {
            Owner = owner,
        };
        return dialog.ShowDialog() == true && dialog.Confirmed;
    }

    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        Confirmed = true;
        DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        Confirmed = false;
        DialogResult = false;
    }

    /// <summary>拖拽标题栏移动窗口（无边框窗口的手动拖动支持）。</summary>
    private void DragStrip_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Left || IsOnInteractiveElement(e.OriginalSource))
            return;
        DragMove();
    }

    /// <summary>原点击目标是否位于可交互控件上（按钮等），是则不触发窗口拖动。</summary>
    private static bool IsOnInteractiveElement(object source)
    {
        for (var d = source as DependencyObject; d is not null; d = VisualTreeHelper.GetParent(d))
        {
            if (d is ButtonBase or TextBoxBase)
                return true;
        }

        return false;
    }
}
