import SwiftUI

@main
struct WisperKitApp: App {
    static let settingsWindowID = "settings"

    @State private var app: AppModel

    init() {
        let model = AppModel()
        _app = State(initialValue: model)
        #if DEBUG
        Snapshots.runIfRequested(app: model)
        Snapshots.runSelfTestIfRequested(app: model)
        #endif
    }

    var body: some Scene {
        MenuBarExtra {
            MenuBarView().environment(app)
        } label: {
            MenuBarIcon(dictation: app.dictation.state, model: app.modelState)
        }
        .menuBarExtraStyle(.window)

        Window("Réglages WisperKit", id: Self.settingsWindowID) {
            SettingsView().environment(app)
        }
        .windowStyle(.hiddenTitleBar)
        .windowResizability(.contentSize)
        .defaultPosition(.center)
    }
}
