using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Progressing.Controls;
using Progressing.Core;
using Progressing.Models;
using Progressing.Services;
using Progressing.ViewModels;

namespace Progressing.Windows;

/// <summary>
/// 主设置窗口（技术实现说明书 §3.12）：
/// 标签栏（全局页 + 实例页，右键菜单 重命名/复制/删除）+ 实例分区（外观/位置/指针/时间段/文字样式/其它）。
/// 点击 X 隐藏到托盘继续运行；退出仅通过托盘右键（AllowClose 由 App 在退出前调用）。
/// </summary>
public partial class SettingsWindow : Window
{
    private readonly MainViewModel _vm;
    private readonly List<string> _recentColors;
    private bool _allowClose;

    public SettingsWindow(MainViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        _recentColors = vm.Global.RecentColors;
        DataContext = vm;
    }

    /// <summary>允许真正关闭（App 退出前调用）。</summary>
    public void AllowClose() => _allowClose = true;

    protected override void OnClosing(CancelEventArgs e)
    {
        if (!_allowClose)
        {
            e.Cancel = true;
            Hide();
        }

        base.OnClosing(e);
    }

    /// <summary>当前选中的实例页（全局页被选中时为 null）。</summary>
    private InstanceViewModel? CurrentInstance => _vm.SelectedInstance;

    // ---------------- 标签栏 ----------------

    private void AddInstance_Click(object sender, RoutedEventArgs e)
        => _vm.AddInstanceCommand.Execute(null);

    // ---------------- 标签右键菜单（重命名 / 复制 / 删除） ----------------

    /// <summary>右键菜单对应的实例 VM（命令在 MainViewModel 上，故经 Click 事件取 PlacementTarget 的 DataContext）。</summary>
    private static InstanceViewModel? TabVmFromMenu(object sender)
    {
        var menuItem = (MenuItem)sender;
        var menu = menuItem.Parent as ContextMenu;
        return (menu?.PlacementTarget as FrameworkElement)?.DataContext as InstanceViewModel;
    }

    private void TabRename_Click(object sender, RoutedEventArgs e)
    {
        if (TabVmFromMenu(sender) is { } vm)
            _vm.RenameInstanceCommand.Execute(vm);
    }

    private void TabClone_Click(object sender, RoutedEventArgs e)
    {
        if (TabVmFromMenu(sender) is { } vm)
            _vm.CloneInstanceCommand.Execute(vm);
    }

    private void TabDelete_Click(object sender, RoutedEventArgs e)
    {
        if (TabVmFromMenu(sender) is { } vm)
            _vm.DeleteInstanceCommand.Execute(vm);
    }

    private void RenameBox_KeyDown(object sender, KeyEventArgs e)
    {
        var box = (TextBox)sender;
        if (e.Key == Key.Enter)
            CommitRename(box);
        else if (e.Key == Key.Escape)
            CancelRename(box);
    }

    private void RenameBox_LostFocus(object sender, RoutedEventArgs e)
        => CommitRename((TextBox)sender);

    private void CommitRename(TextBox box)
    {
        if (box.DataContext is InstanceViewModel vm)
        {
            _vm.CommitRename(vm, vm.RenameText);
            vm.IsRenaming = false;
        }
        else if (box.DataContext is TabViewModel tab)
        {
            tab.IsRenaming = false;
        }
    }

    private static void CancelRename(TextBox box)
    {
        if (box.DataContext is TabViewModel tab)
            tab.IsRenaming = false;
    }

    // ---------------- 时间段备注 ----------------

    private void AddNote_Click(object sender, RoutedEventArgs e)
    {
        var vm = CurrentInstance;
        if (vm is null)
            return;

        vm.AddNote();
        ResolveConflicts(vm, keep: vm.Notes[^1]);
    }

    /// <summary>切换方向：横向 / 竖向（保持进度条几何中心不动）。</summary>
    private void SetHorizontal_Click(object sender, RoutedEventArgs e)
        => CurrentInstance?.SetOrientation(BarOrientation.Horizontal);

    private void SetVertical_Click(object sender, RoutedEventArgs e)
        => CurrentInstance?.SetOrientation(BarOrientation.Vertical);

    /// <summary>备注文字排列方向：横排 / 竖排。</summary>
    private void SetArrangementHorizontal_Click(object sender, RoutedEventArgs e)
    {
        var vm = CurrentInstance;
        if (vm is not null)
            vm.TextArrangement = TextArrangement.Horizontal;
    }

    private void SetArrangementVertical_Click(object sender, RoutedEventArgs e)
    {
        var vm = CurrentInstance;
        if (vm is not null)
            vm.TextArrangement = TextArrangement.Vertical;
    }

    /// <summary>长度铺满当前屏幕（横放 = 宽度、竖放 = 高度）。</summary>
    private void FillScreenLength_Click(object sender, RoutedEventArgs e)
        => CurrentInstance?.FillScreen();

    private void NoteEditor_Loaded(object sender, RoutedEventArgs e)
    {
        var editor = (SegmentEditor)sender;
        var note = (SegmentNote)editor.DataContext;
        editor.BindNote(note);
        editor.RecentColors = _recentColors;
        editor.Changed += NoteEditor_Changed;
        editor.DeleteRequested += NoteEditor_Delete;
        editor.SortRequested += NoteEditor_Sort;
    }

    private void NoteEditor_Unloaded(object sender, RoutedEventArgs e)
    {
        var editor = (SegmentEditor)sender;
        editor.Changed -= NoteEditor_Changed;
        editor.DeleteRequested -= NoteEditor_Delete;
        editor.SortRequested -= NoteEditor_Sort;
    }

    private void NoteEditor_Changed(object? sender, EventArgs e)
    {
        var editor = (SegmentEditor)sender!;
        var vm = CurrentInstance;
        if (vm is null)
            return;

        // 只列出与正在编辑的这条备注重叠的其它备注（排除它自身），
        // 确认后删除冲突项、保留正在编辑的这条——否则编辑中这条也会被一并删除
        var conflicts = OverlapValidator.FindConflicts(vm.Config.Notes, editor.Note);
        if (conflicts.Count > 0)
        {
            if (ConflictDialog.Ask(this, conflicts))
            {
                foreach (var c in conflicts)
                    vm.RemoveNote(c);
            }
            else
            {
                editor.Revert();
                vm.Save();
            }
        }
        else
        {
            vm.Save();
        }
    }

    private void NoteEditor_Delete(object? sender, EventArgs e)
    {
        var vm = CurrentInstance;
        if (vm is null)
            return;

        if (!ConfirmDialog.Ask(this, "确定删除这个时间段？删除后其备注与颜色将一并移除。", "删除时间段"))
            return;

        vm.RemoveNote(((SegmentEditor)sender!).Note);
    }

    /// <summary>起止时间提交后按时间自动排序。</summary>
    private void NoteEditor_Sort(object? sender, EventArgs e)
    {
        var vm = CurrentInstance;
        if (vm is null)
            return;
        vm.SortNotes();
    }

    /// <summary>新增后的重叠校验：确认则删除与新建备注重叠的已有备注（保留新建），否则移除刚新增的备注。</summary>
    private void ResolveConflicts(InstanceViewModel vm, SegmentNote keep)
    {
        // 只找与新建备注重叠的已有备注（按 ID 排除自身），
        // 绝不能把新建的列入冲突列表，否则确认后新建的也会被删除
        var conflicts = OverlapValidator.FindConflicts(vm.Config.Notes, keep);
        if (conflicts.Count == 0)
            return;

        if (ConflictDialog.Ask(this, conflicts))
        {
            foreach (var c in conflicts)
                vm.RemoveNote(c);
        }
        else
        {
            // 取消 = 放弃本次新增：删除刚新建的这条（已有备注保留）
            vm.RemoveNote(keep);
        }
    }

    // ---------------- 位置编辑模式 ----------------

    private void EditMode_Click(object sender, RoutedEventArgs e)
        => CurrentInstance?.ToggleEditModeCommand.Execute(null);

    // ---------------- 颜色 ----------------

    private void BorderColor_Click(object sender, RoutedEventArgs e)
    {
        var vm = CurrentInstance;
        if (vm is null)
            return;

        var picked = ColorPickerDialog.Pick(this, Palettes.FromHex(vm.BorderColor), _recentColors);
        if (picked is { } c)
            vm.BorderColor = Palettes.ToHex(c);
    }

    private void TextColor_Click(object sender, RoutedEventArgs e)
    {
        var vm = CurrentInstance;
        if (vm is null)
            return;

        var picked = ColorPickerDialog.Pick(this, Palettes.FromHex(vm.TextColor), _recentColors);
        if (picked is { } c)
            vm.TextColor = Palettes.ToHex(c);
    }

    private void TextBorderColor_Click(object sender, RoutedEventArgs e)
    {
        var vm = CurrentInstance;
        if (vm is null)
            return;

        var picked = ColorPickerDialog.Pick(this, Palettes.FromHex(vm.TextBorderColor), _recentColors);
        if (picked is { } c)
            vm.TextBorderColor = Palettes.ToHex(c);
    }

    // ---------------- 指针文件 ----------------

    private void PointerBrowse_Click(object sender, RoutedEventArgs e)
    {
        var vm = CurrentInstance;
        if (vm is null)
            return;

        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = "选择指针图标",
            Filter = "图片文件|*.png;*.svg|PNG 图片|*.png|SVG 矢量图|*.svg",
        };
        if (dialog.ShowDialog(this) == true)
        {
            vm.PointerSource = PointerSource.File;
            vm.PointerFilePath = dialog.FileName;
        }
    }

    // ---------------- 其它 ----------------

    private void ResetDefaults_Click(object sender, RoutedEventArgs e)
    {
        var vm = CurrentInstance;
        if (vm is null)
            return;

        var result = MessageBox.Show(this,
            "恢复本条进度条为默认配置？（保留名称与显示状态，时间段备注清空）",
            "恢复默认配置",
            MessageBoxButton.OKCancel,
            MessageBoxImage.Question);
        if (result == MessageBoxResult.OK)
            vm.ResetToDefaultsCommand.Execute(null);
    }
}
