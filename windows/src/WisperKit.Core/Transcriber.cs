using Whisper.net;

namespace WisperKit.Core;

/// <summary>Enveloppe Whisper.net (whisper.cpp) : le modèle est chargé une fois puis gardé en mémoire.</summary>
public sealed class Transcriber : ITranscriber, IDisposable
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private WhisperFactory? _factory;

    public WhisperModel? LoadedModel { get; private set; }

    /// <summary>Télécharge (si besoin) puis charge le modèle.</summary>
    public async Task LoadAsync(WhisperModel model, ModelStore store, IProgress<double>? progress = null,
                                CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (LoadedModel == model && _factory is not null) return;
            _factory?.Dispose();
            _factory = null;
            LoadedModel = null;
            var path = await store.EnsureAsync(model, progress, cancellationToken);
            _factory = await Task.Run(() => WhisperFactory.FromPath(path), cancellationToken);
            LoadedModel = model;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<string> TranscribeAsync(float[] samples, string language, CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var factory = _factory ?? throw new InvalidOperationException("The model is not loaded yet.");
            var builder = factory.CreateBuilder();
            builder = language == "auto" ? builder.WithLanguageDetection() : builder.WithLanguage(language);
            await using var processor = builder.Build();
            var parts = new List<string>();
            await foreach (var segment in processor.ProcessAsync(samples, cancellationToken))
                parts.Add(segment.Text);
            return string.Join(" ", parts);
        }
        finally
        {
            _gate.Release();
        }
    }

    public void Dispose()
    {
        _factory?.Dispose();
        _gate.Dispose();
    }
}
