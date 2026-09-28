import SwiftUI
import WidgetKit

/// DashyNMS's widgets, all drawn from the snapshot the app saves after each
/// alert check. The kinds are what a placed widget is tied to: keep them.
@main
struct DashyNMSWidgets: WidgetBundle {
    var body: some Widget {
        AlertsWidget()
        AlertPieWidget()
        OverviewWidget()
        PinnedDevicesWidget()
        SensorsWidget()
        LockScreenWidget()
    }
}

struct SnapshotEntry: TimelineEntry {
    let date: Date
    let snapshot: Snapshot
}

/// Re-reads the snapshot every 15 minutes (iOS may stretch that), with
/// entries five minutes apart in between so ages ("12 min") keep moving. The
/// app can't ask WidgetKit to reload from .NET - WidgetCenter is Swift-only -
/// so the widget comes to the snapshot instead.
struct Provider: TimelineProvider {
    func placeholder(in context: Context) -> SnapshotEntry {
        SnapshotEntry(date: Date(), snapshot: .preview)
    }

    func getSnapshot(in context: Context, completion: @escaping (SnapshotEntry) -> Void) {
        completion(SnapshotEntry(date: Date(), snapshot: context.isPreview ? .preview : Snapshot.load()))
    }

    func getTimeline(in context: Context, completion: @escaping (Timeline<SnapshotEntry>) -> Void) {
        let now = Date()
        let snapshot = Snapshot.load()
        let entries = [0, 5, 10].map { SnapshotEntry(date: now.addingTimeInterval(TimeInterval($0 * 60)), snapshot: snapshot) }
        completion(Timeline(entries: entries, policy: .after(now.addingTimeInterval(15 * 60))))
    }
}

/// Alerts in full: the worst alert (small), three with counts (medium), or
/// six with their rules wrapped in full (large).
struct AlertsWidget: Widget {
    let kind = "DashyNMSAlerts"

    var body: some WidgetConfiguration {
        StaticConfiguration(kind: kind, provider: Provider()) { entry in
            AlertsWidgetView(entry: entry)
        }
        .configurationDisplayName("Alerts")
        .description("Open alerts, worst first, from your last alert check.")
        .supportedFamilies([.systemSmall, .systemMedium, .systemLarge])
    }
}

struct AlertsWidgetView: View {
    @Environment(\.widgetFamily) private var family
    let entry: SnapshotEntry

    private var snapshot: Snapshot { entry.snapshot }

    var body: some View {
        Group {
            if !snapshot.signedIn {
                SignedOutView()
            } else {
                switch family {
                case .systemSmall: small
                case .systemLarge: large
                default: medium
                }
            }
        }
        .frame(maxWidth: .infinity, maxHeight: .infinity, alignment: .topLeading)
        .widgetURL(Links.alerts)
        .modifier(WidgetBackground())
    }

    // MARK: Small

    private var small: some View {
        VStack(alignment: .leading, spacing: 5) {
            HStack {
                Eyebrow(text: "Alerts")
                Spacer()
                HStack(spacing: 7) {
                    Text(Format.count(snapshot.critical)).foregroundColor(.dnCriticalText)
                    Text(Format.count(snapshot.warning)).foregroundColor(.dnWarningText)
                    Text(Format.count(snapshot.acknowledged)).foregroundColor(.dnAcknowledgedText)
                }
                .font(.system(size: 12, weight: .semibold, design: .monospaced))
            }

            if let worst = snapshot.activeAlerts.first {
                Text(worst.severity == "critical" ? "CRITICAL" : "WARNING")
                    .font(.system(size: 10.5, weight: .bold))
                    .tracking(0.6)
                    .foregroundColor(worst.severity == "critical" ? .dnCriticalText : .dnWarningText)
                    .padding(.horizontal, 7)
                    .padding(.vertical, 2)
                    .background(RoundedRectangle(cornerRadius: 6).fill(worst.severity == "critical" ? Color.dnCriticalTile : Color.dnWarningTile))
                Text(worst.rule)
                    .font(.system(size: 16, weight: .semibold))
                    .foregroundColor(.dnText)
                    .lineLimit(3)
                    .minimumScaleFactor(0.85)
                Spacer(minLength: 0)
                Text(worst.device)
                    .font(.system(size: 12.5))
                    .foregroundColor(.dnText.opacity(0.85))
                    .lineLimit(1)
                Text(smallFooter(worst))
                    .font(.system(size: 11.5))
                    .foregroundColor(.dnSecondary)
                    .lineLimit(1)
            } else {
                Spacer(minLength: 0)
                EmptyMessage(text: "No active alerts.", systemImage: "checkmark.circle.fill")
                Spacer(minLength: 0)
            }
        }
    }

    private func smallFooter(_ alert: WidgetAlert) -> String {
        let age = Format.age(alert.raisedAt, now: entry.date)
        let more = snapshot.active - 1
        let parts = [age.isEmpty ? nil : (age == "just now" ? age : "\(age) ago"), more > 0 ? "\(more) more" : nil].compactMap { $0 }
        return parts.joined(separator: " · ")
    }

    // MARK: Medium

    private var medium: some View {
        VStack(alignment: .leading, spacing: 7) {
            HStack(spacing: 10) {
                Text("\(snapshot.critical) critical").foregroundColor(.dnCriticalText)
                Text("\(snapshot.warning) warning").foregroundColor(.dnWarningText)
                if snapshot.acknowledged > 0 {
                    Text("\(snapshot.acknowledged) ack'd").foregroundColor(.dnAcknowledgedText)
                }
                Spacer(minLength: 0)
                Text(Format.age(snapshot.checkedAt, now: entry.date))
                    .foregroundColor(.dnSecondary)
                    .font(.system(size: 11))
            }
            .font(.system(size: 12, weight: .semibold))
            .lineLimit(1)

            if snapshot.activeAlerts.isEmpty {
                Spacer(minLength: 0)
                EmptyMessage(text: "No active alerts.", systemImage: "checkmark.circle.fill")
                Spacer(minLength: 0)
            } else {
                ForEach(snapshot.activeAlerts.prefix(3), id: \.alertId) { alert in
                    Link(destination: Links.alert(alert)) {
                        HStack(alignment: .top, spacing: 9) {
                            Dot(color: .state(alert.severity)).padding(.top, 5)
                            VStack(alignment: .leading, spacing: 0) {
                                Text(alert.rule)
                                    .font(.system(size: 13, weight: .semibold))
                                    .foregroundColor(.dnText)
                                    .lineLimit(1)
                                Text(alert.device)
                                    .font(.system(size: 11.5))
                                    .foregroundColor(.dnSecondary)
                                    .lineLimit(1)
                            }
                            Spacer(minLength: 4)
                            Text(Format.age(alert.raisedAt, now: entry.date))
                                .font(.system(size: 11.5))
                                .foregroundColor(.dnSecondary)
                        }
                    }
                }
                Spacer(minLength: 0)
            }
        }
    }

    // MARK: Large

    private var large: some View {
        VStack(alignment: .leading, spacing: 11) {
            HStack(alignment: .firstTextBaseline) {
                Text("Alerts")
                    .font(.system(size: 15, weight: .semibold))
                    .foregroundColor(.dnText)
                Spacer()
                Text(Format.checked(snapshot.checkedAt, now: entry.date))
                    .font(.system(size: 11))
                    .foregroundColor(.dnSecondary)
            }

            HStack(spacing: 8) {
                CountTile(value: snapshot.critical, label: "Critical", text: .dnCriticalText, tile: .dnCriticalTile)
                CountTile(value: snapshot.warning, label: "Warning", text: .dnWarningText, tile: .dnWarningTile)
                CountTile(value: snapshot.acknowledged, label: "Acknowledged", text: .dnAcknowledgedText, tile: .dnAcknowledgedTile)
            }

            if snapshot.alerts.isEmpty {
                Spacer(minLength: 0)
                EmptyMessage(text: "No open alerts.", systemImage: "checkmark.circle.fill")
                Spacer(minLength: 0)
            } else {
                VStack(alignment: .leading, spacing: 9) {
                    ForEach(snapshot.alerts.prefix(6), id: \.alertId) { alert in
                        Link(destination: Links.alert(alert)) {
                            HStack(alignment: .top, spacing: 9) {
                                Dot(color: alert.acknowledged ? .dnAcknowledged : .state(alert.severity)).padding(.top, 5)
                                VStack(alignment: .leading, spacing: 1) {
                                    Text(alert.rule)
                                        .font(.system(size: 13, weight: .semibold))
                                        .foregroundColor(.dnText)
                                        .lineLimit(2)
                                        .fixedSize(horizontal: false, vertical: true)
                                    Text(largeDetail(alert))
                                        .font(.system(size: 11.5))
                                        .foregroundColor(.dnSecondary)
                                        .lineLimit(1)
                                }
                                Spacer(minLength: 0)
                            }
                            .opacity(alert.acknowledged ? 0.6 : 1)
                        }
                    }
                }
                Spacer(minLength: 0)
                let more = snapshot.critical + snapshot.warning + snapshot.acknowledged - min(snapshot.alerts.count, 6)
                if more > 0 {
                    Text("\(more) more in the app")
                        .font(.system(size: 11))
                        .foregroundColor(.dnSecondary)
                }
            }
        }
    }

    private func largeDetail(_ alert: WidgetAlert) -> String {
        let age = Format.age(alert.raisedAt, now: entry.date)
        return [alert.device, age.isEmpty ? nil : age, alert.acknowledged ? "Acknowledged" : nil]
            .compactMap { $0 }
            .joined(separator: " · ")
    }
}
