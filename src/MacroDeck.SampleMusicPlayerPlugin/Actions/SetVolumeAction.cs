using MacroDeck.Sdk.Actions;

namespace MacroDeck.SampleMusicPlayerPlugin.Actions;

/// <summary>
/// A slider action whose state is read back from the player, so a Slider widget shows the volume the
/// player actually has - including after something else changed it.
/// </summary>
internal sealed class SetVolumeAction(MusicPlayerIntegration integration)
	: IActionDefinition, ISliderActionDefinition, IDynamicOptionsActionDefinition
{
	private const double Min = 0;
	private const double Max = 100;

	public string Id => "set-volume";

	public string Name => "Set volume";

	public string Description => "Sets the volume of one of the sample players.";

	public string SliderValueParameter => "volume";

	/// <summary>Volume is cheap to apply continuously, so the drag is not deferred to release.</summary>
	public bool CommitOnRelease => false;

	public IReadOnlyList<ActionParameter> Parameters { get; } =
	[
		ActionParameter.DynamicChoice("player", label: "Player", required: true),
		ActionParameter.Slider("volume", Min, Max, label: "Volume (%)", step: 1, defaultValue: 60)
	];

	public IActionExecutor CreateExecutor() => new Executor(integration);

	public Task<DynamicOptionsResult> GetDynamicOptionsAsync(DynamicOptionsContext context, CancellationToken cancellationToken)
		=> Task.FromResult(new DynamicOptionsResult { Options = integration.InstanceOptions() });

	public Task<SliderActionState?> GetSliderStateAsync(
		IReadOnlyDictionary<string, object?> parameters,
		CancellationToken cancellationToken)
	{
		var engine = parameters.GetValueOrDefault("player") is string instanceId
			? integration.EngineOf(instanceId)
			: null;

		// Null means "no state to bind to" - the widget keeps its configured value instead of jumping
		// to a number this action made up for a player it could not resolve.
		return Task.FromResult(engine is null
			? null
			: new SliderActionState(Min, Max, 1, engine.VolumePercent));
	}

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

			if (context.Parameters.GetValueOrDefault("volume") is not double volume)
			{
				return Task.FromResult(ActionResult.Failed(ActionErrorCodes.InvalidParameter,
					"volume must be a number."));
			}

			engine.SetVolume((int)volume);
			return ActionResult.SucceededTask;
		}
	}
}
