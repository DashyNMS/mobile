import SwiftUI
import WidgetKit

/// The devices pinned in the app, in pinned order: up or down, and what's wrong.
struct PinnedDevicesWidget: Widget {
    let kind = "DashyNMSPinned"

    var body: some WidgetConfiguration {
        StaticConfiguration(kind: kind, provider: Provider()) { entry in
            PinnedDevicesWidgetView(entry: entry)
        }
        .configurationDisplayName("Pinned devices")
        .description("The devices you've pinned in DashyNMS, and how they are.")
        .supportedFamilies([.systemMedium, .systemLarge])
    }
}

struct PinnedDevicesWidgetView: View {
    @Environment(\.widgetFamily) private var family
    let entry: SnapshotEntry

    private var snapshot: Snapshot { entry.snapshot }

    var body: some View {
        Group {
            if !snapshot.signedIn {
                SignedOutView()
            } else if snapshot.pinned.isEmpty {
                VStack(alignment: .leading, spacing: 6) {
                    Eyebrow(text: "Pinned")
                    EmptyMessage(text: "Pin devices in DashyNMS to see them here.")
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

    private func tile(_ device: PinnedDevice) -> some View {
        Link(destination: Links.device(device.deviceId)) {
            VStack(alignment: .leading, spacing: 2) {
                HStack(spacing: 7) {
                    Dot(color: .state(device.state))
                    Text(device.name)
                        .font(.system(size: 13, weight: .semibold))
                        .foregroundColor(.dnText)
                        .lineLimit(1)
                }
                Text(Format.deviceStatus(device, now: entry.date))
                    .font(.system(size: 11.5))
                    .foregroundColor(.stateText(device.state))
                    .lineLimit(1)
            }
            .padding(.horizontal, 10)
            .padding(.vertical, 8)
            .frame(maxWidth: .infinity, maxHeight: .infinity, alignment: .leading)
            .background(RoundedRectangle(cornerRadius: 12).fill(Color.dnTile))
        }
    }

    /// Four devices, two by two.
    private var medium: some View {
        let devices = Array(snapshot.pinned.prefix(4))
        return VStack(spacing: 8) {
            ForEach(Array(stride(from: 0, to: devices.count, by: 2)), id: \.self) { start in
                HStack(spacing: 8) {
                    tile(devices[start])
                    if start + 1 < devices.count {
                        tile(devices[start + 1])
                    } else {
                        Color.clear.frame(maxWidth: .infinity)
                    }
                }
            }
            if devices.count <= 2 {
                Spacer(minLength: 0)
            }
        }
    }

    private var large: some View {
        VStack(alignment: .leading, spacing: 9) {
            HStack(alignment: .firstTextBaseline) {
                Text("Pinned")
                    .font(.system(size: 15, weight: .semibold))
                    .foregroundColor(.dnText)
                Spacer()
                Text(upDown)
                    .font(.system(size: 11))
                    .foregroundColor(.dnSecondary)
            }

            VStack(alignment: .leading, spacing: 8) {
                ForEach(snapshot.pinned.prefix(8), id: \.deviceId) { device in
                    Link(destination: Links.device(device.deviceId)) {
                        HStack(spacing: 10) {
                            Dot(color: .state(device.state))
                            VStack(alignment: .leading, spacing: 0) {
                                Text(device.name)
                                    .font(.system(size: 13, weight: .semibold))
                                    .foregroundColor(.dnText)
                                    .lineLimit(1)
                                Text(Format.deviceStatus(device, now: entry.date))
                                    .font(.system(size: 11.5))
                                    .foregroundColor(.stateText(device.state))
                                    .lineLimit(1)
                            }
                            Spacer(minLength: 4)
                            if let location = device.location {
                                Text(location)
                                    .font(.system(size: 11))
                                    .foregroundColor(.dnSecondary)
                                    .lineLimit(1)
                            }
                        }
                    }
                }
            }
            Spacer(minLength: 0)
        }
    }

    private var upDown: String {
        let down = snapshot.pinned.filter { $0.state == "down" }.count
        let up = snapshot.pinned.filter { $0.state != "down" && $0.state != "disabled" }.count
        return down > 0 ? "\(up) up · \(down) down" : "\(up) up"
    }
}
