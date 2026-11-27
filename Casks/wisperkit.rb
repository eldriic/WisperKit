cask "wisperkit" do
  version "0.2.1"
  sha256 "74b69765edc4a91270b53e8b23ec64d05d7d7cd0035eee33e2df3b13344a6a4f"

  url "https://github.com/eldriic/WisperKit/releases/download/v#{version}/WisperKit.dmg"
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
