using System.Windows;
using Microsoft.Web.WebView2.Core;

namespace Doorstep;

public partial class PopupWindow : Window
{
    private readonly RingBrowser _browser;
    private bool _shown;

    public PopupWindow()
    {
        InitializeComponent();
        AppearanceSettings.Apply(this, panel: null, label: null);
        _browser = new RingBrowser(WebView);
    }

    public CoreWebView2? Core => _browser.Core;

    public async Task InitializeAsync()
    {
        if (!IsLoaded)
        {
            var loaded = new TaskCompletionSource();
            Loaded += (_, _) => loaded.TrySetResult();
            Show();
            _shown = true;
            await loaded.Task;
        }
        else if (!_shown)
        {
            Show();
            _shown = true;
        }

        await _browser.InitializeAsync(startUrl: null);
        Activate();
    }

    public void ApplyAppearance()
    {
        AppearanceSettings.Apply(this, panel: null, label: null);
        _browser.ApplyAppearance(reload: true);
    }

    protected override void OnClosed(EventArgs e)
    {
        _browser.Dispose();
        base.OnClosed(e);
    }
}
