using System.Text.RegularExpressions;
using WisperKit.Core;

namespace WisperKit.Core.Tests;

public class ModelStoreTests
{
    [Fact]
    public void PartialDownloadIsNotInstalled()
    {
        var dir = Path.Combine(Path.GetTempPath(), "wk-ms-" + Guid.NewGuid());
        var store = new ModelStore(dir);
        Directory.CreateDirectory(dir);
        File.WriteAllText(store.PathFor(WhisperModel.BaseModel) + ".part", "x");
        Assert.False(store.IsInstalled(WhisperModel.BaseModel));
        File.WriteAllText(store.PathFor(WhisperModel.BaseModel), "x");
        Assert.True(store.IsInstalled(WhisperModel.BaseModel));
        store.Remove(WhisperModel.BaseModel);
        Assert.False(store.IsInstalled(WhisperModel.BaseModel));
        Directory.Delete(dir, true);
    }

    [Fact]
    public void UnknownModelFallsBackToDefault() => Assert.Equal(WhisperModel.Turbo, WhisperModel.Named("nope"));
}

public class SettingsTests
{
    [Fact]
    public void RoundTripsAndDefaultsToEnglish()
    {
        var path = Path.Combine(Path.GetTempPath(), "wk-set-" + Guid.NewGuid() + ".json");
        var fresh = AppSettings.Load(path);
        Assert.Equal("en", fresh.InterfaceLanguage);
        Assert.Equal("Ctrl+Alt+D", fresh.Shortcut);
        fresh.NeverUseClipboard = true;
        fresh.Mode = TriggerMode.Hold;
        fresh.Save(path);
        var again = AppSettings.Load(path);
        Assert.True(again.NeverUseClipboard);
        Assert.Equal(TriggerMode.Hold, again.Mode);
        File.Delete(path);
    }
}

[Collection("Localizer")]
public class LocalizerTests
{
    [Fact]
    public void EnglishIsTheDefaultAndFrenchSwitches()
    {
        Localizer.SetLanguage("en");
        Assert.Equal("Start dictation", Localizer.L("Start dictation"));
        Localizer.SetLanguage("fr");
        Assert.Equal("Démarrer la dictée", Localizer.L("Start dictation"));
        Assert.Equal("Téléchargement 42 %", Localizer.L("Downloading %d%%", 42));
        Assert.Equal("Prêt · base", Localizer.L("Ready · %@", "base"));
        Localizer.SetLanguage("xx");
        Assert.Equal("en", Localizer.Language);
    }

    /// <summary>Chaque texte L("…") de l'app Windows doit avoir sa traduction française.</summary>
    [Fact]
    public void EveryWindowsStringIsTranslated()
    {
        var root = FindRepoRoot();
        var call = new Regex("""\bL\("((?:[^"\\]|\\.)*)"[,)]""");
        var keys = Directory.EnumerateFiles(Path.Combine(root, "windows", "src"), "*.*", SearchOption.AllDirectories)
            .Where(f => f.EndsWith(".cs") || f.EndsWith(".xaml"))
            .SelectMany(f => call.Matches(File.ReadAllText(f)).Select(m => Regex.Unescape(m.Groups[1].Value)))
            .Distinct().ToList();
        var fr = Localizer.Table("fr");
        var missing = keys.Where(k => !fr.ContainsKey(k)).ToList();
        Assert.True(missing.Count == 0, "Traductions manquantes : " + string.Join(" | ", missing));
    }

    internal static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "windows", "src"))) dir = dir.Parent;
        return dir?.FullName ?? throw new DirectoryNotFoundException("racine du dépôt introuvable");
    }
}

/// <summary>Transcription réelle (télécharge le modèle base, ~140 Mo) :
/// WK_INTEGRATION=1 dotnet test --filter Integration</summary>
public class IntegrationTests
{
    [Fact]
    public async Task TranscribesFrenchSpeech()
    {
        if (Environment.GetEnvironmentVariable("WK_INTEGRATION") != "1" || !OperatingSystem.IsMacOS()) return;
        var dir = Path.Combine(Path.GetTempPath(), "wk-it-" + Guid.NewGuid());
        Directory.CreateDirectory(dir);
        var wav = Path.Combine(dir, "phrase.wav");
        var say = System.Diagnostics.Process.Start("/usr/bin/say",
            ["-v", "Thomas", "-o", wav, "--data-format=LEF32@16000", "Bonjour, je veux créer une application de budget."]);
        await say!.WaitForExitAsync();

        var data = await File.ReadAllBytesAsync(wav);
        var start = IndexOf(data, "data"u8.ToArray()) + 8;
        var samples = new float[(data.Length - start) / 4];
        Buffer.BlockCopy(data, start, samples, 0, samples.Length * 4);

        using var t = new Transcriber();
        await t.LoadAsync(WhisperModel.BaseModel, new ModelStore(Path.Combine(Path.GetTempPath(), "wk-models")));
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var text = await t.TranscribeAsync(samples, "fr", CancellationToken.None);
        Console.WriteLine($"Transcription ({sw.Elapsed.TotalSeconds:F2} s) : {text}");
        Assert.Contains("budget", TextCleaner.Clean(text).ToLowerInvariant());
    }

    private static int IndexOf(byte[] haystack, byte[] needle)
    {
        for (var i = 0; i <= haystack.Length - needle.Length; i++)
            if (haystack.AsSpan(i, needle.Length).SequenceEqual(needle)) return i;
        return -1;
    }
}
