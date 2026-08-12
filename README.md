# Progressing —— 每天陪你完成计划的时间进度条

把一天 24 小时画成一条进度条，箭头标出现在的时刻，"今天还剩多少时间"一眼就能看到。常驻桌面角落，不抢焦点、不挡鼠标；所有数据只保存在你自己电脑上。

---

# 第一部分 使用（普通用户）

## 1. 下载

点此链接下载压缩包：

**https://github.com/Jumpker/Progressing/releases/download/v1.0.2/Progressing-win-x64-v1.0.2.zip**

## 2. 运行

1. 把压缩包解压到一个固定文件夹（如 `D:\Progressing`）；
2. 双击 **`Progressing.exe`**；
3. 若弹出"Windows 已保护你的电脑"：点 **更多信息 → 仍要运行**；或右键 exe → 属性 → 勾选 **"解除锁定"**，以后就不再提示。
> 没有病毒！不信你让AI审一下。🦠🚫

## 3. 基本用法

- 桌面上会出现一条**时间进度条**，箭头指向的位置 = 现在的时刻，整条 = 一天 24 小时。
- 右下角**托盘图标**是总开关：**左键单击** = 打开设置；**右键** = 新建进度条 / 打开设置 / 退出。
- 设置窗口里可以：开关置顶显示、调透明度、给一天分段上色并写备注、调整进度条位置等。

## 4. 数据与卸载

- 所有设置保存在本机：`C:\Users\你的用户名\AppData\Roaming\Progressing\config.json`
- **彻底重置**：先退出程序，再删除上面这个文件，重启即恢复初始状态。
- **卸载**：运行解压目录里的 **`uninstall.exe`**（会连同你的数据一起删除，其它文件不受影响）。

## 常见问题

| 问题 | 解决 |
|---|---|
| 进度条不见了 | 右键托盘图标 → 设置 → 确认勾选了"显示进度条"；或重新双击 exe；或用设置更改进度条位置 |
| 弹出"Windows 已保护你的电脑" | 点"更多信息 → 仍要运行"，或右键 exe → 属性 → 解除锁定 |
| 程序出问题 | 运行目录下会生成 `error.log`，把它发给开发者即可快速定位 |

---

# 第二部分 开发

## 技术栈

WPF（.NET 10 / C#）、CommunityToolkit.Mvvm、SkiaSharp（进度条渲染）、Hardcodet.NotifyIcon（托盘）。

## 环境要求

Windows 10/11 + .NET 10 SDK。

## 构建与测试

```powershell
dotnet build Progressing.slnx -c Release   # 构建
dotnet test                                 # 单元测试
```

## 发布（自包含，免装任何环境）

```powershell
dotnet publish src\Progressing\Progressing.csproj -c Release -r win-x64 --self-contained true
```

产物在 `src\Progressing\bin\Release\net10.0-windows\win-x64\publish\`，已自动完成瘦身（清理 PDB、仅保留中文语言包、卸载器裁剪），文件夹约 185 MB / 压缩后约 78 MB。整个 publish 文件夹压缩即可分发，内含：

- `Progressing.exe` —— 主程序
- `uninstall.exe` —— 卸载程序


## 目录结构

```
src/Progressing/                  WPF 主程序
├── Core/                         进度条渲染、时间映射等纯逻辑
├── Services/                     配置、托盘、主题、自启等
├── ViewModels/                   设置窗口视图模型
├── Windows/                      窗口与控件
src/Uninstaller/                  卸载程序（uninstall.exe）
tests/Progressing.Core.Tests/     单元测试
```

## 许可证

[MIT](LICENSE)
