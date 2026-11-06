import AppKit
import ApplicationServices
import AVFoundation
import KeyboardShortcuts
import Observation
import ServiceManagement
import WisperKitCore

extension KeyboardShortcuts.Name {
    static let toggleDictation = Self("toggleDictation", initial: .init(.d, modifiers: [.control, .option]))
    /// Actif seulement pendant l'écoute, pour ne pas voler Échap au reste du système.
    static let cancelDictation = Self("cancelDictation", initial: .init(.escape))
}

enum TriggerMode: String, CaseIterable, Identifiable {
    case toggle, hold
    var id: String { rawValue }
    @MainActor var label: String { self == .toggle ? L("Toggle") : L("Hold") }
    @MainActor var detail: String {
        self == .toggle ? L("Press once to start, press again to stop.")
                        : L("Speak while holding the shortcut, release to transcribe.")
    }
}

enum ModelState: Equatable {
    case missing
    case downloading(Double)
    case loading
    case ready
    case failed(String)
}

/// État global de l'app : réglages persistés, modèle, dictée, raccourcis.
@MainActor
@Observable
final class AppModel {
    static let languages: [(id: String, label: String)] = [
        ("en", "English"), ("fr", "French"), ("auto", "Auto-detect"),
    ]

    let dictation: DictationController
    private let transcriber = Transcriber()
    private let defaults = UserDefaults.standard
    @ObservationIgnored private lazy var hud = RecordingHUD(dictation: dictation)
    /// Coupe les sons sans toucher au réglage persisté (autotest).
    @ObservationIgnored var transientMute = false
    @ObservationIgnored private var askedAccessibility = false
    /// Le dernier collage auto a échoué faute d'autorisation Accessibilité.
    private(set) var pasteBlocked = false
    /// Premier lancement : la fenêtre s'ouvre sur l'accueil et les autorisations.
    let isFirstLaunch: Bool

    private(set) var modelState: ModelState = .missing
    /// Téléchargements en cours depuis la page Modèle (hors modèle actif).
    private(set) var downloads: [String: Double] = [:]
    private(set) var installed: Set<String> = []

    // MARK: Réglages persistés

    var modelID: String { didSet { defaults.set(modelID, forKey: "modelID") } }
    var language: String { didSet { defaults.set(language, forKey: "language"); dictation.language = language } }
    var autoPaste: Bool { didSet { defaults.set(autoPaste, forKey: "autoPaste"); dictation.autoPaste = autoPaste } }
    var restoreClipboard: Bool {
        didSet { defaults.set(restoreClipboard, forKey: "restoreClipboard"); dictation.restoreClipboard = restoreClipboard }
    }
    var sounds: Bool { didSet { defaults.set(sounds, forKey: "sounds") } }
    /// Mode frappe : le texte est tapé touche par touche, le presse-papiers n'est jamais utilisé.
    var neverUseClipboard: Bool {
        didSet {
            defaults.set(neverUseClipboard, forKey: "neverUseClipboard")
            dictation.insertionMethod = neverUseClipboard ? .type : .paste
        }
    }
    var interfaceLanguage: AppLanguage {
        get { Localizer.shared.language }
        set { Localizer.shared.language = newValue }
    }
    var mode: TriggerMode { didSet { defaults.set(mode.rawValue, forKey: "mode") } }
    var escapeCancels: Bool { didSet { defaults.set(escapeCancels, forKey: "escapeCancels") } }
    var showHUD: Bool { didSet { defaults.set(showHUD, forKey: "showHUD") } }

    var launchAtLogin: Bool {
        get { _ = launchTick; return SMAppService.mainApp.status == .enabled }
        set {
            do {
                if newValue { try SMAppService.mainApp.register() } else { try SMAppService.mainApp.unregister() }
            } catch {
                launchError = error.localizedDescription
            }
            launchTick += 1
        }
    }
    private(set) var launchError: String?
    private var launchTick = 0 // force la relecture de `launchAtLogin`

    var activeModel: WhisperModel { WhisperModel.named(modelID) }

    init() {
        defaults.register(defaults: [
            "modelID": WhisperModel.default.id, "language": "fr", "autoPaste": true,
            "restoreClipboard": true, "sounds": true, "mode": TriggerMode.toggle.rawValue, "escapeCancels": true,
            "neverUseClipboard": false,
            "showHUD": true,
        ])
        modelID = defaults.string(forKey: "modelID") ?? WhisperModel.default.id
        language = defaults.string(forKey: "language") ?? "fr"
        autoPaste = defaults.bool(forKey: "autoPaste")
        restoreClipboard = defaults.bool(forKey: "restoreClipboard")
        sounds = defaults.bool(forKey: "sounds")
        neverUseClipboard = defaults.bool(forKey: "neverUseClipboard")
        mode = TriggerMode(rawValue: defaults.string(forKey: "mode") ?? "") ?? .toggle
        escapeCancels = defaults.bool(forKey: "escapeCancels")
        let args = CommandLine.arguments
        isFirstLaunch = !defaults.bool(forKey: "onboarded") || args.contains("--first-launch")
        // Les modes Debug (captures, autotest) partagent les réglages : ils ne consomment pas l'accueil.
        if !args.contains("--snapshots") && !args.contains("--selftest") && !args.contains("--icon") {
            defaults.set(true, forKey: "onboarded")
        }
        showHUD = defaults.bool(forKey: "showHUD")

        dictation = DictationController(recorder: AudioRecorder(), transcriber: transcriber, output: TextInserter())
        dictation.language = language
        dictation.autoPaste = autoPaste
        dictation.restoreClipboard = restoreClipboard
        dictation.insertionMethod = neverUseClipboard ? .type : .paste
        dictation.onEvent = { [weak self] in self?.handle($0) }

        refreshInstalled()
        setUpShortcuts()
        let renderOnly = CommandLine.arguments.contains("--snapshots") || CommandLine.arguments.contains("--icon")
        if !renderOnly { Task { await loadActiveModel() } }
        let bundle = Bundle.main.bundlePath
        DiagnosticLog.write("démarrage \(bundle) envoiTouches=\(TextInserter.canPostEvents) axTrusted=\(AXIsProcessTrusted())")
    }

    // MARK: Dictée

    var canDictate: Bool { modelState == .ready }

    func toggleDictation() {
        guard canDictate else { return }
        dictation.toggle()
    }

    private func setUpShortcuts() {
        KeyboardShortcuts.disable(.cancelDictation)
        KeyboardShortcuts.onKeyDown(for: .toggleDictation) { [weak self] in
            guard let self else { return }
            DiagnosticLog.write("raccourci ↓ état=\(String(describing: self.dictation.state)) modèle=\(String(describing: self.modelState))")
            guard self.canDictate else { return }
            switch self.mode {
            case .toggle: self.dictation.toggle()
            case .hold: self.dictation.start()
            }
        }
        KeyboardShortcuts.onKeyUp(for: .toggleDictation) { [weak self] in
            guard let self, self.mode == .hold else { return }
            self.dictation.stop()
        }
        KeyboardShortcuts.onKeyDown(for: .cancelDictation) { [weak self] in
            self?.dictation.cancel()
        }
    }

    private func handle(_ event: DictationController.Event) {
        switch event {
        case .started:
            if escapeCancels { KeyboardShortcuts.enable(.cancelDictation) }
            if showHUD { hud.show() }
            play("Tink")
        case .stopped:
            KeyboardShortcuts.disable(.cancelDictation)
            play("Pop")
        case .delivered(let result):
            let blocked = autoPaste && (result == .clipboardOnly || result == .notInserted)
            pasteBlocked = blocked
            switch result {
            case .pasted, .typed: hud.hide()
            case .clipboardOnly: autoPaste ? hud.flash(L("Copied · ⌘V to paste")) : hud.hide()
            case .notInserted: autoPaste ? hud.flash(L("Accessibility required to type the text")) : hud.hide()
            }
            // Insertion demandée mais refusée par macOS : on propose l'autorisation une fois.
            if blocked && !askedAccessibility {
                askedAccessibility = true
                TextInserter.requestAccessibility()
            }
            DiagnosticLog.write("texte livré, résultat=\(result) accessibilité=\(TextInserter.hasAccessibility)")
            play("Glass")
        case .cancelled:
            hud.hide()
            KeyboardShortcuts.disable(.cancelDictation)
        case .failed:
            hud.hide()
            KeyboardShortcuts.disable(.cancelDictation)
            play("Basso")
        }
    }

    private func play(_ name: String) {
        guard sounds, !transientMute else { return }
        NSSound(named: NSSound.Name(name))?.play()
    }

    #if DEBUG
    /// Captures d'écran : état « prêt » sans attendre le chargement du modèle.
    func forceReadyForSnapshots() { modelState = .ready }
    #endif

    // MARK: Modèles

    func refreshInstalled() {
        installed = Set(WhisperModel.all.filter { ModelStore.isInstalled($0) }.map(\.id))
    }

    /// Télécharge (si besoin) puis charge le modèle actif.
    func loadActiveModel() async {
        let model = activeModel
        modelState = ModelStore.isInstalled(model) ? .loading : .downloading(0)
        do {
            try await transcriber.load(model) { fraction in
                Task { @MainActor in
                    guard self.activeModel == model else { return }
                    self.modelState = fraction >= 1 ? .loading : .downloading(fraction)
                }
            }
            guard activeModel == model else { return } // changé pendant le chargement
            modelState = .ready
        } catch {
            guard activeModel == model else { return }
            modelState = .failed(error.localizedDescription)
        }
        refreshInstalled()
    }

    func use(_ model: WhisperModel) {
        guard model.id != modelID, dictation.state == .idle || isFailed else { return }
        modelID = model.id
        Task { await loadActiveModel() }
    }

    func download(_ model: WhisperModel) {
        guard downloads[model.id] == nil else { return }
        downloads[model.id] = 0
        Task {
            do {
                _ = try await Transcriber.download(model) { fraction in
                    Task { @MainActor in self.downloads[model.id] = fraction }
                }
            } catch {
                NSLog("WisperKit: échec du téléchargement de \(model.id): \(error)")
            }
            downloads[model.id] = nil
            refreshInstalled()
        }
    }

    func delete(_ model: WhisperModel) {
        guard model.id != modelID else { return }
        try? ModelStore.remove(model)
        refreshInstalled()
    }

    private var isFailed: Bool { if case .failed = dictation.state { true } else { false } }

    // MARK: Autorisations

    var micAuthorized: Bool { AVCaptureDevice.authorizationStatus(for: .audio) == .authorized }
    var micDenied: Bool {
        let s = AVCaptureDevice.authorizationStatus(for: .audio)
        return s == .denied || s == .restricted
    }

    func requestMicrophone() async -> Bool {
        await AVCaptureDevice.requestAccess(for: .audio)
    }

    static func openPrivacyPane(_ anchor: String) {
        if let url = URL(string: "x-apple.systempreferences:com.apple.preference.security?\(anchor)") {
            NSWorkspace.shared.open(url)
        }
    }

    // MARK: Libellés

    var statusLine: String {
        switch dictation.state {
        case .recording: return L("Listening…")
        case .transcribing: return L("Transcribing…")
        case .failed(let message): return L(message)
        case .idle: break
        }
        switch modelState {
        case .missing: return L("No model")
        case .downloading(let f): return L("Downloading %d%%", Int(f * 100))
        case .loading: return L("Loading model…")
        case .ready:
            if pasteBlocked && !TextInserter.hasAccessibility { return L("Auto-insert blocked: allow Accessibility") }
            return L("Ready · %@", activeModel.title)
        case .failed(let message): return message
        }
    }
}
