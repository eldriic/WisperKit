using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using WisperKit.App.Platform;
using WisperKit.Core;
using static WisperKit.App.Platform.NativeMethods;

namespace WisperKit.App.UI;

/// <summary>Rendu pur de la pilule (vagues, halo, point + minuteur) — portage de HUDContent (macOS).</summary>
internal sealed class WaveView : FrameworkElement
{
    public const double PillWidth = 200, PillHeight = 64;
    private static readonly Color[] Palette =
    [
        Color.FromRgb(64, 140, 255), Color.FromRgb(130, 89, 255), Color.FromRgb(255, 79, 209), Color.FromRgb(89, 209, 255),
    ];

    public double Time { get; set; }
    public double Level { get; set; }
    public double Elapsed { get; set; }
    public bool Transcribing { get; set; }
    public string? Message { get; set; }

    public WaveView()
    {
        Width = PillWidth;
        Height = PillHeight;
    }

    protected override void OnRender(DrawingContext dc)
    {
        var pill = new Rect(0.5, 0.5, PillWidth - 1, PillHeight - 1);
        var radius = PillHeight / 2;
        dc.DrawRoundedRectangle(new SolidColorBrush(Color.FromArgb(235, 14, 14, 18)),
                                new Pen(new SolidColorBrush(Color.FromArgb(28, 255, 255, 255)), 1), pill, radius, radius);
        if (Message is not null)
        {
            var text = new FormattedText(Message, System.Globalization.CultureInfo.CurrentUICulture, FlowDirection.LeftToRight,
                                         new Typeface(Theme.Font, FontStyles.Normal, FontWeights.SemiBold, FontStretches.Normal),
                                         12, new SolidColorBrush(Color.FromArgb(230, 255, 255, 255)), 1.0)
            { MaxTextWidth = PillWidth - 24, TextAlignment = TextAlignment.Center };
            dc.DrawText(text, new Point(12, (PillHeight - text.Height) / 2));
            return;
        }

        dc.PushClip(new RectangleGeometry(pill, radius, radius));
        var level = Transcribing ? 0.1 : Level;
        var halo = new RadialGradientBrush(Color.FromArgb((byte)(255 * (0.16 + 0.36 * level)), 120, 110, 255), Color.FromArgb(0, 120, 110, 255))
        {
            Center = new Point(0.42, 0.5), GradientOrigin = new Point(0.42, 0.5), RadiusX = 0.55, RadiusY = 1.6,
        };
        dc.DrawRectangle(halo, null, pill);
        DrawWaves(dc, new Rect(0, 0, PillWidth - 56, PillHeight), Time,
                  Transcribing ? 0.12 : Math.Max(Level, 0.08), Transcribing ? 0.45 : 1);
        dc.Pop();

        // Point rouge qui pulse + minuteur (écoute) ; point bleu + « ··· » (transcription).
        var dotColor = Transcribing ? Color.FromRgb(140, 171, 255) : Color.FromRgb(255, 69, 58);
        var alpha = Transcribing ? 0.9 : 0.6 + 0.4 * Math.Abs(Math.Sin(Time * 3));
        dc.DrawEllipse(new SolidColorBrush(Color.FromArgb((byte)(255 * alpha), dotColor.R, dotColor.G, dotColor.B)), null,
                       new Point(PillWidth - 50, PillHeight / 2), 3, 3);
        var label = Transcribing ? new string('·', (int)(Time * 3) % 3 + 1) : Format(Elapsed);
        var ft = new FormattedText(label, System.Globalization.CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
                                   new Typeface(Theme.Font, FontStyles.Normal, FontWeights.SemiBold, FontStretches.Normal),
                                   12, new SolidColorBrush(Color.FromArgb(217, 255, 255, 255)), 1.0);
        dc.DrawText(ft, new Point(PillWidth - 42, (PillHeight - ft.Height) / 2));
    }

    public static string Format(double seconds)
    {
        var s = Math.Max(0, (int)seconds);
        return $"{s / 60}:{s % 60:00}";
    }

    /// <summary>Vagues superposées estompées aux deux bouts (même géométrie que WaveRenderer.swift).</summary>
    public static void DrawWaves(DrawingContext dc, Rect area, double t, double level, double speed)
    {
        double left = area.Left + 16, right = area.Right - 4, midY = area.Top + area.Height / 2;
        var amplitude = (area.Height / 2 - 9) * Math.Min(level, 1);
        const int steps = 70;
        for (var i = 0; i < Palette.Length; i++)
        {
            var a = amplitude * (1 - i * 0.12);
            var freq = 1.1 + i * 0.35;
            var phase = t * (1.3 + i * 0.4) * speed + i * 2;
            var geometry = new StreamGeometry();
            using (var ctx = geometry.Open())
            {
                for (var k = 0; k <= steps; k++)
                {
                    var u = (double)k / steps;
                    var p = new Point(left + u * (right - left), midY + Math.Sin(u * Math.PI * 2 * freq + phase) * a * Math.Pow(Math.Sin(Math.PI * u), 1.4));
                    if (k == 0) ctx.BeginFigure(p, true, true); else ctx.LineTo(p, true, false);
                }
                for (var k = steps; k >= 0; k--)
                {
                    var u = (double)k / steps;
                    ctx.LineTo(new Point(left + u * (right - left),
                        midY - Math.Sin(u * Math.PI * 2 * freq + phase + 1) * a * Math.Pow(Math.Sin(Math.PI * u), 1.4) * 0.7), true, false);
                }
            }
            geometry.Freeze();
            var c = Palette[i];
            var brush = new LinearGradientBrush
            {
                StartPoint = new Point(0, 0.5), EndPoint = new Point(1, 0.5),
                GradientStops =
                {
                    new GradientStop(Color.FromArgb(0, c.R, c.G, c.B), 0),
                    new GradientStop(Color.FromArgb(170, c.R, c.G, c.B), 0.5),
                    new GradientStop(Color.FromArgb(0, c.R, c.G, c.B), 1),
                },
            };
            dc.DrawGeometry(brush, null, geometry);
        }
        // Cœur lumineux (remplace le mélange additif de macOS).
        var core = new LinearGradientBrush
        {
            StartPoint = new Point(0, 0.5), EndPoint = new Point(1, 0.5),
            GradientStops =
            {
                new GradientStop(Color.FromArgb(0, 255, 255, 255), 0.1),
                new GradientStop(Color.FromArgb((byte)(120 * Math.Min(level * 1.5, 1)), 255, 255, 255), 0.5),
                new GradientStop(Color.FromArgb(0, 255, 255, 255), 0.9),
            },
        };
        dc.DrawRectangle(core, null, new Rect(left, midY - Math.Max(1.5, amplitude * 0.25), right - left, Math.Max(3, amplitude * 0.5)));
    }
}

/// <summary>Fenêtre flottante au centre de l'écran actif : topmost, jamais activée, transparente aux clics.</summary>
internal sealed class RecordingHud : Window
{
    private readonly DictationController _dictation;
    private readonly WaveView _view = new();
    private double _smoothed;
    private DateTime _startedAt = DateTime.Now;
    private CancellationTokenSource? _hide;

    public RecordingHud(DictationController dictation)
    {
        _dictation = dictation;
        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        ShowInTaskbar = false;
        ShowActivated = false;
        Topmost = true;
        ResizeMode = ResizeMode.NoResize;
        SizeToContent = SizeToContent.WidthAndHeight;
        Content = _view;
        Opacity = 0;
        SourceInitialized += (_, _) =>
        {
            var hwnd = new WindowInteropHelper(this).Handle;
            var ex = (long)GetWindowLongPtr(hwnd, GWL_EXSTYLE);
            SetWindowLongPtr(hwnd, GWL_EXSTYLE, (IntPtr)(ex | WS_EX_NOACTIVATE | WS_EX_TRANSPARENT | WS_EX_TOOLWINDOW | WS_EX_LAYERED));
        };
    }

    public void ShowRecording()
    {
        _view.Message = null;
        _startedAt = DateTime.Now;
        Reveal();
    }

    /// <summary>Court message (ex. texte seulement copié) puis masquage.</summary>
    public async void Flash(string message)
    {
        _view.Message = message;
        Reveal();
        var cts = new CancellationTokenSource();
        _hide = cts;
        try { await Task.Delay(1600, cts.Token); } catch (TaskCanceledException) { return; }
        HideHud();
    }

    private void Reveal()
    {
        _hide?.Cancel();
        if (!IsVisible)
        {
            Show();
            Center();
            System.Windows.Media.CompositionTarget.Rendering += OnFrame;
        }
        BeginAnimation(OpacityProperty, new System.Windows.Media.Animation.DoubleAnimation(1, TimeSpan.FromMilliseconds(150)));
    }

    public async void HideHud()
    {
        if (!IsVisible) return;
        var cts = new CancellationTokenSource();
        _hide = cts;
        BeginAnimation(OpacityProperty, new System.Windows.Media.Animation.DoubleAnimation(0, TimeSpan.FromMilliseconds(200)));
        try { await Task.Delay(220, cts.Token); } catch (TaskCanceledException) { return; }
        System.Windows.Media.CompositionTarget.Rendering -= OnFrame;
        Hide();
    }

    private void OnFrame(object? sender, EventArgs e)
    {
        var target = _dictation.State == DictationController.DictationState.Recording ? _dictation.AudioLevel : 0;
        _smoothed += (target - _smoothed) * (target > _smoothed ? 0.3 : 0.08);
        _view.Time = DateTime.Now.TimeOfDay.TotalSeconds;
        _view.Level = _smoothed;
        _view.Elapsed = (DateTime.Now - _startedAt).TotalSeconds;
        _view.Transcribing = _dictation.State == DictationController.DictationState.Transcribing;
        _view.InvalidateVisual();
    }

    /// <summary>Centre sur l'écran où se trouve le pointeur (pixels physiques, DPI par écran).</summary>
    private void Center()
    {
        GetCursorPos(out var cursor);
        var monitor = MonitorFromPoint(cursor, 2 /*MONITOR_DEFAULTTONEAREST*/);
        var info = new MONITORINFO { cbSize = System.Runtime.InteropServices.Marshal.SizeOf<MONITORINFO>() };
        if (!GetMonitorInfo(monitor, ref info)) return;
        GetDpiForMonitor(monitor, 0, out var dpi, out _);
        var scale = dpi / 96.0;
        int w = (int)(WaveView.PillWidth * scale), h = (int)(WaveView.PillHeight * scale);
        var r = info.rcMonitor;
        var hwnd = new WindowInteropHelper(this).Handle;
        SetWindowPos(hwnd, HWND_TOPMOST, (r.Left + r.Right - w) / 2, (r.Top + r.Bottom - h) / 2, w, h, SWP_NOACTIVATE);
    }
}
