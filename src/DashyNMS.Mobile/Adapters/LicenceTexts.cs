using DashyNMS.Mobile.Services;

namespace DashyNMS.Mobile.Adapters;

/// <summary>The licence texts shipped under Resources/Raw/licences (#164).</summary>
public sealed class LicenceTexts : ILicenceTexts
{
    public bool IsAndroid => DeviceInfo.Current.Platform == DevicePlatform.Android;

    public async Task<string> ReadAsync(string licenceFile)
    {
        await using var stream = await FileSystem.OpenAppPackageFileAsync(OpenSourceNotices.LicencesFolder + "/" + licenceFile);
        using var reader = new StreamReader(stream);
        return await reader.ReadToEndAsync();
    }
}
