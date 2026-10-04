using System.Globalization;

namespace DashyNMS.Mobile.Services;

/// <summary>
/// What a navigation opened, for the diagnostics (#126): "device 7",
/// "alert #4821", "rule 12" - ids, never names, so it means something to
/// whoever reads the log without saying whose network it is.
/// </summary>
public static class NavigationDescription
{
    /// <summary>"device · device 7", "alertrules", "logs · device 3 · section EventLog".</summary>
    public static string Describe(string route, IDictionary<string, object>? parameters)
    {
        var name = route.TrimStart('/').Replace("main/", string.Empty, StringComparison.Ordinal);
        if (parameters is null || parameters.Count == 0)
        {
            return name;
        }

        var context = new List<string>();
        Add(context, parameters, Routes.DeviceIdParameter, id => "device " + id);
        Add(context, parameters, Routes.AlertIdParameter, id => "alert #" + id);
        Add(context, parameters, Routes.RuleIdParameter, id => "rule " + id);
        Add(context, parameters, Routes.TemplateIdParameter, id => "template " + id);
        Add(context, parameters, Routes.SectionParameter, section => "section " + section);
        Add(context, parameters, Routes.WidgetIdParameter, _ => "a dashboard card");
        Add(context, parameters, Routes.StateParameter, state => "showing " + state);
        Add(context, parameters, Routes.AlertFilterParameter, show => "showing " + show);
        return context.Count == 0 ? name : name + " · " + string.Join(" · ", context);
    }

    private static void Add(List<string> context, IDictionary<string, object> parameters, string key, Func<string, string> describe)
    {
        if (parameters.TryGetValue(key, out var value) && value is not null)
        {
            context.Add(describe(Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty));
        }
    }
}
