import Foundation

/// One alert as a widget row. Field names match the app's WidgetAlert
/// (src/DashyNMS.Mobile.Shared/Widgets/WidgetSnapshot.cs): change both together.
struct WidgetAlert: Codable, Hashable {
    let alertId: Int
    let deviceId: Int
    let severity: String
    let rule: String
    let device: String
}

/// What the app saved after its last alert check. The widget never talks to
/// LibreNMS itself and holds no token: it only draws this.
struct Snapshot: Codable {
    var signedIn: Bool = false
    var critical: Int = 0
    var warning: Int = 0
    var acknowledged: Int = 0
    var devicesDown: Int? = nil
    var alerts: [WidgetAlert] = []
    /// Unix seconds.
    var checkedAt: Int64 = 0

    static let appGroup = "group.net.pckp.DashyNMS"
    static let fileName = "widget-snapshot.json"

    var active: Int { critical + warning }

    var checkedDate: Date? {
        checkedAt > 0 ? Date(timeIntervalSince1970: TimeInterval(checkedAt)) : nil
    }

    /// The saved snapshot, or a signed-out one when there's none yet.
    static func load() -> Snapshot {
        guard
            let folder = FileManager.default.containerURL(forSecurityApplicationGroupIdentifier: appGroup),
            let data = try? Data(contentsOf: folder.appendingPathComponent(fileName)),
            let snapshot = try? JSONDecoder().decode(Snapshot.self, from: data)
        else {
            return Snapshot()
        }

        return snapshot
    }

    /// For the widget gallery.
    static let preview = Snapshot(
        signedIn: true,
        critical: 2,
        warning: 3,
        acknowledged: 1,
        devicesDown: 1,
        alerts: [
            WidgetAlert(alertId: 1, deviceId: 1, severity: "critical", rule: "Device down", device: "core-sw-01"),
            WidgetAlert(alertId: 2, deviceId: 2, severity: "critical", rule: "Port errors", device: "edge-rtr"),
            WidgetAlert(alertId: 3, deviceId: 3, severity: "warning", rule: "High temperature", device: "dist-sw-04"),
        ],
        checkedAt: Int64(Date().timeIntervalSince1970))
}
