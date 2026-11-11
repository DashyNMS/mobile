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
	private readonly Intelligence.IntelligenceConversationViewModel _intelligence;
	private readonly Intelligence.IntelligenceChatListViewModel _chats;
	private readonly Intelligence.IntelligenceSnapshotShield _snapshots;

	public App(AppShell shell, AlertWatchCoordinator alerts, MauiAppearance appearance, Services.IShareService share, Services.DiagnosticsLog diagnostics,
		Security.CertificatePrompt certificates, Services.AppDiagnostics appDiagnostics, Services.SensorDefinitions definitions,
		Services.WhatsNew whatsNew, Diagnostics.DiagnosticsReporter errors, Diagnostics.AppMetrics metrics,
		Diagnostics.StartupTiming startup, DesktopNMS.Core.Api.LibreNmsTransport transport,
		Diagnostics.UiStallMonitor stalls, Diagnostics.MemoryMonitor memory, Diagnostics.SessionHealth sessions,
		Intelligence.IntelligenceConversationViewModel intelligence, Intelligence.IntelligenceChatListViewModel chats,
		Intelligence.IntelligenceSnapshotShield snapshots)
	{
		InitializeComponent();
		_intelligence = intelligence;
		_chats = chats;
		_snapshots = snapshots;

		// Single-chat mode's snapshot shield (#230) follows what is on screen:
		// pages, tabs and the chat's sheets. Android keeps its window secure
		// while the chat shows; iOS draws its cover only on the way out.
		shell.Navigated += (_, _) => snapshots.Refresh();
		ModalPushed += (_, _) => snapshots.Refresh();
		ModalPopped += (_, _) => snapshots.Refresh();
#if ANDROID
		snapshots.SecureChanged += (_, _) => MainThread.BeginInvokeOnMainThread(() => AndroidSnapshotSecurity.Apply(snapshots.IsSecure));
#endif

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

		// Single-chat mode's cover (#230) goes up before iOS takes the
		// app-switcher snapshot: resigning active comes first, entering the
		// background (Stopped) is the last chance.
		window.Deactivated += (_, _) => CoverPrivateChat(window);

		// Check for alerts on a timer while the app is on screen; in the
		// background the platform's scheduler takes over.
		// Error reports (#217) start only here, on screen: a launch for a
		// background alert check is never reported. Each return sends what
		// waited - last launch's fatal error, or what queued offline.
		window.Activated += (_, _) =>
		{
			UncoverPrivateChat();
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
			UncoverPrivateChat();
			_alerts.SetForeground(true);
			_errors.Start();
			_metrics.Start();
			_startup.WindowShown();
			_sessions.Begin();
			_stalls.Resume();
			_memory.Resume();
			CheckSensorDefinitions();
		};
		window.Stopped += async (_, _) =>
		{
			CoverPrivateChat(window);
			var endingIntelligence = InterruptIntelligenceAsync();
			_alerts.SetForeground(false);
			_stalls.Pause();
			_memory.Pause();
			_sessions.End();

			// What's gathered goes to the queue before iOS or Android may end the app.
			_metrics.Flush();
			await endingIntelligence;
		};

		// Pages and the app coming and going, for diagnostics - and a page left
		// blank after unlocking is put right (#107).
		PageHealth.Watch(this, window, _diagnostics);

		return window;
	}

	/// <summary>Hides a single-chat conversation before the app-switcher snapshot (#230).</summary>
	private void CoverPrivateChat(Window window)
	{
		if (!_snapshots.Leaving()) return;
#if IOS
		IosSnapshotCover.Show(window);
#endif
	}

	/// <summary>Back in front: the cover comes down, whatever has changed while away.</summary>
	private void UncoverPrivateChat()
	{
		// Removed even when the shield had no record of it, so a cover can never be left behind.
		_snapshots.Returned();
#if IOS
		IosSnapshotCover.Hide();
#elif ANDROID
		AndroidSnapshotSecurity.Apply(_snapshots.IsSecure);
#endif
	}

	/// <summary>
	/// Give cancellation and the saved chat (#229) a short iOS reservation before
	/// suspension: an answer still running is interrupted, then the chat - with its
	/// partial answer marked Interrupted - is queued and written. Never logs model content.
	/// </summary>
	private async Task InterruptIntelligenceAsync()
	{
#if IOS
		var application = UIKit.UIApplication.SharedApplication;
		var identifier = UIKit.UIApplication.BackgroundTaskInvalid;
		void EndReservation()
		{
			if (identifier == UIKit.UIApplication.BackgroundTaskInvalid) return;
			var ending = identifier;
			identifier = UIKit.UIApplication.BackgroundTaskInvalid;
			application.EndBackgroundTask(ending);
		}
#endif
		var interruptionRequested = false;
		try
		{
			try
			{
#if IOS
				await MainThread.InvokeOnMainThreadAsync(() =>
					identifier = application.BeginBackgroundTask("Save Intelligence chat", () =>
						MainThread.BeginInvokeOnMainThread(EndReservation)));
#endif
				if (_intelligence.IsRunning)
				{
					interruptionRequested = true;
					await _intelligence.InterruptAsync().WaitAsync(TimeSpan.FromSeconds(4));
				}
			}
			catch
			{
				// Cancellation is requested once. A late expiration/timeout must
				// not interrupt a newer answer after the app has resumed.
				if (!interruptionRequested) _intelligence.Interrupt();
			}

			try
			{
				// Captured on the main thread, written off it. A write still
				// going when this gives up carries on; the streaming saves
				// already on disk are what a forced quit leaves.
				await _chats.SaveNowAsync().WaitAsync(TimeSpan.FromSeconds(4));
			}
			catch
			{
				// The writer reports its own failures to the chat list.
			}
		}
		finally
		{
#if IOS
			await MainThread.InvokeOnMainThreadAsync(EndReservation);
#endif
		}
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
