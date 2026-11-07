import Combine
import KeyboardShortcuts
import SwiftUI
import WisperKitCore

enum SettingsPage: String, CaseIterable, Identifiable {
    case general, model, keyboard, permissions
    var id: String { rawValue }

    @MainActor var title: String {
        switch self {
        case .general: L("General")
        case .model: L("Model")
        case .keyboard: L("Keyboard")
        case .permissions: L("Permissions")
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
            Text(L("Settings")).font(.system(size: 11)).foregroundStyle(Theme.secondary)
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
                SectionTitle(text: "WisperKit")
                SettingsCard {
                    SettingsRow(title: L("Interface language"),
                                description: L("Language of WisperKit's menus and settings.")) {
                        PillMenu(selection: $app.interfaceLanguage,
                                 options: AppLanguage.allCases.map { ($0, $0.nativeName) })
                    }
                }
            }
            VStack(alignment: .leading, spacing: 0) {
                SectionTitle(text: L("Dictation"))
                SettingsCard {
                    SettingsRow(title: L("Insert automatically"),
                                description: L("Writes the text into the frontmost app as soon as it is transcribed.")) {
                        BlueSwitch(isOn: $app.autoPaste)
                    }
                    SettingsRow(title: L("Never use the clipboard"),
                                description: L("Types the text key by key instead of pasting it. A bit slower; your clipboard is never touched.")) {
                        BlueSwitch(isOn: $app.neverUseClipboard)
                    }
                    if !app.neverUseClipboard {
                        SettingsRow(title: L("Restore clipboard"),
                                    description: L("Puts back what you had copied before dictating.")) {
                            BlueSwitch(isOn: $app.restoreClipboard)
                        }
                    }
                    SettingsRow(title: L("Dictation language"), description: L("Auto-detect works for most people; forcing a language avoids detection mistakes.")) {
                        PillMenu(selection: $app.language, options: AppModel.languages.map { ($0.id, L($0.label)) })
                    }
                    SettingsRow(title: L("Translate to English"),
                                description: L("Speak any language, get English text. Works best with large-v3.")) {
                        BlueSwitch(isOn: $app.translateToEnglish)
                    }
                    SettingsRow(title: L("Sounds"), description: L("A chime when listening starts and ends.")) {
                        BlueSwitch(isOn: $app.sounds)
                    }
                    SettingsRow(title: L("On-screen indicator"),
                                description: L("Small waves in the middle of the screen while listening and transcribing.")) {
                        BlueSwitch(isOn: $app.showHUD)
                    }
                }
            }
            VStack(alignment: .leading, spacing: 0) {
                SectionTitle(text: L("Startup"))
                SettingsCard {
                    SettingsRow(title: L("Open at login"),
                                description: app.launchError ?? L("WisperKit starts with your macOS session.")) {
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
                SectionTitle(text: L("Installed and available models"))
                SettingsCard {
                    ForEach(WhisperModel.all) { model in
                        SettingsRow(title: model.title, description: "\(L(model.detail)) · \(L(model.size))",
                                    badge: model.id == app.modelID ? L("active") : nil) {
                            trailing(for: model)
                        }
                    }
                }
            }
            VStack(alignment: .leading, spacing: 0) {
                SectionTitle(text: L("Storage"))
                SettingsCard {
                    SettingsRow(title: L("Models folder"),
                                description: "~/Library/Application Support/WisperKit · \(usage)") {
                        Button(L("Show")) {
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
                Button(L("Retry")) { Task { await app.loadActiveModel() } }.buttonStyle(PillButtonStyle())
            case .missing:
                Button(L("Download")) { Task { await app.loadActiveModel() } }.buttonStyle(PillButtonStyle())
            }
        } else if let f = app.downloads[model.id] {
            progress(f)
        } else if app.installed.contains(model.id) {
            HStack(spacing: 8) {
                Button { app.delete(model) } label: { Image(systemName: "trash") }
                    .buttonStyle(PillButtonStyle(foreground: Theme.secondary)).help(L("Delete model"))
                Button(L("Use")) { app.use(model) }.buttonStyle(PillButtonStyle())
            }
        } else {
            Button { app.download(model) } label: { Label(L("Download"), systemImage: "arrow.down") }
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
            SectionTitle(text: L("Shortcut"))
            SettingsCard {
                SettingsRow(title: L("Start / stop dictation"), description: L("Works from any app.")) {
                    KeyboardShortcuts.Recorder(for: .toggleDictation).frame(width: 150)
                }
                SettingsRow(title: L("Mode"), description: app.mode.detail) {
                    PillMenu(selection: $app.mode, options: TriggerMode.allCases.map { ($0, $0.label) })
                }
                SettingsRow(title: L("Cancel with Esc"), description: L("Pressing Esc while listening discards the recording.")) {
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
            if app.isFirstLaunch { WelcomeCard().padding(.bottom, 20) }
            SectionTitle(text: "macOS")
            SettingsCard {
                SettingsRow(title: L("Microphone"), description: L("To record your voice. Nothing leaves your Mac.")) {
                    if mic {
                        granted
                    } else if micDenied {
                        openButton { AppModel.openPrivacyPane("Privacy_Microphone") }
                    } else {
                        Button(L("Allow")) { Task { _ = await app.requestMicrophone(); refresh() } }
                            .buttonStyle(PillButtonStyle())
                    }
                }
                SettingsRow(title: L("Accessibility"),
                            description: L("To write the text at your cursor. Without it, the text stays in the clipboard.")) {
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
            Text(L("If inserting stops working after an update: System Settings > Accessibility, remove WisperKit with − then add it back with +."))
                .font(.system(size: 11.5)).foregroundStyle(Theme.secondary).padding(.top, 10)
        }
        .onAppear(perform: refresh)
        .onReceive(timer) { _ in refresh() }
    }

    private var granted: some View {
        Label(L("Allowed"), systemImage: "checkmark.circle")
            .font(.system(size: 12)).foregroundStyle(Theme.green)
            .padding(.horizontal, 12).padding(.vertical, 6)
            .background(Capsule().fill(Theme.pill))
    }

    private func openButton(_ action: @escaping () -> Void) -> some View {
        Button(L("Open Settings"), action: action)
            .buttonStyle(PillButtonStyle(foreground: Theme.danger, fill: Theme.dangerFill))
    }

    private func refresh() {
        accessibility = TextInserter.hasAccessibility
        mic = app.micAuthorized
        micDenied = app.micDenied
    }
}

// MARK: - Accueil (premier lancement)

struct WelcomeCard: View {
    @Environment(AppModel.self) private var app

    var body: some View {
        VStack(alignment: .leading, spacing: 12) {
            Text(L("Welcome to WisperKit")).font(.system(size: 15, weight: .medium)).foregroundStyle(Theme.text)
            Text(L("A few steps and you can dictate anywhere. Everything stays on your Mac."))
                .font(.system(size: 12)).foregroundStyle(Theme.secondary)
            step(1, L("Allow the microphone below."))
            step(2, L("Allow Accessibility so the text is written at your cursor."))
            step(3, modelStep)
            step(4, L("Put your cursor in any app, press ⌃⌥D, speak, then press it again."))
        }
        .padding(16)
        .frame(maxWidth: .infinity, alignment: .leading)
        .background(RoundedRectangle(cornerRadius: 14).fill(Theme.blue.opacity(0.10)))
        .overlay(RoundedRectangle(cornerRadius: 14).strokeBorder(Theme.blue.opacity(0.35), lineWidth: 1))
    }

    private var modelStep: String {
        switch app.modelState {
        case .downloading(let f): L("The transcription model is downloading (%d%%, about 1.6 GB, one time only).", Int(f * 100))
        case .loading: L("The model is loading…")
        case .ready: L("The model is ready.")
        case .failed(let message): L("Model download failed: %@ (retry from the Model tab).", L(message))
        case .missing: L("The transcription model will be downloaded (about 1.6 GB, one time only).")
        }
    }

    private func step(_ n: Int, _ text: String) -> some View {
        HStack(alignment: .top, spacing: 10) {
            Text("\(n)").font(.system(size: 11, weight: .semibold)).foregroundStyle(Theme.text)
                .frame(width: 20, height: 20).background(Circle().fill(Theme.blue.opacity(0.5)))
            Text(text).font(.system(size: 12.5)).foregroundStyle(Theme.text).fixedSize(horizontal: false, vertical: true)
        }
    }
}
