using System;
using System.IO;
using System.Threading;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using AutoBackup.Services;
using AutoBackup.UI;

namespace AutoBackup.Views;

public sealed class SettingsView : UserControl
{
    private readonly AppConfig _config;
    private readonly Action _uninstall;
    private string _settingsPath;
    private readonly TextBlock _settingsPathText;
    private readonly TextBox _winrarBox;
    private readonly TextBlock _winrarStatus;
    private readonly TextBlock _error;
    private readonly TextBlock _notice;
    private readonly TextBlock _updateStatus;
    private readonly Button _checkUpdatesButton;
    private readonly Button _installUpdateButton;
    private UpdateInfo _availableUpdate;
    private bool _checkingUpdates;

    public event Action<AppConfig, string> Saved;
    public event Action<string> SettingsMoved;

    public SettingsView(AppConfig config, string settingsPath, Action uninstall)
    {
        _config = config;
        _settingsPath = settingsPath;
        _uninstall = uninstall;

        var leftColumn = new StackPanel();
        var rightColumn = new StackPanel();

        var header = new StackPanel { Margin = new Thickness(0, 0, 0, 14) };
        header.Children.Add(Ui.Display("Settings", 22, FontWeights.SemiBold));
        var subtitle = Ui.T("WinRAR and the shared settings file used by every device.", 12.5, Ui.Muted);
        subtitle.TextWrapping = TextWrapping.Wrap;
        subtitle.LineHeight = 19;
        subtitle.Margin = new Thickness(0, 5, 0, 0);
        header.Children.Add(subtitle);

        _settingsPathText = Ui.PathText(settingsPath, 12, Ui.Text);
        var settingsCard = new StackPanel();
        settingsCard.Children.Add(SectionHeader("\uE8A5", Ui.Accent, Ui.AccentSoft, "Shared settings file",
            "Keep this file on a synced drive so every device shares it."));
        var pathBox = new Border
        {
            Background = Ui.Bg2,
            BorderBrush = Ui.Line,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(9),
            Padding = new Thickness(12, 10, 12, 10),
            Margin = new Thickness(0, 12, 0, 0),
            Child = _settingsPathText
        };
        settingsCard.Children.Add(pathBox);
        var moveRow = new Grid { Margin = new Thickness(0, 8, 0, 0) };
        moveRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        moveRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var moveNote = Ui.T("Moving the file copies your current settings and updates this device.", 11.5, Ui.Dim);
        moveNote.TextWrapping = TextWrapping.Wrap;
        moveNote.VerticalAlignment = VerticalAlignment.Center;
        moveRow.Children.Add(moveNote);
        var move = Ui.Button("Change location...", "\uE8B7", ButtonKind.Ghost, MoveSettingsFile);
        move.MinWidth = 152;
        Grid.SetColumn(move, 1);
        moveRow.Children.Add(move);
        settingsCard.Children.Add(moveRow);
        leftColumn.Children.Add(SectionCard(settingsCard));

        _winrarBox = Ui.Input(config.WinRarPath ?? string.Empty, "WinRAR path");
        _winrarBox.TextChanged += delegate { RefreshWinRarStatus(); };
        var winrarBrowse = Ui.Button("Browse", "\uE8B7", ButtonKind.Secondary, BrowseWinRar);
        winrarBrowse.MinWidth = 92;
        AutomationProperties.SetName(winrarBrowse, "Browse WinRAR");
        var detect = Ui.Button("Auto-detect", "\uE72C", ButtonKind.Ghost, DetectWinRar);
        detect.MinWidth = 116;
        AutomationProperties.SetName(detect, "Auto-detect WinRAR");
        var winrarStack = new StackPanel();
        winrarStack.Children.Add(_winrarBox);
        var winrarButtons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 8, 0, 0)
        };
        detect.Margin = new Thickness(0, 0, 8, 0);
        winrarButtons.Children.Add(detect);
        winrarButtons.Children.Add(winrarBrowse);
        winrarStack.Children.Add(winrarButtons);

        _winrarStatus = Ui.T(string.Empty, 11.5, Ui.Muted);
        _winrarStatus.TextWrapping = TextWrapping.Wrap;
        _winrarStatus.Margin = new Thickness(2, 9, 0, 0);

        var rarCard = new StackPanel();
        rarCard.Children.Add(SectionHeader("\uE7B8", Ui.Success, Ui.SuccessSoft, "WinRAR",
            "Auto Backup uses the Rar.exe command-line tool that ships with WinRAR."));
        var rarField = Ui.Field("Rar.exe path", winrarStack, null);
        rarField.Margin = new Thickness(0, 12, 0, 0);
        rarCard.Children.Add(rarField);
        rarCard.Children.Add(_winrarStatus);
        leftColumn.Children.Add(SectionCard(rarCard));

        _updateStatus = Ui.T(string.Empty, 11.5, Ui.Dim);
        _updateStatus.TextWrapping = TextWrapping.Wrap;
        _updateStatus.Margin = new Thickness(0, 10, 0, 0);
        _checkUpdatesButton = Ui.Button("Check for updates", "\uE72C", ButtonKind.Secondary, CheckForUpdates);
        _checkUpdatesButton.MinWidth = 176;
        AutomationProperties.SetName(_checkUpdatesButton, "Check for updates");
        _installUpdateButton = Ui.Button("Install update", "\uE896", ButtonKind.Primary, InstallUpdate);
        _installUpdateButton.MinWidth = 150;
        _installUpdateButton.Margin = new Thickness(8, 0, 0, 0);
        _installUpdateButton.Visibility = Visibility.Collapsed;
        AutomationProperties.SetName(_installUpdateButton, "Install update");
        var updateButtons = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 10, 0, 0) };
        updateButtons.Children.Add(_checkUpdatesButton);
        updateButtons.Children.Add(_installUpdateButton);
        var updatesCard = new StackPanel();
        updatesCard.Children.Add(SectionHeader("\uE895", Ui.Text, Ui.Hex("#1A1A1A"), "Updates",
            "Installed version " + AppInfo.Version + ". New versions are downloaded from GitHub."));
        updatesCard.Children.Add(_updateStatus);
        updatesCard.Children.Add(updateButtons);
        rightColumn.Children.Add(SectionCard(updatesCard));

        var maintenance = new StackPanel();
        maintenance.Children.Add(SectionHeader("\uE713", Ui.Muted, Ui.Hex("#1A1A1A"), "Maintenance",
            "Version " + AppInfo.Version + "  \u2022  " + Environment.MachineName));
        var actions = new StackPanel { Margin = new Thickness(0, 12, 0, 0) };
        var uninstaller = GetUninstallerPath();
        if (!string.IsNullOrEmpty(uninstaller))
        {
            var uninstallButton = Ui.Button("Uninstall Auto Backup", "\uE74D", ButtonKind.DangerGhost, delegate
            {
                if (_uninstall != null) { _uninstall(); }
            });
            uninstallButton.HorizontalAlignment = HorizontalAlignment.Left;
            uninstallButton.MinWidth = 190;
            uninstallButton.Margin = new Thickness(0, 10, 0, 0);
            AutomationProperties.SetName(uninstallButton, "Uninstall Auto Backup");
            actions.Children.Add(uninstallButton);
            var uninstallNote = Ui.T("Removing the app does not delete your shared settings file or your backups.", 11.5, Ui.Dim);
            uninstallNote.TextWrapping = TextWrapping.Wrap;
            uninstallNote.Margin = new Thickness(2, 7, 0, 0);
            actions.Children.Add(uninstallNote);
        }
        maintenance.Children.Add(actions);
        rightColumn.Children.Add(SectionCard(maintenance));

        _error = Ui.T(string.Empty, 11.5, Ui.Danger);
        _error.TextWrapping = TextWrapping.Wrap;
        _error.Visibility = Visibility.Collapsed;
        _notice = Ui.T(string.Empty, 11.5, Ui.Success);
        _notice.TextWrapping = TextWrapping.Wrap;
        _notice.Visibility = Visibility.Collapsed;

        var root = new Grid { Margin = new Thickness(30, 22, 30, 18) };
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        Grid.SetRow(header, 0);
        root.Children.Add(header);

        var columns = new Grid();
        columns.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        columns.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(16) });
        columns.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        Grid.SetColumn(leftColumn, 0);
        columns.Children.Add(leftColumn);
        Grid.SetColumn(rightColumn, 2);
        columns.Children.Add(rightColumn);

        var scroll = Ui.ScrollHost(columns);
        Grid.SetRow(scroll, 1);
        root.Children.Add(scroll);

        var footer = new Grid { Margin = new Thickness(0, 12, 0, 0) };
        footer.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        footer.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        footer.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        footer.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var messages = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(2, 0, 16, 0) };
        messages.Children.Add(_notice);
        messages.Children.Add(_error);
        footer.Children.Add(messages);
        var save = Ui.Button("Save changes", "\uE73E", ButtonKind.Primary, Save);
        save.MinWidth = 154;
        AutomationProperties.SetName(save, "Save changes");
        Grid.SetColumn(save, 1);
        footer.Children.Add(save);
        Grid.SetRow(footer, 2);
        root.Children.Add(footer);

        Content = root;
        RefreshWinRarStatus();
    }

    private static Border SectionCard(UIElement content)
    {
        var card = Ui.Card(content, new Thickness(20, 16, 20, 16), 14);
        card.Margin = new Thickness(0, 0, 0, 12);
        return card;
    }

    private static Grid SectionHeader(string glyph, Brush color, Brush background, string title, string description)
    {
        var row = new Grid();
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        var tile = Ui.IconTile(glyph, color, background, 38, 16);
        tile.Margin = new Thickness(0, 1, 13, 0);
        tile.VerticalAlignment = VerticalAlignment.Top;
        row.Children.Add(tile);
        var text = new StackPanel();
        text.Children.Add(Ui.T(title, 14, FontWeights.SemiBold, Ui.Text));
        var desc = Ui.T(description, 11.5, Ui.Muted);
        desc.TextWrapping = TextWrapping.Wrap;
        desc.LineHeight = 17;
        desc.Margin = new Thickness(0, 3, 0, 0);
        text.Children.Add(desc);
        Grid.SetColumn(text, 1);
        row.Children.Add(text);
        return row;
    }

    private void Save()
    {
        ClearMessages();
        var winrar = WinRarLocator.Normalize(_winrarBox.Text);

        if (!WinRarLocator.IsValid(winrar))
        {
            SetError("WinRAR was not found. Install WinRAR, click Auto-detect, or choose Rar.exe manually.");
            return;
        }
        if (string.IsNullOrEmpty(_settingsPath))
        {
            SetError("The shared settings file location is missing.");
            return;
        }

        _config.WinRarPath = winrar;
        try
        {
            ConfigStore.SaveSettings(_config, _settingsPath);
            ConfigStore.SaveBootstrap(_settingsPath);
        }
        catch (Exception ex)
        {
            SetError("The settings could not be saved. " + ex.Message);
            return;
        }

        _notice.Text = "Settings saved.";
        _notice.Visibility = Visibility.Visible;
        var handler = Saved;
        if (handler != null)
        {
            handler(_config.Clone(), _settingsPath);
        }
    }

    private async void CheckForUpdates()
    {
        if (_checkingUpdates)
        {
            return;
        }
        _checkingUpdates = true;
        _checkUpdatesButton.IsEnabled = false;
        _installUpdateButton.Visibility = Visibility.Collapsed;
        SetUpdateStatus("Checking GitHub for updates...", Ui.Dim);
        try
        {
            var update = await UpdateService.CheckAsync(CancellationToken.None);
            if (update == null)
            {
                _availableUpdate = null;
                SetUpdateStatus("You are using the latest version (v" + AppInfo.Version + ").", Ui.Success);
            }
            else
            {
                _availableUpdate = update;
                SetUpdateStatus("Version " + update.Version + " is available on GitHub.", Ui.Text);
                _installUpdateButton.Visibility = Visibility.Visible;
            }
        }
        catch (Exception ex)
        {
            SetUpdateStatus("Could not check for updates. " + ex.Message, Ui.Warning);
        }
        finally
        {
            _checkingUpdates = false;
            _checkUpdatesButton.IsEnabled = true;
        }
    }

    private async void InstallUpdate()
    {
        if (_availableUpdate == null)
        {
            return;
        }
        _checkUpdatesButton.IsEnabled = false;
        _installUpdateButton.IsEnabled = false;
        SetUpdateStatus("Downloading version " + _availableUpdate.Version + "...", Ui.Dim);
        try
        {
            var destination = UpdateService.PrepareInstallerPath(_availableUpdate);
            await UpdateService.DownloadAsync(_availableUpdate, destination, delegate(double percent)
            {
                Dispatcher.BeginInvoke(new Action(delegate
                {
                    SetUpdateStatus("Downloading version " + _availableUpdate.Version + "... " + ((int)percent) + "%", Ui.Dim);
                }));
            }, CancellationToken.None);
            SetUpdateStatus("Starting the installer...", Ui.Text);
            UpdateService.RunInstaller(destination);
            var window = Window.GetWindow(this);
            if (window != null)
            {
                window.Close();
            }
        }
        catch (Exception ex)
        {
            SetUpdateStatus("The update could not be installed. " + ex.Message, Ui.Danger);
            _checkUpdatesButton.IsEnabled = true;
            _installUpdateButton.IsEnabled = true;
        }
    }

    private void SetUpdateStatus(string text, Brush color)
    {
        _updateStatus.Text = text;
        _updateStatus.Foreground = color ?? Ui.Dim;
    }

    private void MoveSettingsFile()
    {
        ClearMessages();
        try
        {
            var currentFolder = Path.GetDirectoryName(_settingsPath);
            if (string.IsNullOrEmpty(currentFolder) || !Directory.Exists(currentFolder))
            {
                currentFolder = ConfigStore.SuggestedSettingsFolder;
            }
            var folder = FolderPicker.Pick(Window.GetWindow(this), "Choose the new shared settings folder", currentFolder);
            if (string.IsNullOrEmpty(folder))
            {
                return;
            }
            var newPath = Path.GetFullPath(ConfigStore.BuildSettingsPath(folder));
            if (string.Equals(newPath, _settingsPath, StringComparison.OrdinalIgnoreCase))
            {
                _notice.Text = "The settings file already lives in that folder.";
                _notice.Visibility = Visibility.Visible;
                return;
            }
            if (File.Exists(newPath))
            {
                var replace = DialogService.Confirm(Window.GetWindow(this), "Replace the existing settings file?",
                    "A settings file already exists at:\n" + newPath + "\n\nIt will be replaced with the settings from this device.", "Replace", false);
                if (!replace)
                {
                    return;
                }
            }
            EnsureDirectory(Path.GetDirectoryName(newPath));
            ConfigStore.SaveSettings(_config, newPath);
            ConfigStore.SaveBootstrap(newPath);
            _settingsPath = newPath;
            _settingsPathText.Text = newPath;
            _settingsPathText.ToolTip = newPath;
            _notice.Text = "The settings file was moved. Other devices can point to the same file.";
            _notice.Visibility = Visibility.Visible;
            var handler = SettingsMoved;
            if (handler != null)
            {
                handler(newPath);
            }
        }
        catch (Exception ex)
        {
            SetError("The settings file could not be moved. " + ex.Message);
        }
    }

    private static void EnsureDirectory(string directory)
    {
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }
    }

    private void BrowseWinRar()
    {
        ClearMessages();
        try
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Title = "Select Rar.exe",
                Filter = "WinRAR command line (Rar.exe)|Rar.exe|Executables (*.exe)|*.exe|All files (*.*)|*.*",
                CheckFileExists = true,
                Multiselect = false
            };
            var current = WinRarLocator.Normalize(_winrarBox.Text);
            if (!string.IsNullOrEmpty(current) && File.Exists(current))
            {
                dialog.InitialDirectory = Path.GetDirectoryName(current);
                dialog.FileName = Path.GetFileName(current);
            }
            var owner = Window.GetWindow(this);
            if (owner != null && dialog.ShowDialog(owner) == true)
            {
                _winrarBox.Text = WinRarLocator.Normalize(dialog.FileName);
            }
        }
        catch (Exception ex)
        {
            SetError("WinRAR could not be selected. " + ex.Message);
        }
    }

    private void DetectWinRar()
    {
        ClearMessages();
        var detected = WinRarLocator.Detect();
        if (string.IsNullOrEmpty(detected))
        {
            SetError("WinRAR was not found in the usual locations. Choose Rar.exe manually.");
            return;
        }
        _winrarBox.Text = detected;
    }

    private void RefreshWinRarStatus()
    {
        var path = WinRarLocator.Normalize(_winrarBox.Text);
        if (WinRarLocator.IsValid(path))
        {
            _winrarStatus.Text = "WinRAR is ready.";
            _winrarStatus.Foreground = Ui.Success;
        }
        else if (string.IsNullOrWhiteSpace(path))
        {
            _winrarStatus.Text = "WinRAR was not configured. Use Auto-detect or install WinRAR first.";
            _winrarStatus.Foreground = Ui.Warning;
        }
        else
        {
            _winrarStatus.Text = "Rar.exe was not found at this path. Use Auto-detect or choose another file.";
            _winrarStatus.Foreground = Ui.Warning;
        }
    }

    private static string GetUninstallerPath()
    {
        try
        {
            var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, AppInfo.UninstallerName);
            return File.Exists(path) ? path : null;
        }
        catch
        {
            return null;
        }
    }

    private void ClearMessages()
    {
        _error.Visibility = Visibility.Collapsed;
        _notice.Visibility = Visibility.Collapsed;
    }

    private void SetError(string message)
    {
        _error.Text = message;
        _error.Visibility = Visibility.Visible;
        _notice.Visibility = Visibility.Collapsed;
    }
}
