using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using Microsoft.Win32;

namespace Doorstep;

internal static class UpdateChecker
{
    private const string LatestReleaseUrl =
        "https://api.github.com/repos/SequaciousGAMES/Doorstep/releases/latest";
    private const string SetupName = "DoorstepSetup.exe";
    private const string UninstallKey =
        @"Software\Microsoft\Windows\CurrentVersion\Uninstall\{8F3C1D6A-2E47-4B90-A5C8-1D7E9B04F6A2}_is1";

    private static readonly HttpClient Http = CreateClient();
    private static readonly object Gate = new();
    private static Task? _current;
    private static bool _announce;
    private static int _busy;
    private static Pending? _pending;

    public static bool IsInstalling { get; private set; }

    public static bool UpdateAvailable
    {
        get
        {
            lock (Gate)
                return _pending != null;
        }
    }

    public static event Action? ExitRequested;

    public static void Check(bool announce)
    {
        lock (Gate)
        {
            if (announce)
                _announce = true;
            if (_current is { IsCompleted: false })
                return;

            _current = CheckAsync();
        }
    }

    public static void Install()
    {
        if (Interlocked.Exchange(ref _busy, 1) == 1)
            return;

        _ = InstallAsync();
    }

    public static bool IsReleasePage(string? uri)
    {
        if (!Uri.TryCreate(uri, UriKind.Absolute, out var parsed))
            return false;
        if (!parsed.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
            return false;
        if (!parsed.Host.Equals("github.com", StringComparison.OrdinalIgnoreCase))
            return false;

        var path = parsed.AbsolutePath.TrimEnd('/');
        return path.Equals("/SequaciousGAMES/Doorstep/releases", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("/SequaciousGAMES/Doorstep/releases/", StringComparison.OrdinalIgnoreCase);
    }

    private static async Task CheckAsync()
    {
        while (true)
        {
            Report report;
            try
            {
                report = await FetchAsync();
            }
            catch (Exception ex)
            {
                StatusLog.Write("update check failed: " + ex.Message);
                report = Report.Failed();
            }

            bool announce;
            lock (Gate)
            {
                announce = _announce;
                _announce = false;
            }

            Announce(report, announce);

            lock (Gate)
            {
                if (!_announce)
                    return;
            }
        }
    }

    private static async Task InstallAsync()
    {
        try
        {
            Pending? cached;
            lock (Gate)
                cached = _pending;

            Pending pending;
            if (cached is { } known && IsSetupDownload(known.Url))
            {
                pending = known;
            }
            else
            {
                var report = await FetchAsync();
                if (report.Kind != ReportKind.Available || !IsSetupDownload(report.DownloadUrl))
                {
                    if (report.Kind == ReportKind.Available)
                    {
                        ToastService.Show(
                            "Doorstep",
                            "Version " + report.Latest + " is available, but the setup program was not published.",
                            "update",
                            null,
                            report.Page);
                    }
                    else
                    {
                        Announce(report, announce: true);
                    }

                    return;
                }

                pending = new Pending(report.Latest!, report.DownloadUrl!, report.DownloadSize);
            }

            ToastService.Show("Doorstep", "Downloading version " + pending.Version + ".", "update", null);
            var file = await DownloadAsync(pending);
            if (file == null)
            {
                ToastService.Show("Doorstep", "Couldn't download the update.", "update", null);
                return;
            }

            if (!StartInstaller(file))
            {
                ToastService.Show("Doorstep", "Couldn't start the update.", "update", null);
                return;
            }

            IsInstalling = true;
            RequestExit();
        }
        catch (Exception ex)
        {
            StatusLog.Write("update install failed: " + ex.Message);
            ToastService.Show("Doorstep", "Couldn't install the update.", "update", null);
        }
        finally
        {
            if (!IsInstalling)
                Interlocked.Exchange(ref _busy, 0);
        }
    }

    private static async Task<Report> FetchAsync()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        using var request = new HttpRequestMessage(HttpMethod.Get, LatestReleaseUrl);
        request.Headers.Accept.ParseAdd("application/vnd.github+json");
        using var response = await Http.SendAsync(request, timeout.Token);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            StatusLog.Write("update check found no release");
            Remember(null);
            return Report.None();
        }

        if (!response.IsSuccessStatusCode)
        {
            StatusLog.Write("update check failed: HTTP " + (int)response.StatusCode);
            return Report.Failed();
        }

        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(timeout.Token));
        var root = json.RootElement;
        var tag = root.TryGetProperty("tag_name", out var tagProperty) ? tagProperty.GetString() : null;
        var page = root.TryGetProperty("html_url", out var pageProperty) ? pageProperty.GetString() : null;
        if (!TryParseVersion(tag, out var latest))
        {
            StatusLog.Write("update check failed: unreadable version");
            return Report.Failed();
        }

        if (!IsReleasePage(page))
            page = "https://github.com/SequaciousGAMES/Doorstep/releases/latest";

        string? download = null;
        long? size = null;
        if (root.TryGetProperty("assets", out var assets))
        {
            foreach (var asset in assets.EnumerateArray())
            {
                var name = asset.TryGetProperty("name", out var nameProperty) ? nameProperty.GetString() : null;
                if (!string.Equals(name, SetupName, StringComparison.OrdinalIgnoreCase))
                    continue;

                download = asset.TryGetProperty("browser_download_url", out var urlProperty)
                    ? urlProperty.GetString()
                    : null;
                if (asset.TryGetProperty("size", out var sizeProperty) && sizeProperty.TryGetInt64(out var bytes))
                    size = bytes;
                break;
            }
        }

        if (!IsSetupDownload(download))
            download = null;

        var current = CurrentVersion();
        var compared = Compare(current, latest);
        StatusLog.Write("update check " + Format(current) + " latest " + Format(latest));
        if (compared < 0)
        {
            Pending? pending = download == null ? null : new Pending(Format(latest), download, size);
            Remember(pending);
            return new Report(ReportKind.Available, Format(current), Format(latest), page, download, size);
        }

        Remember(null);
        if (compared > 0)
            return new Report(ReportKind.Ahead, Format(current), Format(latest), page, null, null);
        return new Report(ReportKind.Current, Format(current), Format(latest), page, null, null);
    }

    private static async Task<string?> DownloadAsync(Pending pending)
    {
        var folder = Path.Combine(Path.GetTempPath(), "DoorstepUpdate");
        Directory.CreateDirectory(folder);
        var destination = Path.Combine(folder, "DoorstepSetup-" + pending.Version + ".exe");
        var partial = destination + ".partial";

        using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(10));
        using var request = new HttpRequestMessage(HttpMethod.Get, pending.Url);
        using var response = await Http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token);
        if (!response.IsSuccessStatusCode)
        {
            StatusLog.Write("update download failed: HTTP " + (int)response.StatusCode);
            return null;
        }

        await using (var input = await response.Content.ReadAsStreamAsync(timeout.Token))
        await using (var output = new FileStream(partial, FileMode.Create, FileAccess.Write, FileShare.None))
            await input.CopyToAsync(output, timeout.Token);

        var info = new FileInfo(partial);
        if (pending.Size is > 0 && info.Length != pending.Size)
        {
            StatusLog.Write("update download size " + info.Length + " expected " + pending.Size);
            TryDelete(partial);
            return null;
        }

        if (!HasExeHeader(partial))
        {
            StatusLog.Write("update download was not an executable");
            TryDelete(partial);
            return null;
        }

        if (File.Exists(destination))
            File.Delete(destination);
        File.Move(partial, destination);
        MarkFromInternet(destination);
        StatusLog.Write("update downloaded " + destination);
        return destination;
    }

    private static bool StartInstaller(string file)
    {
        var install = FindInstalledCopy();
        var arguments = "";
        if (install is { } copy && !copy.Directory.Contains('"'))
        {
            arguments = "/SILENT /NORESTART /NORESTARTAPPLICATIONS /CLOSEAPPLICATIONS /FORCECLOSEAPPLICATIONS /launch=1 "
                + (copy.AllUsers ? "/ALLUSERS" : "/CURRENTUSER")
                + " /DIR=\"" + copy.Directory + "\"";
            StatusLog.Write("update installing over " + copy.Directory);
        }
        else
        {
            StatusLog.Write("update opening setup wizard");
        }

        var started = Process.Start(new ProcessStartInfo(file)
        {
            UseShellExecute = true,
            Arguments = arguments
        });
        if (started == null)
            return false;

        StatusLog.Write("update installer started");
        return true;
    }

    private static InstalledCopy? FindInstalledCopy()
    {
        var allUsers = ReadInstall(RegistryHive.LocalMachine, allUsers: true);
        var currentUser = ReadInstall(RegistryHive.CurrentUser, allUsers: false);
        var exeDir = Path.GetDirectoryName(AppIdentity.ExecutablePath);
        if (allUsers is { } machine && SameDirectory(machine.Directory, exeDir))
            return machine;
        if (currentUser is { } user && SameDirectory(user.Directory, exeDir))
            return user;
        if (allUsers != null && currentUser == null)
            return allUsers;
        if (currentUser != null && allUsers == null)
            return currentUser;
        return null;
    }

    private static bool SameDirectory(string? left, string? right)
    {
        if (string.IsNullOrWhiteSpace(left) || string.IsNullOrWhiteSpace(right))
            return false;

        return string.Equals(
            Path.GetFullPath(left).TrimEnd('\\'),
            Path.GetFullPath(right).TrimEnd('\\'),
            StringComparison.OrdinalIgnoreCase);
    }

    private static InstalledCopy? ReadInstall(RegistryHive hive, bool allUsers)
    {
        try
        {
            using var baseKey = RegistryKey.OpenBaseKey(hive, RegistryView.Registry64);
            using var key = baseKey.OpenSubKey(UninstallKey);
            var path = key?.GetValue("Inno Setup: App Path") as string;
            if (string.IsNullOrWhiteSpace(path))
                path = key?.GetValue("InstallLocation") as string;
            if (string.IsNullOrWhiteSpace(path))
                return null;

            path = Path.GetFullPath(path.Trim().TrimEnd('\\'));
            return Directory.Exists(path) ? new InstalledCopy(path, allUsers) : null;
        }
        catch (Exception ex)
        {
            StatusLog.Write("update install lookup failed: " + ex.Message);
            return null;
        }
    }

    private static void Announce(Report report, bool announce)
    {
        if (report.Kind == ReportKind.Available)
        {
            var body = report.DownloadUrl == null
                ? "Version " + report.Latest + " is available, but the setup program was not published."
                : "Version " + report.Latest + " is available.";
            ToastService.Show(
                "Doorstep",
                body,
                "update",
                null,
                report.Page,
                offerInstall: report.DownloadUrl != null);
            return;
        }

        if (!announce)
            return;

        var message = report.Kind switch
        {
            ReportKind.Current => "Version " + report.Current + " is up to date.",
            ReportKind.Ahead => "Version " + report.Current + " is newer than the latest release, " + report.Latest + ".",
            ReportKind.None => "No Doorstep release was found.",
            _ => "Couldn't check for updates."
        };
        ToastService.Show("Doorstep", message, "update", null);
    }

    private static void RequestExit()
    {
        var handler = ExitRequested;
        if (handler == null)
            return;

        var dispatcher = System.Windows.Application.Current?.Dispatcher;
        if (dispatcher == null || dispatcher.CheckAccess())
            handler();
        else
            dispatcher.BeginInvoke(handler);
    }

    private static void Remember(Pending? pending)
    {
        lock (Gate)
            _pending = pending;
    }

    private static bool IsSetupDownload(string? uri)
    {
        if (!Uri.TryCreate(uri, UriKind.Absolute, out var parsed))
            return false;
        if (!parsed.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
            return false;
        if (!parsed.Host.Equals("github.com", StringComparison.OrdinalIgnoreCase))
            return false;

        var path = parsed.AbsolutePath;
        return path.Contains("/SequaciousGAMES/Doorstep/releases/download/", StringComparison.OrdinalIgnoreCase)
            && path.EndsWith("/" + SetupName, StringComparison.OrdinalIgnoreCase);
    }

    private static bool HasExeHeader(string path)
    {
        Span<byte> header = stackalloc byte[2];
        using var stream = File.OpenRead(path);
        return stream.Read(header) == 2 && header[0] == (byte)'M' && header[1] == (byte)'Z';
    }

    private static void MarkFromInternet(string path)
    {
        try
        {
            File.WriteAllText(
                path + ":Zone.Identifier",
                "[ZoneTransfer]\r\nZoneId=3\r\nHostUrl=https://github.com/SequaciousGAMES/Doorstep/releases\r\n");
        }
        catch (Exception ex)
        {
            StatusLog.Write("update mark failed: " + ex.Message);
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch (Exception ex)
        {
            StatusLog.Write("update cleanup failed: " + ex.Message);
        }
    }

    private static Version CurrentVersion() =>
        Assembly.GetExecutingAssembly().GetName().Version ?? new Version(0, 0);

    private static int Compare(Version local, Version remote)
    {
        int Part(int value) => value < 0 ? 0 : value;
        int[] left = [local.Major, Part(local.Minor), Part(local.Build), Part(local.Revision)];
        int[] right = [remote.Major, Part(remote.Minor), Part(remote.Build), Part(remote.Revision)];
        for (var i = 0; i < left.Length; i++)
        {
            var compared = left[i].CompareTo(right[i]);
            if (compared != 0)
                return compared;
        }

        return 0;
    }

    private static bool TryParseVersion(string? text, out Version version)
    {
        version = new Version(0, 0);
        if (string.IsNullOrWhiteSpace(text))
            return false;

        text = text.Trim();
        if (text.StartsWith('v') || text.StartsWith('V'))
            text = text[1..];
        var dash = text.IndexOf('-');
        if (dash >= 0)
            text = text[..dash];
        return Version.TryParse(text, out version!);
    }

    private static string Format(Version version)
    {
        var build = version.Build < 0 ? 0 : version.Build;
        return version.Major + "." + version.Minor + "." + build;
    }

    private static HttpClient CreateClient()
    {
        var handler = new HttpClientHandler
        {
            AutomaticDecompression = DecompressionMethods.All
        };
        var client = new HttpClient(handler)
        {
            Timeout = Timeout.InfiniteTimeSpan
        };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("Doorstep");
        client.DefaultRequestHeaders.Add("X-GitHub-Api-Version", "2022-11-28");
        return client;
    }

    private enum ReportKind
    {
        Failed,
        None,
        Current,
        Ahead,
        Available
    }

    private readonly record struct Pending(string Version, string Url, long? Size);

    private readonly record struct InstalledCopy(string Directory, bool AllUsers);

    private readonly record struct Report(
        ReportKind Kind,
        string? Current,
        string? Latest,
        string? Page,
        string? DownloadUrl,
        long? DownloadSize)
    {
        public static Report Failed() => new(ReportKind.Failed, null, null, null, null, null);

        public static Report None() => new(ReportKind.None, null, null, null, null, null);
    }
}
