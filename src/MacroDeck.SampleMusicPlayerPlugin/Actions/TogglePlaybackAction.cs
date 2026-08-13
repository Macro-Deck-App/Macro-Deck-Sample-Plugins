using MacroDeck.Sdk.Actions;

namespace MacroDeck.SampleMusicPlayerPlugin.Actions;

/// <summary>
/// The relationship between a provider capability and an ordinary action: the host's own media widget
/// drives <c>IMusicPlayer.TogglePlayPauseAsync</c>, and this action reaches the same state so a plain
/// button can do it too.
/// </summary>
internal sealed class TogglePlaybackAction(MusicPlayerIntegration integration)
	: IActionDefinition, IDynamicOptionsActionDefinition
{
	public string Id => "toggle-playback";

	public string Name => "Play/pause";

	public string Description => "Toggles playback on one of the sample players.";

	public IReadOnlyList<ActionParameter> Parameters { get; } =
	[
		ActionParameter.DynamicChoice("player", label: "Player", required: true)
	];

	public IActionExecutor CreateExecutor() => new Executor(integration);

	public Task<DynamicOptionsResult> GetDynamicOptionsAsync(DynamicOptionsContext context, CancellationToken cancellationToken)
		=> Task.FromResult(new DynamicOptionsResult { Options = integration.InstanceOptions() });

	private sealed class Executor(MusicPlayerIntegration integration) : IActionExecutor
	{
		public Task<ActionResult> ExecuteAsync(ActionExecutionContext context)
		{
			if (context.Parameters.GetValueOrDefault("player") is not string instanceId ||
				integration.EngineOf(instanceId) is not { } engine)
			{
				return Task.FromResult(ActionResult.Failed(ActionErrorCodes.InvalidParameter,
					"player must name one of the sample players."));
			}

			engine.Toggle();
			return ActionResult.SucceededTask;
		}
	}
}
