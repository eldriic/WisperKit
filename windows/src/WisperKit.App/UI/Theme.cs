using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace WisperKit.App.UI;

/// <summary>Palette et composants partagés (mêmes valeurs que la version macOS).</summary>
internal static class Theme
{
    public static readonly Color BackgroundColor = Colors.Black;
    public static readonly SolidColorBrush Background = Freeze(new SolidColorBrush(BackgroundColor));
    public static readonly SolidColorBrush Selection = Brush(0x21, 0x21, 0x21);
    public static readonly SolidColorBrush Stroke = Brush(0x2A, 0x2A, 0x2A);
    public static readonly SolidColorBrush Pill = Brush(0x2A, 0x2A, 0x2A);
    public static readonly SolidColorBrush Popover = Brush(0x2B, 0x2B, 0x2B);
    public static readonly SolidColorBrush PopoverStroke = Brush(0x3A, 0x3A, 0x3A);
    public static readonly SolidColorBrush Text = Brush(0xEC, 0xEC, 0xEC);
    public static readonly SolidColorBrush Secondary = Brush(0x9B, 0x9B, 0x9B);
    public static readonly Color BlueColor = Color.FromRgb(0x2F, 0x7C, 0xF6);
    public static readonly SolidColorBrush Blue = Freeze(new SolidColorBrush(BlueColor));
    public static readonly SolidColorBrush Green = Brush(0x4C, 0xD9, 0x64);
    public static readonly SolidColorBrush Danger = Brush(0xFF, 0x6B, 0x6B);
    public static readonly SolidColorBrush DangerFill = Brush(0x3A, 0x17, 0x17);
    public static readonly SolidColorBrush Red = Brush(0xFF, 0x45, 0x3A);
    public static readonly FontFamily Font = new("Segoe UI Variable Text, Segoe UI");
    public static readonly FontFamily Icons = new("Segoe Fluent Icons, Segoe MDL2 Assets");

    private static SolidColorBrush Brush(byte r, byte g, byte b) => Freeze(new SolidColorBrush(Color.FromRgb(r, g, b)));
    private static T Freeze<T>(T f) where T : Freezable { f.Freeze(); return f; }

    public static TextBlock Label(string text, double size = 13, Brush? color = null, FontWeight? weight = null) => new()
    {
        Text = text,
        FontSize = size,
        FontFamily = Font,
        Foreground = color ?? Text,
        FontWeight = weight ?? FontWeights.Normal,
        TextWrapping = TextWrapping.Wrap,
    };

    /// <summary>Glyphe Segoe Fluent Icons (ex. "" micro).</summary>
    public static TextBlock Icon(string glyph, double size = 15, Brush? color = null) => new()
    {
        Text = glyph,
        FontFamily = Icons,
        FontSize = size,
        Foreground = color ?? Text,
        VerticalAlignment = VerticalAlignment.Center,
    };

    public static TextBlock SectionTitle(string text)
    {
        var t = Label(text, 13, weight: FontWeights.SemiBold);
        t.Margin = new Thickness(0, 4, 0, 8);
        return t;
    }

    /// <summary>Carte à bord fin contenant des lignes séparées par un filet.</summary>
    public static Border Card(params UIElement?[] rows)
    {
        var stack = new StackPanel();
        var visible = rows.Where(r => r is not null).ToList();
        for (var i = 0; i < visible.Count; i++)
        {
            stack.Children.Add(visible[i]!);
            if (i < visible.Count - 1) stack.Children.Add(new Rectangle { Height = 1, Fill = Stroke });
        }
        return new Border
        {
            BorderBrush = Stroke,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(14),
            Padding = new Thickness(16, 0, 16, 0),
            Child = stack,
            Margin = new Thickness(0, 0, 0, 18),
        };
    }

    /// <summary>Ligne « titre + description grise » avec un contrôle aligné à droite.</summary>
    public static Grid Row(string title, string? description, UIElement trailing, string? badge = null)
    {
        var grid = new Grid { Margin = new Thickness(0, 12, 0, 12) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var texts = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 16, 0) };
        var titleLine = new StackPanel { Orientation = Orientation.Horizontal };
        titleLine.Children.Add(Label(title));
        if (badge is not null)
        {
            var b = Label(badge, 11, Blue);
            b.Margin = new Thickness(6, 1, 0, 0);
            titleLine.Children.Add(b);
        }
        texts.Children.Add(titleLine);
        if (description is not null)
        {
            var d = Label(description, 11.5, Secondary);
            d.Margin = new Thickness(0, 3, 0, 0);
            texts.Children.Add(d);
        }
        grid.Children.Add(texts);
        if (trailing is FrameworkElement fe) fe.VerticalAlignment = VerticalAlignment.Center;
        Grid.SetColumn(trailing, 1);
        grid.Children.Add(trailing);
        return grid;
    }

    /// <summary>Bouton en pilule grise.</summary>
    public static Button PillButton(string text, Action onClick, Brush? foreground = null, Brush? fill = null, string? glyph = null)
    {
        var content = new StackPanel { Orientation = Orientation.Horizontal };
        if (glyph is not null)
        {
            var g = Icon(glyph, 11, foreground ?? Text);
            g.Margin = new Thickness(0, 0, 6, 0);
            content.Children.Add(g);
        }
        content.Children.Add(Label(text, 12, foreground ?? Text));
        var button = new Button { Content = content, Cursor = Cursors.Hand, Template = PillTemplate(fill ?? Pill) };
        button.Click += (_, _) => onClick();
        return button;
    }

    /// <summary>Menu déroulant en pilule (libellé + chevron).</summary>
    public static Button PillMenu<T>(T selection, IReadOnlyList<(T Value, string Label)> options, Action<T> onSelect)
    {
        var current = options.FirstOrDefault(o => Equals(o.Value, selection)).Label ?? "";
        var content = new StackPanel { Orientation = Orientation.Horizontal };
        content.Children.Add(Label(current, 12));
        var chevron = Icon("", 9, Text);
        chevron.Margin = new Thickness(7, 1, 0, 0);
        content.Children.Add(chevron);
        var button = new Button { Content = content, Cursor = Cursors.Hand, Template = PillTemplate(Pill) };
        var menu = new ContextMenu
        {
            Background = Popover,
            Foreground = Text,
            BorderBrush = PopoverStroke,
            FontFamily = Font,
            PlacementTarget = button,
            Placement = PlacementMode.Bottom,
        };
        foreach (var (value, label) in options)
        {
            var item = new MenuItem { Header = label, IsChecked = Equals(value, selection), Foreground = Text, Background = Popover };
            item.Click += (_, _) => onSelect(value);
            menu.Items.Add(item);
        }
        button.Click += (_, _) => menu.IsOpen = true;
        return button;
    }

    private static ControlTemplate PillTemplate(Brush fill)
    {
        var border = new FrameworkElementFactory(typeof(Border));
        border.SetValue(Border.BackgroundProperty, fill);
        border.SetValue(Border.CornerRadiusProperty, new CornerRadius(999));
        border.SetValue(Border.PaddingProperty, new Thickness(12, 6, 12, 6));
        var presenter = new FrameworkElementFactory(typeof(ContentPresenter));
        presenter.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Center);
        presenter.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
        border.AppendChild(presenter);
        return new ControlTemplate(typeof(Button)) { VisualTree = border };
    }
}

/// <summary>Interrupteur bleu dessiné à la main (identique au style macOS des maquettes).</summary>
internal sealed class Switch : FrameworkElement
{
    private bool _isOn;
    public event Action<bool>? Toggled;

    public Switch(bool isOn, Action<bool> onToggle)
    {
        _isOn = isOn;
        Toggled += onToggle;
        Width = 36;
        Height = 20;
        Cursor = Cursors.Hand;
        Focusable = true;
    }

    public bool IsOn
    {
        get => _isOn;
        set { _isOn = value; InvalidateVisual(); }
    }

    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        IsOn = !IsOn;
        Toggled?.Invoke(IsOn);
        e.Handled = true;
    }

    protected override void OnRender(DrawingContext dc)
    {
        var track = _isOn ? Theme.Blue : new SolidColorBrush(Color.FromRgb(0x3A, 0x3A, 0x3A));
        dc.DrawRoundedRectangle(track, null, new Rect(0, 0, 36, 20), 10, 10);
        dc.DrawEllipse(Brushes.White, null, new Point(_isOn ? 26 : 10, 10), 8, 8);
    }
}
