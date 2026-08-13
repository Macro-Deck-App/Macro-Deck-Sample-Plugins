using MacroDeck.Plugin.Hosting.Transport;
using MacroDeck.Sdk.Actions;

namespace MacroDeck.SampleVirtualProfilePlugin.Actions;

/// <summary>
/// Drives the deck of the client that pressed the button, through <c>IDeckNavigator</c>. The target
/// field only appears for the two kinds that need one, and its options come from the same host-pushed
/// cache <c>GetFolders()</c>/<c>GetProfiles()</c> read.
/// </summary>
internal sealed class NavigateDeckAction(ControlRoomIntegration integration)
	: IActionDefinition, IDynamicOptionsActionDefinition
{
	private const string FolderKind = "folder";
	private const string ProfileKind = "profile";
	private const string ParentKind = "parent";
	private const string BackKind = "back";

	public string Id => "navigate-deck";

	public string Name => "Navigate deck";

	public string Description => "Opens a folder or profile on the client that triggered the action.";

	public IReadOnlyList<ActionParameter> Parameters { get; } =
	[
		ActionParameter.Choice("kind",
			[
				new ActionParameterOption { Value = FolderKind, Label = "Open folder" },
				new ActionParameterOption { Value = ProfileKind, Label = "Switch profile" },
				new ActionParameterOption { Value = ParentKind, Label = "Go to parent" },
				new ActionParameterOption { Value = BackKind, Label = "Go back" }
			],
			label: "Target",
			defaultValue: FolderKind,
			required: true),
		ActionParameter.DynamicChoice("targetId", label: "Folder or profile")
			.OnlyWhen("kind", FolderKind, ProfileKind)
	];

	public IActionExecutor CreateExecutor() => new Executor(integration);

	public Task<DynamicOptionsResult> GetDynamicOptionsAsync(DynamicOptionsContext context, CancellationToken cancellationToken)
	{
		var deck = integration.Context?.Deck;
		var options = context.CurrentParameters.GetValueOrDefault("kind") as string == ProfileKind
			? deck?.GetProfiles().Select(profile => new ActionParameterOption { Value = profile.Id, Label = profile.Label })
			: deck?.GetFolders().Select(folder => new ActionParameterOption { Value = folder.Id, Label = folder.Label });

		return Task.FromResult(new DynamicOptionsResult { Options = [.. options ?? []] });
	}

	private sealed class Executor(ControlRoomIntegration integration) : IActionExecutor
	{
		public async Task<ActionResult> ExecuteAsync(ActionExecutionContext context)
		{
			if (integration.Context is not { } integrationContext)
			{
				return ActionResult.Failed(ActionErrorCodes.Unavailable, "The integration is not initialized.");
			}

			var kind = context.Parameters.GetValueOrDefault("kind") as string ?? FolderKind;
			var targetId = context.Parameters.GetValueOrDefault("targetId") as string;

			if (kind is FolderKind or ProfileKind && string.IsNullOrWhiteSpace(targetId))
			{
				return ActionResult.Failed(ActionErrorCodes.InvalidParameter, "targetId is required for this target.");
			}

			try
			{
				// OriginClientId names the client that pressed the button; navigating without it would move
				// every connected deck.
				var deck = integrationContext.Deck;
				await (kind switch
				{
					FolderKind => deck.ChangeFolderAsync(targetId!, context.OriginClientId, context.CancellationToken),
					ProfileKind => deck.ChangeProfileAsync(targetId!, context.OriginClientId, context.CancellationToken),
					ParentKind => deck.GoToParentAsync(context.OriginClientId, context.CancellationToken),
					_ => deck.GoBackAsync(context.OriginClientId, context.CancellationToken)
				});

				return ActionResult.Success();
			}
			catch (HostInvocationException exception)
			{
				return ActionResult.Failed(ActionErrorCodes.NotConnected, exception.Message);
			}
		}
	}
}
