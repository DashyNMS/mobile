import Foundation

/// The words round a snapshot's numbers, worked out as the widget draws so
/// ages keep up between checks. Mirrors WidgetFormat in the app
/// (src/DashyNMS.Mobile.Shared/Widgets/WidgetFormat.cs): change both together.
enum Format {
    /// "just now", "12 min", "3 hr", "1 day", "4 days".
    static func age(_ unixSeconds: Int64, now: Date) -> String {
        guard unixSeconds > 0 else { return "" }
        let seconds = now.timeIntervalSince1970 - TimeInterval(unixSeconds)
        if seconds < 60 { return "just now" }
        if seconds < 3600 { return "\(Int(seconds / 60)) min" }
        if seconds < 86400 { return "\(Int(seconds / 3600)) hr" }
        let days = Int(seconds / 86400)
        return days == 1 ? "1 day" : "\(days) days"
    }

    /// "Checked 2 min ago".
    static func checked(_ unixSeconds: Int64, now: Date, verb: String = "Checked") -> String {
        let text = age(unixSeconds, now: now)
        if text.isEmpty { return "" }
        return text == "just now" ? "\(verb) just now" : "\(verb) \(text) ago"
    }

    /// "Down for 12 min" rather than a bare "Down" when its alert says since when.
    static func deviceStatus(_ device: PinnedDevice, now: Date) -> String {
        guard device.state == "down", device.since > 0 else { return device.status }
        let text = age(device.since, now: now)
        return text.isEmpty || text == "just now" ? device.status : "Down for \(text)"
    }

    static func count(_ value: Int) -> String {
        value > 99 ? "99+" : String(value)
    }

    /// The pie's slices in drawing order; a slice that exists always gets a sliver.
    static func pieSlices(_ counts: DeviceCounts) -> [(state: String, fraction: Double)] {
        let minimum = 0.015
        let parts: [(String, Int)] = [
            ("critical", counts.critical),
            ("warning", counts.warning),
            ("acknowledged", counts.acknowledged),
            ("ok", counts.ok),
        ]
        let total = parts.reduce(0) { $0 + $1.1 }
        guard total > 0 else { return [] }
        let raw = parts.filter { $0.1 > 0 }.map { ($0.0, max(Double($0.1) / Double(total), minimum)) }
        let sum = raw.reduce(0) { $0 + $1.1 }
        return raw.map { (state: $0.0, fraction: $0.1 / sum) }
    }
}
