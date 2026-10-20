using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;
using H.NotifyIcon;

namespace Doorstep;

public partial class MainWindow : Window
{
    private readonly RingBrowser _browser;
    private TaskbarIcon? _tray;
    private readonly DispatcherTimer _resumeTimer;
    private bool _parked;
    private bool _restoring;
    private bool _allowExit;
    private bool _handlingState;
    private Rect _restoreBounds;

    public MainWindow()
    {
        StatusLog.Write("window init");
        InitializeComponent();
        StatusLog.Write("window xaml");
        Root.Children.Remove(WebView);
        AppearanceSettings.Apply(this, LoadingPanel, LoadingText);
        AppearanceSettings.Changed += OnAppearanceChanged;
        UpdateChecker.ExitRequested += Quit;
        _browser = new RingBrowser(WebView);
        _browser.NavigationSucceeded += (_, _) => LoadingPanel.Visibility = Visibility.Collapsed;
        _browser.NavigationFailed += (_, status) =>
        {
            if (LoadingPanel.Visibility == Visibility.Visible)
                LoadingText.Text = "Couldn't reach Ring (" + status + "). Use Reload in the tray menu.";
        };
        _resumeTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(15) };
        _resumeTimer.Tick += (_, _) =>
        {
            if (_parked)
                _browser.ResumeIfSuspended();
        };
        _resumeTimer.Start();
    }

    public void StartShell()
    {
        BeginBrowser();
        if (UpdateSettings.CheckAtStartup)
            UpdateChecker.Check(announce: false);
    }

    public void BeginBrowser()
    {
        Dispatcher.BeginInvoke(DispatcherPriority.Normal, new Action(() =>
        {
            _ = StartBrowserAsync();
        }));
    }

    private async Task StartBrowserAsync()
    {
        try
        {
            StatusLog.Write("browser starting");
            var environment = await WebViewEnvironment.GetAsync();
            StatusLog.Write("environment ready");
            Root.Children.Insert(0, WebView);
            StatusLog.Write("webview inserted");
            WebView.Visibility = Visibility.Visible;
            StatusLog.Write("webview visible");
            await _browser.InitializeAsync(RingNavigation.DashboardUrl, environment);
            StatusLog.Write("browser ready");
        }
        catch (Exception ex)
        {
            StatusLog.Write("webview init failed: " + ex);
            LoadingText.Text = "Doorstep couldn't start the browser view.";
        }
        finally
        {
            EnsureTray();
        }
    }

    private void EnsureTray()
    {
        if (_tray != null)
            return;

        StatusLog.Write("creating tray");
        _tray = CreateTray();
        StatusLog.Write("tray ready");
    }

    public void StartInTray()
    {
        _parked = true;
        ShowActivated = false;
        WindowStartupLocation = WindowStartupLocation.Manual;
        Left = -32000;
        Top = -32000;
    }

    public void BringToFront()
    {
        _restoring = true;
        try
        {
            _parked = false;
            ShowActivated = true;
            RestoreSavedBounds();
            if (!IsVisible)
                Show();
            WindowState = WindowState.Normal;
            ApplyToolWindow(false);
            Activate();
            Topmost = true;
            Topmost = false;
            StatusLog.Write("window restored");
        }
        finally
        {
            _restoring = false;
        }
    }

    public void OpenFromToast(string? arguments)
    {
        if (ActivationRouter.GetArgument(arguments, "install") == "1")
        {
            UpdateChecker.Install();
            return;
        }

        var update = ActivationRouter.GetArgument(arguments, "update");
        if (!string.IsNullOrWhiteSpace(update))
        {
            if (UpdateChecker.IsReleasePage(update))
                RingNavigation.OpenExternal(update);
            return;
        }

        BringToFront();
        var tag = ActivationRouter.GetArgument(arguments, "tag");
        _browser.ReportClicked(tag);
        var url = ActivationRouter.GetArgument(arguments, "url");
        if (RingNavigation.IsAllowed(url))
        {
            _browser.Navigate(url!);
            return;
        }

        if (!SourceIsAccount())
            _browser.Navigate(RingNavigation.DashboardUrl);
    }

    public void Quit()
    {
        _allowExit = true;
        foreach (var popup in Application.Current.Windows.OfType<PopupWindow>().ToList())
            popup.Close();

        Close();
        Application.Current.Shutdown();
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        StatusLog.Write("source initialized");
        base.OnSourceInitialized(e);
        if (_parked)
            ApplyToolWindow(true);
    }

    protected override void OnStateChanged(EventArgs e)
    {
        base.OnStateChanged(e);
        if (_handlingState || _allowExit || _restoring || WindowState != WindowState.Minimized)
            return;

        _handlingState = true;
        WindowState = WindowState.Normal;
        Park();
        _handlingState = false;
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        if (!_allowExit && !UpdateChecker.IsInstalling)
        {
            e.Cancel = true;
            if (!_restoring)
                Park();
            return;
        }

        _allowExit = true;

        _resumeTimer.Stop();
        _tray?.Dispose();
        _browser.Dispose();
        base.OnClosing(e);
    }

    private void Park()
    {
        if (_allowExit || _parked)
            return;

        RememberBounds();
        _parked = true;
        if (WindowState != WindowState.Normal)
            WindowState = WindowState.Normal;
        ApplyToolWindow(true);
        Left = -32000;
        Top = -32000;
        _browser.ResumeIfSuspended();
        StatusLog.Write("window parked");
    }

    private void RememberBounds()
    {
        var bounds = WindowState == WindowState.Normal
            ? new Rect(Left, Top, Width, Height)
            : RestoreBounds;
        if (bounds.Width >= MinWidth && bounds.Height >= MinHeight && bounds.Left > -10000)
            _restoreBounds = bounds;
    }

    private void RestoreSavedBounds()
    {
        if (_restoreBounds.Width >= MinWidth && _restoreBounds.Height >= MinHeight)
        {
            Left = _restoreBounds.Left;
            Top = _restoreBounds.Top;
            Width = _restoreBounds.Width;
            Height = _restoreBounds.Height;
            return;
        }

        Width = 1200;
        Height = 800;
        Left = SystemParameters.WorkArea.Left + ((SystemParameters.WorkArea.Width - Width) / 2);
        Top = SystemParameters.WorkArea.Top + ((SystemParameters.WorkArea.Height - Height) / 2);
    }

    private bool SourceIsAccount()
    {
        return _browser.Source.Contains("account.ring.com", StringComparison.OrdinalIgnoreCase);
    }

    private static System.Drawing.Icon LoadTrayIcon()
    {
        try
        {
            var icon = System.Drawing.Icon.ExtractAssociatedIcon(AppIdentity.ExecutablePath);
            if (icon != null)
                return icon;
        }
        catch (Exception ex)
        {
            StatusLog.Write("tray icon failed: " + ex.Message);
        }

        return System.Drawing.SystemIcons.Application;
    }

    private TaskbarIcon CreateTray()
    {
        var tray = new TaskbarIcon
        {
            ToolTipText = "Doorstep (unofficial)",
            Icon = LoadTrayIcon(),
            MenuActivation = H.NotifyIcon.Core.PopupActivationMode.RightClick,
            NoLeftClickDelay = true
        };
        tray.TrayLeftMouseUp += (_, _) => BringToFront();
        tray.TrayRightMouseUp += (_, _) => QueueTrayMenu();
        tray.TrayContextMenuOpen += (_, _) => QueueTrayMenu();
        tray.ForceCreate(true);
        return tray;
    }

    private bool _trayMenuQueued;

    private void QueueTrayMenu()
    {
        if (_trayMenuQueued)
            return;

        _trayMenuQueued = true;
        Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, () =>
        {
            _trayMenuQueued = false;
            ShowTrayMenu();
        });
    }

    private void ShowTrayMenu()
    {
        StatusLog.Write("tray right click");
        var command = TrayMenu.Show(
            new WindowInteropHelper(this).Handle,
            AppearanceSettings.Mode,
            StartupRegistration.IsEnabled,
            UpdateSettings.CheckAtStartup,
            UpdateChecker.UpdateAvailable);
        switch (command)
        {
            case TrayCommand.Show:
                BringToFront();
                break;
            case TrayCommand.Reload:
                _browser.Reload();
                break;
            case TrayCommand.Test:
                ToastService.ShowTest();
                break;
            case TrayCommand.Startup:
                ToggleStartup();
                break;
            case TrayCommand.Check:
                UpdateChecker.Check(announce: true);
                break;
            case TrayCommand.CheckAtStartup:
                UpdateSettings.SetCheckAtStartup(!UpdateSettings.CheckAtStartup);
                break;
            case TrayCommand.Install:
                UpdateChecker.Install();
                break;
            case TrayCommand.AppearanceSystem:
                AppearanceSettings.Set(AppearanceMode.System);
                break;
            case TrayCommand.AppearanceLight:
                AppearanceSettings.Set(AppearanceMode.Light);
                break;
            case TrayCommand.AppearanceDark:
                AppearanceSettings.Set(AppearanceMode.Dark);
                break;
            case TrayCommand.Quit:
                Quit();
                break;
        }
    }

    private void ToggleStartup()
    {
        try
        {
            StartupRegistration.SetEnabled(!StartupRegistration.IsEnabled);
        }
        catch (Exception ex)
        {
            StatusLog.Write("startup registration failed: " + ex.Message);
        }
    }

    private void OnAppearanceChanged()
    {
        if (_allowExit)
            return;

        AppearanceSettings.Apply(this, LoadingPanel, LoadingText);
        _browser.ApplyAppearance(reload: true);
        foreach (var popup in Application.Current.Windows.OfType<PopupWindow>().ToList())
            popup.ApplyAppearance();
    }

    private void ApplyToolWindow(bool tool)
    {
        var handle = new WindowInteropHelper(this).Handle;
        if (handle == IntPtr.Zero)
            return;

        const int extendedStyle = -20;
        const long toolWindow = 0x00000080;
        const long appWindow = 0x00040000;
        var style = GetWindowLong(handle, extendedStyle).ToInt64();
        style = tool
            ? (style | toolWindow) & ~appWindow
            : (style & ~toolWindow) | appWindow;
        SetWindowLong(handle, extendedStyle, new IntPtr(style));
        SetWindowPos(handle, IntPtr.Zero, 0, 0, 0, 0, 0x0001 | 0x0002 | 0x0004 | 0x0020);
    }

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
    private static extern IntPtr GetWindowLongPtr64(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongW")]
    private static extern IntPtr GetWindowLong32(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")]
    private static extern IntPtr SetWindowLongPtr64(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongW")]
    private static extern IntPtr SetWindowLong32(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

    [DllImport("user32.dll")]
    private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, uint flags);

    private static IntPtr GetWindowLong(IntPtr handle, int index) =>
        IntPtr.Size == 8 ? GetWindowLongPtr64(handle, index) : GetWindowLong32(handle, index);

    private static void SetWindowLong(IntPtr handle, int index, IntPtr value)
    {
        if (IntPtr.Size == 8)
            SetWindowLongPtr64(handle, index, value);
        else
            SetWindowLong32(handle, index, value);
    }
}
