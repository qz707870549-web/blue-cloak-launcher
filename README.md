# 蓝斗篷启动 · Blue Cloak Launcher

面向 Windows 的应用分组与快捷启动工具。支持应用、文件夹和网站，提供可选的开始菜单入口及工作空间视图。

**公开版 v1.3.7**。只包含通用默认分组，不内置个人应用清单、网站、文件夹路径或分组备份。第一次运行会扫描本机应用。

## 下载与运行

在本仓库 **Releases** 下载 `BlueCloakLauncher-v1.3.7-public-Windows-x64.zip`，解压到可写的固定文件夹，运行 `BlueCloakLauncher.exe`。GitHub 自动提供的 Source code 是源码，不是可直接运行的软件。

运行环境：Windows 10/11 x64、.NET Framework 4.8。程序无需安装 .NET SDK。发布文件未进行商业代码签名。

## 功能

- 自动发现本机快捷方式与应用，过滤重复入口、系统附带项目和软件配套工具。
- 分类创建、重命名、隐藏、排序；组内拖动排序与虚拟应用文件夹。
- 手动置顶，横向多行展示；收藏、最近使用、搜索与批量整理。
- 文件夹收藏、网站快捷方式、网站图标与自定义图标。
- 设置中开启或关闭工作空间，深浅主题与减少动画。
- 鼠标开始按钮与单按 Win 键呼出；保留 Windows 组合快捷键，可分别关闭入口接管。
- 主窗口置顶，点击外部或按 Esc 收起到后台。
- 单文件 `.lumabackup` 分组导出和导入，导入前保存现有配置快照。
- 用户可控的登录启动：当前用户计划任务及备用启动项，关闭时移除。

不包含 Dock、桌面状态组件和自动“建议置顶”。

## 从源码编译

本项目沿用 C# / WPF / .NET Framework 技术栈，通过 PowerShell 调用固定版本 Roslyn 编译器。没有要求 Visual Studio 工程或 SDK 风格项目。

在仓库根目录用 Windows PowerShell 5.1 或 PowerShell 7 执行：

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\build.ps1
```

首次编译从 NuGet 下载 Roslyn **4.14.0** 到本仓库 `artifacts/tools/`。输出为 `dist/LumaDock.exe`。内部文件名及 `Luma` 命名空间用于兼容旧版；分发文件名为 `BlueCloakLauncher.exe`。

```powershell
# 隔离的离线功能验证
powershell -NoProfile -ExecutionPolicy Bypass -File .\verify.ps1

# 先验证，再生成公开发行 ZIP 和 SHA256 校验文件
powershell -NoProfile -ExecutionPolicy Bypass -File .\package.ps1
```

`-ExecutionPolicy Bypass` 只作用于这次 PowerShell 进程，不更改系统永久执行策略。

## 目录

```text
src/                    当前应用源码、主题、品牌头像、字体与通用分组
tests/                  数据、布局、窗口尺寸和图标的隔离验证
docs/                   使用说明、隐私说明、GitHub 发布步骤与版本说明
build.ps1               编译程序
verify.ps1              编译并执行离线验证
package.ps1             白名单打包，排除本机数据
LICENSE                 代码 MIT 许可证
THIRD_PARTY_NOTICES.md   字体、品牌资源及贡献来源
```

## 数据与隐私

运行数据保存在程序旁的 `data/`。旧个人模式仍可识别 `personal-data/`；更新时保留原数据目录。公开版不会自动导入旧位置的个人配置，需使用软件内的导入入口。

网站图标获取会访问对应网站；扫描和分组数据保存在本机。没有加入遥测或自动上传应用清单。日志及备份可能包含本机路径和网址，反馈问题前请脱敏。

详见 [隐私与数据说明](docs/隐私与数据.md) 和 [使用说明](docs/使用说明.md)。

## 验证范围与限制

离线检查不调用应用启动入口、不打开原生窗口、不安装键盘或鼠标钩子，不修改系统启动项。实际登录重启、自启动、开始按钮接管、窗口层级及不同机器 DPI 仍需手动验证。网站图标可能因网络或站点限制获取失败；程序路径失效可通过重新发现或编辑快捷入口修复。

安全桌面、独占全屏及其他置顶窗口由 Windows 管理。退出本程序后，开始菜单入口恢复系统行为。

## 贡献与许可

欢迎通过 Issues 提交问题，通过 Pull Requests 贡献修改。请先阅读 [CONTRIBUTING.md](CONTRIBUTING.md)。

应用代码采用 [MIT](LICENSE)，字体使用随附 OFL 授权。品牌图片及第三方资源的适用范围见 [第三方说明](THIRD_PARTY_NOTICES.md)。
