// swift-tools-version: 6.0
import PackageDescription

let package = Package(
    name: "WisperKit",
    platforms: [.macOS(.v15)],
    products: [
        .library(name: "WisperKitCore", targets: ["WisperKitCore"]),
    ],
    dependencies: [
        .package(url: "https://github.com/argmaxinc/WhisperKit.git", from: "1.1.1"),
    ],
    targets: [
        // Cœur : audio, transcription, machine à états, insertion de texte.
        // L'app SwiftUI (App/WisperKit) est un target Xcode séparé (project.yml XcodeGen).
        .target(name: "WisperKitCore", dependencies: [
            .product(name: "WhisperKit", package: "WhisperKit"),
        ]),
        .testTarget(name: "WisperKitCoreTests", dependencies: ["WisperKitCore"]),
    ]
)
