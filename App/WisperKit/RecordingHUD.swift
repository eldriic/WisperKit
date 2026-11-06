import AppKit
import SwiftUI
import WisperKitCore

/// Petit indicateur flottant au centre de l'écran actif pendant l'écoute
/// et la transcription. Ne prend jamais le focus : le texte part dans l'app d'origine.
@MainActor
final class RecordingHUD {
    static let size = CGSize(width: 160, height: 60)

    private let panel: NSPanel
    private var hideTask: Task<Void, Never>?

    init(dictation: DictationController) {
        panel = NSPanel(contentRect: CGRect(origin: .zero, size: Self.size),
                        styleMask: [.borderless, .nonactivatingPanel], backing: .buffered, defer: true)
        panel.isOpaque = false
        panel.backgroundColor = .clear
        panel.hasShadow = false
        panel.level = .statusBar
        panel.ignoresMouseEvents = true
        panel.isReleasedWhenClosed = false
        panel.hidesOnDeactivate = false
        panel.collectionBehavior = [.canJoinAllSpaces, .fullScreenAuxiliary, .stationary, .ignoresCycle]
        panel.contentView = NSHostingView(rootView: HUDView(dictation: dictation))
    }

    func show() {
        hideTask?.cancel()
        hideTask = nil
        if !panel.isVisible {
            center()
            panel.alphaValue = 0
            panel.orderFrontRegardless()
        }
        NSAnimationContext.runAnimationGroup { ctx in
            ctx.duration = 0.15
            panel.animator().alphaValue = 1
        }
    }

    func hide() {
        guard panel.isVisible, hideTask == nil else { return }
        NSAnimationContext.runAnimationGroup { ctx in
            ctx.duration = 0.2
            panel.animator().alphaValue = 0
        }
        hideTask = Task { [panel] in
            try? await Task.sleep(for: .milliseconds(220))
            guard !Task.isCancelled else { return }
            panel.orderOut(nil)
            self.hideTask = nil
        }
    }

    /// Centre sur l'écran où se trouve le pointeur (écran « actif »).
    private func center() {
        let mouse = NSEvent.mouseLocation
        let screen = NSScreen.screens.first { NSMouseInRect(mouse, $0.frame, false) } ?? NSScreen.main
        guard let frame = screen?.frame else { return }
        panel.setFrameOrigin(CGPoint(x: frame.midX - Self.size.width / 2, y: frame.midY - Self.size.height / 2))
    }
}

/// Pilule noire translucide avec des vagues bleu / rose qui suivent la voix.
struct HUDView: View {
    let dictation: DictationController
    @State private var meter = LevelSmoother()

    var body: some View {
        TimelineView(.animation) { timeline in
            Canvas { ctx, size in
                let t = timeline.date.timeIntervalSinceReferenceDate
                let level = meter.next(target: target)
                WaveRenderer.draw(in: &ctx, size: size, time: t, level: level)
            }
        }
        .frame(width: RecordingHUD.size.width, height: RecordingHUD.size.height)
        .background(Capsule().fill(Color(white: 0.04).opacity(0.88)))
        .overlay(Capsule().strokeBorder(.white.opacity(0.12), lineWidth: 1))
        .clipShape(Capsule())
    }

    /// Pendant la transcription, les vagues se calment et ondulent doucement.
    private var target: Double {
        switch dictation.state {
        case .recording: 0.15 + 0.85 * Double(dictation.audioLevel)
        case .transcribing: 0.18
        default: 0
        }
    }
}

/// Lissage du niveau entre deux images (réactif à l'attaque, plus lent à la retombée).
final class LevelSmoother {
    private var value = 0.0

    func next(target: Double) -> Double {
        value += (target - value) * (target > value ? 0.35 : 0.1)
        return value
    }
}

enum WaveRenderer {
    static let colors: [Color] = [
        Color(red: 0.24, green: 0.51, blue: 1.0).opacity(0.55),
        Color(red: 0.47, green: 0.35, blue: 1.0).opacity(0.45),
        Color(red: 1.0, green: 0.35, blue: 0.86).opacity(0.50),
        Color(red: 0.47, green: 0.86, blue: 1.0).opacity(0.35),
        Color.white.opacity(0.30),
    ]

    static func draw(in ctx: inout GraphicsContext, size: CGSize, time t: Double, level: Double) {
        let pad = 18.0
        let midY = size.height / 2
        let amplitude = (size.height / 2 - 4) * max(level, 0.06)
        ctx.blendMode = .plusLighter

        for (i, color) in colors.enumerated() {
            let freq = 1.2 + Double(i) * 0.45
            let phase = t * (1.6 + Double(i) * 0.5) + Double(i) * 1.7
            let a = amplitude * (1 - Double(i) * 0.13)
            var path = Path()
            let steps = 60
            for k in 0...steps {
                let u = Double(k) / Double(steps)
                let x = pad + u * (size.width - 2 * pad)
                let envelope = pow(sin(.pi * u), 1.5)
                let y = midY + sin(u * .pi * 2 * freq + phase) * a * envelope
                k == 0 ? path.move(to: CGPoint(x: x, y: y)) : path.addLine(to: CGPoint(x: x, y: y))
            }
            for k in stride(from: steps, through: 0, by: -1) {
                let u = Double(k) / Double(steps)
                let x = pad + u * (size.width - 2 * pad)
                let envelope = pow(sin(.pi * u), 1.5)
                let y = midY - sin(u * .pi * 2 * freq + phase + 0.8) * a * envelope * 0.8
                path.addLine(to: CGPoint(x: x, y: y))
            }
            path.closeSubpath()
            ctx.fill(path, with: .color(color))
        }
    }
}
