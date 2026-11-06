using System.Windows;
using System.Windows.Threading;
using WisperKit.App.Platform;
using WisperKit.App.UI;
using WisperKit.Core;
using static WisperKit.Core.Localizer;

namespace WisperKit.App;

internal enum ModelStatus { Missing, Downloading, Loading, Ready, Failed }

/// <summary>État global de l'app : réglages, modèle, dictée, raccourci (équivalent d'AppModel.swift).</summary>
internal sealed class AppController : IDisposable
{
    private readonly Dispatcher _ui = Dispatcher.CurrentDispatcher;
    private readonly Transcriber _transcriber = new();
    private readonly GlobalHotkey _hotkey;
    private readonly RecordingHud _hud;
    private bool _askedAboutMic;

    public AppSettings Settings { get; }
    public DictationController Dictation { get; }
    public ModelStore Store { get; } = ModelStore.Default;
    public bool IsFirstLaunch { get; }

    public ModelStatus ModelStatus { get; private set; } = ModelStatus.Missing;
    public double ModelProgress { get; private set; }
    public string? ModelError { get; private set; }
    /// <summary>Téléchargements lancés depuis la page Modèle (hors modèle actif).</summary>
    public Dictionary<string, double> Downloads { get; } = [];

    /// <summary>Tout changement visible (rafraîchit icône, menu et réglages).</summary>
    public event Action? Changed;

    public AppController(bool renderOnly = false)
    {
        Settings = AppSettings.Load();
        IsFirstLaunch = !Settings.Onboarded;
        SetLanguage(Settings.InterfaceLanguage);
        Sounds.Enabled = Settings.Sounds;

        Dictation = new DictationController(new AudioRecorder(), _transcriber, new TextInserter());
        ApplySettings();
        Dictation.StateChanged += () => Changed?.Invoke();
        Dictation.EventRaised += Handle;
        _hud = new RecordingHud(Dictation);

        _hotkey = renderOnly ? null! : new GlobalHotkey();
        if (!renderOnly)
        {
            _hotkey.Shortcut = Shortcut.Parse(Settings.Shortcut) ?? Shortcut.Default;
            _hotkey.EscapeActive = () => Settings.EscapeCancels && Dictation.IsRecording;
            _hotkey.Pressed += () => _ui.BeginInvoke(OnShortcutDown);
            _hotkey.Released += () => _ui.BeginInvoke(OnShortcutUp);
            _hotkey.EscapePressed += () => _ui.BeginInvoke(Dictation.Cancel);
            if (IsFirstLaunch) { Settings.Onboarded = true; Save(); }
            _ = LoadActiveModelAsync();
        }
        DiagnosticLog.Write($"démarrage {Environment.ProcessPath} micro={(MicrophonePrivacy.HasDevice ? "ok" : "absent")} bloqué={MicrophonePrivacy.IsBlocked}");
    }

    public WhisperModel ActiveModel => WhisperModel.Named(Settings.ModelId);
    public bool CanDictate => ModelStatus == ModelStatus.Ready;

    // ---- Réglages ----

    public void Update(Action<AppSettings> change)
    {
        change(Settings);
        ApplySettings();
        Save();
        Changed?.Invoke();
    }

    private void ApplySettings()
    {
        Dictation.Language = Settings.DictationLanguage;
        Dictation.AutoInsert = Settings.AutoInsert;
        Dictation.InsertionMethod = Settings.NeverUseClipboard ? InsertionMethod.Type : InsertionMethod.Paste;
        Dictation.RestoreClipboard = Settings.RestoreClipboard;
        Sounds.Enabled = Settings.Sounds;
        if (Language != Settings.InterfaceLanguage) SetLanguage(Settings.InterfaceLanguage);
    }

    private void Save()
    {
        try { Settings.Save(); } catch (Exception e) { DiagnosticLog.Write($"enregistrement des réglages impossible : {e.Message}"); }
    }

    public void SetShortcut(Shortcut shortcut)
    {
        _hotkey.Shortcut = shortcut;
        Update(s => s.Shortcut = shortcut.ToString());
    }

    /// <summary>Capture la prochaine combinaison de touches comme nouveau raccourci.</summary>
    public void RecordShortcut(Action<Shortcut> done) =>
        _hotkey.Capture = s => _ui.BeginInvoke(() => { SetShortcut(s); done(s); });

    public string ShortcutLabel => (Shortcut.Parse(Settings.Shortcut) ?? Shortcut.Default).ToString();

    public bool StartWithWindows
    {
        get => Startup.IsEnabled;
        set { try { Startup.IsEnabled = value; } catch (Exception e) { DiagnosticLog.Write($"démarrage auto : {e.Message}"); } Changed?.Invoke(); }
    }

    // ---- Dictée ----

    public void ToggleDictation()
    {
        if (CanDictate || Dictation.IsRecording) Dictation.Toggle();
    }

    private void OnShortcutDown()
    {
        DiagnosticLog.Write($"raccourci ↓ état={Dictation.State} modèle={ModelStatus}");
        if (!CanDictate) return;
        if (Settings.Mode == TriggerMode.Toggle) Dictation.Toggle(); else Dictation.Start();
    }

    private void OnShortcutUp()
    {
        if (Settings.Mode == TriggerMode.Hold) Dictation.Stop();
    }

    private void Handle(DictationController.Event e)
    {
        switch (e)
        {
            case DictationController.Event.Started:
                if (Settings.ShowIndicator) _hud.ShowRecording();
                Sounds.Start();
                if (!_askedAboutMic && MicrophonePrivacy.IsBlocked)
                {
                    _askedAboutMic = true;
                    _hud.Flash(L("Microphone blocked in Windows privacy settings"));
                }
                break;
            case DictationController.Event.Stopped:
                Sounds.Stop();
                break;
            case DictationController.Event.Delivered d:
                if (d.Result == DeliveryResult.ClipboardOnly && Settings.AutoInsert) _hud.Flash(L("Copied · Ctrl+V to paste"));
                else _hud.HideHud();
                Sounds.Delivered();
                DiagnosticLog.Write($"texte livré, résultat={d.Result}");
                break;
            case DictationController.Event.Cancelled:
                _hud.HideHud();
                break;
            case DictationController.Event.Failed f:
                _hud.HideHud();
                Sounds.Error();
                DiagnosticLog.Write($"échec : {f.Message}");
                break;
        }
        Changed?.Invoke();
    }

    // ---- Modèles ----

    public async Task LoadActiveModelAsync()
    {
        var model = ActiveModel;
        ModelStatus = Store.IsInstalled(model) ? ModelStatus.Loading : ModelStatus.Downloading;
        ModelProgress = 0;
        ModelError = null;
        Changed?.Invoke();
        var progress = new Progress<double>(f =>
        {
            if (ActiveModel != model) return;
            ModelProgress = f;
            ModelStatus = f >= 1 ? ModelStatus.Loading : ModelStatus.Downloading;
            Changed?.Invoke();
        });
        try
        {
            await _transcriber.LoadAsync(model, Store, progress);
            if (ActiveModel != model) return;
            ModelStatus = ModelStatus.Ready;
            DiagnosticLog.Write($"modèle prêt : {model.Id}");
        }
        catch (Exception e)
        {
            if (ActiveModel != model) return;
            ModelStatus = ModelStatus.Failed;
            ModelError = e.Message;
            DiagnosticLog.Write($"échec du modèle {model.Id} : {e.Message}");
        }
        Changed?.Invoke();
    }

    public void Use(WhisperModel model)
    {
        if (model.Id == Settings.ModelId || Dictation.State is DictationController.DictationState.Recording or DictationController.DictationState.Transcribing) return;
        Update(s => s.ModelId = model.Id);
        _ = LoadActiveModelAsync();
    }

    public async void Download(WhisperModel model)
    {
        if (Downloads.ContainsKey(model.Id)) return;
        Downloads[model.Id] = 0;
        Changed?.Invoke();
        try
        {
            await Store.EnsureAsync(model, new Progress<double>(f => { Downloads[model.Id] = f; Changed?.Invoke(); }));
        }
        catch (Exception e)
        {
            DiagnosticLog.Write($"échec du téléchargement de {model.Id} : {e.Message}");
        }
        Downloads.Remove(model.Id);
        Changed?.Invoke();
    }

    public void Delete(WhisperModel model)
    {
        if (model.Id == Settings.ModelId) return;
        try { Store.Remove(model); } catch (Exception e) { DiagnosticLog.Write($"suppression impossible : {e.Message}"); }
        Changed?.Invoke();
    }

    // ---- Libellés ----

    public string StatusLine => Dictation.State switch
    {
        DictationController.DictationState.Recording => L("Listening…"),
        DictationController.DictationState.Transcribing => L("Transcribing…"),
        DictationController.DictationState.Failed => L(Dictation.ErrorMessage ?? ""),
        _ => ModelStatus switch
        {
            ModelStatus.Missing => L("No model"),
            ModelStatus.Downloading => L("Downloading %d%%", (int)(ModelProgress * 100)),
            ModelStatus.Loading => L("Loading model…"),
            ModelStatus.Failed => L(ModelError ?? ""),
            _ => L("Ready · %@", ActiveModel.Title),
        },
    };

    public void Dispose()
    {
        _hotkey?.Dispose();
        _transcriber.Dispose();
        _hud.Close();
    }
}
