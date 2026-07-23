using System.ComponentModel;
using System.Runtime.InteropServices;

namespace TopMonitor.Infrastructure.Windows;

/// <summary>
/// 对悬浮窗所需 Win32 扩展样式的安全封装。
/// </summary>
public sealed class WindowInteractionService
{
    private const int ExtendedStyleIndex = -20;
    private const long ToolWindowStyle = 0x00000080L;
    private const long TransparentStyle = 0x00000020L;
    private const long NoActivateStyle = 0x08000000L;

    public void ApplyOverlayStyles(nint windowHandle, bool clickThrough)
    {
        if (!OperatingSystem.IsWindows() || windowHandle == nint.Zero)
        {
            return;
        }

        Marshal.SetLastPInvokeError(0);
        var current = NativeMethods.GetWindowLongPtr(windowHandle, ExtendedStyleIndex);
        var readError = Marshal.GetLastPInvokeError();
        if (current == nint.Zero && readError != 0)
        {
            throw new Win32Exception(readError);
        }

        var styles = current.ToInt64() | ToolWindowStyle | NoActivateStyle;
        styles = clickThrough
            ? styles | TransparentStyle
            : styles & ~TransparentStyle;

        Marshal.SetLastPInvokeError(0);
        var previous = NativeMethods.SetWindowLongPtr(
            windowHandle,
            ExtendedStyleIndex,
            new nint(styles));
        var writeError = Marshal.GetLastPInvokeError();
        if (previous == nint.Zero && writeError != 0)
        {
            throw new Win32Exception(writeError);
        }
    }
}
