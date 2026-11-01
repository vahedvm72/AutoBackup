using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using Microsoft.Win32;

namespace AutoBackup.Services;

public sealed class UpdateInfo
{
    public Version Version { get; set; }
    public string Tag { get; set; }
    public string PageUrl { get; set; }
    public string DownloadUrl { get; set; }
    public string Notes { get; set; }
}

public static class UpdateService
{
    public const string Repository = "vahedvm72/AutoBackup";

    private const string LatestReleaseUrl = "https://api.github.com/repos/" + Repository + "/releases/latest";
    private const string InstallerAssetName = "AutoBackupSetup.exe";
    private const string UserAgent = "AutoBackup-Updater";

    public static Version CurrentVersion
    {
        get
        {
            Version version;
            return Version.TryParse(AppInfo.Version, out version) ? version : new Version(0, 0);
        }
    }

    public static bool IsInstalled
    {
        get
        {
            try
            {
                using (var key = Registry.CurrentUser.OpenSubKey(AppInfo.UninstallRegistryKey))
                {
                    return key != null;
                }
            }
            catch
            {
                return false;
            }
        }
    }

    public static async Task<UpdateInfo> CheckAsync(CancellationToken token)
    {
        EnableModernTls();
        var request = (HttpWebRequest)WebRequest.Create(LatestReleaseUrl);
        request.UserAgent = UserAgent;
        request.Accept = "application/vnd.github+json";
        request.Timeout = 12000;
        request.ReadWriteTimeout = 12000;

        using (token.Register(delegate
        {
            try { request.Abort(); } catch { }
        }))
        {
            try
            {
                using (var response = (HttpWebResponse)await request.GetResponseAsync().ConfigureAwait(false))
                using (var reader = new StreamReader(response.GetResponseStream()))
                {
                    var json = await reader.ReadToEndAsync().ConfigureAwait(false);
                    return ParseRelease(json);
                }
            }
            catch (WebException ex)
            {
                var response = ex.Response as HttpWebResponse;
                if (response != null && response.StatusCode == HttpStatusCode.NotFound)
                {
                    return null;
                }
                throw new AutoBackupException("Could not reach GitHub. Check your internet connection and try again.", ex);
            }
        }
    }

    public static async Task DownloadAsync(UpdateInfo update, string destination, Action<double> progress, CancellationToken token)
    {
        if (update == null || string.IsNullOrEmpty(update.DownloadUrl))
        {
            throw new AutoBackupException("The update download address is missing.");
        }
        EnableModernTls();
        var folder = Path.GetDirectoryName(destination);
        if (!string.IsNullOrEmpty(folder))
        {
            Directory.CreateDirectory(folder);
        }

        var request = (HttpWebRequest)WebRequest.Create(update.DownloadUrl);
        request.UserAgent = UserAgent;
        request.Timeout = 20000;
        request.ReadWriteTimeout = 20000;

        using (token.Register(delegate
        {
            try { request.Abort(); } catch { }
        }))
        {
            try
            {
                using (var response = (HttpWebResponse)await request.GetResponseAsync().ConfigureAwait(false))
                using (var input = response.GetResponseStream())
                using (var output = new FileStream(destination, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    var total = response.ContentLength;
                    long done = 0;
                    var buffer = new byte[81920];
                    while (true)
                    {
                        var read = await input.ReadAsync(buffer, 0, buffer.Length, token).ConfigureAwait(false);
                        if (read <= 0)
                        {
                            break;
                        }
                        await output.WriteAsync(buffer, 0, read, token).ConfigureAwait(false);
                        done += read;
                        if (progress != null && total > 0)
                        {
                            progress(Math.Min(100, done * 100.0 / total));
                        }
                    }
                }
            }
            catch (WebException ex)
            {
                TryDelete(destination);
                throw new AutoBackupException("The update could not be downloaded from GitHub.", ex);
            }
            catch
            {
                TryDelete(destination);
                throw;
            }
        }
        if (progress != null)
        {
            progress(100);
        }
    }

    public static string PrepareInstallerPath(UpdateInfo update)
    {
        var folder = Path.Combine(Path.GetTempPath(), "AutoBackupUpdate", update != null && update.Version != null ? update.Version.ToString() : "latest");
        Directory.CreateDirectory(folder);
        return Path.Combine(folder, InstallerAssetName);
    }

    public static void RunInstaller(string setupPath)
    {
        if (string.IsNullOrEmpty(setupPath) || !File.Exists(setupPath))
        {
            throw new AutoBackupException("The downloaded installer was not found.");
        }
        Process.Start(new ProcessStartInfo
        {
            FileName = setupPath,
            Arguments = "--silent --relaunch",
            WorkingDirectory = Path.GetDirectoryName(setupPath),
            UseShellExecute = false
        });
    }

    private static UpdateInfo ParseRelease(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }
        var serializer = new JavaScriptSerializer { MaxJsonLength = int.MaxValue };
        var root = serializer.Deserialize<Dictionary<string, object>>(json);
        if (root == null)
        {
            return null;
        }
        var tag = GetString(root, "tag_name");
        var version = ParseVersion(tag);
        if (version == null || version <= CurrentVersion)
        {
            return null;
        }

        var info = new UpdateInfo
        {
            Version = version,
            Tag = tag,
            PageUrl = GetString(root, "html_url"),
            Notes = GetString(root, "body")
        };

        object assetsValue;
        if (root.TryGetValue("assets", out assetsValue))
        {
            var assets = assetsValue as System.Collections.IEnumerable;
            if (assets != null)
            {
                foreach (var asset in assets)
                {
                    var map = asset as Dictionary<string, object>;
                    if (map == null)
                    {
                        continue;
                    }
                    var name = GetString(map, "name");
                    if (string.Equals(name, InstallerAssetName, StringComparison.OrdinalIgnoreCase))
                    {
                        info.DownloadUrl = GetString(map, "browser_download_url");
                        break;
                    }
                }
            }
        }
        return string.IsNullOrEmpty(info.DownloadUrl) ? null : info;
    }

    private static Version ParseVersion(string tag)
    {
        if (string.IsNullOrWhiteSpace(tag))
        {
            return null;
        }
        var value = tag.Trim();
        if (value.StartsWith("v", StringComparison.OrdinalIgnoreCase))
        {
            value = value.Substring(1);
        }
        var cut = value.IndexOfAny(new[] { '-', '+', ' ' });
        if (cut >= 0)
        {
            value = value.Substring(0, cut);
        }
        Version parsed;
        return Version.TryParse(value, out parsed) ? parsed : null;
    }

    private static string GetString(Dictionary<string, object> map, string key)
    {
        object value;
        return map != null && map.TryGetValue(key, out value) && value != null ? value.ToString() : null;
    }

    private static void EnableModernTls()
    {
        try
        {
            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
        }
        catch
        {
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
        }
    }
}
