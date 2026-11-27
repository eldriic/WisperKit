cask "wisperkit" do
  version "0.3.0"
  sha256 "6b3390c5a91734581e53fdf4198aeea45b2d6e0f10e5710cb4a637d6141969df"

  url "https://github.com/eldriic/WisperKit/releases/download/v#{version}/WisperKit.dmg"
  name "WisperKit"
  desc "Local Whisper dictation from the menu bar"
  homepage "https://github.com/eldriic/WisperKit"

  depends_on arch: :arm64
  depends_on macos: :sequoia

  # Mises à jour intégrées (Sparkle) : brew ne force pas la sienne.
  auto_updates true

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
