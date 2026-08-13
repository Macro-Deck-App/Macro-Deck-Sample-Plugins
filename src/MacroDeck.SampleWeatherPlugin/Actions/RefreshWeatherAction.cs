using MacroDeck.Sdk.Actions;

namespace MacroDeck.SampleWeatherPlugin.Actions;

/// <summary>
/// The plain action of the three: no parameters, no dynamic behavior. Advances the sample's
/// synthetic weather reading by one tick, which republishes the <c>weather-refreshed</c> event and
/// updates the temperature variable and the weather snapshot - the one button that ties every other
/// capability in this sample together.
/// </summary>
internal sealed class RefreshWeatherAction(WeatherIntegration integration) : IActionDefinition
{
	public string Id => "refresh-weather";

	public string Name => "Refresh weather";

	public string Description => "Advances the sample's synthetic weather reading and republishes it.";

	public IReadOnlyList<ActionParameter> Parameters { get; } = [];

	public IActionExecutor CreateExecutor() => new Executor(integration);

	private sealed class Executor(WeatherIntegration integration) : IActionExecutor
	{
		public Task<ActionResult> ExecuteAsync(ActionExecutionContext context)
		{
			integration.Station.Tick();
			return ActionResult.SucceededTask;
		}
	}
}
