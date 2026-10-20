using System.IO;
using Microsoft.Web.WebView2.Core;

namespace Doorstep;

internal static class WebViewEnvironment
{
    private static readonly SemaphoreSlim Gate = new(1, 1);
    private static CoreWebView2Environment? _environment;

    public static Task<CoreWebView2Environment> GetAsync()
    {
        if (_environment != null)
            return Task.FromResult(_environment);

        return CreateAsync();
    }

    private static async Task<CoreWebView2Environment> CreateAsync()
    {
        await Gate.WaitAsync();
        try
        {
            if (_environment != null)
                return _environment;

            var folder = Path.Combine(StatusLog.Folder, "WebView2");
            Directory.CreateDirectory(folder);
            StatusLog.Write("creating environment");
            _environment = await CoreWebView2Environment.CreateAsync(
                browserExecutableFolder: null,
                userDataFolder: folder);
            StatusLog.Write("environment created");
            return _environment;
        }
        catch (Exception ex)
        {
            StatusLog.Write("environment failed: " + ex.Message);
            throw;
        }
        finally
        {
            Gate.Release();
        }
    }
}
