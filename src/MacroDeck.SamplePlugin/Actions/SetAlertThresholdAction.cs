using MacroDeck.Sdk.Actions;

namespace MacroDeck.SamplePlugin.Actions;

/// <summary>
/// The slider action of the three: a Slider widget drags <see cref="SliderValueParameter"/> and reads
/// it back through <see cref="GetSliderStateAsync"/> for two-way binding. Sets the temperature above
/// which the next <c>weather-refreshed</c> event reports <c>isAlert</c>.
/// </summary>
internal sealed class SetAlertThresholdAction(SampleIntegration integration) : IActionDefinition, ISliderActionDefinition
{
	private const double Min = -10;
	private const double Max = 40;

	public string Id => "set-alert-threshold";

	public string Name => "Set alert threshold";

	public string Description => "Sets the temperature (°C) above which weather-refreshed events report isAlert.";

	public string SliderValueParameter => "thresholdCelsius";

	public IReadOnlyList<ActionParameter> Parameters { get; } =
	[
		ActionParameter.Slider("thresholdCelsius", Min, Max, label: "Alert threshold (°C)", step: 1, defaultValue: 30)
	];

	public IActionExecutor CreateExecutor() => new Executor(integration);

	public Task<SliderActionState?> GetSliderStateAsync(
		IReadOnlyDictionary<string, object?> parameters,
		CancellationToken cancellationToken)
		=> Task.FromResult<SliderActionState?>(new SliderActionState(Min, Max, 1, integration.AlertThresholdCelsius));

	private sealed class Executor(SampleIntegration integration) : IActionExecutor
	{
		public Task<ActionResult> ExecuteAsync(ActionExecutionContext context)
		{
			if (context.Parameters.GetValueOrDefault("thresholdCelsius") is not double threshold)
			{
				return Task.FromResult(ActionResult.Failed(ActionErrorCodes.InvalidParameter,
					"thresholdCelsius must be a number."));
			}

			integration.AlertThresholdCelsius = threshold;
			return ActionResult.SucceededTask;
		}
	}
}
