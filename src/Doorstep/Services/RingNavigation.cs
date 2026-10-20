using System.Diagnostics;

namespace Doorstep;

internal static class RingNavigation
{
    public const string DashboardUrl = "https://account.ring.com/account/dashboard";

    public static bool IsAllowed(string? uri)
    {
        if (string.IsNullOrWhiteSpace(uri))
            return false;
        if (!Uri.TryCreate(uri, UriKind.Absolute, out var parsed))
            return false;
        if (parsed.Scheme.Equals("about", StringComparison.OrdinalIgnoreCase))
            return true;
        if (parsed.Scheme != Uri.UriSchemeHttps && parsed.Scheme != Uri.UriSchemeHttp)
            return false;
        return IsRingHost(parsed.Host);
    }

    public static bool IsRingHost(string? host)
    {
        if (string.IsNullOrWhiteSpace(host))
            return false;
        return host.Equals("ring.com", StringComparison.OrdinalIgnoreCase)
            || host.EndsWith(".ring.com", StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsHttp(string? uri)
    {
        return Uri.TryCreate(uri, UriKind.Absolute, out var parsed)
            && (parsed.Scheme == Uri.UriSchemeHttps || parsed.Scheme == Uri.UriSchemeHttp);
    }

    public static string Safe(string? uri)
    {
        if (!Uri.TryCreate(uri, UriKind.Absolute, out var parsed))
            return "";
        if (parsed.Scheme.Equals("about", StringComparison.OrdinalIgnoreCase))
            return parsed.Scheme;
        if (!IsRingHost(parsed.Host))
            return parsed.Host;
        return parsed.GetLeftPart(UriPartial.Path);
    }

    public static string HostOf(string? uri)
    {
        return Uri.TryCreate(uri, UriKind.Absolute, out var parsed) ? parsed.Host : "";
    }

    public static void OpenExternal(string uri)
    {
        try
        {
            Process.Start(new ProcessStartInfo(uri) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            StatusLog.Write("external open failed for " + HostOf(uri) + ": " + ex.Message);
        }
    }
}
