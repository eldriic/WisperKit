import Foundation

/// Nettoie la sortie de Whisper avant de l'insérer.
public enum TextCleaner {
    /// Phrases que Whisper « hallucine » sur du silence ou du bruit (issues des
    /// sous-titres de son corpus). Supprimées seulement si elles constituent
    /// tout le texte, pour ne jamais tronquer une vraie dictée.
    static let hallucinations: [String] = [
        "sous-titres réalisés par la communauté d'amara.org",
        "sous-titrage st' 501",
        "sous-titrage société radio-canada",
        "merci d'avoir regardé cette vidéo",
        "merci d'avoir regardé",
        "merci de votre attention",
        "abonnez-vous",
        "thank you for watching",
        "thanks for watching",
        "you",
    ]

    public static func clean(_ raw: String) -> String {
        // Jetons non verbaux : [Musique], (rires), *applaudissements*…
        var text = raw.replacingOccurrences(of: #"\[[^\]]*\]|\([^)]*\)|\*[^*]*\*"#, with: " ",
                                            options: .regularExpression)
        text = text.replacingOccurrences(of: #"\s+"#, with: " ", options: .regularExpression)
            .trimmingCharacters(in: .whitespacesAndNewlines)

        let key = text.lowercased()
            .trimmingCharacters(in: CharacterSet(charactersIn: " .!…"))
        if hallucinations.contains(key) { return "" }
        return text
    }
}
