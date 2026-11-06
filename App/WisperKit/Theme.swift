import SwiftUI

/// Palette noire de l'app (cf. maquettes validées).
enum Theme {
    static let background = Color.black
    static let selection = Color(white: 0.13)      // #212121
    static let stroke = Color(white: 0.165)        // #2a2a2a
    static let pill = Color(white: 0.165)
    static let popover = Color(white: 0.17)        // #2b2b2b
    static let popoverStroke = Color(white: 0.23)
    static let text = Color(white: 0.925)          // #ececec
    static let secondary = Color(white: 0.61)      // #9b9b9b
    static let blue = Color(red: 0.184, green: 0.486, blue: 0.965) // #2f7cf6
    static let green = Color(red: 0.30, green: 0.85, blue: 0.39)
    static let danger = Color(red: 1, green: 0.42, blue: 0.42)
    static let dangerFill = Color(red: 0.23, green: 0.09, blue: 0.09)
}

// MARK: - Composants des réglages

/// Carte à bord fin contenant des lignes séparées par un filet.
struct SettingsCard<Content: View>: View {
    @ViewBuilder var content: Content

    var body: some View {
        VStack(spacing: 0) {
            Group(subviews: content) { rows in
                ForEach(Array(rows.enumerated()), id: \.offset) { index, row in
                    row
                    if index < rows.count - 1 {
                        Rectangle().fill(Theme.stroke).frame(height: 1)
                    }
                }
            }
        }
        .padding(.horizontal, 16)
        .overlay(RoundedRectangle(cornerRadius: 14).strokeBorder(Theme.stroke, lineWidth: 1))
    }
}

/// Ligne « titre + description grise » avec un contrôle aligné à droite.
struct SettingsRow<Trailing: View>: View {
    let title: String
    var description: String?
    var badge: String?
    @ViewBuilder var trailing: Trailing

    var body: some View {
        HStack(alignment: .center, spacing: 16) {
            VStack(alignment: .leading, spacing: 3) {
                HStack(spacing: 6) {
                    Text(title).font(.system(size: 13)).foregroundStyle(Theme.text)
                    if let badge { Text(badge).font(.system(size: 10.5)).foregroundStyle(Theme.blue) }
                }
                if let description {
                    Text(description).font(.system(size: 11.5)).foregroundStyle(Theme.secondary)
                        .fixedSize(horizontal: false, vertical: true)
                }
            }
            Spacer(minLength: 12)
            trailing
        }
        .padding(.vertical, 12)
    }
}

struct SectionTitle: View {
    let text: String
    var body: some View {
        Text(text).font(.system(size: 13, weight: .medium)).foregroundStyle(Theme.text)
            .padding(.bottom, 8).padding(.top, 4)
    }
}

/// Bouton en pilule grise.
struct PillButtonStyle: ButtonStyle {
    var foreground: Color = Theme.text
    var fill: Color = Theme.pill

    func makeBody(configuration: Configuration) -> some View {
        configuration.label
            .font(.system(size: 12))
            .foregroundStyle(foreground)
            .padding(.horizontal, 12).padding(.vertical, 6)
            .background(Capsule().fill(fill.opacity(configuration.isPressed ? 0.7 : 1)))
            .contentShape(Capsule())
    }
}

/// Menu déroulant en pilule (libellé + chevron).
struct PillMenu<Value: Hashable>: View {
    @Binding var selection: Value
    let options: [(value: Value, label: String)]

    var body: some View {
        Menu {
            ForEach(options, id: \.value) { option in
                Button {
                    selection = option.value
                } label: {
                    if option.value == selection { Label(option.label, systemImage: "checkmark") } else { Text(option.label) }
                }
            }
        } label: {
            HStack(spacing: 6) {
                Text(options.first { $0.value == selection }?.label ?? "")
                Image(systemName: "chevron.down").font(.system(size: 9, weight: .semibold))
            }
            .font(.system(size: 12))
            .foregroundStyle(Theme.text)
            .padding(.horizontal, 12).padding(.vertical, 6)
            .background(Capsule().fill(Theme.pill))
            .contentShape(Capsule())
        }
        .menuStyle(.button)
        .buttonStyle(.plain)
        .menuIndicator(.hidden)
        .fixedSize()
    }
}

struct BlueSwitch: View {
    @Binding var isOn: Bool
    var body: some View {
        Toggle("", isOn: $isOn).toggleStyle(.switch).labelsHidden().tint(Theme.blue).controlSize(.small)
    }
}
