// The Objective-C face of Apple's on-device language model (#570), for the
// .NET binding in src/DashyNMS.Mobile.Intelligence.iOS.
//
// Every class here exists on iOS 15 and later, because the app does. Only
// the code inside `#available(iOS 26.0, *)` touches Foundation Models, which
// is weak-linked (see project.yml), so an older iPhone loads the framework
// and is told the model is unsupported.

import Foundation
import FoundationModels

/// Whether the on-device model can answer; mirrors IntelligenceAvailability in Mobile.Shared.
@objc(DNMAvailability)
public enum ModelAvailability: Int {
    case available = 0
    case unsupported = 1
    case deviceNotEligible = 2
    case notEnabled = 3
    case modelNotReady = 4
    case unavailable = 5
}

/// Where C# puts a lookup's result. An object rather than a block inside a
/// block, which keeps the .NET binding to plain types.
@objc(DNMLookupReply)
public final class LookupReply: NSObject, @unchecked Sendable {
    private let lock = NSLock()
    private var continuation: CheckedContinuation<String, Never>?

    init(_ continuation: CheckedContinuation<String, Never>) {
        self.continuation = continuation
    }

    /// The lookup's text, or nil when it failed. Only the first call counts.
    @objc(finish:)
    public func finish(_ text: String?) {
        lock.lock()
        let waiting = continuation
        continuation = nil
        lock.unlock()
        waiting?.resume(returning: text ?? "The lookup failed.")
    }
}

/// A lookup the model may call: its name, when to use it, and the C# that runs it.
@objc(DNMLookup)
public final class Lookup: NSObject, @unchecked Sendable {
    let name: String
    let purpose: String
    let handler: (String, LookupReply) -> Void

    @objc(initWithName:purpose:handler:)
    public init(name: String, purpose: String, handler: @escaping (String, LookupReply) -> Void) {
        self.name = name
        self.purpose = purpose
        self.handler = handler
        super.init()
    }

    func run(_ query: String) async -> String {
        await withCheckedContinuation { continuation in
            handler(query, LookupReply(continuation))
        }
    }
}

/// A lookup as Foundation Models sees it. Its arguments are
/// described by hand rather than with the @Generable macro: CodeQL builds
/// this under Rosetta, where the compiler's macro plugin can't load.
@available(iOS 26.0, *)
struct LookupTool: Tool {
    let lookup: Lookup

    var name: String { lookup.name }
    var description: String { lookup.purpose }

    var parameters: GenerationSchema {
        if lookup.name == "find_device" {
            return GenerationSchema(type: GeneratedContent.self, description: "Device search", properties: [
                GenerationSchema.Property(name: "query", description: "Name, IP or MAC", type: String?.self),
                GenerationSchema.Property(name: "status", description: "up, down, disabled", type: String?.self),
                GenerationSchema.Property(name: "group", description: "Group name", type: String?.self),
                GenerationSchema.Property(name: "location", description: "Location name", type: String?.self)
            ])
        }
        return GenerationSchema(
            type: GeneratedContent.self,
            description: "What to look up",
            properties: [
                GenerationSchema.Property(
                    name: "query",
                    description: "What to look for, or an empty string for everything",
                    type: String.self)
            ])
    }

    func call(arguments: GeneratedContent) async throws -> String {
        if lookup.name == "find_device" {
            var fields: [String: String] = [:]
            for key in ["query", "status", "group", "location"] {
                if let value = try? arguments.value(String.self, forProperty: key) {
                    fields[key] = value
                }
            }
            guard let data = try? JSONSerialization.data(withJSONObject: fields),
                  let payload = String(data: data, encoding: .utf8) else {
                return "The lookup failed."
            }
            return await lookup.run(payload)
        }
        let query = (try? arguments.value(String.self, forProperty: "query")) ?? ""
        return await lookup.run(query)
    }
}

/// The model itself: whether it's there, and what things cost in its context.
@objc(DNMIntelligenceModel)
public final class IntelligenceModel: NSObject {
    @objc(availability)
    public static var availability: ModelAvailability {
        guard #available(iOS 26.0, *) else { return .unsupported }
        switch SystemLanguageModel.default.availability {
        case .available:
            return .available
        case .unavailable(.deviceNotEligible):
            return .deviceNotEligible
        case .unavailable(.appleIntelligenceNotEnabled):
            return .notEnabled
        case .unavailable(.modelNotReady):
            return .modelNotReady
        default:
            return .unavailable
        }
    }

    /// The whole context in tokens, or -1 where the model won't say.
    @objc(contextSize)
    public static var contextSize: Int {
        guard #available(iOS 26.0, *) else { return -1 }
        return SystemLanguageModel.default.contextSize
    }

    /// The tokens `text` costs as instructions, or -1 before iOS 26.4, which can't count.
    @objc(countTokens:completion:)
    public static func countTokens(_ text: String, completion: @escaping (Int) -> Void) {
        guard #available(iOS 26.4, *) else {
            completion(-1)
            return
        }
        Task {
            let count = try? await SystemLanguageModel.default.tokenCount(for: Instructions(text))
            completion(count ?? -1)
        }
    }
}

/// One conversation: instructions and lookups fixed at the start, one question at a time.
@objc(DNMIntelligenceSession)
public final class IntelligenceSession: NSObject {
    private let instructions: String
    private let lookups: [Lookup]
    private let lock = NSLock()
    // LanguageModelSession, kept untyped because this class also exists before iOS 26.
    private var session: Any?
    private var task: Task<Void, Never>?

    @objc(initWithInstructions:lookups:)
    public init(instructions: String, lookups: [Lookup]) {
        self.instructions = instructions
        self.lookups = lookups
        super.init()
    }

    @available(iOS 26.0, *)
    private func liveSession() -> LanguageModelSession {
        lock.lock()
        defer { lock.unlock() }
        if let existing = session as? LanguageModelSession {
            return existing
        }
        let text = instructions
        let created = LanguageModelSession(
            model: SystemLanguageModel.default,
            tools: lookups.map { LookupTool(lookup: $0) },
            instructions: { text })
        session = created
        return created
    }

    /// Loads the model ahead of the first question, when there's a second or more to spare.
    @objc(prewarm)
    public func prewarm() {
        guard #available(iOS 26.0, *) else { return }
        liveSession().prewarm()
    }

    /// Asks. `onText` gets the whole answer so far each time more is written;
    /// `completion` gets the finished answer and the model's own token usage,
    /// or what had been written and why it stopped. Both arrive off the main thread.
    @objc(ask:onText:completion:)
    public func ask(
        _ question: String,
        onText: @escaping (String) -> Void,
        completion: @escaping (String?, String?, NSError?) -> Void
    ) {
        guard #available(iOS 26.0, *) else {
            completion(nil, nil, Self.failure("unsupported", "The on-device model needs iOS 26 or later."))
            return
        }
        let live = liveSession()
        let asking = Task {
            var answer = ""
            var usage: String?
            do {
                let stream = live.streamResponse(options: GenerationOptions()) { question }
                for try await snapshot in stream {
                    answer = snapshot.content
                    if #available(iOS 27.0, *) {
                        usage = String(describing: snapshot.usage)
                    }
                    onText(answer)
                }
                try Task.checkCancellation()
                completion(answer, usage, nil)
            } catch is CancellationError {
                completion(answer, usage, Self.failure("stopped", "Stopped."))
            } catch {
                // The error's own name ("contextSizeExceeded", "guardrailViolation"…),
                // which the SDKs have renamed between releases, as text.
                completion(answer, usage, Self.failure(String(describing: error), error.localizedDescription))
            }
        }
        lock.lock()
        task = asking
        lock.unlock()
    }

    /// Stops the answer being written; `ask`'s completion then reports "stopped".
    @objc(stop)
    public func stop() {
        lock.lock()
        let asking = task
        lock.unlock()
        asking?.cancel()
    }

    private static func failure(_ reason: String, _ message: String) -> NSError {
        NSError(
            domain: "DashyNMSIntelligence",
            code: reason == "stopped" ? 1 : 2,
            userInfo: [NSLocalizedDescriptionKey: message, NSLocalizedFailureReasonErrorKey: reason])
    }
}
