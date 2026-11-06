import AVFoundation

/// Source audio abstraite (permet de tester le contrôleur sans micro).
@MainActor
public protocol AudioRecording: AnyObject {
    func start() throws
    /// Arrête l'enregistrement et renvoie les échantillons 16 kHz mono.
    func stop() -> [Float]
    /// Niveau sonore récent, normalisé 0…1 (pour l'indicateur à l'écran).
    var level: Float { get }
}

public extension AudioRecording {
    var level: Float { 0 }
}

public enum AudioRecorderError: LocalizedError {
    case noInputDevice
    case converterUnavailable

    public var errorDescription: String? {
        switch self {
        case .noInputDevice: "Aucun micro disponible."
        case .converterUnavailable: "Format audio du micro non pris en charge."
        }
    }
}

/// Enregistre le micro par défaut via AVAudioEngine, converti à la volée en
/// Float32 16 kHz mono (format attendu par Whisper), entièrement en mémoire.
@MainActor
public final class AudioRecorder: AudioRecording {
    public static let sampleRate: Double = 16_000

    private var engine: AVAudioEngine?
    private let buffer = SampleBuffer()

    public init() {}

    public func start() throws {
        buffer.reset()
        let engine = AVAudioEngine()
        let input = engine.inputNode
        let inputFormat = input.outputFormat(forBus: 0)
        guard inputFormat.channelCount > 0, inputFormat.sampleRate > 0 else {
            throw AudioRecorderError.noInputDevice
        }
        guard let target = AVAudioFormat(commonFormat: .pcmFormatFloat32, sampleRate: Self.sampleRate,
                                         channels: 1, interleaved: false),
              let converter = AVAudioConverter(from: inputFormat, to: target) else {
            throw AudioRecorderError.converterUnavailable
        }
        input.installTap(onBus: 0, bufferSize: 4096, format: inputFormat,
                         block: Self.makeTap(converter: converter, target: target, sink: buffer))
        engine.prepare()
        try engine.start()
        self.engine = engine
    }

    /// Construit hors du MainActor : le bloc est appelé sur le thread audio temps réel,
    /// une closure isolée au MainActor y déclencherait un piège d'isolation (SIGTRAP).
    private nonisolated static func makeTap(converter: AVAudioConverter, target: AVAudioFormat,
                                            sink: SampleBuffer) -> AVAudioNodeTapBlock {
        return { pcm, _ in
            let ratio = target.sampleRate / pcm.format.sampleRate
            let capacity = AVAudioFrameCount(Double(pcm.frameLength) * ratio) + 32
            guard let out = AVAudioPCMBuffer(pcmFormat: target, frameCapacity: capacity) else { return }
            var consumed = false
            var error: NSError?
            converter.convert(to: out, error: &error) { _, status in
                if consumed { status.pointee = .noDataNow; return nil }
                consumed = true
                status.pointee = .haveData
                return pcm
            }
            guard error == nil, let data = out.floatChannelData else { return }
            sink.append(UnsafeBufferPointer(start: data[0], count: Int(out.frameLength)))
        }
    }

    public var level: Float { buffer.level }

    public func stop() -> [Float] {
        if let engine {
            engine.inputNode.removeTap(onBus: 0)
            engine.stop()
        }
        engine = nil
        return buffer.drain()
    }
}

/// Tampon partagé avec le thread audio temps réel.
final class SampleBuffer: @unchecked Sendable {
    private let lock = NSLock()
    private var samples: [Float] = []
    private var lastLevel: Float = 0

    var level: Float {
        lock.lock(); defer { lock.unlock() }
        return lastLevel
    }

    func append(_ chunk: UnsafeBufferPointer<Float>) {
        let level = Self.normalizedLevel(chunk)
        lock.lock(); samples.append(contentsOf: chunk); lastLevel = level; lock.unlock()
    }

    func reset() {
        lock.lock(); samples.removeAll(keepingCapacity: true); lastLevel = 0; lock.unlock()
    }

    /// RMS du bloc en dB, ramené à 0…1 entre -50 dB (silence) et -10 dB (voix forte).
    static func normalizedLevel(_ chunk: UnsafeBufferPointer<Float>) -> Float {
        guard !chunk.isEmpty else { return 0 }
        var sum: Float = 0
        for s in chunk { sum += s * s }
        let rms = (sum / Float(chunk.count)).squareRoot()
        let db = 20 * log10(max(rms, 1e-7))
        return min(max((db + 50) / 40, 0), 1)
    }

    func drain() -> [Float] {
        lock.lock(); defer { lock.unlock() }
        let out = samples
        samples.removeAll()
        return out
    }
}
