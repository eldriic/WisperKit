import AppKit
import SwiftUI
import WisperKitCore

/// Petit indicateur flottant au centre de l'écran actif pendant l'écoute
/// et la transcription. Ne prend jamais le focus : le texte part dans l'app d'origine.
@MainActor
final class RecordingHUD {
    static let size = CGSize(width: 200, height: 64)

    private let panel: NSPanel
    private let state = HUDState()
    private var hideTask: Task<Void, Never>?

    init(dictation: DictationController) {
        panel = NSPanel(contentRect: CGRect(origin: .zero, size: Self.size),
                        styleMask: [.borderless, .nonactivatingPanel], backing: .buffered, defer: true)
        panel.isOpaque = false
        panel.backgroundColor = .clear
        panel.hasShadow = true
        panel.level = .statusBar
        panel.ignoresMouseEvents = true
        panel.isReleasedWhenClosed = false
        panel.hidesOnDeactivate = false
        panel.collectionBehavior = [.canJoinAllSpaces, .fullScreenAuxiliary, .stationary, .ignoresCycle]
        panel.contentView = NSHostingView(rootView: HUDView(dictation: dictation, state: state))
    }

    func show() {
        state.message = nil
        state.startedAt = .now
        reveal()
    }

    /// Affiche un court message (ex. texte seulement copié) puis masque.
    func flash(_ message: String) {
        state.message = message
        reveal()
        hideTask = Task {
            try? await Task.sleep(for: .milliseconds(1600))
            guard !Task.isCancelled else { return }
            self.hideTask = nil
            self.hide()
        }
    }

    private func reveal() {
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

@MainActor
@Observable
final class HUDState {
    var message: String?
    var startedAt = Date.now
}

/// Pilule animée (lit l'état et le niveau micro à chaque image).
struct HUDView: View {
    let dictation: DictationController
    let state: HUDState
    @State private var meter = LevelSmoother()

    var body: some View {
        TimelineView(.animation) { timeline in
            let transcribing = dictation.state == .transcribing
            let target = dictation.state == .recording ? Double(dictation.audioLevel) : 0
            HUDContent(time: timeline.date.timeIntervalSinceReferenceDate,
                       level: meter.next(target: target),
                       elapsed: timeline.date.timeIntervalSince(state.startedAt),
                       transcribing: transcribing,
                       message: state.message)
        }
    }
}

/// Rendu pur de la pilule (partagé avec les captures Debug).
struct HUDContent: View {
    let time: Double
    let level: Double
    let elapsed: Double
    let transcribing: Bool
    var message: String?

    var body: some View {
        ZStack {
            if let message {
                Label(message, systemImage: "doc.on.clipboard")
                    .font(.system(size: 12, weight: .medium))
                    .foregroundStyle(.white.opacity(0.9))
            } else {
                HStack(spacing: 0) {
                    Canvas { ctx, size in
                        WaveRenderer.draw(in: &ctx, size: size, time: time,
                                          level: transcribing ? 0.12 : max(level, 0.08),
                                          speed: transcribing ? 0.45 : 1)
                    }
                    status.frame(width: 56)
                }
            }
        }
        .frame(width: RecordingHUD.size.width, height: RecordingHUD.size.height)
        .background {
            ZStack {
                VisualEffectBlur()
                Color.black.opacity(0.45)
                if message == nil {
                    Canvas { ctx, size in
                        WaveRenderer.drawHalo(in: &ctx, size: size, level: transcribing ? 0.1 : level)
                    }
                }
            }
        }
        .overlay(Capsule().strokeBorder(.white.opacity(0.10), lineWidth: 1))
        .clipShape(Capsule())
    }

    /// Point rouge qui pulse + minuteur pendant l'écoute ; point bleu + « ··· » en transcription.
    private var status: some View {
        HStack(spacing: 6) {
            Circle()
                .fill(transcribing ? Color(red: 0.55, green: 0.67, blue: 1) : Color(nsColor: .systemRed))
                .opacity(transcribing ? 0.9 : 0.6 + 0.4 * abs(sin(time * 3)))
                .frame(width: 6, height: 6)
            Text(transcribing ? dots : Self.format(elapsed))
                .font(.system(size: 12, weight: .medium).monospacedDigit())
                .foregroundStyle(.white.opacity(0.85))
                .frame(width: 30, alignment: .leading)
        }
        .padding(.trailing, 10)
    }

    private var dots: String {
        String(repeating: "·", count: Int(time * 3) % 3 + 1)
    }

    static func format(_ seconds: Double) -> String {
        let s = max(0, Int(seconds))
        return "\(s / 60):\(String(format: "%02d", s % 60))"
    }
}

/// Flou natif de ce qu'il y a derrière la fenêtre (comme les panneaux macOS).
struct VisualEffectBlur: NSViewRepresentable {
    func makeNSView(context: Context) -> NSVisualEffectView {
        let view = NSVisualEffectView()
        view.material = .hudWindow
        view.blendingMode = .behindWindow
        view.state = .active
        view.appearance = NSAppearance(named: .darkAqua)
        return view
    }

    func updateNSView(_ nsView: NSVisualEffectView, context: Context) {}
}

/// Lissage du niveau entre deux images (réactif à l'attaque, plus lent à la retombée).
final class LevelSmoother {
    private var value = 0.0

    func next(target: Double) -> Double {
        value += (target - value) * (target > value ? 0.3 : 0.08)
        return value
    }
}

enum WaveRenderer {
    /// Bleu, violet, rose, cyan.
    static let palette: [(Double, Double, Double)] = [
        (0.25, 0.55, 1.0), (0.51, 0.35, 1.0), (1.0, 0.31, 0.82), (0.35, 0.82, 1.0),
    ]

    /// Halo violet qui s'éclaire avec la voix.
    static func drawHalo(in ctx: inout GraphicsContext, size: CGSize, level: Double) {
        let center = CGPoint(x: size.width * 0.42, y: size.height / 2)
        let radius = size.width * 0.55
        let color = Color(red: 0.47, green: 0.43, blue: 1.0)
        ctx.fill(Path(CGRect(origin: .zero, size: size)), with: .radialGradient(
            Gradient(colors: [color.opacity(0.16 + 0.36 * level), color.opacity(0)]),
            center: center, startRadius: 2, endRadius: radius))
    }

    /// Vagues superposées, estompées aux deux bouts, en mélange additif.
    static func draw(in ctx: inout GraphicsContext, size: CGSize, time t: Double, level: Double, speed: Double = 1) {
        let left = 16.0, right = size.width - 4
        let midY = size.height / 2
        let amplitude = (size.height / 2 - 9) * min(level, 1)
        var layer = ctx
        layer.blendMode = .plusLighter

        for (i, (r, g, b)) in palette.enumerated() {
            let a = amplitude * (1 - Double(i) * 0.12)
            let freq = 1.1 + Double(i) * 0.35
            let phase = t * (1.3 + Double(i) * 0.4) * speed + Double(i) * 2
            var path = Path()
            let steps = 70
            for k in 0...steps {
                let u = Double(k) / Double(steps)
                let y = midY + sin(u * .pi * 2 * freq + phase) * a * pow(sin(.pi * u), 1.4)
                let p = CGPoint(x: left + u * (right - left), y: y)
                k == 0 ? path.move(to: p) : path.addLine(to: p)
            }
            for k in stride(from: steps, through: 0, by: -1) {
                let u = Double(k) / Double(steps)
                let y = midY - sin(u * .pi * 2 * freq + phase + 1) * a * pow(sin(.pi * u), 1.4) * 0.7
                path.addLine(to: CGPoint(x: left + u * (right - left), y: y))
            }
            path.closeSubpath()
            let color = Color(red: r, green: g, blue: b)
            layer.fill(path, with: .linearGradient(
                Gradient(stops: [.init(color: color.opacity(0), location: 0),
                                 .init(color: color.opacity(0.62), location: 0.5),
                                 .init(color: color.opacity(0), location: 1)]),
                startPoint: CGPoint(x: left, y: midY), endPoint: CGPoint(x: right, y: midY)))
        }
    }
}
