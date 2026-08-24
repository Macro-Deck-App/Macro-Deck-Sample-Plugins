using MacroDeck.Localization;
using MacroDeck.SampleMusicPlayerPlugin.Player;
using MacroDeck.Sdk.Actions;
using MacroDeck.Sdk.MusicPlayer;

namespace MacroDeck.SampleMusicPlayerPlugin.Actions;

/// <summary>
/// Dynamic options that depend on another parameter: the item list is filtered by the chosen kind,
/// which the host passes back in <see cref="DynamicOptionsContext.CurrentParameters"/>. Leaving the
/// item empty pops the host's own item picker instead of failing.
/// </summary>
internal sealed class PlayCatalogItemAction(MusicPlayerIntegration integration)
	: IActionDefinition, IDynamicOptionsActionDefinition
{
	public string Id => "play-catalog-item";

	public LocalizedText Name => Strings.Actions.PlayCatalogItem.Name();

	public LocalizedText Description => Strings.Actions.PlayCatalogItem.Description();

	public IReadOnlyList<ActionParameter> Parameters { get; } =
	[
		// An option's Value is the wire identity the executor parses back, so it stays the enum name;
		// only its Label is localized.
		ActionParameter.Choice("kind",
			[
				new ActionParameterOption
				{
					Value = nameof(MusicPlayerCatalogItemKind.Track),
					Label = Strings.CatalogKinds.Track()
				},
				new ActionParameterOption
				{
					Value = nameof(MusicPlayerCatalogItemKind.Playlist),
					Label = Strings.CatalogKinds.Playlist()
				}
			],
			label: Strings.Fields.Kind.Label(),
			defaultValue: nameof(MusicPlayerCatalogItemKind.Track),
			required: true),
		ActionParameter.DynamicChoice("item",
			label: Strings.Fields.Item.Label(),
			description: Strings.Actions.PlayCatalogItem.Item.Description()),
		ActionParameter.Toggle("shuffle", label: Strings.Actions.PlayCatalogItem.Shuffle.Label())
			// Shuffling a single track means nothing, so the toggle only shows for a playlist.
			.OnlyWhen("kind", nameof(MusicPlayerCatalogItemKind.Playlist))
	];

	public IActionExecutor CreateExecutor() => new Executor(integration);

	public Task<DynamicOptionsResult> GetDynamicOptionsAsync(DynamicOptionsContext context, CancellationToken cancellationToken)
	{
		var kind = ParseKind(context.CurrentParameters.GetValueOrDefault("kind"));
		var items = MusicLibrary.CatalogItems(kind, context.Filter);

		// A track title is content, not UI text: it is already in its final form and stays a literal.
		return Task.FromResult(new DynamicOptionsResult
		{
			Options = [.. items.Select(item => new ActionParameterOption { Value = item.Id, Label = item.Title })],
			CacheSeconds = 30
		});
	}

	private static MusicPlayerCatalogItemKind ParseKind(object? value)
		=> value is string text && Enum.TryParse<MusicPlayerCatalogItemKind>(text, out var kind)
			? kind
			: MusicPlayerCatalogItemKind.Track;

	private sealed class Executor(MusicPlayerIntegration integration) : IActionExecutor
	{
		public Task<ActionResult> ExecuteAsync(ActionExecutionContext context)
		{
			var kind = ParseKind(context.Parameters.GetValueOrDefault("kind"));

			if (context.Parameters.GetValueOrDefault("item") is not string { Length: > 0 } itemId)
			{
				// A picker request is only accepted while this execution is still running, and it is
				// fire-and-forget: the user's choice arrives as a later execution, not as a return value.
				// The prompt is a plain string because IActionInteractions types it as one.
				context.Interactions?.RequestItemPicker(context.OriginClientId,
					MusicPlayerIntegration.LibraryInstanceId,
					kind,
					prompt: "Pick something to play");

				return Task.FromResult(ActionResult.Accepted(Strings.Actions.PlayCatalogItem.PickerRequested()));
			}

			var item = kind == MusicPlayerCatalogItemKind.Playlist
				? MusicLibrary.FindPlaylist(itemId) is { } playlist
					? new MusicPlayerCatalogItem(playlist.Id, playlist.Title, MusicPlayerCatalogItemKind.Playlist)
					: null
				: MusicLibrary.FindTrack(itemId) is { } track
					? new MusicPlayerCatalogItem(track.Id, track.Title, MusicPlayerCatalogItemKind.Track)
					: null;

			if (item is null)
			{
				return Task.FromResult(ActionResult.Failed(ActionErrorCodes.NotFound,
					Strings.Errors.CatalogItemNotFound(itemId)));
			}

			var engine = integration.Library.Engine;
			engine.SetShuffle(context.Parameters.GetValueOrDefault("shuffle") is true);
			engine.PlayItem(item);

			return ActionResult.SucceededTask;
		}
	}
}
