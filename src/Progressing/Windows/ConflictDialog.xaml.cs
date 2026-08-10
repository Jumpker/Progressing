using System.Windows;
using Progressing.Models;

namespace Progressing.Windows;

/// <summary>
/// 备注重叠冲突确认弹窗（产品设计书 §3.3.1）：列出全部冲突备注，确认则删除、取消则放弃本次操作。
/// </summary>
public partial class ConflictDialog : Window
{
    public bool Confirmed { get; private set; }

    public ConflictDialog(IReadOnlyList<SegmentNote> conflicts)
    {
        InitializeComponent();
        ConflictList.ItemsSource = conflicts.Select(c => new ConflictItem
        {
            Range = $"{c.Start} ~ {c.End}",
            Text = string.IsNullOrWhiteSpace(c.Text) ? "（无文案）" : c.Text,
        }).ToList();
    }

    public static bool Ask(Window owner, IReadOnlyList<SegmentNote> conflicts)
    {
        var dialog = new ConflictDialog(conflicts)
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

    private sealed class ConflictItem
    {
        public required string Range { get; init; }

        public required string Text { get; init; }
    }
}
