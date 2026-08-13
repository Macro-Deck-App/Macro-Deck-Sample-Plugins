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

	public string Name => "Style widget";

	public string Description => "Applies a label and colours to a widget, or resets them.";

	public IReadOnlyList<ActionParameter> Parameters { get; } =
	[
		ActionParameter.WidgetTarget("widget", label: "Widget"),
		ActionParameter.Text("label", label: "Label", maxLength: 40),
		ActionParameter.Color("backgroundColor", label: "Background", supportsReset: true),
		ActionParameter.Icon("icon", label: "Icon"),
		ActionParameter.Choice("state",
			[
				new ActionParameterOption { Value = nameof(WidgetStateSelector.Current), Label = "Current state" },
				new ActionParameterOption { Value = nameof(WidgetStateSelector.On), Label = "On state" },
				new ActionParameterOption { Value = nameof(WidgetStateSelector.Off), Label = "Off state" },
				new ActionParameterOption { Value = nameof(WidgetStateSelector.Both), Label = "Both states" }
			],
			label: "Apply to",
			defaultValue: nameof(WidgetStateSelector.Current))
	];

	public IActionExecutor CreateExecutor() => new Executor(integration);

	private sealed class Executor(ControlRoomIntegration integration) : IActionExecutor
	{
		public async Task<ActionResult> ExecuteAsync(ActionExecutionContext context)
		{
			if (integration.Context is not { } integrationContext)
			{
				return ActionResult.Failed(ActionErrorCodes.Unavailable, "The integration is not initialized.");
			}

			var target = context.Parameters.GetValueOrDefault("widget") as string;
			var widgetId = WidgetTargets.IsSelf(target) || string.IsNullOrWhiteSpace(target)
				? context.OwnerWidgetId
				: target;

			if (widgetId is null)
			{
				// $self only resolves for a widget-triggered run; a script or an automation has no owner.
				return ActionResult.Failed(ActionErrorCodes.InvalidParameter,
					"Pick a widget: this run has no widget of its own to style.");
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
					: ActionResult.Failed(ActionErrorCodes.NotFound, $"The host does not know widget '{widgetId}'.");
			}
			catch (HostInvocationException exception)
			{
				return ActionResult.Failed(ActionErrorCodes.NotConnected, exception.Message);
			}
		}
	}
}
