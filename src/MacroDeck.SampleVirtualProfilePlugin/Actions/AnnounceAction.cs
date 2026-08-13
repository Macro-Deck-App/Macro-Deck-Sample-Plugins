using MacroDeck.Sdk.Actions;
using MacroDeck.Sdk.Notifications;

namespace MacroDeck.SampleVirtualProfilePlugin.Actions;

/// <summary>
/// Notifies the user through <c>IUserNotifier</c>. Notifying under a key replaces the previous
/// notification with that key instead of stacking a new one, which is also how a plugin expresses
/// progress - there is no separate progress contract.
/// </summary>
internal sealed class AnnounceAction(ControlRoomIntegration integration) : IActionDefinition
{
	public string Id => "announce";

	public string Name => "Announce";

	public string Description => "Shows a notification in the Macro Deck app.";

	public IReadOnlyList<ActionParameter> Parameters { get; } =
	[
		ActionParameter.Text("title", label: "Title", required: true, maxLength: 60),
		ActionParameter.MultilineText("message", label: "Message", placeholder: "Optional details"),
		ActionParameter.Choice("level",
			[
				new ActionParameterOption { Value = nameof(UserNotificationLevel.Info), Label = "Info" },
				new ActionParameterOption { Value = nameof(UserNotificationLevel.Warning), Label = "Warning" },
				new ActionParameterOption { Value = nameof(UserNotificationLevel.Error), Label = "Error" }
			],
			label: "Level",
			defaultValue: nameof(UserNotificationLevel.Info)),
		ActionParameter.Text("key",
			label: "Replace key",
			description: "Notifications sharing a key replace each other instead of piling up.")
	];

	public IActionExecutor CreateExecutor() => new Executor(integration);

	private sealed class Executor(ControlRoomIntegration integration) : IActionExecutor
	{
		public Task<ActionResult> ExecuteAsync(ActionExecutionContext context)
		{
			if (integration.Context is not { } integrationContext)
			{
				return Task.FromResult(ActionResult.Failed(ActionErrorCodes.Unavailable, "The integration is not initialized."));
			}

			if (context.Parameters.GetValueOrDefault("title") is not string { Length: > 0 } title)
			{
				return Task.FromResult(ActionResult.Failed(ActionErrorCodes.InvalidParameter, "title is required."));
			}

			// Notifying is fire-and-forget: it never throws, even with no connection, so there is nothing
			// to await and nothing to report back.
			integrationContext.Notifications.Notify(new UserNotificationRequest
			{
				Title = title,
				Message = context.Parameters.GetValueOrDefault("message") as string,
				Level = Enum.TryParse<UserNotificationLevel>(context.Parameters.GetValueOrDefault("level") as string, out var level)
					? level
					: UserNotificationLevel.Info,
				Key = context.Parameters.GetValueOrDefault("key") as string
			});

			return ActionResult.SucceededTask;
		}
	}
}
