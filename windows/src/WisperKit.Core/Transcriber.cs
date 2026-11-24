using Whisper.net;
using Whisper.net.LibraryLoader;

namespace WisperKit.Core;

/// <summary>Enveloppe Whisper.net (whisper.cpp) : le modèle est chargé une fois puis gardé en mémoire.</summary>
public sealed class Transcriber : ITranscriber, IDisposable
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private GpuGuard? _guard;
    private WhisperFactory? _factory;

    /// <param name="useGpu">Faux : processeur seulement (Vulkan ignoré). Fixé au premier chargement
    /// pour tout le processus : un changement demande de relancer l'app.</param>
    /// <param name="guard">Témoin de plantage GPU (voir <see cref="GpuGuard"/>).</param>
    public Transcriber(bool useGpu = true, GpuGuard? guard = null)
    {
        UsesGpu = useGpu;
        _guard = useGpu ? guard : null;
        if (!useGpu) RuntimeOptions.RuntimeLibraryOrder = [RuntimeLibrary.Cpu, RuntimeLibrary.CpuNoAvx];
    }

    public bool UsesGpu { get; }

    /// <summary>Bibliothèque native réellement chargée (Vulkan, Cpu…), connue après le premier chargement.</summary>
    public string? Runtime => RuntimeOptions.LoadedLibrary?.ToString();

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
            _factory = await Task.Run(() =>
            {
                using var _ = _guard?.Enter();
                return WhisperFactory.FromPath(path);
            }, cancellationToken);
            // Repli de Whisper.net sur le processeur (pas de pilote Vulkan) : un plantage ne viendrait pas du GPU.
            if (RuntimeOptions.LoadedLibrary is { } loaded && loaded != RuntimeLibrary.Vulkan) _guard = null;
            LoadedModel = model;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<string> TranscribeAsync(float[] samples, string language, bool translate, CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var factory = _factory ?? throw new InvalidOperationException("The model is not loaded yet.");
            var plan = DecodingPlan.Make(language, translate, LoadedModel?.Id.Contains("turbo") ?? false);
            var builder = factory.CreateBuilder();
            builder = plan.Language is null ? builder.WithLanguageDetection() : builder.WithLanguage(plan.Language);
            if (plan.TranslateTask) builder = builder.WithTranslate();
            using var guarded = _guard?.Enter();
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
