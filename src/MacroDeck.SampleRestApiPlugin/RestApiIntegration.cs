using MacroDeck.SampleRestApiPlugin.Actions;
using MacroDeck.SampleRestApiPlugin.Api;
using MacroDeck.SampleRestApiPlugin.ConfigFlow;
using MacroDeck.Sdk;
using MacroDeck.Sdk.Actions;
using MacroDeck.Sdk.ConfigFlow;
using MacroDeck.Sdk.Events;
using MacroDeck.Sdk.Issues;
using MacroDeck.Sdk.Notifications;
using MacroDeck.Sdk.Variables;
using Serilog;

namespace MacroDeck.SampleRestApiPlugin;

/// <summary>
/// How a plugin for a third-party service is put together: a typed client behind
/// <see cref="IHttpClientFactory"/>, credentials that arrive from a config flow, variables and events
/// fed by API state, and integration issues for the problems a user can actually fix.
/// </summary>
public sealed class RestApiIntegration : IPluginIntegration, IVariableProvider, IEventProvider,
	IConfigFlowProvider, IIntegrationIssueProvider
{
	internal const string CardCompletedEventId = "card-completed";

	private const string NotConfiguredIssueId = "not-configured";
	private const string UnauthorizedIssueId = "unauthorized";
	private const string UnreachableIssueId = "unreachable";

	private const string FailureNotificationKey = "task-board-failure";

	private readonly TaskBoardClient _client;
	private readonly TaskBoardCredentials _credentials;
	private readonly ILogger _logger;

	private IIntegrationContext? _context;
	private IReadOnlyList<TaskBoardCard> _openCards = [];

	public RestApiIntegration(TaskBoardClient client, TaskBoardCredentials credentials, ILogger logger)
	{
		_client = client;
		_credentials = credentials;
		_logger = logger.ForContext<RestApiIntegration>();
		Actions = [new RefreshBoardAction(this), new CreateCardAction(this), new CompleteCardAction(this)];
	}

	public IReadOnlyList<IActionDefinition> Actions { get; }

	public async Task InitializeAsync(IIntegrationContext context)
	{
		_context = context;

		// Runs again whenever the host reports the configuration changed, so applying the entry here
		// rather than in the flow is what makes reconfiguration take effect without a restart.
		var entries = await context.Config.GetEntriesAsync();
		if (entries.Count == 0)
		{
			_credentials.Clear();
			_logger.Information("Waiting for configuration.");
			return;
		}

		var entryId = entries[0].Id;
		var serverUrl = await context.Config.GetStringAsync(entryId, TaskBoardConfigFlow.ServerUrlKey);
		var token = await context.Config.GetSecretAsync(entryId, TaskBoardConfigFlow.TokenKey);

		_credentials.Apply(
			Uri.TryCreate(serverUrl, UriKind.Absolute, out var baseAddress) ? baseAddress : null,
			token);

		await RefreshAsync(CancellationToken.None);
	}

	public Task ShutdownAsync()
	{
		_context = null;
		_credentials.Clear();
		return Task.CompletedTask;
	}

	internal TaskBoardClient Client => _client;

	internal IReadOnlyList<TaskBoardCard> OpenCards => _openCards;

	/// <summary>Re-reads the board and reports the outcome once, through a keyed notification that
	/// replaces the previous one instead of stacking up. Returns the failure so a caller can report the
	/// real reason rather than inventing one.</summary>
	internal async Task<TaskBoardException?> RefreshAsync(CancellationToken cancellationToken)
	{
		try
		{
			_openCards = await _client.GetCardsAsync(openOnly: true, cancellationToken);
			_context?.Notifications.Dismiss(FailureNotificationKey);
			return null;
		}
		catch (TaskBoardException exception)
		{
			_openCards = [];
			_logger.Warning(exception, "Reading the board failed ({Reason}).", exception.Reason);

			if (exception.Reason != TaskBoardFailure.NotConfigured)
			{
				_context?.Notifications.Notify(new UserNotificationRequest
				{
					Title = "Task Board unavailable",
					Message = exception.Message,
					Level = UserNotificationLevel.Warning,
					Key = FailureNotificationKey
				});
			}

			return exception;
		}
	}

	internal void PublishCardCompleted(TaskBoardCard card)
		=> _context?.Events.Publish(CardCompletedEventId, new Dictionary<string, object?>
		{
			["cardId"] = card.Id,
			["title"] = card.Title
		});

	public IReadOnlyList<ProvidedVariable> ProvidedVariables { get; } =
	[
		new ProvidedVariable("sample_taskboard_open_cards", VariableType.Numeric, RefreshInterval: TimeSpan.FromMinutes(1))
		{
			DefinitionId = "open-cards"
		},
		new ProvidedVariable("sample_taskboard_next_card", VariableType.Text) { DefinitionId = "next-card" },
		new ProvidedVariable("sample_taskboard_configured", VariableType.Boolean) { DefinitionId = "configured" }
	];

	/// <summary>Every value here is unconfigured until the flow has run: returning null is how a provider
	/// says "unavailable", which the host renders as an empty value rather than a zero.</summary>
	public Task<object?> GetValueAsync(string name, CancellationToken cancellationToken)
		=> Task.FromResult(name switch
		{
			"sample_taskboard_configured" => (object?)_credentials.IsConfigured,
			_ when !_credentials.IsConfigured => null,
			"sample_taskboard_open_cards" => _openCards.Count,
			"sample_taskboard_next_card" => _openCards.Count > 0 ? _openCards[0].Title : null,
			_ => null
		});

	public IReadOnlyList<EventDefinition> EventDefinitions { get; } =
	[
		new EventDefinition
		{
			Id = CardCompletedEventId,
			Name = "Card completed",
			Description = "Raised when this integration completes a card on the Task Board.",
			PayloadParameters =
			[
				ActionParameter.Text("cardId", "Card id"),
				ActionParameter.Text("title", "Title")
			]
		}
	];

	public IConfigFlow CreateConfigFlow() => new TaskBoardConfigFlow(_client);

	public bool AllowsMultipleConfigurations => false;

	/// <summary>
	/// The problems a user can act on, checked live rather than cached - a stale issue list is worse
	/// than none. A healthy integration returns an empty list.
	/// </summary>
	public async Task<IReadOnlyList<IntegrationIssue>> GetIssuesAsync(CancellationToken cancellationToken = default)
	{
		if (!_credentials.IsConfigured)
		{
			return
			[
				new IntegrationIssue
				{
					Id = NotConfiguredIssueId,
					Title = "Task Board is not configured",
					Description = "Add the server URL and a token to start using the integration.",
					Severity = IntegrationIssueSeverity.Error,
					ActionLabel = "Configure"
				}
			];
		}

		try
		{
			await _client.VerifyAsync(cancellationToken);
			return [];
		}
		catch (TaskBoardException exception) when (exception.Reason == TaskBoardFailure.Unauthorized)
		{
			return
			[
				new IntegrationIssue
				{
					Id = UnauthorizedIssueId,
					Title = "The Task Board token is no longer valid",
					Description = "The server rejected the stored token. Sign in again to replace it.",
					Severity = IntegrationIssueSeverity.Error,
					ActionLabel = "Sign in again"
				}
			];
		}
		catch (TaskBoardException exception)
		{
			return
			[
				new IntegrationIssue
				{
					Id = UnreachableIssueId,
					Title = "The Task Board server did not answer",
					Description = exception.Message,
					Severity = IntegrationIssueSeverity.Warning,
					ActionLabel = "Retry"
				}
			];
		}
	}

	/// <summary>
	/// What pressing an issue's action does. Two of these hand the user back to the config flow, which
	/// is the only thing that can actually fix them; the third just retries.
	/// </summary>
	public async Task<IssueResolution> ResolveIssueAsync(string issueId, CancellationToken cancellationToken = default)
	{
		switch (issueId)
		{
			case NotConfiguredIssueId:
			case UnauthorizedIssueId:
				return IssueResolution.Ok(followUp: IssueResolutionFollowUp.StartConfigFlow);

			case UnreachableIssueId:
				var failure = await RefreshAsync(cancellationToken);
				return failure is null
					? IssueResolution.Ok("The Task Board server answered again.")
					: IssueResolution.Failed(failure.Message);

			default:
				return IssueResolution.Failed($"Unknown issue '{issueId}'.");
		}
	}
}
