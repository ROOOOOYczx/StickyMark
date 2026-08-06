# StickyMark Native

StickyMark 的 Windows 原生桌面版本，使用 C# WinForms + WebView2 编写，不依赖 Python，也不使用 WPF 窗口。

界面采用 Apple Design 风格：克制的系统色、圆角材质、清晰的排版层级、轻量阴影和即时按下反馈。

## 直接运行

解压 `StickyMark-WebView2-win-x64.zip` 后，双击其中的 `StickyMark.exe`；源码目录也可以双击当前目录下的 `run.bat`。

发布文件是 self-contained 单文件程序，目标为 Windows x64，用户不需要预装 Python 或 .NET；WebView2 页面使用系统已安装的 Microsoft Edge WebView2 Runtime。

## 功能

- 全局快捷键 `Ctrl+Alt+Space`
- 无边框便签窗口，顶部工具区融入便签纸背景
- 主窗口使用融入页面的自定义标题栏，支持拖动、最小化、最大化和关闭
- 顶部 `✦` 区域可拖动窗口
- 所见即所得 Markdown 编辑，不需要手动输入 Markdown 符号
- 工具栏支持字体、字号、粗体、斜体、下划线、删除线、标题级别、无序列表和有序列表
- 选中文本后可选择复制为普通文本或 Markdown
- 自定义字体、字号、便签背景色、明暗主题和窗口置顶
- 单实例运行：重复打开程序会唤起已有实例，不会创建第二个后台进程
- 开机自动启动开关
- 静默启动到系统托盘开关
- 关闭主页后收进系统托盘开关
- 主页、便签列表、设置页
- 本地 JSON 自动保存到 `%APPDATA%\StickyMark\`

## 从源码重新发布

要求安装 .NET 8 SDK，并联网下载 Windows 运行时包和 Microsoft.Web.WebView2。执行：

```powershell
.\publish.ps1
```

发布结果在 `release-webview2\StickyMark.exe`。
