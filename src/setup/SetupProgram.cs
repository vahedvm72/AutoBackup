using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using AutoBackup.UI;

namespace AutoBackup.Setup;

public static class SetupProgram
{
    [STAThread]
    public static void Main(string[] args)
    {
        if (HasArgument(args, "--silent"))
        {
            RunSilent(args);
            return;
        }

        var app = new Application { ShutdownMode = ShutdownMode.OnMainWindowClose };
        Ui.InstallGlobalResources(app);
        app.Run(new SetupWindow());
    }

    private static void RunSilent(string[] args)
    {
        string installFolder;
        try
        {
            string installed;
            var isInstalled = InstallCommon.IsInstalled(out installed);
            installFolder = isInstalled && !string.IsNullOrWhiteSpace(installed)
                ? installed
                : InstallInfo.DefaultInstallFolder;

            for (var attempt = 0; attempt < 40; attempt++)
            {
                var running = InstallCommon.GetRunningApps();
                var alive = running.Length > 0;
                foreach (var process in running)
                {
                    try { process.Dispose(); } catch { }
                }
                if (!alive)
                {
                    break;
                }
                Thread.Sleep(250);
            }
            if (InstallCommon.GetRunningApps().Length > 0)
            {
                InstallCommon.CloseRunningApps();
            }

            var desktopShortcut = isInstalled ? File.Exists(InstallInfo.DesktopShortcut) : true;
            var startMenuShortcut = isInstalled ? File.Exists(InstallInfo.StartMenuShortcut) : true;
            InstallCommon.Install(installFolder, desktopShortcut, startMenuShortcut, null);

            if (HasArgument(args, "--relaunch"))
            {
                StartApp(installFolder);
            }
            Environment.ExitCode = 0;
        }
        catch (Exception ex)
        {
            try
            {
                File.WriteAllText(Path.Combine(Path.GetTempPath(), "AutoBackupUpdate.log"),
                    DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "\r\n" + ex);
            }
            catch
            {
            }
            try
            {
                string installed;
                var folder = InstallCommon.IsInstalled(out installed) && !string.IsNullOrWhiteSpace(installed)
                    ? installed
                    : InstallInfo.DefaultInstallFolder;
                if (HasArgument(args, "--relaunch") && File.Exists(InstallInfo.AppPath(folder)))
                {
                    StartApp(folder);
                }
            }
            catch
            {
            }
            Environment.ExitCode = 1;
        }
    }

    private static void StartApp(string installFolder)
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = InstallInfo.AppPath(installFolder),
            WorkingDirectory = installFolder,
            UseShellExecute = true
        });
    }

    private static bool HasArgument(string[] args, string value)
    {
        if (args == null)
        {
            return false;
        }
        foreach (var arg in args)
        {
            if (string.Equals(arg, value, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }
        return false;
    }
}

internal sealed class SetupWindow : ChromeWindow
{
    private readonly string _installFolder;
    private readonly bool _isUpdate;
    private readonly string _installLabel;
    private readonly CheckBox _desktopShortcut;
    private readonly CheckBox _startMenuShortcut;
    private readonly CheckBox _launch;
    private readonly ProgressBarEx _bar;
    private readonly TextBlock _status;
    private readonly TextBlock _error;
    private readonly Button _installButton;
    private readonly Button _cancelButton;
    private bool _installing;

    public SetupWindow()
        : base(AppInfo.Name + " Setup", 900, 660, false, false, true, "Version " + AppInfo.Version)
    {
        string installedFolder;
        _isUpdate = InstallCommon.IsInstalled(out installedFolder);
        _installFolder = string.IsNullOrWhiteSpace(installedFolder) ? InstallInfo.DefaultInstallFolder : installedFolder;
        _installLabel = _isUpdate ? "Update now" : "Install now";

        CloseGuard = delegate { return !_installing; };

        _desktopShortcut = Ui.CheckBox("Create a desktop shortcut", true, "Create desktop shortcut");
        _startMenuShortcut = Ui.CheckBox("Create a Start menu shortcut", true, "Create Start menu shortcut");
        _launch = Ui.CheckBox("Launch Auto Backup when setup finishes", true, "Launch Auto Backup");
        _bar = new ProgressBarEx { Margin = new Thickness(0, 18, 0, 0), Visibility = Visibility.Collapsed };
        _status = Ui.T(string.Empty, 11.5, Ui.Muted);
        _status.Margin = new Thickness(0, 9, 0, 0);
        _status.Visibility = Visibility.Collapsed;
        _error = Ui.T(string.Empty, 11.5, Ui.Danger);
        _error.TextWrapping = TextWrapping.Wrap;
        _error.Margin = new Thickness(2, 12, 0, 0);
        _error.Visibility = Visibility.Collapsed;

        var root = new Grid { Margin = new Thickness(30, 26, 30, 26) };
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(304) });
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(22) });
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var left = BuildMarketing();
        var leftCard = new Border
        {
            Background = new LinearGradientBrush(Color.FromRgb(24, 24, 24), Color.FromRgb(12, 12, 12), 115),
            BorderBrush = Ui.Hex("#2C2C2C"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(18),
            Padding = new Thickness(26, 26, 26, 22),
            Child = left
        };
        root.Children.Add(leftCard);

        var form = new StackPanel();
        form.Children.Add(Ui.Display(_isUpdate ? "Update Auto Backup" : "Install Auto Backup", 21, FontWeights.SemiBold));
        var subtitle = Ui.T(_isUpdate
            ? "A previous installation was found. Setup replaces the app files and keeps your settings and backups."
            : "Setup installs the app for your Windows account. No administrator rights are required.", 12, Ui.Muted);
        subtitle.TextWrapping = TextWrapping.Wrap;
        subtitle.LineHeight = 19;
        subtitle.Margin = new Thickness(0, 7, 0, 0);
        form.Children.Add(subtitle);

        var location = new Border
        {
            Background = Ui.Bg2,
            BorderBrush = Ui.Line,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(13, 11, 13, 11),
            Margin = new Thickness(0, 20, 0, 0)
        };
        var locationStack = new StackPanel();
        var locationLabel = Ui.Label("Install location", Ui.Dim);
        locationLabel.Margin = new Thickness(0, 0, 0, 4);
        locationStack.Children.Add(locationLabel);
        locationStack.Children.Add(Ui.PathText(_installFolder, 12, Ui.Text));
        location.Child = locationStack;
        form.Children.Add(location);

        var options = new StackPanel { Margin = new Thickness(2, 18, 0, 0) };
        options.Children.Add(_desktopShortcut);
        _desktopShortcut.Margin = new Thickness(0, 0, 0, 11);
        options.Children.Add(_startMenuShortcut);
        _startMenuShortcut.Margin = new Thickness(0, 0, 0, 11);
        options.Children.Add(_launch);
        form.Children.Add(options);

        var note = new Border
        {
            Background = Ui.Surface,
            BorderBrush = Ui.Line,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(9),
            Padding = new Thickness(12, 9, 12, 9),
            Margin = new Thickness(0, 18, 0, 0)
        };
        var noteRow = new Grid();
        noteRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        noteRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        var noteIcon = Ui.Icon("\uE946", 12, Ui.Accent);
        noteIcon.VerticalAlignment = VerticalAlignment.Top;
        noteIcon.Margin = new Thickness(0, 2, 8, 0);
        noteRow.Children.Add(noteIcon);
        var noteText = Ui.T("Your shared settings file and backup archives are never changed by setup.", 11.5, Ui.Muted);
        noteText.TextWrapping = TextWrapping.Wrap;
        noteText.LineHeight = 17;
        Grid.SetColumn(noteText, 1);
        noteRow.Children.Add(noteText);
        note.Child = noteRow;
        form.Children.Add(note);

        form.Children.Add(_error);
        form.Children.Add(_bar);
        form.Children.Add(_status);

        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 22, 0, 0)
        };
        _cancelButton = Ui.Button("Cancel", null, ButtonKind.Secondary, delegate { Close(); });
        _cancelButton.MinWidth = 96;
        _cancelButton.Margin = new Thickness(0, 0, 10, 0);
        buttons.Children.Add(_cancelButton);
        _installButton = Ui.Button(_installLabel, "\uE7B8", ButtonKind.Primary, delegate { _ = InstallAsync(); });
        _installButton.MinWidth = 156;
        AutomationProperties.SetName(_installButton, _installLabel);
        buttons.Children.Add(_installButton);
        form.Children.Add(buttons);

        var formCard = Ui.Card(form, new Thickness(28, 26, 28, 26), 18);
        Grid.SetColumn(formCard, 2);
        root.Children.Add(formCard);

        ViewHost.Content = root;
        KeyDown += delegate(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape && !_installing)
            {
                Close();
            }
        };
    }

    private static UIElement BuildMarketing()
    {
        var panel = new StackPanel();
        panel.Children.Add(Ui.Logo(62));
        var title = Ui.Display("Auto Backup", 27, FontWeights.SemiBold);
        title.Margin = new Thickness(0, 18, 0, 0);
        panel.Children.Add(title);
        var version = Ui.Badge("Version " + AppInfo.Version, Ui.AccentHover, Ui.AccentSoft);
        version.HorizontalAlignment = HorizontalAlignment.Left;
        version.Margin = new Thickness(0, 10, 0, 0);
        panel.Children.Add(version);
        var description = Ui.T("A small Windows app that compresses a folder with WinRAR and stores the archive in your backup location - ready to restore on any device.", 12, Ui.Muted);
        description.TextWrapping = TextWrapping.Wrap;
        description.LineHeight = 19;
        description.Margin = new Thickness(0, 16, 0, 0);
        panel.Children.Add(description);

        var features = new StackPanel { Margin = new Thickness(0, 22, 0, 0) };
        features.Children.Add(Feature("\uE7B8", Ui.Accent, Ui.AccentSoft, "One-click backups", "Pick your folders once, then create a RAR archive whenever you need."));
        features.Children.Add(Feature("\uE81C", Ui.Purple, Ui.PurpleSoft, "Restore anywhere", "Choose a previous archive and extract it on this PC or another device."));
        features.Children.Add(Feature("\uE895", Ui.Success, Ui.SuccessSoft, "Shared settings", "One settings file on a synced drive keeps every computer in step."));
        panel.Children.Add(features);

        var spacer = new Border { Height = 14 };
        panel.Children.Add(spacer);
        var footer = Ui.T("Installs in your user profile.\nNo administrator rights needed.", 11, Ui.Dim);
        footer.TextWrapping = TextWrapping.Wrap;
        footer.LineHeight = 17;
        panel.Children.Add(footer);
        return panel;
    }

    private static Grid Feature(string glyph, Brush color, Brush background, string title, string description)
    {
        var row = new Grid { Margin = new Thickness(0, 0, 0, 14) };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        var icon = Ui.IconTile(glyph, color, background, 34, 14);
        icon.Margin = new Thickness(0, 1, 11, 0);
        row.Children.Add(icon);
        var text = new StackPanel();
        text.Children.Add(Ui.T(title, 12, FontWeights.SemiBold, Ui.Text));
        var desc = Ui.T(description, 10.5, Ui.Muted);
        desc.TextWrapping = TextWrapping.Wrap;
        desc.LineHeight = 15;
        desc.Margin = new Thickness(0, 2, 0, 0);
        text.Children.Add(desc);
        Grid.SetColumn(text, 1);
        row.Children.Add(text);
        return row;
    }

    private async Task InstallAsync()
    {
        if (_installing)
        {
            return;
        }

        var running = InstallCommon.GetRunningApps();
        if (running.Length > 0)
        {
            var close = DialogService.Confirm(this, "Auto Backup is running",
                "Setup needs to close the running app before its files can be updated.", "Close app and continue", false);
            if (!close)
            {
                return;
            }
            InstallCommon.CloseRunningApps();
        }

        _installing = true;
        SetBusy(true);
        _error.Visibility = Visibility.Collapsed;
        _bar.Value = 0;
        _bar.Visibility = Visibility.Visible;
        _status.Text = "Starting setup...";
        _status.Visibility = Visibility.Visible;

        var progress = new Progress<InstallProgress>(delegate(InstallProgress update)
        {
            _bar.Value = update.Percent;
            if (!string.IsNullOrEmpty(update.Message))
            {
                _status.Text = update.Message;
            }
        });

        try
        {
            var desktopShortcut = _desktopShortcut.IsChecked == true;
            var startMenuShortcut = _startMenuShortcut.IsChecked == true;
            await Task.Run(delegate
            {
                InstallCommon.Install(_installFolder, desktopShortcut, startMenuShortcut, progress);
            });
            _installing = false;
            ShowDone();
        }
        catch (Exception ex)
        {
            _installing = false;
            SetBusy(false);
            _bar.Visibility = Visibility.Collapsed;
            _status.Visibility = Visibility.Collapsed;
            _error.Text = "Setup could not finish. " + ex.Message;
            _error.Visibility = Visibility.Visible;
        }
    }

    private void SetBusy(bool busy)
    {
        _installButton.IsEnabled = !busy;
        _cancelButton.IsEnabled = !busy;
        _desktopShortcut.IsEnabled = !busy;
        _startMenuShortcut.IsEnabled = !busy;
        _launch.IsEnabled = !busy;
        var label = ((StackPanel)_installButton.Content).Children[((StackPanel)_installButton.Content).Children.Count - 1] as TextBlock;
        if (label != null)
        {
            label.Text = busy ? "Installing..." : _installLabel;
        }
    }

    private void ShowDone()
    {
        SetTitle("Setup complete", AppInfo.Name + " is ready");
        string launchError = null;
        var launched = false;
        if (_launch.IsChecked == true)
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = InstallInfo.AppPath(_installFolder),
                    WorkingDirectory = _installFolder,
                    UseShellExecute = true
                });
                launched = true;
            }
            catch (Exception ex)
            {
                launchError = ex.Message;
            }
        }

        var inner = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center };
        inner.Children.Add(Ui.IconTile("\uE73E", Ui.Success, Ui.SuccessSoft, 64, 27));
        var title = Ui.Display("Auto Backup is installed", 23, FontWeights.SemiBold);
        title.HorizontalAlignment = HorizontalAlignment.Center;
        title.Margin = new Thickness(0, 18, 0, 0);
        inner.Children.Add(title);
        var subtitle = Ui.T("Version " + AppInfo.Version + " is ready to protect your folders.", 12.5, Ui.Muted);
        subtitle.HorizontalAlignment = HorizontalAlignment.Center;
        subtitle.Margin = new Thickness(0, 7, 0, 0);
        inner.Children.Add(subtitle);

        var pathBox = new Border
        {
            Background = Ui.Bg2,
            BorderBrush = Ui.Line,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(9),
            Padding = new Thickness(13, 11, 13, 11),
            Margin = new Thickness(0, 22, 0, 0)
        };
        var pathStack = new StackPanel();
        var label = Ui.Label("Install location", Ui.Dim);
        label.Margin = new Thickness(0, 0, 0, 4);
        pathStack.Children.Add(label);
        pathStack.Children.Add(Ui.PathText(_installFolder, 12, Ui.Text));
        pathBox.Child = pathStack;
        inner.Children.Add(pathBox);

        if (!string.IsNullOrEmpty(launchError))
        {
            var failed = Ui.T("Auto Backup could not be started automatically. " + launchError, 11.5, Ui.Warning);
            failed.TextWrapping = TextWrapping.Wrap;
            failed.TextAlignment = TextAlignment.Center;
            failed.MaxWidth = 480;
            failed.Margin = new Thickness(0, 14, 0, 0);
            inner.Children.Add(failed);
        }
        else if (launched)
        {
            var started = Ui.T("Auto Backup is starting now. The setup window can be closed.", 11.5, Ui.Muted);
            started.HorizontalAlignment = HorizontalAlignment.Center;
            started.Margin = new Thickness(0, 14, 0, 0);
            inner.Children.Add(started);
        }
        else if (_desktopShortcut.IsChecked != true && _startMenuShortcut.IsChecked != true)
        {
            var noShortcut = Ui.T("No shortcuts were created. Open AutoBackup.exe from the install folder whenever you need it.", 11.5, Ui.Dim);
            noShortcut.TextWrapping = TextWrapping.Wrap;
            noShortcut.TextAlignment = TextAlignment.Center;
            noShortcut.MaxWidth = 460;
            noShortcut.Margin = new Thickness(0, 14, 0, 0);
            inner.Children.Add(noShortcut);
        }

        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 24, 0, 0)
        };
        var openFolder = Ui.Button("Open folder", "\uE838", ButtonKind.Secondary, delegate { OpenFolder(); });
        openFolder.MinWidth = 132;
        openFolder.Margin = new Thickness(0, 0, 10, 0);
        buttons.Children.Add(openFolder);
        var finish = Ui.Button("Finish", "\uE73E", ButtonKind.Primary, delegate { Close(); });
        finish.MinWidth = 118;
        AutomationProperties.SetName(finish, "Finish");
        buttons.Children.Add(finish);
        inner.Children.Add(buttons);

        var card = Ui.Card(inner, new Thickness(38, 32, 38, 30), 18);
        card.Width = 600;
        card.HorizontalAlignment = HorizontalAlignment.Center;
        card.VerticalAlignment = VerticalAlignment.Center;
        var doneRoot = new Grid { Margin = new Thickness(30, 24, 30, 24) };
        doneRoot.Children.Add(card);
        ViewHost.Content = doneRoot;
        Ui.FadeIn(doneRoot);
    }

    private void OpenFolder()
    {
        try
        {
            Process.Start(new ProcessStartInfo { FileName = "explorer.exe", Arguments = "\"" + _installFolder + "\"", UseShellExecute = true });
        }
        catch (Exception ex)
        {
            DialogService.Alert(this, "The folder could not be opened", ex.Message, true);
        }
    }
}
