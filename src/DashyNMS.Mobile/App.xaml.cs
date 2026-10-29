using DashyNMS.Mobile.Adapters;
using DashyNMS.Mobile.Alerts;

namespace DashyNMS.Mobile;

public partial class App : Application
{
	private readonly AppShell _shell;
	private readonly AlertWatchCoordinator _alerts;
	private readonly Services.DiagnosticsLog _diagnostics;
	private readonly Services.SensorDefinitions _definitions;

	public App(AppShell shell, AlertWatchCoordinator alerts, MauiAppearance appearance, Services.IShareService share, Services.DiagnosticsLog diagnostics,
		Security.CertificatePrompt certificates, Services.AppDiagnostics appDiagnostics, Services.SensorDefinitions definitions)
	{
		InitializeComponent();

		// Errors, memory, the network and the theme; settings changes and slow pages (#126).
		SystemWatch.Start(this, diagnostics);
		appDiagnostics.Start();
		appearance.Apply();
		_shell = shell;
		_alerts = alerts;
		_diagnostics = diagnostics;
		_alerts.Start();
		_definitions = definitions;

		// A certificate met while signed in - the backup address's, say - is
		// asked about there and then (#141); once trusted, the page showing loads again.
		certificates.Trusted += (_, _) => MainThread.BeginInvokeOnMainThread(() =>
		{
			if (Shell.Current?.CurrentPage?.BindingContext is ViewModels.IRefreshable page)
			{
				page.RefreshCommand.Execute(null);
			}
		});
		certificates.Start();

		// Exports left from last time, if the phone never got round to clearing its cache (#9).
		share.ClearExports();
	}

	protected override Window CreateWindow(IActivationState? activationState)
	{
		var window = new Window(_shell) { Title = "DashyNMS" };

		// Check for alerts on a timer while the app is on screen; in the
		// background the platform's scheduler takes over.
		window.Activated += (_, _) =>
		{
			_alerts.SetForeground(true);
			CheckSensorDefinitions();
		};
		window.Resumed += (_, _) =>
		{
			_alerts.SetForeground(true);
			CheckSensorDefinitions();
		};
		window.Stopped += (_, _) => _alerts.SetForeground(false);

		// Pages and the app coming and going, for diagnostics - and a page left
		// blank after unlocking is put right (#107).
		PageHealth.Watch(this, window, _diagnostics);

		return window;
	}

	private Task? _definitionsStarted;

	/// <summary>
	/// The sensor-definitions catalogue (#209): started, then checked, only once
	/// the app is on screen - iOS can launch it in the background for an alert
	/// check, which must never reach GitHub. Core fetches only if its last
	/// check is a day old.
	/// </summary>
	private void CheckSensorDefinitions()
	{
		if (_definitionsStarted is null)
		{
			_definitionsStarted = Task.Run(_definitions.StartAsync);
		}
		else if (_definitionsStarted.IsCompleted)
		{
			_ = Task.Run(() => _definitions.CheckAsync());
		}
	}
}
