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

	public string Name => "Play from library";

	public string Description => "Plays a track or playlist from the sample library.";

	public IReadOnlyList<ActionParameter> Parameters { get; } =
	[
		ActionParameter.Choice("kind",
			[
				new ActionParameterOption { Value = nameof(MusicPlayerCatalogItemKind.Track), Label = "Track" },
				new ActionParameterOption { Value = nameof(MusicPlayerCatalogItemKind.Playlist), Label = "Playlist" }
			],
			label: "Kind",
			defaultValue: nameof(MusicPlayerCatalogItemKind.Track),
			required: true),
		ActionParameter.DynamicChoice("item",
			label: "Item",
			description: "Leave empty to pick one on the client that pressed the button."),
		ActionParameter.Toggle("shuffle", label: "Shuffle the playlist")
			// Shuffling a single track means nothing, so the toggle only shows for a playlist.
			.OnlyWhen("kind", nameof(MusicPlayerCatalogItemKind.Playlist))
	];

	public IActionExecutor CreateExecutor() => new Executor(integration);

	public Task<DynamicOptionsResult> GetDynamicOptionsAsync(DynamicOptionsContext context, CancellationToken cancellationToken)
	{
		var kind = ParseKind(context.CurrentParameters.GetValueOrDefault("kind"));
		var items = MusicLibrary.CatalogItems(kind, context.Filter);

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
				context.Interactions?.RequestItemPicker(context.OriginClientId,
					MusicPlayerIntegration.LibraryInstanceId,
					kind,
					prompt: "Pick something to play");

				return Task.FromResult(ActionResult.Accepted("Asked the client to pick an item."));
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
					$"The sample library has no {kind} with id '{itemId}'."));
			}

			var engine = integration.Library.Engine;
			engine.SetShuffle(context.Parameters.GetValueOrDefault("shuffle") is true);
			engine.PlayItem(item);

			return ActionResult.SucceededTask;
		}
	}
}
