using MacroDeck.Localization;
using MacroDeck.Plugin.Hosting.Transport;
using MacroDeck.Sdk.Actions;
using MacroDeck.Sdk.Widgets;

namespace MacroDeck.SampleVirtualProfilePlugin.Actions;

/// <summary>
/// Writes back to a widget the user owns, through <c>IWidgetApi</c>. The widget target parameter
/// defaults to <c>$self</c>, so the action styles the button it was triggered from unless another one
/// was picked, and an empty colour clears the override rather than setting one.
/// </summary>
internal sealed class StyleWidgetAction(ControlRoomIntegration integration)
	: IActionDefinition, IDynamicOptionsActionDefinition
{
	private const string WidgetParameter = "widget";
	private const string StateParameter = "state";

	public string Id => "style-widget";

	public LocalizedText Name => Strings.Actions.StyleWidget.Name();

	public LocalizedText Description => Strings.Actions.StyleWidget.Description();

	public IReadOnlyList<ActionParameter> Parameters { get; } =
	[
		ActionParameter.WidgetTarget(WidgetParameter, label: Strings.Actions.StyleWidget.Widget.Label()),
		ActionParameter.Text("label", label: Strings.Actions.StyleWidget.LabelText.Label(), maxLength: 40),
		ActionParameter.Color("backgroundColor", label: Strings.Actions.StyleWidget.Background.Label(), supportsReset: true),
		ActionParameter.Icon("icon", label: Strings.Actions.StyleWidget.Icon.Label()),
		// The states a widget has are the widget's own, so they cannot be listed at declaration time -
		// GetDynamicOptionsAsync reads them off the target the user picked.
		ActionParameter.DynamicChoice(StateParameter,
			label: Strings.Actions.StyleWidget.State.Label(),
			placeholder: Strings.WidgetStates.Current())
	];

	public IActionExecutor CreateExecutor() => new Executor(integration);

	/// <summary>
	/// The two sentinels every widget accepts, followed by whatever states this particular widget
	/// declares. A widget with a single appearance reports none, and then only the sentinels are offered.
	/// </summary>
	public Task<DynamicOptionsResult> GetDynamicOptionsAsync(DynamicOptionsContext context, CancellationToken cancellationToken)
	{
		List<ActionParameterOption> options =
		[
			new() { Value = WidgetStates.Current, Label = Strings.WidgetStates.Current() },
			new() { Value = WidgetStates.All, Label = Strings.WidgetStates.All() }
		];

		var target = context.CurrentParameters.GetValueOrDefault(WidgetParameter) as string;
		var widget = integration.Context?.Widgets.GetWidgets()
			.FirstOrDefault(candidate => string.Equals(candidate.Id, target, StringComparison.Ordinal));

		// A state's label is the one the deck author gave it, so it stays a literal.
		options.AddRange(widget?.States.Select(state => new ActionParameterOption
		{
			Value = state.Id,
			Label = state.Label
		}) ?? []);

		return Task.FromResult(new DynamicOptionsResult { Options = options });
	}

	private sealed class Executor(ControlRoomIntegration integration) : IActionExecutor
	{
		public async Task<ActionResult> ExecuteAsync(ActionExecutionContext context)
		{
			if (integration.Context is not { } integrationContext)
			{
				return ActionResult.Failed(ActionErrorCodes.Unavailable, Strings.Errors.NotInitialized());
			}

			var target = context.Parameters.GetValueOrDefault(WidgetParameter) as string;
			var widgetId = WidgetTargets.IsSelf(target) || string.IsNullOrWhiteSpace(target)
				? context.OwnerWidgetId
				: target;

			if (widgetId is null)
			{
				// $self only resolves for a widget-triggered run; a script or an automation has no owner.
				return ActionResult.Failed(ActionErrorCodes.InvalidParameter, Strings.Actions.StyleWidget.NoWidget());
			}

			var background = context.Parameters.GetValueOrDefault("backgroundColor") as string;
			var clear = WidgetAppearanceValues.IsReset(background)
				? new[] { WidgetAppearanceProperty.BackgroundColor }
				: [];

			// StateIds, not the deprecated State selector: a widget's states are addressed by their own
			// stable ids now, and the two sentinels cover "whichever it shows" and "all of them".
			var stateId = context.Parameters.GetValueOrDefault(StateParameter) as string;
			var request = new WidgetAppearanceRequest
			{
				WidgetId = widgetId,
				StateIds = [string.IsNullOrWhiteSpace(stateId) ? WidgetStates.Current : stateId],
				ClearProperties = clear,
				Patch = new WidgetAppearancePatch
				{
					Label = context.Parameters.GetValueOrDefault("label") as string,
					BackgroundColor = clear.Length == 0 ? background : null,
					IconId = context.Parameters.GetValueOrDefault("icon") as string
				}
			};

			try
			{
				var applied = await integrationContext.Widgets.ApplyAsync(request, context.CancellationToken);
				return applied
					? ActionResult.Success()
					: ActionResult.Failed(ActionErrorCodes.NotFound,
						Strings.Actions.StyleWidget.UnknownWidget(widgetId));
			}
			catch (HostInvocationException exception)
			{
				return ActionResult.Failed(ActionErrorCodes.NotConnected, exception.Message);
			}
		}
	}
}
