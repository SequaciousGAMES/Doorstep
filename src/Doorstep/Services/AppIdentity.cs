using System.Runtime.InteropServices;
using Microsoft.Toolkit.Uwp.Notifications;

namespace Doorstep;

internal static class AppIdentity
{
    public const string AppUserModelId = "Local.Doorstep";

    public static string ExecutablePath =>
        Environment.ProcessPath
        ?? System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName
        ?? "";

    public static void SetProcessId()
    {
        SetCurrentProcessExplicitAppUserModelID(AppUserModelId);
    }

    public static void Register()
    {
        try
        {
            ToastNotificationManagerCompat.OnActivated += args =>
                ActivationRouter.Enqueue(args.Argument);
        }
        catch (Exception ex)
        {
            StatusLog.Write("toast registration failed: " + ex.Message);
        }

        try
        {
            ShortcutInstaller.Ensure(ExecutablePath);
        }
        catch (Exception ex)
        {
            StatusLog.Write("shortcut failed: " + ex.Message);
        }
    }

    [DllImport("shell32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern int SetCurrentProcessExplicitAppUserModelID(string appId);
}
