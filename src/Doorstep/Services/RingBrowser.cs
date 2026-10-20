using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;

namespace Doorstep;

internal sealed class RingBrowser : IDisposable
{
    private readonly WebView2 _webView;
    private readonly Dictionary<string, CoreWebView2Notification> _notifications = new();
    private bool _initialized;
    private bool _loggedAgent;
    private string? _pendingUrl;
    private const string LightPageMedia =
        "{\"features\":[{\"name\":\"prefers-color-scheme\",\"value\":\"light\"}]}";

    private int _themeGeneration;
    private int _filterToken;
    private string? _filterScriptId;

    public RingBrowser(WebView2 webView)
    {
        _webView = webView;
    }

    public event EventHandler? NavigationSucceeded;
    public event EventHandler<string>? NavigationFailed;

    public CoreWebView2? Core => _webView.CoreWebView2;

    public string Source => _webView.CoreWebView2?.Source ?? "";

    public async Task InitializeAsync(string? startUrl, CoreWebView2Environment? environment = null)
    {
        if (_initialized)
            return;
        _initialized = true;

        environment ??= await WebViewEnvironment.GetAsync();
        StatusLog.Write("creating browser view");
        await _webView.EnsureCoreWebView2Async(environment);
        StatusLog.Write("browser view created");
        var core = _webView.CoreWebView2;
        core.Settings.AreDevToolsEnabled = false;
        core.Settings.IsStatusBarEnabled = false;
        core.Settings.AreDefaultContextMenusEnabled = true;
        core.Settings.IsWebMessageEnabled = false;
        core.MemoryUsageTargetLevel = CoreWebView2MemoryUsageTargetLevel.Normal;

        await GrantNotificationPermissionAsync(core.Profile);
        await ApplyAppearanceAsync(reload: false);

        core.NavigationStarting += OnNavigationStarting;
        core.NewWindowRequested += OnNewWindowRequested;
        core.PermissionRequested += OnPermissionRequested;
        core.NotificationReceived += OnNotificationReceived;
        core.NavigationCompleted += OnNavigationCompleted;
        core.ProcessFailed += (_, args) =>
            StatusLog.Write("webview process failed: " + args.ProcessFailedKind);

        var destination = _pendingUrl ?? startUrl;
        _pendingUrl = null;
        if (!string.IsNullOrWhiteSpace(destination))
            core.Navigate(destination);
    }

    public void Navigate(string url)
    {
        if (!RingNavigation.IsAllowed(url))
            return;
        if (_webView.CoreWebView2 == null)
        {
            _pendingUrl = url;
            return;
        }

        _webView.CoreWebView2.Navigate(url);
    }

    public void Reload() => _webView.CoreWebView2?.Reload();

    public void ApplyAppearance(bool reload) => _ = ApplyAppearanceAsync(reload);

    private async Task ApplyAppearanceAsync(bool reload)
    {
        var core = _webView.CoreWebView2;
        if (core == null)
            return;

        var generation = ++_themeGeneration;
        var dark = AppearanceSettings.IsDark;

        try
        {
            // Menus follow the chosen appearance. The page itself stays on the
            // light scheme, because the filter darkens that rendering and a dark
            // page would be inverted back toward light.
            core.Profile.PreferredColorScheme = dark
                ? CoreWebView2PreferredColorScheme.Dark
                : CoreWebView2PreferredColorScheme.Light;
            _webView.DefaultBackgroundColor = AppearanceSettings.PageColor;
            await core.CallDevToolsProtocolMethodAsync("Emulation.setEmulatedMedia", LightPageMedia);
            if (generation != _themeGeneration)
                return;

            if (dark)
                await EnsureFilterAsync(core, generation);
            else
                RemoveFilter(core);

            if (generation != _themeGeneration)
                return;

            StatusLog.Write("appearance applied " + AppearanceSettings.Mode.ToString().ToLowerInvariant()
                + (dark ? " filter" : " light"));
            if (reload)
                core.Reload();
        }
        catch (Exception ex)
        {
            StatusLog.Write("appearance apply failed: " + ex.Message);
        }
    }

    private async Task HoldFilteredPageAsync(CoreWebView2 core)
    {
        try
        {
            await core.CallDevToolsProtocolMethodAsync("Emulation.setEmulatedMedia", LightPageMedia);
            await core.ExecuteScriptAsync(AppearanceFilter.Script);
        }
        catch (Exception ex)
        {
            StatusLog.Write("appearance hold failed: " + ex.Message);
        }
    }

    private async Task EnsureFilterAsync(CoreWebView2 core, int generation)
    {
        if (_filterScriptId != null)
            return;

        var token = ++_filterToken;
        var id = await core.AddScriptToExecuteOnDocumentCreatedAsync(AppearanceFilter.Script);
        if (token != _filterToken || generation != _themeGeneration)
        {
            core.RemoveScriptToExecuteOnDocumentCreated(id);
            return;
        }

        _filterScriptId = id;
    }

    private void RemoveFilter(CoreWebView2 core)
    {
        _filterToken++;
        if (_filterScriptId == null)
            return;

        core.RemoveScriptToExecuteOnDocumentCreated(_filterScriptId);
        _filterScriptId = null;
    }

    public void ResumeIfSuspended()
    {
        var core = _webView.CoreWebView2;
        if (core == null)
            return;

        try
        {
            core.MemoryUsageTargetLevel = CoreWebView2MemoryUsageTargetLevel.Normal;
            if (core.IsSuspended)
                core.Resume();
        }
        catch (Exception ex)
        {
            StatusLog.Write("resume failed: " + ex.Message);
        }
    }

    public void ReportClicked(string? tag)
    {
        if (string.IsNullOrWhiteSpace(tag))
            return;

        var key = tag.StartsWith("web:", StringComparison.Ordinal) ? tag[4..] : tag;
        if (!_notifications.TryGetValue(key, out var notification)
            && !_notifications.TryGetValue("last", out notification))
            return;

        try
        {
            notification.ReportClicked();
        }
        catch (Exception ex)
        {
            StatusLog.Write("notification click report failed: " + ex.Message);
        }
    }

    public void Dispose() => _webView.Dispose();

    private static async Task GrantNotificationPermissionAsync(CoreWebView2Profile profile)
    {
        string[] origins = ["https://account.ring.com", "https://ring.com", "https://www.ring.com"];
        foreach (var origin in origins)
        {
            try
            {
                await profile.SetPermissionStateAsync(
                    CoreWebView2PermissionKind.Notifications,
                    origin,
                    CoreWebView2PermissionState.Allow);
            }
            catch (Exception ex)
            {
                StatusLog.Write("notification permission failed for " + origin + ": " + ex.Message);
            }
        }
    }

    private void OnNavigationStarting(object? sender, CoreWebView2NavigationStartingEventArgs e)
    {
        if (RingNavigation.IsAllowed(e.Uri))
            return;

        e.Cancel = true;
        if (RingNavigation.IsHttp(e.Uri))
            RingNavigation.OpenExternal(e.Uri);
        StatusLog.Write("blocked navigation host=" + RingNavigation.HostOf(e.Uri));
    }

    private async void OnNewWindowRequested(object? sender, CoreWebView2NewWindowRequestedEventArgs e)
    {
        var deferral = e.GetDeferral();
        try
        {
            if (!string.IsNullOrWhiteSpace(e.Uri) && !RingNavigation.IsAllowed(e.Uri))
            {
                e.Handled = true;
                if (RingNavigation.IsHttp(e.Uri))
                    RingNavigation.OpenExternal(e.Uri);
                return;
            }

            var popup = new PopupWindow();
            await popup.InitializeAsync();
            e.NewWindow = popup.Core;
            e.Handled = true;
        }
        catch (Exception ex)
        {
            e.Handled = true;
            StatusLog.Write("popup failed: " + ex.Message);
        }
        finally
        {
            deferral.Complete();
        }
    }

    private static void OnPermissionRequested(object? sender, CoreWebView2PermissionRequestedEventArgs e)
    {
        if (e.PermissionKind == CoreWebView2PermissionKind.Notifications
            && RingNavigation.IsRingHost(RingNavigation.HostOf(e.Uri)))
        {
            e.State = CoreWebView2PermissionState.Allow;
        }
    }

    private void OnNotificationReceived(object? sender, CoreWebView2NotificationReceivedEventArgs e)
    {
        if (!RingNavigation.IsAllowed(e.SenderOrigin))
            return;

        e.Handled = true;
        var notification = e.Notification;
        var key = string.IsNullOrWhiteSpace(notification.Tag) ? "last" : notification.Tag;
        _notifications[key] = notification;
        notification.CloseRequested += (_, _) => _notifications.Remove(key);
        AlertDispatcher.Raise(notification.Title, notification.Body, "web:" + key, null);
        try
        {
            notification.ReportShown();
        }
        catch (Exception ex)
        {
            StatusLog.Write("notification shown report failed: " + ex.Message);
        }
    }

    private async void OnNavigationCompleted(object? sender, CoreWebView2NavigationCompletedEventArgs e)
    {
        var core = _webView.CoreWebView2;
        if (!e.IsSuccess)
        {
            StatusLog.Write("navigation failed " + e.WebErrorStatus + " " + RingNavigation.Safe(core?.Source));
            if (e.WebErrorStatus is not CoreWebView2WebErrorStatus.ConnectionAborted
                and not CoreWebView2WebErrorStatus.OperationCanceled)
            {
                NavigationFailed?.Invoke(this, e.WebErrorStatus.ToString());
            }
            return;
        }

        StatusLog.Write("navigation ok " + RingNavigation.Safe(core?.Source) + " title=" + Trim(core?.DocumentTitle, 80));
        if (core != null && _filterScriptId != null)
            await HoldFilteredPageAsync(core);
        NavigationSucceeded?.Invoke(this, EventArgs.Empty);
        if (_loggedAgent || core == null)
            return;

        _loggedAgent = true;
        try
        {
            var agent = await core.ExecuteScriptAsync("navigator.userAgent");
            StatusLog.Write("user agent " + Trim(agent?.Trim('"'), 180));
        }
        catch (Exception ex)
        {
            StatusLog.Write("user agent failed: " + ex.Message);
        }
    }

    private static string Trim(string? value, int max)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "";
        var text = value.Replace('\r', ' ').Replace('\n', ' ').Trim();
        return text.Length <= max ? text : text[..max];
    }
}
