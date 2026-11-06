import Combine
import KeyboardShortcuts
import SwiftUI
import WisperKitCore

enum SettingsPage: String, CaseIterable, Identifiable {
    case general, model, keyboard, permissions
    var id: String { rawValue }

    var title: String {
        switch self {
        case .general: "Général"
        case .model: "Modèle"
        case .keyboard: "Clavier"
        case .permissions: "Autorisations"
        }
    }

    var icon: String {
        switch self {
        case .general: "gearshape"
        case .model: "cpu"
        case .keyboard: "keyboard"
        case .permissions: "checkmark.shield"
        }
    }
}

struct SettingsView: View {
    @Environment(AppModel.self) private var app
    @State private var page: SettingsPage

    init(initialPage: SettingsPage = .general) {
        _page = State(initialValue: initialPage)
    }

    var body: some View {
        HStack(spacing: 0) {
            sidebar
            Rectangle().fill(Color(white: 0.11)).frame(width: 1)
            ScrollView {
                VStack(alignment: .leading, spacing: 0) {
                    Text(page.title).font(.system(size: 22)).foregroundStyle(Theme.text).padding(.bottom, 20)
                    switch page {
                    case .general: GeneralPage()
                    case .model: ModelPage()
                    case .keyboard: KeyboardPage()
                    case .permissions: PermissionsPage()
                    }
                }
                .padding(.horizontal, 28).padding(.top, 34).padding(.bottom, 28)
                .frame(maxWidth: .infinity, alignment: .leading)
            }
            .scrollIndicators(.never)
        }
        .frame(width: 760, height: 540)
        .background(Theme.background)
        .preferredColorScheme(.dark)
    }

    private var sidebar: some View {
        VStack(alignment: .leading, spacing: 2) {
            Text("Réglages").font(.system(size: 11)).foregroundStyle(Theme.secondary)
                .padding(.horizontal, 10).padding(.bottom, 6)
            ForEach(SettingsPage.allCases) { item in
                Button { page = item } label: {
                    HStack(spacing: 10) {
                        Image(systemName: item.icon).frame(width: 18)
                        Text(item.title)
                        Spacer()
                    }
                    .font(.system(size: 13))
                    .foregroundStyle(Theme.text)
                    .padding(.horizontal, 10).padding(.vertical, 7)
                    .background(RoundedRectangle(cornerRadius: 8).fill(page == item ? Theme.selection : .clear))
                    .contentShape(Rectangle())
                }
                .buttonStyle(.plain)
            }
            Spacer()
            Text("WisperKit \(Bundle.main.object(forInfoDictionaryKey: "CFBundleShortVersionString") as? String ?? "")")
                .font(.system(size: 11)).foregroundStyle(Theme.secondary).padding(.horizontal, 10)
        }
        .padding(.horizontal, 8).padding(.top, 44).padding(.bottom, 14)
        .frame(width: 190)
    }
}

// MARK: - Général

struct GeneralPage: View {
    @Environment(AppModel.self) private var app

    var body: some View {
        @Bindable var app = app
        VStack(alignment: .leading, spacing: 18) {
            VStack(alignment: .leading, spacing: 0) {
                SectionTitle(text: "Dictée")
                SettingsCard {
                    SettingsRow(title: "Collage automatique",
                                description: "Tape le texte dans l'app au premier plan dès la fin de la transcription.") {
                        BlueSwitch(isOn: $app.autoPaste)
                    }
                    SettingsRow(title: "Restaurer le presse-papiers",
                                description: "Remet ce que tu avais copié avant la dictée.") {
                        BlueSwitch(isOn: $app.restoreClipboard)
                    }
                    SettingsRow(title: "Langue", description: "Forcer la langue évite les erreurs de détection.") {
                        PillMenu(selection: $app.language, options: AppModel.languages.map { ($0.id, $0.label) })
                    }
                    SettingsRow(title: "Sons", description: "Signal au début et à la fin de l'écoute.") {
                        BlueSwitch(isOn: $app.sounds)
                    }
                    SettingsRow(title: "Indicateur à l'écran",
                                description: "Petites vagues au centre de l'écran pendant l'écoute et la transcription.") {
                        BlueSwitch(isOn: $app.showHUD)
                    }
                }
            }
            VStack(alignment: .leading, spacing: 0) {
                SectionTitle(text: "Démarrage")
                SettingsCard {
                    SettingsRow(title: "Ouvrir à la connexion",
                                description: app.launchError ?? "WisperKit se lance avec ta session macOS.") {
                        BlueSwitch(isOn: $app.launchAtLogin)
                    }
                }
            }
        }
    }
}

// MARK: - Modèle

struct ModelPage: View {
    @Environment(AppModel.self) private var app

    var body: some View {
        VStack(alignment: .leading, spacing: 18) {
            VStack(alignment: .leading, spacing: 0) {
                SectionTitle(text: "Modèles installés et disponibles")
                SettingsCard {
                    ForEach(WhisperModel.all) { model in
                        SettingsRow(title: model.title, description: "\(model.detail) · \(model.size)",
                                    badge: model.id == app.modelID ? "actif" : nil) {
                            trailing(for: model)
                        }
                    }
                }
            }
            VStack(alignment: .leading, spacing: 0) {
                SectionTitle(text: "Stockage")
                SettingsCard {
                    SettingsRow(title: "Dossier des modèles",
                                description: "~/Library/Application Support/WisperKit · \(usage)") {
                        Button("Afficher") {
                            try? FileManager.default.createDirectory(at: ModelStore.baseURL, withIntermediateDirectories: true)
                            NSWorkspace.shared.activateFileViewerSelecting([ModelStore.baseURL])
                        }
                        .buttonStyle(PillButtonStyle())
                    }
                }
            }
        }
    }

    private var usage: String {
        _ = app.installed
        return ByteCountFormatter.string(fromByteCount: ModelStore.diskUsage(), countStyle: .file)
    }

    @ViewBuilder private func trailing(for model: WhisperModel) -> some View {
        let isActive = model.id == app.modelID
        if isActive {
            switch app.modelState {
            case .downloading(let f): progress(f)
            case .loading: ProgressView().controlSize(.small)
            case .ready: Image(systemName: "checkmark").foregroundStyle(Theme.text)
            case .failed:
                Button("Réessayer") { Task { await app.loadActiveModel() } }.buttonStyle(PillButtonStyle())
            case .missing:
                Button("Télécharger") { Task { await app.loadActiveModel() } }.buttonStyle(PillButtonStyle())
            }
        } else if let f = app.downloads[model.id] {
            progress(f)
        } else if app.installed.contains(model.id) {
            HStack(spacing: 8) {
                Button { app.delete(model) } label: { Image(systemName: "trash") }
                    .buttonStyle(PillButtonStyle(foreground: Theme.secondary)).help("Supprimer le modèle")
                Button("Utiliser") { app.use(model) }.buttonStyle(PillButtonStyle())
            }
        } else {
            Button { app.download(model) } label: { Label("Télécharger", systemImage: "arrow.down") }
                .buttonStyle(PillButtonStyle())
        }
    }

    private func progress(_ fraction: Double) -> some View {
        HStack(spacing: 8) {
            ProgressView(value: fraction).frame(width: 90).tint(Theme.blue)
            Text("\(Int(fraction * 100)) %").font(.system(size: 11).monospacedDigit()).foregroundStyle(Theme.secondary)
                .frame(width: 34, alignment: .trailing)
        }
    }
}

// MARK: - Clavier

struct KeyboardPage: View {
    @Environment(AppModel.self) private var app

    var body: some View {
        @Bindable var app = app
        VStack(alignment: .leading, spacing: 0) {
            SectionTitle(text: "Raccourci")
            SettingsCard {
                SettingsRow(title: "Lancer / arrêter la dictée", description: "Fonctionne depuis n'importe quelle app.") {
                    KeyboardShortcuts.Recorder(for: .toggleDictation).frame(width: 150)
                }
                SettingsRow(title: "Mode", description: app.mode.detail) {
                    PillMenu(selection: $app.mode, options: TriggerMode.allCases.map { ($0, $0.label) })
                }
                SettingsRow(title: "Annuler avec Échap", description: "Échap pendant l'écoute jette l'enregistrement.") {
                    BlueSwitch(isOn: $app.escapeCancels)
                }
            }
        }
    }
}

// MARK: - Autorisations

struct PermissionsPage: View {
    @Environment(AppModel.self) private var app
    @State private var accessibility = TextInserter.hasAccessibility
    @State private var mic = false
    @State private var micDenied = false
    private let timer = Timer.publish(every: 1, on: .main, in: .common).autoconnect()

    var body: some View {
        VStack(alignment: .leading, spacing: 0) {
            SectionTitle(text: "macOS")
            SettingsCard {
                SettingsRow(title: "Microphone", description: "Pour enregistrer ta voix. Rien ne quitte le Mac.") {
                    if mic {
                        granted
                    } else if micDenied {
                        openButton { AppModel.openPrivacyPane("Privacy_Microphone") }
                    } else {
                        Button("Autoriser") { Task { _ = await app.requestMicrophone(); refresh() } }
                            .buttonStyle(PillButtonStyle())
                    }
                }
                SettingsRow(title: "Accessibilité",
                            description: "Pour coller le texte au curseur. Sans elle, le texte reste dans le presse-papiers.") {
                    if accessibility {
                        granted
                    } else {
                        openButton {
                            TextInserter.requestAccessibility()
                            AppModel.openPrivacyPane("Privacy_Accessibility")
                        }
                    }
                }
            }
            Text("Après un nouveau build non signé, macOS peut demander de réactiver l'Accessibilité : retire WisperKit de la liste puis rajoute-le.")
                .font(.system(size: 11.5)).foregroundStyle(Theme.secondary).padding(.top, 10)
        }
        .onAppear(perform: refresh)
        .onReceive(timer) { _ in refresh() }
    }

    private var granted: some View {
        Label("Autorisé", systemImage: "checkmark.circle")
            .font(.system(size: 12)).foregroundStyle(Theme.green)
            .padding(.horizontal, 12).padding(.vertical, 6)
            .background(Capsule().fill(Theme.pill))
    }

    private func openButton(_ action: @escaping () -> Void) -> some View {
        Button("Ouvrir les Réglages", action: action)
            .buttonStyle(PillButtonStyle(foreground: Theme.danger, fill: Theme.dangerFill))
    }

    private func refresh() {
        accessibility = TextInserter.hasAccessibility
        mic = app.micAuthorized
        micDenied = app.micDenied
    }
}
