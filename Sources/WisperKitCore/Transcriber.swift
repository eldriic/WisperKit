import Foundation
import WhisperKit

/// Moteur de transcription abstrait (permet de tester le contrôleur sans modèle).
public protocol Transcribing: Sendable {
    func transcribe(_ samples: [Float], language: String) async throws -> String
}

public enum TranscriberError: LocalizedError {
    case notLoaded

    public var errorDescription: String? {
        switch self {
        case .notLoaded: "Le modèle n'est pas encore chargé."
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

    public func transcribe(_ samples: [Float], language: String) async throws -> String {
        guard let pipe else { throw TranscriberError.notLoaded }
        let auto = language == "auto"
        let options = DecodingOptions(task: .transcribe, language: auto ? nil : language, temperature: 0,
                                      usePrefillPrompt: !auto, detectLanguage: auto,
                                      skipSpecialTokens: true, withoutTimestamps: true)
        let results = try await pipe.transcribe(audioArray: samples, decodeOptions: options)
        return results.map(\.text).joined(separator: " ")
    }
}
