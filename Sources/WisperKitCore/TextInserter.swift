import AppKit
import ApplicationServices

/// Destination du texte transcrit (permet de tester le contrôleur).
@MainActor
public protocol TextOutput: AnyObject {
    /// Renvoie `true` si le texte a été collé au curseur, `false` s'il est
    /// seulement resté dans le presse-papiers.
    func deliver(_ text: String, autoPaste: Bool, restoreClipboard: Bool) -> Bool
}

/// Met le texte dans le presse-papiers puis simule ⌘V dans l'app au premier plan.
@MainActor
public final class TextInserter: TextOutput {
    public init() {}

    public static var hasAccessibility: Bool { AXIsProcessTrusted() }

    /// Ouvre la demande système d'autorisation Accessibilité.
    public static func requestAccessibility() {
        let key = "AXTrustedCheckOptionPrompt" // valeur de kAXTrustedCheckOptionPrompt
        _ = AXIsProcessTrustedWithOptions([key: true] as CFDictionary)
    }

    public func deliver(_ text: String, autoPaste: Bool, restoreClipboard: Bool) -> Bool {
        let pb = NSPasteboard.general
        let saved = restoreClipboard ? Self.snapshot(pb) : nil

        pb.clearContents()
        pb.setString(text, forType: .string)

        guard autoPaste, Self.hasAccessibility else { return false }
        Self.pressCommandV()

        if let saved {
            // Laisse le temps à l'app cible de lire le presse-papiers avant de le restaurer.
            let count = pb.changeCount
            Task { @MainActor in
                try? await Task.sleep(for: .milliseconds(600))
                guard pb.changeCount == count else { return } // l'utilisateur a recopié entre-temps
                pb.clearContents()
                pb.writeObjects(saved)
            }
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

    private static func pressCommandV() {
        let source = CGEventSource(stateID: .combinedSessionState)
        let v: CGKeyCode = 9 // touche V (ANSI ; même code en AZERTY)
        let down = CGEvent(keyboardEventSource: source, virtualKey: v, keyDown: true)
        let up = CGEvent(keyboardEventSource: source, virtualKey: v, keyDown: false)
        down?.flags = .maskCommand
        up?.flags = .maskCommand
        down?.post(tap: .cghidEventTap)
        up?.post(tap: .cghidEventTap)
    }
}
