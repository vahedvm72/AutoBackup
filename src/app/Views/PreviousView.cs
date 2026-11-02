using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using AutoBackup.Services;
using AutoBackup.UI;

namespace AutoBackup.Views;

public sealed class PreviousView : UserControl
{
    private readonly AppConfig _config;
    private readonly Action<ArchiveInfo> _restore;
    private readonly Action _createBackup;
    private readonly StackPanel _list;
    private readonly TextBlock _summary;
    private readonly TextBlock _subtitle;
    private readonly ScrollViewer _scroll;
    private bool _loading;

    public PreviousView(AppConfig config, Action<ArchiveInfo> restore, Action createBackup, string heading = "Previous Backups")
    {
        _config = config;
        _restore = restore;
        _createBackup = createBackup;

        var root = new Grid { Margin = new Thickness(30, 22, 30, 22) };
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        var header = new Grid();
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var headerText = new StackPanel();
        headerText.Children.Add(Ui.Display(heading, 24, FontWeights.SemiBold));
        var jobFolders = ArchiveService.GetBackupFolders(config);
        var folderSummary = jobFolders.Count == 1 ? jobFolders[0] : jobFolders.Count + " backup folders";
        _subtitle = Ui.T("Archives saved in " + folderSummary, 12.5, Ui.Muted);
        _subtitle.TextTrimming = TextTrimming.CharacterEllipsis;
        _subtitle.ToolTip = jobFolders.Count > 0 ? string.Join("\n", jobFolders) : config.BackupFolder;
        _subtitle.Margin = new Thickness(0, 6, 0, 0);
        headerText.Children.Add(_subtitle);
        header.Children.Add(headerText);

        var actions = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Top };
        var refresh = Ui.IconButton("\uE72C", "Refresh", delegate { _ = RefreshAsync(); }, ButtonKind.Ghost, 34, 13.5);
        refresh.Margin = new Thickness(0, 1, 4, 0);
        actions.Children.Add(refresh);
        var openFolder = Ui.Button("Open folder", "\uE838", ButtonKind.Secondary, OpenFolder);
        openFolder.MinWidth = 132;
        AutomationProperties.SetName(openFolder, "Open backup folder");
        actions.Children.Add(openFolder);
        Grid.SetColumn(actions, 1);
        header.Children.Add(actions);
        Grid.SetRow(header, 0);
        root.Children.Add(header);

        _summary = Ui.T("Scanning the backup folder...", 11.5, Ui.Dim);
        _summary.Margin = new Thickness(2, 12, 0, 12);
        Grid.SetRow(_summary, 1);
        root.Children.Add(_summary);

        _list = new StackPanel();
        _scroll = Ui.ScrollHost(_list);
        Grid.SetRow(_scroll, 2);
        root.Children.Add(_scroll);

        Content = root;
        Loaded += async delegate { await RefreshAsync(); };
    }

    public async Task RefreshAsync()
    {
        if (_loading)
        {
            return;
        }
        _loading = true;
        _summary.Text = "Scanning the backup folder...";
        _summary.Foreground = Ui.Dim;
        try
        {
            var scan = await Task.Run(delegate { return ArchiveService.ScanArchives(_config); });
            Render(scan);
        }
        catch (Exception ex)
        {
            Render(new ArchiveScanResult { Error = ex.Message });
        }
        finally
        {
            _loading = false;
        }
    }

    private void Render(ArchiveScanResult scan)
    {
        _list.Children.Clear();
        if (scan == null)
        {
            _summary.Text = "The backup folder could not be scanned.";
            _summary.Foreground = Ui.Warning;
            return;
        }

        if (!scan.Available)
        {
            _summary.Text = "The backup folder is not available.";
            _summary.Foreground = Ui.Warning;
            _subtitle.Text = scan.Folder ?? _config.BackupFolder;
            _list.Children.Add(BuildUnavailable(scan));
            return;
        }

        var count = scan.Archives.Count;
        _summary.Text = count == 0
            ? "No archives found in this folder."
            : count + (count == 1 ? " archive" : " archives") + "  \u2022  " + Formats.Size(scan.TotalBytes) + " total";
        _summary.Foreground = count == 0 ? Ui.Dim : Ui.Muted;
        _subtitle.Text = "Archives saved in " + scan.Folder;
        _subtitle.ToolTip = scan.Folder;

        if (count == 0)
        {
            _list.Children.Add(BuildEmpty());
            return;
        }

        foreach (var archive in scan.Archives)
        {
            _list.Children.Add(BuildRow(archive));
        }
    }

    private Border BuildRow(ArchiveInfo archive)
    {
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var tile = Ui.IconTile("\uE81C", Ui.Text, Ui.Hex("#1E1E1E"), 42, 17);
        tile.Margin = new Thickness(0, 0, 14, 0);
        tile.VerticalAlignment = VerticalAlignment.Center;
        grid.Children.Add(tile);

        var text = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        var name = Ui.T(archive.Name, 12.5, FontWeights.SemiBold, Ui.Text);
        name.TextTrimming = TextTrimming.CharacterEllipsis;
        name.ToolTip = archive.FullPath;
        text.Children.Add(name);
        var meta = Ui.T(archive.DateText + "   \u2022   " + archive.SizeText, 11.5, Ui.Dim);
        meta.Margin = new Thickness(0, 3, 0, 0);
        text.Children.Add(meta);
        Grid.SetColumn(text, 1);
        grid.Children.Add(text);

        var buttons = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(14, 0, 0, 0) };
        var restore = Ui.Button("Restore", "\uE896", ButtonKind.Secondary, delegate { Restore(archive); });
        restore.MinWidth = 108;
        restore.Height = 36;
        restore.Margin = new Thickness(0, 0, 6, 0);
        AutomationProperties.SetName(restore, "Restore " + archive.Name);
        buttons.Children.Add(restore);
        var delete = Ui.IconButton("\uE74D", "Delete backup", delegate { Delete(archive); }, ButtonKind.DangerGhost, 34, 13);
        AutomationProperties.SetName(delete, "Delete " + archive.Name);
        buttons.Children.Add(delete);
        Grid.SetColumn(buttons, 2);
        grid.Children.Add(buttons);

        var card = new Border
        {
            Background = Ui.Surface,
            BorderBrush = Ui.Line,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(12),
            Padding = new Thickness(14, 12, 12, 12),
            Margin = new Thickness(0, 0, 0, 10),
            Child = grid,
            Cursor = Cursors.Hand
        };
        card.MouseEnter += delegate { card.BorderBrush = Ui.LineStrong; card.Background = Ui.SurfaceHover; };
        card.MouseLeave += delegate { card.BorderBrush = Ui.Line; card.Background = Ui.Surface; };
        card.MouseLeftButtonUp += delegate(object sender, MouseButtonEventArgs e)
        {
            if (!IsInsideButton(e.OriginalSource as DependencyObject))
            {
                Restore(archive);
            }
        };
        return card;
    }

    private static bool IsInsideButton(DependencyObject source)
    {
        while (source != null)
        {
            if (source is Button)
            {
                return true;
            }
            source = (source is Visual || source is System.Windows.Media.Media3D.Visual3D)
                ? VisualTreeHelper.GetParent(source)
                : null;
        }
        return false;
    }

    private Border BuildEmpty()
    {
        var stack = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 34, 0, 0) };
        var tile = Ui.IconTile("\uE81C", Ui.Text, Ui.Hex("#1E1E1E"), 58, 24);
        tile.HorizontalAlignment = HorizontalAlignment.Center;
        stack.Children.Add(tile);
        var title = Ui.Display("No backups yet", 19, FontWeights.SemiBold);
        title.HorizontalAlignment = HorizontalAlignment.Center;
        title.Margin = new Thickness(0, 16, 0, 0);
        stack.Children.Add(title);
        var text = Ui.T("Create your first backup and it will show up here, ready to restore on any device.", 12, Ui.Muted);
        text.HorizontalAlignment = HorizontalAlignment.Center;
        text.TextAlignment = TextAlignment.Center;
        text.TextWrapping = TextWrapping.Wrap;
        text.MaxWidth = 380;
        text.Margin = new Thickness(0, 7, 0, 0);
        stack.Children.Add(text);
        var button = Ui.Button("Create Backup", "\uE7B8", ButtonKind.Primary, delegate { if (_createBackup != null) { _createBackup(); } });
        button.MinWidth = 156;
        button.HorizontalAlignment = HorizontalAlignment.Center;
        button.Margin = new Thickness(0, 20, 0, 0);
        stack.Children.Add(button);
        return Ui.Card(stack, new Thickness(30, 28, 30, 30), 16);
    }

    private Border BuildUnavailable(ArchiveScanResult scan)
    {
        var stack = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 30, 0, 0) };
        var tile = Ui.IconTile("\uE7BA", Ui.Warning, Ui.WarningSoft, 58, 24);
        tile.HorizontalAlignment = HorizontalAlignment.Center;
        stack.Children.Add(tile);
        var title = Ui.Display("Backup folder unavailable", 19, FontWeights.SemiBold);
        title.HorizontalAlignment = HorizontalAlignment.Center;
        title.Margin = new Thickness(0, 16, 0, 0);
        stack.Children.Add(title);
        var text = Ui.T(scan.Error ?? "Reconnect the drive or check the backup folder in Settings, then try again.", 12, Ui.Muted);
        text.HorizontalAlignment = HorizontalAlignment.Center;
        text.TextAlignment = TextAlignment.Center;
        text.TextWrapping = TextWrapping.Wrap;
        text.MaxWidth = 420;
        text.Margin = new Thickness(0, 7, 0, 0);
        stack.Children.Add(text);
        var retry = Ui.Button("Try again", "\uE72C", ButtonKind.Secondary, delegate { _ = RefreshAsync(); });
        retry.MinWidth = 132;
        retry.HorizontalAlignment = HorizontalAlignment.Center;
        retry.Margin = new Thickness(0, 20, 0, 0);
        stack.Children.Add(retry);
        return Ui.Card(stack, new Thickness(30, 28, 30, 30), 16);
    }

    private void Restore(ArchiveInfo archive)
    {
        if (_restore != null)
        {
            _restore(archive);
        }
    }

    private void Delete(ArchiveInfo archive)
    {
        var owner = Window.GetWindow(this);
        if (!DialogService.Confirm(owner, "Delete this backup?", "Delete \"" + archive.Name + "\"?\n\nThis removes the archive from the backup folder and cannot be undone.", "Delete", true))
        {
            return;
        }
        try
        {
            File.Delete(archive.FullPath);
            _ = RefreshAsync();
        }
        catch (Exception ex)
        {
            DialogService.Alert(owner, "The backup could not be deleted", ex.Message, true);
        }
    }

    private void OpenFolder()
    {
        try
        {
            var folders = ArchiveService.GetBackupFolders(_config);
            var folder = folders.Count > 0 ? folders[0] : AppConfig.CleanPath(_config.BackupFolder);
            if (string.IsNullOrEmpty(folder) || !Directory.Exists(folder))
            {
                DialogService.Alert(Window.GetWindow(this), "Backup folder unavailable", "The backup folder could not be found:\n" + folder, true);
                return;
            }
            Process.Start(new ProcessStartInfo { FileName = "explorer.exe", Arguments = "\"" + folder + "\"", UseShellExecute = true });
        }
        catch (Exception ex)
        {
            DialogService.Alert(Window.GetWindow(this), "The folder could not be opened", ex.Message, true);
        }
    }
}
