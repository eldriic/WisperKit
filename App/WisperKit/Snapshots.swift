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

        app.forceReadyForSnapshots()
        render(MenuBarView().environment(app), size: CGSize(width: 280, height: 168), to: dir.appending(path: "menu.png"))
        for page in SettingsPage.allCases {
            render(SettingsView(initialPage: page).environment(app), size: CGSize(width: 760, height: 540),
                   to: dir.appending(path: "settings-\(page.rawValue).png"))
        }
        let frames: [(String, Double, Double, Bool)] = [
            ("hud-voix", 0.85, 1.3, false), ("hud-silence", 0.0, 2.1, false), ("hud-transcription", 0, 0.7, true),
        ]
        for (name, level, t, transcribing) in frames {
            render(HUDContent(time: t, level: level, elapsed: 4, transcribing: transcribing)
                    .padding(.horizontal, 60).padding(.vertical, 38)
                    .background(LinearGradient(colors: [Color(red: 0.10, green: 0.12, blue: 0.24),
                                                        Color(red: 0.22, green: 0.10, blue: 0.28)],
                                               startPoint: .topLeading, endPoint: .bottomTrailing)),
                   size: CGSize(width: 320, height: 140), to: dir.appending(path: "\(name).png"))
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
            // Sans passer par les réglages persistés (même identifiant que l'app installée).
            app.transientMute = true
            // --paste <bundle id> : colle dans cette app seulement (remise au premier plan avant).
            let target = args.firstIndex(of: "--paste").flatMap { $0 + 1 < args.count ? args[$0 + 1] : nil }
            app.dictation.autoPaste = false
            print("SELFTEST écoute \(seconds) s"); fflush(stdout)
            app.dictation.start()
            try? await Task.sleep(for: .seconds(seconds))
            if let target, let running = NSRunningApplication.runningApplications(withBundleIdentifier: target).first {
                running.activate()
                try? await Task.sleep(for: .milliseconds(500))
                let front = NSWorkspace.shared.frontmostApplication?.bundleIdentifier
                app.dictation.autoPaste = front == target
                print("SELFTEST cible=\(target) premier plan=\(front ?? "?") collage=\(app.dictation.autoPaste)")
            }
            let clock = ContinuousClock(), t0 = clock.now
            app.dictation.stop()
            await app.dictation.waitForCompletion()
            print("SELFTEST état: \(app.dictation.state) · transcription \(clock.now - t0)")
            print("SELFTEST texte: \(app.dictation.lastText ?? "<vide>")")
            print("SELFTEST presse-papiers: \(NSPasteboard.general.string(forType: .string) ?? "<vide>")")
            try? await Task.sleep(for: .milliseconds(1500)) // laisse partir ⌘V et la restauration
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
