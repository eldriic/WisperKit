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
public sealed class ModelStore(string directory)
{
    public static ModelStore Default { get; } = new(Path.Combine(AppPaths.LocalData, "Models"));

    private static readonly HttpClient Http = CreateClient();

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

    /// <summary>Télécharge le modèle si besoin (progression 0…1) et renvoie son chemin.</summary>
    public async Task<string> EnsureAsync(WhisperModel model, IProgress<double>? progress = null,
                                          CancellationToken cancellationToken = default)
    {
        var target = PathFor(model);
        if (File.Exists(target)) return target;
        System.IO.Directory.CreateDirectory(Directory);
        var part = target + ".part";

        using var response = await Http.GetAsync(model.Url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();
        var total = response.Content.Headers.ContentLength;
        await using (var source = await response.Content.ReadAsStreamAsync(cancellationToken))
        await using (var file = File.Create(part))
        {
            var buffer = new byte[1 << 16];
            long done = 0;
            int read;
            var lastReport = 0.0;
            while ((read = await source.ReadAsync(buffer, cancellationToken)) > 0)
            {
                await file.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
                done += read;
                if (total is > 0)
                {
                    var fraction = (double)done / total.Value;
                    if (fraction - lastReport >= 0.005) { progress?.Report(fraction); lastReport = fraction; }
                }
            }
        }
        File.Move(part, target, overwrite: true);
        progress?.Report(1);
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
