using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using AutoBackup.Services;
using AutoBackup.UI;

namespace AutoBackup.Views;

public sealed class RecoveryView : UserControl
{
    public RecoveryView(ConfigLoadResult load, Action locateFile, Action chooseFolder, Action startSetup)
    {
        var title = "Settings are missing";
        var description = "Auto Backup needs the shared settings file before it can create or restore backups.";
        var detail = load == null ? null : load.Error;

        if (load != null)
        {
            switch (load.Status)
            {
                case ConfigLoadStatus.MissingSettingsFile:
                    title = "Shared settings file not found";
                    description = "The file this device points to is not available. It may be on a drive that is disconnected, or the file was moved.";
                    break;
                case ConfigLoadStatus.InvalidSettingsFile:
                    title = "Shared settings file is unreadable";
                    description = "The settings file exists but its contents could not be read. Locate a valid settings file, or start setup to create a new one.";
                    break;
                case ConfigLoadStatus.InvalidBootstrap:
                    title = "This device needs to be linked again";
                    description = "The local pointer to your shared settings file is damaged or incomplete. Locate the settings file to link this device again.";
                    break;
            }
        }

        var inner = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center };
        var badge = Ui.IconTile("\uE7BA", Ui.Warning, Ui.WarningSoft, 58, 24);
        badge.HorizontalAlignment = HorizontalAlignment.Center;
        inner.Children.Add(badge);
        var titleBlock = Ui.Display(title, 22, FontWeights.SemiBold);
        titleBlock.HorizontalAlignment = HorizontalAlignment.Center;
        titleBlock.TextWrapping = TextWrapping.Wrap;
        titleBlock.TextAlignment = TextAlignment.Center;
        titleBlock.MaxWidth = 480;
        titleBlock.Margin = new Thickness(0, 18, 0, 0);
        inner.Children.Add(titleBlock);

        var descriptionBlock = Ui.T(description, 12.5, Ui.Muted);
        descriptionBlock.HorizontalAlignment = HorizontalAlignment.Center;
        descriptionBlock.TextWrapping = TextWrapping.Wrap;
        descriptionBlock.TextAlignment = TextAlignment.Center;
        descriptionBlock.MaxWidth = 500;
        descriptionBlock.LineHeight = 20;
        descriptionBlock.Margin = new Thickness(0, 8, 0, 0);
        inner.Children.Add(descriptionBlock);

        var path = load == null ? null : load.SettingsPath;
        if (!string.IsNullOrEmpty(path))
        {
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
            var label = Ui.Label("Expected settings file", Ui.Dim);
            label.Margin = new Thickness(0, 0, 0, 4);
            pathStack.Children.Add(label);
            pathStack.Children.Add(Ui.PathText(path, 12, Ui.Text));
            pathBox.Child = pathStack;
            inner.Children.Add(pathBox);
        }
        else
        {
            inner.Children.Add(new Border { Height = 12 });
        }

        if (!string.IsNullOrWhiteSpace(detail))
        {
            var detailBlock = Ui.T(detail, 11.5, Ui.Dim);
            detailBlock.TextWrapping = TextWrapping.Wrap;
            detailBlock.TextAlignment = TextAlignment.Center;
            detailBlock.MaxWidth = 500;
            detailBlock.Margin = new Thickness(0, 12, 0, 0);
            inner.Children.Add(detailBlock);
        }

        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 24, 0, 0)
        };
        var locate = Ui.Button("Locate settings file", "\uE8E5", ButtonKind.Primary, locateFile);
        locate.MinWidth = 168;
        locate.Margin = new Thickness(0, 0, 10, 0);
        buttons.Children.Add(locate);
        var folder = Ui.Button("Choose folder", "\uE8B7", ButtonKind.Secondary, chooseFolder);
        folder.MinWidth = 138;
        folder.Margin = new Thickness(0, 0, 10, 0);
        buttons.Children.Add(folder);
        var setup = Ui.Button("Start setup", "\uE76C", ButtonKind.Ghost, startSetup);
        setup.MinWidth = 120;
        buttons.Children.Add(setup);
        inner.Children.Add(buttons);

        var note = Ui.T("No backup is created or restored until the shared settings are linked.", 11.5, Ui.Dim);
        note.HorizontalAlignment = HorizontalAlignment.Center;
        note.TextAlignment = TextAlignment.Center;
        note.TextWrapping = TextWrapping.Wrap;
        note.Margin = new Thickness(0, 18, 0, 0);
        inner.Children.Add(note);

        var card = Ui.Card(inner, new Thickness(38, 32, 38, 28), 18);
        card.Width = 650;
        card.HorizontalAlignment = HorizontalAlignment.Center;
        card.VerticalAlignment = VerticalAlignment.Center;

        var root = new Grid { Margin = new Thickness(30, 24, 30, 24) };
        root.Children.Add(card);
        Content = root;
    }
}
