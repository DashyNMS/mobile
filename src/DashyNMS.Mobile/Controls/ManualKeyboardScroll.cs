#if IOS
namespace DashyNMS.Mobile.Controls;

/// <summary>Keeps automatic page translation off while any visible layout owns keyboard clearance.</summary>
/// <remarks>Acquire and release run on the UI thread. Overlapping page lifecycles must not reconnect a sibling.</remarks>
internal static class ManualKeyboardScroll
{
    private static readonly HashSet<object> Owners = new(ReferenceEqualityComparer.Instance);

    /// <summary>Repeated activation reclaims translation without adding another ownership count.</summary>
    internal static void Acquire(object owner)
    {
        Owners.Add(owner);
        Microsoft.Maui.Platform.KeyboardAutoManagerScroll.Disconnect();
    }

    /// <summary>Only the final departing manual layout restores native automatic translation.</summary>
    internal static void Release(object owner)
    {
        if (Owners.Remove(owner) && Owners.Count == 0)
            Microsoft.Maui.Platform.KeyboardAutoManagerScroll.Connect();
    }
}
#endif
