import Foundation
import os

/// Journal lisible hors Console : ~/Library/Logs/WisperKit/wisperkit.log
/// (doublé dans os_log, sous-système com.eldriic.wisperkit).
public enum DiagnosticLog {
    public static let url: URL = FileManager.default.homeDirectoryForCurrentUser
        .appending(path: "Library/Logs/WisperKit/wisperkit.log")

    private static let logger = Logger(subsystem: "com.eldriic.wisperkit", category: "diag")
    private static let queue = DispatchQueue(label: "com.eldriic.wisperkit.log")
    private static let maxSize = 512 * 1024

    public static func write(_ message: String) {
        logger.info("\(message, privacy: .public)")
        let line = "\(Date().ISO8601Format(.iso8601.time(includingFractionalSeconds: true))) \(message)\n"
        queue.async {
            let fm = FileManager.default
            try? fm.createDirectory(at: url.deletingLastPathComponent(), withIntermediateDirectories: true)
            if let size = try? fm.attributesOfItem(atPath: url.path)[.size] as? Int, size > maxSize {
                try? fm.removeItem(at: url) // rotation simple : le journal ne sert qu'au diagnostic récent
            }
            if let handle = try? FileHandle(forWritingTo: url) {
                handle.seekToEndOfFile()
                handle.write(Data(line.utf8))
                try? handle.close()
            } else {
                try? Data(line.utf8).write(to: url)
            }
        }
    }
}
