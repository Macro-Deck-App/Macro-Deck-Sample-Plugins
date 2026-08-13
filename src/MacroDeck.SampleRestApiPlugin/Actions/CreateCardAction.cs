using MacroDeck.SampleRestApiPlugin.Api;
using MacroDeck.Sdk.Actions;

namespace MacroDeck.SampleRestApiPlugin.Actions;

/// <summary>
/// The POST-shaped action: parameters become a request body, and the API's answer decides what the
/// action reports. Its list options come from the API itself, so the picker always shows the lists the
/// account really has.
/// </summary>
internal sealed class CreateCardAction(RestApiIntegration integration)
	: IActionDefinition, IDynamicOptionsActionDefinition
{
	private const string HighPriority = "high";

	public string Id => "create-card";

	public string Name => "Create card";

	public string Description => "Creates a card on the Task Board.";

	public IReadOnlyList<ActionParameter> Parameters { get; } =
	[
		ActionParameter.Text("title", label: "Title", required: true, maxLength: 120),
		ActionParameter.DynamicChoice("listId", label: "List", required: true),
		ActionParameter.Choice("priority",
			[
				new ActionParameterOption { Value = "low", Label = "Low" },
				new ActionParameterOption { Value = "normal", Label = "Normal" },
				new ActionParameterOption { Value = HighPriority, Label = "High" }
			],
			label: "Priority",
			defaultValue: "normal"),
		ActionParameter.MultilineText("notes", label: "Notes", placeholder: "Optional details"),
		// Only a high-priority card gets a due date in this workflow, so the field follows the choice.
		ActionParameter.DateTime("dueAt", label: "Due").OnlyWhen("priority", HighPriority)
	];

	public IActionExecutor CreateExecutor() => new Executor(integration);

	public async Task<DynamicOptionsResult> GetDynamicOptionsAsync(DynamicOptionsContext context, CancellationToken cancellationToken)
	{
		try
		{
			var lists = await integration.Client.GetListsAsync(cancellationToken);
			return new DynamicOptionsResult
			{
				Options = [.. lists.Select(list => new ActionParameterOption { Value = list.Id, Label = list.Name })],
				CacheSeconds = 60
			};
		}
		catch (TaskBoardException)
		{
			// An empty list is the honest answer while the integration cannot reach its API; the issue
			// provider is what tells the user why.
			return new DynamicOptionsResult { Options = [] };
		}
	}

	private sealed class Executor(RestApiIntegration integration) : IActionExecutor
	{
		public async Task<ActionResult> ExecuteAsync(ActionExecutionContext context)
		{
			if (context.Parameters.GetValueOrDefault("title") is not string { Length: > 0 } title)
			{
				return ActionResult.Failed(ActionErrorCodes.InvalidParameter, "title is required.");
			}

			if (context.Parameters.GetValueOrDefault("listId") is not string { Length: > 0 } listId)
			{
				return ActionResult.Failed(ActionErrorCodes.InvalidParameter, "listId is required.");
			}

			var request = new CreateCardRequest(
				title,
				listId,
				context.Parameters.GetValueOrDefault("priority") as string ?? "normal",
				context.Parameters.GetValueOrDefault("notes") as string,
				context.Parameters.GetValueOrDefault("dueAt") is string due &&
					DateTimeOffset.TryParse(due, out var dueAt)
						? dueAt
						: null);

			try
			{
				// The token belongs to the invocation, not the plugin: a cancelled or timed-out call has
				// to stop the HTTP request too.
				await integration.Client.CreateCardAsync(request, context.CancellationToken);
				await integration.RefreshAsync(context.CancellationToken);
				return ActionResult.Success();
			}
			catch (TaskBoardException exception)
			{
				return TaskBoardActionResults.From(exception);
			}
		}
	}
}
