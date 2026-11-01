using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using AutoBackup.UI;

namespace AutoBackup.Setup;

public static class UninstallerProgram
{
    [STAThread]
    public static void Main(string[] args)
    {
        if (args != null && args.Length > 0 && string.Equals(args[0], "--cleanup", StringComparison.OrdinalIgnoreCase))
        {
            Cleanup(args);
            return;
        }

        if (args != null && args.Length > 0 && string.Equals(args[0], "--quiet", StringComparison.OrdinalIgnoreCase))
        {
            var folder = ResolveInstallFolder();
            string error;
            if (folder == null || !PrepareUninstall(folder, true, out error))
            {
                Environment.ExitCode = 1;
            }
            return;
        }

        var app = new Application { ShutdownMode = ShutdownMode.OnMainWindowClose };
        Ui.InstallGlobalResources(app);
        app.Run(new UninstallWindow());
    }

    internal static string ResolveInstallFolder()
    {
        var registered = InstallCommon.GetInstalledLocation();
        if (!string.IsNullOrWhiteSpace(registered) && IsManagedFolder(registered))
        {
            return Path.GetFullPath(registered);
        }
        try
        {
            var exe = Process.GetCurrentProcess().MainModule.FileName;
            var folder = Path.GetDirectoryName(exe);
            if (!string.IsNullOrEmpty(folder) && IsManagedFolder(folder))
            {
                return Path.GetFullPath(folder);
            }
        }
        catch
        {
        }
        return null;
    }

    internal static bool IsManagedFolder(string folder)
    {
        try
        {
            var full = Path.GetFullPath(folder).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            var programs = Path.GetFullPath(Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs"))
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            return full.StartsWith(programs + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
                && string.Equals(Path.GetFileName(full), "AutoBackup", StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    internal static bool PrepareUninstall(string installFolder, bool removePointer, out string error)
    {
        error = null;
        if (!IsManagedFolder(installFolder))
        {
            error = "The install folder was not recognized as an Auto Backup installation:\n" + installFolder;
            return false;
        }
        try
        {
            var tempDir = Path.Combine(Path.GetTempPath(), "AutoBackupUninstall_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);
            var self = Process.GetCurrentProcess().MainModule.FileName;
            if (string.IsNullOrEmpty(self) || !File.Exists(self))
            {
                throw new InvalidOperationException("The uninstaller path could not be determined.");
            }
            var tempExe = Path.Combine(tempDir, "Uninstall.exe");
            File.Copy(self, tempExe, true);

            InstallCommon.UnregisterUninstall();
            InstallCommon.DeleteShortcuts();
            if (removePointer)
            {
                InstallCommon.RemoveLocalPointer();
            }

            var arguments = "--cleanup \"" + installFolder + "\" " + Process.GetCurrentProcess().Id + " \"" + tempDir + "\"";
            Process.Start(new ProcessStartInfo
            {
                FileName = tempExe,
                Arguments = arguments,
                WorkingDirectory = tempDir,
                UseShellExecute = false,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden
            });
            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }

    private static void Cleanup(string[] args)
    {
        if (args.Length < 4)
        {
            return;
        }
        var installFolder = args[1];
        int hostProcessId;
        if (!int.TryParse(args[2], out hostProcessId))
        {
            hostProcessId = 0;
        }
        var tempDir = args[3];

        if (hostProcessId > 0)
        {
            try
            {
                using (var host = Process.GetProcessById(hostProcessId))
                {
                    host.WaitForExit(20000);
                }
            }
            catch
            {
            }
        }

        if (IsManagedFolder(installFolder))
        {
            for (var attempt = 0; attempt < 80; attempt++)
            {
                try
                {
                    if (!Directory.Exists(installFolder))
                    {
                        break;
                    }
                    Directory.Delete(installFolder, true);
                    break;
                }
                catch
                {
                    Thread.Sleep(250);
                }
            }
        }

        try
        {
            var tempRoot = Path.GetFullPath(Path.GetTempPath());
            var tempFull = Path.GetFullPath(tempDir);
            var name = Path.GetFileName(tempFull.TrimEnd(Path.DirectorySeparatorChar));
            if (tempFull.StartsWith(tempRoot, StringComparison.OrdinalIgnoreCase)
                && name.StartsWith("AutoBackupUninstall_", StringComparison.Ordinal))
            {
                var info = new ProcessStartInfo("cmd.exe", "/c ping 127.0.0.1 -n 3 >nul & rmdir /s /q \"" + tempFull + "\"")
                {
                    WorkingDirectory = tempRoot,
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    WindowStyle = ProcessWindowStyle.Hidden
                };
                Process.Start(info);
            }
        }
        catch
        {
        }
    }
}

internal sealed class UninstallWindow : ChromeWindow
{
    private readonly CheckBox _removePointer;

    public UninstallWindow()
        : base(AppInfo.Name + " Uninstall", 640, 560, false, false, false)
    {
        ShowInTaskbar = true;
        var root = new Grid { Margin = new Thickness(30, 24, 30, 24) };
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var header = new Grid();
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        var icon = Ui.IconTile("\uE74D", Ui.Danger, Ui.DangerSoft, 52, 22);
        icon.Margin = new Thickness(0, 2, 15, 0);
        icon.VerticalAlignment = VerticalAlignment.Top;
        header.Children.Add(icon);
        var text = new StackPanel();
        text.Children.Add(Ui.Display("Uninstall Auto Backup", 22, FontWeights.SemiBold));
        var subtitle = Ui.T("The app will be removed from this PC. Your settings and backup archives are not touched.", 12, Ui.Muted);
        subtitle.TextWrapping = TextWrapping.Wrap;
        subtitle.LineHeight = 19;
        subtitle.Margin = new Thickness(0, 6, 0, 0);
        text.Children.Add(subtitle);
        Grid.SetColumn(text, 1);
        header.Children.Add(text);
        Grid.SetRow(header, 0);
        root.Children.Add(header);

        var body = new StackPanel { Margin = new Thickness(0, 20, 0, 0) };
        var lists = Ui.Card(BuildLists(), new Thickness(20, 16, 20, 16), 14);
        body.Children.Add(lists);

        _removePointer = Ui.CheckBox("Remove this device's local settings pointer", true);
        _removePointer.Margin = new Thickness(2, 16, 0, 0);
        body.Children.Add(_removePointer);
        var pointerNote = Ui.T("Only the local pointer is removed. The shared settings file on your synced drive and all backup archives stay exactly where they are.", 11.5, Ui.Dim);
        pointerNote.TextWrapping = TextWrapping.Wrap;
        pointerNote.LineHeight = 17;
        pointerNote.Margin = new Thickness(30, 7, 4, 0);
        body.Children.Add(pointerNote);
        Grid.SetRow(body, 1);
        root.Children.Add(body);

        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 16, 0, 0) };
        var cancel = Ui.Button("Cancel", null, ButtonKind.Secondary, delegate { Close(); });
        cancel.MinWidth = 96;
        cancel.Margin = new Thickness(0, 0, 10, 0);
        buttons.Children.Add(cancel);
        var remove = Ui.Button("Uninstall", "\uE74D", ButtonKind.DangerGhost, Remove);
        remove.MinWidth = 132;
        buttons.Children.Add(remove);
        Grid.SetRow(buttons, 2);
        root.Children.Add(buttons);

        ViewHost.Content = root;
        KeyDown += delegate(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                Close();
            }
        };
    }

    private static UIElement BuildLists()
    {
        var stack = new StackPanel();
        stack.Children.Add(Ui.Label("Will be removed", Ui.Danger));
        stack.Children.Add(InfoRow("\uE7B8", Ui.Muted, "Application files, Start menu and desktop shortcuts"));
        stack.Children.Add(InfoRow("\uE713", Ui.Muted, "The Auto Backup entry in Windows installed apps"));
        stack.Children.Add(new Border { Height = 1, Background = Ui.Line, Margin = new Thickness(0, 12, 0, 12) });
        stack.Children.Add(Ui.Label("Will be kept", Ui.Success));
        stack.Children.Add(InfoRow("\uE8A5", Ui.Muted, "Your shared settings file (AutoBackup.settings.json)"));
        stack.Children.Add(InfoRow("\uE81C", Ui.Muted, "All backup archives in your backup folder"));
        return stack;
    }

    private static Grid InfoRow(string glyph, Brush color, string text)
    {
        var row = new Grid { Margin = new Thickness(0, 0, 0, 7) };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        var icon = Ui.Icon(glyph, 12.5, color);
        icon.VerticalAlignment = VerticalAlignment.Top;
        icon.Margin = new Thickness(1, 3, 9, 0);
        row.Children.Add(icon);
        var label = Ui.T(text, 12, Ui.Muted);
        label.TextWrapping = TextWrapping.Wrap;
        Grid.SetColumn(label, 1);
        row.Children.Add(label);
        return row;
    }

    private void Remove()
    {
        var folder = UninstallerProgram.ResolveInstallFolder();
        if (string.IsNullOrEmpty(folder))
        {
            DialogService.Alert(this, "Uninstaller location not recognized",
                "This uninstaller could not find an Auto Backup installation in the usual folder.", true);
            return;
        }

        var running = InstallCommon.GetRunningApps();
        if (running.Length > 0)
        {
            var close = DialogService.Confirm(this, "Auto Backup is running",
                "The app must be closed before it can be uninstalled.", "Close and uninstall", false);
            if (!close)
            {
                return;
            }
            InstallCommon.CloseRunningApps();
        }

        string error;
        if (!UninstallerProgram.PrepareUninstall(folder, _removePointer.IsChecked == true, out error))
        {
            DialogService.Alert(this, "Auto Backup could not be removed", error, true);
            return;
        }
        Close();
    }
}
