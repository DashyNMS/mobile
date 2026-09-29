import SwiftUI
import WidgetKit

/// The app's dark surface and status colours (Resources/Styles/Colors.xaml),
/// plus the lighter tints the mock-ups use for coloured text and tiles, which
/// the plain status colours are too dark for on this background.
extension Color {
    init(hex: UInt32) {
        self.init(
            red: Double((hex >> 16) & 0xFF) / 255,
            green: Double((hex >> 8) & 0xFF) / 255,
            blue: Double(hex & 0xFF) / 255)
    }

    static let dnBackground = Color(hex: 0x171B23)
    static let dnTile = Color(hex: 0x1E2430)
    static let dnTrack = Color(hex: 0x262C38)
    static let dnText = Color(hex: 0xE8EEF6)
    static let dnSecondary = Color(hex: 0xA3ADBB)
    static let dnFaint = Color(hex: 0x7F8A9A)

    static let dnCritical = Color(hex: 0xDA3633)
    static let dnWarning = Color(hex: 0xDB9A04)
    static let dnAcknowledged = Color(hex: 0x6E7A91)
    static let dnOk = Color(hex: 0x2EA043)
    static let dnInactive = Color(hex: 0x6E6E6E)

    static let dnCriticalText = Color(hex: 0xFF7B72)
    static let dnWarningText = Color(hex: 0xF2C14E)
    static let dnAcknowledgedText = Color(hex: 0x9AA6BE)
    static let dnOkText = Color(hex: 0x56D364)

    static let dnCriticalTile = Color(hex: 0x3A1E20)
    static let dnWarningTile = Color(hex: 0x352A12)
    static let dnAcknowledgedTile = Color(hex: 0x232A38)

    /// A dot's colour for a severity or state key from the snapshot.
    static func state(_ key: String) -> Color {
        switch key {
        case "critical", "down": return .dnCritical
        case "warning": return .dnWarning
        case "acknowledged": return .dnAcknowledged
        case "ok": return .dnOk
        case "disabled": return .dnInactive
        default: return .dnSecondary
        }
    }

    /// Text in a state's colour, readable on the dark background; quiet for all-well.
    static func stateText(_ key: String) -> Color {
        switch key {
        case "critical", "down": return .dnCriticalText
        case "warning": return .dnWarningText
        case "acknowledged": return .dnAcknowledgedText
        default: return .dnSecondary
        }
    }
}

/// iOS 17 wants the background declared as the widget's container
/// background; iOS 16 just draws it.
struct WidgetBackground: ViewModifier {
    @ViewBuilder
    func body(content: Content) -> some View {
        if #available(iOS 17.0, *) {
            content.containerBackground(Color.dnBackground, for: .widget)
        } else {
            content.padding().background(Color.dnBackground)
        }
    }
}

/// Lock-screen widgets are tinted by the system; they only need to say
/// "no background of my own" on iOS 17.
struct AccessoryBackground: ViewModifier {
    @ViewBuilder
    func body(content: Content) -> some View {
        if #available(iOS 17.0, *) {
            content.containerBackground(for: .widget) { Color.clear }
        } else {
            content
        }
    }
}

enum Links {
    static let alerts = URL(string: "dashynms://alerts")!

    static func alert(_ alert: WidgetAlert) -> URL {
        URL(string: "dashynms://alert/\(alert.alertId)?device=\(alert.deviceId)") ?? alerts
    }

    static func device(_ id: Int) -> URL {
        URL(string: "dashynms://device/\(id)") ?? alerts
    }
}

struct Dot: View {
    let color: Color
    var size: CGFloat = 8

    var body: some View {
        Circle().fill(color).frame(width: size, height: size)
    }
}

/// A small uppercase heading ("ALERTS", "NEEDING A LOOK").
struct Eyebrow: View {
    let text: String

    var body: some View {
        Text(text.uppercased())
            .font(.system(size: 11, weight: .semibold))
            .tracking(0.6)
            .foregroundColor(.dnSecondary)
    }
}

/// A number over its label on a tinted tile (Critical 2, Warning 3...).
struct CountTile: View {
    let value: Int
    let label: String
    let text: Color
    let tile: Color
    var size: CGFloat = 20

    var body: some View {
        VStack(alignment: .leading, spacing: 0) {
            Text(Format.count(value))
                .font(.system(size: size, weight: .semibold, design: .monospaced))
                .foregroundColor(text)
                .lineLimit(1)
                .minimumScaleFactor(0.6)
            Text(label)
                .font(.system(size: 11))
                .foregroundColor(.dnText.opacity(0.75))
                .lineLimit(1)
                .minimumScaleFactor(0.8)
        }
        .padding(.horizontal, 8)
        .padding(.vertical, 6)
        .frame(maxWidth: .infinity, alignment: .leading)
        .background(RoundedRectangle(cornerRadius: 10).fill(tile))
    }
}

/// Before the first check, or after signing out.
struct SignedOutView: View {
    var body: some View {
        VStack(alignment: .leading, spacing: 6) {
            Text("DashyNMS")
                .font(.caption)
                .bold()
                .foregroundColor(.dnText)
            Text("Open DashyNMS to sign in.")
                .font(.caption)
                .foregroundColor(.dnSecondary)
            Spacer(minLength: 0)
        }
        .frame(maxWidth: .infinity, maxHeight: .infinity, alignment: .topLeading)
    }
}

/// A quiet message where a list would be ("No active alerts.").
struct EmptyMessage: View {
    let text: String
    var systemImage: String? = nil

    var body: some View {
        HStack(spacing: 6) {
            if let systemImage {
                Image(systemName: systemImage).foregroundColor(.dnOkText)
            }
            Text(text)
                .font(.caption)
                .foregroundColor(.dnSecondary)
        }
    }
}
