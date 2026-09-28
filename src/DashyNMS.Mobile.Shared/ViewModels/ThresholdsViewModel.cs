using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DashyNMS.Mobile.Services;
using DesktopNMS.Core.Configuration;

namespace DashyNMS.Mobile.ViewModels;

/// <summary>
/// Desktop's Health thresholds: the dBm, signal, temperature and fan-speed
/// bands that colour sensors on the Health tab and in Device View, and whether
/// they override a sensor's own LibreNMS limits.
/// </summary>
/// <remarks>
/// The same settings desktop has, edited as text and saved together. Desktop
/// doesn't check them; here Save refuses bands in the wrong order (a warning
/// beyond its critical), since a typo would otherwise quietly recolour every
/// sensor.
/// </remarks>
public sealed partial class ThresholdsViewModel : ViewModelBase
{
    private readonly ISettingsStore _settings;
    private readonly INavigationService _navigation;

    [ObservableProperty] private string _dbmWarning = string.Empty;
    [ObservableProperty] private string _dbmCritical = string.Empty;
    [ObservableProperty] private string _signalWarning = string.Empty;
    [ObservableProperty] private string _signalCritical = string.Empty;
    [ObservableProperty] private string _temperatureLowCritical = string.Empty;
    [ObservableProperty] private string _temperatureLowWarning = string.Empty;
    [ObservableProperty] private string _temperatureHighWarning = string.Empty;
    [ObservableProperty] private string _temperatureHighCritical = string.Empty;
    [ObservableProperty] private string _fanLowCritical = string.Empty;
    [ObservableProperty] private string _fanLowWarning = string.Empty;
    [ObservableProperty] private string _fanHighWarning = string.Empty;
    [ObservableProperty] private string _fanHighCritical = string.Empty;
    [ObservableProperty] private bool _overrideSensorLimits;

    public ThresholdsViewModel(ISettingsStore settings, INavigationService navigation)
    {
        _settings = settings;
        _navigation = navigation;
        Load(settings.Current);
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        ErrorMessage = Validate(out var values);
        if (ErrorMessage is not null)
        {
            return;
        }

        var current = _settings.Current;
        current.DbmThresholds.WarningThreshold = values["dbmWarning"];
        current.DbmThresholds.CriticalThreshold = values["dbmCritical"];
        current.SignalThresholds.WarningThreshold = values["signalWarning"];
        current.SignalThresholds.CriticalThreshold = values["signalCritical"];
        Apply(current.TemperatureThresholds, values, "temperature");
        Apply(current.FanSpeedThresholds, values, "fan");
        current.OverrideSensorLimitsWithAppThresholds = OverrideSensorLimits;

        // Health and Device View recolour on Changed.
        _settings.Save();
        await _navigation.GoToAsync(Routes.Back);
    }

    /// <summary>Desktop's defaults, into the fields - saved only with Save.</summary>
    [RelayCommand]
    private void ResetToDefaults()
    {
        var defaults = new AppSettings();
        Load(defaults);
        ErrorMessage = null;
    }

    private void Load(AppSettings settings)
    {
        DbmWarning = Text(settings.DbmThresholds.WarningThreshold);
        DbmCritical = Text(settings.DbmThresholds.CriticalThreshold);
        SignalWarning = Text(settings.SignalThresholds.WarningThreshold);
        SignalCritical = Text(settings.SignalThresholds.CriticalThreshold);
        TemperatureLowCritical = Text(settings.TemperatureThresholds.LowCritical);
        TemperatureLowWarning = Text(settings.TemperatureThresholds.LowWarning);
        TemperatureHighWarning = Text(settings.TemperatureThresholds.HighWarning);
        TemperatureHighCritical = Text(settings.TemperatureThresholds.HighCritical);
        FanLowCritical = Text(settings.FanSpeedThresholds.LowCritical);
        FanLowWarning = Text(settings.FanSpeedThresholds.LowWarning);
        FanHighWarning = Text(settings.FanSpeedThresholds.HighWarning);
        FanHighCritical = Text(settings.FanSpeedThresholds.HighCritical);
        OverrideSensorLimits = settings.OverrideSensorLimitsWithAppThresholds;
    }

    /// <summary>Every field a number, and each band in order; null when all's well.</summary>
    internal string? Validate(out Dictionary<string, double> values)
    {
        values = new Dictionary<string, double>();
        var fields = new (string Key, string Label, string Text)[]
        {
            ("dbmWarning", "dBm warning", DbmWarning),
            ("dbmCritical", "dBm critical", DbmCritical),
            ("signalWarning", "Signal warning", SignalWarning),
            ("signalCritical", "Signal critical", SignalCritical),
            ("temperatureLowCritical", "Temperature low critical", TemperatureLowCritical),
            ("temperatureLowWarning", "Temperature low warning", TemperatureLowWarning),
            ("temperatureHighWarning", "Temperature high warning", TemperatureHighWarning),
            ("temperatureHighCritical", "Temperature high critical", TemperatureHighCritical),
            ("fanLowCritical", "Fan speed low critical", FanLowCritical),
            ("fanLowWarning", "Fan speed low warning", FanLowWarning),
            ("fanHighWarning", "Fan speed high warning", FanHighWarning),
            ("fanHighCritical", "Fan speed high critical", FanHighCritical),
        };

        foreach (var (key, label, text) in fields)
        {
            if (!TryParse(text, out var value))
            {
                return $"{label} must be a number.";
            }

            values[key] = value;
        }

        // Weaker signal is more negative, so critical sits at or below warning.
        if (values["dbmCritical"] > values["dbmWarning"])
        {
            return "dBm critical must be at or below dBm warning.";
        }

        if (values["signalCritical"] > values["signalWarning"])
        {
            return "Signal critical must be at or below signal warning.";
        }

        return BandError(values, "temperature", "Temperature") ?? BandError(values, "fan", "Fan speed");
    }

    private static string? BandError(Dictionary<string, double> values, string prefix, string label) =>
        values[prefix + "LowCritical"] <= values[prefix + "LowWarning"]
        && values[prefix + "LowWarning"] <= values[prefix + "HighWarning"]
        && values[prefix + "HighWarning"] <= values[prefix + "HighCritical"]
            ? null
            : $"{label} thresholds must go low critical, low warning, high warning, high critical, from lowest to highest.";

    private static void Apply(BandThresholdSettings band, Dictionary<string, double> values, string prefix)
    {
        band.LowCritical = values[prefix + "LowCritical"];
        band.LowWarning = values[prefix + "LowWarning"];
        band.HighWarning = values[prefix + "HighWarning"];
        band.HighCritical = values[prefix + "HighCritical"];
    }

    private static string Text(double value) => value.ToString("0.###", CultureInfo.CurrentCulture);

    /// <summary>The phone's own decimal separator, or a full stop.</summary>
    private static bool TryParse(string text, out double value) =>
        double.TryParse(text.Trim(), NumberStyles.Float, CultureInfo.CurrentCulture, out value)
        || double.TryParse(text.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out value);
}
