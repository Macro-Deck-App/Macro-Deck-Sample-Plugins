using MacroDeck.Localization;
using MacroDeck.Sdk.Actions;

namespace MacroDeck.SampleMusicPlayerPlugin.Actions;

/// <summary>
/// Sets one player's volume from a button. A Slider widget binds the writable <c>sample_music_volume</c>
/// variable instead, which reads the volume the player actually has back.
/// </summary>
internal sealed class SetVolumeAction(MusicPlayerIntegration integration)
	: IActionDefinition, IDynamicOptionsActionDefinition
{
	private const double Min = 0;
	private const double Max = 100;

	public string Id => "set-volume";

	public LocalizedText Name => Strings.Actions.SetVolume.Name();

	public LocalizedText Description => Strings.Actions.SetVolume.Description();

	public IReadOnlyList<ActionParameter> Parameters { get; } =
	[
		ActionParameter.DynamicChoice("player", label: Strings.Fields.Player.Label(), required: true),
		ActionParameter.Slider("volume", Min, Max, label: Strings.Fields.Volume.Label(), step: 1, defaultValue: 60)
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
					MacroDeckStrings.Validation.InvalidValue(Strings.Fields.Player.Label())));
			}

			if (context.Parameters.GetValueOrDefault("volume") is not double volume)
			{
				return Task.FromResult(ActionResult.Failed(ActionErrorCodes.InvalidParameter,
					MacroDeckStrings.Validation.InvalidValue(Strings.Fields.Volume.Label())));
			}

			engine.SetVolume((int)volume);
			return ActionResult.SucceededTask;
		}
	}
}
