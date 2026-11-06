import AppKit
import KeyboardShortcuts
import SwiftUI
import WisperKitCore

/// Contenu du popover de la barre de menus.
struct MenuBarView: View {
    @Environment(AppModel.self) private var app
    @Environment(\.openWindow) private var openWindow
    @Environment(\.dismiss) private var dismiss

    var body: some View {
        VStack(alignment: .leading, spacing: 0) {
            header
            separator
            MenuRow(icon: app.dictation.isRecording ? "stop.circle" : "mic",
                    title: app.dictation.isRecording ? "Arrêter la dictée" : "Démarrer la dictée",
                    shortcut: KeyboardShortcuts.getShortcut(for: .toggleDictation)?.description,
                    enabled: app.canDictate || app.dictation.isRecording) {
                dismiss()
                // Laisse le popover se fermer pour que le texte arrive dans l'app d'origine.
                Task { try? await Task.sleep(for: .milliseconds(150)); app.toggleDictation() }
            }
            if let last = app.dictation.lastText {
                MenuRow(icon: "clock.arrow.circlepath", title: "Dernière dictée", subtitle: last,
                        trailingIcon: "doc.on.doc") {
                    NSPasteboard.general.clearContents()
                    NSPasteboard.general.setString(last, forType: .string)
                    dismiss()
                }
                .help("Copier la dernière dictée")
            }
            separator
            MenuRow(icon: "gearshape", title: "Réglages…", shortcut: "⌘,") {
                dismiss()
                openWindow(id: WisperKitApp.settingsWindowID)
                NSApp.activate()
            }
            .keyboardShortcut(",", modifiers: .command)
            MenuRow(icon: "rectangle.portrait.and.arrow.right", title: "Quitter", shortcut: "⌘Q") {
                NSApp.terminate(nil)
            }
            .keyboardShortcut("q", modifiers: .command)
        }
        .padding(6)
        .frame(width: 280)
        .background(Theme.popover)
        .preferredColorScheme(.dark)
    }

    private var header: some View {
        HStack(spacing: 10) {
            Image(nsImage: NSApp.applicationIconImage)
                .resizable()
                .frame(width: 34, height: 34)
            VStack(alignment: .leading, spacing: 1) {
                Text("WisperKit").font(.system(size: 13)).foregroundStyle(Theme.text)
                Text(app.statusLine).font(.system(size: 11)).foregroundStyle(statusColor).lineLimit(2)
            }
            Spacer()
            if case .downloading(let f) = app.modelState {
                ProgressView(value: f).frame(width: 50).tint(Theme.blue)
            }
        }
        .padding(.horizontal, 10).padding(.vertical, 8)
    }

    private var statusColor: Color {
        if case .failed = app.dictation.state { return Theme.danger }
        if case .failed = app.modelState { return Theme.danger }
        return app.dictation.isRecording ? Color(nsColor: .systemRed) : Theme.secondary
    }

    private var separator: some View {
        Rectangle().fill(Theme.popoverStroke).frame(height: 1).padding(.horizontal, 10).padding(.vertical, 4)
    }
}

/// Ligne de menu avec survol en pastille.
struct MenuRow: View {
    let icon: String
    let title: String
    var subtitle: String?
    var shortcut: String?
    var trailingIcon: String?
    var enabled = true
    let action: () -> Void
    @State private var hover = false

    var body: some View {
        Button(action: action) {
            HStack(alignment: subtitle == nil ? .center : .top, spacing: 10) {
                Image(systemName: icon).frame(width: 18).padding(.top, subtitle == nil ? 0 : 1)
                VStack(alignment: .leading, spacing: 1) {
                    Text(title)
                    if let subtitle {
                        Text(subtitle).font(.system(size: 11)).foregroundStyle(Theme.secondary)
                            .lineLimit(1).truncationMode(.tail)
                    }
                }
                Spacer(minLength: 8)
                if let shortcut { Text(shortcut).font(.system(size: 12)).foregroundStyle(Theme.secondary) }
                if let trailingIcon { Image(systemName: trailingIcon).foregroundStyle(Theme.secondary) }
            }
            .font(.system(size: 13))
            .foregroundStyle(enabled ? Theme.text : Theme.secondary)
            .padding(.horizontal, 10).padding(.vertical, 7)
            .background(RoundedRectangle(cornerRadius: 8).fill(hover && enabled ? Theme.popoverStroke : .clear))
            .contentShape(Rectangle())
        }
        .buttonStyle(.plain)
        .disabled(!enabled)
        .onHover { hover = $0 }
    }
}

/// Icône de la barre de menus selon l'état.
struct MenuBarIcon: View {
    let dictation: DictationController.State
    let model: ModelState

    var body: some View {
        Image(nsImage: image)
    }

    private var image: NSImage {
        switch dictation {
        case .recording: return Self.symbol("mic.fill", color: .systemRed)
        case .transcribing: return Self.symbol("waveform", color: NSColor(Theme.blue))
        case .failed: return Self.symbol("mic.slash", color: nil)
        case .idle: break
        }
        switch model {
        case .downloading, .loading: return Self.symbol("arrow.down.circle", color: nil)
        case .failed, .missing: return Self.symbol("mic.slash", color: nil)
        case .ready: return Self.symbol("mic", color: nil)
        }
    }

    /// Symbole SF ; en couleur (non template) pour les états actifs, sinon template monochrome.
    static func symbol(_ name: String, color: NSColor?) -> NSImage {
        var config = NSImage.SymbolConfiguration(pointSize: 14, weight: .regular)
        if let color { config = config.applying(.init(paletteColors: [color])) }
        let image = NSImage(systemSymbolName: name, accessibilityDescription: "WisperKit")?
            .withSymbolConfiguration(config) ?? NSImage()
        image.isTemplate = color == nil
        return image
    }
}
