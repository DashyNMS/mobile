using DashyNMS.Mobile.Adapters;
using DashyNMS.Mobile.Alerts;

namespace DashyNMS.Mobile;

public partial class App : Application
{
	private readonly AppShell _shell;
	private readonly AlertWatchCoordinator _alerts;
	private readonly Services.DiagnosticsLog _diagnostics;
	private readonly Services.SensorDefinitions _definitions;
	private readonly Diagnostics.DiagnosticsReporter _errors;
	private readonly Diagnostics.AppMetrics _metrics;
	private readonly Diagnostics.StartupTiming _startup;
	private readonly Diagnostics.UiStallMonitor _stalls;
	private readonly Diagnostics.MemoryMonitor _memory;
	private readonly Diagnostics.SessionHealth _sessions;

	public App(AppShell shell, AlertWatchCoordinator alerts, MauiAppearance appearance, Services.IShareService share, Services.DiagnosticsLog diagnostics,
		Security.CertificatePrompt certificates, Services.AppDiagnostics appDiagnostics, Services.SensorDefinitions definitions,
		Services.WhatsNew whatsNew, Diagnostics.DiagnosticsReporter errors, Diagnostics.AppMetrics metrics,
		Diagnostics.StartupTiming startup, DesktopNMS.Core.Api.LibreNmsTransport transport,
		Diagnostics.UiStallMonitor stalls, Diagnostics.MemoryMonitor memory, Diagnostics.SessionHealth sessions)
	{
		InitializeComponent();

		// Errors, memory, the network and the theme; settings changes and slow pages (#126).
		SystemWatch.Start(this, diagnostics, errors);
		_errors = errors;

		// What a page caught unexpectedly is a warning report (#217); the
		// reporter drops offline, timeouts and the server's own errors.
		ViewModels.ViewModelBase.Failed += (_, ex) => errors.Capture(ex, Diagnostics.ErrorOrigin.Caught);

		// Usage and performance (#218): pages' first loads, start-up to the
		// first usable page, and Core's requests - each kept only if shared.
		_metrics = metrics;
		_startup = startup;
		ViewModels.ViewModelBase.FirstLoadFinished += (vm, load) =>
		{
			Diagnostics.Measurements.PageLoaded(metrics, vm, load);
			startup.Ready(metrics);
		};
		shell.Navigated += (_, _) =>
		{
			if (shell.CurrentPage?.BindingContext is ViewModels.SignInViewModel)
			{
				startup.Ready(metrics);
			}
		};
		Diagnostics.Measurements.Observe(transport, metrics);

		// Feature use (#219): the listed pages each time they're opened - not
		// returned to - plus a card added or a CSV export.
		_stalls = stalls;
		_memory = memory;
		_sessions = sessions;
		Diagnostics.FeatureUse.Used += (_, area) => metrics.Record(new Diagnostics.MetricObservation(Diagnostics.MetricName.FeatureUse, 1, Area: area));
		shell.Navigated += (_, e) =>
		{
			if (e.Source is not (ShellNavigationSource.Pop or ShellNavigationSource.PopToRoot or ShellNavigationSource.Remove)
				&& shell.CurrentPage?.BindingContext is { } vm
				&& Diagnostics.MetricRules.FeatureArea(vm.GetType().Name) is { } feature)
			{
				metrics.Record(new Diagnostics.MetricObservation(Diagnostics.MetricName.FeatureUse, 1, Area: feature));
			}
		};
		appDiagnostics.Start();
		appearance.Apply();
		_shell = shell;
		_alerts = alerts;
		_diagnostics = diagnostics;
		_alerts.Start();
		_definitions = definitions;

		// Before sign-in can save a server: a fresh install has nothing that's new to it (#159).
		whatsNew.MarkSeenIfFreshInstall();

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
		// Error reports (#217) start only here, on screen: a launch for a
		// background alert check is never reported. Each return sends what
		// waited - last launch's fatal error, or what queued offline.
		window.Activated += (_, _) =>
		{
			_alerts.SetForeground(true);
			_errors.Start();
			_metrics.Start();
			_startup.WindowShown();
			_sessions.Begin();
			_stalls.Resume();
			_memory.Resume();
			CheckSensorDefinitions();
		};
		window.Resumed += (_, _) =>
		{
			_alerts.SetForeground(true);
			_errors.Start();
			_metrics.Start();
			_startup.WindowShown();
			_sessions.Begin();
			_stalls.Resume();
			_memory.Resume();
			CheckSensorDefinitions();
		};
		window.Stopped += (_, _) =>
		{
			_alerts.SetForeground(false);
			_stalls.Pause();
			_memory.Pause();
			_sessions.End();

			// What's gathered goes to the queue before iOS or Android may end the app.
			_metrics.Flush();
		};

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
