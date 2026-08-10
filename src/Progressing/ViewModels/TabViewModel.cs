using CommunityToolkit.Mvvm.ComponentModel;

namespace Progressing.ViewModels;

/// <summary>设置窗口标签页基类（全局页 + 实例页）。</summary>
public abstract class TabViewModel : ObservableObject
{
    /// <summary>标签页标题。</summary>
    public abstract string Header { get; }

    /// <summary>是否为全局标签页（不可重命名 / 删除 / 复制，右键无菜单）。</summary>
    public virtual bool IsGlobal => false;

    private bool _isRenaming;

    /// <summary>是否处于重命名状态（新建实例自动进入）。</summary>
    public bool IsRenaming
    {
        get => _isRenaming;
        set
        {
            if (SetProperty(ref _isRenaming, value) && value)
                RenameText = Header; // 进入重命名时预填当前名称
        }
    }

    private string _renameText = "";

    /// <summary>重命名输入框文本（Header 为只读，不能直接 TwoWay 绑定，故走此可写属性）。</summary>
    public string RenameText
    {
        get => _renameText;
        set => SetProperty(ref _renameText, value);
    }
}
