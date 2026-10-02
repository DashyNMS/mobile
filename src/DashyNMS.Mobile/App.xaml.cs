using DashyNMS.Mobile.Adapters;
using DashyNMS.Mobile.Alerts;

namespace DashyNMS.Mobile;

public partial class App : Application
{
	private readonly AppShell _shell;
	private readonly AlertWatchCoordinator _alerts;
	private readonly Services.DiagnosticsLog _diagnostics;

	public App(AppShell shell, AlertWatchCoordinator alerts, MauiAppearance appearance, Services.IShareService share, Services.DiagnosticsLog diagnostics)
	{
		InitializeComponent();
		appearance.Apply();
		_shell = shell;
		_alerts = alerts;
		_diagnostics = diagnostics;
		_alerts.Start();

		// Exports left from last time, if the phone never got round to clearing its cache (#9).
		share.ClearExports();
	}

	protected override Window CreateWindow(IActivationState? activationState)
	{
		var window = new Window(_shell) { Title = "DashyNMS" };

		// Check for alerts on a timer while the app is on screen; in the
		// background the platform's scheduler takes over.
		window.Activated += (_, _) => _alerts.SetForeground(true);
		window.Resumed += (_, _) => _alerts.SetForeground(true);
		window.Stopped += (_, _) => _alerts.SetForeground(false);

		// Pages and the app coming and going, for diagnostics - and a page left
		// blank after unlocking is put right (#107).
		PageHealth.Watch(this, window, _diagnostics);

		return window;
	}
}
