using MacroDeck.Localization;
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

	public LocalizedText Name => Strings.Actions.Announce.Name();

	public LocalizedText Description => Strings.Actions.Announce.Description();

	public IReadOnlyList<ActionParameter> Parameters { get; } =
	[
		ActionParameter.Text("title", label: Strings.Actions.Announce.Title.Label(), required: true, maxLength: 60),
		ActionParameter.MultilineText("message",
			label: Strings.Actions.Announce.Message.Label(),
			placeholder: Strings.Actions.Announce.Message.Placeholder()),
		ActionParameter.Choice("level",
			[
				new ActionParameterOption
				{
					Value = nameof(UserNotificationLevel.Info),
					Label = Strings.NotificationLevels.Info()
				},
				new ActionParameterOption
				{
					Value = nameof(UserNotificationLevel.Warning),
					Label = Strings.NotificationLevels.Warning()
				},
				new ActionParameterOption
				{
					Value = nameof(UserNotificationLevel.Error),
					Label = Strings.NotificationLevels.Error()
				}
			],
			label: Strings.Actions.Announce.Level.Label(),
			defaultValue: nameof(UserNotificationLevel.Info)),
		ActionParameter.Text("key",
			label: Strings.Actions.Announce.Key.Label(),
			description: Strings.Actions.Announce.Key.Description())
	];

	public IActionExecutor CreateExecutor() => new Executor(integration);

	private sealed class Executor(ControlRoomIntegration integration) : IActionExecutor
	{
		public Task<ActionResult> ExecuteAsync(ActionExecutionContext context)
		{
			if (integration.Context is not { } integrationContext)
			{
				return Task.FromResult(ActionResult.Failed(ActionErrorCodes.Unavailable,
					Strings.Errors.NotInitialized()));
			}

			if (context.Parameters.GetValueOrDefault("title") is not string { Length: > 0 } title)
			{
				return Task.FromResult(ActionResult.Failed(ActionErrorCodes.InvalidParameter,
					MacroDeckStrings.Validation.Required(Strings.Actions.Announce.Title.Label())));
			}

			// Notifying is fire-and-forget: it never throws, even with no connection, so there is nothing
			// to await and nothing to report back. Title and Message are plain strings on the request -
			// this text is what the user typed, already in its final form.
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
