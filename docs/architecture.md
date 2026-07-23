# TopMonitor 架构说明

## 总体架构

TopMonitor 是一个 .NET 10、WPF、MVVM 架构的 Windows 桌面硬件监控悬浮窗。系统把“指标定义”“采样编排”“硬件访问”和“桌面界面”分开，避免 UI 直接依赖传感器库或 Win32 API。

```text
Windows / LibreHardwareMonitor
              │
              ▼
Infrastructure Providers
              │ MetricValue
              ▼
Application Sampling + Cache
              │ change event
              ▼
App ViewModels ──binding──> WPF Overlay / Settings / Tray
              │
              └───────────> JSON Settings / Startup / Display services
```

依赖方向固定为 `App -> Application -> Domain` 和 `App -> Infrastructure -> Application/Domain`。Domain 不引用其他项目；Application 不引用 WPF、LibreHardwareMonitor 或 Win32。

## 项目分层

- `TopMonitor.Domain`：指标 ID、指标值、格式化、告警规则和用户配置等纯业务模型。
- `TopMonitor.Application`：Provider 接口、订阅、分组调度、缓存和变化通知。
- `TopMonitor.Infrastructure`：LibreHardwareMonitor、Windows API、网络接口、配置文件、开机启动和显示器实现。
- `TopMonitor.App`：WPF 窗口、MVVM、依赖注入、托盘菜单、应用生命周期和系统事件协调。
- `TopMonitor.Domain.Tests`、`TopMonitor.Application.Tests`：不依赖真实硬件的单元测试。

## 数据流

1. 应用启动时先从 `%LocalAppData%\TopMonitor\settings.json` 读取配置。
2. App 根据启用的 Widget 创建 `MetricSubscription`，交给 `MetricSamplingService`。
3. 调度器按刷新周期把指标分组，并只调用能够提供这些指标的 Provider。
4. Provider 返回带时间戳和状态的 `MetricValue`。
5. `MetricValueCache` 根据各指标容差判断是否发生有意义的变化。
6. 有变化时发布事件；`OverlayViewModel` 在 WPF Dispatcher 上更新可绑定属性。
7. 用户在设置窗口修改配置后，预览、订阅和持久化配置同步刷新。

## 线程模型

- WPF UI、窗口、托盘和 ViewModel 集合更新运行在 UI 线程。
- `MetricSamplingService` 使用异步循环和取消令牌在后台采样，不阻塞 UI。
- 不同 Provider 有独立执行门，避免同一个非线程安全 Provider 重入。
- Provider 异常被隔离并节流记录，单个采集器失败不会停止其他指标。
- 系统显示变化与休眠恢复事件进入 `SystemEventCoordinator`，再通过 Dispatcher 切回 UI 线程。
- 退出时按顺序停止调度器、注销系统事件、释放托盘图标和 Provider。

## 指标 ID 设计

`MetricId` 是稳定的字符串值对象，而不是 UI 文本或传感器索引。内置 ID 集中在 `MetricIds`，例如 CPU 温度、CPU 使用率、内存使用率和网络速率。稳定 ID 可用于配置持久化、Provider 路由、缓存键和未来协议通信。

新增指标时必须使用不会随语言或硬件顺序变化的 ID。多实例设备可采用稳定前缀加设备标识的方式，但不应直接使用每次启动可能变化的数组下标。

## 采样调度

订阅包含指标 ID 和采样周期。调度器将相同周期的指标合并，避免每个 Widget 各自创建计时器。配置变化会重建订阅；取消令牌保证退出或重载时旧循环终止。

缓存为不同指标设置抖动容差：温度、负载和百分比 0.1，网络速率 1024 B/s，已用内存 1 MiB。低于阈值的变化不会反复触发 UI 更新。

## 权限模型

默认桌面进程以普通用户权限运行。CPU 使用率、内存、网络和部分公开传感器通常不要求管理员权限；主板、风扇、电压、嵌入式控制器或底层驱动访问可能受系统策略限制。当前程序不会自动提权，也不会绕过驱动或反作弊保护。读取失败时指标应显示不可用并记录日志，而不是让应用退出。

## FPS 扩展方案

FPS 不应通过 WPF 或 LibreHardwareMonitor 猜测。后续可新增独立 `IFpsMetricProvider`，基于 PresentMon/ETW 消费目标进程的 Present 事件：

1. 用户选择或自动识别前台进程。
2. 采集模块以进程 ID 聚合帧呈现时间。
3. 计算滑动窗口 FPS、帧时间和 1% Low。
4. 通过现有 `MetricValue` 接入缓存与 UI。

ETW 会涉及会话权限、游戏反作弊兼容性、进程切换和较高数据量，因此应默认关闭并独立限流。不能支持的游戏必须明确显示不可用。

## 高权限采集进程拆分方案

若未来确实需要管理员权限，将采集能力拆为最小权限的 `TopMonitor.Collector`，UI 继续以普通用户运行。两者通过仅限本机、带访问控制列表的 Named Pipe 通信：

```text
TopMonitor.exe (普通权限)
       │ 版本化 DTO / 请求白名单
       ▼
Named Pipe (当前用户 SID ACL)
       │
       ▼
TopMonitor.Collector.exe (按需提权)
```

协议必须包含版本、请求超时、最大消息长度和指标白名单；Collector 不接受任意命令或路径。UI 断开时 Collector 应自动退出。安装、升级和签名需要同时覆盖两个二进制文件。
