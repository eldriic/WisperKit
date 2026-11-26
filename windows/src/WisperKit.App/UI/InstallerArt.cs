using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace WisperKit.App.UI;

/// <summary>Images de l'installeur (Inno Setup) dessinées avec le code de l'app : fond noir de
/// <see cref="Theme"/>, halo violet et vagues de <see cref="WaveView"/>, logo. Générées par
/// WisperKit.exe --installer-art &lt;dossier&gt; puis versionnées dans windows/installer/art.</summary>
internal sealed class InstallerArt : FrameworkElement
{
    /// <summary>Panneau gauche des pages Bienvenue et Terminé (164 × 314 à 100 %).</summary>
    public const double WizardWidth = 164, WizardHeight = 314;
    /// <summary>Vignette en haut à droite des autres pages (55 × 55 à 100 %).</summary>
    public const double SmallSize = 55;

    private static readonly ImageSource Logo = LoadLogo();
    private readonly bool _small;

    private InstallerArt(bool small)
    {
        _small = small;
        Width = small ? SmallSize : WizardWidth;
        Height = small ? SmallSize : WizardHeight;
    }

    public static InstallerArt Wizard() => new(false);
    public static InstallerArt Small() => new(true);

    private static ImageSource LoadLogo()
    {
        var logo = new BitmapImage(new Uri("pack://application:,,,/Assets/icon-256.png"));
        logo.Freeze();
        return logo;
    }

    protected override void OnRender(DrawingContext dc)
    {
        if (_small)
        {
            dc.DrawImage(Logo, new Rect(4, 4, SmallSize - 8, SmallSize - 8));
            return;
        }

        var area = new Rect(0, 0, WizardWidth, WizardHeight);
        dc.DrawRectangle(Theme.Background, null, area);
        var halo = new RadialGradientBrush(Color.FromArgb(120, 120, 110, 255), Color.FromArgb(0, 120, 110, 255))
        {
            Center = new Point(0.5, 0.62), GradientOrigin = new Point(0.5, 0.62), RadiusX = 0.95, RadiusY = 0.42,
        };
        dc.DrawRectangle(halo, null, area);

        const double logoSize = 76;
        dc.DrawImage(Logo, new Rect((WizardWidth - logoSize) / 2, 70, logoSize, logoSize));

        var title = new FormattedText("WisperKit", System.Globalization.CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
                                      new Typeface(Theme.Font, FontStyles.Normal, FontWeights.SemiBold, FontStretches.Normal),
                                      20, Theme.Text, 1.0);
        dc.DrawText(title, new Point((WizardWidth - title.Width) / 2, 158));

        // Même rendu que la pastille d'enregistrement, voix bien présente.
        WaveView.DrawWaves(dc, new Rect(-10, 196, WizardWidth + 14, 80), 1.3, 0.9, 1);
    }
}
