using System.IO;

namespace Doorstep;

internal static class DisclaimerNotice
{
    private static string FilePath => Path.Combine(StatusLog.Folder, "notice-accepted");

    public static bool IsAcknowledged => File.Exists(FilePath);

    public static void Acknowledge()
    {
        Directory.CreateDirectory(StatusLog.Folder);
        File.WriteAllText(FilePath, "accepted");
    }
}
