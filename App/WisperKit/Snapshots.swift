#if DEBUG
import AppKit
import SwiftUI

/// Export PNG des écrans sans autorisation d'enregistrement d'écran :
///   WisperKit.app/Contents/MacOS/WisperKit --snapshots <dossier>
@MainActor
enum Snapshots {
    static func runIfRequested(app: AppModel) {
        let args = CommandLine.arguments
        guard let i = args.firstIndex(of: "--snapshots"), i + 1 < args.count else { return }
        let dir = URL(filePath: args[i + 1], directoryHint: .isDirectory)
        try? FileManager.default.createDirectory(at: dir, withIntermediateDirectories: true)

        render(MenuBarView().environment(app), size: CGSize(width: 280, height: 260), to: dir.appending(path: "menu.png"))
        for page in SettingsPage.allCases {
            render(SettingsView(initialPage: page).environment(app), size: CGSize(width: 760, height: 540),
                   to: dir.appending(path: "settings-\(page.rawValue).png"))
        }
        exit(0)
    }

    /// Chaîne réelle micro → modèle actif → presse-papiers, sans raccourci :
    ///   WisperKit --selftest <secondes>   (parler, ou `say`, pendant l'écoute)
    static func runSelfTestIfRequested(app: AppModel) {
        let args = CommandLine.arguments
        guard let i = args.firstIndex(of: "--selftest") else { return }
        let seconds = i + 1 < args.count ? Double(args[i + 1]) ?? 5 : 5
        Task { @MainActor in
            await app.loadActiveModel()
            guard app.modelState == .ready else { print("SELFTEST modèle: \(app.modelState)"); exit(1) }
            app.sounds = false
            app.autoPaste = false
            print("SELFTEST écoute \(seconds) s"); fflush(stdout)
            app.dictation.start()
            try? await Task.sleep(for: .seconds(seconds))
            let clock = ContinuousClock(), t0 = clock.now
            app.dictation.stop()
            await app.dictation.waitForCompletion()
            print("SELFTEST état: \(app.dictation.state) · transcription \(clock.now - t0)")
            print("SELFTEST texte: \(app.dictation.lastText ?? "<vide>")")
            print("SELFTEST presse-papiers: \(NSPasteboard.general.string(forType: .string) ?? "<vide>")")
            exit(0)
        }
    }

    private static func render(_ view: some View, size: CGSize, to url: URL) {
        let host = NSHostingView(rootView: view.frame(width: size.width, height: size.height, alignment: .top))
        host.frame = CGRect(origin: .zero, size: size)
        host.appearance = NSAppearance(named: .darkAqua)
        let window = NSWindow(contentRect: host.frame, styleMask: [.borderless], backing: .buffered, defer: false)
        window.contentView = host
        host.layoutSubtreeIfNeeded()
        RunLoop.main.run(until: Date().addingTimeInterval(0.3))
        guard let rep = host.bitmapImageRepForCachingDisplay(in: host.bounds) else { return }
        host.cacheDisplay(in: host.bounds, to: rep)
        try? rep.representation(using: .png, properties: [:])?.write(to: url)
    }
}
#endif
