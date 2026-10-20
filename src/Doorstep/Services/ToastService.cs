using System.Windows.Threading;
using Microsoft.Toolkit.Uwp.Notifications;

namespace Doorstep;

internal static class ToastService
{
    public static void Show(string title, string? body, string? tag, string? url, string? releasePage = null, bool offerInstall = false)
    {
        var thread = new Thread(() =>
        {
            var frame = new DispatcherFrame();
            Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.Normal, () =>
            {
                try
                {
                    ShowOnSta(title, body, tag, url, releasePage, offerInstall);
                }
                finally
                {
                    frame.Continue = false;
                }
            });
            Dispatcher.PushFrame(frame);
            Dispatcher.CurrentDispatcher.InvokeShutdown();
        })
        {
            IsBackground = true,
            Name = "Doorstep.Toast"
        };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
    }

    private static void ShowOnSta(string title, string? body, string? tag, string? url, string? releasePage, bool offerInstall)
    {
        try
        {
            var builder = new ToastContentBuilder().AddText(title);
            if (!string.IsNullOrWhiteSpace(body))
                builder.AddText(body);
            if (!string.IsNullOrWhiteSpace(tag))
                builder.AddArgument("tag", tag);
            if (!string.IsNullOrWhiteSpace(url) && RingNavigation.IsAllowed(url))
                builder.AddArgument("url", url);
            if (offerInstall)
            {
                builder.AddButton(new ToastButton()
                    .SetContent("Install update")
                    .AddArgument("install", "1"));
            }
            if (!string.IsNullOrWhiteSpace(releasePage) && UpdateChecker.IsReleasePage(releasePage))
            {
                builder.AddButton(new ToastButton()
                    .SetContent("View release")
                    .AddArgument("update", releasePage));
            }

            builder.Show(toast =>
            {
                toast.Group = "Doorstep";
                if (!string.IsNullOrWhiteSpace(tag))
                    toast.Tag = tag.Length <= 64 ? tag : tag[..64];
            });
            StatusLog.Write("toast shown: " + title);
        }
        catch (Exception ex)
        {
            StatusLog.Write("toast failed: " + ex.Message);
        }
    }

    public static void ShowTest()
    {
        Show(
            "Doorstep",
            "Notifications are coming from Doorstep, an unofficial app. Alerts can be missed.",
            "test",
            null);
    }
}
