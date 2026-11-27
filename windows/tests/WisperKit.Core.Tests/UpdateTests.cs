using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using WisperKit.Core;

namespace WisperKit.Core.Tests;

public class UpdateTests
{
    private const string Url = UpdateChecker.AllowedPrefix + "v0.3.0/WisperKit-Setup-x64.exe";
    private static readonly byte[] Installer = Encoding.UTF8.GetBytes("faux installeur");
    private static readonly string InstallerHash = Convert.ToHexString(SHA256.HashData(Installer));

    private readonly ECDsa _key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
    private string PublicPem => _key.ExportSubjectPublicKeyInfoPem();

    private static byte[] Manifest(string version = "0.3.0", string url = Url, string? hash = null, long? size = null) =>
        JsonSerializer.SerializeToUtf8Bytes(new UpdateManifest(version, "https://github.com/eldriic/WisperKit/releases/tag/v" + version,
            new() { ["x64"] = new(url, hash ?? InstallerHash, size ?? Installer.Length) }));

    private string Sign(byte[] data) =>
        Convert.ToBase64String(_key.SignData(data, HashAlgorithmName.SHA256, DSASignatureFormat.Rfc3279DerSequence));

    [Fact]
    public void NewerSignedVersionIsOffered()
    {
        var manifest = Manifest();
        var update = UpdateChecker.Evaluate(manifest, Sign(manifest), PublicPem, new Version(0, 2, 1), "x64");
        Assert.NotNull(update);
        Assert.Equal("0.3.0", update.DisplayVersion);
        Assert.Equal(Url, update.Installer.Url);
    }

    [Theory]
    [InlineData("0.2.1")]
    [InlineData("0.2.0")]
    [InlineData("0.2.1.0")]
    public void SameOrOlderVersionIsIgnored(string version)
    {
        var manifest = Manifest(version);
        Assert.Null(UpdateChecker.Evaluate(manifest, Sign(manifest), PublicPem, new Version(0, 2, 1, 0), "x64"));
    }

    [Fact]
    public void VersionsCompareNumerically()
    {
        var manifest = Manifest("0.10.0");
        Assert.NotNull(UpdateChecker.Evaluate(manifest, Sign(manifest), PublicPem, new Version(0, 9, 0), "x64"));
    }

    [Fact]
    public void TamperedManifestIsRejected()
    {
        var signature = Sign(Manifest());
        var tampered = Manifest(hash: new string('0', 64));
        Assert.Throws<UpdateException>(() => UpdateChecker.Evaluate(tampered, signature, PublicPem, new Version(0, 2, 1), "x64"));
    }

    [Fact]
    public void SignatureFromAnotherKeyIsRejected()
    {
        var manifest = Manifest();
        using var other = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var signature = Convert.ToBase64String(other.SignData(manifest, HashAlgorithmName.SHA256, DSASignatureFormat.Rfc3279DerSequence));
        Assert.Throws<UpdateException>(() => UpdateChecker.Evaluate(manifest, signature, PublicPem, new Version(0, 2, 1), "x64"));
        Assert.Throws<UpdateException>(() => UpdateChecker.Evaluate(manifest, "pas du base64", PublicPem, new Version(0, 2, 1), "x64"));
    }

    [Fact]
    public void InstallerOutsideTheReleasesIsRejected()
    {
        var manifest = Manifest(url: "https://example.com/WisperKit-Setup-x64.exe");
        Assert.Throws<UpdateException>(() => UpdateChecker.Evaluate(manifest, Sign(manifest), PublicPem, new Version(0, 2, 1), "x64"));
    }

    [Fact]
    public void MissingArchitectureIsRejected()
    {
        var manifest = Manifest();
        Assert.Throws<UpdateException>(() => UpdateChecker.Evaluate(manifest, Sign(manifest), PublicPem, new Version(0, 2, 1), "arm64"));
    }

    [Fact]
    public void EmbeddedPublicKeyIsValid()
    {
        using var key = ECDsa.Create();
        key.ImportFromPem(UpdateChecker.PublicKeyPem);
        Assert.Equal(256, key.KeySize);
    }

    [Fact]
    public async Task DownloadKeepsAnInstallerWithTheSignedHash()
    {
        var dir = Path.Combine(Path.GetTempPath(), "wk-up-" + Guid.NewGuid());
        var checker = new UpdateChecker(new HttpClient(new BytesHandler(Installer)));
        var update = new AvailableUpdate(new Version(0, 3, 0), "", new UpdateInstaller(Url, InstallerHash, Installer.Length));
        var seen = new List<double>();
        var path = await checker.DownloadAsync(update, dir, new SyncProgress(seen.Add));
        Assert.Equal(Installer, File.ReadAllBytes(path));
        Assert.Equal(1, seen.Last());
        Directory.Delete(dir, true);
    }

    [Fact]
    public async Task DownloadWithAnotherHashIsDeleted()
    {
        var dir = Path.Combine(Path.GetTempPath(), "wk-up-" + Guid.NewGuid());
        var checker = new UpdateChecker(new HttpClient(new BytesHandler(Encoding.UTF8.GetBytes("autre installeur"))));
        var update = new AvailableUpdate(new Version(0, 3, 0), "", new UpdateInstaller(Url, InstallerHash, 16));
        await Assert.ThrowsAsync<UpdateException>(() => checker.DownloadAsync(update, dir));
        Assert.Empty(Directory.GetFiles(dir));
        Directory.Delete(dir, true);
    }

    private sealed class SyncProgress(Action<double> report) : IProgress<double>
    {
        public void Report(double value) => report(value);
    }

    private sealed class BytesHandler(byte[] body) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(body) });
    }
}
