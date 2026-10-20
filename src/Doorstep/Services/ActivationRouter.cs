using System.Windows;

namespace Doorstep;

internal static class ActivationRouter
{
    private static readonly object Gate = new();
    private static Action<string?>? _handler;
    private static string? _pending;

    public static void SetHandler(Action<string?> handler)
    {
        string? pending;
        lock (Gate)
        {
            _handler = handler;
            pending = _pending;
            _pending = null;
        }

        if (pending != null)
            Dispatch(handler, pending);
    }

    public static void Enqueue(string? arguments)
    {
        Action<string?>? handler;
        lock (Gate)
        {
            handler = _handler;
            if (handler == null)
            {
                _pending = arguments ?? "";
                return;
            }
        }

        Dispatch(handler, arguments);
    }

    public static string? GetArgument(string? arguments, string name)
    {
        if (string.IsNullOrWhiteSpace(arguments))
            return null;

        foreach (var part in arguments.Split(['&', ';'], StringSplitOptions.RemoveEmptyEntries))
        {
            var separator = part.IndexOf('=');
            if (separator <= 0)
                continue;
            var key = Uri.UnescapeDataString(part[..separator]);
            if (!key.Equals(name, StringComparison.OrdinalIgnoreCase))
                continue;
            return Uri.UnescapeDataString(part[(separator + 1)..]);
        }

        return null;
    }

    private static void Dispatch(Action<string?> handler, string? arguments)
    {
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher == null || dispatcher.CheckAccess())
            handler(arguments);
        else
            dispatcher.BeginInvoke(() => handler(arguments));
    }
}
