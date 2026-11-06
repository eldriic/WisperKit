import Foundation

/// Modèles Whisper proposés dans les réglages (dépôt argmaxinc/whisperkit-coreml).
public struct WhisperModel: Identifiable, Hashable, Sendable {
    public let id: String          // variante WhisperKit
    public let title: String
    public let detail: String
    public let size: String

    public static let turbo = WhisperModel(id: "openai_whisper-large-v3-v20240930_turbo", title: "large-v3-turbo",
                                           detail: "Meilleur compromis précision / vitesse", size: "1,6 Go")
    public static let large = WhisperModel(id: "openai_whisper-large-v3", title: "large-v3",
                                           detail: "Le plus précis, plus lent", size: "3 Go")
    public static let small = WhisperModel(id: "openai_whisper-small", title: "small",
                                           detail: "Rapide, moins précis sur le jargon", size: "480 Mo")
    public static let base = WhisperModel(id: "openai_whisper-base", title: "base",
                                          detail: "Très rapide, pour tester", size: "140 Mo")

    public static let all: [WhisperModel] = [.turbo, .large, .small, .base]
    public static let `default` = WhisperModel.turbo

    public static func named(_ id: String) -> WhisperModel {
        all.first { $0.id == id } ?? .default
    }
}

public enum ModelStore {
    /// ~/Library/Application Support/WisperKit/Models
    public static var baseURL: URL {
        let support = FileManager.default.urls(for: .applicationSupportDirectory, in: .userDomainMask)[0]
        return support.appending(path: "WisperKit/Models", directoryHint: .isDirectory)
    }

    /// Emplacement où WhisperKit (Hub) range une variante téléchargée.
    public static func folder(for model: WhisperModel, base: URL = baseURL) -> URL {
        base.appending(path: "models/argmaxinc/whisperkit-coreml/\(model.id)", directoryHint: .isDirectory)
    }

    /// Fichier écrit une fois le téléchargement terminé : un dossier partiel
    /// (téléchargement interrompu) n'est donc jamais pris pour un modèle installé.
    static let completeMarker = ".wisperkit-complete"

    public static func isInstalled(_ model: WhisperModel, base: URL = baseURL) -> Bool {
        FileManager.default.fileExists(atPath: folder(for: model, base: base).appending(path: completeMarker).path)
    }

    static func markInstalled(_ model: WhisperModel, base: URL = baseURL) {
        FileManager.default.createFile(atPath: folder(for: model, base: base).appending(path: completeMarker).path,
                                       contents: Data())
    }

    public static func remove(_ model: WhisperModel, base: URL = baseURL) throws {
        try FileManager.default.removeItem(at: folder(for: model, base: base))
    }

    public static func diskUsage(base: URL = baseURL) -> Int64 {
        guard let e = FileManager.default.enumerator(at: base, includingPropertiesForKeys: [.totalFileAllocatedSizeKey]) else { return 0 }
        var total: Int64 = 0
        for case let url as URL in e {
            total += Int64((try? url.resourceValues(forKeys: [.totalFileAllocatedSizeKey]).totalFileAllocatedSize) ?? 0)
        }
        return total
    }
}
