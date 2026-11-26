using DesktopNMS.Core.Models;

namespace DashyNMS.Mobile.Search;

/// <summary>What a Network Search result can do outside the page (#129): open what it names, or copy a value.</summary>
public interface INetworkSearchActions
{
    void OpenDevice(int deviceId);

    /// <summary>A port's graphs, on its device.</summary>
    void OpenPort(int deviceId, int portId);

    void Copy(string value);
}

/// <summary>A port's state in words, as Device View's port cards say it.</summary>
public static class PortText
{
    public static string State(Port port)
    {
        var status = port.IfOperStatus switch
        {
            "up" => "Up",
            "down" => port.IfAdminStatus == "down" ? "Shut down" : "Down",
            { Length: > 0 } other => char.ToUpperInvariant(other[0]) + other[1..],
            _ => "State unknown",
        };
        return status + " · " + DeviceSections.Units.Speed(port.IfSpeed);
    }
}
