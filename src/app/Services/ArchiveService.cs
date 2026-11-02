using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace AutoBackup.Services;

public sealed class ArchiveInfo
{
    public string FullPath { get; set; }
    public string Name { get; set; }
    public string BackupFolder { get; set; }
    public long SizeBytes { get; set; }
    public DateTime Modified { get; set; }
    public string SizeText { get { return Formats.Size(SizeBytes); } }
    public string DateText { get { return Modified.ToString("MMM d, yyyy  HH:mm", CultureInfo.InvariantCulture); } }
}

public sealed class ArchiveScanResult
{
    public string Folder { get; set; }
    public List<ArchiveInfo> Archives { get; set; } = new List<ArchiveInfo>();
    public bool Available { get; set; }
    public string Error { get; set; }
    public long TotalBytes { get; set; }
}

public sealed class BackupResult
{
    public List<string> ArchivePaths { get; set; } = new List<string>();
    public long SizeBytes { get; set; }
    public TimeSpan Duration { get; set; }
    public bool Warning { get; set; }
    public string Output { get; set; }

    public string ArchivePath
    {
        get { return ArchivePaths.Count == 1 ? ArchivePaths[0] : null; }
    }
}

public sealed class RestoreResult
{
    public string TargetFolder { get; set; }
    public TimeSpan Duration { get; set; }
    public bool Warning { get; set; }
    public string Output { get; set; }
}

public static class Formats
{
    public static string Size(long bytes)
    {
        if (bytes < 1024)
        {
            return bytes.ToString(CultureInfo.InvariantCulture) + " B";
        }
        var kb = bytes / 1024.0;
        if (kb < 1024)
        {
            return kb.ToString("0.#", CultureInfo.InvariantCulture) + " KB";
        }
        var mb = kb / 1024.0;
        if (mb < 1024)
        {
            return mb.ToString("0.#", CultureInfo.InvariantCulture) + " MB";
        }
        var gb = mb / 1024.0;
        return gb.ToString("0.##", CultureInfo.InvariantCulture) + " GB";
    }

    public static string Duration(TimeSpan duration)
    {
        if (duration.TotalSeconds < 1)
        {
            return "less than a second";
        }
        if (duration.TotalMinutes < 1)
        {
            return Math.Round(duration.TotalSeconds).ToString(CultureInfo.InvariantCulture) + "s";
        }
        if (duration.TotalHours < 1)
        {
            return ((int)duration.TotalMinutes).ToString(CultureInfo.InvariantCulture) + "m " +
                duration.Seconds.ToString("00", CultureInfo.InvariantCulture) + "s";
        }
        return ((int)duration.TotalHours).ToString(CultureInfo.InvariantCulture) + "h " +
            duration.Minutes.ToString("00", CultureInfo.InvariantCulture) + "m";
    }

    public static string FriendlyDate(string value)
    {
        DateTime parsed;
        if (DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out parsed))
        {
            return parsed.ToString("MMM d, yyyy  HH:mm", CultureInfo.InvariantCulture);
        }
        return value;
    }
}

public static class ArchiveService
{
    private static readonly CultureInfo En = CultureInfo.InvariantCulture;

    public static List<BackupJob> GetJobs(AppConfig config)
    {
        var jobs = new List<BackupJob>();
        if (config == null)
        {
            return jobs;
        }
        if (config.Jobs != null && config.Jobs.Count > 0)
        {
            foreach (var job in config.Jobs)
            {
                if (job == null)
                {
                    continue;
                }
                var source = AppConfig.CleanPath(job.SourceFolder);
                if (string.IsNullOrEmpty(source))
                {
                    continue;
                }
                try
                {
                    source = NormalizedFullPath(source);
                }
                catch
                {
                }
                source = AppConfig.AdaptPathToCurrentUser(source);
                var backup = AppConfig.CleanPath(job.BackupFolder);
                if (!string.IsNullOrEmpty(backup))
                {
                    try
                    {
                        backup = NormalizedFullPath(backup);
                    }
                    catch
                    {
                    }
                    backup = AppConfig.AdaptPathToCurrentUser(backup);
                }
                var name = (job.Name ?? string.Empty).Trim();
                if (string.IsNullOrEmpty(name))
                {
                    name = SourceDisplayName(source);
                }
                jobs.Add(new BackupJob { Name = name, SourceFolder = source, BackupFolder = backup });
            }
        }
        if (jobs.Count == 0)
        {
            var backup = AppConfig.CleanPath(config.BackupFolder);
            if (!string.IsNullOrEmpty(backup))
            {
                try
                {
                    backup = NormalizedFullPath(backup);
                }
                catch
                {
                }
                backup = AppConfig.AdaptPathToCurrentUser(backup);
            }
            foreach (var source in LegacySources(config))
            {
                jobs.Add(new BackupJob { Name = SourceDisplayName(source), SourceFolder = source, BackupFolder = backup });
            }
        }
        return jobs;
    }

    private static List<string> LegacySources(AppConfig config)
    {
        var result = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        IEnumerable<string> raw = null;
        if (config.SourceFolders != null && config.SourceFolders.Count > 0)
        {
            raw = config.SourceFolders;
        }
        else if (!string.IsNullOrWhiteSpace(config.SourceFolder))
        {
            raw = new[] { config.SourceFolder };
        }
        if (raw == null)
        {
            return result;
        }
        foreach (var item in raw)
        {
            var cleaned = AppConfig.CleanPath(item);
            if (string.IsNullOrEmpty(cleaned))
            {
                continue;
            }
            try
            {
                cleaned = NormalizedFullPath(cleaned);
            }
            catch
            {
            }
            cleaned = AppConfig.AdaptPathToCurrentUser(cleaned);
            if (seen.Add(cleaned))
            {
                result.Add(cleaned);
            }
        }
        return result;
    }

    public static List<string> GetSourceFolders(AppConfig config)
    {
        var result = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var job in GetJobs(config))
        {
            if (seen.Add(job.SourceFolder))
            {
                result.Add(job.SourceFolder);
            }
        }
        return result;
    }

    public static List<string> GetBackupFolders(AppConfig config)
    {
        var result = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var job in GetJobs(config))
        {
            if (!string.IsNullOrEmpty(job.BackupFolder) && seen.Add(job.BackupFolder))
            {
                result.Add(job.BackupFolder);
            }
        }
        if (result.Count == 0 && config != null)
        {
            var backup = AppConfig.CleanPath(config.BackupFolder);
            if (!string.IsNullOrEmpty(backup))
            {
                try
                {
                    backup = NormalizedFullPath(backup);
                }
                catch
                {
                }
                backup = AppConfig.AdaptPathToCurrentUser(backup);
                result.Add(backup);
            }
        }
        return result;
    }

    public static string ValidateForBackup(AppConfig config)
    {
        if (config == null)
        {
            return "The settings could not be loaded.";
        }
        return ValidateForBackup(GetJobs(config), config.WinRarPath);
    }

    public static string ValidateForBackup(IList<BackupJob> jobs, string winRarPath)
    {
        if (jobs == null || jobs.Count == 0)
        {
            return "Add at least one backup with a name, main folder, and backup folder.";
        }
        foreach (var job in jobs)
        {
            var label = string.IsNullOrWhiteSpace(job.Name) ? "a backup" : "\"" + job.Name + "\"";
            var source = AppConfig.CleanPath(job.SourceFolder);
            var backup = AppConfig.CleanPath(job.BackupFolder);
            if (string.IsNullOrEmpty(source))
            {
                return "The main folder is missing for " + label + ".";
            }
            if (string.IsNullOrEmpty(backup))
            {
                return "The backup folder is missing for " + label + ".";
            }
            if (!Directory.Exists(source))
            {
                return "The main folder for " + label + " was not found:\n" + source;
            }
            try
            {
                using (var entries = Directory.EnumerateFileSystemEntries(source).GetEnumerator())
                {
                    entries.MoveNext();
                }
            }
            catch (Exception ex)
            {
                return "The main folder for " + label + " cannot be read:\n" + source + "\n\n" + ex.Message;
            }
            if (PathsEqual(source, backup))
            {
                return "The backup folder must be different from the main folder for " + label + ".";
            }
            var root = GetRoot(backup);
            if (!string.IsNullOrEmpty(root) && !Directory.Exists(root))
            {
                return "The backup drive is not available for " + label + ":\n" + root + "\n\nReconnect the drive and try again.";
            }
        }
        if (!WinRarLocator.IsValid(winRarPath))
        {
            return "WinRAR was not found. Open Settings and select Rar.exe (usually inside C:\\Program Files\\WinRAR).";
        }
        return null;
    }

    public static void EnsureBackupFolder(string backupFolder)
    {
        try
        {
            Directory.CreateDirectory(backupFolder);
        }
        catch (Exception ex)
        {
            throw new AutoBackupException("The backup folder could not be created:\n" + backupFolder + "\n\n" + ex.Message, ex);
        }
    }

    public static Task<BackupResult> CreateBackupAsync(
        AppConfig config,
        Action<double, string> progress,
        CancellationToken token)
    {
        if (config == null)
        {
            throw new AutoBackupException("The settings could not be loaded.");
        }
        return CreateBackupAsync(GetJobs(config), config.WinRarPath, progress, token);
    }

    public static async Task<BackupResult> CreateBackupAsync(
        IList<BackupJob> jobs,
        string winRarPath,
        Action<double, string> progress,
        CancellationToken token)
    {
        var error = ValidateForBackup(jobs, winRarPath);
        if (error != null)
        {
            throw new AutoBackupException(error);
        }

        var work = new List<BackupJob>();
        foreach (var job in jobs)
        {
            work.Add(new BackupJob
            {
                Name = job.Name,
                SourceFolder = Path.GetFullPath(AppConfig.CleanPath(job.SourceFolder)),
                BackupFolder = Path.GetFullPath(AppConfig.CleanPath(job.BackupFolder))
            });
        }
        foreach (var job in work)
        {
            EnsureBackupFolder(job.BackupFolder);
        }

        var tempRoot = Path.Combine(Path.GetTempPath(), "AutoBackup", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);
        var tempArchives = new List<string>();
        var archiveNames = new List<string>();
        var finalPaths = new List<string>();
        var warning = false;
        var output = new StringBuilder();
        var started = DateTime.UtcNow;

        try
        {
            for (var index = 0; index < work.Count; index++)
            {
                token.ThrowIfCancellationRequested();
                var job = work[index];
                var display = string.IsNullOrWhiteSpace(job.Name) ? SourceDisplayName(job.SourceFolder) : job.Name;
                var archiveName = BuildArchiveName(job.SourceFolder);
                var tempArchive = Path.Combine(tempRoot, index.ToString("D3", En) + "_" + archiveName);
                tempArchives.Add(tempArchive);
                archiveNames.Add(archiveName);

                var arguments = "a -r -m3 -ep1 -y " + Quote(tempArchive) + " *";
                var nested = NestedRelativePath(job.SourceFolder, job.BackupFolder);
                if (nested != null)
                {
                    arguments += " -x" + Quote(nested);
                }

                if (progress != null)
                {
                    var position = work.Count > 1
                        ? " (" + (index + 1).ToString(En) + "/" + work.Count.ToString(En) + ")"
                        : string.Empty;
                    progress(CreationPercent(index, 0, work.Count), "Preparing " + display + position + "...");
                }

                var rar = await RarRunner.RunAsync(
                    winRarPath,
                    arguments,
                    job.SourceFolder,
                    delegate(double percent, string status)
                    {
                        if (progress == null)
                        {
                            return;
                        }
                        var label = status;
                        if (!string.IsNullOrEmpty(label) && work.Count > 1)
                        {
                            label = "(" + (index + 1).ToString(En) + "/" + work.Count.ToString(En) + ") " + label;
                        }
                        progress(CreationPercent(index, percent, work.Count), label);
                    },
                    token).ConfigureAwait(true);

                warning |= rar.Warning;
                AppendOutput(output, rar.Output);

                if (!File.Exists(tempArchive))
                {
                    throw new AutoBackupException("WinRAR did not create an archive for \"" + display + "\".\n\n" + job.SourceFolder);
                }
                var tempInfo = new FileInfo(tempArchive);
                if (tempInfo.Length <= 0)
                {
                    throw new AutoBackupException("WinRAR created an empty archive for \"" + display + "\".\n\n" + job.SourceFolder);
                }
            }

            token.ThrowIfCancellationRequested();
            long totalSize = 0;
            for (var index = 0; index < tempArchives.Count; index++)
            {
                token.ThrowIfCancellationRequested();
                var job = work[index];
                var tempArchive = tempArchives[index];
                var tempInfo = new FileInfo(tempArchive);
                var destinationPath = EnsureUniquePath(job.BackupFolder, archiveNames[index]);
                if (progress != null)
                {
                    progress(SavingPercent(index, 0, tempArchives.Count), "Saving the archive for \"" + job.Name + "\"...");
                }

                await MoveArchiveAsync(tempArchive, destinationPath, tempInfo.Length, delegate(double percent, string status)
                {
                    if (progress != null)
                    {
                        progress(SavingPercent(index, percent, tempArchives.Count), status);
                    }
                }, token).ConfigureAwait(true);

                var resultInfo = new FileInfo(destinationPath);
                if (!resultInfo.Exists || resultInfo.Length != tempInfo.Length)
                {
                    throw new AutoBackupException("The archive could not be verified after copying it to the backup folder:\n" + destinationPath);
                }
                totalSize += resultInfo.Length;
                finalPaths.Add(destinationPath);
            }

            if (progress != null)
            {
                progress(100, null);
            }

            return new BackupResult
            {
                ArchivePaths = finalPaths,
                SizeBytes = totalSize,
                Duration = DateTime.UtcNow - started,
                Warning = warning,
                Output = output.ToString()
            };
        }
        catch
        {
            foreach (var path in finalPaths)
            {
                TryDeleteFile(path);
            }
            throw;
        }
        finally
        {
            foreach (var temp in tempArchives)
            {
                TryDeleteFile(temp);
            }
            TryDeleteDirectory(tempRoot);
        }
    }

    public static string ValidateRestoreTarget(AppConfig config, string targetFolder, string protectedSettingsPath = null)
    {
        var cleaned = AppConfig.CleanPath(targetFolder);
        if (string.IsNullOrEmpty(cleaned))
        {
            return "Choose a destination folder for the restore.";
        }
        string target;
        try
        {
            target = NormalizedFullPath(cleaned);
        }
        catch (Exception ex)
        {
            return "The destination folder path is not valid. " + ex.Message;
        }
        if (File.Exists(target))
        {
            return "The destination is a file. Choose a folder instead.";
        }

        var root = GetRoot(target);
        if (!string.IsNullOrEmpty(root) && PathsEqual(target, root))
        {
            return "A drive root cannot be used as a restore destination.";
        }

        var protectedFolders = new[]
        {
            Environment.GetFolderPath(Environment.SpecialFolder.Windows),
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)
        };
        foreach (var folder in protectedFolders)
        {
            if (!string.IsNullOrEmpty(folder) && PathsEqual(target, folder))
            {
                return "This protected folder cannot be used as a restore destination:\n" + folder;
            }
        }

        var appFolder = AppDomain.CurrentDomain.BaseDirectory;
        if (!string.IsNullOrEmpty(appFolder))
        {
            try
            {
                var app = NormalizedFullPath(appFolder);
                if (IsInside(app, target) || IsInside(target, app))
                {
                    return "The restore destination overlaps the Auto Backup installation folder:\n" + app;
                }
            }
            catch
            {
            }
        }

        try
        {
            var temp = NormalizedFullPath(Path.GetTempPath());
            if (IsInside(temp, target))
            {
                return "The restore destination cannot contain the Windows temporary folder.";
            }
        }
        catch
        {
        }

        if (config != null)
        {
            foreach (var backupFolder in GetBackupFolders(config))
            {
                if (PathsEqual(target, backupFolder) || IsInside(backupFolder, target) || IsInside(target, backupFolder))
                {
                    return "The restore destination overlaps a backup folder:\n" + backupFolder
                        + "\n\nChoose a different destination, or move the backup folder outside this folder.";
                }
            }
        }

        if (!string.IsNullOrWhiteSpace(protectedSettingsPath))
        {
            try
            {
                var settings = NormalizedFullPath(protectedSettingsPath);
                if (IsInside(settings, target))
                {
                    return "The shared settings file would be deleted by this restore:\n" + settings
                        + "\n\nChoose a different destination.";
                }
            }
            catch
            {
            }
        }

        try
        {
            if (Directory.Exists(target))
            {
                var attributes = File.GetAttributes(target);
                if ((attributes & FileAttributes.ReparsePoint) != 0)
                {
                    return "The destination is a link or junction. Choose a normal folder instead.";
                }
            }
        }
        catch
        {
        }
        return null;
    }

    public static async Task<RestoreResult> RestoreAsync(
        AppConfig config,
        string archivePath,
        string targetFolder,
        Action<double, string> progress,
        CancellationToken token)
    {
        if (!File.Exists(archivePath))
        {
            throw new AutoBackupException("The selected backup archive was not found:\n" + archivePath);
        }
        if (!WinRarLocator.IsValid(config.WinRarPath))
        {
            throw new AutoBackupException("WinRAR was not found. Open Settings and select Rar.exe.");
        }
        if (string.IsNullOrWhiteSpace(targetFolder))
        {
            throw new AutoBackupException("Choose a destination folder for the restore.");
        }

        var target = NormalizedFullPath(AppConfig.CleanPath(targetFolder));
        var validation = ValidateRestoreTarget(config, target);
        if (validation != null)
        {
            throw new AutoBackupException(validation);
        }

        var started = DateTime.UtcNow;
        var tempRoot = Path.Combine(Path.GetTempPath(), "AutoBackup", "restore-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);
        try
        {
            if (IsInside(tempRoot, target) || PathsEqual(tempRoot, target))
            {
                throw new AutoBackupException("The restore destination overlaps the update temporary folder. Choose another destination.");
            }

            if (progress != null)
            {
                progress(0, "Opening the archive in a temporary folder...");
            }

            var rar = await RarRunner.RunAsync(
                config.WinRarPath,
                "x -o+ -y -p- " + Quote(Path.GetFullPath(archivePath)),
                tempRoot,
                delegate(double percent, string status)
                {
                    if (progress != null)
                    {
                        progress(percent * 0.85, status);
                    }
                },
                token).ConfigureAwait(true);

            token.ThrowIfCancellationRequested();
            if (!Directory.EnumerateFileSystemEntries(tempRoot).Any())
            {
                throw new AutoBackupException("The archive contains no files. The destination folder was not changed.");
            }

            if (progress != null)
            {
                progress(85, "Replacing the destination folder contents...");
            }
            ReplaceFolderContents(target, tempRoot, delegate(double percent, string status)
            {
                if (progress != null)
                {
                    progress(85 + (percent * 0.15), status);
                }
            });

            if (progress != null)
            {
                progress(100, null);
            }

            return new RestoreResult
            {
                TargetFolder = target,
                Duration = DateTime.UtcNow - started,
                Warning = rar.Warning,
                Output = rar.Output
            };
        }
        finally
        {
            TryDeleteDirectory(tempRoot);
        }
    }

    private static void ReplaceFolderContents(string target, string extracted, Action<double, string> progress)
    {
        var trimmed = target.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var parent = Path.GetDirectoryName(trimmed);
        var name = Path.GetFileName(trimmed);
        if (string.IsNullOrEmpty(parent) || string.IsNullOrEmpty(name))
        {
            throw new AutoBackupException("The restore destination is not valid:\n" + target);
        }
        var oldPath = Path.Combine(parent, name + ".autobackup-old-"
            + DateTime.Now.ToString("yyyyMMdd-HHmmss-fff", En) + "-"
            + Guid.NewGuid().ToString("N").Substring(0, 4));
        var hasExisting = Directory.Exists(target) && Directory.EnumerateFileSystemEntries(target).Any();
        var renamed = false;

        try
        {
            if (hasExisting)
            {
                Directory.Move(target, oldPath);
                renamed = true;
            }
            Directory.CreateDirectory(target);
            CopyDirectoryContents(extracted, target, progress);
        }
        catch (Exception ex)
        {
            var rollback = TryRollback(target, renamed ? oldPath : null);
            throw new AutoBackupException("The restore could not replace the destination folder. " + rollback
                + "\n\n" + ex.Message, ex);
        }

        if (renamed)
        {
            DeleteDirectoryBestEffort(oldPath);
        }
    }

    private static string TryRollback(string target, string oldPath)
    {
        if (string.IsNullOrEmpty(oldPath))
        {
            return "The previous contents were kept.";
        }
        string error = null;
        try
        {
            if (Directory.Exists(target))
            {
                DeleteDirectoryBestEffort(target);
            }
        }
        catch (Exception ex)
        {
            error = ex.Message;
        }
        if (Directory.Exists(target))
        {
            return "The previous contents are saved at:\n" + oldPath;
        }
        try
        {
            Directory.Move(oldPath, target);
            return "The previous contents were restored.";
        }
        catch (Exception ex)
        {
            return "The previous contents are saved at:\n" + oldPath + "\n\n" + ex.Message;
        }
    }

    private static void CopyDirectoryContents(string source, string destination, Action<double, string> progress)
    {
        var directories = Directory.GetDirectories(source, "*", SearchOption.AllDirectories);
        foreach (var directory in directories)
        {
            var relative = directory.Substring(source.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            Directory.CreateDirectory(Path.Combine(destination, relative));
        }

        var files = Directory.GetFiles(source, "*", SearchOption.AllDirectories);
        long total = 0;
        foreach (var file in files)
        {
            try { total += new FileInfo(file).Length; } catch { }
        }
        long done = 0;
        foreach (var file in files)
        {
            var relative = file.Substring(source.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            var output = Path.Combine(destination, relative);
            var outputFolder = Path.GetDirectoryName(output);
            if (!string.IsNullOrEmpty(outputFolder))
            {
                Directory.CreateDirectory(outputFolder);
            }
            File.Copy(file, output, true);
            try
            {
                File.SetAttributes(output, File.GetAttributes(file));
                File.SetLastWriteTimeUtc(output, File.GetLastWriteTimeUtc(file));
            }
            catch
            {
            }
            long size = 0;
            try { size = new FileInfo(file).Length; } catch { }
            done += size;
            if (progress != null && total > 0)
            {
                progress(done * 100.0 / total, "Copying " + Path.GetFileName(file));
            }
        }
        if (progress != null)
        {
            progress(100, "Restore complete.");
        }
    }

    private static void DeleteDirectoryBestEffort(string path)
    {
        try
        {
            if (!Directory.Exists(path))
            {
                return;
            }
            foreach (var file in Directory.GetFiles(path, "*", SearchOption.AllDirectories))
            {
                try { File.SetAttributes(file, FileAttributes.Normal); } catch { }
            }
            Directory.Delete(path, true);
        }
        catch
        {
        }
    }

    private static bool IsInside(string candidate, string parent)
    {
        try
        {
            var item = NormalizedFullPath(candidate);
            var folder = NormalizedFullPath(parent);
            if (PathsEqual(item, folder))
            {
                return true;
            }
            var prefix = folder.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal)
                ? folder
                : folder + Path.DirectorySeparatorChar;
            return item.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    public static ArchiveScanResult ScanArchives(AppConfig config)
    {
        return ScanArchives(GetBackupFolders(config));
    }

    public static ArchiveScanResult ScanArchives(IEnumerable<string> folders)
    {
        var roots = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (folders != null)
        {
            foreach (var item in folders)
            {
                var cleaned = AppConfig.CleanPath(item);
                if (string.IsNullOrEmpty(cleaned))
                {
                    continue;
                }
                try
                {
                    cleaned = NormalizedFullPath(cleaned);
                }
                catch
                {
                }
                if (seen.Add(cleaned))
                {
                    roots.Add(cleaned);
                }
            }
        }

        var result = new ArchiveScanResult
        {
            Folder = roots.Count == 1 ? roots[0] : roots.Count + " backup folders"
        };
        if (roots.Count == 0)
        {
            result.Error = "No backup folder has been configured.";
            return result;
        }

        var list = new List<ArchiveInfo>();
        var available = false;
        foreach (var root in roots)
        {
            if (!Directory.Exists(root))
            {
                continue;
            }
            available = true;
            try
            {
                foreach (var file in Directory.EnumerateFiles(root, "*.rar", SearchOption.TopDirectoryOnly))
                {
                    var fileName = Path.GetFileName(file);
                    if (!fileName.StartsWith(AppInfo.ArchivePrefix, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }
                    var info = new FileInfo(file);
                    list.Add(new ArchiveInfo
                    {
                        FullPath = info.FullName,
                        Name = info.Name,
                        BackupFolder = root,
                        SizeBytes = info.Length,
                        Modified = info.LastWriteTime
                    });
                    result.TotalBytes += info.Length;
                }
            }
            catch
            {
            }
        }

        result.Available = available;
        if (!available)
        {
            result.Error = "The backup folders are not available.";
            return result;
        }
        result.Archives = list.OrderByDescending(a => a.Modified).ToList();
        return result;
    }

    public static ArchiveScanResult ScanArchives(string backupFolder)
    {
        var folder = AppConfig.CleanPath(backupFolder);
        var result = new ArchiveScanResult { Folder = folder };
        if (string.IsNullOrEmpty(folder))
        {
            result.Error = "No backup folder has been configured.";
            return result;
        }
        if (!Directory.Exists(folder))
        {
            result.Error = "The backup folder is not available.";
            return result;
        }

        try
        {
            var list = new List<ArchiveInfo>();
            foreach (var file in Directory.EnumerateFiles(folder, "*.rar", SearchOption.TopDirectoryOnly))
            {
                var fileName = Path.GetFileName(file);
                if (!fileName.StartsWith(AppInfo.ArchivePrefix, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }
                var info = new FileInfo(file);
                list.Add(new ArchiveInfo
                {
                    FullPath = info.FullName,
                    Name = info.Name,
                    BackupFolder = folder,
                    SizeBytes = info.Length,
                    Modified = info.LastWriteTime
                });
                result.TotalBytes += info.Length;
            }
            result.Archives = list.OrderByDescending(a => a.Modified).ToList();
            result.Available = true;
        }
        catch (Exception ex)
        {
            result.Error = ex.Message;
        }
        return result;
    }

    public static string BuildArchiveName(string sourceFolder)
    {
        var full = NormalizedFullPath(sourceFolder);
        var machine = Sanitize(Environment.MachineName, 24);
        var stamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss", En);
        return AppInfo.ArchivePrefix + machine + "_" + SourceFolderToken(full) + "_" + SourceHash(full) + "_" + stamp + ".rar";
    }

    public static bool MatchesSource(string archiveName, string source)
    {
        if (string.IsNullOrEmpty(archiveName) || string.IsNullOrEmpty(source))
        {
            return false;
        }
        return MatchesSourceHash(archiveName, source) || MatchesSourceName(archiveName, source);
    }

    private static bool MatchesSourceHash(string archiveName, string source)
    {
        try
        {
            var full = NormalizedFullPath(source);
            var token = SourceFolderToken(full) + "_" + SourceHash(full);
            return archiveName.IndexOf("_" + token + "_", StringComparison.OrdinalIgnoreCase) >= 0;
        }
        catch
        {
            return false;
        }
    }

    private static bool MatchesSourceName(string archiveName, string source)
    {
        try
        {
            var token = SourceFolderToken(NormalizedFullPath(source));
            return archiveName.IndexOf("_" + token + "_", StringComparison.OrdinalIgnoreCase) >= 0;
        }
        catch
        {
            return false;
        }
    }

    public static string TryFindSourceForArchive(AppConfig config, string archiveName)
    {
        var sources = GetSourceFolders(config);
        if (sources.Count == 0 || string.IsNullOrEmpty(archiveName))
        {
            return null;
        }
        foreach (var source in sources)
        {
            if (MatchesSourceHash(archiveName, source))
            {
                return source;
            }
        }
        foreach (var source in sources)
        {
            if (MatchesSourceName(archiveName, source))
            {
                return source;
            }
        }
        return null;
    }

    public static string FindSourceForArchive(AppConfig config, string archiveName)
    {
        var source = TryFindSourceForArchive(config, archiveName);
        if (!string.IsNullOrEmpty(source))
        {
            return source;
        }
        var sources = GetSourceFolders(config);
        return sources.Count > 0 ? sources[0] : null;
    }

    private static string NormalizedFullPath(string path)
    {
        var full = Path.GetFullPath(path);
        var root = Path.GetPathRoot(full);
        if (!string.IsNullOrEmpty(root) && string.Equals(full, root, StringComparison.OrdinalIgnoreCase))
        {
            return full;
        }
        return full.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
    }

    private static string SourceFolderToken(string fullPath)
    {
        var folderName = Path.GetFileName(fullPath);
        if (string.IsNullOrEmpty(folderName))
        {
            var root = Path.GetPathRoot(fullPath) ?? "Drive";
            folderName = "Drive-" + root.Replace(":", string.Empty).Replace("\\", string.Empty).Replace("/", string.Empty);
        }
        return Sanitize(folderName, 38);
    }

    private static string SourceHash(string fullPath)
    {
        var value = fullPath.ToLowerInvariant();
        uint hash = 2166136261;
        foreach (var ch in value)
        {
            hash ^= ch;
            hash *= 16777619;
        }
        return hash.ToString("x4", En);
    }

    private static string SourceDisplayName(string source)
    {
        try
        {
            var full = NormalizedFullPath(source);
            var name = Path.GetFileName(full);
            if (!string.IsNullOrEmpty(name))
            {
                return name;
            }
            var root = Path.GetPathRoot(full);
            return string.IsNullOrEmpty(root) ? "Folder" : root;
        }
        catch
        {
            return "Folder";
        }
    }

    private static double CreationPercent(int index, double percent, int count)
    {
        var total = Math.Max(1, count);
        var value = Math.Max(0, Math.Min(100, percent));
        return ((index + (value / 100.0)) / total) * 85.0;
    }

    private static double SavingPercent(int index, double percent, int count)
    {
        var total = Math.Max(1, count);
        var value = Math.Max(0, Math.Min(100, percent));
        return 85.0 + ((index + (value / 100.0)) / total) * 15.0;
    }

    private static void AppendOutput(StringBuilder builder, string text)
    {
        if (string.IsNullOrEmpty(text) || builder.Length > 200000)
        {
            return;
        }
        if (builder.Length > 0)
        {
            builder.AppendLine();
        }
        builder.Append(text);
    }

    private static string Sanitize(string value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "Folder";
        }
        var builder = new StringBuilder(value.Length);
        foreach (var ch in value.Trim())
        {
            if (char.IsLetterOrDigit(ch) || ch == '-' || ch == '_' || ch == '.')
            {
                builder.Append(ch);
            }
            else if (char.IsWhiteSpace(ch) || ch == '&' || ch == '+' || ch == '(' || ch == ')' || ch == '#')
            {
                if (builder.Length > 0 && builder[builder.Length - 1] != '-')
                {
                    builder.Append('-');
                }
            }
        }
        var clean = builder.ToString().Trim('-', '.', ' ');
        if (clean.Length > maxLength)
        {
            clean = clean.Substring(0, maxLength).TrimEnd('-', '.', ' ');
        }
        return string.IsNullOrEmpty(clean) ? "Folder" : clean;
    }

    private static string EnsureUniquePath(string folder, string fileName)
    {
        var path = Path.Combine(folder, fileName);
        if (!File.Exists(path))
        {
            return path;
        }
        var baseName = Path.GetFileNameWithoutExtension(fileName);
        for (var i = 2; i < 1000; i++)
        {
            var candidate = Path.Combine(folder, baseName + "_" + i.ToString(En) + ".rar");
            if (!File.Exists(candidate))
            {
                return candidate;
            }
        }
        throw new AutoBackupException("Could not find an available file name in the backup folder.");
    }

    private static async Task MoveArchiveAsync(
        string sourcePath,
        string destinationPath,
        long length,
        Action<double, string> progress,
        CancellationToken token)
    {
        var sourceRoot = GetRoot(sourcePath);
        var destinationRoot = GetRoot(destinationPath);
        if (string.Equals(sourceRoot, destinationRoot, StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                File.Move(sourcePath, destinationPath);
                if (progress != null) { progress(100, "Archive saved."); }
                return;
            }
            catch (IOException)
            {
            }
        }

        try
        {
            using (var input = new FileStream(sourcePath, FileMode.Open, FileAccess.Read, FileShare.Read, 1048576, FileOptions.SequentialScan))
            using (var output = new FileStream(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None, 1048576, FileOptions.SequentialScan))
            {
                var buffer = new byte[1048576];
                long total = 0;
                var lastReport = DateTime.UtcNow;
                while (true)
                {
                    token.ThrowIfCancellationRequested();
                    var read = await input.ReadAsync(buffer, 0, buffer.Length, token).ConfigureAwait(true);
                    if (read <= 0)
                    {
                        break;
                    }
                    await output.WriteAsync(buffer, 0, read, token).ConfigureAwait(true);
                    total += read;
                    if (progress != null && (DateTime.UtcNow - lastReport).TotalMilliseconds > 80)
                    {
                        lastReport = DateTime.UtcNow;
                        progress(length > 0 ? (total * 100.0 / length) : 100, "Copying the archive...");
                    }
                }
                output.Flush();
            }
        }
        catch
        {
            TryDeleteFile(destinationPath);
            throw;
        }

        try { File.Delete(sourcePath); } catch { }
        if (progress != null) { progress(100, "Archive saved."); }
    }

    private static string NestedRelativePath(string source, string candidate)
    {
        var sourceFull = Path.GetFullPath(source).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var candidateFull = Path.GetFullPath(candidate).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var prefix = sourceFull + Path.DirectorySeparatorChar;
        if (candidateFull.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            return candidateFull.Substring(prefix.Length);
        }
        return null;
    }

    private static bool PathsEqual(string first, string second)
    {
        try
        {
            return string.Equals(
                Path.GetFullPath(first).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
                Path.GetFullPath(second).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
                StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    private static string GetRoot(string path)
    {
        try
        {
            return Path.GetPathRoot(Path.GetFullPath(path));
        }
        catch
        {
            return null;
        }
    }

    private static string Quote(string value)
    {
        return "\"" + value + "\"";
    }

    private static void TryDeleteFile(string path)
    {
        try { if (File.Exists(path)) { File.Delete(path); } } catch { }
    }

    private static void TryDeleteDirectory(string path)
    {
        try { if (Directory.Exists(path)) { Directory.Delete(path, true); } } catch { }
    }
}
