import Foundation
@preconcurrency import WhisperKit

/// Moteur de transcription abstrait (permet de tester le contrôleur sans modèle).
public protocol Transcribing: Sendable {
    /// `language` : code ISO ou "auto" ; `translate` : produire du texte anglais quelle que soit la langue parlée.
    func transcribe(_ samples: [Float], language: String, translate: Bool) async throws -> String
}

/// Comment décoder : transcrire dans la langue parlée, ou traduire en anglais.
public struct DecodingPlan: Equatable, Sendable {
    public var translateTask: Bool
    /// nil = langue détectée automatiquement.
    public var language: String?

    /// `large-v3-turbo` n'a pas été entraîné pour la tâche « translate » de Whisper : on lui impose
    /// alors la sortie anglaise (il traduit en écrivant directement en anglais), ce qui marche bien en pratique.
    public static func make(language: String, translate: Bool, modelIsTurbo: Bool) -> DecodingPlan {
        let source: String? = language == "auto" ? nil : language
        guard translate else { return DecodingPlan(translateTask: false, language: source) }
        if modelIsTurbo { return DecodingPlan(translateTask: false, language: "en") }
        return DecodingPlan(translateTask: true, language: source)
    }
}

public enum TranscriberError: LocalizedError {
    case notLoaded

    public var errorDescription: String? {
        switch self {
        case .notLoaded: "The model is not loaded yet."
        }
    }
}

/// Enveloppe WhisperKit : le modèle est chargé une fois puis gardé en mémoire,
/// ce qui supprime le temps de chargement à chaque dictée.
public actor Transcriber: Transcribing {
    private var pipe: WhisperKit?
    public private(set) var loadedModel: WhisperModel?

    public init() {}

    /// Télécharge la variante si besoin (progression 0…1), puis la charge.
    public func load(_ model: WhisperModel, base: URL = ModelStore.baseURL,
                     progress: (@Sendable (Double) -> Void)? = nil) async throws {
        if loadedModel == model, pipe != nil { return }
        pipe = nil
        loadedModel = nil
        let folder = try await Self.download(model, base: base, progress: progress)
        let config = WhisperKitConfig(modelFolder: folder.path, verbose: false, logLevel: .error,
                                      prewarm: true, load: true, download: false)
        pipe = try await WhisperKit(config)
        loadedModel = model
    }

    public static func download(_ model: WhisperModel, base: URL = ModelStore.baseURL,
                                progress: (@Sendable (Double) -> Void)? = nil) async throws -> URL {
        if ModelStore.isInstalled(model, base: base) { return ModelStore.folder(for: model, base: base) }
        try FileManager.default.createDirectory(at: base, withIntermediateDirectories: true)
        let folder = try await WhisperKit.download(variant: model.id, downloadBase: base) { p in
            progress?(p.fractionCompleted)
        }
        ModelStore.markInstalled(model, base: base)
        return folder
    }

    public func unload() {
        pipe = nil
        loadedModel = nil
    }

    public func transcribe(_ samples: [Float], language: String, translate: Bool) async throws -> String {
        guard let pipe else { throw TranscriberError.notLoaded }
        let plan = DecodingPlan.make(language: language, translate: translate,
                                     modelIsTurbo: loadedModel?.id.contains("turbo") ?? false)
        let auto = plan.language == nil
        let options = DecodingOptions(task: plan.translateTask ? .translate : .transcribe, language: plan.language,
                                      temperature: 0, usePrefillPrompt: !auto, detectLanguage: auto,
                                      skipSpecialTokens: true, withoutTimestamps: true)
        let results = try await pipe.transcribe(audioArray: samples, decodeOptions: options)
        return results.map(\.text).joined(separator: " ")
    }
}
