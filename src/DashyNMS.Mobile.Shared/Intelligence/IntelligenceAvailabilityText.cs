namespace DashyNMS.Mobile.Intelligence;

/// <summary>Availability guidance kept independent of any page or diagnostic.</summary>
public static class IntelligenceAvailabilityText
{
    /// <summary>Explains a model problem; an available model needs no status message.</summary>
    public static string Describe(IntelligenceAvailability availability) => availability switch
    {
        IntelligenceAvailability.Available => string.Empty,
        IntelligenceAvailability.DeviceNotEligible => "This device doesn't support Apple Intelligence.",
        IntelligenceAvailability.NotEnabled => "Apple Intelligence is turned off. Turn it on in the Settings app, under Apple Intelligence & Siri.",
        IntelligenceAvailability.ModelNotReady => "The on-device model is still downloading or getting ready. Try again later.",
        IntelligenceAvailability.Unsupported => "There's no on-device model on this phone. It needs iOS 26 or later.",
        _ => "The on-device model isn't available.",
    };
}
