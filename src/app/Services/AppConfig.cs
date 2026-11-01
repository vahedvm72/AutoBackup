using System;
using System.Collections.Generic;

namespace AutoBackup.Services;

public sealed class BackupJob
{
    public string Name { get; set; } = string.Empty;
    public string SourceFolder { get; set; } = string.Empty;
    public string BackupFolder { get; set; } = string.Empty;

    public BackupJob Clone()
    {
        return new BackupJob
        {
            Name = Name,
            SourceFolder = SourceFolder,
            BackupFolder = BackupFolder
        };
    }
}

public sealed class AppConfig
{
    public int Version { get; set; } = 1;
    public string SourceFolder { get; set; } = string.Empty;
    public List<string> SourceFolders { get; set; } = new List<string>();
    public string BackupFolder { get; set; } = string.Empty;
    public List<BackupJob> Jobs { get; set; } = new List<BackupJob>();
    public string WinRarPath { get; set; } = string.Empty;
    public string LastBackupLocal { get; set; } = string.Empty;
    public string LastRestoreLocal { get; set; } = string.Empty;

    public AppConfig Clone()
    {
        var jobs = new List<BackupJob>();
        if (Jobs != null)
        {
            foreach (var job in Jobs)
            {
                if (job != null)
                {
                    jobs.Add(job.Clone());
                }
            }
        }
        return new AppConfig
        {
            Version = Version,
            SourceFolder = SourceFolder,
            SourceFolders = new List<string>(SourceFolders ?? new List<string>()),
            BackupFolder = BackupFolder,
            Jobs = jobs,
            WinRarPath = WinRarPath,
            LastBackupLocal = LastBackupLocal,
            LastRestoreLocal = LastRestoreLocal
        };
    }

    public void Normalize()
    {
        Version = 1;

        var jobs = new List<BackupJob>();
        if (Jobs != null)
        {
            foreach (var job in Jobs)
            {
                if (job == null)
                {
                    continue;
                }
                var source = CanonicalPath(CleanPath(job.SourceFolder));
                if (string.IsNullOrEmpty(source))
                {
                    continue;
                }
                jobs.Add(new BackupJob
                {
                    Name = (job.Name ?? string.Empty).Trim(),
                    SourceFolder = source,
                    BackupFolder = CanonicalPath(CleanPath(job.BackupFolder))
                });
            }
        }

        if (jobs.Count == 0)
        {
            var legacyBackup = CanonicalPath(CleanPath(BackupFolder));
            var legacySources = new List<string>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (SourceFolders != null)
            {
                foreach (var folder in SourceFolders)
                {
                    var cleaned = CanonicalPath(CleanPath(folder));
                    if (!string.IsNullOrEmpty(cleaned) && seen.Add(cleaned))
                    {
                        legacySources.Add(cleaned);
                    }
                }
            }
            var legacy = CanonicalPath(CleanPath(SourceFolder));
            if (legacySources.Count == 0 && !string.IsNullOrEmpty(legacy))
            {
                legacySources.Add(legacy);
            }
            foreach (var source in legacySources)
            {
                jobs.Add(new BackupJob
                {
                    Name = string.Empty,
                    SourceFolder = source,
                    BackupFolder = legacyBackup
                });
            }
        }

        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var job in jobs)
        {
            var baseName = string.IsNullOrEmpty(job.Name) ? FolderLabel(job.SourceFolder) : job.Name;
            var name = baseName;
            var suffix = 2;
            while (!names.Add(name))
            {
                name = baseName + " (" + suffix.ToString() + ")";
                suffix++;
            }
            job.Name = name;
        }

        Jobs = jobs;

        var sources = new List<string>();
        var sourceSeen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var job in jobs)
        {
            if (sourceSeen.Add(job.SourceFolder))
            {
                sources.Add(job.SourceFolder);
            }
        }
        SourceFolders = sources;
        SourceFolder = sources.Count > 0 ? sources[0] : string.Empty;
        BackupFolder = jobs.Count > 0 ? jobs[0].BackupFolder : CanonicalPath(CleanPath(BackupFolder));
        WinRarPath = CleanPath(WinRarPath);
        LastBackupLocal = LastBackupLocal ?? string.Empty;
        LastRestoreLocal = LastRestoreLocal ?? string.Empty;
    }

    private static string FolderLabel(string path)
    {
        try
        {
            var full = path.TrimEnd('\\');
            var name = System.IO.Path.GetFileName(full);
            if (!string.IsNullOrEmpty(name))
            {
                return name;
            }
            var root = System.IO.Path.GetPathRoot(full);
            if (!string.IsNullOrEmpty(root))
            {
                return "Drive " + root.Replace(":", string.Empty).Replace("\\", string.Empty).Replace("/", string.Empty);
            }
        }
        catch
        {
        }
        return "Backup";
    }

    private static string CanonicalPath(string value)
    {
        try
        {
            var full = System.IO.Path.GetFullPath(value);
            var root = System.IO.Path.GetPathRoot(full);
            if (!string.IsNullOrEmpty(root) && full.Length > root.Length)
            {
                full = full.TrimEnd(System.IO.Path.DirectorySeparatorChar, System.IO.Path.AltDirectorySeparatorChar);
            }
            return full;
        }
        catch
        {
            return value;
        }
    }

    public static string CleanPath(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }
        var path = value.Trim().Trim('"').Trim();
        if (path.Length >= 2 && path[0] == '\'' && path[path.Length - 1] == '\'')
        {
            path = path.Substring(1, path.Length - 2).Trim();
        }
        return path;
    }
}

public sealed class BootstrapConfig
{
    public string SettingsFile { get; set; } = string.Empty;
}
