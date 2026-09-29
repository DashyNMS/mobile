import SwiftUI
import WidgetKit

/// Every device once, by its worst open alert: critical, warning,
/// acknowledged only, or OK - a ring that's all green when all is well.
struct AlertPieWidget: Widget {
    let kind = "DashyNMSAlertPie"

    var body: some WidgetConfiguration {
        StaticConfiguration(kind: kind, provider: Provider()) { entry in
            AlertPieWidgetView(entry: entry)
        }
        .configurationDisplayName("Alert pie chart")
        .description("Your devices by their worst alert: critical, warning, acknowledged or OK.")
        .supportedFamilies([.systemSmall])
    }
}

/// A ring of slices, drawn with trimmed circles (Swift Charts' sector marks
/// need iOS 17; the widget supports 16).
struct Donut: View {
    let counts: DeviceCounts
    let lineWidth: CGFloat

    private var segments: [(color: Color, start: Double, end: Double)] {
        let slices = Format.pieSlices(counts)
        let gap = slices.count > 1 ? 0.008 : 0
        var start = 0.0
        return slices.map { slice -> (color: Color, start: Double, end: Double) in
            defer { start += slice.fraction }
            let from = start + gap / 2
            let to = max(from + 0.004, start + slice.fraction - gap / 2)
            return (color: Color.state(slice.state), start: from, end: to)
        }
    }

    var body: some View {
        ZStack {
            Circle().stroke(Color.dnTrack, lineWidth: lineWidth)
            ForEach(Array(segments.enumerated()), id: \.offset) { _, segment in
                Circle()
                    .trim(from: segment.start, to: segment.end)
                    .stroke(segment.color, style: StrokeStyle(lineWidth: lineWidth, lineCap: .butt))
                    .rotationEffect(.degrees(-90))
            }
        }
        .padding(lineWidth / 2)
    }
}

struct PieLegend: View {
    let counts: DeviceCounts

    var body: some View {
        VStack(alignment: .leading, spacing: 7) {
            row("Critical", counts.critical, .dnCritical)
            row("Warning", counts.warning, .dnWarning)
            row("Acknowledged", counts.acknowledged, .dnAcknowledged)
            row("OK", counts.ok, .dnOk)
        }
    }

    private func row(_ label: String, _ value: Int, _ color: Color) -> some View {
        HStack(spacing: 8) {
            RoundedRectangle(cornerRadius: 3).fill(color).frame(width: 10, height: 10)
            Text(label)
                .font(.system(size: 13))
                .foregroundColor(.dnText)
                .lineLimit(1)
            Spacer(minLength: 4)
            Text(String(value))
                .font(.system(size: 13, weight: .semibold, design: .monospaced))
                .foregroundColor(.dnText)
        }
    }
}

struct AlertPieWidgetView: View {
    @Environment(\.widgetFamily) private var family
    let entry: SnapshotEntry

    private var snapshot: Snapshot { entry.snapshot }

    var body: some View {
        Group {
            if !snapshot.signedIn {
                SignedOutView()
            } else if let counts = snapshot.devices {
                switch family {
                case .systemSmall: small(counts)
                case .systemLarge: large(counts)
                default: medium(counts)
                }
            } else {
                VStack(alignment: .leading, spacing: 6) {
                    Eyebrow(text: "Devices")
                    EmptyMessage(text: "Waiting for the device list - open DashyNMS.")
                    Spacer(minLength: 0)
                }
            }
        }
        .frame(maxWidth: .infinity, maxHeight: .infinity, alignment: .topLeading)
        .widgetURL(Links.alerts)
        .modifier(WidgetBackground())
    }

    private func centre(_ value: String, _ label: String, size: CGFloat) -> some View {
        VStack(spacing: 0) {
            Text(value)
                .font(.system(size: size, weight: .semibold, design: .monospaced))
                .foregroundColor(.dnText)
                .lineLimit(1)
                .minimumScaleFactor(0.5)
            Text(label)
                .font(.system(size: 10.5))
                .foregroundColor(.dnSecondary)
                .lineLimit(1)
        }
        .padding(.horizontal, 16)
    }

    /// As the mock-up: the ring filling the widget, the device count inside.
    private func small(_ counts: DeviceCounts) -> some View {
        ZStack {
            Donut(counts: counts, lineWidth: 14)
            centre(String(counts.total), "devices", size: 24)
        }
        .padding(4)
        .frame(maxWidth: .infinity, maxHeight: .infinity)
    }

    private func percentOk(_ counts: DeviceCounts) -> String {
        guard counts.watched > 0 else { return "–" }
        return "\(Int((Double(counts.ok) / Double(counts.watched) * 100).rounded(.down)))%"
    }

    private func medium(_ counts: DeviceCounts) -> some View {
        HStack(spacing: 16) {
            ZStack {
                Donut(counts: counts, lineWidth: 14)
                centre(percentOk(counts), "OK", size: 22)
            }
            .aspectRatio(1, contentMode: .fit)
            VStack(alignment: .leading, spacing: 6) {
                PieLegend(counts: counts)
                Text("\(counts.watched) devices · \(Format.age(snapshot.checkedAt, now: entry.date))")
                    .font(.system(size: 11))
                    .foregroundColor(.dnSecondary)
                    .lineLimit(1)
            }
        }
    }

    /// The devices behind the red and amber: each device's worst active alert, once.
    private var devicesNeedingALook: [WidgetAlert] {
        var seen = Set<Int>()
        return snapshot.activeAlerts.filter { seen.insert($0.deviceId).inserted }
    }

    private func large(_ counts: DeviceCounts) -> some View {
        VStack(alignment: .leading, spacing: 12) {
            HStack(spacing: 16) {
                ZStack {
                    Donut(counts: counts, lineWidth: 14)
                    centre(counts.watched > 0 ? "\(counts.ok)/\(counts.watched)" : "–", "devices OK", size: 22)
                }
                .frame(width: 136, height: 136)
                PieLegend(counts: counts)
            }

            Rectangle().fill(Color.dnTrack).frame(height: 1)
            Eyebrow(text: "Needing a look")

            if devicesNeedingALook.isEmpty {
                EmptyMessage(text: "Every device is OK.", systemImage: "checkmark.circle.fill")
            } else {
                VStack(alignment: .leading, spacing: 8) {
                    ForEach(devicesNeedingALook.prefix(5), id: \.alertId) { alert in
                        Link(destination: Links.alert(alert)) {
                            HStack(spacing: 9) {
                                Dot(color: .state(alert.severity))
                                Text(alert.device)
                                    .font(.system(size: 13, weight: .semibold))
                                    .foregroundColor(.dnText)
                                    .lineLimit(1)
                                    .frame(width: 104, alignment: .leading)
                                Text(alert.rule)
                                    .font(.system(size: 12))
                                    .foregroundColor(.dnSecondary)
                                    .lineLimit(1)
                                Spacer(minLength: 0)
                            }
                        }
                    }
                }
            }
            Spacer(minLength: 0)
        }
    }
}
