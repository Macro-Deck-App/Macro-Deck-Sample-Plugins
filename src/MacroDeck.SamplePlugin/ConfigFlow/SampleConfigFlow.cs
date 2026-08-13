using MacroDeck.Sdk.Actions;
using MacroDeck.Sdk.ConfigFlow;

namespace MacroDeck.SamplePlugin.ConfigFlow;

/// <summary>
/// A single-step setup flow that collects the location the sample's synthetic weather station
/// reports for. Deliberately the simplest possible shape - one step, one required field, no OAuth -
/// so it reads as the config-flow contract's minimum rather than a second walkthrough of
/// <c>IOAuthSession</c> (see the Spotify integration for that).
///
/// A new instance is created per session (<see cref="SampleIntegration.CreateConfigFlow"/>), but this
/// flow keeps no session state of its own: the submitted value goes straight into the completed
/// config entry, and <see cref="SampleIntegration.InitializeAsync"/> is what actually reads it back -
/// the same division of labor <c>SpotifyIntegration.ConnectFromConfig</c> uses.
/// </summary>
internal sealed class SampleConfigFlow : IConfigFlow
{
	/// <summary>
	/// The field name, which is also the config entry key <see cref="SampleIntegration.InitializeAsync"/>
	/// reads back - a config flow's <see cref="ConfigFlowStep.Fields"/> are persisted under their own
	/// names automatically, with no need to echo them into <see cref="ConfigFlowResult.Complete"/>'s
	/// <c>values</c> argument.
	/// </summary>
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

		return Task.FromResult(ConfigFlowResult.Complete($"Sample ({location})"));
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
