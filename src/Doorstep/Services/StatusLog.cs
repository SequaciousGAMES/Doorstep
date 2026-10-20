using System.IO;

namespace Doorstep;

internal static class StatusLog
{
    private static readonly object Gate = new();

    public static string Folder { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Doorstep");

    public static void Write(string message)
    {
        try
        {
            Directory.CreateDirectory(Folder);
            var path = Path.Combine(Folder, "status.log");
            var line = DateTimeOffset.Now.ToString("yyyy-MM-dd HH:mm:ss")
                + " "
                + message
                + Environment.NewLine;
            lock (Gate)
            {
                if (File.Exists(path) && new FileInfo(path).Length > 200_000)
                    File.Delete(path);
                File.AppendAllText(path, line);
            }
        }
        catch
        {
            // Logging must not take down the app.
        }
    }
}
