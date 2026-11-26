namespace WisperKit.Core;

/// <summary>Source audio (micro réel ou faux pour les tests).</summary>
public interface IAudioRecorder
{
    void Start();
    /// <summary>Arrête l'enregistrement et renvoie les échantillons 16 kHz mono.</summary>
    float[] Stop();
    /// <summary>Niveau sonore récent normalisé 0…1 (indicateur à l'écran).</summary>
    float Level { get; }
}

/// <summary>Moteur de transcription.</summary>
public interface ITranscriber
{
    /// <param name="language">Code ISO ou "auto".</param>
    /// <param name="translate">Produire du texte anglais quelle que soit la langue parlée.</param>
    Task<string> TranscribeAsync(float[] samples, string language, bool translate, CancellationToken cancellationToken);
}

/// <summary>Comment décoder : transcrire dans la langue parlée, ou traduire en anglais (même règle que macOS).</summary>
public readonly record struct DecodingPlan(bool TranslateTask, string? Language)
{
    /// <summary><c>large-v3-turbo</c> n'a pas été entraîné pour la tâche « translate » : on lui impose
    /// la sortie anglaise, ce qui le fait traduire en écrivant directement en anglais.</summary>
    public static DecodingPlan Make(string language, bool translate, bool modelIsTurbo)
    {
        var source = language == "auto" ? null : language;
        if (!translate) return new(false, source);
        return modelIsTurbo ? new(false, "en") : new(true, source);
    }
}

/// <summary>Façon d'écrire le texte dans l'application active.</summary>
public enum InsertionMethod
{
    /// <summary>Presse-papiers + Ctrl+V, presse-papiers restauré ensuite.</summary>
    Paste,
    /// <summary>Frappe Unicode simulée : le presse-papiers n'est jamais touché.</summary>
    Type,
}

/// <summary>Ce qu'il est advenu du texte transcrit.</summary>
public enum DeliveryResult
{
    Pasted,
    Typed,
    /// <summary>Laissé dans le presse-papiers seulement.</summary>
    ClipboardOnly,
    /// <summary>Ni inséré ni copié.</summary>
    NotInserted,
}

/// <summary>Destination du texte transcrit.</summary>
public interface ITextOutput
{
    DeliveryResult Deliver(string text, bool autoInsert, InsertionMethod method, bool restoreClipboard);
}

public static class AudioFormat
{
    public const int SampleRate = 16_000;

    /// <summary>RMS du bloc en dB ramené à 0…1 entre -50 dB (silence) et -10 dB (voix forte),
    /// identique à la version macOS.</summary>
    public static float NormalizedLevel(ReadOnlySpan<float> chunk) =>
        chunk.IsEmpty ? 0 : (float)Math.Clamp((Decibels(chunk) + 50) / 40, 0, 1);

    /// <summary>RMS du bloc en dBFS (-140 pour un silence numérique).</summary>
    public static double Decibels(ReadOnlySpan<float> chunk)
    {
        if (chunk.IsEmpty) return -140;
        double sum = 0;
        foreach (var s in chunk) sum += s * s;
        return 20 * Math.Log10(Math.Max(Math.Sqrt(sum / chunk.Length), 1e-7));
    }
}

/// <summary>Niveau 0…1 de la vague, adapté au micro. Sous Windows, WaveIn livre le signal brut, sans
/// contrôle de gain : une voix normale y tourne souvent vers -40 dBFS, 15 à 25 dB sous macOS, et l'échelle
/// fixe de <see cref="AudioFormat.NormalizedLevel"/> laissait la vague presque plate. Ici le 0 suit le bruit
/// de fond et le 1 la voix la plus forte récente ; l'instance est gardée d'une dictée à l'autre.</summary>
public sealed class LevelMeter
{
    /// <summary>Écart minimal entre bruit de fond et pleine échelle : le souffle seul ne remplit jamais la vague.</summary>
    public const double MinSpan = 18;
    private const double Gate = 4;            // dB au-dessus du bruit de fond avant que la vague bouge
    private const double FloorRise = 0.025;   // dB par bloc de 50 ms (0,5 dB/s) : la voix ne relève presque pas le 0
    private const double PeakFall = 0.15;     // dB par bloc (3 dB/s) : le 1 redescend après un éclat de voix

    private double _floor = -50;
    private double _peak = double.NegativeInfinity; // suit d'abord le bruit de fond + MinSpan

    public double FloorDb => _floor;
    /// <summary>Crête depuis <see cref="BeginSession"/>, pour le journal de diagnostic.</summary>
    public double SessionPeakDb { get; private set; } = -140;

    public void BeginSession() => SessionPeakDb = -140;

    public float Next(ReadOnlySpan<float> chunk)
    {
        if (chunk.IsEmpty) return 0;
        var db = AudioFormat.Decibels(chunk);
        SessionPeakDb = Math.Max(SessionPeakDb, db);
        _floor = db < _floor ? db : _floor + FloorRise;
        _peak = Math.Max(db, Math.Max(_peak - PeakFall, _floor + MinSpan));
        var level = Math.Clamp((db - _floor - Gate) / (_peak - _floor - Gate), 0, 1);
        return (float)Math.Pow(level, 0.8);
    }
}
