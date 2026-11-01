using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Win32;

namespace AutoBackup.Services;

public static class WinRarLocator
{
    public static string Detect()
    {
        foreach (var candidate in Candidates())
        {
            var normalized = Normalize(candidate);
            if (IsValid(normalized))
            {
                return normalized;
            }
        }
        return null;
    }

    public static string Normalize(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }
        var cleaned = AppConfig.CleanPath(path);
        try
        {
            if (Directory.Exists(cleaned))
            {
                var inside = Path.Combine(cleaned, "Rar.exe");
                if (File.Exists(inside)) { return inside; }
                var winrarInside = Path.Combine(cleaned, "WinRAR.exe");
                if (File.Exists(winrarInside)) { return Path.Combine(cleaned, "Rar.exe"); }
                return cleaned;
            }
            var fileName = Path.GetFileName(cleaned);
            if (string.Equals(fileName, "WinRAR.exe", StringComparison.OrdinalIgnoreCase))
            {
                var dir = Path.GetDirectoryName(cleaned);
                if (!string.IsNullOrEmpty(dir))
                {
                    var rar = Path.Combine(dir, "Rar.exe");
                    if (File.Exists(rar)) { return rar; }
                }
            }
        }
        catch
        {
        }
        return cleaned;
    }

    public static bool IsValid(string path)
    {
        try
        {
            return !string.IsNullOrWhiteSpace(path)
                && File.Exists(path)
                && string.Equals(Path.GetFileName(path), "Rar.exe", StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    private static IEnumerable<string> Candidates()
    {
        var results = new List<string>();

        foreach (var view in new[] { RegistryView.Registry64, RegistryView.Registry32 })
        {
            try
            {
                using (var baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, view))
                using (var key = baseKey.OpenSubKey(@"SOFTWARE\WinRAR"))
                {
                    if (key != null)
                    {
                        var exe = key.GetValue("exe64") as string;
                        if (string.IsNullOrEmpty(exe)) { exe = key.GetValue("exe32") as string; }
                        if (!string.IsNullOrEmpty(exe)) { results.Add(exe); }
                    }
                }
            }
            catch
            {
            }
            try
            {
                using (var baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, view))
                using (var key = baseKey.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths\WinRAR.exe"))
                {
                    var exe = key == null ? null : key.GetValue(string.Empty) as string;
                    if (!string.IsNullOrEmpty(exe)) { results.Add(exe); }
                }
            }
            catch
            {
            }
        }

        var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        var programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
        if (!string.IsNullOrEmpty(programFiles)) { results.Add(Path.Combine(programFiles, "WinRAR", "Rar.exe")); }
        if (!string.IsNullOrEmpty(programFilesX86)) { results.Add(Path.Combine(programFilesX86, "WinRAR", "Rar.exe")); }

        try
        {
            var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            if (!string.IsNullOrEmpty(local)) { results.Add(Path.Combine(local, "Programs", "WinRAR", "Rar.exe")); }
        }
        catch
        {
        }

        return results;
    }
}
