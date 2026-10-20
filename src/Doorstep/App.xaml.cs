using System.Windows;
using System.Windows.Threading;
using Microsoft.Toolkit.Uwp.Notifications;

namespace Doorstep;

public partial class App : Application
{
    private MainWindow? _window;

    public App()
    {
        AppIdentity.SetProcessId();
        AppIdentity.Register();
        InitializeComponent();
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppearanceSettings.Load();
        UpdateSettings.Load();

        var background = HasArg(e.Args, "--background");
        var testToast = HasArg(e.Args, "--test-toast");
        var toastActivated = ToastNotificationManagerCompat.WasCurrentProcessToastActivated();
        StatusLog.Write(background ? "starting background" : "starting window");

        if (!InstanceManager.TryStart(OnRemoteCommand))
        {
            var command = testToast ? "test-toast" : "show";
            StatusLog.Write("signaling " + command);
            try
            {
                InstanceManager.Signal(command);
            }
            catch (Exception ex)
            {
                StatusLog.Write("signal failed: " + ex.Message);
            }

            Shutdown();
            return;
        }

        if (!DisclaimerNotice.IsAcknowledged)
        {
            var notice = new DisclaimerWindow();
            var accepted = notice.ShowDialog() == true;
            if (!accepted)
            {
                StatusLog.Write("notice declined");
                Shutdown();
                return;
            }

            DisclaimerNotice.Acknowledge();
            StatusLog.Write("notice accepted");
        }

        try
        {
            _window = new MainWindow();
        }
        catch (Exception ex)
        {
            StatusLog.Write("window failed: " + ex);
            Shutdown();
            return;
        }

        MainWindow = _window;
        if (background && !testToast && !toastActivated)
            _window.StartInTray();

        StatusLog.Write("showing window");
        _window.Show();
        StatusLog.Write("window shown");
        _window.StartShell();
        ActivationRouter.SetHandler(arguments => _window.OpenFromToast(arguments));

        if (testToast)
            ToastService.ShowTest();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        InstanceManager.Release();
        base.OnExit(e);
    }

    private void OnRemoteCommand(string command)
    {
        StatusLog.Write("remote " + command);
        Dispatcher.BeginInvoke(DispatcherPriority.Send, () =>
        {
            if (command == "test-toast")
                ToastService.ShowTest();
            else
                _window?.BringToFront();
        });
    }

    private static void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        StatusLog.Write("ui error: " + e.Exception.Message);
        e.Handled = true;
    }

    private static bool HasArg(IEnumerable<string> args, string value) =>
        args.Any(arg => arg.Equals(value, StringComparison.OrdinalIgnoreCase));
}
