using ObjCRuntime;

namespace DashyNMS.Mobile.Intelligence.Native;

/// <summary>DNMAvailability in ios-intelligence/: whether the on-device model can answer.</summary>
[Native]
public enum DnmAvailability : long
{
    Available = 0,
    Unsupported = 1,
    DeviceNotEligible = 2,
    NotEnabled = 3,
    ModelNotReady = 4,
    Unavailable = 5,
}
