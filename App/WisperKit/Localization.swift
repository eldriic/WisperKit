import Foundation
import Observation

/// Langue de l'interface, choisie dans les réglages (anglais par défaut, quelle que soit
/// la langue du Mac). Les textes sources sont en anglais ; les traductions sont dans
/// `<langue>.lproj/Localizable.strings`.
enum AppLanguage: String, CaseIterable, Identifiable {
    case english = "en"
    case french = "fr"

    var id: String { rawValue }

    /// Toujours affiché dans sa propre langue.
    var nativeName: String {
        switch self {
        case .english: "English"
        case .french: "Français"
        }
    }
}

@MainActor
@Observable
final class Localizer {
    static let shared = Localizer()

    var language: AppLanguage {
        didSet {
            UserDefaults.standard.set(language.rawValue, forKey: "appLanguage")
            bundle = Self.bundle(for: language)
        }
    }

    @ObservationIgnored private var bundle: Bundle?

    private init() {
        let stored = UserDefaults.standard.string(forKey: "appLanguage")
        language = AppLanguage(rawValue: stored ?? "") ?? .english
        bundle = Self.bundle(for: language)
    }

    /// Lecture observée : une vue qui affiche un texte traduit se redessine quand la langue change.
    func string(_ key: String) -> String {
        _ = language
        return bundle?.localizedString(forKey: key, value: key, table: nil) ?? key
    }

    var locale: Locale { Locale(identifier: language.rawValue) }

    private static func bundle(for language: AppLanguage) -> Bundle? {
        guard language != .english,
              let path = Bundle.main.path(forResource: language.rawValue, ofType: "lproj") else { return nil }
        return Bundle(path: path)
    }
}

/// Texte d'interface traduit ; `args` remplit les `%@` / `%d` du texte source anglais.
@MainActor
func L(_ key: String, _ args: CVarArg...) -> String {
    let localizer = Localizer.shared
    let format = localizer.string(key)
    return args.isEmpty ? format : String(format: format, locale: localizer.locale, arguments: args)
}
