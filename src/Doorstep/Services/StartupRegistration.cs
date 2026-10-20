using Microsoft.Win32;

namespace Doorstep;

internal static class StartupRegistration
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "Doorstep";

    public static bool IsEnabled
    {
        get
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey, writable: false);
            return key?.GetValue(ValueName) is string value
                && value.Contains("Doorstep", StringComparison.OrdinalIgnoreCase);
        }
    }

    public static void SetEnabled(bool enabled)
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKey, writable: true)
            ?? Registry.CurrentUser.CreateSubKey(RunKey, writable: true);
        if (enabled)
        {
            var exe = AppIdentity.ExecutablePath;
            key.SetValue(ValueName, $"\"{exe}\" --background");
            return;
        }

        key.DeleteValue(ValueName, throwOnMissingValue: false);
    }
}
