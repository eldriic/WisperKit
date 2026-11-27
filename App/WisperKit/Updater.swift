import Foundation
import Observation
import Sparkle
import WisperKitCore

/// Mises à jour via Sparkle : vérification quotidienne de l'appcast publié avec chaque release
/// (GitHub), archives signées EdDSA. Seule requête réseau hors modèles ; désactivable dans les réglages.
///
/// App de barre de menus : on utilise les « rappels discrets » de Sparkle. Une mise à jour trouvée
/// en arrière-plan n'ouvre pas de fenêtre ; elle s'affiche dans le menu, et un clic lance la fenêtre
/// standard de Sparkle (notes de version, installation, relance).
@MainActor
@Observable
final class Updater: NSObject {
    /// Version trouvée et pas encore installée ni ignorée (ligne « Update available » du menu).
    private(set) var availableVersion: String?
    private(set) var lastCheck: Date?

    @ObservationIgnored private var controller: SPUStandardUpdaterController?

    /// `enabled` à false (captures, autotest) : rien n'est vérifié ni planifié.
    init(enabled: Bool) {
        super.init()
        guard enabled else { return }
        controller = SPUStandardUpdaterController(startingUpdater: true, updaterDelegate: self, userDriverDelegate: self)
        lastCheck = controller?.updater.lastUpdateCheckDate
        automaticChecks = controller?.updater.automaticallyChecksForUpdates ?? false
    }

    var automaticChecks = false {
        didSet {
            guard let updater = controller?.updater, updater.automaticallyChecksForUpdates != automaticChecks else { return }
            updater.automaticallyChecksForUpdates = automaticChecks
            DiagnosticLog.write("mises à jour automatiques=\(automaticChecks)")
        }
    }

    var canCheck: Bool { controller != nil }

    /// Vérification demandée par l'utilisateur : Sparkle affiche toujours sa fenêtre (à jour ou non).
    func checkNow() {
        controller?.checkForUpdates(nil)
    }

    private func found(_ item: SUAppcastItem) {
        availableVersion = item.displayVersionString
        lastCheck = controller?.updater.lastUpdateCheckDate
        DiagnosticLog.write("mise à jour disponible : \(item.displayVersionString)")
    }

    private func notFound() {
        availableVersion = nil
        lastCheck = controller?.updater.lastUpdateCheckDate
    }

    #if DEBUG
    func forceAvailableForSnapshots(_ version: String) { availableVersion = version }
    #endif
}

// Sparkle appelle ses délégués sur le fil principal.
extension Updater: SPUUpdaterDelegate {
    nonisolated func updater(_ updater: SPUUpdater, didFindValidUpdate item: SUAppcastItem) {
        MainActor.assumeIsolated { found(item) }
    }

    nonisolated func updaterDidNotFindUpdate(_ updater: SPUUpdater) {
        MainActor.assumeIsolated { notFound() }
    }

    nonisolated func updater(_ updater: SPUUpdater, userDidMake choice: SPUUserUpdateChoice,
                             forUpdate updateItem: SUAppcastItem, state: SPUUserUpdateState) {
        // « Ignorer cette version » : on retire la ligne du menu jusqu'à la suivante.
        if choice == .skip { MainActor.assumeIsolated { availableVersion = nil } }
    }
}

extension Updater: SPUStandardUserDriverDelegate {
    nonisolated var supportsGentleScheduledUpdateReminders: Bool { true }

    /// Vérification planifiée : fenêtre seulement si l'app est déjà au premier plan, sinon le menu suffit.
    nonisolated func standardUserDriverShouldHandleShowingScheduledUpdate(_ update: SUAppcastItem,
                                                                         andInImmediateFocus immediateFocus: Bool) -> Bool {
        immediateFocus
    }

    nonisolated func standardUserDriverWillHandleShowingUpdate(_ handleShowingUpdate: Bool, forUpdate update: SUAppcastItem,
                                                               state: SPUUserUpdateState) {
        MainActor.assumeIsolated { found(update) }
    }
}
