using System.Runtime.InteropServices;

namespace TopMonitor.Infrastructure.Windows;

/// <summary>
/// TopMonitor 使用的 Win32 API 集中入口，避免 P/Invoke 散落到 Provider 或 UI。
/// </summary>
internal static class NativeMethods
{
    /// <summary>
    /// 获取系统累计的空闲、内核和用户时间，用于计算两次采样之间的 CPU 利用率。
    /// WPF 不提供系统级 CPU 累计时间；调用失败时 Provider 返回不可用，不会终止程序。
    /// </summary>
    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool GetSystemTimes(
        out FileTime idleTime,
        out FileTime kernelTime,
        out FileTime userTime);

    /// <summary>
    /// 获取物理内存总量和可用量。
    /// WPF 只负责界面，不提供可靠的系统物理内存统计；调用失败时指标降级为不可用。
    /// </summary>
    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool GlobalMemoryStatusEx(ref MemoryStatusEx buffer);

    /// <summary>
    /// 读取和修改窗口扩展样式，用于鼠标穿透、工具窗口和不抢焦点。
    /// WPF 没有公开 WS_EX_TRANSPARENT/WS_EX_NOACTIVATE 的完整控制；调用失败时保留普通可交互窗口。
    /// </summary>
    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW", SetLastError = true)]
    internal static extern nint GetWindowLongPtr(nint windowHandle, int index);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW", SetLastError = true)]
    internal static extern nint SetWindowLongPtr(
        nint windowHandle,
        int index,
        nint newLong);

    internal delegate bool MonitorEnumProcedure(
        nint monitor,
        nint deviceContext,
        nint monitorRectangle,
        nint data);

    /// <summary>
    /// 枚举显示器并读取各自工作区，供悬浮窗选择目标屏幕。
    /// WPF 的 SystemParameters 只直接暴露主屏工作区；调用失败时退回主屏定位。
    /// </summary>
    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool EnumDisplayMonitors(
        nint deviceContext,
        nint clipRectangle,
        MonitorEnumProcedure callback,
        nint data);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool GetMonitorInfo(
        nint monitor,
        ref MonitorInfo monitorInfo);

    [StructLayout(LayoutKind.Sequential)]
    internal struct FileTime
    {
        public uint LowDateTime;
        public uint HighDateTime;

        public readonly ulong ToUInt64() => ((ulong)HighDateTime << 32) | LowDateTime;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    internal struct MemoryStatusEx
    {
        public uint Length;
        public uint MemoryLoad;
        public ulong TotalPhysical;
        public ulong AvailablePhysical;
        public ulong TotalPageFile;
        public ulong AvailablePageFile;
        public ulong TotalVirtual;
        public ulong AvailableVirtual;
        public ulong AvailableExtendedVirtual;

        public static MemoryStatusEx Create() =>
            new()
            {
                Length = (uint)Marshal.SizeOf<MemoryStatusEx>()
            };
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    internal struct MonitorInfo
    {
        public uint Size;
        public Rectangle Monitor;
        public Rectangle WorkArea;
        public uint Flags;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string DeviceName;

        public static MonitorInfo Create() =>
            new()
            {
                Size = (uint)Marshal.SizeOf<MonitorInfo>(),
                DeviceName = string.Empty
            };
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct Rectangle
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }
}
