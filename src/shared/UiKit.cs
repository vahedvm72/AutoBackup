using System;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace AutoBackup.UI;

public enum ButtonKind
{
    Primary,
    Secondary,
    Ghost,
    DangerGhost,
    Link
}

public static class Ui
{
    public static readonly FontFamily Font = new FontFamily("Segoe UI Variable Text, Segoe UI, Arial");
    public static readonly FontFamily DisplayFont = new FontFamily("Segoe UI Variable Display, Segoe UI, Arial");
    public static readonly FontFamily IconFont = new FontFamily("Segoe MDL2 Assets");

    public static readonly SolidColorBrush Bg = Hex("#080808");
    public static readonly SolidColorBrush Bg2 = Hex("#0E0E0E");
    public static readonly SolidColorBrush Surface = Hex("#141414");
    public static readonly SolidColorBrush SurfaceHover = Hex("#1D1D1D");
    public static readonly SolidColorBrush SurfacePressed = Hex("#101010");
    public static readonly SolidColorBrush CardBg = Hex("#111111");
    public static readonly SolidColorBrush CardHover = Hex("#161616");
    public static readonly SolidColorBrush Line = Hex("#262626");
    public static readonly SolidColorBrush LineStrong = Hex("#3A3A3A");
    public static readonly SolidColorBrush Text = Hex("#F4F4F4");
    public static readonly SolidColorBrush Muted = Hex("#A3A3A3");
    public static readonly SolidColorBrush Dim = Hex("#6D6D6D");
    public static readonly SolidColorBrush Accent = Hex("#FFFFFF");
    public static readonly SolidColorBrush AccentHover = Hex("#E8E8E8");
    public static readonly SolidColorBrush AccentPressed = Hex("#D0D0D0");
    public static readonly SolidColorBrush AccentSoft = Hex("#1E1E1E");
    public static readonly SolidColorBrush OnAccent = Hex("#0A0A0A");
    public static readonly SolidColorBrush Purple = Hex("#E2E2E2");
    public static readonly SolidColorBrush PurpleSoft = Hex("#1A1A1A");
    public static readonly SolidColorBrush Success = Hex("#EDEDED");
    public static readonly SolidColorBrush SuccessSoft = Hex("#1C1C1C");
    public static readonly SolidColorBrush Danger = Hex("#F0F0F0");
    public static readonly SolidColorBrush DangerHover = Hex("#FFFFFF");
    public static readonly SolidColorBrush DangerSoft = Hex("#242424");
    public static readonly SolidColorBrush Warning = Hex("#C4C4C4");
    public static readonly SolidColorBrush WarningSoft = Hex("#1B1B1B");
    public static readonly SolidColorBrush White = Brushes.White;

    private static readonly Brush AccentSelection = Hex("#59FFFFFF");
    private static readonly Style InputStyleInternal = BuildInputStyle();
    private static readonly Style ToolTipStyleInternal = BuildToolTipStyle();
    private static readonly Style ScrollBarStyleInternal = BuildScrollBarStyle();
    private static ImageSource _logoCache;

    public static SolidColorBrush Hex(string value)
    {
        var hex = value.TrimStart('#');
        byte a = 255, r, g, b;
        if (hex.Length == 8)
        {
            a = Convert.ToByte(hex.Substring(0, 2), 16);
            hex = hex.Substring(2);
        }
        r = Convert.ToByte(hex.Substring(0, 2), 16);
        g = Convert.ToByte(hex.Substring(2, 2), 16);
        b = Convert.ToByte(hex.Substring(4, 2), 16);
        var brush = new SolidColorBrush(Color.FromArgb(a, r, g, b));
        brush.Freeze();
        return brush;
    }

    public static LinearGradientBrush Gradient(Color from, Color to, double angle = 45)
    {
        var brush = new LinearGradientBrush(from, to, angle);
        brush.Freeze();
        return brush;
    }

    public static void InstallGlobalResources(Application app)
    {
        app.Resources[typeof(ToolTip)] = ToolTipStyleInternal;
        app.Resources[typeof(ScrollBar)] = ScrollBarStyleInternal;
        app.Resources[typeof(TextBox)] = InputStyleInternal;
    }

    public static TextBlock T(string text, double size)
    {
        return T(text, size, FontWeights.Normal, Text);
    }

    public static TextBlock T(string text, double size, Brush color)
    {
        return T(text, size, FontWeights.Normal, color);
    }

    public static TextBlock T(string text, double size, FontWeight weight, Brush color = null)
    {
        return new TextBlock
        {
            Text = text,
            FontFamily = Font,
            FontSize = size,
            FontWeight = weight,
            Foreground = color ?? Text,
            TextWrapping = TextWrapping.NoWrap,
            VerticalAlignment = VerticalAlignment.Center
        };
    }

    public static TextBlock Display(string text, double size, FontWeight weight, Brush color = null)
    {
        var block = T(text, size, weight, color);
        block.FontFamily = DisplayFont;
        return block;
    }

    public static TextBlock Label(string text, Brush color = null)
    {
        var block = T(text.ToUpperInvariant(), 10.5, FontWeights.SemiBold, color ?? Dim);
        block.Margin = new Thickness(2, 0, 0, 8);
        return block;
    }

    public static TextBlock Icon(string glyph, double size, Brush color = null)
    {
        return new TextBlock
        {
            Text = glyph,
            FontFamily = IconFont,
            FontSize = size,
            Foreground = color ?? Text,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
    }

    public static Border Logo(double size)
    {
        var border = new Border
        {
            Width = size,
            Height = size,
            CornerRadius = new CornerRadius(size * 0.26),
            Background = Gradient(Color.FromRgb(255, 255, 255), Color.FromRgb(214, 214, 214), 90),
            ClipToBounds = true
        };
        try
        {
            if (_logoCache == null)
            {
                var frame = BitmapFrame.Create(new Uri("pack://application:,,,/AutoBackup.Logo.png", UriKind.Absolute));
                frame.Freeze();
                _logoCache = frame;
            }
            border.Child = new Image { Source = _logoCache, Stretch = Stretch.UniformToFill };
        }
        catch
        {
            border.Child = Icon("\uE7B8", size * 0.5, OnAccent);
        }
        return border;
    }

    public static Border IconTile(string glyph, Brush foreground, Brush background, double size = 52, double iconSize = 22)
    {
        return new Border
        {
            Width = size,
            Height = size,
            CornerRadius = new CornerRadius(14),
            Background = background,
            Child = Icon(glyph, iconSize, foreground),
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Center
        };
    }

    public static Border Card(UIElement content, Thickness? padding = null, double radius = 16)
    {
        return new Border
        {
            Background = CardBg,
            BorderBrush = Line,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(radius),
            Padding = padding ?? new Thickness(24),
            Child = content
        };
    }

    public static Button Button(string label, string glyph, ButtonKind kind, Action onClick = null, Brush foreground = null)
    {
        var brush = GetButtonColors(kind);
        var button = new Button
        {
            Height = kind == ButtonKind.Link ? 26 : (kind == ButtonKind.Ghost || kind == ButtonKind.DangerGhost ? 36 : 40),
            MinWidth = kind == ButtonKind.Link ? 0 : 84,
            Padding = kind == ButtonKind.Link ? new Thickness(2, 0, 2, 0) : new Thickness(kind == ButtonKind.Ghost || kind == ButtonKind.DangerGhost ? 12 : 16, 0, kind == ButtonKind.Ghost || kind == ButtonKind.DangerGhost ? 12 : 16, 0),
            Background = brush.Item1,
            BorderBrush = brush.Item3,
            BorderThickness = new Thickness(brush.Item4),
            Foreground = foreground ?? brush.Item2,
            FontFamily = Font,
            FontSize = 13,
            FontWeight = FontWeights.SemiBold,
            Cursor = Cursors.Hand,
            FocusVisualStyle = null,
            SnapsToDevicePixels = true,
            Template = BuildButtonTemplate(brush.Item5, brush.Item6, brush.Item7)
        };
        var panel = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        if (!string.IsNullOrEmpty(glyph))
        {
            var icon = Icon(glyph, 13.5, foreground ?? brush.Item2);
            icon.Margin = new Thickness(0, 0.5, 8, 0);
            panel.Children.Add(icon);
        }
        panel.Children.Add(T(label, 13, FontWeights.SemiBold, foreground ?? brush.Item2));
        button.Content = panel;
        AutomationProperties.SetName(button, label);
        if (onClick != null)
        {
            button.Click += delegate { onClick(); };
        }
        return button;
    }

    public static Button IconButton(string glyph, string tooltip, Action onClick, ButtonKind kind = ButtonKind.Ghost, double size = 34, double iconSize = 14)
    {
        Brush fg = kind == ButtonKind.DangerGhost ? Danger : Muted;
        var brush = GetButtonColors(kind);
        var button = new Button
        {
            Width = size,
            Height = size,
            Padding = new Thickness(0),
            Background = brush.Item1,
            BorderThickness = new Thickness(0),
            Cursor = Cursors.Hand,
            FocusVisualStyle = null,
            Content = Icon(glyph, iconSize, fg),
            ToolTip = tooltip,
            Template = BuildIconButtonTemplate(brush.Item5, brush.Item6, size * 0.26)
        };
        AutomationProperties.SetName(button, tooltip);
        button.Click += delegate { onClick(); };
        return button;
    }

    public static CheckBox CheckBox(string label, bool isChecked = false, string automationName = null)
    {
        return CheckBox(T(label, 13, Text), isChecked, automationName ?? label);
    }

    public static CheckBox CheckBox(UIElement content, bool isChecked = false, string automationName = null)
    {
        var check = Icon("\uE73E", 10, OnAccent);
        check.Visibility = Visibility.Collapsed;
        var box = new Border
        {
            Width = 18,
            Height = 18,
            CornerRadius = new CornerRadius(5),
            BorderThickness = new Thickness(1),
            BorderBrush = LineStrong,
            Background = Surface,
            Child = check,
            VerticalAlignment = VerticalAlignment.Center
        };
        var row = new DockPanel { LastChildFill = true, VerticalAlignment = VerticalAlignment.Center };
        DockPanel.SetDock(box, Dock.Left);
        row.Children.Add(box);
        if (content != null)
        {
            var element = content as FrameworkElement;
            if (element != null)
            {
                element.Margin = new Thickness(10, 0, 0, 0);
                element.VerticalAlignment = VerticalAlignment.Center;
            }
            row.Children.Add(content);
        }

        var template = new ControlTemplate(typeof(System.Windows.Controls.CheckBox));
        var root = new FrameworkElementFactory(typeof(ContentPresenter));
        root.SetValue(ContentPresenter.ContentProperty, new TemplateBindingExtension(System.Windows.Controls.CheckBox.ContentProperty));
        root.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
        root.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Stretch);
        template.VisualTree = root;

        var cb = new System.Windows.Controls.CheckBox
        {
            IsChecked = isChecked,
            Content = row,
            Cursor = Cursors.Hand,
            FocusVisualStyle = null,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            Template = template
        };
        if (!string.IsNullOrEmpty(automationName))
        {
            AutomationProperties.SetName(cb, automationName);
        }
        cb.Checked += delegate
        {
            box.Background = Accent;
            box.BorderBrush = Accent;
            check.Visibility = Visibility.Visible;
        };
        cb.Unchecked += delegate
        {
            box.Background = Surface;
            box.BorderBrush = LineStrong;
            check.Visibility = Visibility.Collapsed;
        };
        if (isChecked)
        {
            box.Background = Accent;
            box.BorderBrush = Accent;
            check.Visibility = Visibility.Visible;
        }
        cb.MouseEnter += delegate { if (cb.IsChecked != true) box.BorderBrush = AccentHover; };
        cb.MouseLeave += delegate { if (cb.IsChecked != true) box.BorderBrush = LineStrong; };
        return cb;
    }

    public static TextBox Input(string value, string automationName = null, bool readOnly = false)
    {
        var box = new TextBox
        {
            Text = value ?? string.Empty,
            Style = InputStyleInternal,
            Tag = automationName,
            IsReadOnly = readOnly
        };
        if (readOnly)
        {
            box.Foreground = Muted;
            box.Background = Bg2;
        }
        if (!string.IsNullOrEmpty(automationName))
        {
            AutomationProperties.SetName(box, automationName);
        }
        return box;
    }

    public static Grid PathInput(TextBox box, Button browseButton)
    {
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(10) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        Grid.SetColumn(box, 0);
        Grid.SetColumn(browseButton, 2);
        browseButton.Height = 42;
        grid.Children.Add(box);
        grid.Children.Add(browseButton);
        return grid;
    }

    public static StackPanel Field(string label, UIElement control, string hint = null)
    {
        var panel = new StackPanel { Margin = new Thickness(0, 0, 0, 18) };
        panel.Children.Add(Label(label));
        panel.Children.Add(control);
        if (!string.IsNullOrEmpty(hint))
        {
            var hintBlock = T(hint, 11.5, Dim);
            hintBlock.TextWrapping = TextWrapping.Wrap;
            hintBlock.Margin = new Thickness(2, 7, 0, 0);
            hintBlock.VerticalAlignment = VerticalAlignment.Top;
            panel.Children.Add(hintBlock);
        }
        return panel;
    }

    public static Border Badge(string text, Brush foreground, Brush background)
    {
        return new Border
        {
            Background = background,
            CornerRadius = new CornerRadius(20),
            Padding = new Thickness(10, 0, 10, 0),
            Height = 24,
            Child = T(text, 10.5, FontWeights.SemiBold, foreground),
            VerticalAlignment = VerticalAlignment.Center
        };
    }

    public static Border Divider()
    {
        return new Border { Height = 1, Background = Line, Margin = new Thickness(0, 4, 0, 4) };
    }

    public static ScrollViewer ScrollHost(UIElement content)
    {
        return new ScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Content = content,
            Focusable = false,
            Padding = new Thickness(0, 0, 8, 0)
        };
    }

    public static TextBlock PathText(string text, double size = 12.5, Brush color = null)
    {
        var block = T(text, size, color ?? Muted);
        block.TextTrimming = TextTrimming.CharacterEllipsis;
        block.ToolTip = text;
        return block;
    }

    public static void FadeIn(UIElement element, double offset = 8)
    {
        var transform = new TranslateTransform(0, offset);
        element.RenderTransform = transform;
        element.Opacity = 0;
        var fade = new System.Windows.Media.Animation.DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(170))
        {
            EasingFunction = new System.Windows.Media.Animation.CubicEase { EasingMode = System.Windows.Media.Animation.EasingMode.EaseOut }
        };
        var move = new System.Windows.Media.Animation.DoubleAnimation(offset, 0, TimeSpan.FromMilliseconds(200))
        {
            EasingFunction = new System.Windows.Media.Animation.CubicEase { EasingMode = System.Windows.Media.Animation.EasingMode.EaseOut }
        };
        element.BeginAnimation(UIElement.OpacityProperty, fade);
        transform.BeginAnimation(TranslateTransform.YProperty, move);
    }

    public static string TruncateMiddle(string value, int max)
    {
        if (string.IsNullOrEmpty(value) || value.Length <= max)
        {
            return value ?? string.Empty;
        }
        var half = Math.Max(4, (max - 3) / 2);
        return value.Substring(0, half) + "..." + value.Substring(value.Length - half);
    }

    private static (Brush, Brush, Brush, int, Brush, Brush, Brush) GetButtonColors(ButtonKind kind)
    {
        switch (kind)
        {
            case ButtonKind.Primary:
                return (Accent, OnAccent, Brushes.Transparent, 0, AccentHover, AccentPressed, Accent);
            case ButtonKind.Secondary:
                return (SurfaceHover, Text, LineStrong, 1, Hex("#242424"), SurfacePressed, LineStrong);
            case ButtonKind.DangerGhost:
                return (Brushes.Transparent, Danger, Brushes.Transparent, 0, DangerSoft, Hex("#2E2E2E"), Danger);
            case ButtonKind.Link:
                return (Brushes.Transparent, Accent, Brushes.Transparent, 0, AccentSoft, Brushes.Transparent, Accent);
            default:
                return (Brushes.Transparent, Muted, Brushes.Transparent, 0, SurfaceHover, SurfacePressed, Muted);
        }
    }

    private static ControlTemplate BuildButtonTemplate(Brush hover, Brush pressed, Brush accentBorder)
    {
        var border = new FrameworkElementFactory(typeof(Border), "bd");
        border.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(Control.BackgroundProperty));
        border.SetValue(Border.BorderBrushProperty, new TemplateBindingExtension(Control.BorderBrushProperty));
        border.SetValue(Border.BorderThicknessProperty, new TemplateBindingExtension(Control.BorderThicknessProperty));
        border.SetValue(Border.CornerRadiusProperty, new CornerRadius(10));
        border.SetValue(Border.SnapsToDevicePixelsProperty, true);
        var content = new FrameworkElementFactory(typeof(ContentPresenter));
        content.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
        content.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
        content.SetValue(FrameworkElement.MarginProperty, new TemplateBindingExtension(Control.PaddingProperty));
        content.SetValue(ContentPresenter.RecognizesAccessKeyProperty, false);
        border.AppendChild(content);

        var template = new ControlTemplate(typeof(Button)) { VisualTree = border };
        var hoverTrigger = new Trigger { Property = UIElement.IsMouseOverProperty, Value = true };
        hoverTrigger.Setters.Add(new Setter(Border.BackgroundProperty, hover) { TargetName = "bd" });
        template.Triggers.Add(hoverTrigger);
        var pressedTrigger = new Trigger { Property = ButtonBase.IsPressedProperty, Value = true };
        pressedTrigger.Setters.Add(new Setter(Border.BackgroundProperty, pressed) { TargetName = "bd" });
        template.Triggers.Add(pressedTrigger);
        var disabledTrigger = new Trigger { Property = UIElement.IsEnabledProperty, Value = false };
        disabledTrigger.Setters.Add(new Setter(UIElement.OpacityProperty, 0.45));
        template.Triggers.Add(disabledTrigger);
        return template;
    }

    private static ControlTemplate BuildIconButtonTemplate(Brush hover, Brush pressed, double radius)
    {
        var border = new FrameworkElementFactory(typeof(Border), "bd");
        border.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(Control.BackgroundProperty));
        border.SetValue(Border.CornerRadiusProperty, new CornerRadius(radius));
        border.SetValue(Border.SnapsToDevicePixelsProperty, true);
        var content = new FrameworkElementFactory(typeof(ContentPresenter));
        content.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
        content.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
        border.AppendChild(content);
        var template = new ControlTemplate(typeof(Button)) { VisualTree = border };
        var hoverTrigger = new Trigger { Property = UIElement.IsMouseOverProperty, Value = true };
        hoverTrigger.Setters.Add(new Setter(Border.BackgroundProperty, hover) { TargetName = "bd" });
        template.Triggers.Add(hoverTrigger);
        var pressedTrigger = new Trigger { Property = ButtonBase.IsPressedProperty, Value = true };
        pressedTrigger.Setters.Add(new Setter(Border.BackgroundProperty, pressed) { TargetName = "bd" });
        template.Triggers.Add(pressedTrigger);
        var disabledTrigger = new Trigger { Property = UIElement.IsEnabledProperty, Value = false };
        disabledTrigger.Setters.Add(new Setter(UIElement.OpacityProperty, 0.4));
        template.Triggers.Add(disabledTrigger);
        return template;
    }

    private static Style BuildInputStyle()
    {
        var style = new Style(typeof(TextBox));
        style.Setters.Add(new Setter(Control.BackgroundProperty, Surface));
        style.Setters.Add(new Setter(Control.ForegroundProperty, Text));
        style.Setters.Add(new Setter(Control.BorderBrushProperty, Line));
        style.Setters.Add(new Setter(Control.BorderThicknessProperty, new Thickness(1)));
        style.Setters.Add(new Setter(Control.FontFamilyProperty, Font));
        style.Setters.Add(new Setter(Control.FontSizeProperty, 13.5));
        style.Setters.Add(new Setter(FrameworkElement.HeightProperty, 42.0));
        style.Setters.Add(new Setter(Control.VerticalContentAlignmentProperty, VerticalAlignment.Center));
        style.Setters.Add(new Setter(TextBoxBase.CaretBrushProperty, Accent));
        style.Setters.Add(new Setter(TextBoxBase.SelectionBrushProperty, AccentSelection));
        style.Setters.Add(new Setter(TextBoxBase.SelectionOpacityProperty, 0.5));
        style.Setters.Add(new Setter(TextBoxBase.IsInactiveSelectionHighlightEnabledProperty, true));

        var border = new FrameworkElementFactory(typeof(Border), "bd");
        border.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(Control.BackgroundProperty));
        border.SetValue(Border.BorderBrushProperty, new TemplateBindingExtension(Control.BorderBrushProperty));
        border.SetValue(Border.BorderThicknessProperty, new TemplateBindingExtension(Control.BorderThicknessProperty));
        border.SetValue(Border.CornerRadiusProperty, new CornerRadius(9));
        border.SetValue(Border.SnapsToDevicePixelsProperty, true);
        var scroll = new FrameworkElementFactory(typeof(ScrollViewer), "PART_ContentHost");
        scroll.SetValue(FrameworkElement.MarginProperty, new Thickness(12, 0, 12, 0));
        scroll.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
        scroll.SetValue(ScrollViewer.HorizontalScrollBarVisibilityProperty, ScrollBarVisibility.Hidden);
        scroll.SetValue(ScrollViewer.VerticalScrollBarVisibilityProperty, ScrollBarVisibility.Hidden);
        border.AppendChild(scroll);
        var template = new ControlTemplate(typeof(TextBox)) { VisualTree = border };
        style.Setters.Add(new Setter(Control.TemplateProperty, template));

        var hoverTrigger = new Trigger { Property = UIElement.IsMouseOverProperty, Value = true };
        hoverTrigger.Setters.Add(new Setter(Control.BorderBrushProperty, LineStrong));
        style.Triggers.Add(hoverTrigger);
        var focusTrigger = new Trigger { Property = UIElement.IsKeyboardFocusWithinProperty, Value = true };
        focusTrigger.Setters.Add(new Setter(Control.BorderBrushProperty, Accent));
        style.Triggers.Add(focusTrigger);
        var readonlyTrigger = new Trigger { Property = TextBoxBase.IsReadOnlyProperty, Value = true };
        readonlyTrigger.Setters.Add(new Setter(Control.BorderBrushProperty, Line));
        style.Triggers.Add(readonlyTrigger);
        return style;
    }

    private static Style BuildToolTipStyle()
    {
        var style = new Style(typeof(ToolTip));
        style.Setters.Add(new Setter(Control.ForegroundProperty, Text));
        style.Setters.Add(new Setter(Control.BackgroundProperty, Hex("#1C1C1C")));
        style.Setters.Add(new Setter(Control.BorderBrushProperty, LineStrong));
        style.Setters.Add(new Setter(Control.BorderThicknessProperty, new Thickness(1)));
        style.Setters.Add(new Setter(Control.FontFamilyProperty, Font));
        style.Setters.Add(new Setter(Control.FontSizeProperty, 11.5));
        style.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(9, 6, 9, 6)));
        var border = new FrameworkElementFactory(typeof(Border));
        border.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(Control.BackgroundProperty));
        border.SetValue(Border.BorderBrushProperty, new TemplateBindingExtension(Control.BorderBrushProperty));
        border.SetValue(Border.BorderThicknessProperty, new TemplateBindingExtension(Control.BorderThicknessProperty));
        border.SetValue(Border.CornerRadiusProperty, new CornerRadius(7));
        border.SetValue(Border.PaddingProperty, new TemplateBindingExtension(Control.PaddingProperty));
        var content = new FrameworkElementFactory(typeof(ContentPresenter));
        content.SetValue(ContentPresenter.RecognizesAccessKeyProperty, false);
        border.AppendChild(content);
        style.Setters.Add(new Setter(Control.TemplateProperty, new ControlTemplate(typeof(ToolTip)) { VisualTree = border }));
        return style;
    }

    private static Style BuildScrollBarStyle()
    {
        var style = new Style(typeof(ScrollBar));
        style.Setters.Add(new Setter(FrameworkElement.WidthProperty, 10.0));
        style.Setters.Add(new Setter(Control.BackgroundProperty, Brushes.Transparent));
        style.Setters.Add(new Setter(FrameworkElement.MarginProperty, new Thickness(2, 0, 0, 0)));

        var track = new FrameworkElementFactory(typeof(ScrollTrack), "PART_Track");
        track.SetValue(Track.IsDirectionReversedProperty, true);
        track.SetValue(UIElement.FocusableProperty, false);
        track.SetValue(Track.OrientationProperty, new TemplateBindingExtension(ScrollBar.OrientationProperty));

        var root = new FrameworkElementFactory(typeof(Grid));
        root.AppendChild(track);
        var template = new ControlTemplate(typeof(ScrollBar)) { VisualTree = root };
        style.Setters.Add(new Setter(Control.TemplateProperty, template));
        return style;
    }

    private static ControlTemplate BuildThumbTemplate()
    {
        var thumbBorder = new FrameworkElementFactory(typeof(Border), "thumb");
        thumbBorder.SetValue(Border.BackgroundProperty, Hex("#3A3A3A"));
        thumbBorder.SetValue(Border.CornerRadiusProperty, new CornerRadius(3));
        thumbBorder.SetValue(FrameworkElement.MarginProperty, new Thickness(2, 2, 2, 2));
        var template = new ControlTemplate(typeof(Thumb)) { VisualTree = thumbBorder };
        var hover = new Trigger { Property = UIElement.IsMouseOverProperty, Value = true };
        hover.Setters.Add(new Setter(Border.BackgroundProperty, Hex("#5C5C5C")) { TargetName = "thumb" });
        template.Triggers.Add(hover);
        return template;
    }

    private sealed class ScrollTrack : Track
    {
        public ScrollTrack()
        {
            Thumb = new Thumb
            {
                MinHeight = 22,
                Template = BuildThumbTemplate()
            };
            DecreaseRepeatButton = new RepeatButton
            {
                Command = ScrollBar.PageUpCommand,
                Opacity = 0,
                Focusable = false,
                IsTabStop = false
            };
            IncreaseRepeatButton = new RepeatButton
            {
                Command = ScrollBar.PageDownCommand,
                Opacity = 0,
                Focusable = false,
                IsTabStop = false
            };
        }
    }
}

public class ProgressBarEx : Grid
{
    private readonly Border _fill;
    private readonly Border _track;
    private double _value;

    public ProgressBarEx()
    {
        Height = 8;
        _track = new Border
        {
            CornerRadius = new CornerRadius(4),
            Background = Ui.Hex("#1A1A1A"),
            BorderBrush = Ui.Line,
            BorderThickness = new Thickness(1)
        };
        _fill = new Border
        {
            CornerRadius = new CornerRadius(3),
            Background = new LinearGradientBrush(Color.FromRgb(255, 255, 255), Color.FromRgb(198, 198, 198), 0),
            HorizontalAlignment = HorizontalAlignment.Left,
            Visibility = Visibility.Collapsed
        };
        Children.Add(_track);
        Children.Add(_fill);
        SizeChanged += delegate { UpdateFill(); };
    }

    public double Value
    {
        get { return _value; }
        set
        {
            var clamped = Math.Max(0, Math.Min(100, value));
            if (Math.Abs(clamped - _value) < 0.1)
            {
                return;
            }
            _value = clamped;
            UpdateFill();
        }
    }

    private void UpdateFill()
    {
        var width = ActualWidth * (_value / 100.0);
        _fill.Width = Math.Max(0, width);
        _fill.Visibility = _value > 0.5 ? Visibility.Visible : Visibility.Collapsed;
    }
}
