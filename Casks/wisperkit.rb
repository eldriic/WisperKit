cask "wisperkit" do
  version "0.2.0"
  sha256 "942132412c7b44bc165694a7cc1e3f05ceabec3fc45de744d9c34dca1d66f9fa"

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
