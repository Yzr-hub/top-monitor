# TopMonitor 传感器兼容性

## 验证状态

当前构建、自动化测试和发布在 Windows 11 build 26200 x64 上完成。已观察
Intel Core i7-14700KF、NVIDIA GPU、内存和网络指标；PawnIO 初始化后的 CPU
温度与真实游戏 FPS 仍需按本文验收步骤做最终人工确认。

## 已测试硬件

| 类别 | 已测试型号 | 结果 |
| --- | --- | --- |
| CPU | Intel Core i7-14700KF | 使用率可用；未安装 PawnIO 时温度不可用，等待初始化后复测 |
| GPU | NVIDIA（具体型号待记录） | 核心温度和负载可用 |
| 主板/EC | 无 | 待 Windows 实机验证 |

## 当前实现的指标

| 指标 | 数据来源 | 当前状态 |
| --- | --- | --- |
| CPU 使用率 | Windows `GetSystemTimes` | i7-14700KF 可用 |
| 内存使用率/已用内存 | Windows `GlobalMemoryStatusEx` | 可用 |
| 上行/下行网络速率 | .NET 网络接口累计字节差值 | 可用 |
| CPU 温度 | LibreHardwareMonitor + PawnIO | 已实现，等待一次性 PawnIO 初始化后验收 |
| GPU 温度/负载 | LibreHardwareMonitor | NVIDIA 实机可用 |
| 前台游戏 FPS | PresentMon 2.4.1 / ETW | 已实现，等待真实游戏验收 |

当前未实现或未验证的传感器包括风扇转速、电压、功耗、硬盘温度、
主板温度、内存 SPD 和帧时间。传感器缺失时显示不可用，不使用虚构或
固定值。

## 管理员权限

TopMonitor 默认且日常都以普通用户运行。若 Intel 14 代 CPU 温度显示
`--`，打开设置 → 行为 → “初始化 CPU 温度访问”，确认一次 UAC，安装
发布包 `Dependencies\PawnIO_setup.exe` 中的签名 PawnIO 组件。成功后应用
会重新扫描传感器。

CPU 温度候选按以下顺序选择，并拒绝非有限值或超出 -20°C～125°C 的值：

1. `CPU Package` / `Package`
2. `Core Max`
3. 包含 `Package`、`Tdie` 或 `Tctl/Tdie` 的等价项
4. 有效 CPU Core 中的最高温度

日志包含 `CPU temperature discovery`、候选列表、最终传感器和选择原因。
日志目录为 `%LocalAppData%\TopMonitor\logs\`。

## 驱动与反作弊风险

- 内核驱动、硬件监控驱动和厂商工具可能争用相同设备。
- 企业安全策略、核心隔离、驱动签名策略可能阻止低层访问。
- 部分游戏反作弊系统可能限制 ETW、句柄访问或硬件驱动；当前 FPS
  实现不会绕过限制，也未针对任何反作弊产品认证。
- 不应为了读取传感器绕过反作弊、禁用安全软件或加载来源不明的驱动。

出现冲突时先关闭 TopMonitor，保留日志，并记录 Windows 版本、CPU、GPU、主板、BIOS、显卡驱动和安全软件版本。

## Windows 验收矩阵

正式发布前至少覆盖：

- Intel Core 近三代桌面与移动平台。
- AMD Ryzen 近三代桌面与移动平台。
- NVIDIA、AMD、Intel 独立或集成 GPU。
- 单 GPU、多 GPU、无独立 GPU。
- 普通用户与管理员权限对比。
- 休眠恢复、锁屏、远程桌面和显示器热插拔。
- Windows 11 当前受支持版本，开启核心隔离的环境。

每次实测后在本文件记录具体型号、Windows/驱动版本、可读与不可读传感器、权限要求和异常日志摘要。
