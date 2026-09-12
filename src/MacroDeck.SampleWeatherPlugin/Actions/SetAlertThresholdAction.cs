using MacroDeck.Localization;
using MacroDeck.Sdk.Actions;

namespace MacroDeck.SampleWeatherPlugin.Actions;

/// <summary>
/// Sets the temperature above which the next <c>weather-refreshed</c> event reports <c>isAlert</c>. A
/// Slider widget binds the writable <c>sample_alert_threshold_celsius</c> variable instead, which reads
/// the threshold back for two-way binding.
/// </summary>
internal sealed class SetAlertThresholdAction(WeatherIntegration integration) : IActionDefinition
{
	internal const double Min = -10;
	internal const double Max = 40;

	public string Id => "set-alert-threshold";

	public LocalizedText Name => Strings.Actions.SetAlertThreshold.Name();

	public LocalizedText Description => Strings.Actions.SetAlertThreshold.Description();

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
