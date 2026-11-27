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
    public void DefaultStoreHasAnHttpClient()
    {
        Assert.NotNull(ModelStore.Default.Http);
        Assert.NotNull(new ModelStore(Path.GetTempPath()).Http);
    }

    [Fact]
    public void UnknownModelFallsBackToDefault() => Assert.Equal(WhisperModel.Turbo, WhisperModel.Named("nope"));

    [Fact]
    public async Task ConcurrentDownloadsOfTheSameModelShareOneRequest()
    {
        var dir = Path.Combine(Path.GetTempPath(), "wk-ms-" + Guid.NewGuid());
        var handler = new SlowHandler();
        var store = new ModelStore(dir, new HttpClient(handler));
        var seen = new List<double>();
        var first = store.EnsureAsync(WhisperModel.BaseModel);
        var second = store.EnsureAsync(WhisperModel.BaseModel, new SyncProgress(seen.Add));
        handler.Release.SetResult();
        var paths = await Task.WhenAll(first, second);
        Assert.Equal(1, handler.Requests);
        Assert.Equal(paths[0], paths[1]);
        Assert.True(store.IsInstalled(WhisperModel.BaseModel));
        Assert.Equal(1, seen.Last());
        // Une fois installé, plus aucune requête.
        await store.EnsureAsync(WhisperModel.BaseModel);
        Assert.Equal(1, handler.Requests);
        Directory.Delete(dir, true);
    }

    [Fact]
    public async Task TruncatedDownloadIsNeitherInstalledNorLeftBehind()
    {
        var dir = Path.Combine(Path.GetTempPath(), "wk-ms-" + Guid.NewGuid());
        var store = new ModelStore(dir, new HttpClient(new TruncatingHandler()));
        await Assert.ThrowsAsync<IOException>(() => store.EnsureAsync(WhisperModel.BaseModel));
        Assert.False(store.IsInstalled(WhisperModel.BaseModel));
        Assert.False(File.Exists(store.PathFor(WhisperModel.BaseModel) + ".part"));
        Directory.Delete(dir, true);
    }

    [Fact]
    public async Task FailedDownloadCanBeRetried()
    {
        var dir = Path.Combine(Path.GetTempPath(), "wk-ms-" + Guid.NewGuid());
        var handler = new FlakyHandler();
        var store = new ModelStore(dir, new HttpClient(handler));
        await Assert.ThrowsAsync<HttpRequestException>(() => store.EnsureAsync(WhisperModel.BaseModel));
        Assert.False(File.Exists(store.PathFor(WhisperModel.BaseModel) + ".part"));
        await store.EnsureAsync(WhisperModel.BaseModel); // le téléchargement en échec n'est plus « en cours »
        Assert.True(store.IsInstalled(WhisperModel.BaseModel));
        Assert.Equal(2, handler.Requests);
        Directory.Delete(dir, true);
    }

    /// <summary>Annonce 100 000 octets mais la connexion se ferme après 1 000.</summary>
    sealed class TruncatingHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var content = new StreamContent(new MemoryStream(new byte[1_000]));
            content.Headers.ContentLength = 100_000;
            return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK) { Content = content });
        }
    }

    sealed class FlakyHandler : HttpMessageHandler
    {
        public int Requests;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) =>
            Task.FromResult(Interlocked.Increment(ref Requests) == 1
                ? new HttpResponseMessage(System.Net.HttpStatusCode.ServiceUnavailable)
                : new HttpResponseMessage(System.Net.HttpStatusCode.OK) { Content = new ByteArrayContent(new byte[10_000]) });
    }

    sealed class SlowHandler : HttpMessageHandler
    {
        public readonly TaskCompletionSource Release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public int Requests;
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Interlocked.Increment(ref Requests);
            await Release.Task;
            return new HttpResponseMessage(System.Net.HttpStatusCode.OK) { Content = new ByteArrayContent(new byte[100_000]) };
        }
    }

    sealed class SyncProgress(Action<double> report) : IProgress<double>
    {
        public void Report(double value) { lock (this) report(value); }
    }
}

public class ShortcutRulesTests
{
    [Theory]
    [InlineData(true, true, false, 0x44, true)]    // Ctrl+Alt+D
    [InlineData(false, false, true, 0x48, true)]   // Win+H
    [InlineData(false, false, false, 0x7B, true)]  // F12 seule
    [InlineData(false, false, false, 0x44, false)] // D seul : bloquerait la lettre
    [InlineData(true, false, false, 0x1B, false)]  // Ctrl+Échap
    [InlineData(false, false, false, 0, false)]
    public void RequiresAModifierOrAFunctionKey(bool ctrl, bool alt, bool win, int vk, bool allowed) =>
        Assert.Equal(allowed, ShortcutRules.IsAllowed(ctrl, alt, win, vk));

    [Fact]
    public void GuardsMenusOnlyForWinOrAlt()
    {
        Assert.True(ShortcutRules.NeedsMenuGuard(alt: false, win: true));
        Assert.True(ShortcutRules.NeedsMenuGuard(alt: true, win: false));
        Assert.False(ShortcutRules.NeedsMenuGuard(alt: false, win: false));
    }
}

public class GpuGuardTests
{
    [Fact]
    public void DetectsAnUnfinishedGpuCall()
    {
        var guard = new GpuGuard(Path.Combine(Path.GetTempPath(), "wk-gpu-" + Guid.NewGuid(), "gpu-in-use"));
        using (guard.Enter()) { }
        Assert.False(guard.ConsumeCrash()); // appel terminé normalement

        guard.Enter(); // le processus « meurt » sans sortir
        Assert.True(guard.ConsumeCrash());
        Assert.False(guard.ConsumeCrash()); // signalé une seule fois

        guard.Enter(); // calcul en cours quand l'utilisateur quitte
        guard.Clear();
        Assert.False(guard.ConsumeCrash());
    }
}

public class SettingsTests
{
    [Fact]
    public void RoundTripsAndDefaultsToEnglish()
    {
        var path = Path.Combine(Path.GetTempPath(), "wk-set-" + Guid.NewGuid() + ".json");
        var fresh = AppSettings.Load(path);
        Assert.Equal("en", fresh.InterfaceLanguage);
        Assert.Equal("auto", fresh.DictationLanguage);
        Assert.False(fresh.TranslateToEnglish);
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

