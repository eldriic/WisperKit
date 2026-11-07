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
    public static float NormalizedLevel(ReadOnlySpan<float> chunk)
    {
        if (chunk.IsEmpty) return 0;
        double sum = 0;
        foreach (var s in chunk) sum += s * s;
        var rms = Math.Sqrt(sum / chunk.Length);
        var db = 20 * Math.Log10(Math.Max(rms, 1e-7));
        return (float)Math.Clamp((db + 50) / 40, 0, 1);
    }
}
