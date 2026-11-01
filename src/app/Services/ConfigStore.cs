using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Web.Script.Serialization;

namespace AutoBackup.Services;

public sealed class ExistingSetup
{
    public string SettingsPath { get; set; }
    public AppConfig Config { get; set; }
    public int ArchiveCount { get; set; }
    public string BackupFolder { get; set; }
    public int BackupCount { get; set; }

    public bool HasSettings
    {
        get { return Config != null && !string.IsNullOrEmpty(SettingsPath); }
    }

    public bool HasBackups
    {
        get { return !string.IsNullOrEmpty(BackupFolder) && BackupCount > 0; }
    }
}

public enum ConfigLoadStatus
{
    Ok,
    NoBootstrap,
    MissingSettingsFile,
    InvalidBootstrap,
    InvalidSettingsFile
}

public sealed class ConfigLoadResult
{
    public ConfigLoadStatus Status { get; set; }
    public AppConfig Config { get; set; }
    public string SettingsPath { get; set; }
    public string Error { get; set; }

    public bool HasConfig
    {
        get { return Status == ConfigLoadStatus.Ok && Config != null; }
    }
}

public static class ConfigStore
{
    private static readonly JavaScriptSerializer Serializer = new JavaScriptSerializer { MaxJsonLength = int.MaxValue };

    public static string BootstrapFolder
    {
        get
        {
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), AppInfo.BootstrapFolderName);
        }
    }

    public static string BootstrapPath
    {
        get { return Path.Combine(BootstrapFolder, "bootstrap.json"); }
    }

    public static string SuggestedSettingsFolder
    {
        get
        {
            var documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            return string.IsNullOrEmpty(documents) ? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) : documents;
        }
    }

    public static string BuildSettingsPath(string folder)
    {
        return Path.Combine(folder, AppInfo.SettingsFileName);
    }

    public static ConfigLoadResult Load()
    {
        if (!File.Exists(BootstrapPath))
        {
            return new ConfigLoadResult { Status = ConfigLoadStatus.NoBootstrap };
        }

        BootstrapConfig bootstrap;
        try
        {
            bootstrap = Serializer.Deserialize<BootstrapConfig>(File.ReadAllText(BootstrapPath, Encoding.UTF8));
        }
        catch (Exception ex)
        {
            return new ConfigLoadResult
            {
                Status = ConfigLoadStatus.InvalidBootstrap,
                Error = "The local settings pointer could not be read. " + ex.Message
            };
        }

        if (bootstrap == null || string.IsNullOrWhiteSpace(bootstrap.SettingsFile))
        {
            return new ConfigLoadResult
            {
                Status = ConfigLoadStatus.InvalidBootstrap,
                Error = "The local settings pointer does not contain a settings file path."
            };
        }

        var settingsPath = AppConfig.CleanPath(bootstrap.SettingsFile);
        if (!File.Exists(settingsPath))
        {
            return new ConfigLoadResult
            {
                Status = ConfigLoadStatus.MissingSettingsFile,
                SettingsPath = settingsPath,
                Error = "The shared settings file could not be found."
            };
        }

        AppConfig loaded;
        string loadError;
        if (TryReadSettings(settingsPath, out loaded, out loadError))
        {
            return new ConfigLoadResult
            {
                Status = ConfigLoadStatus.Ok,
                Config = loaded,
                SettingsPath = settingsPath
            };
        }
        return new ConfigLoadResult
        {
            Status = ConfigLoadStatus.InvalidSettingsFile,
            SettingsPath = settingsPath,
            Error = "The shared settings file could not be read. " + loadError
        };
    }

    public static bool TryReadSettings(string settingsPath, out AppConfig config, out string error)
    {
        config = null;
        error = null;
        try
        {
            if (!File.Exists(settingsPath))
            {
                error = "The settings file does not exist.";
                return false;
            }
            var loaded = Serializer.Deserialize<AppConfig>(File.ReadAllText(settingsPath, Encoding.UTF8));
            if (loaded == null)
            {
                error = "The settings file is empty.";
                return false;
            }
            loaded.Normalize();
            config = loaded;
            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }

    public static ExistingSetup DiscoverExisting()
    {
        if (string.Equals(Environment.GetEnvironmentVariable("AUTOBACKUP_NO_DISCOVERY"), "1", StringComparison.Ordinal))
        {
            return new ExistingSetup();
        }

        var scanner = new DiscoveryScanner();
        foreach (var root in DiscoveryRoots())
        {
            scanner.Scan(root, 0, 3);
            if (scanner.Expired)
            {
                break;
            }
        }

        var result = new ExistingSetup();
        AppConfig bestConfig = null;
        string bestPath = null;
        var bestArchives = -1;
        var bestTime = DateTime.MinValue;

        foreach (var path in scanner.SettingsFiles)
        {
            AppConfig config;
            string error;
            if (!TryReadSettings(path, out config, out error))
            {
                continue;
            }
            var archives = CountArchives(config.BackupFolder);
            DateTime time;
            try { time = File.GetLastWriteTimeUtc(path); }
            catch { time = DateTime.MinValue; }
            if (bestConfig == null || archives > bestArchives || (archives == bestArchives && time > bestTime))
            {
                bestConfig = config;
                bestPath = path;
                bestArchives = archives;
                bestTime = time;
            }
        }

        if (bestConfig != null)
        {
            result.SettingsPath = bestPath;
            result.Config = bestConfig;
            result.ArchiveCount = Math.Max(0, bestArchives);
            return result;
        }

        var newest = DateTime.MinValue;
        foreach (var pair in scanner.ArchiveFolders)
        {
            if (pair.Value.Newest > newest)
            {
                newest = pair.Value.Newest;
                result.BackupFolder = pair.Key;
                result.BackupCount = pair.Value.Count;
            }
        }
        return result;
    }

    private static IEnumerable<string> DiscoveryRoots()
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var roots = new List<string>();
        var profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

        if (!string.IsNullOrEmpty(profile))
        {
            foreach (var name in new[] { "Nextcloud", "Dropbox", "Google Drive", "OneDrive", "iCloudDrive", "Sync", "Cloud", "Backup", "Backups" })
            {
                AddRoot(roots, seen, Path.Combine(profile, name));
            }
        }
        AddRoot(roots, seen, Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments));
        AddRoot(roots, seen, Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory));
        AddRoot(roots, seen, Environment.GetEnvironmentVariable("OneDrive"));
        AddRoot(roots, seen, Environment.GetEnvironmentVariable("OneDriveConsumer"));
        AddRoot(roots, seen, Environment.GetEnvironmentVariable("OneDriveCommercial"));
        AddRoot(roots, seen, profile);
        if (!string.IsNullOrEmpty(profile))
        {
            AddRoot(roots, seen, Path.Combine(profile, "Downloads"));
        }

        try
        {
            foreach (var drive in DriveInfo.GetDrives())
            {
                if ((drive.DriveType != DriveType.Fixed && drive.DriveType != DriveType.Removable) || !drive.IsReady)
                {
                    continue;
                }
                var root = drive.RootDirectory.FullName;
                if (!string.IsNullOrEmpty(profile) && profile.StartsWith(root, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }
                AddRoot(roots, seen, root);
            }
        }
        catch
        {
        }
        return roots;
    }

    private static void AddRoot(List<string> roots, HashSet<string> seen, string root)
    {
        if (string.IsNullOrWhiteSpace(root))
        {
            return;
        }
        try
        {
            if (!Directory.Exists(root) || !seen.Add(root))
            {
                return;
            }
            roots.Add(root);
        }
        catch
        {
        }
    }

    private static int CountArchives(string folder)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder))
            {
                return 0;
            }
            var count = 0;
            foreach (var file in Directory.EnumerateFiles(folder, AppInfo.ArchivePrefix + "*.rar", SearchOption.TopDirectoryOnly))
            {
                count++;
            }
            return count;
        }
        catch
        {
            return 0;
        }
    }

    public static void SaveBootstrap(string settingsFile)
    {
        if (string.IsNullOrWhiteSpace(settingsFile))
        {
            throw new ArgumentException("Settings file path is required.", "settingsFile");
        }
        Directory.CreateDirectory(BootstrapFolder);
        var json = "{\r\n  \"settingsFile\": " + Quote(settingsFile) + "\r\n}\r\n";
        WriteAtomic(BootstrapPath, json);
    }

    public static void SaveSettings(AppConfig config, string settingsFile)
    {
        if (config == null) { throw new ArgumentNullException("config"); }
        if (string.IsNullOrWhiteSpace(settingsFile))
        {
            throw new ArgumentException("Settings file path is required.", "settingsFile");
        }
        config.Normalize();
        var directory = Path.GetDirectoryName(settingsFile);
        if (string.IsNullOrEmpty(directory))
        {
            throw new InvalidOperationException("The settings file path is not valid.");
        }
        Directory.CreateDirectory(directory);

        var json = new StringBuilder();
        json.Append("{\r\n");
        json.Append("  \"version\": 1,\r\n");
        json.Append("  \"jobs\": [");
        var jobs = config.Jobs ?? new List<BackupJob>();
        if (jobs.Count > 0)
        {
            json.Append("\r\n");
            for (var i = 0; i < jobs.Count; i++)
            {
                json.Append("    { \"name\": ").Append(Quote(jobs[i].Name))
                    .Append(", \"sourceFolder\": ").Append(Quote(jobs[i].SourceFolder))
                    .Append(", \"backupFolder\": ").Append(Quote(jobs[i].BackupFolder)).Append(" }");
                if (i < jobs.Count - 1)
                {
                    json.Append(',');
                }
                json.Append("\r\n");
            }
            json.Append("  ");
        }
        json.Append("],\r\n");
        json.Append("  \"sourceFolder\": ").Append(Quote(config.SourceFolder)).Append(",\r\n");
        json.Append("  \"sourceFolders\": [");
        var folders = config.SourceFolders ?? new List<string>();
        if (folders.Count > 0)
        {
            json.Append("\r\n");
            for (var i = 0; i < folders.Count; i++)
            {
                json.Append("    ").Append(Quote(folders[i]));
                if (i < folders.Count - 1)
                {
                    json.Append(',');
                }
                json.Append("\r\n");
            }
            json.Append("  ");
        }
        json.Append("],\r\n");
        json.Append("  \"backupFolder\": ").Append(Quote(config.BackupFolder)).Append(",\r\n");
        json.Append("  \"winRarPath\": ").Append(Quote(config.WinRarPath)).Append(",\r\n");
        json.Append("  \"lastBackupLocal\": ").Append(Quote(config.LastBackupLocal)).Append(",\r\n");
        json.Append("  \"lastRestoreLocal\": ").Append(Quote(config.LastRestoreLocal)).Append("\r\n");
        json.Append("}\r\n");
        WriteAtomic(settingsFile, json.ToString());
    }

    public static void UpdateLastBackup(AppConfig config, string settingsFile)
    {
        try
        {
            config.LastBackupLocal = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            SaveSettings(config, settingsFile);
        }
        catch
        {
        }
    }

    public static void UpdateLastRestore(AppConfig config, string settingsFile)
    {
        try
        {
            config.LastRestoreLocal = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            SaveSettings(config, settingsFile);
        }
        catch
        {
        }
    }

    public static void RemoveBootstrap()
    {
        try
        {
            if (Directory.Exists(BootstrapFolder))
            {
                Directory.Delete(BootstrapFolder, true);
            }
        }
        catch
        {
        }
    }

    private sealed class ArchiveFolder
    {
        public int Count;
        public DateTime Newest;
    }

    private sealed class DiscoveryScanner
    {
        private static readonly HashSet<string> IgnoredNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "AppData", "Application Data", "Local Settings", "Windows", "Program Files",
            "Program Files (x86)", "ProgramData", "System Volume Information", "$Recycle.Bin",
            "$RECYCLE.BIN", "Recovery", "PerfLogs", "node_modules", ".git", ".svn", ".hg",
            ".vs", "obj", "bin", "Temp", "temp", "tmp", "Cache", "cache", ".cache",
            ".nuget", ".npm", ".gradle", ".m2", ".vscode", ".android", ".cargo",
            ".rustup", ".docker", ".conda", ".venv", "venv", "__pycache__", "packages"
        };

        private readonly Stopwatch _watch = Stopwatch.StartNew();
        private int _visited;

        public readonly List<string> SettingsFiles = new List<string>();
        public readonly Dictionary<string, ArchiveFolder> ArchiveFolders = new Dictionary<string, ArchiveFolder>(StringComparer.OrdinalIgnoreCase);

        public bool Expired
        {
            get { return _watch.ElapsedMilliseconds > 2500 || _visited >= 3000; }
        }

        public void Scan(string directory, int depth, int maxDepth)
        {
            if (Expired || string.IsNullOrEmpty(directory))
            {
                return;
            }
            try
            {
                if (!Directory.Exists(directory))
                {
                    return;
                }
            }
            catch
            {
                return;
            }

            _visited++;

            try
            {
                foreach (var file in Directory.EnumerateFiles(directory, AppInfo.SettingsFileName, SearchOption.TopDirectoryOnly))
                {
                    SettingsFiles.Add(file);
                }
            }
            catch
            {
            }

            try
            {
                var count = 0;
                var newest = DateTime.MinValue;
                foreach (var file in Directory.EnumerateFiles(directory, AppInfo.ArchivePrefix + "*.rar", SearchOption.TopDirectoryOnly))
                {
                    count++;
                    try
                    {
                        var stamp = File.GetLastWriteTimeUtc(file);
                        if (stamp > newest) { newest = stamp; }
                    }
                    catch
                    {
                    }
                }
                if (count > 0)
                {
                    ArchiveFolder existing;
                    if (!ArchiveFolders.TryGetValue(directory, out existing) || existing.Newest < newest)
                    {
                        ArchiveFolders[directory] = new ArchiveFolder { Count = count, Newest = newest };
                    }
                }
            }
            catch
            {
            }

            if (depth >= maxDepth)
            {
                return;
            }

            try
            {
                foreach (var child in Directory.EnumerateDirectories(directory))
                {
                    if (Expired)
                    {
                        return;
                    }
                    try
                    {
                        if (IgnoredNames.Contains(Path.GetFileName(child)))
                        {
                            continue;
                        }
                        var attributes = File.GetAttributes(child);
                        if ((attributes & (FileAttributes.System | FileAttributes.ReparsePoint)) != 0)
                        {
                            continue;
                        }
                        Scan(child, depth + 1, maxDepth);
                    }
                    catch
                    {
                    }
                }
            }
            catch
            {
            }
        }
    }

    private static void WriteAtomic(string path, string content)
    {
        var temp = path + ".tmp";
        File.WriteAllText(temp, content, new UTF8Encoding(false));
        try
        {
            if (File.Exists(path))
            {
                try
                {
                    File.Replace(temp, path, null);
                }
                catch
                {
                    File.Delete(path);
                    File.Move(temp, path);
                }
            }
            else
            {
                File.Move(temp, path);
            }
        }
        finally
        {
            try { if (File.Exists(temp)) { File.Delete(temp); } } catch { }
        }
    }

    private static string Quote(string value)
    {
        if (value == null)
        {
            return "null";
        }
        var builder = new StringBuilder(value.Length + 8);
        builder.Append('"');
        foreach (var ch in value)
        {
            switch (ch)
            {
                case '"': builder.Append("\\\""); break;
                case '\\': builder.Append("\\\\"); break;
                case '\b': builder.Append("\\b"); break;
                case '\f': builder.Append("\\f"); break;
                case '\n': builder.Append("\\n"); break;
                case '\r': builder.Append("\\r"); break;
                case '\t': builder.Append("\\t"); break;
                default:
                    if (ch < ' ')
                    {
                        builder.Append("\\u").Append(((int)ch).ToString("x4"));
                    }
                    else
                    {
                        builder.Append(ch);
                    }
                    break;
            }
        }
        builder.Append('"');
        return builder.ToString();
    }
}
