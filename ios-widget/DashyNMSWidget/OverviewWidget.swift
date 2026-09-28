import SwiftUI
import WidgetKit

/// The dashboard in one glance: devices, alert counts and the top alerts.
struct OverviewWidget: Widget {
    let kind = "DashyNMSOverview"

    var body: some WidgetConfiguration {
        StaticConfiguration(kind: kind, provider: Provider()) { entry in
            OverviewWidgetView(entry: entry)
        }
        .configurationDisplayName("Overview")
        .description("Devices up and down, alert counts and the top alerts.")
        .supportedFamilies([.systemLarge])
    }
}

/// Devices as one bar, split by worst alert.
struct DeviceBar: View {
    let counts: DeviceCounts

    var body: some View {
        GeometryReader { geometry in
            let slices = Format.pieSlices(counts)
            let spacing: CGFloat = slices.count > 1 ? 2 : 0
            let usable = max(geometry.size.width - spacing * CGFloat(max(slices.count - 1, 0)), 0)
            HStack(spacing: spacing) {
                ForEach(Array(slices.enumerated()), id: \.offset) { _, slice in
                    Rectangle()
                        .fill(Color.state(slice.state))
                        .frame(width: usable * CGFloat(slice.fraction))
                }
            }
        }
        .frame(height: 10)
        .background(Color.dnTrack)
        .clipShape(Capsule())
    }
}

struct OverviewWidgetView: View {
    let entry: SnapshotEntry

    private var snapshot: Snapshot { entry.snapshot }

    var body: some View {
        Group {
            if !snapshot.signedIn {
                SignedOutView()
            } else {
                content
            }
        }
        .frame(maxWidth: .infinity, maxHeight: .infinity, alignment: .topLeading)
        .widgetURL(Links.alerts)
        .modifier(WidgetBackground())
    }

    private var content: some View {
        VStack(alignment: .leading, spacing: 11) {
            HStack(alignment: .firstTextBaseline) {
                Text("Network")
                    .font(.system(size: 15, weight: .semibold))
                    .foregroundColor(.dnText)
                Spacer()
                Text(Format.checked(snapshot.checkedAt, now: entry.date))
                    .font(.system(size: 11))
                    .foregroundColor(.dnSecondary)
            }

            if let counts = snapshot.devices {
                VStack(alignment: .leading, spacing: 5) {
                    DeviceBar(counts: counts)
                    HStack {
                        Text(deviceLine(counts))
                        Spacer()
                        Text("\(counts.total) devices")
                    }
                    .font(.system(size: 11.5))
                    .foregroundColor(.dnSecondary)
                    .lineLimit(1)
                }
            }

            HStack(spacing: 6) {
                CountTile(value: snapshot.critical, label: "Critical", text: .dnCriticalText, tile: .dnCriticalTile, size: 19)
                CountTile(value: snapshot.warning, label: "Warning", text: .dnWarningText, tile: .dnWarningTile, size: 19)
                CountTile(value: snapshot.acknowledged, label: "Ack'd", text: .dnAcknowledgedText, tile: .dnAcknowledgedTile, size: 19)
                let down = snapshot.devicesDown ?? 0
                CountTile(
                    value: down,
                    label: "Down",
                    text: down > 0 ? .dnCriticalText : .dnText,
                    tile: down > 0 ? .dnCriticalTile : .dnTile,
                    size: 19)
            }

            Rectangle().fill(Color.dnTrack).frame(height: 1)
            Eyebrow(text: "Top alerts")

            if snapshot.activeAlerts.isEmpty {
                EmptyMessage(text: "No active alerts.", systemImage: "checkmark.circle.fill")
            } else {
                VStack(alignment: .leading, spacing: 8) {
                    ForEach(snapshot.activeAlerts.prefix(5), id: \.alertId) { alert in
                        Link(destination: Links.alert(alert)) {
                            HStack(spacing: 9) {
                                Dot(color: .state(alert.severity))
                                Text(alert.rule)
                                    .font(.system(size: 13, weight: .semibold))
                                    .foregroundColor(.dnText)
                                    .lineLimit(1)
                                Spacer(minLength: 4)
                                Text(trailing(alert))
                                    .font(.system(size: 11.5))
                                    .foregroundColor(.dnSecondary)
                                    .lineLimit(1)
                                    .layoutPriority(1)
                            }
                        }
                    }
                }
            }
            Spacer(minLength: 0)
        }
    }

    private func deviceLine(_ counts: DeviceCounts) -> String {
        var parts = ["\(counts.up) up", "\(counts.down) down"]
        if counts.disabled > 0 { parts.append("\(counts.disabled) disabled") }
        return parts.joined(separator: " · ")
    }

    private func trailing(_ alert: WidgetAlert) -> String {
        let age = Format.age(alert.raisedAt, now: entry.date)
        return age.isEmpty ? alert.device : "\(alert.device) · \(age)"
    }
}
