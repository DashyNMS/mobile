using Foundation;
using ObjCRuntime;

namespace DashyNMS.Mobile.Intelligence.Native;

// Written by hand against ios-intelligence/DashyNMSIntelligence/Intelligence.swift:
// each selector here is spelled out in an @objc(...) there. Change both together.

/// <summary>Runs a lookup for the model's query and answers through <paramref name="reply"/>.</summary>
delegate void DnmLookupHandler(string query, DnmLookupReply reply);

/// <summary>The answer so far, all of it.</summary>
delegate void DnmTextHandler(string text);

/// <summary>The answer, the model's own token usage, and why it stopped if it didn't finish.</summary>
delegate void DnmAnswerHandler([NullAllowed] string answer, [NullAllowed] string usage, [NullAllowed] NSError error);

/// <summary>A token count, or -1 where the model can't count.</summary>
delegate void DnmCountHandler(nint tokens);

// @interface DNMLookupReply : NSObject
[BaseType(typeof(NSObject), Name = "DNMLookupReply")]
[DisableDefaultCtor]
interface DnmLookupReply
{
    // - (void)finish:(NSString * _Nullable)text;
    [Export("finish:")]
    void Finish([NullAllowed] string text);
}

// @interface DNMLookup : NSObject
[BaseType(typeof(NSObject), Name = "DNMLookup")]
[DisableDefaultCtor]
interface DnmLookup
{
    // - (instancetype)initWithName:(NSString *)name purpose:(NSString *)purpose handler:(void (^)(NSString *, DNMLookupReply *))handler;
    [Export("initWithName:purpose:handler:")]
    NativeHandle Constructor(string name, string purpose, DnmLookupHandler handler);
}

// @interface DNMIntelligenceModel : NSObject
[BaseType(typeof(NSObject), Name = "DNMIntelligenceModel")]
[DisableDefaultCtor]
interface DnmIntelligenceModel
{
    // @property (class, readonly) DNMAvailability availability;
    [Static]
    [Export("availability")]
    DnmAvailability Availability { get; }

    // @property (class, readonly) NSInteger contextSize;
    [Static]
    [Export("contextSize")]
    nint ContextSize { get; }

    // + (void)countTokens:(NSString *)text completion:(void (^)(NSInteger))completion;
    [Static]
    [Export("countTokens:completion:")]
    void CountTokens(string text, DnmCountHandler completion);
}

// @interface DNMIntelligenceSession : NSObject
[BaseType(typeof(NSObject), Name = "DNMIntelligenceSession")]
[DisableDefaultCtor]
interface DnmIntelligenceSession
{
    // - (instancetype)initWithInstructions:(NSString *)instructions lookups:(NSArray<DNMLookup *> *)lookups;
    [Export("initWithInstructions:lookups:")]
    NativeHandle Constructor(string instructions, DnmLookup[] lookups);

    // - (void)prewarm;
    [Export("prewarm")]
    void Prewarm();

    // - (void)ask:(NSString *)question onText:(void (^)(NSString *))onText completion:(void (^)(NSString * _Nullable, NSString * _Nullable, NSError * _Nullable))completion;
    [Export("ask:onText:completion:")]
    void Ask(string question, DnmTextHandler onText, DnmAnswerHandler completion);

    // - (void)stop;
    [Export("stop")]
    void Stop();
}
