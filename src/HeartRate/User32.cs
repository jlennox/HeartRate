using System;
using System.Runtime.InteropServices;

namespace HeartRate;

internal static class User32
{
    [DllImport("user32.dll")]
    public static extern int GetSystemMetrics(SystemMetric nIndex);

    [DllImport("user32.dll")]
    public static extern int SetForegroundWindow(int hWnd);

    [DllImport("user32.dll")]
    public static extern bool DestroyIcon(IntPtr handle);

    public enum SystemMetric
    {
        SmallIconX = 49, // SM_CXSMICON
        SmallIconY = 50, // SM_CYSMICON
    }
}