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

    private void NoteEditor_Loaded(object sender, RoutedEventArgs e)
    {
        var editor = (SegmentEditor)sender;
        var note = (SegmentNote)editor.DataContext;
        editor.BindNote(note);
        editor.RecentColors = _recentColors;
        editor.Changed += NoteEditor_Changed;
        editor.DeleteRequested += NoteEditor_Delete;
        editor.MoveUpRequested += (_, _) => NoteEditor_Move(editor, -1);
        editor.MoveDownRequested += (_, _) => NoteEditor_Move(editor, +1);
    }

    private void NoteEditor_Unloaded(object sender, RoutedEventArgs e)
    {
        var editor = (SegmentEditor)sender;
        editor.Changed -= NoteEditor_Changed;
        editor.DeleteRequested -= NoteEditor_Delete;
    }

    private void NoteEditor_Changed(object? sender, EventArgs e)
    {
        var editor = (SegmentEditor)sender!;
        var vm = CurrentInstance;
        if (vm is null)
            return;

        var conflicts = vm.ValidateNoteChange();
        if (conflicts.Count > 0)
        {
            if (ConflictDialog.Ask(this, conflicts))
            {
                foreach (var c in conflicts.ToList())
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
        vm.RemoveNote(((SegmentEditor)sender!).Note);
    }

    private void NoteEditor_Move(SegmentEditor editor, int delta)
    {
        var vm = CurrentInstance;
        if (vm is null)
            return;
        vm.MoveNote(editor.Note, delta);
    }

    /// <summary>新增后的重叠校验：确认删除全部冲突备注，否则移除刚新增的备注。</summary>
    private void ResolveConflicts(InstanceViewModel vm, SegmentNote keep)
    {
        var conflicts = vm.ValidateNoteChange();
        if (conflicts.Count == 0)
            return;

        if (ConflictDialog.Ask(this, conflicts))
        {
            foreach (var c in conflicts.ToList())
                vm.RemoveNote(c);
        }
        else if (vm.Notes.Contains(keep))
        {
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
