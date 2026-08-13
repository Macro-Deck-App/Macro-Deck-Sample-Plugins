using MacroDeck.SampleRestApiPlugin.Api;
using MacroDeck.Sdk.Actions;

namespace MacroDeck.SampleRestApiPlugin.Actions;

/// <summary>
/// Changes remote state and then tells the rest of the plugin about it: the card list is re-read so
/// the variables agree with the server, and the event fires for anything listening.
/// </summary>
internal sealed class CompleteCardAction(RestApiIntegration integration)
	: IActionDefinition, IDynamicOptionsActionDefinition
{
	public string Id => "complete-card";

	public string Name => "Complete card";

	public string Description => "Marks a card on the Task Board as done.";

	public IReadOnlyList<ActionParameter> Parameters { get; } =
	[
		ActionParameter.DynamicChoice("cardId", label: "Card", required: true)
	];

	public IActionExecutor CreateExecutor() => new Executor(integration);

	public async Task<DynamicOptionsResult> GetDynamicOptionsAsync(DynamicOptionsContext context, CancellationToken cancellationToken)
	{
		try
		{
			var cards = await integration.Client.GetCardsAsync(openOnly: true, cancellationToken);
			var matching = string.IsNullOrWhiteSpace(context.Filter)
				? cards
				: [.. cards.Where(card => card.Title.Contains(context.Filter, StringComparison.OrdinalIgnoreCase))];

			return new DynamicOptionsResult
			{
				Options = [.. matching.Select(card => new ActionParameterOption { Value = card.Id, Label = card.Title })]
			};
		}
		catch (TaskBoardException)
		{
			return new DynamicOptionsResult { Options = [] };
		}
	}

	private sealed class Executor(RestApiIntegration integration) : IActionExecutor
	{
		public async Task<ActionResult> ExecuteAsync(ActionExecutionContext context)
		{
			if (context.Parameters.GetValueOrDefault("cardId") is not string { Length: > 0 } cardId)
			{
				return ActionResult.Failed(ActionErrorCodes.InvalidParameter, "cardId is required.");
			}

			try
			{
				var card = await integration.Client.CompleteCardAsync(cardId, context.CancellationToken);
				integration.PublishCardCompleted(card);
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
