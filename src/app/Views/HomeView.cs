using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using AutoBackup.Services;
using AutoBackup.UI;

namespace AutoBackup.Views;

public sealed class HomeRestore
{
    public ArchiveInfo Archive { get; set; }
    public string TargetFolder { get; set; }
}

public sealed class HomeSelection
{
    public List<BackupJob> BackupJobs { get; set; } = new List<BackupJob>();
    public List<HomeRestore> Restores { get; set; } = new List<HomeRestore>();
}

public sealed class HomeView : UserControl
{
    private readonly AppConfig _config;
    private readonly string _settingsPath;
    private readonly Action _newBackup;
    private readonly Action<BackupJob> _editJob;
    private readonly Action<BackupJob> _deleteJob;
    private readonly Action<HomeSelection> _start;
    private readonly Action _browseBackups;
    private readonly StackPanel _rowsPanel;
    private readonly TextBlock _backupCount;
    private readonly TextBlock _restoreCount;
    private TextBlock _summary;
    private Button _startButton;
    private readonly List<SourceRow> _rows = new List<SourceRow>();
    private bool _scanning;

    public HomeView(
        AppConfig config,
        string settingsPath,
        Action newBackup,
        Action<BackupJob> editJob,
        Action<BackupJob> deleteJob,
        Action<HomeSelection> start,
        Action browseBackups,
        Action settings,
        string notice = null)
    {
        _config = config;
        _settingsPath = settingsPath;
        _newBackup = newBackup;
        _editJob = editJob;
        _deleteJob = deleteJob;
        _start = start;
        _browseBackups = browseBackups;

        var root = new Grid { Margin = new Thickness(30, 20, 30, 20) };
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var header = new Grid { Margin = new Thickness(0, 0, 0, 14) };
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var headerText = new StackPanel();
        headerText.Children.Add(Ui.Display("Backup & Restore", 26, FontWeights.SemiBold));
        var subtitle = Ui.T("Each row has its own name, main folder, backup folder, and restore history.", 13, Ui.Muted);
        subtitle.Margin = new Thickness(0, 6, 0, 0);
        headerText.Children.Add(subtitle);
        header.Children.Add(headerText);

        var newBackupButton = Ui.Button("New Backup", "\uE710", ButtonKind.Secondary, _newBackup);
        newBackupButton.MinWidth = 150;
        newBackupButton.VerticalAlignment = VerticalAlignment.Top;
        AutomationProperties.SetName(newBackupButton, "New Backup");
        Grid.SetColumn(newBackupButton, 1);
        header.Children.Add(newBackupButton);
        Grid.SetRow(header, 0);
        root.Children.Add(header);

        if (!string.IsNullOrEmpty(notice))
        {
            var banner = BuildNotice(notice);
            banner.Margin = new Thickness(0, 0, 0, 12);
            Grid.SetRow(banner, 1);
            root.Children.Add(banner);
        }

        _backupCount = Ui.T(string.Empty, 11, Ui.Dim);
        _restoreCount = Ui.T("Scanning...", 11, Ui.Dim);
        var columnsHead = BuildColumnHeader();
        Grid.SetRow(columnsHead, 2);
        root.Children.Add(columnsHead);

        _rowsPanel = new StackPanel();
        var scroll = Ui.ScrollHost(_rowsPanel);
        Grid.SetRow(scroll, 3);
        root.Children.Add(scroll);

        var footer = BuildFooter(settings);
        footer.Margin = new Thickness(0, 14, 0, 0);
        Grid.SetRow(footer, 4);
        root.Children.Add(footer);

        Content = root;

        RenderRows();
        Loaded += async delegate { await RefreshArchivesAsync(); };
    }

    private Grid BuildColumnHeader()
    {
        var head = new Grid { Margin = new Thickness(0, 0, 0, 10) };
        head.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        head.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(24) });
        head.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var left = new StackPanel { Margin = new Thickness(14, 0, 0, 0) };
        var leftTitle = new Grid();
        leftTitle.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        leftTitle.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        leftTitle.Children.Add(Ui.T("Backup", 13.5, FontWeights.SemiBold, Ui.Text));
        Grid.SetColumn(_backupCount, 1);
        leftTitle.Children.Add(_backupCount);
        left.Children.Add(leftTitle);
        var leftSub = Ui.T("Tick the backups to run.", 10.5, Ui.Dim);
        leftSub.Margin = new Thickness(0, 3, 0, 0);
        left.Children.Add(leftSub);
        head.Children.Add(left);

        var right = new StackPanel { Margin = new Thickness(14, 0, 0, 0) };
        var rightTitle = new Grid();
        rightTitle.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        rightTitle.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        rightTitle.Children.Add(Ui.T("Restore", 13.5, FontWeights.SemiBold, Ui.Text));
        var actions = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        _restoreCount.VerticalAlignment = VerticalAlignment.Center;
        _restoreCount.Margin = new Thickness(0, 0, 6, 0);
        actions.Children.Add(_restoreCount);
        var refresh = Ui.IconButton("\uE72C", "Refresh backups", delegate { _ = RefreshArchivesAsync(); }, ButtonKind.Ghost, 30, 12);
        AutomationProperties.SetName(refresh, "Refresh backups");
        actions.Children.Add(refresh);
        var browse = Ui.IconButton("\uE81C", "Browse Backups", _browseBackups, ButtonKind.Ghost, 30, 13);
        browse.Margin = new Thickness(2, 0, 0, 0);
        AutomationProperties.SetName(browse, "Browse Backups");
        actions.Children.Add(browse);
        Grid.SetColumn(actions, 1);
        rightTitle.Children.Add(actions);
        right.Children.Add(rightTitle);
        var rightSub = Ui.T("Pick one archive per backup to restore to its main folder.", 10.5, Ui.Dim);
        rightSub.Margin = new Thickness(0, 3, 0, 0);
        right.Children.Add(rightSub);
        Grid.SetColumn(right, 2);
        head.Children.Add(right);

        var arrow = Ui.Icon("\uE72A", 11, Ui.Dim);
        arrow.HorizontalAlignment = HorizontalAlignment.Center;
        Grid.SetColumn(arrow, 1);
        head.Children.Add(arrow);
        return head;
    }

    private void RenderRows()
    {
        _rowsPanel.Children.Clear();
        _rows.Clear();

        var jobs = ArchiveService.GetJobs(_config);
        if (jobs.Count == 0)
        {
            var hint = Ui.T("No backups yet. Click New Backup and choose a name, main folder, and backup folder.", 12, Ui.Dim);
            hint.TextWrapping = TextWrapping.Wrap;
            hint.Margin = new Thickness(4, 12, 0, 0);
            _rowsPanel.Children.Add(hint);
            UpdateSelectionState();
            return;
        }

        foreach (var job in jobs)
        {
            var row = BuildSourceRow(job);
            _rows.Add(row);
            _rowsPanel.Children.Add(row.Root);
        }
        UpdateSelectionState();
    }

    private SourceRow BuildSourceRow(BackupJob job)
    {
        var row = new SourceRow { Job = job };

        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(24) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var info = new StackPanel();
        var name = Ui.T(job.Name, 12.5, FontWeights.SemiBold, Ui.Text);
        name.TextTrimming = TextTrimming.CharacterEllipsis;
        name.ToolTip = job.Name;
        info.Children.Add(name);
        var sourceLine = Ui.T("Main: " + job.SourceFolder, 10.5, Ui.Dim);
        sourceLine.TextTrimming = TextTrimming.CharacterEllipsis;
        sourceLine.ToolTip = job.SourceFolder;
        sourceLine.Margin = new Thickness(0, 3, 0, 0);
        info.Children.Add(sourceLine);
        var backupLine = Ui.T("Backup: " + (string.IsNullOrEmpty(job.BackupFolder) ? "not set" : job.BackupFolder), 10.5, Ui.Dim);
        backupLine.TextTrimming = TextTrimming.CharacterEllipsis;
        backupLine.ToolTip = job.BackupFolder;
        backupLine.Margin = new Thickness(0, 2, 0, 0);
        info.Children.Add(backupLine);

        row.BackupCheck = Ui.CheckBox(info, false, "Select " + job.Name);
        row.BackupCheck.Checked += delegate { UpdateSelectionState(); };
        row.BackupCheck.Unchecked += delegate { UpdateSelectionState(); };

        var edit = Ui.Button("Edit", "\uE70F", ButtonKind.Ghost, delegate { _editJob(job); });
        edit.MinWidth = 0;
        edit.Height = 30;
        edit.Padding = new Thickness(8, 0, 8, 0);
        edit.Margin = new Thickness(6, 0, 0, 0);
        AutomationProperties.SetName(edit, "Edit " + job.Name);

        var delete = Ui.Button("Delete", "\uE74D", ButtonKind.DangerGhost, delegate { _deleteJob(job); });
        delete.MinWidth = 0;
        delete.Height = 30;
        delete.Padding = new Thickness(8, 0, 8, 0);
        delete.Margin = new Thickness(4, 0, 0, 0);
        AutomationProperties.SetName(delete, "Delete " + job.Name);

        var actions = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        actions.Children.Add(edit);
        actions.Children.Add(delete);

        var left = new Grid();
        left.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        left.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        left.Children.Add(row.BackupCheck);
        Grid.SetColumn(actions, 1);
        left.Children.Add(actions);
        grid.Children.Add(left);

        var arrow = Ui.Icon("\uE72A", 11, Ui.Dim);
        arrow.HorizontalAlignment = HorizontalAlignment.Center;
        arrow.VerticalAlignment = VerticalAlignment.Center;
        Grid.SetColumn(arrow, 1);
        grid.Children.Add(arrow);

        var restore = new Grid();
        restore.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        restore.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        restore.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var restoreTop = new Grid();
        restoreTop.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        restoreTop.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        restoreTop.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        restoreTop.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        row.RestoreCheck = Ui.CheckBox(Ui.T("Restore", 12, FontWeights.Normal, Ui.Text), false, "Restore " + job.Name);
        row.RestoreCheck.Checked += delegate { UpdateSelectionState(); };
        row.RestoreCheck.Unchecked += delegate { UpdateSelectionState(); };
        restoreTop.Children.Add(row.RestoreCheck);

        row.Older = Ui.IconButton("\uE76B", "Older backup", delegate { StepArchive(row, 1); }, ButtonKind.Ghost, 28, 11);
        row.Older.Margin = new Thickness(6, 0, 0, 0);
        Grid.SetColumn(row.Older, 1);
        restoreTop.Children.Add(row.Older);

        row.Counter = Ui.T(string.Empty, 10, Ui.Dim);
        row.Counter.VerticalAlignment = VerticalAlignment.Center;
        row.Counter.Margin = new Thickness(6, 0, 6, 0);
        Grid.SetColumn(row.Counter, 2);
        restoreTop.Children.Add(row.Counter);

        row.Newer = Ui.IconButton("\uE76C", "Newer backup", delegate { StepArchive(row, -1); }, ButtonKind.Ghost, 28, 11);
        Grid.SetColumn(row.Newer, 3);
        restoreTop.Children.Add(row.Newer);
        Grid.SetRow(restoreTop, 0);
        restore.Children.Add(restoreTop);

        row.ArchiveName = Ui.T(string.Empty, 11.5, Ui.Text);
        row.ArchiveName.TextTrimming = TextTrimming.CharacterEllipsis;
        row.ArchiveName.Margin = new Thickness(0, 5, 0, 0);
        Grid.SetRow(row.ArchiveName, 1);
        restore.Children.Add(row.ArchiveName);

        row.ArchiveMeta = Ui.T(string.Empty, 10.5, Ui.Dim);
        row.ArchiveMeta.Margin = new Thickness(0, 2, 0, 0);
        Grid.SetRow(row.ArchiveMeta, 2);
        restore.Children.Add(row.ArchiveMeta);

        row.NoArchive = Ui.T("No backups yet", 11, Ui.Dim);
        row.NoArchive.Margin = new Thickness(0, 6, 0, 0);
        Grid.SetRow(row.NoArchive, 1);
        Grid.SetRowSpan(row.NoArchive, 2);
        restore.Children.Add(row.NoArchive);

        Grid.SetColumn(restore, 2);
        grid.Children.Add(restore);

        row.Root = new Border
        {
            Background = Ui.Hex("#101010"),
            BorderBrush = Ui.Line,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(14),
            Padding = new Thickness(14, 11, 14, 11),
            Margin = new Thickness(0, 0, 6, 8),
            Child = grid
        };
        row.Root.MouseEnter += delegate { row.Root.BorderBrush = Ui.LineStrong; };
        row.Root.MouseLeave += delegate { row.Root.BorderBrush = Ui.Line; };
        UpdateRow(row);
        return row;
    }

    private void StepArchive(SourceRow row, int delta)
    {
        if (row.Archives.Count == 0)
        {
            return;
        }
        var next = row.Index + delta;
        if (next < 0 || next >= row.Archives.Count)
        {
            return;
        }
        row.Index = next;
        UpdateRow(row);
    }

    private void UpdateRow(SourceRow row)
    {
        var hasArchives = row.Archives.Count > 0 && row.BackupAvailable;
        row.RestoreCheck.Visibility = hasArchives ? Visibility.Visible : Visibility.Collapsed;
        row.Older.Visibility = hasArchives ? Visibility.Visible : Visibility.Collapsed;
        row.Newer.Visibility = hasArchives ? Visibility.Visible : Visibility.Collapsed;
        row.Counter.Visibility = hasArchives ? Visibility.Visible : Visibility.Collapsed;
        row.ArchiveName.Visibility = hasArchives ? Visibility.Visible : Visibility.Collapsed;
        row.ArchiveMeta.Visibility = hasArchives ? Visibility.Visible : Visibility.Collapsed;
        row.NoArchive.Visibility = hasArchives ? Visibility.Collapsed : Visibility.Visible;

        if (!hasArchives)
        {
            if (_scanning)
            {
                row.NoArchive.Text = "Scanning backups...";
            }
            else if (string.IsNullOrEmpty(row.Job.BackupFolder))
            {
                row.NoArchive.Text = "Backup folder not set";
            }
            else if (!row.BackupAvailable)
            {
                row.NoArchive.Text = "Backup folder unavailable";
            }
            else
            {
                row.NoArchive.Text = "No backups yet";
            }
            if (row.RestoreCheck.IsChecked == true)
            {
                row.RestoreCheck.IsChecked = false;
            }
            return;
        }

        if (row.Index >= row.Archives.Count)
        {
            row.Index = 0;
        }
        var archive = row.Archives[row.Index];
        row.ArchiveName.Text = archive.Name;
        row.ArchiveName.ToolTip = archive.FullPath;
        row.ArchiveMeta.Text = archive.DateText + "  \u2022  " + archive.SizeText;
        row.Counter.Text = (row.Index + 1) + " / " + row.Archives.Count;
        row.Older.IsEnabled = row.Index < row.Archives.Count - 1;
        row.Newer.IsEnabled = row.Index > 0;
    }

    private async Task RefreshArchivesAsync()
    {
        if (_scanning)
        {
            return;
        }
        _scanning = true;
        _restoreCount.Text = "Scanning...";
        foreach (var row in _rows)
        {
            UpdateRow(row);
        }

        var folders = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var row in _rows)
        {
            var folder = row.Job.BackupFolder;
            if (!string.IsNullOrEmpty(folder) && seen.Add(folder))
            {
                folders.Add(folder);
            }
        }

        try
        {
            var scans = await Task.Run(delegate
            {
                var results = new Dictionary<string, ArchiveScanResult>(StringComparer.OrdinalIgnoreCase);
                foreach (var folder in folders)
                {
                    results[folder] = ArchiveService.ScanArchives(folder);
                }
                return results;
            });

            foreach (var row in _rows)
            {
                row.Archives.Clear();
                row.Index = 0;
                row.BackupAvailable = false;
                var folder = row.Job.BackupFolder;
                if (string.IsNullOrEmpty(folder))
                {
                    continue;
                }
                ArchiveScanResult scan;
                if (!scans.TryGetValue(folder, out scan) || scan == null || !scan.Available)
                {
                    continue;
                }
                row.BackupAvailable = true;
                foreach (var archive in scan.Archives)
                {
                    if (ArchiveService.MatchesSource(archive.Name, row.Job.SourceFolder))
                    {
                        row.Archives.Add(archive);
                    }
                }
            }
        }
        catch
        {
            foreach (var row in _rows)
            {
                row.BackupAvailable = false;
            }
        }
        finally
        {
            _scanning = false;
            foreach (var row in _rows)
            {
                UpdateRow(row);
            }
            UpdateSelectionState();
        }
    }

    private Border BuildFooter(Action settings)
    {
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var icon = Ui.IconTile("\uE713", Ui.Text, Ui.Hex("#1A1A1A"), 36, 15);
        icon.Margin = new Thickness(0, 0, 12, 0);
        grid.Children.Add(icon);

        var text = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        text.Children.Add(Ui.T("Shared settings", 12.5, FontWeights.SemiBold, Ui.Text));
        var path = Ui.PathText(_settingsPath, 11.5, Ui.Dim);
        path.Margin = new Thickness(0, 2, 0, 0);
        text.Children.Add(path);
        Grid.SetColumn(text, 1);
        grid.Children.Add(text);

        var right = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        _summary = Ui.T("Tick items to start.", 11.5, Ui.Muted);
        _summary.VerticalAlignment = VerticalAlignment.Center;
        _summary.TextTrimming = TextTrimming.CharacterEllipsis;
        _summary.Margin = new Thickness(0, 0, 12, 0);
        right.Children.Add(_summary);

        var openSettings = Ui.Button("Open Settings", "\uE713", ButtonKind.Ghost, settings);
        openSettings.MinWidth = 132;
        openSettings.Margin = new Thickness(0, 0, 10, 0);
        AutomationProperties.SetName(openSettings, "Open Settings");
        right.Children.Add(openSettings);

        _startButton = Ui.Button("Start", "\uE768", ButtonKind.Primary, StartSelection);
        _startButton.MinWidth = 128;
        _startButton.IsEnabled = false;
        AutomationProperties.SetName(_startButton, "Start");
        right.Children.Add(_startButton);
        Grid.SetColumn(right, 2);
        grid.Children.Add(right);

        return new Border
        {
            Background = Ui.Surface,
            BorderBrush = Ui.Line,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(14),
            Padding = new Thickness(16, 12, 14, 12),
            Child = grid
        };
    }

    private void StartSelection()
    {
        var selection = new HomeSelection();
        foreach (var row in _rows)
        {
            if (row.BackupCheck.IsChecked == true)
            {
                selection.BackupJobs.Add(row.Job);
            }
            if (row.RestoreCheck.IsChecked == true && row.Archives.Count > 0)
            {
                selection.Restores.Add(new HomeRestore
                {
                    Archive = row.Archives[Math.Max(0, Math.Min(row.Index, row.Archives.Count - 1))],
                    TargetFolder = row.Job.SourceFolder
                });
            }
        }
        if (selection.BackupJobs.Count == 0 && selection.Restores.Count == 0)
        {
            return;
        }
        _start(selection);
    }

    private void UpdateSelectionState()
    {
        var backups = _rows.Count(delegate(SourceRow row) { return row.BackupCheck.IsChecked == true; });
        var withArchives = _rows.Count(delegate(SourceRow row) { return row.Archives.Count > 0; });
        var restores = _rows.Count(delegate(SourceRow row)
        {
            return row.Archives.Count > 0 && row.RestoreCheck.IsChecked == true;
        });

        _backupCount.Text = _rows.Count == 0
            ? "No backups"
            : backups + " of " + _rows.Count + " selected";
        _restoreCount.Text = _rows.Count == 0
            ? "No backups"
            : withArchives == 0
                ? (_scanning ? "Scanning..." : "No archives")
                : restores + " of " + withArchives + " selected";

        var total = backups + restores;
        if (_startButton != null)
        {
            _startButton.IsEnabled = total > 0;
        }
        if (_summary != null)
        {
            if (total == 0)
            {
                _summary.Text = "Tick items to start.";
            }
            else
            {
                var parts = new List<string>();
                if (backups > 0)
                {
                    parts.Add(backups + (backups == 1 ? " backup" : " backups"));
                }
                if (restores > 0)
                {
                    parts.Add(restores + (restores == 1 ? " restore" : " restores"));
                }
                _summary.Text = string.Join("  \u2022  ", parts) + " selected";
            }
        }
    }

    private Border BuildNotice(string message)
    {
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        var icon = Ui.Icon("\uE946", 13, Ui.Text);
        icon.VerticalAlignment = VerticalAlignment.Center;
        icon.Margin = new Thickness(0, 0, 10, 0);
        grid.Children.Add(icon);
        var text = Ui.T(message, 11.5, Ui.Muted);
        text.TextWrapping = TextWrapping.Wrap;
        text.LineHeight = 17;
        Grid.SetColumn(text, 1);
        grid.Children.Add(text);
        return new Border
        {
            Background = Ui.Surface,
            BorderBrush = Ui.Line,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(14, 10, 14, 10),
            Child = grid
        };
    }

    private sealed class SourceRow
    {
        public BackupJob Job;
        public Border Root;
        public CheckBox BackupCheck;
        public CheckBox RestoreCheck;
        public TextBlock ArchiveName;
        public TextBlock ArchiveMeta;
        public TextBlock Counter;
        public TextBlock NoArchive;
        public Button Older;
        public Button Newer;
        public readonly List<ArchiveInfo> Archives = new List<ArchiveInfo>();
        public int Index;
        public bool BackupAvailable;
    }
}
