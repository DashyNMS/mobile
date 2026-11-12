using DashyNMS.Mobile.Intelligence;

namespace DashyNMS.Mobile.Tests;

public sealed class IntelligenceAvailabilityTextTests
{
    [Theory]
    [InlineData(IntelligenceAvailability.DeviceNotEligible, "doesn't support Apple Intelligence")]
    [InlineData(IntelligenceAvailability.NotEnabled, "turned off")]
    [InlineData(IntelligenceAvailability.ModelNotReady, "still downloading")]
    [InlineData(IntelligenceAvailability.Unsupported, "iOS 26 or later")]
    [InlineData(IntelligenceAvailability.Unavailable, "isn't available")]
    public void Chat_availability_guidance_survives_removing_the_diagnostic(IntelligenceAvailability state, string reason)
    {
        Assert.Contains(reason, IntelligenceAvailabilityText.Describe(state));
    }

    [Fact]
    public void An_available_model_needs_no_success_message() =>
        Assert.Empty(IntelligenceAvailabilityText.Describe(IntelligenceAvailability.Available));

    [Fact]
    public void A_phone_with_no_model_is_unsupported_and_refuses_a_session()
    {
        Assert.Equal(IntelligenceAvailability.Unsupported, new NoIntelligenceModel().Availability);
        Assert.Throws<IntelligenceException>(() => new NoIntelligenceModel().StartSession("x", []));
    }
}
