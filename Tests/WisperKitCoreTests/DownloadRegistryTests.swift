import Foundation
import Testing
@testable import WisperKitCore

/// Compteur partagé entre tâches.
private actor Counter {
    var value = 0
    func increment() { value += 1 }
}

@Test func concurrentDownloadsOfTheSameKeyRunOnce() async throws {
    let registry = DownloadRegistry()
    let calls = Counter()
    let url = URL(fileURLWithPath: "/tmp/model")
    let work: @Sendable (@escaping @Sendable (Double) -> Void) async throws -> URL = { report in
        await calls.increment()
        try await Task.sleep(for: .milliseconds(200))
        report(1)
        return url
    }
    async let first = registry.run(key: "base", progress: nil, work: work)
    try await Task.sleep(for: .milliseconds(50))
    async let second = registry.run(key: "base", progress: nil, work: work)
    let results = try await [first, second]
    #expect(results == [url, url])
    #expect(await calls.value == 1)

    // Terminé : un nouvel appel relance le travail.
    _ = try await registry.run(key: "base", progress: nil, work: work)
    #expect(await calls.value == 2)
}
