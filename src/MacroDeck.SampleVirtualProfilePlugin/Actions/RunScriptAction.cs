using MacroDeck.Plugin.Hosting.Transport;
using MacroDeck.Sdk.Actions;

namespace MacroDeck.SampleVirtualProfilePlugin.Actions;

/// <summary>
/// Runs one of the user's own scripts through <c>IScriptApi</c>. The option list comes from
/// <c>GetScripts()</c>, which is served from a host-pushed cache rather than a round trip - so it is
/// empty for a moment right after connecting, and that is expected rather than an error.
/// </summary>
internal sealed class RunScriptAction(ControlRoomIntegration integration)
	: IActionDefinition, IDynamicOptionsActionDefinition
{
	public string Id => "run-script";

	public string Name => "Run script";

	public string Description => "Runs a script configured in Macro Deck.";

	public IReadOnlyList<ActionParameter> Parameters { get; } =
	[
		ActionParameter.DynamicChoice("scriptId", label: "Script", required: true)
	];

	public IActionExecutor CreateExecutor() => new Executor(integration);

	public Task<DynamicOptionsResult> GetDynamicOptionsAsync(DynamicOptionsContext context, CancellationToken cancellationToken)
	{
		var scripts = integration.Context?.Scripts.GetScripts() ?? [];
		return Task.FromResult(new DynamicOptionsResult
		{
			Options = [.. scripts.Select(script => new ActionParameterOption { Value = script.Id, Label = script.Name })]
		});
	}

	private sealed class Executor(ControlRoomIntegration integration) : IActionExecutor
	{
		public async Task<ActionResult> ExecuteAsync(ActionExecutionContext context)
		{
			if (integration.Context is not { } integrationContext)
			{
				return ActionResult.Failed(ActionErrorCodes.Unavailable, "The integration is not initialized.");
			}

			if (context.Parameters.GetValueOrDefault("scriptId") is not string { Length: > 0 } scriptId)
			{
				return ActionResult.Failed(ActionErrorCodes.InvalidParameter, "scriptId is required.");
			}

			try
			{
				// The script's own result is this action's result: a failing script must not look like a
				// successful button press.
				return await integrationContext.Scripts.RunAsync(scriptId, context.OriginClientId, context.CancellationToken);
			}
			catch (HostInvocationException exception)
			{
				return ActionResult.Failed(ActionErrorCodes.NotConnected, exception.Message);
			}
		}
	}
}
