using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace WisperKit.Core;

/// <summary>Installeur d'une architecture dans le manifeste de mise à jour.</summary>
public sealed record UpdateInstaller(
    [property: JsonPropertyName("url")] string Url,
    [property: JsonPropertyName("sha256")] string Sha256,
    [property: JsonPropertyName("size")] long Size);

/// <summary>update-windows.json, attaché à chaque release et signé hors de GitHub
/// (Scripts/sign-windows-update.sh) : version, notes et installeurs x64 / arm64.</summary>
public sealed record UpdateManifest(
    [property: JsonPropertyName("version")] string Version,
    [property: JsonPropertyName("notes")] string NotesUrl,
    [property: JsonPropertyName("installers")] Dictionary<string, UpdateInstaller> Installers);

/// <summary>Mise à jour plus récente que la version installée, pour cette architecture.</summary>
public sealed record AvailableUpdate(Version Version, string NotesUrl, UpdateInstaller Installer)
{
    public string DisplayVersion => Version.ToString(3);
}

public sealed class UpdateException(string message) : Exception(message);

/// <summary>Vérification de mise à jour (équivalent de Sparkle côté macOS). Une seule requête,
/// vers GitHub, sans aucune donnée envoyée ; le manifeste n'est accepté que si sa signature ECDSA
/// correspond à la clé publique embarquée, puis l'installeur téléchargé doit avoir le SHA-256 signé.</summary>
public sealed class UpdateChecker(HttpClient? http = null)
{
    public static readonly Uri ManifestUri = new("https://github.com/eldriic/WisperKit/releases/latest/download/update-windows.json");
    public static readonly Uri SignatureUri = new(ManifestUri + ".sig");
    /// <summary>Les installeurs ne peuvent venir que des releases du dépôt.</summary>
    public const string AllowedPrefix = "https://github.com/eldriic/WisperKit/releases/download/";

    /// <summary>Clé publique ECDSA P-256 ; la clé privée est dans le trousseau du Mac de release
    /// (copie dans Vaultwarden), jamais sur GitHub.</summary>
    public const string PublicKeyPem = """
        -----BEGIN PUBLIC KEY-----
        MFkwEwYHKoZIzj0CAQYIKoZIzj0DAQcDQgAE+kPYyPFKC8VKTWfockaXXCnX7qSm
        f1uTPFtL4IErm+1km2UUD/72v93qgDsyZ8tGolzOznJAw+bxEyrdVu/RCQ==
        -----END PUBLIC KEY-----
        """;

    private readonly HttpClient _http = http ?? CreateClient();

    private static HttpClient CreateClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromMinutes(10) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("WisperKit-Updater");
        return client;
    }

    /// <summary>Null si aucune version plus récente. Lève <see cref="UpdateException"/> si le manifeste
    /// est absent, mal signé ou invalide.</summary>
    public async Task<AvailableUpdate?> CheckAsync(Version current, string arch, CancellationToken ct = default)
    {
        var manifest = await _http.GetByteArrayAsync(ManifestUri, ct);
        var signature = await _http.GetStringAsync(SignatureUri, ct);
        return Evaluate(manifest, signature, PublicKeyPem, current, arch);
    }

    /// <summary>Vérifie la signature (base64 DER, ECDSA P-256 / SHA-256) puis compare les versions.</summary>
    public static AvailableUpdate? Evaluate(byte[] manifestJson, string signatureBase64, string publicKeyPem, Version current, string arch)
    {
        byte[] signature;
        try { signature = Convert.FromBase64String(signatureBase64.Trim()); }
        catch (FormatException) { throw new UpdateException("signature illisible"); }
        using (var key = ECDsa.Create())
        {
            key.ImportFromPem(publicKeyPem);
            if (!key.VerifyData(manifestJson, signature, HashAlgorithmName.SHA256, DSASignatureFormat.Rfc3279DerSequence))
                throw new UpdateException("signature invalide");
        }

        UpdateManifest? manifest;
        try { manifest = JsonSerializer.Deserialize<UpdateManifest>(manifestJson); }
        catch (JsonException) { throw new UpdateException("manifeste illisible"); }
        if (manifest is null || !System.Version.TryParse(manifest.Version, out var version))
            throw new UpdateException("version illisible");
        if (Normalize(version) <= Normalize(current)) return null;
        if (manifest.Installers is null || !manifest.Installers.TryGetValue(arch, out var installer))
            throw new UpdateException($"pas d'installeur {arch}");
        if (!installer.Url.StartsWith(AllowedPrefix, StringComparison.Ordinal) || installer.Size <= 0
            || installer.Sha256.Length != 64)
            throw new UpdateException("installeur invalide");
        var notes = manifest.NotesUrl.StartsWith("https://github.com/eldriic/WisperKit/", StringComparison.Ordinal)
            ? manifest.NotesUrl : "https://github.com/eldriic/WisperKit/releases/latest";
        return new AvailableUpdate(Normalize(version), notes, installer);
    }

    /// <summary>Télécharge l'installeur dans <paramref name="directory"/> et vérifie taille et SHA-256 ;
    /// le fichier est supprimé s'il ne correspond pas.</summary>
    public async Task<string> DownloadAsync(AvailableUpdate update, string directory, IProgress<double>? progress = null,
                                            CancellationToken ct = default)
    {
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, $"WisperKit-Setup-{update.DisplayVersion}.exe");
        try
        {
            using (var response = await _http.GetAsync(update.Installer.Url, HttpCompletionOption.ResponseHeadersRead, ct))
            {
                response.EnsureSuccessStatusCode();
                await using var source = await response.Content.ReadAsStreamAsync(ct);
                await using var file = File.Create(path);
                var buffer = new byte[81920];
                long done = 0;
                int read;
                while ((read = await source.ReadAsync(buffer, ct)) > 0)
                {
                    done += read;
                    if (done > update.Installer.Size) throw new UpdateException("installeur plus gros qu'annoncé");
                    await file.WriteAsync(buffer.AsMemory(0, read), ct);
                    progress?.Report((double)done / update.Installer.Size);
                }
            }
            if (new FileInfo(path).Length != update.Installer.Size) throw new UpdateException("taille de l'installeur incorrecte");
            await using (var check = File.OpenRead(path))
            {
                var hash = Convert.ToHexString(await SHA256.HashDataAsync(check, ct));
                if (!hash.Equals(update.Installer.Sha256, StringComparison.OrdinalIgnoreCase))
                    throw new UpdateException("empreinte SHA-256 de l'installeur incorrecte");
            }
            return path;
        }
        catch
        {
            try { File.Delete(path); } catch { }
            throw;
        }
    }

    /// <summary>« 0.3 » et « 0.3.0 » sont la même version ; la 4e composante est ignorée.</summary>
    internal static Version Normalize(Version v) => new(v.Major, v.Minor, Math.Max(v.Build, 0));
}
