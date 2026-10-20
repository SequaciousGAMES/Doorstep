using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using Microsoft.Win32;

namespace Doorstep;

internal enum AppearanceMode
{
    System,
    Light,
    Dark
}

internal static class AppearanceSettings
{
    private static readonly string FilePath = Path.Combine(StatusLog.Folder, "appearance.txt");
    private static readonly HashSet<Window> TitleBarWaiting = new();
    private static bool _loaded;
    private static bool _systemDark;

    public static event Action? Changed;

    public static AppearanceMode Mode { get; private set; } = AppearanceMode.System;

    public static bool IsDark => Mode switch
    {
        AppearanceMode.Light => false,
        AppearanceMode.Dark => true,
        _ => _systemDark
    };

    public static System.Drawing.Color PageColor => IsDark
        ? System.Drawing.Color.FromArgb(255, 16, 20, 24)
        : System.Drawing.Color.FromArgb(255, 244, 247, 248);

    public static void Load()
    {
        if (_loaded)
            return;
        _loaded = true;
        _systemDark = ReadSystemDark();
        Mode = ReadMode();
        SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged;
        StatusLog.Write("appearance " + Mode.ToString().ToLowerInvariant() + (IsDark ? " dark" : " light"));
    }

    public static void Set(AppearanceMode mode)
    {
        Load();
        if (mode == Mode)
            return;

        Mode = mode;
        try
        {
            Directory.CreateDirectory(StatusLog.Folder);
            File.WriteAllText(FilePath, mode.ToString().ToLowerInvariant());
        }
        catch (Exception ex)
        {
            StatusLog.Write("appearance save failed: " + ex.Message);
        }

        StatusLog.Write("appearance " + Mode.ToString().ToLowerInvariant() + (IsDark ? " dark" : " light"));
        Changed?.Invoke();
    }

    public static void Apply(Window window, Border? panel, TextBlock? label)
    {
        var background = IsDark ? Color.FromRgb(16, 20, 24) : Color.FromRgb(244, 247, 248);
        var foreground = IsDark ? Color.FromRgb(215, 226, 232) : Color.FromRgb(26, 36, 40);
        window.Background = new SolidColorBrush(background);
        if (panel != null)
            panel.Background = new SolidColorBrush(background);
        if (label != null)
            label.Foreground = new SolidColorBrush(foreground);
        ApplyTitleBar(window, background, foreground);
    }

    private static void ApplyTitleBar(Window window, Color background, Color foreground)
    {
        var hwnd = new WindowInteropHelper(window).Handle;
        if (hwnd == IntPtr.Zero)
        {
            if (TitleBarWaiting.Add(window))
                window.SourceInitialized += (_, _) => Apply(window, panel: null, label: null);
            return;
        }

        TitleBarWaiting.Remove(window);
        var dark = IsDark ? 1 : 0;
        DwmSetWindowAttribute(hwnd, UseImmersiveDarkMode, ref dark, sizeof(int));
        DwmSetWindowAttribute(hwnd, UseImmersiveDarkModeOld, ref dark, sizeof(int));

        var caption = ColorRef(background);
        var text = ColorRef(foreground);
        DwmSetWindowAttribute(hwnd, CaptionColor, ref caption, sizeof(int));
        DwmSetWindowAttribute(hwnd, TextColor, ref text, sizeof(int));
        DwmSetWindowAttribute(hwnd, BorderColor, ref caption, sizeof(int));
        SetWindowPos(hwnd, IntPtr.Zero, 0, 0, 0, 0, FrameChanged);
    }

    private static int ColorRef(Color color) => color.R | (color.G << 8) | (color.B << 16);

    private const int UseImmersiveDarkMode = 20;
    private const int UseImmersiveDarkModeOld = 19;
    private const int BorderColor = 34;
    private const int CaptionColor = 35;
    private const int TextColor = 36;
    private const uint FrameChanged = 0x0020 | 0x0001 | 0x0002 | 0x0004 | 0x0010;

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

    [DllImport("user32.dll")]
    private static extern bool SetWindowPos(IntPtr hwnd, IntPtr insertAfter, int x, int y, int cx, int cy, uint flags);

    private static void OnUserPreferenceChanged(object sender, UserPreferenceChangedEventArgs e)
    {
        if (Mode != AppearanceMode.System)
            return;
        if (e.Category is not UserPreferenceCategory.General
            and not UserPreferenceCategory.Color
            and not UserPreferenceCategory.VisualStyle)
            return;

        var dark = ReadSystemDark();
        if (dark == _systemDark)
            return;

        _systemDark = dark;
        var handler = Changed;
        if (handler == null)
            return;

        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher == null || dispatcher.CheckAccess())
            handler();
        else
            dispatcher.BeginInvoke(handler);
    }

    private static AppearanceMode ReadMode()
    {
        try
        {
            var text = File.Exists(FilePath) ? File.ReadAllText(FilePath).Trim() : "";
            if (text.Equals("light", StringComparison.OrdinalIgnoreCase))
                return AppearanceMode.Light;
            if (text.Equals("dark", StringComparison.OrdinalIgnoreCase))
                return AppearanceMode.Dark;
        }
        catch (Exception ex)
        {
            StatusLog.Write("appearance read failed: " + ex.Message);
        }

        return AppearanceMode.System;
    }

    private static bool ReadSystemDark()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            return key?.GetValue("AppsUseLightTheme") is int light && light == 0;
        }
        catch (Exception ex)
        {
            StatusLog.Write("system theme read failed: " + ex.Message);
            return true;
        }
    }
}
