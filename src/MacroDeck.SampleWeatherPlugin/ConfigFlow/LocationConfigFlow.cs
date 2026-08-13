using MacroDeck.Sdk.Actions;
using MacroDeck.Sdk.ConfigFlow;

namespace MacroDeck.SampleWeatherPlugin.ConfigFlow;

/// <summary>
/// A single-step flow collecting the location the station reports for - deliberately the contract's
/// minimum. See the REST API sample for a multi-step flow with secrets and an external step.
/// </summary>
internal sealed class LocationConfigFlow : IConfigFlow
{
	/// <summary>Also the config entry key <see cref="WeatherIntegration.InitializeAsync"/> reads back: a
	/// step's fields are persisted under their own names, with nothing to echo into
	/// <see cref="ConfigFlowResult.Complete"/>.</summary>
	internal const string LocationFieldName = "location";

	private const string StepId = "location";

	public Task<ConfigFlowResult> StartAsync(IConfigFlowContext context, CancellationToken cancellationToken)
		=> Task.FromResult(ConfigFlowResult.Step(BuildStep()));

	public Task<ConfigFlowResult> SubmitAsync(
		string stepId,
		IReadOnlyDictionary<string, object?> input,
		IConfigFlowContext context,
		CancellationToken cancellationToken)
	{
		if (!string.Equals(stepId, StepId, StringComparison.Ordinal))
		{
			return Task.FromResult(ConfigFlowResult.Error(BuildStep(), "Unknown step."));
		}

		if (input.GetValueOrDefault(LocationFieldName) is not string { Length: > 0 } location)
		{
			return Task.FromResult(ConfigFlowResult.Error(BuildStep(),
				"Enter a location name.",
				new Dictionary<string, string> { [LocationFieldName] = "Required." }));
		}

		return Task.FromResult(ConfigFlowResult.Complete($"Weather ({location})"));
	}

	private static ConfigFlowStep BuildStep() => new()
	{
		StepId = StepId,
		Title = "Sample location",
		Description = "Pick the location the sample's synthetic weather station reports for.",
		Fields =
		[
			ActionParameter.Text(LocationFieldName,
				label: "Location name",
				defaultValue: "Berlin, Germany",
				required: true)
		]
	};
}
