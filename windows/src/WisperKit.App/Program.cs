using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using H.NotifyIcon;
using H.NotifyIcon.Core;
using WisperKit.App.UI;
using WisperKit.Core;

namespace WisperKit.App;

internal static class Program
{
    private const string MutexName = @"Local\WisperKit";
    private const string ShowEventName = @"Local\WisperKit.ShowSettings";

    [STAThread]
    private static int Main(string[] args)
    {
        var snapshots = Option(args, "--snapshots");
        if (snapshots is not null) return Snapshots.Render(snapshots, args.Contains("--first-launch"));

        using var mutex = new Mutex(true, MutexName, out var first);
        if (!first)
        {
            // Déjà lancé : on demande à l'instance existante d'ouvrir ses réglages.
            try { EventWaitHandle.OpenExisting(ShowEventName).Set(); } catch { }
            return 0;
        }

        var app = CreateApplication();
        app.DispatcherUnhandledException += (_, e) =>
        {
            DiagnosticLog.Write($"exception : {e.Exception}");
            e.Handled = true;
        };
        AppDomain.CurrentDomain.UnhandledException += (_, e) => DiagnosticLog.Write($"exception fatale : {e.ExceptionObject}");

        using var controller = new AppController();
        SettingsWindow? settings = null;
        void OpenSettings()
        {
            if (settings is { IsLoaded: true })
            {
                settings.Activate();
                return;
            }
            settings = new SettingsWindow(controller, controller.IsFirstLaunch ? SettingsPage.General : SettingsPage.General);
            settings.Closed += (_, _) => settings = null;
            settings.Show();
            settings.Activate();
        }

        var popupHost = new Border();
        using var tray = new TaskbarIcon
        {
            ToolTipText = "WisperKit",
            IconSource = TrayMenu.TrayIcon(controller.Dictation.State, controller.ModelStatus),
            PopupActivation = PopupActivationMode.LeftOrRightClick,
            TrayPopup = popupHost,
            NoLeftClickDelay = true,
        };
        void ClosePopup() => tray.CloseTrayPopup();
        tray.TrayPopupOpen += (_, _) =>
            popupHost.Child = TrayMenu.Build(controller, ClosePopup, OpenSettings, () => app.Shutdown());
        tray.ForceCreate(false);

        controller.Changed += () => app.Dispatcher.BeginInvoke(() =>
        {
            tray.IconSource = TrayMenu.TrayIcon(controller.Dictation.State, controller.ModelStatus);
            tray.ToolTipText = "WisperKit · " + controller.StatusLine;
        });

        // Signal d'une seconde instance → ouvrir les réglages.
        using var showEvent = new EventWaitHandle(false, EventResetMode.AutoReset, ShowEventName);
        var listener = new Thread(() =>
        {
            while (showEvent.WaitOne()) app.Dispatcher.BeginInvoke(OpenSettings);
        }) { IsBackground = true };
        listener.Start();

        if (controller.IsFirstLaunch || !args.Contains("--background"))
            app.Dispatcher.BeginInvoke(OpenSettings, DispatcherPriority.ApplicationIdle);

        return app.Run();
    }

    /// <summary>Thème Fluent sombre de WPF : barres de défilement, menus et progression sombres.</summary>
    internal static Application CreateApplication()
    {
        var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
#pragma warning disable WPF0001 // API ThemeMode marquée expérimentale
        app.ThemeMode = ThemeMode.Dark;
#pragma warning restore WPF0001
        return app;
    }

    private static string? Option(string[] args, string name)
    {
        var i = Array.IndexOf(args, name);
        return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
    }
}

/// <summary>Export PNG de chaque écran (vérification visuelle en CI, sans micro ni modèle) :
/// WisperKit.exe --snapshots &lt;dossier&gt; [--first-launch]</summary>
internal static class Snapshots
{
    public static int Render(string directory, bool firstLaunch)
    {
        System.IO.Directory.CreateDirectory(directory);
        var app = Program.CreateApplication();
        using var controller = new AppController(renderOnly: true);
        foreach (var lang in new[] { "en", "fr" })
        {
            Localizer.SetLanguage(lang);
            foreach (var page in Enum.GetValues<SettingsPage>())
            {
                var window = new SettingsWindow(controller, page);
                var content = (FrameworkElement)window.Content;
                window.Content = null;
                Save(new Border { Background = Theme.Background, Child = content, Width = 780, Height = 580 },
                     System.IO.Path.Combine(directory, $"settings-{page.ToString().ToLowerInvariant()}-{lang}.png"));
                window.Close();
            }
            Save(TrayMenu.Build(controller, () => { }, () => { }, () => { }),
                 System.IO.Path.Combine(directory, $"tray-{lang}.png"));
        }
        foreach (var (name, level, t, transcribing) in new[] { ("hud-voice", 0.85, 1.3, false), ("hud-silence", 0.0, 2.1, false), ("hud-transcribing", 0.0, 0.7, true) })
        {
            var view = new WaveView { Time = t, Level = level, Elapsed = 4, Transcribing = transcribing };
            Save(new Border
            {
                Padding = new Thickness(60, 38, 60, 38),
                Background = new LinearGradientBrush(Color.FromRgb(26, 30, 61), Color.FromRgb(56, 26, 71), 45),
                Child = view,
            }, System.IO.Path.Combine(directory, $"{name}.png"));
        }
        Save(new Image { Source = TrayMenu.TrayIcon(DictationController.DictationState.Recording, ModelStatus.Ready), Width = 64, Height = 64 },
             System.IO.Path.Combine(directory, "tray-icon-recording.png"));
        app.Shutdown();
        return 0;
    }

    private static void Save(FrameworkElement element, string path)
    {
        element.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        var size = new Size(double.IsNaN(element.Width) ? element.DesiredSize.Width : element.Width,
                            double.IsNaN(element.Height) ? element.DesiredSize.Height : element.Height);
        element.Arrange(new Rect(size));
        element.UpdateLayout();
        const double scale = 2;
        var bitmap = new RenderTargetBitmap((int)(size.Width * scale), (int)(size.Height * scale), 96 * scale, 96 * scale, PixelFormats.Pbgra32);
        bitmap.Render(element);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var file = System.IO.File.Create(path);
        encoder.Save(file);
    }
}
