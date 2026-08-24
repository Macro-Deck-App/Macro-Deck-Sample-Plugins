using MacroDeck.Localization;
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
			return Task.FromResult(ConfigFlowResult.Error(BuildStep(), Strings.ConfigFlow.Location.UnknownStep()));
		}

		if (input.GetValueOrDefault(LocationFieldName) is not string { Length: > 0 } location)
		{
			var required = MacroDeckStrings.Validation.Required(Strings.ConfigFlow.Location.LocationName.Label());

			return Task.FromResult(ConfigFlowResult.Error(BuildStep(),
				required,
				new Dictionary<string, LocalizedText> { [LocationFieldName] = required }));
		}

		// Deliberately a plain string, not a LocalizedText: the host stores the entry title as its name
		// and the user renames it from there, so it is written once in the plugin's own language.
		return Task.FromResult(ConfigFlowResult.Complete($"Weather ({location})"));
	}

	private static ConfigFlowStep BuildStep() => new()
	{
		StepId = StepId,
		Title = Strings.ConfigFlow.Location.Title(),
		Description = Strings.ConfigFlow.Location.Description(),
		Fields =
		[
			ActionParameter.Text(LocationFieldName,
				label: Strings.ConfigFlow.Location.LocationName.Label(),
				defaultValue: "Berlin, Germany",
				required: true)
		]
	};
}
