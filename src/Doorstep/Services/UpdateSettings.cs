using System.IO;

namespace Doorstep;

internal static class UpdateSettings
{
    private static readonly string FilePath = Path.Combine(StatusLog.Folder, "updates.txt");
    private static bool _loaded;
    private static bool _checkAtStartup = true;

    public static bool CheckAtStartup
    {
        get
        {
            Load();
            return _checkAtStartup;
        }
    }

    public static void Load()
    {
        if (_loaded)
            return;
        _loaded = true;

        try
        {
            if (File.Exists(FilePath))
            {
                var text = File.ReadAllText(FilePath).Trim();
                if (text.Equals("off", StringComparison.OrdinalIgnoreCase))
                    _checkAtStartup = false;
            }
        }
        catch (Exception ex)
        {
            StatusLog.Write("update setting read failed: " + ex.Message);
        }

        StatusLog.Write("update check at startup " + (_checkAtStartup ? "on" : "off"));
    }

    public static void SetCheckAtStartup(bool enabled)
    {
        Load();
        _checkAtStartup = enabled;
        try
        {
            Directory.CreateDirectory(StatusLog.Folder);
            File.WriteAllText(FilePath, enabled ? "on" : "off");
        }
        catch (Exception ex)
        {
            StatusLog.Write("update setting save failed: " + ex.Message);
        }

        StatusLog.Write("update check at startup " + (enabled ? "on" : "off"));
    }
}
