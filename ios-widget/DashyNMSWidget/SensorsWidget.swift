import SwiftUI
import WidgetKit

/// The dashboard Sensors card's sensors, coloured against the app's thresholds.
struct SensorsWidget: Widget {
    let kind = "DashyNMSSensors"

    var body: some WidgetConfiguration {
        StaticConfiguration(kind: kind, provider: Provider()) { entry in
            SensorsWidgetView(entry: entry)
        }
        .configurationDisplayName("Sensors")
        .description("The sensors on your dashboard's Sensors card.")
        .supportedFamilies([.systemMedium, .systemLarge])
    }
}

/// Where a reading sits between its limits.
struct LimitBar: View {
    let position: Double
    let color: Color

    var body: some View {
        GeometryReader { geometry in
            ZStack(alignment: .leading) {
                Capsule().fill(Color.dnTrack)
                Capsule()
                    .fill(color)
                    .frame(width: max(4, geometry.size.width * CGFloat(min(max(position, 0), 1))))
            }
        }
        .frame(height: 4)
    }
}

struct SensorsWidgetView: View {
    @Environment(\.widgetFamily) private var family
    let entry: SnapshotEntry

    private var snapshot: Snapshot { entry.snapshot }

    var body: some View {
        Group {
            if !snapshot.signedIn {
                SignedOutView()
            } else if snapshot.sensors.isEmpty {
                VStack(alignment: .leading, spacing: 6) {
                    Eyebrow(text: "Sensors")
                    EmptyMessage(text: "Pick sensors for the dashboard's Sensors card in DashyNMS to see them here.")
                    Spacer(minLength: 0)
                }
            } else if family == .systemLarge {
                large
            } else {
                medium
            }
        }
        .frame(maxWidth: .infinity, maxHeight: .infinity, alignment: .topLeading)
        .widgetURL(Links.alerts)
        .modifier(WidgetBackground())
    }

    private func valueColour(_ sensor: SensorReading) -> Color {
        switch sensor.status {
        case "critical": return .dnCriticalText
        case "warning": return .dnWarningText
        case "unknown": return .dnSecondary
        default: return .dnText
        }
    }

    private func tile(_ sensor: SensorReading) -> some View {
        Link(destination: Links.device(sensor.deviceId)) {
            VStack(alignment: .leading, spacing: 0) {
                Text(sensor.value)
                    .font(.system(size: 17, weight: .semibold, design: .monospaced))
                    .foregroundColor(valueColour(sensor))
                    .lineLimit(1)
                    .minimumScaleFactor(0.6)
                Text(sensor.name)
                    .font(.system(size: 11.5, weight: .semibold))
                    .foregroundColor(.dnText)
                    .lineLimit(1)
                Text(sensor.device)
                    .font(.system(size: 11))
                    .foregroundColor(.dnSecondary)
                    .lineLimit(1)
            }
            .padding(.horizontal, 10)
            .padding(.vertical, 6)
            .frame(maxWidth: .infinity, maxHeight: .infinity, alignment: .leading)
            .background(RoundedRectangle(cornerRadius: 12).fill(Color.dnTile))
        }
    }

    /// Four readings, two by two.
    private var medium: some View {
        let sensors = Array(snapshot.sensors.prefix(4))
        return VStack(spacing: 8) {
            ForEach(Array(stride(from: 0, to: sensors.count, by: 2)), id: \.self) { start in
                HStack(spacing: 8) {
                    tile(sensors[start])
                    if start + 1 < sensors.count {
                        tile(sensors[start + 1])
                    } else {
                        Color.clear.frame(maxWidth: .infinity)
                    }
                }
            }
            if sensors.count <= 2 {
                Spacer(minLength: 0)
            }
        }
    }

    private var large: some View {
        VStack(alignment: .leading, spacing: 9) {
            HStack(alignment: .firstTextBaseline) {
                Text("Sensors")
                    .font(.system(size: 15, weight: .semibold))
                    .foregroundColor(.dnText)
                Spacer()
                Text(Format.checked(snapshot.sensorsReadAt, now: entry.date, verb: "Read"))
                    .font(.system(size: 11))
                    .foregroundColor(.dnSecondary)
            }

            VStack(alignment: .leading, spacing: 8) {
                ForEach(snapshot.sensors.prefix(7), id: \.sensorId) { sensor in
                    Link(destination: Links.device(sensor.deviceId)) {
                        VStack(alignment: .leading, spacing: 4) {
                            HStack(alignment: .firstTextBaseline, spacing: 8) {
                                (Text(sensor.name).font(.system(size: 12.5, weight: .semibold)).foregroundColor(.dnText)
                                    + Text(" · \(sensor.device)").font(.system(size: 12.5)).foregroundColor(.dnSecondary))
                                    .lineLimit(1)
                                Spacer(minLength: 4)
                                Text(sensor.value)
                                    .font(.system(size: 13, weight: .semibold, design: .monospaced))
                                    .foregroundColor(valueColour(sensor))
                                    .lineLimit(1)
                                    .layoutPriority(1)
                            }
                            if let position = sensor.position {
                                LimitBar(position: position, color: .state(sensor.status))
                            }
                        }
                    }
                }
            }
            Spacer(minLength: 0)
        }
    }
}
