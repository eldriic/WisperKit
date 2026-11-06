using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using WisperKit.Core;
using static WisperKit.Core.Localizer;

namespace WisperKit.App.UI;

/// <summary>Contenu du menu de la zone de notification (même structure que le menu macOS).</summary>
internal static class TrayMenu
{
    public static Border Build(AppController app, Action close, Action openSettings, Action quit)
    {
        var stack = new StackPanel();

        var header = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(10, 8, 10, 8) };
        header.Children.Add(new Image
        {
            Source = new BitmapImage(new Uri("pack://application:,,,/Assets/icon-256.png")),
            Width = 34, Height = 34, Margin = new Thickness(0, 0, 10, 0),
        });
        var titles = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        titles.Children.Add(Theme.Label("WisperKit"));
        var status = Theme.Label(app.StatusLine, 11, app.Dictation.IsRecording ? Theme.Red : Theme.Secondary);
        status.MaxWidth = 200;
        titles.Children.Add(status);
        header.Children.Add(titles);
        stack.Children.Add(header);
        stack.Children.Add(Separator());

        var recording = app.Dictation.IsRecording;
        stack.Children.Add(Item(recording ? "" : "", recording ? L("Stop dictation") : L("Start dictation"),
            app.ShortcutLabel, null, app.CanDictate || recording, () => { close(); app.ToggleDictation(); }));
        if (app.Dictation.LastText is { } last)
            stack.Children.Add(Item("", L("Last dictation"), null, last, true, () =>
            {
                Platform.TextInserter.SetClipboardText(last, transient: false);
                close();
            }, trailingGlyph: ""));
        stack.Children.Add(Separator());
        stack.Children.Add(Item("", L("Settings…"), null, null, true, () => { close(); openSettings(); }));
        stack.Children.Add(Item("", L("Quit"), null, null, true, quit));

        return new Border
        {
            Child = stack,
            Width = 280,
            Padding = new Thickness(6),
            Background = Theme.Popover,
            BorderBrush = Theme.PopoverStroke,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(12),
        };
    }

    private static UIElement Separator() => new Border
    {
        Height = 1, Background = Theme.PopoverStroke, Margin = new Thickness(10, 4, 10, 4),
    };

    private static Border Item(string glyph, string title, string? shortcut, string? subtitle, bool enabled, Action action,
                               string? trailingGlyph = null)
    {
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(26) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var color = enabled ? Theme.Text : Theme.Secondary;
        grid.Children.Add(Theme.Icon(glyph, 14, color));
        var texts = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        texts.Children.Add(Theme.Label(title, 13, color));
        if (subtitle is not null)
        {
            var sub = Theme.Label(subtitle, 11, Theme.Secondary);
            sub.TextWrapping = TextWrapping.NoWrap;
            sub.TextTrimming = TextTrimming.CharacterEllipsis;
            texts.Children.Add(sub);
        }
        Grid.SetColumn(texts, 1);
        grid.Children.Add(texts);
        UIElement? trailing = shortcut is not null ? Theme.Label(shortcut, 11.5, Theme.Secondary)
                            : trailingGlyph is not null ? Theme.Icon(trailingGlyph, 12, Theme.Secondary) : null;
        if (trailing is not null)
        {
            ((FrameworkElement)trailing).VerticalAlignment = VerticalAlignment.Center;
            Grid.SetColumn(trailing, 2);
            grid.Children.Add(trailing);
        }
        var item = new Border
        {
            Child = grid,
            Padding = new Thickness(10, 7, 10, 7),
            CornerRadius = new CornerRadius(8),
            Background = Brushes.Transparent,
            Cursor = enabled ? Cursors.Hand : Cursors.Arrow,
        };
        if (enabled)
        {
            item.MouseEnter += (_, _) => item.Background = Theme.PopoverStroke;
            item.MouseLeave += (_, _) => item.Background = Brushes.Transparent;
            item.MouseLeftButtonUp += (_, _) => action();
        }
        return item;
    }

    /// <summary>Icône de la zone de notification : logo + pastille rouge (écoute) ou bleue (transcription).</summary>
    public static ImageSource TrayIcon(DictationController.DictationState state, ModelStatus model)
    {
        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen())
        {
            var logo = new BitmapImage(new Uri("pack://application:,,,/Assets/icon-256.png"));
            dc.DrawImage(logo, new Rect(0, 0, 32, 32));
            Color? dot = state switch
            {
                DictationController.DictationState.Recording => Color.FromRgb(255, 69, 58),
                DictationController.DictationState.Transcribing => Theme.BlueColor,
                DictationController.DictationState.Failed => Color.FromRgb(255, 159, 10),
                _ => model is ModelStatus.Downloading or ModelStatus.Loading ? Color.FromRgb(160, 160, 160)
                   : model == ModelStatus.Failed ? Color.FromRgb(255, 159, 10) : null,
            };
            if (dot is { } c) dc.DrawEllipse(new SolidColorBrush(c), new Pen(Brushes.Black, 2), new Point(25, 25), 6, 6);
        }
        var bitmap = new RenderTargetBitmap(32, 32, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(visual);
        bitmap.Freeze();
        return bitmap;
    }
}
