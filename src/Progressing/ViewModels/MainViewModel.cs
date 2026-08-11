using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Progressing.Models;
using Progressing.Services;
using Progressing.Windows;

namespace Progressing.ViewModels;

/// <summary>
/// 设置窗口根 VM（技术实现说明书 §3.12）：
/// 标签页集合（全局页 + 各实例页）、选中页、新建 / 复制 / 删除 / 重命名命令。
/// </summary>
public partial class MainViewModel : ObservableObject
{
    private readonly InstanceManager _manager;

    /// <summary>标签页集合：首个为全局页，其后为各实例页。</summary>
    public ObservableCollection<TabViewModel> Tabs { get; } = new();

    /// <summary>全局页（恒存在）。</summary>
    public GlobalViewModel Global { get; }

    [ObservableProperty]
    private TabViewModel? _selectedTab;

    /// <summary>当前选中的实例页（全局页被选中时为 null）。</summary>
    public InstanceViewModel? SelectedInstance => SelectedTab as InstanceViewModel;

    public MainViewModel(InstanceManager manager, ConfigService configService)
    {
        _manager = manager;
        Global = new GlobalViewModel(configService);
        Tabs.Add(Global);

        foreach (var window in manager.Windows)
            Tabs.Add(CreateInstanceVm(window));

        if (Tabs.Count > 1)
            SelectedTab = Tabs[1];
        else
            SelectedTab = Global;

        _manager.InstancesChanged += OnInstancesChanged;
    }

    private void OnInstancesChanged()
    {
        // 外部实例变化（如托盘新建）时同步标签页
        var existing = Tabs.OfType<InstanceViewModel>().Select(v => v.Config.Id).ToHashSet();
        foreach (var window in _manager.Windows)
        {
            if (!existing.Contains(window.Config.Id))
            {
                var vm = CreateInstanceVm(window);
                Tabs.Add(vm);
                SelectedTab = vm;
            }
        }

        foreach (var vm in Tabs.OfType<InstanceViewModel>().ToList())
        {
            if (!_manager.Windows.Any(w => ReferenceEquals(w, vm.Window)))
            {
                Tabs.Remove(vm);
            }
        }

        DeleteInstanceCommand.NotifyCanExecuteChanged();
    }

    private InstanceViewModel CreateInstanceVm(BarWindow window)
        => new(window, _manager);

    public void CommitRename(InstanceViewModel vm, string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return;
        _manager.Rename(vm.Window, name);
        vm.NotifyHeaderChanged();
    }

    // ---------------- 命令 ----------------

    /// <summary>新建进度条（自动编号命名并进入重命名状态）。</summary>
    [RelayCommand]
    private void AddInstance()
    {
        if (_manager.Windows.Count >= InstanceManager.MaxInstances)
            return;

        // Create 内部触发 InstancesChanged → OnInstancesChanged 已添加标签并选中，无需重复添加
        var window = _manager.Create();
        if (Tabs.OfType<InstanceViewModel>().FirstOrDefault(v => ReferenceEquals(v.Window, window)) is { } vm)
            vm.IsRenaming = true;
        DeleteInstanceCommand.NotifyCanExecuteChanged();
    }

    /// <summary>复制实例。</summary>
    [RelayCommand]
    private void CloneInstance(InstanceViewModel? vm)
    {
        if (vm is null)
            return;

        // Clone 内部触发 InstancesChanged → OnInstancesChanged 已添加标签并选中，无需重复添加
        _manager.Clone(vm.Window);
        DeleteInstanceCommand.NotifyCanExecuteChanged();
    }

    /// <summary>删除实例；最后一条不可删除。</summary>
    [RelayCommand(CanExecute = nameof(CanDeleteInstance))]
    private void DeleteInstance(InstanceViewModel? vm)
    {
        if (vm is null || !CanDeleteInstance(vm))
            return;

        _manager.Delete(vm.Window);
        Tabs.Remove(vm);
        if (ReferenceEquals(SelectedTab, vm))
            SelectedTab = Tabs.Count > 1 ? Tabs[1] : Tabs[0];
        DeleteInstanceCommand.NotifyCanExecuteChanged();
    }

    private bool CanDeleteInstance(InstanceViewModel? vm)
        => vm is not null && _manager.Windows.Count > 1;

    /// <summary>进入重命名状态。</summary>
    [RelayCommand]
    private void RenameInstance(InstanceViewModel? vm)
    {
        if (vm is null)
            return;
        vm.IsRenaming = true;
    }
}
