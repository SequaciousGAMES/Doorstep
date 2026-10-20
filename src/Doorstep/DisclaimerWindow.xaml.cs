using System.Windows;

namespace Doorstep;

public partial class DisclaimerWindow : Window
{
    public DisclaimerWindow()
    {
        InitializeComponent();
        AppearanceSettings.Apply(this, panel: null, label: Heading);
        var foreground = Heading.Foreground;
        Unofficial.Foreground = foreground;
        SignIn.Foreground = foreground;
        Alerts.Foreground = foreground;
    }

    private void OnContinue(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
        Close();
    }
}
