import SwiftUI
import WidgetKit

/// DashyNMS's widgets, all drawn from the snapshot the app saves after each
/// alert check. The kinds are what a placed widget is tied to: keep them.
///
/// Only the mock-ups' set for now: Alerts (small, medium), the alert pie
/// (small), Overview (large) and the lock screen (circular, rectangular).
/// Pinned devices and Sensors are left out of the gallery until they're
/// designed too; their code stays.
@main
struct DashyNMSWidgets: WidgetBundle {
    var body: some Widget {
        AlertsWidget()
        AlertPieWidget()
        OverviewWidget()
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

/// Alerts, as the mock-ups: the worst alert (small), or the counts beside
/// the three worst (medium).
struct AlertsWidget: Widget {
    let kind = "DashyNMSAlerts"

    var body: some WidgetConfiguration {
        StaticConfiguration(kind: kind, provider: Provider()) { entry in
            AlertsWidgetView(entry: entry)
        }
        .configurationDisplayName("Alerts")
        .description("Open alerts, worst first, from your last alert check.")
        .supportedFamilies([.systemSmall, .systemMedium])
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
            } else if family == .systemSmall {
                small
            } else {
                medium
            }
        }
        .frame(maxWidth: .infinity, maxHeight: .infinity, alignment: .topLeading)
        .widgetURL(Links.alerts)
        .modifier(WidgetBackground())
    }

    // MARK: Small

    /// The worst alert: its severity as a pill, the device, the rule, and
    /// how old it is with the counts underneath.
    private var small: some View {
        VStack(alignment: .leading, spacing: 3) {
            if let worst = snapshot.activeAlerts.first {
                SeverityPill(severity: worst.severity)
                    .padding(.bottom, 5)
                Text(worst.device)
                    .font(.system(size: 17, weight: .semibold))
                    .foregroundColor(.dnText)
                    .lineLimit(1)
                    .minimumScaleFactor(0.8)
                Text(worst.rule)
                    .font(.system(size: 13))
                    .foregroundColor(.dnSecondary)
                    .lineLimit(2)
                Spacer(minLength: 0)
                Text(smallFooter(worst))
                    .font(.system(size: 11))
                    .foregroundColor(.dnFaint)
                    .lineLimit(1)
                    .minimumScaleFactor(0.8)
            } else {
                Spacer(minLength: 0)
                EmptyMessage(text: "No active alerts.", systemImage: "checkmark.circle.fill")
                Spacer(minLength: 0)
            }
        }
    }

    /// "4m · 3 critical, 7 warning".
    private func smallFooter(_ alert: WidgetAlert) -> String {
        let age = Format.age(alert.raisedAt, now: entry.date)
        let counts = "\(snapshot.critical) critical, \(snapshot.warning) warning"
        return age.isEmpty ? counts : "\(age == "just now" ? "now" : age) · \(counts)"
    }

    // MARK: Medium

    /// The counts in a column at the left, the three worst alerts beside them.
    private var medium: some View {
        HStack(alignment: .top, spacing: 16) {
            VStack(alignment: .leading, spacing: 0) {
                BigCount(value: snapshot.critical, label: "Critical", color: .dnCriticalText)
                Spacer(minLength: 0)
                BigCount(value: snapshot.warning, label: "Warning", color: .dnWarningText)
            }
            .frame(width: 70, alignment: .leading)

            if snapshot.activeAlerts.isEmpty {
                VStack {
                    Spacer(minLength: 0)
                    EmptyMessage(text: "No active alerts.", systemImage: "checkmark.circle.fill")
                    Spacer(minLength: 0)
                }
            } else {
                VStack(alignment: .leading, spacing: 0) {
                    ForEach(Array(snapshot.activeAlerts.prefix(3).enumerated()), id: \.element.alertId) { index, alert in
                        if index > 0 {
                            Spacer(minLength: 0)
                        }
                        Link(destination: Links.alert(alert)) {
                            HStack(alignment: .firstTextBaseline, spacing: 8) {
                                Dot(color: .state(alert.severity))
                                VStack(alignment: .leading, spacing: 0) {
                                    Text(alert.device)
                                        .font(.system(size: 14, weight: .semibold))
                                        .foregroundColor(.dnText)
                                        .lineLimit(1)
                                    Text(alert.rule)
                                        .font(.system(size: 12))
                                        .foregroundColor(.dnSecondary)
                                        .lineLimit(1)
                                }
                            }
                        }
                    }
                }
            }
        }
    }
}

/// A severity as the mock-ups' tinted pill: "Critical".
struct SeverityPill: View {
    let severity: String

    var body: some View {
        Text(severity == "critical" ? "Critical" : severity == "warning" ? "Warning" : "OK")
            .font(.system(size: 11, weight: .semibold))
            .foregroundColor(Color.stateText(severity))
            .padding(.horizontal, 8)
            .padding(.vertical, 2)
            .background(Capsule().fill(Color.state(severity).opacity(0.18)))
    }
}

/// A count over its label, as the medium Alerts widget's column.
struct BigCount: View {
    let value: Int
    let label: String
    let color: Color

    var body: some View {
        VStack(alignment: .leading, spacing: 0) {
            Text(Format.count(value))
                .font(.system(size: 22, weight: .bold))
                .foregroundColor(color)
                .lineLimit(1)
                .minimumScaleFactor(0.6)
            Text(label)
                .font(.system(size: 11))
                .foregroundColor(.dnSecondary)
        }
    }
}
