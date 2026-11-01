using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using AutoBackup.Services;
using AutoBackup.UI;

namespace AutoBackup.Views;

public sealed class SourceFolderEditor : UserControl
{
    private readonly StackPanel _rows;
    private readonly List<TextBox> _boxes = new List<TextBox>();

    public event Action Changed;

    public SourceFolderEditor()
    {
        var root = new StackPanel();
        _rows = new StackPanel();
        root.Children.Add(_rows);
        var add = Ui.Button("Add folder", "\uE8B7", ButtonKind.Secondary, delegate { AddRow(string.Empty, true); RaiseChanged(); });
        add.MinWidth = 132;
        add.HorizontalAlignment = HorizontalAlignment.Left;
        add.Margin = new Thickness(0, 6, 0, 0);
        AutomationProperties.SetName(add, "Add source folder");
        root.Children.Add(add);
        Content = root;
        AddRow(string.Empty, false);
    }

    public IList<string> Folders
    {
        get
        {
            var list = new List<string>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var box in _boxes)
            {
                var path = AppConfig.CleanPath(box.Text);
                if (string.IsNullOrEmpty(path))
                {
                    continue;
                }
                try
                {
                    path = Path.GetFullPath(path);
                }
                catch
                {
                }
                if (seen.Add(path))
                {
                    list.Add(path);
                }
            }
            return list;
        }
    }

    public void SetFolders(IEnumerable<string> folders)
    {
        _boxes.Clear();
        _rows.Children.Clear();
        if (folders != null)
        {
            foreach (var folder in folders)
            {
                AddRow(folder, false);
            }
        }
        if (_boxes.Count == 0)
        {
            AddRow(string.Empty, false);
        }
        Renumber();
    }

    private void AddRow(string path, bool focus)
    {
        var row = new Grid { Margin = new Thickness(0, 0, 0, 6) };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var box = Ui.Input(path ?? string.Empty, "Source folder");
        box.TextChanged += delegate { RaiseChanged(); };
        row.Children.Add(box);

        var browse = Ui.IconButton("\uE8B7", "Browse source folder", delegate { Browse(box); }, ButtonKind.Ghost, 38, 15);
        browse.Margin = new Thickness(6, 0, 0, 0);
        Grid.SetColumn(browse, 1);
        row.Children.Add(browse);

        var remove = Ui.IconButton("\uE74D", "Remove source folder", delegate { RemoveRow(row, box); }, ButtonKind.DangerGhost, 38, 14);
        remove.Margin = new Thickness(4, 0, 0, 0);
        Grid.SetColumn(remove, 2);
        row.Children.Add(remove);

        _rows.Children.Add(row);
        _boxes.Add(box);
        Renumber();
        if (focus)
        {
            box.Focus();
        }
    }

    private void RemoveRow(Grid row, TextBox box)
    {
        _rows.Children.Remove(row);
        _boxes.Remove(box);
        if (_boxes.Count == 0)
        {
            AddRow(string.Empty, false);
        }
        Renumber();
        RaiseChanged();
    }

    private void Renumber()
    {
        for (var i = 0; i < _boxes.Count; i++)
        {
            var number = (i + 1).ToString();
            AutomationProperties.SetName(_boxes[i], "Source folder " + number);
            var row = _boxes[i].Parent as Grid;
            if (row == null)
            {
                continue;
            }
            foreach (var child in row.Children)
            {
                var button = child as Button;
                if (button == null)
                {
                    continue;
                }
                var name = AutomationProperties.GetName(button);
                if (name != null && name.StartsWith("Browse source folder", StringComparison.Ordinal))
                {
                    AutomationProperties.SetName(button, "Browse source folder " + number);
                }
                else if (name != null && name.StartsWith("Remove source folder", StringComparison.Ordinal))
                {
                    AutomationProperties.SetName(button, "Remove source folder " + number);
                }
            }
        }
    }

    private void Browse(TextBox box)
    {
        try
        {
            var initial = AppConfig.CleanPath(box.Text);
            if (string.IsNullOrEmpty(initial) || !Directory.Exists(initial))
            {
                initial = ConfigStore.SuggestedSettingsFolder;
            }
            var folder = FolderPicker.Pick(Window.GetWindow(this), "Choose a source folder", initial);
            if (!string.IsNullOrEmpty(folder))
            {
                box.Text = folder;
                box.CaretIndex = box.Text.Length;
            }
        }
        catch (Exception ex)
        {
            DialogService.Alert(Window.GetWindow(this), "The folder could not be added", ex.Message, true);
        }
    }

    private void RaiseChanged()
    {
        var handler = Changed;
        if (handler != null)
        {
            handler();
        }
    }
}
