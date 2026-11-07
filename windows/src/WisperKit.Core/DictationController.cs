namespace WisperKit.Core;

/// <summary>Machine à états de la dictée : repos → écoute → transcription → repos.
/// Portage de DictationController.swift ; à utiliser depuis le thread de l'interface.</summary>
public sealed class DictationController
{
    public enum DictationState { Idle, Recording, Transcribing, Failed }

    public abstract record Event
    {
        public sealed record Started : Event;
        public sealed record Stopped : Event;
        public sealed record Delivered(DeliveryResult Result) : Event;
        public sealed record Cancelled : Event;
        public sealed record Failed(string Message) : Event;
    }

    private readonly IAudioRecorder _recorder;
    private readonly ITranscriber _transcriber;
    private readonly ITextOutput _output;
    private CancellationTokenSource? _cts;
    private Task? _task;

    public DictationController(IAudioRecorder recorder, ITranscriber transcriber, ITextOutput output)
    {
        _recorder = recorder;
        _transcriber = transcriber;
        _output = output;
    }

    public DictationState State { get; private set; } = DictationState.Idle;
    public string? ErrorMessage { get; private set; }
    public string? LastText { get; private set; }

    // Réglages appliqués à chaque dictée.
    public string Language { get; set; } = "auto";
    /// <summary>Produit du texte anglais quelle que soit la langue parlée.</summary>
    public bool TranslateToEnglish { get; set; }
    public bool AutoInsert { get; set; } = true;
    public InsertionMethod InsertionMethod { get; set; } = InsertionMethod.Paste;
    public bool RestoreClipboard { get; set; } = true;
    /// <summary>Dictées plus courtes ignorées (appui accidentel).</summary>
    public double MinimumDuration { get; set; } = 0.3;

    /// <summary>Notifié à chaque transition (sons, indicateur, icône…).</summary>
    public event Action<Event>? EventRaised;
    /// <summary>Notifié à chaque changement d'état (rafraîchissement de l'interface).</summary>
    public event Action? StateChanged;

    public bool IsRecording => State == DictationState.Recording;
    public float AudioLevel => State == DictationState.Recording ? _recorder.Level : 0;

    /// <summary>Mode bascule : démarre, ou arrête et transcrit.</summary>
    public void Toggle()
    {
        if (State == DictationState.Recording) Stop(); else Start();
    }

    public void Start()
    {
        if (State is DictationState.Recording or DictationState.Transcribing) return;
        try
        {
            _recorder.Start();
            SetState(DictationState.Recording);
            EventRaised?.Invoke(new Event.Started());
        }
        catch (Exception e)
        {
            Fail(e.Message);
        }
    }

    /// <summary>Arrête l'écoute et lance la transcription.</summary>
    public void Stop()
    {
        if (State != DictationState.Recording) return;
        var samples = _recorder.Stop();
        EventRaised?.Invoke(new Event.Stopped());

        if (samples.Length / (double)AudioFormat.SampleRate < MinimumDuration)
        {
            SetState(DictationState.Idle);
            EventRaised?.Invoke(new Event.Cancelled());
            return;
        }
        SetState(DictationState.Transcribing);
        _cts = new CancellationTokenSource();
        _task = RunAsync(samples, Language, TranslateToEnglish, _cts.Token);
    }

    private async Task RunAsync(float[] samples, string language, bool translate, CancellationToken token)
    {
        // Toujours asynchrone : Stop() rend la main en état « transcription » (comme la version macOS).
        await Task.Yield();
        try
        {
            var raw = await _transcriber.TranscribeAsync(samples, language, translate, token);
            if (token.IsCancellationRequested) return;
            Finish(raw);
        }
        catch (OperationCanceledException)
        {
            // Annulé (Échap) : déjà revenu au repos.
        }
        catch (Exception e)
        {
            if (!token.IsCancellationRequested) Fail(e.Message);
        }
    }

    /// <summary>Abandonne l'écoute ou la transcription sans rien insérer.</summary>
    public void Cancel()
    {
        switch (State)
        {
            case DictationState.Recording:
                _recorder.Stop();
                break;
            case DictationState.Transcribing:
                _cts?.Cancel();
                break;
            default:
                return;
        }
        _task = null;
        SetState(DictationState.Idle);
        EventRaised?.Invoke(new Event.Cancelled());
    }

    public void DismissError()
    {
        if (State == DictationState.Failed) SetState(DictationState.Idle);
    }

    /// <summary>Attend la fin de la transcription en cours (tests).</summary>
    public Task WaitForCompletionAsync() => _task ?? Task.CompletedTask;

    private void Finish(string raw)
    {
        if (State != DictationState.Transcribing) return;
        var text = TextCleaner.Clean(raw);
        SetState(DictationState.Idle);
        _task = null;
        if (text.Length == 0)
        {
            EventRaised?.Invoke(new Event.Cancelled());
            return;
        }
        LastText = text;
        var result = _output.Deliver(text, AutoInsert, InsertionMethod, RestoreClipboard);
        EventRaised?.Invoke(new Event.Delivered(result));
    }

    private void Fail(string message)
    {
        ErrorMessage = message;
        _task = null;
        SetState(DictationState.Failed);
        EventRaised?.Invoke(new Event.Failed(message));
    }

    private void SetState(DictationState state)
    {
        State = state;
        StateChanged?.Invoke();
    }
}
