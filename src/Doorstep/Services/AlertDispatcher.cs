namespace Doorstep;

internal static class AlertDispatcher
{
    private static readonly TimeSpan Window = TimeSpan.FromSeconds(20);
    private static readonly Dictionary<string, DateTimeOffset> Recent = new();

    public static void Raise(string? title, string? body, string? tag, string? url)
    {
        title = Clean(title, 120);
        body = Clean(body, 240);
        tag = Clean(tag, 80);
        if (title.Length == 0)
            title = "Ring";
        if (ContainsSecret(title) || ContainsSecret(body) || ContainsSecret(tag))
            return;
        if (!RingNavigation.IsAllowed(url))
            url = null;

        var keys = new List<string> { "text:" + title + "|" + body };
        if (tag.Length > 0)
            keys.Add("tag:" + tag);

        var now = DateTimeOffset.UtcNow;
        lock (Recent)
        {
            Prune(now);
            if (keys.Any(key => Recent.ContainsKey(key)))
                return;
            foreach (var key in keys)
                Recent[key] = now;
        }

        ToastService.Show(title, body, tag, url);
    }

    private static void Prune(DateTimeOffset now)
    {
        var expired = Recent.Where(pair => now - pair.Value >= Window).Select(pair => pair.Key).ToArray();
        foreach (var key in expired)
            Recent.Remove(key);
    }

    private static string Clean(string? value, int max)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "";
        var text = value.Replace('\r', ' ').Replace('\n', ' ').Trim();
        return text.Length <= max ? text : text[..max];
    }

    private static bool ContainsSecret(string value)
    {
        return value.Contains("password", StringComparison.OrdinalIgnoreCase)
            || value.Contains("access_token", StringComparison.OrdinalIgnoreCase)
            || value.Contains("refresh_token", StringComparison.OrdinalIgnoreCase)
            || value.Contains("authorization", StringComparison.OrdinalIgnoreCase)
            || value.Contains("set-cookie", StringComparison.OrdinalIgnoreCase);
    }
}
