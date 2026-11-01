using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace AutoBackup.UI;

public class ChromeWindow : Window
{
    private readonly StackPanel _leftStack;
    private readonly StackPanel _rightStack;
    private readonly TextBlock _titleText;
    private readonly TextBlock _subtitleText;
    private readonly Border _titleBar;

    public ContentControl ViewHost { get; }
    public StackPanel HeaderExtras { get; }
    public Button BackButton { get; }
    public Button SettingsButton { get; }
    public Button MinimizeButton { get; }
    public Button CloseButton { get; }
    public Func<bool> CloseGuard { get; set; }

    public ChromeWindow(string title, double width, double height, bool showBack = false, bool showSettings = false, bool showMinimize = true, string subtitle = null)
    {
        Title = title;
        WindowStyle = WindowStyle.None;
        ResizeMode = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        Width = Math.Min(width, Math.Max(560, SystemParameters.WorkArea.Width - 32));
        Height = Math.Min(height, Math.Max(440, SystemParameters.WorkArea.Height - 32));
        Background = new LinearGradientBrush(Color.FromRgb(7, 7, 7), Color.FromRgb(14, 14, 14), 90);
        SnapsToDevicePixels = true;
        UseLayoutRounding = true;
        TextOptions.SetTextFormattingMode(this, TextFormattingMode.Display);
        TextOptions.SetTextRenderingMode(this, TextRenderingMode.ClearType);
        Icon = AppIcon.Get();

        var rootGrid = new Grid();
        rootGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(2) });
        rootGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(54) });
        rootGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        var accent = new Border
        {
            Background = new LinearGradientBrush
            {
                StartPoint = new Point(0, 0.5),
                EndPoint = new Point(1, 0.5),
                GradientStops =
                {
                    new GradientStop(Color.FromArgb(0, 255, 255, 255), 0),
                    new GradientStop(Color.FromArgb(235, 255, 255, 255), 0.35),
                    new GradientStop(Color.FromArgb(150, 255, 255, 255), 0.7),
                    new GradientStop(Color.FromArgb(0, 255, 255, 255), 1)
                }
            }
        };
        Grid.SetRow(accent, 0);
        rootGrid.Children.Add(accent);

        _titleBar = new Border
        {
            Background = Ui.Hex("#0C0C0C"),
            BorderBrush = Ui.Line,
            BorderThickness = new Thickness(0, 0, 0, 1),
            Cursor = Cursors.Arrow
        };
        var titleGrid = new Grid();
        titleGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        titleGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        _leftStack = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(16, 0, 0, 0)
        };
        BackButton = Ui.IconButton("\uE72B", "Back", delegate { }, ButtonKind.Ghost, 32, 13);
        BackButton.Visibility = showBack ? Visibility.Visible : Visibility.Collapsed;
        BackButton.Margin = new Thickness(0, 0, 10, 0);
        _leftStack.Children.Add(BackButton);
        _leftStack.Children.Add(Ui.Logo(26));
        var titleStack = new StackPanel { Orientation = Orientation.Vertical, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(10, 0, 0, 0) };
        _titleText = Ui.T(title, 13.5, FontWeights.SemiBold, Ui.Text);
        _subtitleText = Ui.T(subtitle ?? string.Empty, 10.5, Ui.Dim);
        _subtitleText.Visibility = string.IsNullOrEmpty(subtitle) ? Visibility.Collapsed : Visibility.Visible;
        titleStack.Children.Add(_titleText);
        titleStack.Children.Add(_subtitleText);
        _leftStack.Children.Add(titleStack);
        Grid.SetColumn(_leftStack, 0);
        titleGrid.Children.Add(_leftStack);

        _rightStack = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 10, 0)
        };
        HeaderExtras = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 4, 0) };
        _rightStack.Children.Add(HeaderExtras);
        SettingsButton = Ui.IconButton("\uE713", "Settings", delegate { }, ButtonKind.Ghost, 32, 14.5);
        SettingsButton.Visibility = showSettings ? Visibility.Visible : Visibility.Collapsed;
        SettingsButton.Margin = new Thickness(2, 0, 2, 0);
        _rightStack.Children.Add(SettingsButton);
        MinimizeButton = Ui.IconButton("\uE921", "Minimize", delegate { WindowState = WindowState.Minimized; }, ButtonKind.Ghost, 32, 12);
        MinimizeButton.Visibility = showMinimize ? Visibility.Visible : Visibility.Collapsed;
        _rightStack.Children.Add(MinimizeButton);
        CloseButton = Ui.IconButton("\uE8BB", "Close", delegate { Close(); }, ButtonKind.DangerGhost, 32, 12);
        _rightStack.Children.Add(CloseButton);
        Grid.SetColumn(_rightStack, 1);
        titleGrid.Children.Add(_rightStack);

        _titleBar.Child = titleGrid;
        _titleBar.MouseLeftButtonDown += delegate(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed)
            {
                try { DragMove(); } catch { }
            }
        };
        Grid.SetRow(_titleBar, 1);
        rootGrid.Children.Add(_titleBar);

        var contentGrid = new Grid();
        var backdrop = new Rectangle { IsHitTestVisible = false };
        backdrop.Fill = new RadialGradientBrush
        {
            Center = new Point(0.88, -0.05),
            GradientOrigin = new Point(0.88, -0.05),
            RadiusX = 0.85,
            RadiusY = 0.95,
            GradientStops =
            {
                new GradientStop(Color.FromArgb(20, 255, 255, 255), 0),
                new GradientStop(Color.FromArgb(0, 255, 255, 255), 1)
            }
        };
        contentGrid.Children.Add(backdrop);
        ViewHost = new ContentControl
        {
            Focusable = false,
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            VerticalContentAlignment = VerticalAlignment.Stretch
        };
        contentGrid.Children.Add(ViewHost);
        Grid.SetRow(contentGrid, 2);
        rootGrid.Children.Add(contentGrid);

        var outer = new Border
        {
            BorderBrush = Ui.Hex("#232323"),
            BorderThickness = new Thickness(1),
            Child = rootGrid
        };
        Content = outer;
    }

    public Action BackAction { get; set; }
    public Action SettingsAction { get; set; }

    public void SetTitle(string title, string subtitle = null)
    {
        _titleText.Text = title;
        _subtitleText.Text = subtitle ?? string.Empty;
        _subtitleText.Visibility = string.IsNullOrEmpty(subtitle) ? Visibility.Collapsed : Visibility.Visible;
    }

    public void ShowBack(bool visible, Action action = null)
    {
        if (action != null)
        {
            BackAction = action;
        }
        BackButton.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
        if (visible)
        {
            BackButton.Click -= BackClick;
            BackButton.Click += BackClick;
        }
    }

    private void BackClick(object sender, RoutedEventArgs e)
    {
        if (BackAction != null) { BackAction(); }
    }

    public void ShowSettings(bool visible, Action action = null)
    {
        SettingsButton.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
        if (action != null)
        {
            SettingsAction = action;
        }
        SettingsButton.Click -= SettingsClick;
        SettingsButton.Click += SettingsClick;
    }

    private void SettingsClick(object sender, RoutedEventArgs e)
    {
        if (SettingsAction != null) { SettingsAction(); }
    }

    public void ShowView(UIElement view, bool animate = true)
    {
        ViewHost.Content = view;
        if (animate)
        {
            Ui.FadeIn(view);
        }
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        if (CloseGuard != null && !CloseGuard())
        {
            e.Cancel = true;
            return;
        }
        base.OnClosing(e);
    }
}
