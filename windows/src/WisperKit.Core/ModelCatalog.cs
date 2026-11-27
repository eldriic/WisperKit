namespace WisperKit.Core;

/// <summary>Modèles whisper.cpp (ggml) proposés dans les réglages.</summary>
public sealed record WhisperModel(string Id, string Title, string Detail, string Size, string FileName)
{
    private const string Base = "https://huggingface.co/ggerganov/whisper.cpp/resolve/main/";
    public Uri Url => new(Base + FileName);

    // Les textes « Detail » reprennent ceux de la version macOS (traductions partagées).
    public static readonly WhisperModel Turbo = new("large-v3-turbo-q5_0", "large-v3-turbo",
        "Best accuracy / speed trade-off", "550 MB", "ggml-large-v3-turbo-q5_0.bin");
    public static readonly WhisperModel Large = new("large-v3-q5_0", "large-v3",
        "Most accurate, slower", "1.1 GB", "ggml-large-v3-q5_0.bin");
    public static readonly WhisperModel Small = new("small", "small",
        "Fast, less accurate on jargon", "470 MB", "ggml-small.bin");
    public static readonly WhisperModel BaseModel = new("base", "base",
        "Very fast, for testing", "140 MB", "ggml-base.bin");

    public static IReadOnlyList<WhisperModel> All { get; } = [Turbo, Large, Small, BaseModel];
    public static WhisperModel Default => Turbo;

    public static WhisperModel Named(string? id) => All.FirstOrDefault(m => m.Id == id) ?? Default;
}

/// <summary>Stockage local des modèles : %LOCALAPPDATA%\WisperKit\Models.</summary>
public sealed class ModelStore(string directory, HttpClient? http = null)
{
    public static ModelStore Default { get; } = new(Path.Combine(AppPaths.LocalData, "Models"));

    private static readonly HttpClient SharedHttp = CreateClient();
    private readonly HttpClient? _http = http;
    // Résolu à l'usage, pas à la construction : Default est initialisé avant SharedHttp
    // (ordre du fichier), il recevait donc un client null et aucun téléchargement n'aboutissait.
    internal HttpClient Http => _http ?? SharedHttp;
    private readonly object _lock = new();
    /// <summary>Téléchargements en cours, partagés : la page Modèle et le chargement du modèle actif
    /// peuvent demander le même fichier en même temps sans se disputer le .part.</summary>
    private readonly Dictionary<string, Download> _inflight = [];

    public string Directory { get; } = directory;

    public string PathFor(WhisperModel model) => Path.Combine(Directory, model.FileName);

    /// <summary>Le fichier final n'existe qu'une fois le téléchargement terminé
    /// (écriture dans un .part puis renommage) : un téléchargement interrompu n'est jamais pris pour un modèle installé.</summary>
    public bool IsInstalled(WhisperModel model) => File.Exists(PathFor(model));

    public void Remove(WhisperModel model)
    {
        if (File.Exists(PathFor(model))) File.Delete(PathFor(model));
    }

    public long DiskUsage() =>
        System.IO.Directory.Exists(Directory)
            ? new DirectoryInfo(Directory).EnumerateFiles().Sum(f => f.Length)
            : 0;

    /// <summary>Télécharge le modèle si besoin (progression 0…1) et renvoie son chemin.
    /// Un appel pendant un téléchargement du même modèle s'y joint au lieu d'en lancer un second.
    /// L'annulation n'abandonne que l'attente de cet appelant.</summary>
    public async Task<string> EnsureAsync(WhisperModel model, IProgress<double>? progress = null,
                                          CancellationToken cancellationToken = default)
    {
        var target = PathFor(model);
        if (File.Exists(target)) return target;
        Download download;
        lock (_lock)
        {
            if (!_inflight.TryGetValue(model.Id, out download!))
            {
                download = new Download();
                _inflight[model.Id] = download;
                download.Task = Task.Run(async () =>
                {
                    try { return await DownloadAsync(model, download.Report); }
                    finally { lock (_lock) _inflight.Remove(model.Id); }
                });
            }
        }
        Action<double>? handler = progress is null ? null : progress.Report;
        if (handler is not null) download.Progress += handler;
        try
        {
            return await download.Task.WaitAsync(cancellationToken);
        }
        finally
        {
            if (handler is not null) download.Progress -= handler;
        }
    }

    private sealed class Download
    {
        public Task<string> Task = null!;
        public event Action<double>? Progress;
        public void Report(double fraction) => Progress?.Invoke(fraction);
    }

    private async Task<string> DownloadAsync(WhisperModel model, Action<double> progress)
    {
        var target = PathFor(model);
        if (File.Exists(target)) return target;
        System.IO.Directory.CreateDirectory(Directory);
        var part = target + ".part";
        var cancellationToken = CancellationToken.None;

        try
        {
            using var response = await Http.GetAsync(model.Url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            response.EnsureSuccessStatusCode();
            var total = response.Content.Headers.ContentLength;
            long done = 0;
            await using (var source = await response.Content.ReadAsStreamAsync(cancellationToken))
            await using (var file = File.Create(part))
            {
                var buffer = new byte[1 << 16];
                int read;
                var lastReport = 0.0;
                while ((read = await source.ReadAsync(buffer, cancellationToken)) > 0)
                {
                    await file.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
                    done += read;
                    if (total is > 0)
                    {
                        var fraction = (double)done / total.Value;
                        if (fraction - lastReport >= 0.005) { progress(fraction); lastReport = fraction; }
                    }
                }
            }
            // Connexion coupée en cours de route : le flux se termine sans erreur mais le fichier est tronqué.
            // Il ne doit jamais devenir un modèle « installé » (illisible, et impossible à retélécharger).
            if (done == 0 || (total is > 0 && done != total.Value))
                throw new IOException($"Incomplete download ({done} of {total?.ToString() ?? "?"} bytes).");
            File.Move(part, target, overwrite: true);
        }
        catch
        {
            try { File.Delete(part); } catch { }
            throw;
        }
        progress(1);
        return target;
    }

    private static HttpClient CreateClient()
    {
        var client = new HttpClient { Timeout = Timeout.InfiniteTimeSpan };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("WisperKit");
        return client;
    }
}

public static class AppPaths
{
    /// <summary>%LOCALAPPDATA%\WisperKit (modèles, journaux).</summary>
    public static string LocalData { get; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "WisperKit");

    /// <summary>%APPDATA%\WisperKit (réglages).</summary>
    public static string RoamingData { get; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "WisperKit");
}
