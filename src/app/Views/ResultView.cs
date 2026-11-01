using System;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using AutoBackup.UI;

namespace AutoBackup.Views;

public sealed class ResultContent
{
    public bool Success { get; set; }
    public string Title { get; set; }
    public string Subtitle { get; set; }
    public string LocationLabel { get; set; } = "Location";
    public string Location { get; set; }
    public string Size { get; set; }
    public string Duration { get; set; }
    public string Warning { get; set; }
    public string ErrorDetail { get; set; }
    public int AutoCloseSeconds { get; set; }
}

public sealed class ResultView : UserControl
{
    private readonly Action _close;
    private readonly TextBlock _countdown;
    private readonly Button _stayOpen;
    private readonly DispatcherTimer _timer;
    private int _seconds;

    public ResultView(ResultContent content, Action close, Action openFolder, Action retry)
    {
        _close = close;
        _seconds = Math.Max(0, content.AutoCloseSeconds);

        var inner = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center };
        var badge = Ui.IconTile(
            content.Success ? "\uE73E" : "\uE783",
            content.Success ? Ui.OnAccent : Ui.Danger,
            content.Success ? Ui.White : Ui.DangerSoft,
            64,
            27);
        badge.HorizontalAlignment = HorizontalAlignment.Center;
        inner.Children.Add(badge);

        var title = Ui.Display(content.Title, 24, FontWeights.SemiBold);
        title.HorizontalAlignment = HorizontalAlignment.Center;
        title.TextWrapping = TextWrapping.Wrap;
        title.TextAlignment = TextAlignment.Center;
        title.MaxWidth = 480;
        title.Margin = new Thickness(0, 18, 0, 0);
        inner.Children.Add(title);

        var subtitle = Ui.T(content.Subtitle, 12.5, Ui.Muted);
        subtitle.HorizontalAlignment = HorizontalAlignment.Center;
        subtitle.TextWrapping = TextWrapping.Wrap;
        subtitle.TextAlignment = TextAlignment.Center;
        subtitle.MaxWidth = 480;
        subtitle.LineHeight = 19;
        subtitle.Margin = new Thickness(0, 7, 0, 0);
        inner.Children.Add(subtitle);

        var hasMeta = !string.IsNullOrEmpty(content.Location) || !string.IsNullOrEmpty(content.Size) || !string.IsNullOrEmpty(content.Duration);
        if (hasMeta)
        {
            var meta = new StackPanel { Margin = new Thickness(0, 22, 0, 0) };
            if (!string.IsNullOrEmpty(content.Location))
            {
                meta.Children.Add(MetaRow(content.LocationLabel, content.Location, false));
            }
            if (!string.IsNullOrEmpty(content.Size))
            {
                meta.Children.Add(MetaRow("Size", content.Size, true));
            }
            if (!string.IsNullOrEmpty(content.Duration))
            {
                meta.Children.Add(MetaRow("Duration", content.Duration, true));
            }
            inner.Children.Add(new Border
            {
                Background = Ui.Bg2,
                BorderBrush = Ui.Line,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(11),
                Padding = new Thickness(15, 12, 15, 12),
                Child = meta
            });
        }

        if (!string.IsNullOrWhiteSpace(content.Warning))
        {
            inner.Children.Add(Note("\uE7BA", Ui.Warning, Ui.WarningSoft, content.Warning));
        }

        if (!string.IsNullOrWhiteSpace(content.ErrorDetail))
        {
            inner.Children.Add(ErrorBox(content.ErrorDetail));
        }

        if (_seconds > 0)
        {
            var countdownRow = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 16, 0, 0)
            };
            _countdown = Ui.T("Closing automatically in " + _seconds + "s", 11.5, Ui.Dim);
            _countdown.VerticalAlignment = VerticalAlignment.Center;
            countdownRow.Children.Add(_countdown);
            _stayOpen = Ui.Button("Stay open", null, ButtonKind.Link, StopAutoClose);
            _stayOpen.Margin = new Thickness(8, 0, 0, 0);
            AutomationProperties.SetName(_stayOpen, "Stay open");
            countdownRow.Children.Add(_stayOpen);
            inner.Children.Add(countdownRow);
        }

        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 22, 0, 0)
        };
        if (content.Success)
        {
            if (openFolder != null)
            {
                var open = Ui.Button("Open folder", "\uE838", ButtonKind.Secondary, openFolder);
                open.MinWidth = 132;
                open.Margin = new Thickness(0, 0, 10, 0);
                buttons.Children.Add(open);
            }
            var closeButton = Ui.Button("Close now", null, ButtonKind.Primary, close);
            closeButton.MinWidth = 122;
            buttons.Children.Add(closeButton);
        }
        else
        {
            var closeButton = Ui.Button("Close", null, retry == null ? ButtonKind.Primary : ButtonKind.Secondary, close);
            closeButton.MinWidth = 104;
            closeButton.Margin = new Thickness(0, 0, 10, 0);
            buttons.Children.Add(closeButton);
            if (retry != null)
            {
                var retryButton = Ui.Button("Try again", "\uE72C", ButtonKind.Primary, retry);
                retryButton.MinWidth = 128;
                buttons.Children.Add(retryButton);
            }
        }
        inner.Children.Add(buttons);

        var card = Ui.Card(inner, new Thickness(38, 32, 38, 30), 18);
        card.Width = 620;
        card.HorizontalAlignment = HorizontalAlignment.Center;
        card.VerticalAlignment = VerticalAlignment.Center;

        var root = new Grid { Margin = new Thickness(30, 24, 30, 24) };
        root.Children.Add(card);
        Content = root;

        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _timer.Tick += delegate
        {
            _seconds--;
            if (_seconds <= 0)
            {
                _timer.Stop();
                if (_close != null) { _close(); }
                return;
            }
            if (_countdown != null)
            {
                _countdown.Text = "Closing automatically in " + _seconds + "s";
            }
        };
        Loaded += delegate { if (_seconds > 0) { _timer.Start(); } };
        Unloaded += delegate { _timer.Stop(); };
    }

    private static Grid MetaRow(string label, string value, bool compact)
    {
        var row = new Grid { Margin = new Thickness(0, compact ? 4 : 0, 0, compact ? 0 : 4) };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(88) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        var labelBlock = Ui.T(label, 11.5, Ui.Dim);
        labelBlock.VerticalAlignment = VerticalAlignment.Top;
        row.Children.Add(labelBlock);
        var valueBlock = Ui.T(value, 12, Ui.Text);
        valueBlock.TextTrimming = TextTrimming.CharacterEllipsis;
        valueBlock.ToolTip = value;
        valueBlock.TextWrapping = TextWrapping.NoWrap;
        Grid.SetColumn(valueBlock, 1);
        row.Children.Add(valueBlock);
        return row;
    }

    private static Border Note(string glyph, Brush color, Brush background, string message)
    {
        var grid = new Grid { Margin = new Thickness(0, 14, 0, 0) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        var icon = Ui.Icon(glyph, 13, color);
        icon.VerticalAlignment = VerticalAlignment.Top;
        icon.Margin = new Thickness(0, 2, 9, 0);
        grid.Children.Add(icon);
        var text = Ui.T(message, 11.5, Ui.Muted);
        text.TextWrapping = TextWrapping.Wrap;
        text.LineHeight = 18;
        text.VerticalAlignment = VerticalAlignment.Top;
        Grid.SetColumn(text, 1);
        grid.Children.Add(text);
        return new Border
        {
            Background = background,
            BorderBrush = Ui.Line,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(13, 10, 13, 10),
            Child = grid
        };
    }

    private static Border ErrorBox(string detail)
    {
        var text = new TextBlock
        {
            Text = detail,
            FontFamily = new FontFamily("Consolas, Segoe UI"),
            FontSize = 11.5,
            Foreground = Ui.Muted,
            TextWrapping = TextWrapping.Wrap
        };
        return new Border
        {
            Background = Ui.DangerSoft,
            BorderBrush = Ui.LineStrong,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(14, 11, 14, 11),
            Margin = new Thickness(0, 14, 0, 0),
            Child = new ScrollViewer
            {
                Content = text,
                MaxHeight = 150,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                Focusable = false
            }
        };
    }

    private void StopAutoClose()
    {
        _timer.Stop();
        _seconds = 0;
        if (_countdown != null)
        {
            _countdown.Text = "Auto-close paused.";
            _countdown.Foreground = Ui.Dim;
        }
        if (_stayOpen != null)
        {
            _stayOpen.Visibility = Visibility.Collapsed;
        }
    }
}
