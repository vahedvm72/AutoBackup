using System;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using AutoBackup.Services;
using AutoBackup.UI;

namespace AutoBackup;

public static class DialogService
{
    public static void Alert(Window owner, string title, string message, bool error = false)
    {
        var window = new AlertWindow(title, message, error) { Owner = owner, WindowStartupLocation = WindowStartupLocation.CenterOwner };
        window.ShowDialog();
    }

    public static bool Confirm(Window owner, string title, string message, string confirmLabel, bool danger = false)
    {
        var window = new ConfirmWindow(title, message, confirmLabel, danger) { Owner = owner, WindowStartupLocation = WindowStartupLocation.CenterOwner };
        return window.ShowDialog() == true;
    }

    public static RestoreWindow ShowRestore(Window owner, ArchiveInfo archive, AppConfig config, string settingsPath = null)
    {
        var window = new RestoreWindow(archive, config, settingsPath) { Owner = owner, WindowStartupLocation = WindowStartupLocation.CenterOwner };
        return window.ShowDialog() == true ? window : null;
    }

    public static string EditFolder(
        Window owner,
        string title,
        string description,
        string initialPath,
        bool requireExisting,
        Func<string, string> validator,
        string confirmLabel = "Save")
    {
        var window = new ChangeFolderWindow(title, description, initialPath, requireExisting, validator, confirmLabel)
        {
            Owner = owner,
            WindowStartupLocation = WindowStartupLocation.CenterOwner
        };
        return window.ShowDialog() == true ? window.SelectedPath : null;
    }

    public static BackupJob ShowJob(Window owner, string title, BackupJob initial, Func<BackupJob, string> validator)
    {
        var window = new EditJobWindow(title, initial, validator)
        {
            Owner = owner,
            WindowStartupLocation = WindowStartupLocation.CenterOwner
        };
        return window.ShowDialog() == true ? window.Result : null;
    }
}

public sealed class EditJobWindow : AutoBackup.UI.ChromeWindow
{
    private readonly TextBlock _error;
    private readonly Func<BackupJob, string> _validator;
    private readonly TextBox _name;
    private readonly TextBox _source;
    private readonly TextBox _backup;

    public BackupJob Result { get; private set; }

    public EditJobWindow(string title, BackupJob initial, Func<BackupJob, string> validator)
        : base(AppInfo.Name, 660, 500, false, false, false)
    {
        _validator = validator;
        ShowInTaskbar = false;
        initial = initial ?? new BackupJob();

        var root = new Grid { Margin = new Thickness(28, 24, 28, 22) };
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var header = new StackPanel();
        header.Children.Add(Ui.Display(title, 21, FontWeights.SemiBold));
        var subtitle = Ui.T("Choose a name, the main folder to compress, and the folder where the archive is saved.", 12.5, Ui.Muted);
        subtitle.TextWrapping = TextWrapping.Wrap;
        subtitle.LineHeight = 19;
        subtitle.Margin = new Thickness(0, 7, 0, 0);
        header.Children.Add(subtitle);
        Grid.SetRow(header, 0);
        root.Children.Add(header);

        var form = new StackPanel { Margin = new Thickness(0, 18, 0, 0) };

        _name = Ui.Input(initial.Name ?? string.Empty, "Job name");
        var nameField = Ui.Field("Name", _name, "A name you choose, for example \"My documents\".");
        nameField.Margin = new Thickness(0, 0, 0, 14);
        form.Children.Add(nameField);

        _source = Ui.Input(initial.SourceFolder ?? string.Empty, "Job main folder");
        var sourceBrowse = Ui.Button("Browse", "\uE8B7", ButtonKind.Secondary, delegate { BrowseInto(_source, "Choose the main folder"); });
        sourceBrowse.MinWidth = 104;
        AutomationProperties.SetName(sourceBrowse, "Browse job main folder");
        var sourceField = Ui.Field("Main folder", Ui.PathInput(_source, sourceBrowse), "The folder that is compressed into the archive.");
        sourceField.Margin = new Thickness(0, 0, 0, 14);
        form.Children.Add(sourceField);

        _backup = Ui.Input(initial.BackupFolder ?? string.Empty, "Job backup folder");
        var backupBrowse = Ui.Button("Browse", "\uE8B7", ButtonKind.Secondary, delegate { BrowseInto(_backup, "Choose the backup folder"); });
        backupBrowse.MinWidth = 104;
        AutomationProperties.SetName(backupBrowse, "Browse job backup folder");
        var backupField = Ui.Field("Backup folder", Ui.PathInput(_backup, backupBrowse), "Archives are saved here. The folder is created automatically if needed.");
        backupField.Margin = new Thickness(0, 0, 0, 0);
        form.Children.Add(backupField);

        _error = Ui.T(string.Empty, 11.5, Ui.Danger);
        _error.TextWrapping = TextWrapping.Wrap;
        _error.Margin = new Thickness(2, 10, 0, 0);
        _error.Visibility = Visibility.Collapsed;
        form.Children.Add(_error);
        Grid.SetRow(form, 1);
        root.Children.Add(form);

        var cancel = Ui.Button("Cancel", null, ButtonKind.Secondary, delegate { Close(); });
        cancel.MinWidth = 96;
        cancel.Margin = new Thickness(0, 0, 10, 0);
        var save = Ui.Button("Save", "\uE73E", ButtonKind.Primary, Accept);
        save.MinWidth = 120;
        AutomationProperties.SetName(save, "Save job");
        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 16, 0, 0)
        };
        buttons.Children.Add(cancel);
        buttons.Children.Add(save);
        Grid.SetRow(buttons, 2);
        root.Children.Add(buttons);

        ViewHost.Content = root;
        Loaded += delegate { _name.Focus(); _name.CaretIndex = _name.Text.Length; };
        KeyDown += delegate(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape) { Close(); }
        };
    }

    private void BrowseInto(TextBox box, string title)
    {
        try
        {
            var initial = AppConfig.CleanPath(box.Text);
            if (string.IsNullOrEmpty(initial) || !Directory.Exists(initial))
            {
                initial = ConfigStore.SuggestedSettingsFolder;
            }
            var folder = FolderPicker.Pick(this, title, initial);
            if (!string.IsNullOrEmpty(folder))
            {
                box.Text = folder;
                box.CaretIndex = box.Text.Length;
                SetError(null);
            }
        }
        catch (Exception ex)
        {
            SetError("The folder picker could not be opened. " + ex.Message);
        }
    }

    private void Accept()
    {
        var name = (_name.Text ?? string.Empty).Trim();
        var source = AppConfig.CleanPath(_source.Text);
        var backup = AppConfig.CleanPath(_backup.Text);
        if (string.IsNullOrEmpty(name))
        {
            SetError("Choose a name for this backup.");
            return;
        }
        if (string.IsNullOrEmpty(source))
        {
            SetError("Choose the main folder.");
            return;
        }
        try
        {
            source = Path.GetFullPath(source);
            var root = Path.GetPathRoot(source);
            if (!string.IsNullOrEmpty(root) && source.Length > root.Length)
            {
                source = source.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            }
        }
        catch (Exception ex)
        {
            SetError("The main folder path is not valid. " + ex.Message);
            return;
        }
        if (!Directory.Exists(source))
        {
            SetError("The main folder does not exist:\n" + source);
            return;
        }
        if (string.IsNullOrEmpty(backup))
        {
            SetError("Choose the backup folder.");
            return;
        }
        try
        {
            backup = Path.GetFullPath(backup);
            var root = Path.GetPathRoot(backup);
            if (!string.IsNullOrEmpty(root) && backup.Length > root.Length)
            {
                backup = backup.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            }
        }
        catch (Exception ex)
        {
            SetError("The backup folder path is not valid. " + ex.Message);
            return;
        }
        if (string.Equals(source, backup, StringComparison.OrdinalIgnoreCase))
        {
            SetError("The backup folder must be different from the main folder.");
            return;
        }
        var job = new BackupJob { Name = name, SourceFolder = source, BackupFolder = backup };
        if (_validator != null)
        {
            var error = _validator(job);
            if (!string.IsNullOrEmpty(error))
            {
                SetError(error);
                return;
            }
        }
        Result = job;
        DialogResult = true;
    }

    private void SetError(string message)
    {
        if (string.IsNullOrEmpty(message))
        {
            _error.Text = string.Empty;
            _error.Visibility = Visibility.Collapsed;
        }
        else
        {
            _error.Text = message;
            _error.Visibility = Visibility.Visible;
        }
    }
}

public sealed class ChangeFolderWindow : AutoBackup.UI.ChromeWindow
{
    private readonly TextBox _path;
    private readonly TextBlock _error;
    private readonly bool _requireExisting;
    private readonly Func<string, string> _validator;

    public string SelectedPath { get; private set; }

    public ChangeFolderWindow(string title, string description, string initialPath, bool requireExisting, Func<string, string> validator, string confirmLabel = "Save")
        : base(AppInfo.Name, 620, 380, false, false, false)
    {
        _requireExisting = requireExisting;
        _validator = validator;
        ShowInTaskbar = false;

        var root = new Grid { Margin = new Thickness(28, 24, 28, 22) };
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var header = new StackPanel();
        header.Children.Add(Ui.Display(title, 21, FontWeights.SemiBold));
        var subtitle = Ui.T(description, 12.5, Ui.Muted);
        subtitle.TextWrapping = TextWrapping.Wrap;
        subtitle.LineHeight = 19;
        subtitle.Margin = new Thickness(0, 7, 0, 0);
        header.Children.Add(subtitle);
        Grid.SetRow(header, 0);
        root.Children.Add(header);

        var form = new StackPanel { Margin = new Thickness(0, 20, 0, 0) };
        _path = Ui.Input(initialPath ?? string.Empty, "Folder path");
        var browse = Ui.Button("Browse", "\uE8B7", ButtonKind.Secondary, Browse);
        browse.MinWidth = 104;
        AutomationProperties.SetName(browse, "Browse folder");
        var field = Ui.Field("Folder path", Ui.PathInput(_path, browse), null);
        field.Margin = new Thickness(0, 0, 0, 0);
        form.Children.Add(field);

        _error = Ui.T(string.Empty, 11.5, Ui.Danger);
        _error.TextWrapping = TextWrapping.Wrap;
        _error.Margin = new Thickness(2, 10, 0, 0);
        _error.Visibility = Visibility.Collapsed;
        form.Children.Add(_error);
        Grid.SetRow(form, 1);
        root.Children.Add(form);

        var cancel = Ui.Button("Cancel", null, ButtonKind.Secondary, delegate { Close(); });
        cancel.MinWidth = 96;
        cancel.Margin = new Thickness(0, 0, 10, 0);
        var save = Ui.Button(string.IsNullOrEmpty(confirmLabel) ? "Save" : confirmLabel, "\uE73E", ButtonKind.Primary, Accept);
        save.MinWidth = 120;
        AutomationProperties.SetName(save, "Save folder");
        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 16, 0, 0)
        };
        buttons.Children.Add(cancel);
        buttons.Children.Add(save);
        Grid.SetRow(buttons, 2);
        root.Children.Add(buttons);

        ViewHost.Content = root;
        Loaded += delegate
        {
            _path.Focus();
            _path.CaretIndex = _path.Text.Length;
        };
        KeyDown += delegate(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape) { Close(); }
        };
    }

    private void Browse()
    {
        try
        {
            var initial = AppConfig.CleanPath(_path.Text);
            if (string.IsNullOrEmpty(initial) || !Directory.Exists(initial))
            {
                initial = ConfigStore.SuggestedSettingsFolder;
            }
            var folder = FolderPicker.Pick(this, "Choose a folder", initial);
            if (!string.IsNullOrEmpty(folder))
            {
                _path.Text = folder;
                _path.CaretIndex = _path.Text.Length;
                SetError(null);
            }
        }
        catch (Exception ex)
        {
            SetError("The folder picker could not be opened. " + ex.Message);
        }
    }

    private void Accept()
    {
        var value = AppConfig.CleanPath(_path.Text);
        if (string.IsNullOrEmpty(value))
        {
            SetError("Choose a folder.");
            return;
        }
        try
        {
            var full = Path.GetFullPath(value);
            var root = Path.GetPathRoot(full);
            value = !string.IsNullOrEmpty(root) && full.Length > root.Length
                ? full.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                : full;
        }
        catch (Exception ex)
        {
            SetError("The folder path is not valid. " + ex.Message);
            return;
        }
        if (_requireExisting && !Directory.Exists(value))
        {
            SetError("This folder does not exist:\n" + value);
            return;
        }
        if (_validator != null)
        {
            var error = _validator(value);
            if (!string.IsNullOrEmpty(error))
            {
                SetError(error);
                return;
            }
        }
        SelectedPath = value;
        DialogResult = true;
    }

    private void SetError(string message)
    {
        if (string.IsNullOrEmpty(message))
        {
            _error.Text = string.Empty;
            _error.Visibility = Visibility.Collapsed;
        }
        else
        {
            _error.Text = message;
            _error.Visibility = Visibility.Visible;
        }
    }
}

public sealed class AlertWindow : AutoBackup.UI.ChromeWindow
{
    public AlertWindow(string title, string message, bool error)
        : base(AppInfo.Name, 520, 320, false, false, false)
    {
        ShowInTaskbar = false;
        var root = new Grid { Margin = new Thickness(26, 22, 26, 22) };
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var content = new Grid();
        content.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        content.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        var icon = Ui.IconTile(error ? "\uE783" : "\uE946", error ? Ui.Danger : Ui.Accent, error ? Ui.DangerSoft : Ui.AccentSoft, 44, 20);
        icon.VerticalAlignment = VerticalAlignment.Top;
        content.Children.Add(icon);
        var text = new Grid { Margin = new Thickness(16, 0, 0, 0) };
        text.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        text.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        var titleBlock = Ui.Display(title, 18, FontWeights.SemiBold);
        titleBlock.TextWrapping = TextWrapping.Wrap;
        text.Children.Add(titleBlock);
        var messageBlock = Ui.T(message, 13, Ui.Muted);
        messageBlock.TextWrapping = TextWrapping.Wrap;
        messageBlock.VerticalAlignment = VerticalAlignment.Top;
        var messageScroll = new ScrollViewer
        {
            Content = messageBlock,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Margin = new Thickness(0, 8, 0, 0),
            Focusable = false
        };
        Grid.SetRow(messageScroll, 1);
        text.Children.Add(messageScroll);
        Grid.SetColumn(text, 1);
        content.Children.Add(text);
        Grid.SetRow(content, 0);
        root.Children.Add(content);

        var ok = Ui.Button("OK", null, ButtonKind.Primary, delegate { Close(); });
        ok.MinWidth = 92;
        ok.HorizontalAlignment = HorizontalAlignment.Right;
        var buttonRow = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 18, 0, 0) };
        buttonRow.Children.Add(ok);
        Grid.SetRow(buttonRow, 1);
        root.Children.Add(buttonRow);

        ViewHost.Content = root;
        KeyDown += delegate(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape || e.Key == Key.Enter) { Close(); }
        };
    }
}

public sealed class ConfirmWindow : AutoBackup.UI.ChromeWindow
{
    private bool _result;

    public ConfirmWindow(string title, string message, string confirmLabel, bool danger)
        : base(AppInfo.Name, 540, 310, false, false, false)
    {
        ShowInTaskbar = false;
        var root = new Grid { Margin = new Thickness(26, 22, 26, 22) };
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var content = new Grid();
        content.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        content.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        var icon = Ui.IconTile(danger ? "\uE74D" : "\uE946", danger ? Ui.Danger : Ui.Accent, danger ? Ui.DangerSoft : Ui.AccentSoft, 44, 19);
        icon.VerticalAlignment = VerticalAlignment.Top;
        content.Children.Add(icon);
        var text = new StackPanel { Margin = new Thickness(16, 0, 0, 0) };
        var titleBlock = Ui.Display(title, 18, FontWeights.SemiBold);
        titleBlock.TextWrapping = TextWrapping.Wrap;
        text.Children.Add(titleBlock);
        var messageBlock = Ui.T(message, 13, Ui.Muted);
        messageBlock.TextWrapping = TextWrapping.Wrap;
        messageBlock.Margin = new Thickness(0, 8, 0, 0);
        messageBlock.VerticalAlignment = VerticalAlignment.Top;
        text.Children.Add(messageBlock);
        Grid.SetColumn(text, 1);
        content.Children.Add(text);
        Grid.SetRow(content, 0);
        root.Children.Add(content);

        var cancel = Ui.Button("Cancel", null, ButtonKind.Secondary, delegate { _result = false; Close(); });
        var confirm = Ui.Button(confirmLabel, null, ButtonKind.Primary, delegate { _result = true; Close(); });
        cancel.MinWidth = 92;
        confirm.MinWidth = 110;
        cancel.Margin = new Thickness(0, 0, 10, 0);
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 18, 0, 0) };
        buttons.Children.Add(cancel);
        buttons.Children.Add(confirm);
        Grid.SetRow(buttons, 1);
        root.Children.Add(buttons);

        ViewHost.Content = root;
        KeyDown += delegate(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape) { _result = false; Close(); }
        };
    }

    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        if (DialogResult == null) { try { DialogResult = _result; } catch { } }
        base.OnClosing(e);
    }
}

public sealed class RestoreWindow : AutoBackup.UI.ChromeWindow
{
    private readonly TextBox _destination;
    private readonly TextBlock _validation;
    private readonly TextBlock _notice;
    private readonly string _originalSource;
    private readonly AppConfig _config;
    private readonly string _settingsPath;

    public string Destination { get; private set; }

    public RestoreWindow(ArchiveInfo archive, AppConfig config, string settingsPath = null)
        : base(AppInfo.Name, 600, 500, false, false, false)
    {
        _config = config;
        _settingsPath = settingsPath;
        _originalSource = ArchiveService.FindSourceForArchive(config, archive.Name);
        ShowInTaskbar = false;

        var root = new Grid { Margin = new Thickness(28, 22, 28, 22) };
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var header = new StackPanel();
        header.Children.Add(Ui.Display("Restore backup", 22, FontWeights.SemiBold));
        var archiveName = Ui.T(archive.Name, 12.5, Ui.Muted);
        archiveName.TextTrimming = TextTrimming.CharacterEllipsis;
        archiveName.Margin = new Thickness(0, 6, 0, 0);
        archiveName.ToolTip = archive.FullPath;
        header.Children.Add(archiveName);
        var meta = Ui.T(archive.DateText + "   \u2022   " + archive.SizeText, 11.5, Ui.Dim);
        meta.Margin = new Thickness(0, 4, 0, 0);
        header.Children.Add(meta);
        Grid.SetRow(header, 0);
        root.Children.Add(header);

        var form = new StackPanel { Margin = new Thickness(0, 20, 0, 0) };
        _destination = Ui.Input(_originalSource ?? string.Empty, "Restore destination folder");
        var browse = Ui.Button("Browse", "\uE8B7", ButtonKind.Secondary, Browse);
        browse.MinWidth = 104;
        AutomationProperties.SetName(browse, "Browse restore destination");
        var inputRow = Ui.PathInput(_destination, browse);
        form.Children.Add(Ui.Field("Destination folder", inputRow, "The folder will be cleared first: its current files are deleted, then the archive contents are copied in."));

        _notice = Ui.T(string.Empty, 11.5, Ui.Warning);
        _notice.TextWrapping = TextWrapping.Wrap;
        _notice.Margin = new Thickness(2, -4, 0, 0);
        _notice.Visibility = Visibility.Collapsed;
        form.Children.Add(_notice);

        var actions = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 8, 0, 0) };
        var useOriginal = Ui.Button("Use original source folder", null, ButtonKind.Link, delegate
        {
            if (!string.IsNullOrEmpty(_originalSource))
            {
                _destination.Text = _originalSource;
            }
        });
        AutomationProperties.SetName(useOriginal, "Use original source folder");
        actions.Children.Add(useOriginal);
        form.Children.Add(actions);

        _validation = Ui.T(string.Empty, 11.5, Ui.Danger);
        _validation.TextWrapping = TextWrapping.Wrap;
        _validation.Margin = new Thickness(2, 10, 0, 0);
        _validation.Visibility = Visibility.Collapsed;
        form.Children.Add(_validation);
        Grid.SetRow(form, 1);
        root.Children.Add(form);

        var cancel = Ui.Button("Cancel", null, ButtonKind.Secondary, delegate { Close(); });
        cancel.MinWidth = 96;
        cancel.Margin = new Thickness(0, 0, 10, 0);
        var restore = Ui.Button("Restore", "\uE896", ButtonKind.Primary, Accept);
        restore.MinWidth = 122;
        AutomationProperties.SetName(restore, "Restore");
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 14, 0, 0) };
        buttons.Children.Add(cancel);
        buttons.Children.Add(restore);
        Grid.SetRow(buttons, 2);
        root.Children.Add(buttons);

        _destination.TextChanged += delegate { CheckDestinationNotice(); };
        CheckDestinationNotice();
        ViewHost.Content = root;
        Loaded += delegate { _destination.Focus(); _destination.CaretIndex = 0; };
        KeyDown += delegate(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape) { Close(); }
        };
    }

    private void Browse()
    {
        try
        {
            var folder = FolderPicker.Pick(this, "Choose a restore destination", _destination.Text);
            if (!string.IsNullOrEmpty(folder))
            {
                _destination.Text = folder;
                _validation.Visibility = Visibility.Collapsed;
            }
        }
        catch (Exception ex)
        {
            DialogService.Alert(this, "Folder could not be opened", ex.Message, true);
        }
    }

    private void CheckDestinationNotice()
    {
        try
        {
            var path = AppConfig.CleanPath(_destination.Text);
            var hasItems = false;
            if (!string.IsNullOrEmpty(path) && Directory.Exists(path))
            {
                using (var entries = Directory.EnumerateFileSystemEntries(path).GetEnumerator())
                {
                    hasItems = entries.MoveNext();
                }
            }
            if (hasItems)
            {
                _notice.Text = "This folder is not empty. Its current contents will be deleted before the restore.";
                _notice.Visibility = Visibility.Visible;
            }
            else
            {
                _notice.Visibility = Visibility.Collapsed;
            }
        }
        catch
        {
            _notice.Visibility = Visibility.Collapsed;
        }
    }

    private void Accept()
    {
        var path = AppConfig.CleanPath(_destination.Text);
        if (string.IsNullOrEmpty(path))
        {
            _validation.Text = "Choose a destination folder.";
            _validation.Visibility = Visibility.Visible;
            return;
        }
        try
        {
            if (File.Exists(path))
            {
                _validation.Text = "The destination is a file. Choose a folder instead.";
                _validation.Visibility = Visibility.Visible;
                return;
            }
            var targetError = ArchiveService.ValidateRestoreTarget(_config, path, _settingsPath);
            if (!string.IsNullOrEmpty(targetError))
            {
                _validation.Text = targetError;
                _validation.Visibility = Visibility.Visible;
                return;
            }
        }
        catch (Exception ex)
        {
            _validation.Text = ex.Message;
            _validation.Visibility = Visibility.Visible;
            return;
        }
        if (HasEntries(path))
        {
            var confirmed = DialogService.Confirm(this, "Replace the folder contents?",
                "Everything currently inside:\n" + path
                + "\n\nwill be deleted, then the backup contents will be copied there. This cannot be undone.",
                "Replace and restore", true);
            if (!confirmed)
            {
                return;
            }
        }
        Destination = path;
        DialogResult = true;
    }

    private static bool HasEntries(string path)
    {
        try
        {
            if (string.IsNullOrEmpty(path) || !Directory.Exists(path))
            {
                return false;
            }
            using (var entries = Directory.EnumerateFileSystemEntries(path).GetEnumerator())
            {
                return entries.MoveNext();
            }
        }
        catch
        {
            return false;
        }
    }
}
