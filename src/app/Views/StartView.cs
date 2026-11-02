using System;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using AutoBackup.UI;

namespace AutoBackup.Views;

public sealed class StartView : UserControl
{
    public StartView(Action newDevice, Action restore, Action linkSettings)
    {
        var inner = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };

        var logo = Ui.Logo(58);
        logo.HorizontalAlignment = HorizontalAlignment.Center;
        inner.Children.Add(logo);

        var title = Ui.Display("Welcome to Auto Backup", 26, FontWeights.SemiBold);
        title.HorizontalAlignment = HorizontalAlignment.Center;
        title.Margin = new Thickness(0, 18, 0, 0);
        inner.Children.Add(title);

        var subtitle = Ui.T("Is this a new device, or do you want to restore an existing backup?", 13, Ui.Muted);
        subtitle.HorizontalAlignment = HorizontalAlignment.Center;
        subtitle.TextAlignment = TextAlignment.Center;
        subtitle.TextWrapping = TextWrapping.Wrap;
        subtitle.MaxWidth = 560;
        subtitle.Margin = new Thickness(0, 7, 0, 0);
        inner.Children.Add(subtitle);

        var choices = new Grid { Margin = new Thickness(0, 26, 0, 0) };
        choices.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        choices.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(16) });
        choices.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var setup = BuildChoice(
            "\uE7B8",
            Ui.White,
            Ui.OnAccent,
            "New device",
            "Set up folders and start creating backups on this PC.",
            "Set up this device",
            "\uE7B8",
            ButtonKind.Primary,
            newDevice);
        Grid.SetColumn(setup, 0);
        choices.Children.Add(setup);

        var restoreCard = BuildChoice(
            "\uE896",
            Ui.Text,
            Ui.Hex("#1E1E1E"),
            "Restore a backup",
            "Choose a folder that contains your backup archives and restore them here.",
            "Restore a backup",
            "\uE896",
            ButtonKind.Secondary,
            restore);
        Grid.SetColumn(restoreCard, 2);
        choices.Children.Add(restoreCard);
        inner.Children.Add(choices);

        var link = Ui.Button("I already have a settings file", null, ButtonKind.Link, linkSettings);
        link.HorizontalAlignment = HorizontalAlignment.Center;
        link.Margin = new Thickness(0, 18, 0, 0);
        AutomationProperties.SetName(link, "Link existing settings");
        inner.Children.Add(link);

        var note = Ui.T("Your settings can live on a synced drive and be shared between devices.", 11, Ui.Dim);
        note.HorizontalAlignment = HorizontalAlignment.Center;
        note.TextAlignment = TextAlignment.Center;
        note.TextWrapping = TextWrapping.Wrap;
        note.Margin = new Thickness(0, 6, 0, 0);
        inner.Children.Add(note);

        var card = new Border
        {
            Background = Ui.Hex("#0E0E0E"),
            BorderBrush = Ui.Line,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(20),
            Padding = new Thickness(34, 30, 34, 26),
            Width = 760,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Child = inner
        };

        var root = new Grid { Margin = new Thickness(30, 24, 30, 24) };
        root.Children.Add(card);
        Content = root;
    }

    private static Border BuildChoice(
        string glyph,
        Brush iconColor,
        Brush iconBackground,
        string title,
        string description,
        string buttonLabel,
        string buttonGlyph,
        ButtonKind kind,
        Action onClick)
    {
        var stack = new StackPanel();
        stack.Children.Add(Ui.IconTile(glyph, iconColor, iconBackground, 48, 20));
        var heading = Ui.Display(title, 18, FontWeights.SemiBold);
        heading.Margin = new Thickness(0, 16, 0, 0);
        stack.Children.Add(heading);
        var text = Ui.T(description, 11.5, Ui.Muted);
        text.TextWrapping = TextWrapping.Wrap;
        text.LineHeight = 18;
        text.MinHeight = 36;
        text.Margin = new Thickness(0, 6, 0, 0);
        stack.Children.Add(text);
        var button = Ui.Button(buttonLabel, buttonGlyph, kind, onClick);
        button.HorizontalAlignment = HorizontalAlignment.Left;
        button.Margin = new Thickness(0, 16, 0, 0);
        button.MinWidth = 176;
        AutomationProperties.SetName(button, buttonLabel);
        stack.Children.Add(button);

        return new Border
        {
            Background = Ui.Hex("#101010"),
            BorderBrush = Ui.Line,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(16),
            Padding = new Thickness(22, 20, 22, 20),
            Child = stack
        };
    }
}
