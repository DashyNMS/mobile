import Foundation

/// One alert as a widget row. Field names match the app's WidgetAlert
/// (src/DashyNMS.Mobile.Shared/Widgets/WidgetSnapshot.cs): change both together.
struct WidgetAlert: Codable, Hashable {
    var alertId: Int
    var deviceId: Int
    var severity: String
    var rule: String
    var device: String
    var acknowledged: Bool = false
    /// Unix seconds; 0 if LibreNMS didn't say.
    var raisedAt: Int64 = 0
}

extension WidgetAlert {
    /// Tolerant of snapshots saved before acknowledged/raisedAt existed.
    init(from decoder: Decoder) throws {
        let c = try decoder.container(keyedBy: CodingKeys.self)
        alertId = try c.decode(Int.self, forKey: .alertId)
        deviceId = try c.decode(Int.self, forKey: .deviceId)
        severity = try c.decodeIfPresent(String.self, forKey: .severity) ?? "ok"
        rule = try c.decodeIfPresent(String.self, forKey: .rule) ?? ""
        device = try c.decodeIfPresent(String.self, forKey: .device) ?? ""
        acknowledged = try c.decodeIfPresent(Bool.self, forKey: .acknowledged) ?? false
        raisedAt = try c.decodeIfPresent(Int64.self, forKey: .raisedAt) ?? 0
    }
}

/// Every device once, by state - see WidgetDeviceCounts in the app.
struct DeviceCounts: Codable, Hashable {
    var total: Int
    var up: Int
    var down: Int
    var disabled: Int
    var critical: Int
    var warning: Int
    var acknowledged: Int
    var ok: Int

    /// Devices that aren't disabled - the pie's whole.
    var watched: Int { critical + warning + acknowledged + ok }

    /// Devices with an open alert.
    var needingALook: Int { critical + warning + acknowledged }
}

/// A pinned device. `state` is "down", "critical", "warning", "acknowledged", "ok" or "disabled".
struct PinnedDevice: Codable, Hashable {
    var deviceId: Int
    var name: String
    var state: String
    var status: String
    var location: String?
    /// For a down device, when its newest alert was raised (Unix seconds); else 0.
    var since: Int64
}

/// A dashboard sensor, already read against the app's thresholds.
struct SensorReading: Codable, Hashable {
    var sensorId: Int
    var deviceId: Int
    var name: String
    var device: String
    var value: String
    /// "critical", "warning", "ok" or "unknown".
    var status: String
    /// 0 to 1 between its limits; nil when it has none.
    var position: Double?
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
    var devices: DeviceCounts? = nil
    var pinned: [PinnedDevice] = []
    var sensors: [SensorReading] = []
    /// Unix seconds.
    var sensorsReadAt: Int64 = 0
    var hideLockScreenDetails: Bool = true
    /// Unix seconds.
    var checkedAt: Int64 = 0

    static let appGroup = "group.net.pckp.DashyNMS"
    static let fileName = "widget-snapshot.json"

    var active: Int { critical + warning }

    /// Unacknowledged alerts, worst first.
    var activeAlerts: [WidgetAlert] { alerts.filter { !$0.acknowledged } }

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
}

extension Snapshot {
    /// Every field optional, so a snapshot saved by an older app still reads.
    init(from decoder: Decoder) throws {
        let c = try decoder.container(keyedBy: CodingKeys.self)
        signedIn = try c.decodeIfPresent(Bool.self, forKey: .signedIn) ?? false
        critical = try c.decodeIfPresent(Int.self, forKey: .critical) ?? 0
        warning = try c.decodeIfPresent(Int.self, forKey: .warning) ?? 0
        acknowledged = try c.decodeIfPresent(Int.self, forKey: .acknowledged) ?? 0
        devicesDown = try c.decodeIfPresent(Int.self, forKey: .devicesDown)
        alerts = try c.decodeIfPresent([WidgetAlert].self, forKey: .alerts) ?? []
        devices = try c.decodeIfPresent(DeviceCounts.self, forKey: .devices)
        pinned = try c.decodeIfPresent([PinnedDevice].self, forKey: .pinned) ?? []
        sensors = try c.decodeIfPresent([SensorReading].self, forKey: .sensors) ?? []
        sensorsReadAt = try c.decodeIfPresent(Int64.self, forKey: .sensorsReadAt) ?? 0
        hideLockScreenDetails = try c.decodeIfPresent(Bool.self, forKey: .hideLockScreenDetails) ?? true
        checkedAt = try c.decodeIfPresent(Int64.self, forKey: .checkedAt) ?? 0
    }

    /// For the widget gallery: the network from the mock-ups.
    static var preview: Snapshot {
        let now = Int64(Date().timeIntervalSince1970)
        return Snapshot(
            signedIn: true,
            critical: 2,
            warning: 3,
            acknowledged: 1,
            devicesDown: 1,
            alerts: [
                WidgetAlert(alertId: 1, deviceId: 1, severity: "critical", rule: "Device down", device: "core-sw-01", raisedAt: now - 12 * 60),
                WidgetAlert(alertId: 2, deviceId: 2, severity: "critical", rule: "Port errors above 100/s on Gi0/1", device: "edge-rtr-02", raisedAt: now - 34 * 60),
                WidgetAlert(alertId: 3, deviceId: 3, severity: "warning", rule: "Temperature above 70 °C", device: "dist-sw-03", raisedAt: now - 3600),
                WidgetAlert(alertId: 4, deviceId: 4, severity: "warning", rule: "BGP session down", device: "edge-rtr-01", raisedAt: now - 3 * 3600),
                WidgetAlert(alertId: 5, deviceId: 5, severity: "warning", rule: "Wireless clients above 50", device: "ap-lobby-04", raisedAt: now - 5 * 3600),
                WidgetAlert(alertId: 6, deviceId: 6, severity: "critical", rule: "Disk usage above 90%", device: "nas-01", acknowledged: true, raisedAt: now - 86400),
            ],
            devices: DeviceCounts(total: 50, up: 47, down: 1, disabled: 2, critical: 2, warning: 3, acknowledged: 1, ok: 42),
            pinned: [
                PinnedDevice(deviceId: 1, name: "core-sw-01", state: "down", status: "Down", location: "Comms room", since: now - 12 * 60),
                PinnedDevice(deviceId: 2, name: "edge-rtr-02", state: "critical", status: "Port errors above 100/s", location: "Comms room", since: 0),
                PinnedDevice(deviceId: 3, name: "dist-sw-03", state: "warning", status: "Temperature above 70 °C", location: "Floor 2", since: 0),
                PinnedDevice(deviceId: 7, name: "fw-01", state: "ok", status: "Up 41d 2h", location: "Comms room", since: 0),
                PinnedDevice(deviceId: 4, name: "edge-rtr-01", state: "warning", status: "BGP session down", location: "Comms room", since: 0),
                PinnedDevice(deviceId: 5, name: "ap-lobby-04", state: "ok", status: "Up 6d 1h", location: "Reception", since: 0),
            ],
            sensors: [
                SensorReading(sensorId: 1, deviceId: 2, name: "SFP Gi0/2 Rx power", device: "edge-rtr-02", value: "-24.6 dBm", status: "critical", position: 0.08),
                SensorReading(sensorId: 2, deviceId: 3, name: "Chassis temperature", device: "dist-sw-03", value: "71 °C", status: "warning", position: 0.86),
                SensorReading(sensorId: 3, deviceId: 4, name: "SFP Gi0/1 Rx power", device: "edge-rtr-01", value: "-8.4 dBm", status: "ok", position: 0.62),
                SensorReading(sensorId: 4, deviceId: 1, name: "PSU 1 temperature", device: "core-sw-01", value: "38 °C", status: "ok", position: 0.44),
                SensorReading(sensorId: 5, deviceId: 7, name: "Fan 2", device: "fw-01", value: "4200 rpm", status: "ok", position: 0.55),
            ],
            sensorsReadAt: now - 6 * 60,
            hideLockScreenDetails: false,
            checkedAt: now - 2 * 60)
    }
}
