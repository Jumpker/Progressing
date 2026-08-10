using System.Windows.Controls;

namespace Progressing.Windows;

/// <summary>
/// 编辑模式下备注文字容器上的拖动手柄（位置在 BarWindow 中通过 Canvas 定位）。
/// </summary>
public partial class EditModeOverlay : UserControl
{
    public EditModeOverlay()
    {
        InitializeComponent();
    }
}
