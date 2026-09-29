import SwiftUI
import WidgetKit

/// The iPhone lock screen: inline (above the clock), circular and
/// rectangular. iOS tints these itself, so they carry no colour of their
/// own. With the app's "Hide alert details" on (the default), they show
/// counts only - no device or rule names for whoever picks the phone up.
struct LockScreenWidget: Widget {
    let kind = "DashyNMSLockScreen"

    var body: some WidgetConfiguration {
        StaticConfiguration(kind: kind, provider: Provider()) { entry in
            LockScreenWidgetView(entry: entry)
        }
        .configurationDisplayName("Alerts")
        .description("Active alerts on your lock screen.")
        .supportedFamilies([.accessoryCircular, .accessoryRectangular])
    }
}

struct LockScreenWidgetView: View {
    @Environment(\.widgetFamily) private var family
    let entry: SnapshotEntry

    private var snapshot: Snapshot { entry.snapshot }

    var body: some View {
        Group {
            switch family {
            case .accessoryCircular: circular
            case .accessoryRectangular: rectangular
            default: inline
            }
        }
        .widgetURL(Links.alerts)
        .modifier(AccessoryBackground())
    }

    private var summary: String {
        if !snapshot.signedIn { return "Not signed in" }
        if snapshot.active == 0 { return "All clear" }
        if snapshot.critical == 0 { return "\(snapshot.warning) warning" }
        if snapshot.warning == 0 { return "\(snapshot.critical) critical" }
        return "\(snapshot.critical) critical · \(snapshot.warning) warning"
    }

    private var inline: some View {
        ViewThatFits {
            Label(summary, systemImage: snapshot.active == 0 ? "checkmark.circle" : "exclamationmark.triangle")
            Text(snapshot.signedIn ? "\(snapshot.active) alerts" : "DashyNMS")
        }
    }

    /// The share of watched devices with a problem - nearly empty on a healthy network.
    private var problemShare: Double {
        guard let counts = snapshot.devices, counts.watched > 0 else {
            return snapshot.active > 0 ? 1 : 0
        }

        return Double(counts.critical + counts.warning) / Double(counts.watched)
    }

    private var circular: some View {
        Gauge(value: min(max(problemShare, 0), 1)) {
            Text("Alerts")
        } currentValueLabel: {
            VStack(spacing: -2) {
                Text(snapshot.signedIn ? Format.count(snapshot.active) : "–")
                    .font(.system(size: 20, weight: .semibold, design: .rounded))
                Text("ALERTS")
                    .font(.system(size: 8, weight: .semibold))
            }
        }
        .gaugeStyle(.accessoryCircularCapacity)
    }

    private var rectangular: some View {
        VStack(alignment: .leading, spacing: 1) {
            HStack(spacing: 4) {
                Image(systemName: "waveform.path.ecg")
                Text(snapshot.hideLockScreenDetails || !snapshot.signedIn ? "DashyNMS" : headline)
                    .lineLimit(1)
            }
            .font(.headline)
            .widgetAccentable()

            if !snapshot.signedIn {
                Text("Open DashyNMS to sign in.")
                    .font(.caption)
            } else if snapshot.hideLockScreenDetails {
                Text(summary)
                    .font(.system(size: 13, weight: .semibold))
                    .lineLimit(1)
                if snapshot.active > 0 {
                    Text("Unlock for details")
                        .font(.caption)
                        .foregroundStyle(.secondary)
                }
            } else if snapshot.activeAlerts.isEmpty {
                Text("All clear")
                    .font(.system(size: 13, weight: .semibold))
            } else {
                ForEach(snapshot.activeAlerts.prefix(2), id: \.alertId) { alert in
                    Text("\(alert.device): \(alert.rule)")
                        .font(.system(size: 12.5, weight: .medium))
                        .lineLimit(1)
                }
            }
        }
        .frame(maxWidth: .infinity, alignment: .leading)
    }

    /// The worst severity's count ("2 critical").
    private var headline: String {
        if snapshot.critical > 0 { return "\(snapshot.critical) critical" }
        if snapshot.warning > 0 { return "\(snapshot.warning) warning" }
        return "All clear"
    }
}
