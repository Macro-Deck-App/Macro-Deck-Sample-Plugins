using MacroDeck.Localization;
using MacroDeck.Sdk.Actions;

namespace MacroDeck.SampleWeatherPlugin.Actions;

/// <summary>
/// The slider action of the three: a Slider widget drags <see cref="SliderValueParameter"/> and reads
/// it back through <see cref="GetSliderStateAsync"/> for two-way binding. Sets the temperature above
/// which the next <c>weather-refreshed</c> event reports <c>isAlert</c>.
/// </summary>
internal sealed class SetAlertThresholdAction(WeatherIntegration integration) : IActionDefinition, ISliderActionDefinition
{
	private const double Min = -10;
	private const double Max = 40;

	public string Id => "set-alert-threshold";

	public LocalizedText Name => Strings.Actions.SetAlertThreshold.Name();

	public LocalizedText Description => Strings.Actions.SetAlertThreshold.Description();

	public string SliderValueParameter => "thresholdCelsius";

	public IReadOnlyList<ActionParameter> Parameters { get; } =
	[
		ActionParameter.Slider("thresholdCelsius",
			Min,
			Max,
			label: Strings.Actions.SetAlertThreshold.Threshold.Label(),
			step: 1,
			defaultValue: 30)
	];

	public IActionExecutor CreateExecutor() => new Executor(integration);

	public Task<SliderActionState?> GetSliderStateAsync(
		IReadOnlyDictionary<string, object?> parameters,
		CancellationToken cancellationToken)
		=> Task.FromResult<SliderActionState?>(new SliderActionState(Min, Max, 1, integration.AlertThresholdCelsius));

	private sealed class Executor(WeatherIntegration integration) : IActionExecutor
	{
		public Task<ActionResult> ExecuteAsync(ActionExecutionContext context)
		{
			if (context.Parameters.GetValueOrDefault("thresholdCelsius") is not double threshold)
			{
				// Generic validation wording comes from Macro Deck's own catalog rather than from a key of
				// this plugin's, so a translator never re-translates a sentence the app already ships.
				return Task.FromResult(ActionResult.Failed(ActionErrorCodes.InvalidParameter,
					MacroDeckStrings.Validation.InvalidValue(Strings.Actions.SetAlertThreshold.Threshold.Label())));
			}

			integration.AlertThresholdCelsius = threshold;
			return ActionResult.SucceededTask;
		}
	}
}
