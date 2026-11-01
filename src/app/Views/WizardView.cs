using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using AutoBackup.Services;
using AutoBackup.UI;

namespace AutoBackup.Views;

public sealed class WizardView : UserControl
{
    private readonly ContentControl _pageHost;
    private readonly Button _backButton;
    private readonly Button _nextButton;
    private readonly TextBlock _nextLabel;
    private readonly TextBlock _errorText;
    private readonly TextBox _settingsFolderBox;
    private readonly TextBlock _settingsPreview;
    private readonly TextBlock _existingNotice;
    private readonly SourceFolderEditor _sources;
    private readonly TextBox _backupBox;
    private readonly TextBox _winrarBox;
    private readonly TextBlock _winrarStatus;
    private readonly List<StepBadge> _steps = new List<StepBadge>();
    private readonly UIElement[] _pages;
    private AppConfig _draft;
    private string _settingsPath;
    private int _step;

    public event Action<AppConfig, string> Completed;

    public WizardView(string initialBackupFolder = null)
    {
        var root = new Grid { Margin = new Thickness(30, 22, 30, 22) };
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(196) });
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(24) });
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var nav = new StackPanel { Margin = new Thickness(4, 6, 0, 0) };
        nav.Children.Add(Ui.Label("Setup", Ui.Dim));
        var navTitle = Ui.Display("Let's get you ready", 18, FontWeights.SemiBold);
        navTitle.Margin = new Thickness(2, 0, 0, 18);
        navTitle.TextWrapping = TextWrapping.Wrap;
        nav.Children.Add(navTitle);
        _steps.Add(BuildStepItem("1", "Welcome"));
        _steps.Add(BuildStepItem("2", "Shared settings"));
        _steps.Add(BuildStepItem("3", "Folders & WinRAR"));
        var stepsHost = new Grid();
        stepsHost.Children.Add(new Border
        {
            Width = 1,
            Background = Ui.Line,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Stretch,
            Margin = new Thickness(13, 16, 0, 34)
        });
        var stepsPanel = new StackPanel();
        foreach (var step in _steps)
        {
            stepsPanel.Children.Add(step.Root);
        }
        stepsHost.Children.Add(stepsPanel);
        nav.Children.Add(stepsHost);
        var navNote = Ui.T("Your settings stay in one file, ready for every device.", 11.5, Ui.Dim);
        navNote.TextWrapping = TextWrapping.Wrap;
        navNote.Margin = new Thickness(2, 26, 10, 0);
        nav.Children.Add(navNote);
        Grid.SetColumn(nav, 0);
        root.Children.Add(nav);

        var cardGrid = new Grid();
        cardGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        cardGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        _settingsFolderBox = Ui.Input(string.Empty, "Settings folder");
        var settingsBrowse = Ui.Button("Browse", "\uE8B7", ButtonKind.Secondary, BrowseSettingsFolder);
        settingsBrowse.MinWidth = 104;
        AutomationProperties.SetName(settingsBrowse, "Browse settings folder");
        _settingsPreview = Ui.T(string.Empty, 11.5, Ui.Dim);
        _settingsPreview.TextWrapping = TextWrapping.Wrap;
        _existingNotice = Ui.T(string.Empty, 11.5, Ui.Dim);
        _existingNotice.TextWrapping = TextWrapping.Wrap;

        _sources = new SourceFolderEditor();

        _backupBox = Ui.Input(string.Empty, "Backup folder");
        var backupBrowse = Ui.Button("Browse", "\uE8B7", ButtonKind.Secondary, delegate { BrowseInto(_backupBox, "Choose the backup folder"); });
        backupBrowse.MinWidth = 104;
        AutomationProperties.SetName(backupBrowse, "Browse backup folder");

        _winrarBox = Ui.Input(string.Empty, "WinRAR path");
        var winrarBrowse = Ui.Button("Browse", "\uE8B7", ButtonKind.Secondary, BrowseWinRar);
        winrarBrowse.MinWidth = 92;
        AutomationProperties.SetName(winrarBrowse, "Browse WinRAR");
        var winrarDetect = Ui.Button("Auto-detect", "\uE72C", ButtonKind.Ghost, DetectWinRar);
        winrarDetect.MinWidth = 116;
        AutomationProperties.SetName(winrarDetect, "Auto-detect WinRAR");
        _winrarStatus = Ui.T(string.Empty, 11.5, Ui.Muted);
        _winrarStatus.TextWrapping = TextWrapping.Wrap;

        var winrarGrid = new Grid();
        winrarGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        winrarGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(8) });
        winrarGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        winrarGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(8) });
        winrarGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        Grid.SetColumn(_winrarBox, 0);
        winrarGrid.Children.Add(_winrarBox);
        winrarGrid.Children.Add(winrarDetect);
        Grid.SetColumn(winrarDetect, 2);
        winrarGrid.Children.Add(winrarBrowse);
        Grid.SetColumn(winrarBrowse, 4);

        var welcomePage = Center(BuildWelcomePage());
        var settingsPage = Center(BuildSettingsPage(settingsBrowse));
        var foldersPage = BuildFoldersPage(backupBrowse, winrarGrid);
        _pages = new UIElement[] { welcomePage, settingsPage, foldersPage };

        _pageHost = new ContentControl { Focusable = false, HorizontalContentAlignment = HorizontalAlignment.Stretch, VerticalContentAlignment = VerticalAlignment.Stretch };
        Grid.SetRow(_pageHost, 0);
        cardGrid.Children.Add(_pageHost);

        var footer = new Grid { Margin = new Thickness(0, 18, 0, 0) };
        footer.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        footer.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        footer.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        footer.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        _errorText = Ui.T(string.Empty, 11.5, Ui.Danger);
        _errorText.TextWrapping = TextWrapping.Wrap;
        _errorText.Visibility = Visibility.Collapsed;
        _errorText.Margin = new Thickness(2, 0, 16, 0);
        Grid.SetColumn(_errorText, 0);
        footer.Children.Add(_errorText);

        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        _backButton = Ui.Button("Back", "\uE72B", ButtonKind.Secondary, delegate { GoToStep(_step - 1); });
        _backButton.MinWidth = 92;
        _backButton.Margin = new Thickness(0, 0, 10, 0);
        _backButton.Visibility = Visibility.Collapsed;
        _nextButton = Ui.Button("Get started", "\uE76C", ButtonKind.Primary, Next);
        _nextButton.MinWidth = 148;
        _nextLabel = (TextBlock)((StackPanel)_nextButton.Content).Children[((StackPanel)_nextButton.Content).Children.Count - 1];
        buttons.Children.Add(_backButton);
        buttons.Children.Add(_nextButton);
        Grid.SetColumn(buttons, 1);
        Grid.SetRow(buttons, 1);
        footer.Children.Add(buttons);
        cardGrid.RowDefinitions[1].Height = GridLength.Auto;
        Grid.SetRow(footer, 1);
        cardGrid.Children.Add(footer);

        var card = new Border
        {
            Background = Ui.CardBg,
            BorderBrush = Ui.Line,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(16),
            Padding = new Thickness(30, 26, 30, 24),
            Child = cardGrid
        };
        Grid.SetColumn(card, 2);
        root.Children.Add(card);

        Content = root;
        _settingsFolderBox.TextChanged += delegate { RefreshSettingsNotice(); };
        _winrarBox.TextChanged += delegate { RefreshWinRarStatus(); };
        var detected = WinRarLocator.Detect();
        if (!string.IsNullOrEmpty(detected))
        {
            _winrarBox.Text = detected;
        }
        if (!string.IsNullOrEmpty(initialBackupFolder))
        {
            _backupBox.Text = initialBackupFolder;
        }
        RefreshSettingsNotice();
        RefreshWinRarStatus();
        GoToStep(0);
    }

    private static UIElement Center(UIElement content)
    {
        var element = content as FrameworkElement;
        if (element != null)
        {
            element.VerticalAlignment = VerticalAlignment.Center;
        }
        var host = new Grid();
        host.Children.Add(content);
        return host;
    }

    private UIElement BuildWelcomePage()
    {
        var panel = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        panel.Children.Add(Ui.Logo(56));
        var title = Ui.Display("Welcome to Auto Backup", 26, FontWeights.SemiBold);
        title.Margin = new Thickness(0, 20, 0, 0);
        panel.Children.Add(title);
        var subtitle = Ui.T("A small Windows utility that creates a RAR archive of your folder, saves it to a backup location, and restores it whenever you need.", 13, Ui.Muted);
        subtitle.TextWrapping = TextWrapping.Wrap;
        subtitle.LineHeight = 21;
        subtitle.Margin = new Thickness(0, 9, 0, 0);
        panel.Children.Add(subtitle);

        var features = new StackPanel { Margin = new Thickness(0, 24, 0, 0) };
        features.Children.Add(Feature("\uE7B8", Ui.Accent, Ui.AccentSoft, "One-click backups", "Compress the source folder with WinRAR and save it to your backup drive."));
        features.Children.Add(Feature("\uE81C", Ui.Purple, Ui.PurpleSoft, "Restore anywhere", "Pick a previous archive and extract it on this PC or another device."));
        features.Children.Add(Feature("\uE895", Ui.Success, Ui.SuccessSoft, "Shared settings", "Keep one settings file on a synced drive and use it everywhere."));
        panel.Children.Add(features);

        var note = new Border
        {
            Background = Ui.Surface,
            BorderBrush = Ui.Line,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(12, 9, 12, 9),
            Margin = new Thickness(0, 20, 0, 0)
        };
        var noteRow = new Grid();
        noteRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        noteRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        var infoIcon = Ui.Icon("\uE946", 13, Ui.Accent);
        infoIcon.Margin = new Thickness(0, 2, 8, 0);
        infoIcon.VerticalAlignment = VerticalAlignment.Top;
        noteRow.Children.Add(infoIcon);
        var noteText = Ui.T("Auto Backup closes itself automatically when the task is finished.", 11.5, Ui.Muted);
        noteText.TextWrapping = TextWrapping.Wrap;
        noteText.VerticalAlignment = VerticalAlignment.Top;
        Grid.SetColumn(noteText, 1);
        noteRow.Children.Add(noteText);
        note.Child = noteRow;
        panel.Children.Add(note);
        return panel;
    }

    private UIElement BuildSettingsPage(Button browseButton)
    {
        var panel = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        var title = Ui.Display("Shared settings", 24, FontWeights.SemiBold);
        panel.Children.Add(title);
        var subtitle = Ui.T("Choose a folder for your settings file. Put it on a drive that is synced between your devices, so every PC uses the same setup.", 12.5, Ui.Muted);
        subtitle.TextWrapping = TextWrapping.Wrap;
        subtitle.LineHeight = 20;
        subtitle.Margin = new Thickness(0, 8, 0, 0);
        panel.Children.Add(subtitle);

        var row = Ui.PathInput(_settingsFolderBox, browseButton);
        var field = Ui.Field("Settings folder", row, "Example: D:\\Sync\\AutoBackup or a cloud drive folder.");
        field.Margin = new Thickness(0, 24, 0, 0);
        panel.Children.Add(field);

        var preview = new Border
        {
            Background = Ui.Bg2,
            BorderBrush = Ui.Line,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(13, 10, 13, 10),
            Margin = new Thickness(0, 2, 0, 0)
        };
        var previewStack = new StackPanel();
        var previewLabel = Ui.Label("Settings file", Ui.Dim);
        previewLabel.Margin = new Thickness(0, 0, 0, 4);
        previewStack.Children.Add(previewLabel);
        previewStack.Children.Add(_settingsPreview);
        preview.Child = previewStack;
        panel.Children.Add(preview);

        _existingNotice.Margin = new Thickness(2, 12, 0, 0);
        panel.Children.Add(_existingNotice);
        return panel;
    }

    private UIElement BuildFoldersPage(Button backupBrowse, Grid winrarGrid)
    {
        var stack = new StackPanel();
        stack.Children.Add(Ui.Display("Folders & WinRAR", 24, FontWeights.SemiBold));
        var subtitle = Ui.T("These addresses are asked only once. After this, the Backup button does everything for you.", 12.5, Ui.Muted);
        subtitle.TextWrapping = TextWrapping.Wrap;
        subtitle.LineHeight = 20;
        subtitle.Margin = new Thickness(0, 8, 0, 0);
        stack.Children.Add(subtitle);

        var sourceField = Ui.Field("Source folders", _sources, "Add every folder you want to back up. Each folder is saved as its own archive.");
        sourceField.Margin = new Thickness(0, 22, 0, 18);
        stack.Children.Add(sourceField);
        stack.Children.Add(Ui.Field("Backup folder", Ui.PathInput(_backupBox, backupBrowse), "Archives are saved here. The folder is created automatically if it does not exist."));
        stack.Children.Add(Ui.Field("WinRAR", winrarGrid, null));
        _winrarStatus.Margin = new Thickness(2, -8, 0, 0);
        stack.Children.Add(_winrarStatus);

        var note = Ui.T("If the backup folder is inside the source folder, it is excluded from the archive automatically.", 11.5, Ui.Dim);
        note.TextWrapping = TextWrapping.Wrap;
        note.Margin = new Thickness(2, 12, 0, 0);
        stack.Children.Add(note);

        return Ui.ScrollHost(stack);
    }

    private static Grid Feature(string glyph, Brush color, Brush background, string title, string description)
    {
        var row = new Grid { Margin = new Thickness(0, 0, 0, 14) };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        var icon = Ui.IconTile(glyph, color, background, 36, 15);
        icon.Margin = new Thickness(0, 2, 12, 0);
        row.Children.Add(icon);
        var text = new StackPanel();
        text.Children.Add(Ui.T(title, 12.5, FontWeights.SemiBold, Ui.Text));
        var desc = Ui.T(description, 11.5, Ui.Muted);
        desc.TextWrapping = TextWrapping.Wrap;
        desc.Margin = new Thickness(0, 3, 0, 0);
        text.Children.Add(desc);
        Grid.SetColumn(text, 1);
        row.Children.Add(text);
        return row;
    }

    private StepBadge BuildStepItem(string number, string label)
    {
        var root = new Grid { Margin = new Thickness(0, 0, 0, 16) };
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        var circle = new Border
        {
            Width = 28,
            Height = 28,
            CornerRadius = new CornerRadius(14),
            BorderThickness = new Thickness(1),
            BorderBrush = Ui.Line,
            Background = Ui.Surface,
            Child = Ui.T(number, 11.5, FontWeights.SemiBold, Ui.Dim),
            VerticalAlignment = VerticalAlignment.Center
        };
        root.Children.Add(circle);
        var text = Ui.T(label, 12.5, Ui.Dim);
        text.Margin = new Thickness(11, 0, 0, 0);
        Grid.SetColumn(text, 1);
        root.Children.Add(text);
        return new StepBadge { Root = root, Circle = circle, Number = number, Text = text };
    }

    private void GoToStep(int step)
    {
        _step = Math.Max(0, Math.Min(_pages.Length - 1, step));
        SetError(null);
        _pageHost.Content = _pages[_step];
        Ui.FadeIn(_pages[_step], 6);
        _backButton.Visibility = _step > 0 ? Visibility.Visible : Visibility.Collapsed;
        if (_step == 0) { SetNext("Get started", "\uE76C"); }
        else if (_step == 1) { SetNext("Continue", "\uE76C"); }
        else { SetNext("Finish setup", "\uE73E"); }
        for (var i = 0; i < _steps.Count; i++)
        {
            var badge = _steps[i];
            var isActive = i == _step;
            var isDone = i < _step;
            badge.Circle.Background = isDone ? Ui.SuccessSoft : (isActive ? Ui.Accent : Ui.Surface);
            badge.Circle.BorderBrush = isDone ? Ui.LineStrong : (isActive ? Ui.Accent : Ui.Line);
            var circleText = badge.Circle.Child as TextBlock;
            if (circleText != null)
            {
                circleText.Text = isDone ? "\uE73E" : badge.Number;
                circleText.FontFamily = isDone ? Ui.IconFont : Ui.Font;
                circleText.Foreground = isDone ? Ui.Success : (isActive ? Ui.OnAccent : Ui.Dim);
            }
            badge.Text.Foreground = isActive ? Ui.Text : (isDone ? Ui.Muted : Ui.Dim);
            badge.Text.FontWeight = isActive ? FontWeights.SemiBold : FontWeights.Normal;
        }
    }

    private void SetNext(string label, string glyph)
    {
        _nextLabel.Text = label;
        var panel = _nextButton.Content as StackPanel;
        if (panel != null && panel.Children.Count > 0)
        {
            var icon = panel.Children[0] as TextBlock;
            if (icon != null) { icon.Text = glyph; }
        }
        AutomationProperties.SetName(_nextButton, label);
    }

    private void Next()
    {
        if (_step == 0)
        {
            GoToStep(1);
            return;
        }
        if (_step == 1)
        {
            var folder = AppConfig.CleanPath(_settingsFolderBox.Text);
            if (string.IsNullOrEmpty(folder))
            {
                SetError("Choose a folder for the shared settings file.");
                return;
            }
            string settingsPath;
            try
            {
                settingsPath = Path.GetFullPath(ConfigStore.BuildSettingsPath(folder));
            }
            catch (Exception ex)
            {
                SetError("The settings folder path is not valid. " + ex.Message);
                return;
            }
            if (File.Exists(settingsPath))
            {
                AppConfig loaded;
                string error;
                if (!ConfigStore.TryReadSettings(settingsPath, out loaded, out error))
                {
                    SetError("The settings file in this folder could not be read. " + error);
                    return;
                }
                _draft = loaded;
                _sources.SetFolders(loaded.SourceFolders);
                _backupBox.Text = loaded.BackupFolder;
                if (WinRarLocator.IsValid(loaded.WinRarPath))
                {
                    _winrarBox.Text = loaded.WinRarPath;
                }
                else if (string.IsNullOrEmpty(_winrarBox.Text))
                {
                    var detected = WinRarLocator.Detect();
                    if (!string.IsNullOrEmpty(detected)) { _winrarBox.Text = detected; }
                }
            }
            else
            {
                _draft = null;
            }
            _settingsPath = settingsPath;
            GoToStep(2);
            return;
        }

        Finish();
    }

    private void Finish()
    {
        if (string.IsNullOrEmpty(_settingsPath))
        {
            SetError("Choose a settings folder before finishing.");
            GoToStep(1);
            return;
        }
        var folders = new List<string>(_sources.Folders);
        var backup = AppConfig.CleanPath(_backupBox.Text);
        var winrar = WinRarLocator.Normalize(_winrarBox.Text);

        if (folders.Count == 0)
        {
            SetError("Add at least one source folder.");
            return;
        }
        foreach (var folder in folders)
        {
            if (!Directory.Exists(folder))
            {
                SetError("A source folder does not exist:\n" + folder);
                return;
            }
        }
        if (string.IsNullOrEmpty(backup))
        {
            SetError("Choose a backup folder.");
            return;
        }
        try
        {
            backup = Path.GetFullPath(backup);
        }
        catch (Exception ex)
        {
            SetError("The backup folder path is not valid. " + ex.Message);
            return;
        }
        foreach (var folder in folders)
        {
            if (string.Equals(folder.TrimEnd('\\'), backup.TrimEnd('\\'), StringComparison.OrdinalIgnoreCase))
            {
                SetError("The backup folder must be different from the source folders.");
                return;
            }
        }
        if (!WinRarLocator.IsValid(winrar))
        {
            SetError("WinRAR was not found. Install WinRAR, click Auto-detect, or choose Rar.exe manually.");
            return;
        }

        var config = (_draft ?? new AppConfig()).Clone();
        config.SourceFolders = folders;
        config.SourceFolder = folders[0];
        config.BackupFolder = backup;
        config.WinRarPath = winrar;

        var jobs = new List<BackupJob>();
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var folder in folders)
        {
            var baseName = Path.GetFileName(folder.TrimEnd('\\'));
            if (string.IsNullOrEmpty(baseName))
            {
                baseName = "Backup";
            }
            var name = baseName;
            var suffix = 2;
            while (!names.Add(name))
            {
                name = baseName + " (" + suffix.ToString() + ")";
                suffix++;
            }
            jobs.Add(new BackupJob { Name = name, SourceFolder = folder, BackupFolder = backup });
        }
        config.Jobs = jobs;
        try
        {
            var folder = Path.GetDirectoryName(_settingsPath);
            if (!string.IsNullOrEmpty(folder)) { Directory.CreateDirectory(folder); }
            ConfigStore.SaveSettings(config, _settingsPath);
            ConfigStore.SaveBootstrap(_settingsPath);
        }
        catch (Exception ex)
        {
            SetError("The settings could not be saved. " + ex.Message);
            return;
        }

        var handler = Completed;
        if (handler != null)
        {
            handler(config, _settingsPath);
        }
    }

    private void BrowseSettingsFolder()
    {
        BrowseInto(_settingsFolderBox, "Choose a folder for the shared settings file");
    }

    private void BrowseInto(TextBox box, string title)
    {
        try
        {
            var initial = AppConfig.CleanPath(box.Text);
            if (string.IsNullOrEmpty(initial) || !Directory.Exists(initial)) { initial = ConfigStore.SuggestedSettingsFolder; }
            var folder = FolderPicker.Pick(Window.GetWindow(this), title, initial);
            if (!string.IsNullOrEmpty(folder))
            {
                box.Text = folder;
                SetError(null);
            }
        }
        catch (Exception ex)
        {
            SetError("The folder picker could not be opened. " + ex.Message);
        }
    }

    private void BrowseWinRar()
    {
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
                SetError(null);
            }
        }
        catch (Exception ex)
        {
            SetError("WinRAR could not be selected. " + ex.Message);
        }
    }

    private void DetectWinRar()
    {
        var detected = WinRarLocator.Detect();
        if (string.IsNullOrEmpty(detected))
        {
            SetError("WinRAR was not found in the usual locations. Choose Rar.exe manually.");
            return;
        }
        _winrarBox.Text = detected;
        SetError(null);
    }

    private void RefreshSettingsNotice()
    {
        var folder = AppConfig.CleanPath(_settingsFolderBox.Text);
        if (string.IsNullOrEmpty(folder))
        {
            _settingsPreview.Text = "Choose a folder to store AutoBackup.settings.json.";
            _settingsPreview.Foreground = Ui.Dim;
            _existingNotice.Text = string.Empty;
            _existingNotice.Visibility = Visibility.Collapsed;
            return;
        }
        string path;
        try
        {
            path = Path.GetFullPath(ConfigStore.BuildSettingsPath(folder));
        }
        catch (Exception ex)
        {
            _settingsPreview.Text = ex.Message;
            _settingsPreview.Foreground = Ui.Danger;
            _existingNotice.Visibility = Visibility.Collapsed;
            return;
        }
        _settingsPreview.Text = path;
        _settingsPreview.Foreground = Ui.Muted;
        _settingsPreview.ToolTip = path;
        if (File.Exists(path))
        {
            AppConfig loaded;
            string error;
            if (ConfigStore.TryReadSettings(path, out loaded, out error))
            {
                _existingNotice.Text = "Existing settings found here. They will be loaded when you continue.";
                _existingNotice.Foreground = Ui.Success;
            }
            else
            {
                _existingNotice.Text = "A settings file exists here, but it could not be read: " + error;
                _existingNotice.Foreground = Ui.Warning;
            }
            _existingNotice.Visibility = Visibility.Visible;
        }
        else
        {
            _existingNotice.Text = Directory.Exists(folder)
                ? "A new settings file will be created here."
                : "This folder does not exist yet and will be created.";
            _existingNotice.Foreground = Ui.Dim;
            _existingNotice.Visibility = Visibility.Visible;
        }
    }

    private void RefreshWinRarStatus()
    {
        var path = WinRarLocator.Normalize(_winrarBox.Text);
        if (WinRarLocator.IsValid(path))
        {
            _winrarStatus.Text = "WinRAR is ready: " + path;
            _winrarStatus.Foreground = Ui.Success;
        }
        else if (string.IsNullOrWhiteSpace(path))
        {
            _winrarStatus.Text = "WinRAR was not found automatically. Choose Rar.exe, or install WinRAR first.";
            _winrarStatus.Foreground = Ui.Warning;
        }
        else
        {
            _winrarStatus.Text = "Rar.exe was not found at this path. Use Auto-detect or choose another file.";
            _winrarStatus.Foreground = Ui.Warning;
        }
    }

    private void SetError(string message)
    {
        if (string.IsNullOrEmpty(message))
        {
            _errorText.Text = string.Empty;
            _errorText.Visibility = Visibility.Collapsed;
        }
        else
        {
            _errorText.Text = message;
            _errorText.Visibility = Visibility.Visible;
        }
    }

    private sealed class StepBadge
    {
        public Grid Root;
        public Border Circle;
        public TextBlock Text;
        public string Number;
    }
}
