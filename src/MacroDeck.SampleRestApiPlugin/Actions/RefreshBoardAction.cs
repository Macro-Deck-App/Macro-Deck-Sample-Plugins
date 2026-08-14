using MacroDeck.Sdk.Actions;

namespace MacroDeck.SampleRestApiPlugin.Actions;

/// <summary>The read-shaped action: re-reads the board so the variables catch up with the server.</summary>
internal sealed class RefreshBoardAction(RestApiIntegration integration) : IActionDefinition
{
	public string Id => "refresh-board";

	public string Name => "Refresh board";

	public string Description => "Re-reads the open cards from the Task Board.";

	public IReadOnlyList<ActionParameter> Parameters { get; } = [];

	public IActionExecutor CreateExecutor() => new Executor(integration);

	private sealed class Executor(RestApiIntegration integration) : IActionExecutor
	{
		public async Task<ActionResult> ExecuteAsync(ActionExecutionContext context)
		{
			var failure = await integration.RefreshAsync(context.CancellationToken);
			return failure is null ? ActionResult.Success() : TaskBoardActionResults.From(failure);
		}
	}
}
