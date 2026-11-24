using WisperKit.Core;

namespace WisperKit.Core.Tests;

/// <summary>Transcription réelle (télécharge le modèle base, ~140 Mo) :
/// WK_INTEGRATION=1 dotnet test --filter Integration
/// La voix vient de <c>say</c> (macOS) ou de la synthèse vocale de Windows (voix française requise).</summary>
public class IntegrationTests
{
    [Fact]
    public async Task TranscribesFrenchSpeech()
    {
        if (Environment.GetEnvironmentVariable("WK_INTEGRATION") != "1") return;
        var samples = await Speech.SynthesizeFrenchAsync("Bonjour, je veux créer une application de budget.");
        if (samples is null) return;

        using var t = new Transcriber(useGpu: false);
        await t.LoadAsync(WhisperModel.BaseModel, new ModelStore(Path.Combine(Path.GetTempPath(), "wk-models")));
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var text = await t.TranscribeAsync(samples, "fr", false, CancellationToken.None);
        Console.WriteLine($"Transcription ({sw.Elapsed.TotalSeconds:F2} s, {t.Runtime}) : {text}");
        Assert.Contains("budget", TextCleaner.Clean(text).ToLowerInvariant());
    }

    [Fact]
    public async Task TranslatesFrenchSpeechToEnglish()
    {
        if (Environment.GetEnvironmentVariable("WK_INTEGRATION") != "1") return;
        var samples = await Speech.SynthesizeFrenchAsync("Bonjour, je voudrais réserver une table pour ce soir.");
        if (samples is null) return;

        using var t = new Transcriber(useGpu: false);
        await t.LoadAsync(WhisperModel.BaseModel, new ModelStore(Path.Combine(Path.GetTempPath(), "wk-models")));
        var text = await t.TranscribeAsync(samples, "fr", true, CancellationToken.None);
        Console.WriteLine($"Traduction : {text}");
        Assert.Contains("table", text.ToLowerInvariant());
    }
}

/// <summary>Voix de synthèse en échantillons 16 kHz mono (null si aucune voix française n'est disponible).</summary>
internal static class Speech
{
    public static async Task<float[]?> SynthesizeFrenchAsync(string sentence)
    {
        var wav = Path.Combine(Path.GetTempPath(), "wk-tts-" + Guid.NewGuid() + ".wav");
        try
        {
            if (OperatingSystem.IsMacOS())
            {
                var say = System.Diagnostics.Process.Start("/usr/bin/say",
                    ["-v", "Thomas", "-o", wav, "--data-format=LEI16@16000", sentence]);
                await say!.WaitForExitAsync();
                if (say.ExitCode != 0) return null;
            }
            else if (OperatingSystem.IsWindows())
            {
                // System.Speech (SAPI) est fourni avec Windows PowerShell 5.1.
                var script = $$"""
                    Add-Type -AssemblyName System.Speech
                    $s = New-Object System.Speech.Synthesis.SpeechSynthesizer
                    $v = $s.GetInstalledVoices() | Where-Object { $_.VoiceInfo.Culture.Name -like 'fr*' } | Select-Object -First 1
                    if (-not $v) { exit 3 }
                    $s.SelectVoice($v.VoiceInfo.Name)
                    $f = New-Object System.Speech.AudioFormat.SpeechAudioFormatInfo(16000, [System.Speech.AudioFormat.AudioBitsPerSample]::Sixteen, [System.Speech.AudioFormat.AudioChannel]::Mono)
                    $s.SetOutputToWaveFile('{{wav}}', $f)
                    $s.Speak('{{sentence.Replace("'", "''")}}')
                    $s.Dispose()
                    """;
                var encoded = Convert.ToBase64String(System.Text.Encoding.Unicode.GetBytes(script));
                var ps = System.Diagnostics.Process.Start("powershell.exe",
                    ["-NoProfile", "-NonInteractive", "-EncodedCommand", encoded]);
                await ps!.WaitForExitAsync();
                if (ps.ExitCode != 0) return null;
            }
            else return null;
            return ReadPcm16(await File.ReadAllBytesAsync(wav));
        }
        finally
        {
            try { File.Delete(wav); } catch { }
        }
    }

    /// <summary>Bloc « data » d'un WAV PCM 16 bits, en flottants -1…1.</summary>
    private static float[] ReadPcm16(byte[] data)
    {
        var pos = 12; // après « RIFF <taille> WAVE »
        while (pos + 8 <= data.Length)
        {
            var id = System.Text.Encoding.ASCII.GetString(data, pos, 4);
            var size = BitConverter.ToInt32(data, pos + 4);
            if (id == "data")
            {
                var count = Math.Min(size, data.Length - pos - 8) / 2;
                var samples = new float[count];
                for (var i = 0; i < count; i++) samples[i] = BitConverter.ToInt16(data, pos + 8 + i * 2) / 32768f;
                return samples;
            }
            pos += 8 + size + (size & 1);
        }
        throw new InvalidDataException("WAV sans bloc data");
    }
}
