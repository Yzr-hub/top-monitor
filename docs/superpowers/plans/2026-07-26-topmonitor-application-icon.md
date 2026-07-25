# TopMonitor 统一应用图标实施计划

> **执行要求：** 按任务逐项实施和验证；如使用代理执行，需先使用 `subagent-driven-development` 或 `executing-plans`。

**目标：** 生成已确认的“性能曲线”图标，并统一应用于 TopMonitor 的 EXE、WPF 窗口和系统托盘。

**架构：** 在 WPF 项目中保留可编辑的 SVG 源文件、1024 像素 PNG 预览文件和多尺寸 ICO 文件。ICO 同时作为 Win32 应用图标和 WPF 嵌入资源；托盘服务只在构造时加载一次该资源，替换当前动态生成的 “TM” 图标。

**技术栈：** .NET 10、C#、WPF、H.NotifyIcon.Wpf 2.4.1、SVG、PNG、Windows ICO。

## 全局约束

- 图标采用深蓝黑圆角方形背景，外部四角透明。
- 中央使用青色到绿色的上升性能曲线。
- 图标不包含文字。
- ICO 包含 16、20、24、32、40、48、64、128、256 像素图层。
- EXE、主窗口、设置窗口和系统托盘使用同一个嵌入图标。
- 发布目录不依赖外置图标文件。
- 不增加计时器、后台线程、重复绘制或持续分配。
- 保持现有 Windows x64 发布流程不变。

## 文件结构

- 新建 `src/TopMonitor.App/Assets/TopMonitor.svg`：可编辑矢量源文件。
- 新建 `src/TopMonitor.App/Assets/TopMonitor-1024.png`：高分辨率预览。
- 新建 `src/TopMonitor.App/Assets/TopMonitor.ico`：Windows 多尺寸图标。
- 修改 `src/TopMonitor.App/TopMonitor.App.csproj`：声明 EXE 图标和 WPF 资源。
- 修改 `src/TopMonitor.App/MainWindow.xaml`：设置主窗口图标。
- 修改 `src/TopMonitor.App/SettingsWindow.xaml`：设置设置窗口图标。
- 修改 `src/TopMonitor.App/Services/TrayIconService.cs`：加载统一托盘图标。
- 修改 `.gitignore`：忽略本地 `.superpowers/` 视觉草稿目录。

### 任务一：生成确定性的图标资产

- [ ] 先确认 `TopMonitor.ico` 尚不存在，得到预期的失败结果。
- [ ] 创建 1024×1024 SVG：透明画布、深蓝黑圆角底、青绿渐变性能曲线。
- [ ] 使用工作区自带图像工具生成 1024×1024 RGBA PNG。
- [ ] 从高分辨率源图生成包含九种指定尺寸的 ICO。
- [ ] 验证 PNG 尺寸、透明角和 ICO 图层集合。
- [ ] 导出并目视检查 16、32、256 像素预览，确保无裁切和透明异常。
- [ ] 将 `.superpowers/` 加入 `.gitignore`。
- [ ] 提交图标资产。

验证断言：

```python
assert png.mode == "RGBA"
assert png.size == (1024, 1024)
assert png.getpixel((0, 0))[3] == 0
assert ico_sizes == {
    (16, 16), (20, 20), (24, 24), (32, 32), (40, 40),
    (48, 48), (64, 64), (128, 128), (256, 256)
}
```

### 任务二：接入 EXE 和 WPF 窗口

- [ ] 使用 `rg` 确认项目当前没有 `ApplicationIcon` 和新资源引用。
- [ ] 在项目属性中加入：

```xml
<ApplicationIcon>Assets\TopMonitor.ico</ApplicationIcon>
```

- [ ] 将 ICO 声明为 WPF 资源：

```xml
<ItemGroup>
  <Resource Include="Assets\TopMonitor.ico" />
</ItemGroup>
```

- [ ] 在主窗口和设置窗口根节点加入：

```xml
Icon="/Assets/TopMonitor.ico"
```

- [ ] Release 构建并确认零错误。
- [ ] 从生成的 EXE 提取关联图标，目视对比源图。
- [ ] 提交 EXE 和窗口图标接入。

### 任务三：统一系统托盘图标

- [ ] 使用 `rg` 确认托盘当前仍包含 `GeneratedIconSource` 和 `Text = "TM"`。
- [ ] 将托盘的 `IconSource` 改为一次性加载嵌入资源：

```csharp
private static ImageSource LoadIconSource()
{
    var icon = new BitmapImage();
    icon.BeginInit();
    icon.UriSource = new Uri(
        "pack://application:,,,/Assets/TopMonitor.ico",
        UriKind.Absolute);
    icon.CacheOption = BitmapCacheOption.OnLoad;
    icon.EndInit();
    icon.Freeze();
    return icon;
}
```

- [ ] 删除不再需要的动态文字图标和画刷引用。
- [ ] 使用 `rg` 验证旧代码完全移除。
- [ ] 构建应用项目，要求零警告、零错误。
- [ ] 提交托盘图标接入。

### 任务四：完整验证、发布和推送

- [ ] 执行 `dotnet test TopMonitor.sln -c Release --no-restore`，要求所有测试通过。
- [ ] 使用以下方式绕过本机 PowerShell 执行策略并发布：

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\publish-win-x64.ps1
```

- [ ] 确认 `artifacts/publish/win-x64/TopMonitor.exe` 存在并带有新图标。
- [ ] 确认发布目录不需要外置 `TopMonitor.ico`。
- [ ] 启动发布版，检查进程、主窗口、设置窗口和系统托盘。
- [ ] 验证托盘显示/隐藏、打开设置和退出功能正常。
- [ ] 检查 Git 状态和最近提交。
- [ ] 将全部提交推送到 GitHub `main`。
