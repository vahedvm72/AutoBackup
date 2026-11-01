using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using Microsoft.Win32;

namespace AutoBackup.Setup;

public sealed class InstallProgress
{
    public int Percent { get; set; }
    public string Message { get; set; }
}

public static class InstallInfo
{
    public const string AppResourceName = "AutoBackup.App.exe";
    public const string UninstallerResourceName = "AutoBackup.Uninstall.exe";

    public static string DefaultInstallFolder
    {
        get
        {
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "AutoBackup");
        }
    }

    public static string AppPath(string installFolder)
    {
        return Path.Combine(installFolder, AppInfo.ExeName);
    }

    public static string UninstallerPath(string installFolder)
    {
        return Path.Combine(installFolder, AppInfo.UninstallerName);
    }

    public static string StartMenuPrograms
    {
        get
        {
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), @"Microsoft\Windows\Start Menu\Programs");
        }
    }

    public static string StartMenuShortcut
    {
        get { return Path.Combine(StartMenuPrograms, AppInfo.Name + ".lnk"); }
    }

    public static string DesktopShortcut
    {
        get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), AppInfo.Name + ".lnk"); }
    }
}

public static class InstallCommon
{
    public static void Install(string installFolder, bool desktopShortcut, bool startMenuShortcut, IProgress<InstallProgress> progress)
    {
        if (string.IsNullOrWhiteSpace(installFolder))
        {
            throw new ArgumentException("The install folder is required.", "installFolder");
        }
        installFolder = Path.GetFullPath(installFolder);
        Directory.CreateDirectory(installFolder);

        Report(progress, 4, "Preparing the installation...");
        var assembly = Assembly.GetExecutingAssembly();
        var appPath = InstallInfo.AppPath(installFolder);
        var uninstallerPath = InstallInfo.UninstallerPath(installFolder);

        Report(progress, 12, "Installing Auto Backup...");
        WritePayload(assembly, InstallInfo.AppResourceName, appPath);
        Report(progress, 46, "Installing the uninstaller...");
        WritePayload(assembly, InstallInfo.UninstallerResourceName, uninstallerPath);

        Report(progress, 62, "Creating shortcuts...");
        if (startMenuShortcut)
        {
            CreateShortcut(InstallInfo.StartMenuShortcut, appPath, installFolder, AppInfo.Name + " - automatic folder backups");
        }
        if (desktopShortcut)
        {
            CreateShortcut(InstallInfo.DesktopShortcut, appPath, installFolder, AppInfo.Name + " - automatic folder backups");
        }

        Report(progress, 82, "Registering with Windows...");
        RegisterUninstall(installFolder);
        Report(progress, 100, "Installation complete.");
    }

    public static void RegisterUninstall(string installFolder)
    {
        var appPath = InstallInfo.AppPath(installFolder);
        var uninstaller = InstallInfo.UninstallerPath(installFolder);
        long size = 0;
        try
        {
            foreach (var file in Directory.GetFiles(installFolder))
            {
                size += new FileInfo(file).Length;
            }
        }
        catch
        {
        }

        using (var key = Registry.CurrentUser.CreateSubKey(AppInfo.UninstallRegistryKey))
        {
            key.SetValue("DisplayName", AppInfo.Name);
            key.SetValue("DisplayVersion", AppInfo.Version);
            key.SetValue("Publisher", AppInfo.Name);
            key.SetValue("InstallLocation", installFolder);
            key.SetValue("DisplayIcon", "\"" + appPath + "\",0");
            key.SetValue("UninstallString", "\"" + uninstaller + "\"");
            key.SetValue("QuietUninstallString", "\"" + uninstaller + "\" --quiet");
            key.SetValue("NoModify", 1, RegistryValueKind.DWord);
            key.SetValue("NoRepair", 1, RegistryValueKind.DWord);
            key.SetValue("EstimatedSize", (int)Math.Max(1, size / 1024), RegistryValueKind.DWord);
            key.SetValue("InstallDate", DateTime.Now.ToString("yyyyMMdd"));
        }

        using (var key = Registry.CurrentUser.CreateSubKey(AppInfo.AppRegistryKey))
        {
            key.SetValue("InstallLocation", installFolder);
            key.SetValue("Version", AppInfo.Version);
        }
    }

    public static void UnregisterUninstall()
    {
        TryDeleteKey(AppInfo.UninstallRegistryKey);
        TryDeleteKey(AppInfo.AppRegistryKey);
    }

    public static string GetInstalledLocation()
    {
        try
        {
            using (var key = Registry.CurrentUser.OpenSubKey(AppInfo.UninstallRegistryKey))
            {
                var location = key == null ? null : key.GetValue("InstallLocation") as string;
                return string.IsNullOrWhiteSpace(location) ? null : location;
            }
        }
        catch
        {
            return null;
        }
    }

    public static string GetInstalledVersion()
    {
        try
        {
            using (var key = Registry.CurrentUser.OpenSubKey(AppInfo.UninstallRegistryKey))
            {
                return key == null ? null : key.GetValue("DisplayVersion") as string;
            }
        }
        catch
        {
            return null;
        }
    }

    public static bool IsInstalled(out string installFolder)
    {
        installFolder = null;
        var location = GetInstalledLocation();
        if (string.IsNullOrWhiteSpace(location))
        {
            return false;
        }
        if (!File.Exists(InstallInfo.AppPath(location)))
        {
            return false;
        }
        installFolder = location;
        return true;
    }

    public static void DeleteShortcuts()
    {
        TryDeleteFile(InstallInfo.StartMenuShortcut);
        TryDeleteFile(InstallInfo.DesktopShortcut);
    }

    public static Process[] GetRunningApps()
    {
        try
        {
            var name = Path.GetFileNameWithoutExtension(AppInfo.ExeName);
            return Process.GetProcessesByName(name);
        }
        catch
        {
            return new Process[0];
        }
    }

    public static void CloseRunningApps()
    {
        var apps = GetRunningApps();
        foreach (var app in apps)
        {
            try
            {
                app.CloseMainWindow();
            }
            catch
            {
            }
        }
        for (var attempt = 0; attempt < 15; attempt++)
        {
            var alive = false;
            foreach (var app in apps)
            {
                try
                {
                    if (!app.HasExited)
                    {
                        alive = true;
                        break;
                    }
                }
                catch
                {
                }
            }
            if (!alive)
            {
                break;
            }
            Thread.Sleep(200);
        }
        foreach (var app in apps)
        {
            try
            {
                if (!app.HasExited)
                {
                    app.Kill();
                }
            }
            catch
            {
            }
            try
            {
                app.Dispose();
            }
            catch
            {
            }
        }
    }

    public static void RemoveLocalPointer()
    {
        try
        {
            var folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), AppInfo.BootstrapFolderName);
            if (Directory.Exists(folder))
            {
                Directory.Delete(folder, true);
            }
        }
        catch
        {
        }
    }

    public static void CreateShortcut(string shortcutPath, string targetPath, string workingDirectory, string description)
    {
        var directory = Path.GetDirectoryName(shortcutPath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        object instance = null;
        try
        {
            instance = new ShellLink();
            var link = (IShellLinkW)instance;
            link.SetPath(targetPath);
            link.SetWorkingDirectory(workingDirectory);
            link.SetDescription(description);
            link.SetIconLocation(targetPath, 0);
            var persist = (IPersistFile)instance;
            var result = persist.Save(shortcutPath, true);
            if (result != 0)
            {
                Marshal.ThrowExceptionForHR(result);
            }
        }
        finally
        {
            if (instance != null && Marshal.IsComObject(instance))
            {
                Marshal.ReleaseComObject(instance);
            }
        }
    }

    private static void WritePayload(Assembly assembly, string resourceName, string destination)
    {
        using (var stream = assembly.GetManifestResourceStream(resourceName))
        {
            if (stream == null)
            {
                throw new InvalidOperationException("The installer payload '" + resourceName + "' is missing from this file.");
            }
            var temp = destination + ".tmp";
            using (var output = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                stream.CopyTo(output, 81920);
            }

            for (var attempt = 0; ; attempt++)
            {
                try
                {
                    if (File.Exists(destination))
                    {
                        File.Delete(destination);
                    }
                    File.Move(temp, destination);
                    return;
                }
                catch (IOException)
                {
                    if (attempt >= 20) { throw; }
                    Thread.Sleep(200);
                }
                catch (UnauthorizedAccessException)
                {
                    if (attempt >= 20) { throw; }
                    Thread.Sleep(200);
                }
            }
        }
    }

    private static void Report(IProgress<InstallProgress> progress, int percent, string message)
    {
        if (progress != null)
        {
            progress.Report(new InstallProgress { Percent = percent, Message = message });
        }
    }

    private static void TryDeleteKey(string name)
    {
        try
        {
            Registry.CurrentUser.DeleteSubKeyTree(name, false);
        }
        catch
        {
        }
    }

    private static void TryDeleteFile(string path)
    {
        try
        {
            if (!string.IsNullOrEmpty(path) && File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
        }
    }

    [ComImport]
    [Guid("00021401-0000-0000-C000-000000000046")]
    private class ShellLink
    {
    }

    [ComImport]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [Guid("000214F9-0000-0000-C000-000000000046")]
    private interface IShellLinkW
    {
        void GetPath([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszFile, int cch, IntPtr pfd, uint fFlags);
        void GetIDList(out IntPtr ppidl);
        void SetIDList(IntPtr pidl);
        void GetDescription([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszName, int cch);
        void SetDescription([MarshalAs(UnmanagedType.LPWStr)] string pszName);
        void GetWorkingDirectory([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszDir, int cch);
        void SetWorkingDirectory([MarshalAs(UnmanagedType.LPWStr)] string pszDir);
        void GetArguments([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszArgs, int cch);
        void SetArguments([MarshalAs(UnmanagedType.LPWStr)] string pszArgs);
        void GetHotkey(out short pwHotkey);
        void SetHotkey(short wHotkey);
        void GetShowCmd(out int piShowCmd);
        void SetShowCmd(int iShowCmd);
        void GetIconLocation([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszIconPath, int cch, out int piIcon);
        void SetIconLocation([MarshalAs(UnmanagedType.LPWStr)] string pszIconPath, int iIcon);
        void SetRelativePath([MarshalAs(UnmanagedType.LPWStr)] string pszPathRel, uint dwReserved);
        void Resolve(IntPtr hwnd, uint fFlags);
        void SetPath([MarshalAs(UnmanagedType.LPWStr)] string pszFile);
    }

    [ComImport]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [Guid("0000010b-0000-0000-C000-000000000046")]
    private interface IPersistFile
    {
        void GetClassID(out Guid pClassID);
        [PreserveSig]
        int IsDirty();
        void Load([MarshalAs(UnmanagedType.LPWStr)] string pszFileName, uint dwMode);
        [PreserveSig]
        int Save([MarshalAs(UnmanagedType.LPWStr)] string pszFileName, [MarshalAs(UnmanagedType.Bool)] bool fRemember);
        void SaveCompleted([MarshalAs(UnmanagedType.LPWStr)] string pszFileName);
        void GetCurFile([MarshalAs(UnmanagedType.LPWStr)] out string ppszFileName);
    }
}
