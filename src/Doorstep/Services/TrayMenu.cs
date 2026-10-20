using System.Runtime.InteropServices;

namespace Doorstep;

internal enum TrayCommand
{
    None = 0,
    Show = 1,
    Reload = 2,
    Test = 3,
    Startup = 4,
    Check = 5,
    CheckAtStartup = 6,
    Install = 7,
    AppearanceSystem = 10,
    AppearanceLight = 11,
    AppearanceDark = 12,
    Quit = 99
}

internal static class TrayMenu
{
    public static TrayCommand Show(IntPtr owner, AppearanceMode appearance, bool startWithWindows, bool checkAtStartup, bool updateAvailable)
    {
        if (owner == IntPtr.Zero)
        {
            StatusLog.Write("tray menu skipped");
            return TrayCommand.None;
        }

        StatusLog.Write("tray menu");

        var menu = CreatePopupMenu();
        var themes = CreatePopupMenu();
        try
        {
            Append(themes, appearance == AppearanceMode.System, (uint)TrayCommand.AppearanceSystem, "System");
            Append(themes, appearance == AppearanceMode.Light, (uint)TrayCommand.AppearanceLight, "Light");
            Append(themes, appearance == AppearanceMode.Dark, (uint)TrayCommand.AppearanceDark, "Dark");

            Append(menu, false, (uint)TrayCommand.Show, "Show");
            Append(menu, false, (uint)TrayCommand.Reload, "Reload");
            Append(menu, false, (uint)TrayCommand.Test, "Send test notification");
            Append(menu, startWithWindows, (uint)TrayCommand.Startup, "Start with Windows");
            Append(menu, false, (uint)TrayCommand.Check, "Check for updates");
            if (updateAvailable)
                Append(menu, false, (uint)TrayCommand.Install, "Install update");
            Append(menu, checkAtStartup, (uint)TrayCommand.CheckAtStartup, "Check for updates at startup");
            AppendMenu(menu, Popup, (UIntPtr)(ulong)themes.ToInt64(), "Appearance");
            AppendMenu(menu, Separator, UIntPtr.Zero, null);
            Append(menu, false, (uint)TrayCommand.Quit, "Quit");

            if (!GetCursorPos(out var point))
                return TrayCommand.None;

            MakeForeground(owner);
            var selected = TrackPopupMenuEx(
                menu,
                ReturnCommand | RightButton,
                point.X,
                point.Y,
                owner,
                IntPtr.Zero);
            PostMessage(owner, 0, IntPtr.Zero, IntPtr.Zero);
            return Enum.IsDefined(typeof(TrayCommand), selected)
                ? (TrayCommand)selected
                : TrayCommand.None;
        }
        finally
        {
            DestroyMenu(menu);
        }
    }

    private static void Append(IntPtr menu, bool check, uint id, string text)
    {
        AppendMenu(menu, StringItem | (check ? Checked : 0), (UIntPtr)id, text);
    }

    private static void MakeForeground(IntPtr owner)
    {
        var foreground = GetForegroundWindow();
        var foregroundThread = GetWindowThreadProcessId(foreground, out _);
        var currentThread = GetCurrentThreadId();
        var attached = foregroundThread != 0 && foregroundThread != currentThread
            && AttachThreadInput(foregroundThread, currentThread, true);
        SetForegroundWindow(owner);
        if (attached)
            AttachThreadInput(foregroundThread, currentThread, false);
    }

    private const uint StringItem = 0x0000;
    private const uint Checked = 0x0008;
    private const uint Popup = 0x0010;
    private const uint Separator = 0x0800;
    private const uint RightButton = 0x0002;
    private const uint ReturnCommand = 0x0100;

    [StructLayout(LayoutKind.Sequential)]
    private struct Point
    {
        public int X;
        public int Y;
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr CreatePopupMenu();

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern bool AppendMenu(IntPtr menu, uint flags, UIntPtr id, string? text);

    [DllImport("user32.dll")]
    private static extern bool DestroyMenu(IntPtr menu);

    [DllImport("user32.dll")]
    private static extern bool GetCursorPos(out Point point);

    [DllImport("user32.dll")]
    private static extern int TrackPopupMenuEx(IntPtr menu, uint flags, int x, int y, IntPtr owner, IntPtr parameters);

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr window, out uint processId);

    [DllImport("kernel32.dll")]
    private static extern uint GetCurrentThreadId();

    [DllImport("user32.dll")]
    private static extern bool AttachThreadInput(uint sourceThread, uint targetThread, bool attach);

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr window);

    [DllImport("user32.dll")]
    private static extern bool PostMessage(IntPtr window, uint message, IntPtr wParam, IntPtr lParam);
}
