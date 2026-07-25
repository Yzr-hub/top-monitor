# TopMonitor CPU 温度、固定宽度、FPS 与性能优化设计

## 目标

本次改动解决四类问题：

1. 在 Intel Core i7-14700KF 等 Intel 14 代处理器上尽可能稳定地显示真实 CPU 温度。
2. 指标数值变化时保持悬浮条和各指标槽位宽度稳定，避免边框抖动和重复居中。
3. 自动检测当前前台游戏并显示实时 FPS，行为接近 NVIDIA 性能覆盖层。
4. 在功能完成后测量并优化常驻资源占用，保持普通空闲状态平均 CPU 尽量低于 1%。

TopMonitor 日常运行保持普通用户权限。需要安装硬件访问驱动或配置 ETW 权限时，才进行一次性管理员操作。

## 总体方案

采用按需采集、普通权限常驻的模块化方案：

- CPU 温度继续使用 LibreHardwareMonitor，通过 PawnIO 提供底层硬件访问。
- FPS 使用随便携包分发的 Intel PresentMon Console，只在启用 FPS 且检测到候选前台游戏时运行。
- 指标槽宽仅在配置变化时计算，采样值变化不再改变窗口宽度。
- 性能优化以测量结果为依据，优先处理已确认的重复硬件刷新和隐藏窗口事件开销。

不采用以下方案：

- 不让 TopMonitor 长期以管理员身份运行。
- 不要求用户常驻 HWiNFO、RTSS 或 LibreHardwareMonitor GUI。
- 不在缺少测量证据时直接移除 WPF `AllowsTransparency`，避免破坏透明圆角和点击穿透。

## 架构与组件

### TopMonitor.Domain

- 新增稳定指标 ID `graphics.foreground.fps`。
- FPS 的展示单位为 `FPS`。
- 默认配置包含 FPS Widget，用户可以启用、禁用和排序。
- 配置迁移必须为旧版 `settings.json` 补充 FPS Widget，不能覆盖用户已有设置。

### TopMonitor.Infrastructure

新增或调整以下组件：

- CPU 温度传感器选择器：
  - 输入 LibreHardwareMonitor 枚举到的温度传感器描述。
  - 只接受有限、非 NaN、处于 `-20°C` 至 `125°C` 范围内的当前值。
  - 按明确优先级返回一个传感器或不可用结果。
- PawnIO 状态与初始化服务：
  - 检测 PawnIO 是否可用。
  - 提供一次性管理员初始化入口。
  - 初始化完成后触发硬件重新扫描。
- 前台进程检测服务：
  - Win32 P/Invoke 集中在基础设施层。
  - 返回前台窗口的进程 ID 和基本进程信息。
  - 排除 TopMonitor、桌面、资源管理器和已知系统外壳进程。
- PresentMon 进程运行器：
  - 使用固定参数按 PID 启动 PresentMon。
  - 异步读取标准输出，不写逐帧 CSV 到磁盘。
  - 支持取消、超时、正常终止和异常退出。
- `PresentMonFpsProvider`：
  - 管理候选进程防抖、非游戏缓存、目标切换和 FPS 聚合。
  - 对外仍实现现有 `IMetricProvider`。

### TopMonitor.Application

- FPS 通过现有采样服务订阅，默认刷新周期为 500ms。
- Provider 内部维护最近约一秒的有效显示帧窗口，UI 只接收聚合后的 FPS。
- PresentMon、PawnIO 或单个传感器失败时，不影响 CPU 使用率、内存、网络和其他 Provider。
- LibreHardwareMonitor 对同一硬件保存最近更新时间，在最短刷新窗口内复用传感器快照。

### TopMonitor.App

- 设置页面显示 CPU 温度数据源、PawnIO 状态、FPS 权限状态和可操作提示。
- 提供一次性硬件/FPS 权限配置入口。
- 每个指标 ViewModel 暴露稳定槽宽。
- TextBlock 使用固定 `Width`，数值右对齐；指标列表、字号、标签、单位或间距变化时才重新计算。
- 设置窗口隐藏时暂停指标预览订阅，重新显示时从缓存恢复当前值。

## CPU 温度选择规则

候选传感器必须具有有效当前值。优先级如下：

1. 名称匹配 `CPU Package` 或明确的 Package 总温度。
2. 名称匹配 `Core Max`。
3. 名称匹配其他 Package、Tdie 或等价总温度。
4. 所有有效 CPU Core 温度中的最大值。

如果所有候选项均无效，指标返回不可用，不生成估算值。

日志记录：

- CPU 型号与硬件标识。
- 枚举到的温度传感器名称、标识和值状态。
- 最终选中的传感器及选择理由。
- PawnIO 状态以及重新扫描结果。

正常轮询不重复记录传感器清单；仅在启动、手动刷新、设备恢复或选择结果变化时记录。

## PawnIO 与权限

- 启动时只检测状态，不主动弹出 UAC。
- PawnIO 不可用时，CPU 温度显示 `--`，其他指标继续运行。
- 用户从设置页面明确触发一次性初始化。
- 初始化程序必须来自可信的固定来源，并在执行前验证发布者或固定哈希。
- 安装完成后，TopMonitor 以普通权限重新扫描硬件。
- 安装失败或用户取消时记录简洁日志并保持降级状态。

## FPS 数据流

1. 用户启用 FPS Widget 后，Provider 才开始检查前台进程。
2. 大约每 500ms 获取一次前台进程；同一候选进程稳定约 750ms 后才开始探测。
3. 排除 TopMonitor 和系统外壳进程。
4. PresentMon 使用候选进程 PID 启动，并将逐帧数据输出到标准输出。
5. 在探测窗口内检测到连续有效呈现帧后，将进程标记为图形应用并显示 FPS。
6. 未检测到帧时停止 PresentMon，并在该进程生命周期内缓存为非游戏，避免重复探测。
7. Alt+Tab 时保留短暂宽限期，避免频繁创建和终止 PresentMon。
8. 切换到另一游戏或目标退出时，先关闭旧会话，再启动新目标。
9. 在最近约一秒的滑动窗口中，使用有效显示帧数除以实际时间跨度计算 FPS，四舍五入为整数；每 500ms 最多通知 UI 一次。

PresentMon 不在 FPS Widget 禁用时运行，也不在没有候选游戏时持续运行。

## PresentMon 权限与分发

- 固定使用官方发布的 x64 PresentMon Console `2.4.1`，升级版本必须单独验证。
- 下载脚本或依赖清单记录官方发布地址和 SHA-256；构建时校验哈希后才纳入发布目录。
- 保留 PresentMon 许可证和第三方声明。
- 首次配置时可通过一次管理员操作将当前用户加入 Windows `Performance Log Users` 组。
- 用户组配置完成后可能需要注销并重新登录，设置页面必须明确说明。
- 不让 PresentMon 使用 `--restart_as_admin` 在每次进入游戏时重复请求 UAC。
- 发布脚本验证 PresentMon 文件存在并输出版本信息。

## FPS 降级行为

- PresentMon 文件缺失：FPS 显示 `--`，设置页面提示发布目录不完整。
- ETW 权限不足：FPS 显示 `--`，提示完成一次性权限配置。
- 不支持的图形呈现路径：FPS 显示 `--`。
- PresentMon 意外退出：限频记录日志，使用退避延迟重试，禁止快速重启循环。
- TopMonitor 退出：取消读取任务，正常结束子进程和 ETW 会话。
- 不伪造 FPS 数据。

## 固定宽度

- 为每种指标生成代表最长合理显示内容的测量文本。
- 根据当前字体、字号、标签、单位和格式计算像素宽度。
- TextBlock 固定使用该宽度并右对齐。
- 实时数值更新只改变 Text，不改变 Width。
- 只有以下事件触发重新测量：
  - 启用、禁用或排序指标。
  - 修改标签、单位显示、字号或水平间距。
  - 配置迁移或恢复默认。
- 数值优先使用现有紧凑单位格式；若最终文本仍超过槽宽，使用字符省略号截断，不能推动窗口变宽。

## 性能优化顺序

核心功能完成后按以下顺序处理：

1. 合并 LibreHardwareMonitor 同一硬件的短周期重复 `Update()`。
2. 设置窗口隐藏时暂停预览事件处理，显示时从 `MetricValueCache` 快照恢复。
3. 测量 `MetricSamplingService` 热路径中的 LINQ 与临时数组分配。
4. 只有在分配数据表明确指向该热路径时，才缓存 Provider 分组和指标数组。
5. 测量 WPF 分层窗口的 CPU/GPU 成本；本轮不直接替换 `AllowsTransparency`。

## 测试策略

### CPU 温度

- Package 有效时优先选择 Package。
- Package 无效、Core Max 有效时选择 Core Max。
- 前两者均无效时选择有效核心最高温度。
- 空值、NaN、无穷大和明显越界值被拒绝。
- 常见大小写和命名差异不影响匹配。
- 没有有效候选项时返回不可用。

### FPS

- PresentMon 输出解析。
- 最近一秒 FPS 聚合。
- 前台进程防抖和切换。
- 非游戏进程缓存。
- 权限错误、子进程退出、超时和取消。
- TopMonitor 退出时清理子进程。

### 固定宽度

- 每类指标生成稳定测量文本。
- 从短值变成长值时槽宽不变。
- 字号、标签、单位和指标列表变化时重新计算。

### 性能与回归

- 同一硬件在最短更新窗口内只执行一次 `Update()`。
- 设置窗口隐藏时不处理预览事件。
- Provider 异常隔离、采样取消和配置保存测试保持通过。
- 运行现有 Domain 和 Application 全量测试。

## 性能测量

至少记录以下三个场景，各观察 60 秒：

1. FPS 禁用、设置窗口隐藏、普通空闲。
2. FPS 启用但没有游戏，确认 PresentMon 不运行。
3. FPS 启用且前台游戏运行。

记录：

- TopMonitor 平均 CPU 和峰值 CPU。
- PresentMon 平均 CPU。
- 工作集和私有字节。
- 分配率、Gen 0 GC 次数、线程数和句柄数。
- 硬件 Provider 每分钟 `Update()` 次数。

使用 Windows 任务管理器、Visual Studio Diagnostic Tools 和 `dotnet-counters`。如仍有无法解释的热点，再使用 PerfView。

## 验收标准

- i7-14700KF 显示真实的 CPU Package、Core Max 或核心最高温度，并能说明实际数据源。
- 指标值变化不改变悬浮条边框宽度或中心位置。
- 支持的前台游戏启动后自动显示 `FPS <数值>`，退出后恢复 `FPS --`。
- FPS 未启用或没有游戏时，PresentMon 不运行。
- TopMonitor 日常运行不需要管理员权限。
- 普通空闲状态连续观察 60 秒，平均 CPU 尽量低于 1%，无持续内存增长和异常 GC 压力。
- 全量测试、Release 编译、win-x64 自包含发布和本机手动验收通过。

## 实施顺序

1. CPU 温度候选选择、诊断日志和 PawnIO 状态/初始化。
2. 固定指标槽宽与窗口稳定性。
3. FPS Domain 模型、前台进程服务、PresentMon 运行器和 FPS Provider。
4. 设置页面状态与一次性权限配置。
5. Claude 审查中经验证可采纳的性能优化。
6. 性能基线、全量测试、发布和本机验收。
