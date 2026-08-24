using MacroDeck.Localization;
using MacroDeck.Sdk.Actions;
using MacroDeck.Sdk.Weather;

namespace MacroDeck.SampleWeatherPlugin.Actions;

/// <summary>
/// The dynamic-options action of the three: <see cref="Parameters"/> declares its one field with no
/// options attached, and <see cref="GetDynamicOptionsAsync"/> supplies them itself rather than through
/// a named options source - see <see cref="IDynamicOptionsActionDefinition"/>. Forces the condition
/// the next synthetic reading reports, so a demo deck can show every icon the Weather widget knows
/// without waiting on the sine wave to get there.
/// </summary>
internal sealed class SetConditionAction(WeatherIntegration integration) : IActionDefinition, IDynamicOptionsActionDefinition
{
	public string Id => "set-condition";

	public LocalizedText Name => Strings.Actions.SetCondition.Name();

	public LocalizedText Description => Strings.Actions.SetCondition.Description();

	public IReadOnlyList<ActionParameter> Parameters { get; } =
	[
		ActionParameter.DynamicChoice("condition",
			label: Strings.Actions.SetCondition.Condition.Label(),
			required: true)
	];

	public IActionExecutor CreateExecutor() => new Executor(integration);

	/// <summary>An option's <c>Value</c> is the wire identity the executor parses back, so it stays the
	/// enum name; only its <c>Label</c> is localized.</summary>
	public Task<DynamicOptionsResult> GetDynamicOptionsAsync(DynamicOptionsContext context, CancellationToken cancellationToken)
		=> Task.FromResult(new DynamicOptionsResult
		{
			Options =
			[
				.. WeatherIntegration.SelectableConditions
					.Select(condition => new ActionParameterOption
					{
						Value = condition.ToString(),
						Label = ConditionLabel(condition)
					})
			]
		});

	private static LocalizedText ConditionLabel(WeatherCondition condition) => condition switch
	{
		WeatherCondition.Clear => Strings.Conditions.Clear(),
		WeatherCondition.PartlyCloudy => Strings.Conditions.PartlyCloudy(),
		WeatherCondition.Overcast => Strings.Conditions.Overcast(),
		WeatherCondition.Rain => Strings.Conditions.Rain(),
		WeatherCondition.Thunderstorm => Strings.Conditions.Thunderstorm(),
		WeatherCondition.Snow => Strings.Conditions.Snow(),

		// Unreachable while SelectableConditions is the only caller, and deliberately not a throw: an
		// enum value added to that list without a resource should show its name, not break the picker.
		_ => condition.ToString()
	};

	private sealed class Executor(WeatherIntegration integration) : IActionExecutor
	{
		public Task<ActionResult> ExecuteAsync(ActionExecutionContext context)
		{
			if (context.Parameters.GetValueOrDefault("condition") is not string text ||
				!Enum.TryParse<WeatherCondition>(text, out var condition))
			{
				return Task.FromResult(ActionResult.Failed(ActionErrorCodes.InvalidParameter,
					MacroDeckStrings.Validation.InvalidValue(Strings.Actions.SetCondition.Condition.Label())));
			}

			integration.Station.SetForcedCondition(condition);
			return ActionResult.SucceededTask;
		}
	}
}
