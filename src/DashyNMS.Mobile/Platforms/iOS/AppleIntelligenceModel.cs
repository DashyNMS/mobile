using DashyNMS.Mobile.Intelligence;
using DashyNMS.Mobile.Intelligence.Native;
using Foundation;

namespace DashyNMS.Mobile;

/// <summary>
/// Apple's on-device model (#570) as an <see cref="IIntelligenceModel"/>,
/// through the Swift bridge in ios-intelligence/. Before iOS 26 the bridge
/// answers "unsupported" itself, so nothing here checks the OS version.
/// </summary>
public sealed class AppleIntelligenceModel : IIntelligenceModel
{
	public IntelligenceAvailability Availability => DnmIntelligenceModel.Availability switch
	{
		DnmAvailability.Available => IntelligenceAvailability.Available,
		DnmAvailability.Unsupported => IntelligenceAvailability.Unsupported,
		DnmAvailability.DeviceNotEligible => IntelligenceAvailability.DeviceNotEligible,
		DnmAvailability.NotEnabled => IntelligenceAvailability.NotEnabled,
		DnmAvailability.ModelNotReady => IntelligenceAvailability.ModelNotReady,
		_ => IntelligenceAvailability.Unavailable,
	};

	public int? ContextSize
	{
		get
		{
			var size = DnmIntelligenceModel.ContextSize;
			return size > 0 ? (int)size : null;
		}
	}

	public Task<int?> CountTokensAsync(string text, CancellationToken cancellationToken = default)
	{
		var counted = new TaskCompletionSource<int?>(TaskCreationOptions.RunContinuationsAsynchronously);
		DnmIntelligenceModel.CountTokens(text, tokens => counted.TrySetResult(tokens >= 0 ? (int)tokens : null));
		return counted.Task.WaitAsync(cancellationToken);
	}

	public IIntelligenceSession StartSession(string instructions, IReadOnlyList<IntelligenceLookup> lookups) =>
		new Session(instructions, lookups);

	private sealed class Session : IIntelligenceSession
	{
		private readonly CancellationTokenSource _disposed = new();
		private readonly DnmLookup[] _lookups;
		private readonly DnmIntelligenceSession _session;

		/// <summary>The question being answered: stopping it also stops a lookup it's waiting on.</summary>
		private CancellationTokenSource? _asking;

		public Session(string instructions, IReadOnlyList<IntelligenceLookup> lookups)
		{
			_lookups = lookups.Select(Bridge).ToArray();
			_session = new DnmIntelligenceSession(instructions, _lookups);
		}

		/// <summary>
		/// The model calls back from its own thread and waits for the reply.
		/// A failed lookup tells the model so in fixed words, never the
		/// exception's, which could carry an address or a token.
		/// </summary>
		private DnmLookup Bridge(IntelligenceLookup lookup) =>
			new(lookup.Name, lookup.Purpose, IntelligenceToolArguments.Schema(lookup.Parameters), (arguments, reply) =>
			{
				// Read now, on the model's thread, so the lookup belongs to the
				// question that asked for it. One that arrives after its question
				// or the session has ended starts already cancelled.
				CancellationToken token;
				try
				{
					token = (_asking ?? _disposed).Token;
				}
				catch (ObjectDisposedException)
				{
					token = new CancellationToken(canceled: true);
				}

				_ = Task.Run(async () =>
				{
					try
					{
						reply.Finish(await lookup.Run(arguments ?? string.Empty, token));
					}
					catch (Exception)
					{
						reply.Finish("The lookup failed. LibreNMS could not be reached.");
					}
				});
			});

		public async Task<IntelligenceAnswer> AskAsync(string question, Action<string> onText, CancellationToken cancellationToken = default)
		{
			var finished = new TaskCompletionSource<IntelligenceAnswer>(TaskCreationOptions.RunContinuationsAsynchronously);
			using var asking = CancellationTokenSource.CreateLinkedTokenSource(_disposed.Token);
			_asking = asking;
			_session.Ask(
				question,
				text => onText(text ?? string.Empty),
				(answer, usage, error) =>
				{
					if (error is null)
						finished.TrySetResult(new IntelligenceAnswer(answer ?? string.Empty, usage));
					else if (cancellationToken.IsCancellationRequested)
						finished.TrySetCanceled(cancellationToken);
					else
						finished.TrySetException(new IntelligenceException(Describe(error), answer, IntelligenceFailures.FromNativeReason(error.LocalizedFailureReason)));
				});

			// Stopping is the model's to confirm: the answer ends when its completion arrives.
			// A lookup in flight is cancelled too, or Stop would wait for LibreNMS to answer or time out.
			using var stopping = cancellationToken.Register(() =>
			{
				// The model first: cancelling runs the lookup's own callbacks, and
				// one that threw mustn't leave the model writing.
				_session.Stop();
				asking.Cancel();
			});
			try
			{
				return await finished.Task;
			}
			finally
			{
				_asking = null;
			}
		}

		/// <summary>The error's own name first ("contextSizeExceeded"), which is what the write-up needs.</summary>
		private static string Describe(NSError error) =>
			string.IsNullOrEmpty(error.LocalizedFailureReason) || error.LocalizedFailureReason == error.LocalizedDescription
				? error.LocalizedDescription
				: error.LocalizedFailureReason + ": " + error.LocalizedDescription;

		public void Dispose()
		{
			_disposed.Cancel();
			_session.Stop();
			_session.Dispose();
			foreach (var lookup in _lookups)
				lookup.Dispose();
			_disposed.Dispose();
		}
	}
}
