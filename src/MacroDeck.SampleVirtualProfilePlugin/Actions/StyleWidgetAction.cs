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
internal sealed class StyleWidgetAction(ControlRoomIntegration integration) : IActionDefinition
{
	public string Id => "style-widget";

	public LocalizedText Name => Strings.Actions.StyleWidget.Name();

	public LocalizedText Description => Strings.Actions.StyleWidget.Description();

	public IReadOnlyList<ActionParameter> Parameters { get; } =
	[
		ActionParameter.WidgetTarget("widget", label: Strings.Actions.StyleWidget.Widget.Label()),
		ActionParameter.Text("label", label: Strings.Actions.StyleWidget.LabelText.Label(), maxLength: 40),
		ActionParameter.Color("backgroundColor", label: Strings.Actions.StyleWidget.Background.Label(), supportsReset: true),
		ActionParameter.Icon("icon", label: Strings.Actions.StyleWidget.Icon.Label()),
		ActionParameter.Choice("state",
			[
				new ActionParameterOption
				{
					Value = nameof(WidgetStateSelector.Current),
					Label = Strings.WidgetStates.Current()
				},
				new ActionParameterOption { Value = nameof(WidgetStateSelector.On), Label = Strings.WidgetStates.On() },
				new ActionParameterOption { Value = nameof(WidgetStateSelector.Off), Label = Strings.WidgetStates.Off() },
				new ActionParameterOption { Value = nameof(WidgetStateSelector.Both), Label = Strings.WidgetStates.Both() }
			],
			label: Strings.Actions.StyleWidget.State.Label(),
			defaultValue: nameof(WidgetStateSelector.Current))
	];

	public IActionExecutor CreateExecutor() => new Executor(integration);

	private sealed class Executor(ControlRoomIntegration integration) : IActionExecutor
	{
		public async Task<ActionResult> ExecuteAsync(ActionExecutionContext context)
		{
			if (integration.Context is not { } integrationContext)
			{
				return ActionResult.Failed(ActionErrorCodes.Unavailable, Strings.Errors.NotInitialized());
			}

			var target = context.Parameters.GetValueOrDefault("widget") as string;
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

			var request = new WidgetAppearanceRequest
			{
				WidgetId = widgetId,
				State = Enum.TryParse<WidgetStateSelector>(context.Parameters.GetValueOrDefault("state") as string, out var state)
					? state
					: WidgetStateSelector.Current,
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
