# TopMonitor 发布指南

## 前置条件

安装 .NET 10 SDK。Windows 11 x64 是最终运行验证环境；其他系统只能做交叉编译。版本号当前在 `src/TopMonitor.App/TopMonitor.App.csproj` 的 `<Version>` 中维护。

## 标准验证

在仓库根目录执行：

```powershell
dotnet restore TopMonitor.sln
dotnet build TopMonitor.sln -c Release --no-restore
dotnet test TopMonitor.sln -c Release --no-build
```

任何失败都应停止发布。

## win-x64 自包含发布

可直接运行：

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass `
  -File .\scripts\publish-win-x64.ps1
```

等价的 publish 命令为：

```powershell
dotnet publish src/TopMonitor.App/TopMonitor.App.csproj `
  -c Release `
  -r win-x64 `
  --self-contained true `
  -p:PublishSingleFile=true `
  --output artifacts/publish/win-x64
```

`self-contained` 会携带 .NET 运行时，目标电脑无需预装 .NET。最终便携版目录是 `artifacts/publish/win-x64/`，分发时压缩并传递整个目录，不要只复制 exe。

发布脚本会先下载并校验固定依赖：

- PresentMon 2.4.1 x64，SHA-256
  `D74183E7AE630F72CD3690BE0373ECBFDC6CBB86578148AAB8FA2A7166068F34`
- LibreHardwareMonitor 0.9.6 所带 PawnIO 安装器，SHA-256
  `A3A46226C5E2824F4CDD42BE0EECBABFC672C86F7889710F5AB1E6AD385B47A0`

来源、许可证和完整链接记录在 `third_party/NOTICE.md`。发布后两者位于
`artifacts/publish/win-x64/Dependencies/`。本次 Windows 验证中，
PresentMon 的 Authenticode 签名状态为 `Valid`（Intel Corporation），
PawnIO 安装器为 `Valid`（namazso.eu）。

## 单文件限制

`PublishSingleFile=true` 会尽量把托管程序集和运行时合并到 `TopMonitor.exe`，但原生库、驱动、符号文件或发布器判断必须外置的资源可能仍保留。LibreHardwareMonitor 及其依赖在不同版本下可能产生必须伴随 exe 的文件。

因此“单文件”是发布优化，不是分发契约。以实际发布目录为准：

- `TopMonitor.exe` 是启动入口。
- 当前发布实际保留 `D3DCompiler_47_cor3.dll`、`PenImc_cor3.dll`、`PresentationNative_cor3.dll`、`vcruntime140_cor3.dll` 和 `wpfgfx_cor3.dll` 等 WPF 原生运行时文件。
- LibreHardwareMonitor 的传递依赖当前还保留 `MonoPosixHelper.dll` 和 `libMonoPosixHelper.dll`。
- 上述外置 DLL 必须与 exe 一起分发；后续升级依赖后应重新检查清单，不能写死为永远不变。
- `.pdb` 只用于诊断，可在保留内部符号归档后从公开包移除。
- 不启用 trimming；WPF、反射和硬件库在未完整验证前不适合激进裁剪。

## Windows 最终验收

在干净的 Windows 11 x64 普通用户环境中：

1. 解压完整便携版目录并双击 `TopMonitor.exe`。
2. 确认悬浮窗和托盘图标出现，设置可保存。
3. 在设置 → 行为初始化 CPU 温度访问，确认 UAC 后验证 CPU 温度和
   `CPU temperature discovery` 日志。
4. 配置 FPS 权限，注销并重新登录；启用 FPS 后用 DirectX/OpenGL/Vulkan
   游戏验证整数帧率。
5. 让 CPU/GPU/网络数值变化，确认悬浮窗宽度不随实时值改变。
6. Alt+Tab 少于五秒，确认 PresentMon 不快速反复启动；退出游戏后 FPS
   应变为 `--`。
7. 验证拖动、置顶、点击穿透、显示器切换、休眠恢复和退出。
8. 重启应用，确认配置恢复；选择开机启动后检查登录行为。
9. 检查 `%LocalAppData%\TopMonitor\logs\` 无持续异常。
10. 从托盘退出后执行
    `Get-Process TopMonitor,PresentMon -ErrorAction SilentlyContinue`，确认
    没有 TopMonitor 或其拥有的 PresentMon 进程残留。
11. 在未安装 .NET 的机器复测自包含启动。

只有以上检查完成后，才能宣称便携包可在 Windows 双击运行。

## 版本号与产物

使用语义化版本 `主版本.次版本.修订号`，例如 `0.1.0`。发布前更新项目 `<Version>`，并以相同版本创建 Git 标签。建议归档：

```text
TopMonitor-0.1.0-win-x64-portable.zip
TopMonitor-0.1.0-symbols.zip
```

不要把 `artifacts/` 提交到源码仓库；由发布流程重新生成。

## 后续 Inno Setup

安装包暂不实现，预留 `installer/` 目录。未来 Inno Setup 脚本应：

- 将整个便携版目录安装到 `{autopf}\TopMonitor`。
- 创建开始菜单和可选桌面快捷方式。
- 通过应用自身的启动设置管理开机启动，避免重复注册。
- 升级时先退出托盘进程，再原位替换文件。
- 卸载时询问是否保留 `%LocalAppData%\TopMonitor` 的配置和日志。
- 对 exe、安装器以及未来的高权限 Collector 进行代码签名。

安装包发布前仍需重复执行便携版的 Windows 验收矩阵。
