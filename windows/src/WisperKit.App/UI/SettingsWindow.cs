using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using WisperKit.App.Platform;
using WisperKit.Core;
using static WisperKit.Core.Localizer;

namespace WisperKit.App.UI;

internal enum SettingsPage { General, Model, Keyboard, About }

/// <summary>Fenêtre Réglages : barre latérale + pages (même DA que la version macOS).</summary>
internal sealed class SettingsWindow : Window
{
    private readonly AppController _app;
    private SettingsPage _page;
    private readonly StackPanel _sidebar = new();
    private readonly ContentControl _content = new();
    private bool _recording;

    public SettingsWindow(AppController app, SettingsPage page = SettingsPage.General)
    {
        _app = app;
        _page = page;
        Title = "WisperKit Settings";
        Width = 780;
        Height = 580;
        MinWidth = 640;
        MinHeight = 420;
        Background = Theme.Background;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        Icon = new System.Windows.Media.Imaging.BitmapImage(new Uri("pack://application:,,,/Assets/icon-256.png"));
        SourceInitialized += (_, _) => NativeMethods.UseDarkTitleBar(new WindowInteropHelper(this).Handle);

        var root = new Grid();
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(200) });
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1) });
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        _sidebar.Margin = new Thickness(8, 16, 8, 14);
        root.Children.Add(_sidebar);
        var divider = new Border { Background = new SolidColorBrush(Color.FromRgb(0x1C, 0x1C, 0x1C)) };
        Grid.SetColumn(divider, 1);
        root.Children.Add(divider);
        var scroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Content = _content };
        Grid.SetColumn(scroll, 2);
        root.Children.Add(scroll);
        Content = root;

        app.Changed += Refresh;
        LanguageChanged += Refresh;
        Closed += (_, _) => { app.Changed -= Refresh; LanguageChanged -= Refresh; };
        Rebuild();
    }

    private void Refresh() => Dispatcher.BeginInvoke(Rebuild);

    public void Rebuild()
    {
        if (_recording) return; // ne pas effacer le message « appuie sur le nouveau raccourci »
        _sidebar.Children.Clear();
        _sidebar.Children.Add(WithMargin(Theme.Label(L("Settings"), 11, Theme.Secondary), new Thickness(10, 0, 0, 6)));
        foreach (var (page, glyph, title) in new[]
                 {
                     (SettingsPage.General, "", L("General")),
                     (SettingsPage.Model, "", L("Model")),
                     (SettingsPage.Keyboard, "", L("Keyboard")),
                     (SettingsPage.About, "", L("About")),
                 })
            _sidebar.Children.Add(SidebarItem(page, glyph, title));

        var body = new StackPanel { Margin = new Thickness(28, 24, 28, 28) };
        body.Children.Add(WithMargin(Theme.Label(PageTitle(_page), 24), new Thickness(0, 0, 0, 18)));
        switch (_page)
        {
            case SettingsPage.General: General(body); break;
            case SettingsPage.Model: Model(body); break;
            case SettingsPage.Keyboard: Keyboard(body); break;
            case SettingsPage.About: About(body); break;
        }
        _content.Content = body;
    }

    private static string PageTitle(SettingsPage p) => p switch
    {
        SettingsPage.General => L("General"),
        SettingsPage.Model => L("Model"),
        SettingsPage.Keyboard => L("Keyboard"),
        _ => L("About"),
    };

    private Border SidebarItem(SettingsPage page, string glyph, string title)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal };
        var icon = Theme.Icon(glyph, 14);
        icon.Width = 22;
        row.Children.Add(icon);
        row.Children.Add(Theme.Label(title));
        var item = new Border
        {
            Child = row,
            Padding = new Thickness(10, 7, 10, 7),
            CornerRadius = new CornerRadius(8),
            Background = page == _page ? Theme.Selection : Brushes.Transparent,
            Cursor = Cursors.Hand,
            Margin = new Thickness(0, 1, 0, 1),
        };
        item.MouseLeftButtonUp += (_, _) => { _page = page; Rebuild(); };
        return item;
    }

    // ---- Général ----

    private void General(StackPanel body)
    {
        var s = _app.Settings;
        if (_app.IsFirstLaunch) body.Children.Add(Welcome());

        body.Children.Add(Theme.SectionTitle("WisperKit"));
        body.Children.Add(Theme.Card(
            Theme.Row(L("Interface language"), L("Language of WisperKit's menus and settings."),
                Theme.PillMenu(s.InterfaceLanguage, Localizer.Languages.Select(l => (l.Id, l.NativeName)).ToList(),
                    v => _app.Update(x => x.InterfaceLanguage = v)))));

        body.Children.Add(Theme.SectionTitle(L("Dictation")));
        body.Children.Add(Theme.Card(
            Theme.Row(L("Insert automatically"), L("Writes the text into the frontmost app as soon as it is transcribed."),
                new Switch(s.AutoInsert, v => _app.Update(x => x.AutoInsert = v))),
            Theme.Row(L("Never use the clipboard"), L("Types the text key by key instead of pasting it. A bit slower; your clipboard is never touched."),
                new Switch(s.NeverUseClipboard, v => _app.Update(x => x.NeverUseClipboard = v))),
            s.NeverUseClipboard ? null : Theme.Row(L("Restore clipboard"), L("Puts back what you had copied before dictating."),
                new Switch(s.RestoreClipboard, v => _app.Update(x => x.RestoreClipboard = v))),
            Theme.Row(L("Dictation language"), L("Forcing the language avoids detection mistakes."),
                Theme.PillMenu(s.DictationLanguage, [("en", L("English")), ("fr", L("French")), ("auto", L("Auto-detect"))],
                    v => _app.Update(x => x.DictationLanguage = v))),
            Theme.Row(L("Sounds"), L("A chime when listening starts and ends."),
                new Switch(s.Sounds, v => _app.Update(x => x.Sounds = v))),
            Theme.Row(L("On-screen indicator"), L("Small waves in the middle of the screen while listening and transcribing."),
                new Switch(s.ShowIndicator, v => _app.Update(x => x.ShowIndicator = v)))));

        body.Children.Add(Theme.SectionTitle(L("Startup")));
        body.Children.Add(Theme.Card(
            Theme.Row(L("Start with Windows"), L("WisperKit opens when you sign in."),
                new Switch(_app.StartWithWindows, v => _app.StartWithWindows = v))));
    }

    private Border Welcome()
    {
        var stack = new StackPanel();
        stack.Children.Add(Theme.Label(L("Welcome to WisperKit"), 15, weight: FontWeights.SemiBold));
        stack.Children.Add(WithMargin(Theme.Label(L("A few steps and you can dictate anywhere. Everything stays on your PC."), 12, Theme.Secondary),
                                  new Thickness(0, 4, 0, 10)));
        var micStep = MicrophonePrivacy.IsBlocked
            ? L("Windows blocks the microphone for desktop apps: allow it in the privacy settings.")
            : L("Your microphone is ready. Windows may ask for permission the first time.");
        stack.Children.Add(Step(1, micStep, MicrophonePrivacy.IsBlocked
            ? Theme.PillButton(L("Open microphone settings"), MicrophonePrivacy.OpenSettings) : null));
        stack.Children.Add(Step(2, _app.ModelStatus switch
        {
            ModelStatus.Downloading => L("The transcription model is downloading (%d%%, about 550 MB, one time only).", (int)(_app.ModelProgress * 100)),
            ModelStatus.Loading => L("The model is loading…"),
            ModelStatus.Ready => L("The model is ready."),
            ModelStatus.Failed => L("Model download failed: %@ (retry from the Model tab).", L(_app.ModelError ?? "")),
            _ => L("The transcription model will be downloaded (about 550 MB, one time only)."),
        }, null));
        stack.Children.Add(Step(3, L("Put your cursor in any app, press %@, speak, then press it again.", _app.ShortcutLabel), null));
        return new Border
        {
            Child = stack,
            Padding = new Thickness(16),
            CornerRadius = new CornerRadius(14),
            Background = new SolidColorBrush(Color.FromArgb(26, Theme.BlueColor.R, Theme.BlueColor.G, Theme.BlueColor.B)),
            BorderBrush = new SolidColorBrush(Color.FromArgb(90, Theme.BlueColor.R, Theme.BlueColor.G, Theme.BlueColor.B)),
            BorderThickness = new Thickness(1),
            Margin = new Thickness(0, 0, 0, 20),
        };
    }

    private static Grid Step(int n, string text, UIElement? action)
    {
        var grid = new Grid { Margin = new Thickness(0, 4, 0, 4) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(30) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var badge = new Border
        {
            Width = 20, Height = 20, CornerRadius = new CornerRadius(10),
            Background = new SolidColorBrush(Color.FromArgb(128, Theme.BlueColor.R, Theme.BlueColor.G, Theme.BlueColor.B)),
            Child = new TextBlock { Text = n.ToString(), FontSize = 11, FontWeight = FontWeights.SemiBold, Foreground = Theme.Text,
                                    HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center },
            VerticalAlignment = VerticalAlignment.Top,
        };
        grid.Children.Add(badge);
        var label = Theme.Label(text, 12.5);
        Grid.SetColumn(label, 1);
        grid.Children.Add(label);
        if (action is not null) { Grid.SetColumn(action, 2); grid.Children.Add(action); }
        return grid;
    }

    // ---- Modèle ----

    private void Model(StackPanel body)
    {
        body.Children.Add(Theme.SectionTitle(L("Installed and available models")));
        body.Children.Add(Theme.Card(WhisperModel.All.Select(m => (UIElement?)Theme.Row(m.Title, $"{L(m.Detail)} · {L(m.Size)}", ModelTrailing(m),
            m.Id == _app.Settings.ModelId ? L("active") : null)).ToArray()));

        body.Children.Add(Theme.SectionTitle(L("Storage")));
        var usage = _app.Store.DiskUsage() / (1024.0 * 1024 * 1024);
        body.Children.Add(Theme.Card(Theme.Row(L("Models folder"), $"{_app.Store.Directory} · {L("%@ GB", usage.ToString("0.00", System.Globalization.CultureInfo.GetCultureInfo(Language)))}",
            Theme.PillButton(L("Show"), () =>
            {
                System.IO.Directory.CreateDirectory(_app.Store.Directory);
                Process.Start(new ProcessStartInfo("explorer.exe", $"\"{_app.Store.Directory}\"") { UseShellExecute = true });
            }))));
    }

    private UIElement ModelTrailing(WhisperModel m)
    {
        if (m.Id == _app.Settings.ModelId)
        {
            return _app.ModelStatus switch
            {
                ModelStatus.Downloading => Progress(_app.ModelProgress),
                ModelStatus.Loading => Theme.Label(L("Loading model…"), 12, Theme.Secondary),
                ModelStatus.Ready => Theme.Icon("", 14),
                ModelStatus.Failed => Theme.PillButton(L("Retry"), () => _ = _app.LoadActiveModelAsync()),
                _ => Theme.PillButton(L("Download"), () => _ = _app.LoadActiveModelAsync(), glyph: ""),
            };
        }
        if (_app.Downloads.TryGetValue(m.Id, out var f)) return Progress(f);
        if (_app.Store.IsInstalled(m))
        {
            var row = new StackPanel { Orientation = Orientation.Horizontal };
            var delete = Theme.PillButton(L("Delete model"), () => _app.Delete(m), Theme.Secondary, glyph: "");
            delete.Margin = new Thickness(0, 0, 8, 0);
            row.Children.Add(delete);
            row.Children.Add(Theme.PillButton(L("Use"), () => _app.Use(m)));
            return row;
        }
        return Theme.PillButton(L("Download"), () => _app.Download(m), glyph: "");
    }

    private static UIElement Progress(double fraction)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal };
        row.Children.Add(new ProgressBar
        {
            Width = 90, Height = 4, Minimum = 0, Maximum = 1, Value = fraction,
            Foreground = Theme.Blue, Background = Theme.Pill, BorderThickness = new Thickness(0),
            VerticalAlignment = VerticalAlignment.Center,
        });
        row.Children.Add(WithMargin(Theme.Label($"{(int)(fraction * 100)} %", 11, Theme.Secondary), new Thickness(8, 0, 0, 0)));
        return row;
    }

    // ---- Clavier ----

    private void Keyboard(StackPanel body)
    {
        var s = _app.Settings;
        var shortcutButton = Theme.PillButton(_app.ShortcutLabel, () => { });
        shortcutButton.Click += (_, _) =>
        {
            _recording = true;
            ((StackPanel)shortcutButton.Content).Children.Clear();
            ((StackPanel)shortcutButton.Content).Children.Add(Theme.Label(L("Press the new shortcut…"), 12, Theme.Blue));
            _app.RecordShortcut(_ => { _recording = false; Rebuild(); });
        };
        body.Children.Add(Theme.SectionTitle(L("Shortcut")));
        body.Children.Add(Theme.Card(
            Theme.Row(L("Start / stop dictation"), L("Works from any app."), shortcutButton),
            Theme.Row(L("Mode"), s.Mode == TriggerMode.Toggle ? L("Press once to start, press again to stop.")
                                                              : L("Speak while holding the shortcut, release to transcribe."),
                Theme.PillMenu(s.Mode, [(TriggerMode.Toggle, L("Toggle")), (TriggerMode.Hold, L("Hold"))],
                    v => _app.Update(x => x.Mode = v))),
            Theme.Row(L("Cancel with Esc"), L("Pressing Esc while listening discards the recording."),
                new Switch(s.EscapeCancels, v => _app.Update(x => x.EscapeCancels = v)))));
    }

    // ---- À propos ----

    private void About(StackPanel body)
    {
        var version = typeof(SettingsWindow).Assembly.GetName().Version?.ToString(3) ?? "?";
        body.Children.Add(Theme.Card(
            Theme.Row("WisperKit", L("Version %@", version), Theme.PillButton("GitHub", () => Open("https://github.com/eldriic/WisperKit"))),
            Theme.Row(L("Diagnostic log"), DiagnosticLog.FilePath, Theme.PillButton(L("Open"), () =>
            {
                if (System.IO.File.Exists(DiagnosticLog.FilePath)) Open(DiagnosticLog.FilePath);
            })),
            Theme.Row(L("Privacy"), L("Audio is transcribed on your PC and never leaves it. The only download is the model."),
                new Border())));
        body.Children.Add(WithMargin(Theme.Label(L("WisperKit is vibe-coded: built through conversation with an AI coding assistant. MIT license — Whisper by OpenAI, whisper.cpp, Whisper.net, NAudio, H.NotifyIcon."),
                                             11.5, Theme.Secondary), new Thickness(0, 0, 0, 0)));
    }

    private static void Open(string target) =>
        Process.Start(new ProcessStartInfo(target) { UseShellExecute = true });

    private static T WithMargin<T>(T element, Thickness margin) where T : FrameworkElement
    {
        element.Margin = margin;
        return element;
    }
}
