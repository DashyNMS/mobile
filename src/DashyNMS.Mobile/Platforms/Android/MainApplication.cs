using Android.App;
using Android.Content;
using Android.Runtime;

namespace DashyNMS.Mobile;

[Application]
public class MainApplication : MauiApplication
{
	public MainApplication(IntPtr handle, JniHandleOwnership ownership)
		: base(handle, ownership)
	{
	}

	protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();

	/// <summary>Android running short of memory, for the diagnostics (#126).</summary>
	public override void OnLowMemory()
	{
		base.OnLowMemory();
		Adapters.SystemWatch.LowMemory(string.Empty);
	}

	public override void OnTrimMemory(TrimMemory level)
	{
		base.OnTrimMemory(level);
		if (level >= TrimMemory.RunningLow)
		{
			Adapters.SystemWatch.LowMemory(level.ToString());
		}
	}
}
