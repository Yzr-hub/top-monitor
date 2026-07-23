# TopMonitor 开发指南

## 环境安装

开发 Windows UI 推荐使用 Windows 11 x64、Visual Studio 2022/后续支持 .NET 10 的版本，以及 .NET 10 SDK。Visual Studio 工作负载选择“.NET 桌面开发”。可用以下命令确认环境：

```powershell
dotnet --info
dotnet --list-sdks
```

macOS/Linux 可以通过 `EnableWindowsTargeting` 做交叉编译和单元测试，但不能运行或调试 WPF，也不能验证 Windows 传感器。

## 打开、运行与调试

在 Visual Studio 中打开 `TopMonitor.sln`，将 `TopMonitor.App` 设为启动项目，选择 x64 后按 F5。命令行运行：

```powershell
dotnet restore
dotnet run --project src/TopMonitor.App/TopMonitor.App.csproj -c Debug
```

调试硬件问题时查看 `%LocalAppData%\TopMonitor\logs\`。可在 Provider、`MetricSamplingService`、`OverlayViewModel` 和 `SystemEventCoordinator` 设置断点。配置文件位于 `%LocalAppData%\TopMonitor\settings.json`；损坏文件会被备份并回退默认配置。

性能分析可使用 Visual Studio Diagnostic Tools、`dotnet-counters`、Windows 任务管理器和 PerfView。重点观察 CPU、工作集、GC 分配率、线程数以及最小化/静置时的资源变化。

## 添加一个新指标

1. 在 Domain 的 `MetricIds` 增加稳定 ID。
2. 增加 `MetricDefinition`，确定名称、类别、单位和格式。
3. 在合适 Provider 的 `SupportedMetrics` 中声明该 ID，并实现读取。
4. 为该指标选择默认采样周期；需要时在 `MetricValueCache` 添加抖动容差。
5. 在默认 Widget 配置和设置列表中暴露指标。
6. 为格式化、缓存和调度行为补充测试。

UI 不应知道该指标来自 Win32、网络接口还是传感器库。

## 添加一个新 Provider

实现 `IMetricProvider`：

- `SupportedMetrics` 必须准确声明能力。
- `ReadAsync` 只读取请求的指标，尊重 `CancellationToken`。
- 单个指标不可用时返回相应状态；可恢复错误不应导致进程退出。
- 需要释放句柄、计时器或硬件对象时实现 `IDisposable`/`IAsyncDisposable`。

然后在 `App.xaml.cs` 注册 Provider。测试使用 Fake Provider 验证调度，不让 CI 依赖真实硬件。

## 修改悬浮窗

布局和样式在 `MainWindow.xaml`；展示状态与命令在 `OverlayViewModel` 和 `MetricItemViewModel`。保持 code-behind 只处理窗口生命周期、位置和必须依赖 HWND 的行为。新增可配置外观时，同时更新 `OverlayConfig`、`SettingsViewModel`、`SettingsWindow.xaml` 和 JSON 兼容默认值。

悬浮窗必须继续满足：透明、置顶、可拖动、可点击穿透、限制在显示器工作区内，并在显示器变化后重新定位。

## Java 开发者快速对照

- C# `namespace`/`class`/接口与 Java 接近；属性 `public string Name { get; set; }` 通常替代显式 getter/setter。
- `record`/`record struct` 适合值语义模型；`MetricId` 类似受约束的不可变值对象。
- `event` 是类型安全的发布订阅；订阅者必须在释放时退订。
- `async`/`await` 类似 `CompletableFuture` 的顺序写法，但不要使用 `.Result` 阻塞 UI 线程。
- `CancellationToken` 是协作式取消，不等同于强制中断线程。
- `using`/`IDisposable` 类似 Java `try-with-resources`/`AutoCloseable`。
- WPF XAML 描述控件树；Binding 把控件属性连接到 ViewModel。
- `INotifyPropertyChanged` 类似可观察 Bean；`ObservableCollection<T>` 在集合变化时通知 UI。
- WPF Dispatcher 相当于 Swing EDT/JavaFX Application Thread；UI 对象只能在该线程更新。
- 依赖属性是 WPF 样式、动画和 Binding 的基础，不等同于普通 CLR 属性。

## 常见问题

**macOS 上为什么不能启动？** WPF 仅在 Windows 运行；macOS 只能编译、测试和发布 Windows 目标。

**温度显示不可用？** 先查看日志和传感器兼容表，再用管理员方式临时对比。不要默认要求管理员权限。

**设置没有保存？** 检查 `%LocalAppData%\TopMonitor\settings.json` 与日志目录权限。

**关闭窗口后程序仍在？** 关闭行为可能配置为隐藏到托盘；从托盘菜单选择退出。

**新增属性后界面不更新？** 确认 setter 触发 `PropertyChanged`，集合修改发生在 UI Dispatcher。

**测试不应访问真实硬件吗？** 是。硬件层通过 `IMetricProvider` 隔离，单元测试使用 Fake Provider。
