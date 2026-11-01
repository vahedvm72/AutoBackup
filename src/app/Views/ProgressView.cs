using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using System.Windows.Threading;
using AutoBackup.Services;
using AutoBackup.UI;

namespace AutoBackup.Views;

public sealed class ProgressView : UserControl
{
    private readonly TextBlock _status;
    private readonly TextBlock _percent;
    private readonly TextBlock _elapsed;
    private readonly ProgressBarEx _bar;
    private readonly Button _cancel;
    private readonly DispatcherTimer _timer;
    private readonly DateTime _started = DateTime.UtcNow;
    private double _value;

    public event Action CancelRequested;

    public ProgressView(string title, string subtitle, string glyph = "\uE7B8")
    {
        var inner = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center };

        var hero = new Grid { Width = 84, Height = 84, HorizontalAlignment = HorizontalAlignment.Center };
        hero.Children.Add(new Ellipse
        {
            Width = 84,
            Height = 84,
            Stroke = Ui.LineStrong,
            StrokeThickness = 2
        });
        var tile = new Border
        {
            Width = 62,
            Height = 62,
            CornerRadius = new CornerRadius(19),
            Background = new LinearGradientBrush(Color.FromRgb(255, 255, 255), Color.FromRgb(202, 202, 202), 90),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Child = Ui.Icon(glyph, 26, Ui.OnAccent)
        };
        hero.Children.Add(tile);
        tile.BeginAnimation(OpacityProperty, new DoubleAnimation(0.68, 1, TimeSpan.FromMilliseconds(1500))
        {
            AutoReverse = true,
            RepeatBehavior = RepeatBehavior.Forever
        });
        inner.Children.Add(hero);

        var titleBlock = Ui.Display(title, 22, FontWeights.SemiBold);
        titleBlock.HorizontalAlignment = HorizontalAlignment.Center;
        titleBlock.TextWrapping = TextWrapping.Wrap;
        titleBlock.TextAlignment = TextAlignment.Center;
        titleBlock.MaxWidth = 440;
        titleBlock.Margin = new Thickness(0, 20, 0, 0);
        inner.Children.Add(titleBlock);

        var subtitleBlock = Ui.T(subtitle, 12.5, Ui.Muted);
        subtitleBlock.HorizontalAlignment = HorizontalAlignment.Center;
        subtitleBlock.TextWrapping = TextWrapping.Wrap;
        subtitleBlock.TextAlignment = TextAlignment.Center;
        subtitleBlock.MaxWidth = 460;
        subtitleBlock.LineHeight = 19;
        subtitleBlock.Margin = new Thickness(0, 8, 0, 0);
        inner.Children.Add(subtitleBlock);

        _bar = new ProgressBarEx { Margin = new Thickness(0, 28, 0, 0), Height = 8 };
        inner.Children.Add(_bar);

        var statusRow = new Grid { Margin = new Thickness(0, 12, 0, 0) };
        statusRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        statusRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        _status = Ui.T("Starting...", 12, Ui.Muted);
        _status.TextTrimming = TextTrimming.CharacterEllipsis;
        _status.Margin = new Thickness(0, 0, 12, 0);
        statusRow.Children.Add(_status);
        _percent = Ui.T("0%", 13, FontWeights.SemiBold, Ui.AccentHover);
        _percent.HorizontalAlignment = HorizontalAlignment.Right;
        Grid.SetColumn(_percent, 1);
        statusRow.Children.Add(_percent);
        inner.Children.Add(statusRow);

        inner.Children.Add(new Border { Height = 1, Background = Ui.Line, Margin = new Thickness(0, 20, 0, 16) });

        var footer = new Grid();
        footer.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        footer.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var elapsedRow = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        var clock = Ui.Icon("\uE823", 12, Ui.Dim);
        clock.Margin = new Thickness(0, 0, 7, 0);
        elapsedRow.Children.Add(clock);
        _elapsed = Ui.T("0s elapsed", 11.5, Ui.Dim);
        elapsedRow.Children.Add(_elapsed);
        footer.Children.Add(elapsedRow);

        _cancel = Ui.Button("Cancel", null, ButtonKind.Secondary, Cancel);
        _cancel.MinWidth = 104;
        _cancel.HorizontalAlignment = HorizontalAlignment.Right;
        Grid.SetColumn(_cancel, 1);
        footer.Children.Add(_cancel);
        inner.Children.Add(footer);

        var card = Ui.Card(inner, new Thickness(34, 30, 34, 26), 18);
        card.Width = 580;
        card.HorizontalAlignment = HorizontalAlignment.Center;
        card.VerticalAlignment = VerticalAlignment.Center;

        var root = new Grid { Margin = new Thickness(30, 24, 30, 24) };
        root.Children.Add(card);
        Content = root;

        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
        _timer.Tick += delegate
        {
            _elapsed.Text = Formats.Duration(DateTime.UtcNow - _started) + " elapsed";
        };
        Loaded += delegate { _timer.Start(); };
        Unloaded += delegate { _timer.Stop(); };
    }

    public void SetProgress(double value, string status)
    {
        if (value > _value)
        {
            _value = Math.Min(100, value);
            _bar.Value = _value;
            _percent.Text = ((int)Math.Round(_value)).ToString() + "%";
        }
        if (!string.IsNullOrWhiteSpace(status))
        {
            var text = status.Trim();
            if (text.Length > 140)
            {
                text = text.Substring(0, 137) + "...";
            }
            _status.Text = text;
            _status.ToolTip = text;
        }
    }

    public void SetCancelling()
    {
        _cancel.IsEnabled = false;
        _status.Text = "Cancelling...";
    }

    private void Cancel()
    {
        SetCancelling();
        var handler = CancelRequested;
        if (handler != null)
        {
            handler();
        }
    }
}
