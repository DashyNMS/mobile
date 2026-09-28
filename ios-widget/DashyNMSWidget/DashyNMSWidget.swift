import SwiftUI
import WidgetKit

/// DashyNMS's home-screen widget: open alerts by severity, devices down and
/// (medium size) the alerts most needing attention - from the snapshot the
/// app saves after each alert check.
@main
struct AlertsWidget: Widget {
    let kind = "DashyNMSAlerts"

    var body: some WidgetConfiguration {
        StaticConfiguration(kind: kind, provider: Provider()) { entry in
            AlertsWidgetView(entry: entry)
        }
        .configurationDisplayName("Alerts")
        .description("Open alerts and devices down, from your last alert check.")
        .supportedFamilies([.systemSmall, .systemMedium])
    }
}

struct AlertsEntry: TimelineEntry {
    let date: Date
    let snapshot: Snapshot
}

/// Re-reads the snapshot every 15 minutes (iOS may stretch that). The app
/// can't ask WidgetKit to reload from .NET - WidgetCenter is Swift-only -
/// so the widget comes to the snapshot instead.
struct Provider: TimelineProvider {
    func placeholder(in context: Context) -> AlertsEntry {
        AlertsEntry(date: Date(), snapshot: .preview)
    }

    func getSnapshot(in context: Context, completion: @escaping (AlertsEntry) -> Void) {
        completion(AlertsEntry(date: Date(), snapshot: context.isPreview ? .preview : Snapshot.load()))
    }

    func getTimeline(in context: Context, completion: @escaping (Timeline<AlertsEntry>) -> Void) {
        let now = Date()
        let next = now.addingTimeInterval(15 * 60)
        completion(Timeline(entries: [AlertsEntry(date: now, snapshot: Snapshot.load())], policy: .after(next)))
    }
}

/// The app's dark surface and status colours (Resources/Styles/Colors.xaml).
extension Color {
    static let dnBackground = Color(red: 0x17 / 255, green: 0x1B / 255, blue: 0x23 / 255)
    static let dnText = Color(red: 0xE6 / 255, green: 0xEA / 255, blue: 0xF0 / 255)
    static let dnSecondary = Color(red: 0x8B / 255, green: 0x93 / 255, blue: 0xA1 / 255)
    static let dnCritical = Color(red: 0xDA / 255, green: 0x36 / 255, blue: 0x33 / 255)
    static let dnWarning = Color(red: 0xDB / 255, green: 0x9A / 255, blue: 0x04 / 255)
    static let dnOk = Color(red: 0x2E / 255, green: 0xA0 / 255, blue: 0x43 / 255)

    static func severity(_ key: String) -> Color {
        switch key {
        case "critical": return .dnCritical
        case "warning": return .dnWarning
        default: return .dnOk
        }
    }
}

struct AlertsWidgetView: View {
    @Environment(\.widgetFamily) private var family
    let entry: AlertsEntry

    private var snapshot: Snapshot { entry.snapshot }

    var body: some View {
        VStack(alignment: .leading, spacing: 6) {
            header

            if !snapshot.signedIn {
                Text("Open DashyNMS to sign in.")
                    .font(.caption)
                    .foregroundColor(.dnSecondary)
            } else {
                counts

                if snapshot.active == 0 {
                    Text("No active alerts.")
                        .font(.caption)
                        .foregroundColor(.dnSecondary)
                } else if family != .systemSmall {
                    ForEach(snapshot.alerts.prefix(3), id: \.alertId) { alert in
                        Link(destination: link(for: alert)) {
                            AlertRow(alert: alert)
                        }
                    }
                }
            }

            Spacer(minLength: 0)
        }
        .frame(maxWidth: .infinity, maxHeight: .infinity, alignment: .topLeading)
        .widgetURL(URL(string: "dashynms://alerts"))
        .modifier(WidgetBackground())
    }

    private var header: some View {
        HStack {
            Text("DashyNMS")
                .font(.caption)
                .bold()
                .foregroundColor(.dnText)
            Spacer()
            if snapshot.signedIn, let checked = snapshot.checkedDate {
                Text(checked, style: .time)
                    .font(.caption2)
                    .foregroundColor(.dnSecondary)
            }
        }
    }

    private var counts: some View {
        HStack(alignment: .top, spacing: 8) {
            CountView(value: String(snapshot.critical), label: "Critical", color: .dnCritical)
            CountView(value: String(snapshot.warning), label: "Warning", color: .dnWarning)
            CountView(
                value: snapshot.devicesDown.map { String($0) } ?? "–",
                label: "Down",
                color: (snapshot.devicesDown ?? 0) > 0 ? .dnCritical : .dnText)
        }
    }

    private func link(for alert: WidgetAlert) -> URL {
        URL(string: "dashynms://alert/\(alert.alertId)?device=\(alert.deviceId)") ?? URL(string: "dashynms://alerts")!
    }
}

struct CountView: View {
    let value: String
    let label: String
    let color: Color

    var body: some View {
        VStack(alignment: .leading, spacing: 0) {
            Text(value)
                .font(.system(size: 26, weight: .bold))
                .foregroundColor(color)
                .lineLimit(1)
                .minimumScaleFactor(0.6)
            Text(label)
                .font(.caption2)
                .foregroundColor(.dnSecondary)
        }
        .frame(maxWidth: .infinity, alignment: .leading)
    }
}

struct AlertRow: View {
    let alert: WidgetAlert

    var body: some View {
        HStack(spacing: 8) {
            Circle()
                .fill(Color.severity(alert.severity))
                .frame(width: 8, height: 8)
            VStack(alignment: .leading, spacing: 0) {
                Text(alert.rule)
                    .font(.caption)
                    .foregroundColor(.dnText)
                    .lineLimit(1)
                Text(alert.device)
                    .font(.caption2)
                    .foregroundColor(.dnSecondary)
                    .lineLimit(1)
            }
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
