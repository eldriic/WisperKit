import AppKit
import ApplicationServices
import Carbon.HIToolbox

/// Destination du texte transcrit (permet de tester le contrôleur).
@MainActor
public protocol TextOutput: AnyObject {
    /// Renvoie `true` si le texte a été collé au curseur, `false` s'il est
    /// seulement resté dans le presse-papiers.
    func deliver(_ text: String, autoPaste: Bool, restoreClipboard: Bool) -> Bool
}

/// Met le texte dans le presse-papiers puis simule ⌘V dans l'app au premier plan.
/// Diagnostic : ~/Library/Logs/WisperKit/wisperkit.log
@MainActor
public final class TextInserter: TextOutput {
    public init() {}

    /// Le collage n'a besoin que du droit d'envoyer des événements clavier
    /// (rangé par macOS dans Accessibilité) ; AXIsProcessTrusted le couvre aussi.
    public static var hasAccessibility: Bool { canPostEvents || AXIsProcessTrusted() }
    public static var canPostEvents: Bool { CGPreflightPostEventAccess() }

    /// Ouvre la demande système d'autorisation (ajoute WisperKit à la liste Accessibilité).
    public static func requestAccessibility() {
        _ = CGRequestPostEventAccess()
        let key = "AXTrustedCheckOptionPrompt" // valeur de kAXTrustedCheckOptionPrompt
        _ = AXIsProcessTrustedWithOptions([key: true] as CFDictionary)
    }

    public func deliver(_ text: String, autoPaste: Bool, restoreClipboard: Bool) -> Bool {
        let pb = NSPasteboard.general
        let saved = restoreClipboard ? Self.snapshot(pb) : nil

        pb.clearContents()
        pb.setString(text, forType: .string)

        let front = NSWorkspace.shared.frontmostApplication?.bundleIdentifier ?? "?"
        let post = Self.canPostEvents, ax = AXIsProcessTrusted()
        let trusted = post || ax
        let secure = IsSecureEventInputEnabled()
        DiagnosticLog.write("livraison \(text.count) car. → app=\(front) autoPaste=\(autoPaste) envoiTouches=\(post) axTrusted=\(ax) saisieSécurisée=\(secure)")

        guard autoPaste else { return false }
        guard trusted else {
            DiagnosticLog.write("collage impossible : WisperKit n'est pas autorisé dans Accessibilité")
            return false
        }
        let count = pb.changeCount
        Task { @MainActor in
            await Self.pressCommandV()
            DiagnosticLog.write("⌘V envoyé à \(front)")
            guard let saved else { return }
            // Laisse à l'app cible (Chromium/Electron lit le presse-papiers en asynchrone)
            // le temps de coller avant de restaurer l'ancien contenu.
            try? await Task.sleep(for: .milliseconds(1000))
            guard pb.changeCount == count else { return } // l'utilisateur a recopié entre-temps
            pb.clearContents()
            pb.writeObjects(saved)
        }
        return true
    }

    private static func snapshot(_ pb: NSPasteboard) -> [NSPasteboardItem] {
        (pb.pasteboardItems ?? []).map { item in
            let copy = NSPasteboardItem()
            for type in item.types {
                if let data = item.data(forType: type) { copy.setData(data, forType: type) }
            }
            return copy
        }
    }

    /// Séquence complète ⌘↓ V↓ V↑ ⌘↑ : les apps Chromium/Electron ignorent souvent un
    /// « V » portant seulement le drapeau ⌘ sans vrai appui de la touche Commande.
    private static func pressCommandV() async {
        let source = CGEventSource(stateID: .hidSystemState)
        let command: CGKeyCode = 0x37 // kVK_Command
        let v: CGKeyCode = 0x09       // kVK_ANSI_V (même position physique en AZERTY)
        let steps: [(CGKeyCode, Bool, CGEventFlags)] = [
            (command, true, .maskCommand), (v, true, .maskCommand),
            (v, false, .maskCommand), (command, false, []),
        ]
        for (key, down, flags) in steps {
            let event = CGEvent(keyboardEventSource: source, virtualKey: key, keyDown: down)
            event?.flags = flags
            event?.post(tap: .cgSessionEventTap)
            try? await Task.sleep(for: .milliseconds(12))
        }
    }
}
