import Foundation
import Observation

/// Machine à états de la dictée : repos → écoute → transcription → repos.
@MainActor
@Observable
public final class DictationController {
    public enum State: Equatable, Sendable {
        case idle
        case recording
        case transcribing
        case failed(String)
    }

    public enum Event: Sendable {
        case started, stopped, delivered(DeliveryResult), cancelled, failed
    }

    public private(set) var state: State = .idle
    public private(set) var lastText: String?

    // Réglages appliqués à chaque dictée (alimentés par l'app).
    public var language = "fr"
    public var autoPaste = true
    public var insertionMethod: InsertionMethod = .paste
    public var restoreClipboard = true
    /// Dictées plus courtes ignorées (appui accidentel).
    public var minimumDuration: Double = 0.3

    /// Notifié à chaque transition (sons, icône…).
    public var onEvent: ((Event) -> Void)?

    private let recorder: AudioRecording
    private let transcriber: Transcribing
    private let output: TextOutput
    private var task: Task<Void, Never>?

    public init(recorder: AudioRecording, transcriber: Transcribing, output: TextOutput) {
        self.recorder = recorder
        self.transcriber = transcriber
        self.output = output
    }

    public var isRecording: Bool { state == .recording }

    /// Niveau micro 0…1 pendant l'écoute (non observé : à lire à chaque image).
    public var audioLevel: Float { state == .recording ? recorder.level : 0 }

    /// Mode bascule : démarre, ou arrête et transcrit.
    public func toggle() {
        if state == .recording { stop() } else { start() }
    }

    public func start() {
        guard state != .recording, state != .transcribing else { return }
        do {
            try recorder.start()
            state = .recording
            onEvent?(.started)
        } catch {
            fail(error)
        }
    }

    /// Arrête l'écoute et lance la transcription en tâche de fond.
    public func stop() {
        guard state == .recording else { return }
        let samples = recorder.stop()
        onEvent?(.stopped)

        guard Double(samples.count) / AudioRecorder.sampleRate >= minimumDuration else {
            state = .idle
            onEvent?(.cancelled)
            return
        }
        state = .transcribing
        let lang = language
        task = Task { [transcriber] in
            do {
                let raw = try await transcriber.transcribe(samples, language: lang)
                self.finish(raw)
            } catch is CancellationError {
                self.state = .idle
            } catch {
                self.fail(error)
            }
        }
    }

    /// Abandonne l'écoute (Échap) sans rien transcrire.
    public func cancel() {
        switch state {
        case .recording:
            _ = recorder.stop()
        case .transcribing:
            task?.cancel()
        default:
            return
        }
        task = nil
        state = .idle
        onEvent?(.cancelled)
    }

    /// Réinitialise après une erreur affichée.
    public func dismissError() {
        if case .failed = state { state = .idle }
    }

    /// Attend la fin de la transcription en cours (tests).
    public func waitForCompletion() async {
        await task?.value
    }

    private func finish(_ raw: String) {
        guard state == .transcribing else { return } // annulé entre-temps
        let text = TextCleaner.clean(raw)
        state = .idle
        task = nil
        guard !text.isEmpty else {
            onEvent?(.cancelled)
            return
        }
        lastText = text
        let result = output.deliver(text, autoInsert: autoPaste, method: insertionMethod,
                                    restoreClipboard: restoreClipboard)
        onEvent?(.delivered(result))
    }

    private func fail(_ error: Error) {
        state = .failed(error.localizedDescription)
        task = nil
        onEvent?(.failed)
    }
}
