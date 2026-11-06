cask "wisperkit" do
  version "0.1.0"
  sha256 "900e9407307f71e629241c3b85a60f64dca334363b68bc30e85326a7fc99fa49"

  url "https://github.com/eldriic/WisperKit/releases/download/v#{version}/WisperKit-#{version}.dmg"
  name "WisperKit"
  desc "Local Whisper dictation from the menu bar"
  homepage "https://github.com/eldriic/WisperKit"

  depends_on arch: :arm64
  depends_on macos: :sequoia

  app "WisperKit.app"

  # L'app n'est pas notarisée par Apple : on retire la quarantaine pour éviter le blocage Gatekeeper.
  postflight_steps do
    run "/usr/bin/xattr", args: ["-dr", "com.apple.quarantine", "{{appdir}}/WisperKit.app"]
  end

  uninstall quit: "com.eldriic.wisperkit"

  zap trash: [
    "~/Library/Application Support/WisperKit",
    "~/Library/Logs/WisperKit",
    "~/Library/Preferences/com.eldriic.wisperkit.plist",
  ]
end
