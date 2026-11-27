using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using WisperKit.App.Platform;
using WisperKit.Core;
using static WisperKit.Core.Localizer;

namespace WisperKit.App.UI;

/// <summary>« WisperKit x.y.z est disponible » : notes, puis téléchargement vérifié et installation
/// (équivalent de la fenêtre Sparkle sur macOS).</summary>
internal sealed class UpdateWindow : Window
{
    private readonly AppController _app;
    private readonly AvailableUpdate _update;
    private readonly ProgressBar _progress = new() { Height = 4, Minimum = 0, Maximum = 1, Margin = new Thickness(0, 0, 0, 14) };
    private readonly TextBlock _error = Theme.Label("", 11.5, Theme.Danger);
    private readonly StackPanel _buttons = new() { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
    private CancellationTokenSource? _download;

    public UpdateWindow(AppController app, AvailableUpdate update)
    {
        _app = app;
        _update = update;
        Title = "WisperKit · " + L("Update available");
        Width = 460;
        SizeToContent = SizeToContent.Height;
        ResizeMode = ResizeMode.NoResize;
        Background = Theme.Popover;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        Icon = new System.Windows.Media.Imaging.BitmapImage(new Uri("pack://application:,,,/Assets/icon-256.png"));
        SourceInitialized += (_, _) => NativeMethods.UseDarkTitleBar(new WindowInteropHelper(this).Handle);
        Content = Body();
        Closed += (_, _) => _download?.Cancel();
        ShowIdle();
    }

    /// <summary>Contenu seul (captures d'écran).</summary>
    public FrameworkElement Body()
    {
        if (base.Content is FrameworkElement existing) return existing;
        var stack = new StackPanel { Margin = new Thickness(22, 20, 22, 18) };
        var header = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 12) };
        header.Children.Add(new Image
        {
            Source = new System.Windows.Media.Imaging.BitmapImage(new Uri("pack://application:,,,/Assets/icon-256.png")),
            Width = 44, Height = 44, Margin = new Thickness(0, 0, 14, 0), VerticalAlignment = VerticalAlignment.Top,
        });
        var texts = new StackPanel { MaxWidth = 350 };
        texts.Children.Add(Theme.Label(L("WisperKit %@ is available.", _update.DisplayVersion), 15, weight: FontWeights.SemiBold));
        var detail = Theme.Label(L("You have %@. WisperKit will close, install the update, and reopen. Your settings and models are kept.",
                                   AppController.CurrentVersion.ToString(3)), 12, Theme.Secondary);
        detail.Margin = new Thickness(0, 4, 0, 6);
        texts.Children.Add(detail);
        var notes = Theme.Label(L("What's new"), 12, Theme.Blue);
        notes.Cursor = System.Windows.Input.Cursors.Hand;
        notes.MouseLeftButtonUp += (_, _) => Process.Start(new ProcessStartInfo(_update.NotesUrl) { UseShellExecute = true });
        texts.Children.Add(notes);
        header.Children.Add(texts);
        stack.Children.Add(header);
        stack.Children.Add(_progress);
        _error.Margin = new Thickness(0, 0, 0, 10);
        stack.Children.Add(_error);
        stack.Children.Add(_buttons);
        return stack;
    }

    private void ShowIdle()
    {
        _progress.Visibility = Visibility.Collapsed;
        _error.Visibility = string.IsNullOrEmpty(_error.Text) ? Visibility.Collapsed : Visibility.Visible;
        _buttons.Children.Clear();
        var later = Theme.PillButton(L("Later"), Close);
        later.Margin = new Thickness(0, 0, 8, 0);
        _buttons.Children.Add(later);
        _buttons.Children.Add(Theme.PillButton(L("Install and restart"), Install, fill: Theme.Blue));
    }

    private async void Install()
    {
        _download = new CancellationTokenSource();
        _error.Text = "";
        _error.Visibility = Visibility.Collapsed;
        _progress.Value = 0;
        _progress.Visibility = Visibility.Visible;
        _buttons.Children.Clear();
        _buttons.Children.Add(Theme.PillButton(L("Cancel"), () => _download.Cancel()));
        try
        {
            await _app.InstallUpdateAsync(_update, new Progress<double>(f => _progress.Value = f), _download.Token);
        }
        catch (OperationCanceledException)
        {
            ShowIdle();
        }
        catch (Exception e)
        {
            DiagnosticLog.Write($"échec de la mise à jour : {e.Message}");
            _error.Text = L("The update couldn't be installed. Try again, or download it from the website.");
            ShowIdle();
        }
    }
}
