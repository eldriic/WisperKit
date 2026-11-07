import AppKit
import Foundation
import Testing
@testable import WisperKitCore

@MainActor
final class FakeRecorder: AudioRecording {
    var samples: [Float] = Array(repeating: 0.1, count: 16_000) // 1 s
    var startError: Error?
    private(set) var running = false

    func start() throws {
        if let startError { throw startError }
        running = true
    }

    func stop() -> [Float] {
        running = false
        return samples
    }
}

struct FakeTranscriber: Transcribing {
    var text = " Bonjour le monde. "
    var error: Error?
    var delay: Duration = .zero

    func transcribe(_ samples: [Float], language: String, translate: Bool) async throws -> String {
        try await Task.sleep(for: delay)
        if let error { throw error }
        return text
    }
}

@MainActor
final class FakeOutput: TextOutput {
    var delivered: [String] = []
    var methods: [InsertionMethod] = []
    var pastes = true

    func deliver(_ text: String, autoInsert: Bool, method: InsertionMethod, restoreClipboard: Bool) -> DeliveryResult {
        delivered.append(text)
        methods.append(method)
        guard autoInsert && pastes else { return method == .type ? .notInserted : .clipboardOnly }
        return method == .type ? .typed : .pasted
    }
}

struct Boom: LocalizedError { var errorDescription: String? { "boum" } }

@MainActor
@Suite struct DictationControllerTests {
    @Test func toggleRecordsThenDeliversCleanText() async {
        let rec = FakeRecorder(), out = FakeOutput()
        let c = DictationController(recorder: rec, transcriber: FakeTranscriber(), output: out)
        var events: [String] = []
        c.onEvent = { events.append("\($0)") }

        c.toggle()
        #expect(c.state == .recording)
        #expect(rec.running)

        c.toggle()
        #expect(c.state == .transcribing)
        await c.waitForCompletion()

        #expect(c.state == .idle)
        #expect(out.delivered == ["Bonjour le monde."])
        #expect(c.lastText == "Bonjour le monde.")
        #expect(events == ["started", "stopped", "delivered(WisperKitCore.DeliveryResult.pasted)"])
    }

    @Test func tooShortRecordingIsIgnored() async {
        let rec = FakeRecorder(); rec.samples = Array(repeating: 0, count: 1_000)
        let out = FakeOutput()
        let c = DictationController(recorder: rec, transcriber: FakeTranscriber(), output: out)
        c.start(); c.stop()
        #expect(c.state == .idle)
        #expect(out.delivered.isEmpty)
    }

    @Test func cancelDuringRecordingDropsAudio() async {
        let rec = FakeRecorder(), out = FakeOutput()
        let c = DictationController(recorder: rec, transcriber: FakeTranscriber(), output: out)
        c.start(); c.cancel()
        #expect(c.state == .idle)
        #expect(!rec.running)
        #expect(out.delivered.isEmpty)
    }

    @Test func cancelDuringTranscriptionDeliversNothing() async {
        let out = FakeOutput()
        let c = DictationController(recorder: FakeRecorder(),
                                    transcriber: FakeTranscriber(delay: .milliseconds(200)), output: out)
        c.start(); c.stop()
        #expect(c.state == .transcribing)
        c.cancel()
        try? await Task.sleep(for: .milliseconds(300))
        #expect(c.state == .idle)
        #expect(out.delivered.isEmpty)
    }

    @Test func transcriptionErrorSurfacesAndCanRestart() async {
        let c = DictationController(recorder: FakeRecorder(),
                                    transcriber: FakeTranscriber(error: Boom()), output: FakeOutput())
        c.start(); c.stop()
        await c.waitForCompletion()
        #expect(c.state == .failed("boum"))
        c.start()
        #expect(c.state == .recording)
    }

    @Test func microphoneErrorFails() {
        let rec = FakeRecorder(); rec.startError = AudioRecorderError.noInputDevice
        let c = DictationController(recorder: rec, transcriber: FakeTranscriber(), output: FakeOutput())
        c.start()
        #expect(c.state == .failed("No microphone available."))
    }

    @Test func hallucinationOnSilenceIsNotPasted() async {
        let out = FakeOutput()
        let c = DictationController(recorder: FakeRecorder(),
                                    transcriber: FakeTranscriber(text: " Merci d'avoir regardé ! "), output: out)
        c.start(); c.stop()
        await c.waitForCompletion()
        #expect(out.delivered.isEmpty)
        #expect(c.lastText == nil)
    }
}

@Suite struct TextCleanerTests {
    @Test func trimsAndCollapsesSpaces() {
        #expect(TextCleaner.clean("  Bonjour   à\n tous  ") == "Bonjour à tous")
    }

    @Test func removesNonVerbalTokens() {
        #expect(TextCleaner.clean("[Musique] Salut (rires) ça va") == "Salut ça va")
    }

    @Test func dropsFullHallucinationOnly() {
        #expect(TextCleaner.clean("Sous-titres réalisés par la communauté d'Amara.org") == "")
        #expect(TextCleaner.clean("Merci d'avoir regardé le schéma, on passe au code.")
                == "Merci d'avoir regardé le schéma, on passe au code.")
    }
}

/// Test d'intégration réel (télécharge le modèle base, ~140 Mo) :
/// `WK_INTEGRATION=1 swift test --filter Integration`
@Suite(.enabled(if: ProcessInfo.processInfo.environment["WK_INTEGRATION"] == "1"))
struct IntegrationTests {
    @Test func transcribesFrenchSpeech() async throws {
        let dir = FileManager.default.temporaryDirectory.appending(path: "wk-it-\(UUID().uuidString)")
        try FileManager.default.createDirectory(at: dir, withIntermediateDirectories: true)
        let wav = dir.appending(path: "phrase.wav")
        let say = Process()
        say.executableURL = URL(filePath: "/usr/bin/say")
        say.arguments = ["-v", "Thomas", "-o", wav.path, "--data-format=LEF32@16000",
                         "Bonjour, je veux créer une application de budget."]
        try say.run(); say.waitUntilExit()

        let samples = try Self.readFloats(wav)
        let t = Transcriber()
        try await t.load(.base)
        let clock = ContinuousClock()
        var text = ""
        let elapsed = try await clock.measure { text = try await t.transcribe(samples, language: "fr", translate: false) }
        print("Transcription (\(elapsed)): \(text)")
        #expect(TextCleaner.clean(text).lowercased().contains("budget"))
    }

    /// Tâche « translate » de Whisper (modèles non turbo) : parole française → texte anglais.
    @Test func translatesFrenchSpeechToEnglish() async throws {
        let dir = FileManager.default.temporaryDirectory.appending(path: "wk-tr-\(UUID().uuidString)")
        try FileManager.default.createDirectory(at: dir, withIntermediateDirectories: true)
        let wav = dir.appending(path: "phrase.wav")
        let say = Process()
        say.executableURL = URL(filePath: "/usr/bin/say")
        say.arguments = ["-v", "Thomas", "-o", wav.path, "--data-format=LEF32@16000",
                         "Bonjour, je voudrais réserver une table pour ce soir."]
        try say.run(); say.waitUntilExit()
        let t = Transcriber()
        try await t.load(.base)
        let text = try await t.transcribe(try Self.readFloats(wav), language: "fr", translate: true)
        print("Traduction : \(text)")
        let lower = text.lowercased()
        #expect(lower.contains("table") && (lower.contains("hello") || lower.contains("would") || lower.contains("tonight")))
    }

    static func readFloats(_ url: URL) throws -> [Float] {
        let data = try Data(contentsOf: url)
        // Cherche le bloc « data » du WAV et lit le PCM float32 qui suit.
        guard let r = data.range(of: Data("data".utf8)) else { return [] }
        let start = r.upperBound + 4
        return data[start...].withUnsafeBytes { Array($0.bindMemory(to: Float.self)) }
    }
}

@Suite struct ModelStoreTests {
    @Test func partialDownloadIsNotInstalled() throws {
        let base = FileManager.default.temporaryDirectory.appending(path: "wk-ms-\(UUID().uuidString)")
        let folder = ModelStore.folder(for: .base, base: base)
        try FileManager.default.createDirectory(at: folder.appending(path: "AudioEncoder.mlmodelc"),
                                                withIntermediateDirectories: true)
        #expect(!ModelStore.isInstalled(.base, base: base))
        ModelStore.markInstalled(.base, base: base)
        #expect(ModelStore.isInstalled(.base, base: base))
        try ModelStore.remove(.base, base: base)
        #expect(!ModelStore.isInstalled(.base, base: base))
    }
}

@Suite struct LevelTests {
    @Test func silenceIsZeroAndLoudIsOne() {
        let silence = [Float](repeating: 0, count: 1600)
        let loud = (0..<1600).map { Float(sin(Double($0) * 0.1)) * 0.8 }
        let speech = (0..<1600).map { Float(sin(Double($0) * 0.1)) * 0.03 }
        silence.withUnsafeBufferPointer { #expect(SampleBuffer.normalizedLevel($0) == 0) }
        loud.withUnsafeBufferPointer { #expect(SampleBuffer.normalizedLevel($0) == 1) }
        speech.withUnsafeBufferPointer {
            let l = SampleBuffer.normalizedLevel($0)
            #expect(l > 0.2 && l < 0.8)
        }
    }
}

@MainActor
@Suite struct InsertionTests {
    @Test func typeModeIsPassedToOutput() async {
        let out = FakeOutput()
        let c = DictationController(recorder: FakeRecorder(), transcriber: FakeTranscriber(), output: out)
        c.insertionMethod = .type
        var result: DeliveryResult?
        c.onEvent = { if case .delivered(let r) = $0 { result = r } }
        c.start(); c.stop()
        await c.waitForCompletion()
        #expect(out.methods == [.type])
        #expect(result == .typed)
    }

    @Test func chunksNeverSplitCharacters() {
        let text = "Café 👩‍💻 déjà vu, ça marche ?"
        let parts = TextInserter.chunks(of: text, size: 4)
        #expect(parts.joined() == text)
        #expect(parts.allSatisfy { $0.count <= 4 })
    }

    @Test func transientClipboardIsMarked() {
        let pb = NSPasteboard(name: NSPasteboard.Name("wk-test-\(UUID().uuidString)"))
        TextInserter.write("secret", to: pb, transient: true)
        #expect(pb.string(forType: .string) == "secret")
        #expect(pb.types?.contains(NSPasteboard.PasteboardType("org.nspasteboard.TransientType")) == true)
        pb.releaseGlobally()
    }
}

@Suite struct DecodingPlanTests {
    @Test func transcribesInTheSpokenLanguageByDefault() {
        #expect(DecodingPlan.make(language: "auto", translate: false, modelIsTurbo: true)
                == DecodingPlan(translateTask: false, language: nil))
        #expect(DecodingPlan.make(language: "fr", translate: false, modelIsTurbo: false)
                == DecodingPlan(translateTask: false, language: "fr"))
    }

    @Test func translatesWithWhisperTaskWhenTheModelSupportsIt() {
        #expect(DecodingPlan.make(language: "auto", translate: true, modelIsTurbo: false)
                == DecodingPlan(translateTask: true, language: nil))
        #expect(DecodingPlan.make(language: "fr", translate: true, modelIsTurbo: false)
                == DecodingPlan(translateTask: true, language: "fr"))
    }

    @Test func turboForcesEnglishOutputInstead() {
        #expect(DecodingPlan.make(language: "fr", translate: true, modelIsTurbo: true)
                == DecodingPlan(translateTask: false, language: "en"))
    }
}
